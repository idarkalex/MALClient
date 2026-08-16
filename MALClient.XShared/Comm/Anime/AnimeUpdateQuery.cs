using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MALClient.Adapters;
using MALClient.Models.Enums;
using MALClient.Models.Models.Library;
using MALClient.XShared.Utils;
using MALClient.XShared.Utils.Managers;
using MALClient.XShared.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MALClient.XShared.Comm.Anime
{
    public class AnimeUpdateQuery : Query
    {
        private readonly int? _rewatched;
        private readonly IAnimeData _item;
        public static bool SuppressOfflineSync { get; set; }
        public static bool UpdatedSomething { get; set; } //used for data saving on suspending in app.xaml.cs
        private static SemaphoreSlim _updateSemaphore = new SemaphoreSlim(1);

        /// <summary>
        /// Just send rewatched value witch cannot be retrieved back
        /// </summary>
        /// <param name="item"></param>
        /// <param name="rewatched"></param>
        public AnimeUpdateQuery(IAnimeData item, int? rewatched)
        {
            _item = item;
            _rewatched = rewatched;
        }



        public AnimeUpdateQuery(IAnimeData item)
            : this(item.Id, item.MyEpisodes, (int)item.MyStatus, item.MyScore, item.StartDate, item.EndDate, item.Notes,item.IsRewatching)
        {
            _item = item;
            try
            {
                ResourceLocator.LiveTilesManager.UpdateTile(item);
            }
            catch (Exception)
            {
                //not windows
            }
        }


        private AnimeUpdateQuery(int id, int watchedEps, int myStatus, float myScore, string startDate, string endDate, string notes,bool rewatching)
        {
            UpdatedSomething = true;
        }

        public override async Task<string> GetRequestResponse()
        {

            try
            {
                await _updateSemaphore.WaitAsync();
                var result = "";
                try
                {
                    var client = await ResourceLocator.MalHttpContextProvider.GetApiHttpContextAsync();

                    var dateStart = Utilities.FormatMalDate(_item.StartDate);
                    var dateEnd = Utilities.FormatMalDate(_item.EndDate);

                    var data = new List<KeyValuePair<string, string>>
                    {
                        new("status", Utilities.StatusToApiParam(_item.MyStatus)),
                        new("is_rewatching", _item.IsRewatching.ToString().ToLower()),
                        new("score", _item.MyScore.ToString()),
                        new("num_watched_episodes", _item.MyEpisodes.ToString()),
                        new("priority", ((int) _item.Priority).ToString()),
                        new("tags", _item.Notes),
                    };

                    if (_rewatched != null)
                    {
                        data.Add(new KeyValuePair<string, string>("num_times_rewatched", _rewatched.Value.ToString()));
                    }

                    if (dateStart != null)
                    {
                        data.Add(new KeyValuePair<string, string>("start_date", dateStart));
                    }      
                    
                    if (dateEnd != null)
                    {
                        data.Add(new KeyValuePair<string, string>("finish_date", dateEnd));
                    }

                    using var content = new FormUrlEncodedContent(data);
                    await Task.Delay(TimeSpan.FromMilliseconds(300));
                    var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod("PUT"),
                        $"https://api.myanimelist.net/v2/anime/{_item.Id}/my_list_status") {Content = content});

                    response.EnsureSuccessStatusCode();

                    if (response.IsSuccessStatusCode)
                        result = "Updated";

                }
                catch (Exception e)
                {
                    ResourceLocator.SnackbarProvider.ShowText("Failed to send update to MAL. Please try signing in again if problem persists.");
                }

                if (string.IsNullOrEmpty(result) && !SuppressOfflineSync && Settings.EnableOfflineSync)
                {
                    result = "Updated";
                    Settings.AnimeSyncRequired = true;
                }

                ResourceLocator.ApplicationDataService[RoamingDataTypes.LastLibraryUpdate] = DateTime.Now.ToBinary();
                return result;
            }
            finally
            {
                _updateSemaphore.Release();
            }
        }

        public override string SnackbarMessageOnFail => "Your changes will be synced with MAL on next app launch when online.";
    }
}

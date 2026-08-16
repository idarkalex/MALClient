using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MALClient.Models.Models.Library;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;

namespace MALClient.XShared.Comm.Manga
{
    public class MangaUpdateQuery : Query
    {
        private readonly IAnimeData _item;
        private SemaphoreSlim _updateSemaphore = new SemaphoreSlim(1);
        public static bool SuppressOfflineSync { get; set; }
        public static bool UpdatedSomething { get; set; } //used for data saving on suspending in app.xaml.cs

        /// <summary>
        /// Just send rewatched value witch cannot be retrieved back
        /// </summary>
        /// <param name="item"></param>
        /// <param name="rewatched"></param>
        public MangaUpdateQuery(IAnimeData item, int rewatched)
        {
            _item = item;
        }

        public MangaUpdateQuery(IAnimeData item)
            : this(
                item.Id, item.MyEpisodes, (int)item.MyStatus, (int) item.MyScore, item.MyVolumes, item.StartDate,
                item.EndDate,item.Notes,item.IsRewatching)
        {
            _item = item;
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
                        new("status", Utilities.StatusToApiParam(_item.MyStatus, true)),
                        new("is_rereading", _item.IsRewatching.ToString().ToLower()),
                        new("score", _item.MyScore.ToString()),
                        new("num_chapters_read", _item.MyEpisodes.ToString()),
                        new("num_volumes_read", _item.MyVolumes.ToString()),
                        new("priority", ((int) _item.Priority).ToString()),
                        new("tags", _item.Notes),
                    };

                    if (dateStart != null)
                    {
                        data.Add(new KeyValuePair<string, string>("start_date", dateStart));
                    }

                    if (dateEnd != null)
                    {
                        data.Add(new KeyValuePair<string, string>("finish_date", dateEnd));
                    }

                    using var content = new FormUrlEncodedContent(data);
                    var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod("PUT"),
                            $"https://api.myanimelist.net/v2/manga/{_item.Id}/my_list_status")
                        { Content = content });

                    if (response.IsSuccessStatusCode)
                        result = "Updated";
                }
                catch (Exception e)
                {
#if ANDROID
                ResourceLocator.SnackbarProvider.ShowText("Failed to send update to MAL.");
#endif
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

        private MangaUpdateQuery(int id, int watchedEps, int myStatus, int myScore, int myVol, string startDate,
            string endDate,string notes,bool rereading)
        {
            UpdatedSomething = true;
        }
    }
}

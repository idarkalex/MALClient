using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using MALClient.Models.Enums;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;

namespace MALClient.XShared.Comm.Manga
{
    public class MangaAddQuery : Query
    {
        private readonly string _id;

        public MangaAddQuery(string id)
        {
            _id = id;
            MangaUpdateQuery.UpdatedSomething = true;
        }


        public override async Task<string> GetRequestResponse()
        {
            try
            {
                var client = await ResourceLocator.MalHttpContextProvider.GetApiHttpContextAsync();
                var data = new List<KeyValuePair<string, string>>
                {
                    new("status", Utilities.StatusToApiParam(AnimeStatus.PlanToWatch, true)),
                };

                if (Settings.SetStartDateOnListAdd)
                    data.Add(new("start_date", DateTime.Now.ToString("yyyy-MM-dd")));

                using var content = new FormUrlEncodedContent(data);
                var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod("PUT"),
                        $"https://api.myanimelist.net/v2/manga/{_id}/my_list_status")
                    { Content = content });

                if (response.IsSuccessStatusCode)
                    return "Created";

            }
            catch (Exception e)
            {
                ResourceLocator.TelemetryProvider.TrackException(e);
            }

            return "";
        }
    }
}
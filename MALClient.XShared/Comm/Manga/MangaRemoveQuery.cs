using System;
using System.Net.Http;
using System.Threading.Tasks;
using MALClient.XShared.ViewModels;

namespace MALClient.XShared.Comm.Manga
{
    public class MangaRemoveQuery : Query
    {
        private readonly string _id;

        public MangaRemoveQuery(string id)
        {
            _id = id;
            MangaUpdateQuery.UpdatedSomething = true;
        }

        public override async Task<string> GetRequestResponse()
        {
            try
            {
                var client = await ResourceLocator.MalHttpContextProvider.GetApiHttpContextAsync();

                var response =
                    await client.DeleteAsync($"https://api.myanimelist.net/v2/manga/{_id}/my_list_status");

                if (response.IsSuccessStatusCode)
                    return "Updated";
            }
            catch (Exception e)
            {

            }

            return "";
        }
    }
}
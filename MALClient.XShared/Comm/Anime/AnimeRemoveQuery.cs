using System;
using System.Net.Http;
using System.Threading.Tasks;
using MALClient.XShared.ViewModels;

namespace MALClient.XShared.Comm.Anime
{
    public class AnimeRemoveQuery : Query
    {
        private readonly string _id;

        public AnimeRemoveQuery(string id)
        {
            _id = id;
            AnimeUpdateQuery.UpdatedSomething = true;
        }

        public override async Task<string> GetRequestResponse()
        {         
            try
            {
                var client = await ResourceLocator.MalHttpContextProvider.GetApiHttpContextAsync();

                var response =
                    await client.DeleteAsync($"https://api.myanimelist.net/v2/anime/{_id}/my_list_status");

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
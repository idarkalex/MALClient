using System;
using System.Threading.Tasks;
using MALClient.XShared.ViewModels;

namespace MALClient.XShared.Comm
{
    public class AuthQuery : Query
    {
        public override async Task<string> GetRequestResponse()
        {
            try
            {
                ResourceLocator.MalHttpContextProvider.Invalidate();
                var client = await ResourceLocator.MalHttpContextProvider.GetHttpContextAsync(true);
                if (client.Disabled)
                    return null;
                return "ok";
            }
            catch (Exception e)
            {
                return null;
            }         
        }
    }
}
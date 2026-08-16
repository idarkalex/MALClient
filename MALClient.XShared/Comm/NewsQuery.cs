using System;

namespace MALClient.XShared.Comm
{
    public class NewsQuery : Query
    {
        public NewsQuery()
        {
            Request =
                new Uri(
                    Uri.EscapeUriString("https://raw.githubusercontent.com/Mordonus/MALClient/master/NEWS.json"));
        }
    }
}
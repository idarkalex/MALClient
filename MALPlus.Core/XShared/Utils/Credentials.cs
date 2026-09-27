using System;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using MALClient.Adapters;
using MALClient.Adapters.Credentials;
using MALClient.Models.AdapterModels;
using MALClient.Models.Enums;
using MALClient.XShared.Comm;
using MALClient.XShared.ViewModels;

namespace MALClient.XShared.Utils
{
    public static class Credentials
    {
        public static readonly IApplicationDataService ApplicationDataService;
        public static readonly IPasswordVault PasswordVault;
        private static string _userName;

        static Credentials()
        {
            ApplicationDataService = ResourceLocator.ApplicationDataService;
            PasswordVault = ResourceLocator.PasswordVaultProvider;

            HummingbirdToken = (string)(ApplicationDataService["HummingbirdToken"] ?? "");
            Id = (int)(ApplicationDataService["UserId"] ?? 0);
            // This used to be bool.Parse(ApplicationDataService["Auth"] as string ?? "False"),
            // but the data service hands back a parsed bool, so "as string" was always null
            // and the app came up unauthenticated on every cold start no matter what the
            // token store held.
            Authenticated = ReadBool(ApplicationDataService["Auth"]);
        }

        private static bool ReadBool(object raw)
        {
            switch (raw)
            {
                case null:
                    return false;
                case bool value:
                    return value;
                case string text:
                    return bool.TryParse(text, out var parsed) && parsed;
                default:
                    return false;
            }
        }

        public static string HummingbirdToken { get; private set; }


        public static string UserName
        {
            get { return _userName; }
            set { _userName = value?.Trim(); }
        }

        public static string Password { get; set; }

        public static int Id { get; private set; }


        public static bool Authenticated
        {
            get;
            set;
        }

        internal static ICredentials GetHttpCreditentials()
        {
            return new NetworkCredential(UserName, Password);
        }

        public static void Update(string name, string passwd, ApiType type)
        {
            PasswordVault.Reset();

            UserName = name;
            Password = passwd;

            Query.RefreshClientAuthHeader();

            if (!string.IsNullOrWhiteSpace(passwd))
                PasswordVault.Add(new VaultCredential((type == ApiType.Mal ? "MALPlus" : "MALPlusHum"), UserName, Password));
        }

        public static void Reset()
        {
            PasswordVault.Reset();
            SetAuthStatus(false);
            SetAuthToken("");
            UserName = Password = string.Empty;
            SetAuthStatus(false);
        }

        public static void SetAuthStatus(bool status)
        {
            Authenticated = status;
            ApplicationDataService["Auth"] = status.ToString();
            ViewModelLocator.GeneralHamburger.UpdateLogInLabel();
        }

        public static void SetId(int id)
        {
            ApplicationDataService["UserId"] = id;
            Id = id;
        }

        public static void SetAuthToken(string token)
        {
            var trimmedToken = token == "" ? "" : token.Substring(1, token.Length - 2);
            ApplicationDataService["HummingbirdToken"] = trimmedToken;
            HummingbirdToken = trimmedToken;
        }

        public static void Init()
        {
            try
            {
                var deductedApiType = ApiType.Mal;
                VaultCredential credential = null;
                try
                {
                    credential = PasswordVault.Get("MALPlus");
                }
                catch (Exception)
                {
                    credential = PasswordVault.Get("MALPlusHum");
                    deductedApiType = ApiType.Hummingbird;
                }
                if (credential != null)
                {
                    Settings.SelectedApiType = deductedApiType;
                    UserName = credential.UserName;
                    Password = credential.Password;
                    Authenticated = true;
                    if ((Settings.SelectedApiType == ApiType.Mal &&
                        ApplicationDataService["UserId"] == null) ||
                        (Settings.SelectedApiType == ApiType.Hummingbird &&
                        string.IsNullOrEmpty(ApplicationDataService["HummingbirdToken"] as string)))
                        //we have credentials without Id
                        FillInMissingIdData();
                }
                else
                    Authenticated = false;
            }
            catch (Exception)
            {
                Authenticated = false;
            }
        }

        /// <summary>
        /// The vault is the nice path, but it is not the only one: a refresh token survives
        /// in the preferences even when the vault write was dropped or the keystore entry
        /// went away. Rehydrate the session from it so a cold start does not dump the user
        /// back on the login form every single time.
        /// </summary>
        public static async Task<bool> TryRestoreSessionAsync()
        {
            if (Authenticated && !string.IsNullOrWhiteSpace(UserName))
                return true;
            if (string.IsNullOrWhiteSpace(Settings.RefreshToken) || string.IsNullOrWhiteSpace(Settings.ApiToken))
                return false;
            try
            {
                // Refreshing also proves the token is still good.
                var client = await ResourceLocator.MalHttpContextProvider.GetApiHttpContextAsync();
                if (client == null)
                    return false;
                var profile = await client.GetStringAsync("https://api.myanimelist.net/v2/users/@me");
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(profile);
                if (data == null)
                    return false;
                UserName = (string)data["name"];
                var id = (int?)data["id"] ?? 0;
                if (!string.IsNullOrWhiteSpace(UserName) && id > 0)
                {
                    SetId(id);
                    Settings.SelectedApiType = ApiType.Mal;
                    SetAuthStatus(true);
                    ViewModelLocator.AnimeList.ListSource = UserName;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("MALPLUS session restore failed: " + ex.GetType().Name + " " + ex.Message);
            }
            return false;
        }

        /// <summary>
        /// The MAL website session cookies. They are what the community endpoints (forums, clubs,
        /// wall) authenticate with, and they only ever lived in memory after a sign in, so every
        /// cold start used to come back with a working official api and a completely dead forum
        /// section. Restored from the same vault entry that holds them at login time.
        /// </summary>
        public static bool TryRestoreWebSession()
        {
            if (!string.IsNullOrWhiteSpace(Password))
                return true;

            foreach (var domain in new[] {"MALPlus", "MALPlusHum"})
            {
                try
                {
                    var credential = PasswordVault.Get(domain);
                    if (credential == null || string.IsNullOrWhiteSpace(credential.Password))
                        continue;
                    if (string.IsNullOrWhiteSpace(UserName))
                        UserName = credential.UserName;
                    Password = credential.Password;
                    Console.WriteLine("MALPLUS web session restored for " + domain);
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("MALPLUS web session restore failed for " + domain + ": " + ex.GetType().Name);
                }
            }
            return false;
        }

        private static async void FillInMissingIdData()
        {
            try
            {
                string response = null;
                switch (Settings.SelectedApiType)
                {
                    case ApiType.Mal:
                        response = await new AuthQuery().GetRequestResponse();
                        if (string.IsNullOrEmpty(response))
                            throw new Exception();
                        var doc = XDocument.Parse(response);
                        SetId(int.Parse(doc.Element("user").Element("id").Value));
                        break;
                    case ApiType.Hummingbird:
                        response = await new AuthQuery().GetRequestResponse();
                        if (string.IsNullOrEmpty(response))
                            throw new Exception();
                        if (response.Contains("\"error\": \"Invalid credentials\""))
                            throw new Exception();
                        SetAuthToken(response);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception)
            {
                Authenticated = false;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MALClient.Adapters;
using MALClient.Models.Enums;
using MALClient.Models.Models;
using MALClient.Models.Models.Anime;
using MALClient.Models.Models.AnimeScrapped;
using MALClient.Models.Models.Library;
using MALClient.Models.Models.MalSpecific;
using MALClient.Models.Models.Misc;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.ViewModels;
using Newtonsoft.Json;

//Okay it's big copy paste... feel free to laugh

namespace MALClient.XShared.Utils
{
    /// <summary>
    ///     Contains stuff like GlobalScore and air date
    /// </summary>
    public class VolatileDataCache
    {
        public float GlobalScore { get; set; }
        public int DayOfAiring { get; set; }
        public ExactAiringTimeData ExactAiringTime { get; set; }
        public DateTime? LastFailedAiringTimeFetchAttempt { get; set; }
        public List<string> Genres { get; set; }
        public string AirStartDate { get; set; }
        public string TimeTillNextAir { get; set; }
        public DateTime? NextAirUtc { get; set; }
        public DateTime? NextAirFetchedAtUtc { get; set; }
        public string LastKnownStatus { get; set; }
    }

    public static class DataCache
    {
        private static readonly IDataCache DataCacheService;
        private static readonly IApplicationDataService ApplicationDataService;
        private static readonly IConnectionInfoProvider ConnectionInfoProvider;

        static DataCache()
        {
            DataCacheService = ResourceLocator.DataCacheService;
            ApplicationDataService = ResourceLocator.ApplicationDataService;
            ConnectionInfoProvider = ResourceLocator.ConnectionInfoProvider;
            LoadVolatileData();
            RetrieveHumMalIdDictionary();      
        }

        public static async Task ClearApiRelatedCache()
        {
			await DataCacheService.ClearApiRelatedCache();
            _volatileDataCache.Clear();
        }

        public static async Task ClearAnimeListData()
        {
            await DataCacheService.ClearAnimeListData();
        }

        public static async Task SaveDataRoaming<T>(T data, string filename)
        {
            try
            {
                await DataCacheService.SaveDataRoaming(data, filename);
            }
            catch (Exception e)
            {
                //magic
            }
        }

        public static async Task SaveData<T>(T data, string filename, string targetFolder)
        {
            await DataCacheService.SaveData(data, filename,targetFolder);
        }

        public static async Task<T> RetrieveData<T>(string filename, string originFolder, int expiration)
        {
            return await DataCacheService.RetrieveData<T>(filename, originFolder, expiration);
        }

        public static async Task<T> RetrieveDataRoaming<T>(string filename,int expiration)
        {
            try
            {
                return await DataCacheService.RetrieveDataRoaming<T>(filename, expiration);
            }
            catch (Exception)
            {
                //No file
            }
            return default(T);
        }

        #region UserData

        public static async Task SaveDataForUser(string user, IEnumerable<ILibraryData> data, AnimeListWorkModes mode)
        {
            if (!Settings.IsCachingEnabled)
                return;
            try
            {
                if (mode == AnimeListWorkModes.Anime)
                {
                    await DataCacheService.SaveData(
                        new Tuple<DateTime, IEnumerable<AnimeLibraryItemData>>(DateTime.Now,
                            data.Select(item => item as AnimeLibraryItemData)),
                        $"{(mode == AnimeListWorkModes.Anime ? "anime" : "manga")}_data_{user.ToLower()}.json", "");
                }
                else
                {
                    await DataCacheService.SaveData(
                        new Tuple<DateTime, IEnumerable<MangaLibraryItemData>>(DateTime.Now,
                            data.Select(item => item as MangaLibraryItemData)),
                        $"{(mode == AnimeListWorkModes.Anime ? "anime" : "manga")}_data_{user.ToLower()}.json", "");
                }
            }
            catch (Exception)
            {
                //
            }
        }

        public static async Task<List<ILibraryData>> RetrieveDataForUser(string user, AnimeListWorkModes mode)
        {
            if (!Settings.IsCachingEnabled)
                return null;
            try
            {
                var decoded = new List<ILibraryData>();
                if (mode == AnimeListWorkModes.Anime)
                {
                    var jsonObj =
                        await
                            DataCacheService.RetrieveData<Tuple<DateTime, List<AnimeLibraryItemData>>>(
                                $"{(mode == AnimeListWorkModes.Anime ? "anime" : "manga")}_data_{user.ToLower()}.json",
                                "", 0);
                    if (!CheckForOldData(jsonObj.Item1))
                    {
                        return null;
                    }
                    decoded.AddRange(jsonObj.Item2.Select(item => item as ILibraryData));
                }
                else
                {
                    var jsonObj =
                        await
                            DataCacheService.RetrieveData<Tuple<DateTime, List<MangaLibraryItemData>>>(
                                $"{(mode == AnimeListWorkModes.Anime ? "anime" : "manga")}_data_{user.ToLower()}.json",
                                "", 0);
                    if (!CheckForOldData(jsonObj.Item1))
                    {
                        return null;
                    }
                    decoded.AddRange(jsonObj.Item2.Select(item => item as ILibraryData));
                }

                return decoded;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool CheckForOldData(DateTime timestamp)
        {
            if (!ConnectionInfoProvider.HasInternetConnection)
                return true;

            var diff = DateTime.Now.ToUniversalTime().Subtract(timestamp);
            var roamingUpdate = ApplicationDataService[RoamingDataTypes.LastLibraryUpdate] as long?;
            if (diff.TotalSeconds > Settings.CachePersitence || (roamingUpdate != null && timestamp < DateTime.FromBinary(roamingUpdate.Value)))
                return false;
            return true;
        }

        #endregion

        #region SeasonData

        public static async void SaveSeasonalData(List<SeasonalAnimeData> data, string tag)
        {
            try
            {
                await Task.Run(async () =>
                {
                    await
                        DataCacheService.SaveData(data,
                            $"seasonal_data{tag}.json", "");
                });
            }
            catch (Exception)
            {
                // file replace exception?
            }
        }

        public static async Task<List<SeasonalAnimeData>> RetrieveSeasonalData(string tag)
        {
            try
            {
                return
                    await
                        DataCacheService.RetrieveData<List<SeasonalAnimeData>>(
                            $"seasonal_data{tag}.json", "", 7);
            }
            catch (Exception)
            {
                //No file
            }
            return null;
        }

        #endregion

        #region VolatileData

        private static readonly Dictionary<int, VolatileDataCache> _volatileDataCache = new Dictionary<int, VolatileDataCache>();

        private static async void LoadVolatileData()
        {
            try
            {
                var loaded = await DataCacheService.RetrieveData<Dictionary<int, VolatileDataCache>>("volatile_data.json","",0);
                if (loaded == null)
                    return;
                foreach (var kvp in loaded)
                    _volatileDataCache[kvp.Key] = kvp.Value;
            }
            catch (Exception)
            {
            }
        }

        public static async Task SaveVolatileData()
        {
            try
            {
                await DataCacheService.SaveData(_volatileDataCache, "volatile_data.json", "");
            }
            catch (Exception)
            {
                //ignored
            }
        }

        public static void RegisterVolatileData(int id, VolatileDataCache data)
        {
            if (_volatileDataCache.ContainsKey(id))
            {
                //We don't want to lose data here , only anime from seasonal contains genres data.
                if (data.Genres != null && data.Genres.Count > 0)
                    _volatileDataCache[id].Genres = data.Genres;
                _volatileDataCache[id].DayOfAiring = data.DayOfAiring;
                _volatileDataCache[id].GlobalScore = data.GlobalScore;
                _volatileDataCache[id].AirStartDate = data.AirStartDate;
            }
            else
                _volatileDataCache[id] = data;
        }

        public static void UpdateVolatileDataWithExactDate(int id, ExactAiringTimeData data)
        {
            if (_volatileDataCache.ContainsKey(id))
            {
                _volatileDataCache[id].ExactAiringTime = data;
                _volatileDataCache[id].LastFailedAiringTimeFetchAttempt = null;
            }
        }

        public static void UpdateVolatileDataAirDay(int id, int day)
        {
            if (_volatileDataCache.ContainsKey(id))
            {
                _volatileDataCache[id].DayOfAiring = day;
            }
        }

        public static void UpdateVolatileDataWithTimeTillNextAir(int id, string timeTillNextAir)
        {
            if (_volatileDataCache.ContainsKey(id))
            {
                _volatileDataCache[id].TimeTillNextAir = timeTillNextAir;
            }
            else
            {
                _volatileDataCache[id] = new VolatileDataCache { TimeTillNextAir = timeTillNextAir };
            }
        }

        public static void UpdateVolatileDataWithNextAir(int id, DateTime? nextAirUtc)
        {
            if (_volatileDataCache.ContainsKey(id))
            {
                _volatileDataCache[id].NextAirUtc = nextAirUtc;
                _volatileDataCache[id].NextAirFetchedAtUtc = DateTime.UtcNow;
                if (!nextAirUtc.HasValue)
                    _volatileDataCache[id].TimeTillNextAir = "";
            }
            else
            {
                _volatileDataCache[id] = new VolatileDataCache
                {
                    NextAirUtc = nextAirUtc,
                    NextAirFetchedAtUtc = DateTime.UtcNow
                };
            }
        }

        public static void UpdateVolatileStatus(int id, string status)
        {
            if (_volatileDataCache.ContainsKey(id))
                _volatileDataCache[id].LastKnownStatus = status;
            else
                _volatileDataCache[id] = new VolatileDataCache { LastKnownStatus = status };
            if (!string.IsNullOrEmpty(status) && !AirTimeUtils.IsCurrentlyAiringStatus(status))
            {
                _volatileDataCache[id].NextAirUtc = null;
                _volatileDataCache[id].NextAirFetchedAtUtc = null;
                _volatileDataCache[id].TimeTillNextAir = "";
            }
        }

        public static void RegisterVolatileDataAiringTimeFetchFailure(int id)
        {
            if (_volatileDataCache.ContainsKey(id))
            {
                _volatileDataCache[id].LastFailedAiringTimeFetchAttempt = DateTime.UtcNow;
            }
        }

        public static bool TryRetrieveDataForId(int id, out VolatileDataCache data)
        {
            try
            {
                return _volatileDataCache.TryGetValue(id, out data);
            }
            catch (Exception)
            {
                data = null;
                return false;
            }
        }

        #endregion

        #region AnimeDetailsData

        public static async void SaveAnimeDetails(int id, AnimeDetailsData data, bool anime = true)
        {
            try
            {

                await Task.Run(async () =>
                {
                    await
                        DataCacheService.SaveData(data, $"{data.Source}_{id}.json",
                            anime ? "AnimeDetails" : "MangaDetails");
                });
            }
            catch (Exception)
            {
                //probably failed to create folder #windowsmagic
            }
        }

        public static async Task<AnimeDetailsData> RetrieveAnimeGeneralDetailsData(int id, DataSource source,
            bool anime = true)
        {
            try
            {
                var final = await DataCacheService.RetrieveData<AnimeDetailsData>($"{source}_final_{id}.json",
                    anime ? "AnimeDetails" : "MangaDetails", 0);
                if (final != null)
                    return final;
                return await DataCacheService.RetrieveData<AnimeDetailsData>($"{source}_{id}.json",
                    anime ? "AnimeDetails" : "MangaDetails", 1);
            }
            catch (Exception)
            {
                //No file
            }
            return null;
        }

        #endregion

        #region Episodes

        public static async Task SaveAnimeEpisodes(int id, List<AnimeEpisode> data)
        {
            try
            {
                await Task.Run(async () =>
                {
                    await DataCacheService.SaveData(data, $"episodes_{id}.json", "AnimeDetails");
                });
            }
            catch (Exception)
            {
                //magic
            }
        }

        public static async Task<List<AnimeEpisode>> RetrieveAnimeEpisodes(int id, bool airing)
        {
            try
            {
                return await DataCacheService.RetrieveData<List<AnimeEpisode>>($"episodes_{id}.json", "AnimeDetails",
                    airing ? 1 : 0);
            }
            catch (Exception)
            {
                //No file
            }
            return null;
        }

        public static async Task<List<AnimeEpisode>> RetrieveAnimeEpisodesStale(int id)
        {
            try
            {
                return await DataCacheService.RetrieveData<List<AnimeEpisode>>($"episodes_{id}.json", "AnimeDetails", 0);
            }
            catch (Exception)
            {
                //No file
            }
            return null;
        }

        #endregion

        #region DetailsScrapped

        public static async void SaveAnimeDetailsScrappedByStatus(int id, AnimeScrappedDetails data, bool airing)
        {
            try
            {
                await Task.Run(async () =>
                {
                    await DataCacheService.SaveData(data, airing ? $"{id}.json" : $"{id}_final.json",
                        "anime_details_scrapped");
                });
            }
            catch (Exception)
            {
                //magic
            }
        }

        public static async Task<AnimeScrappedDetails> RetrieveAnimeDetailsScrapped(int id, bool airing)
        {
            try
            {
                var final = await DataCacheService.RetrieveData<AnimeScrappedDetails>($"{id}_final.json",
                    "anime_details_scrapped", 0);
                if (final != null)
                    return final;
                return await DataCacheService.RetrieveData<AnimeScrappedDetails>($"{id}.json",
                    "anime_details_scrapped", airing ? 1 : 0);
            }
            catch (Exception)
            {
                //No file
            }
            return null;
        }

        public static async Task<AnimeScrappedDetails> RetrieveAnimeDetailsScrappedStale(int id)
        {
            try
            {
                return await DataCacheService.RetrieveData<AnimeScrappedDetails>($"{id}.json",
                    "anime_details_scrapped", 0);
            }
            catch (Exception)
            {
                //No file
            }
            return null;
        }

        #endregion

        #region Reviews

        public static async void SaveAnimeReviews(int id, List<AnimeReviewData> data, bool anime)
        {
            try
            {
                await Task.Run(async () =>
                {
                    await DataCacheService.SaveData(data, $"reviews_{id}.json", anime ? "AnimeDetails" : "MangaDetails");
                });
            }
            catch (Exception)
            {
                //magic
            }
        }

        public static async Task<List<AnimeReviewData>> RetrieveReviewsData(int animeId, bool anime)
        {
            try
            {
                return
                    await
                        DataCacheService.RetrieveData<List<AnimeReviewData>>($"reviews_{animeId}.json",
                            anime ? "AnimeDetails" : "MangaDetails", 14);
            }
            catch (Exception)
            {
                //No file
            }
            return null;
        }

        #endregion

        #region DirectRecommendations

        public static async void SaveDirectRecommendationsData(int id, List<DirectRecommendationData> data,
            bool anime)
        {
            try
            {
                await Task.Run(async () =>
                {
                        await DataCacheService.SaveData(data, $"direct_recommendations_{id}.json",
                            anime ? "AnimeDetails" : "MangaDetails");
                });
            }
            catch (Exception)
            {
                //magic
            }
        }

        public static async Task<List<DirectRecommendationData>> RetrieveDirectRecommendationData(int id,
            bool anime)
        {
            try
            {
                return await DataCacheService.RetrieveData<List<DirectRecommendationData>>(
                    $"direct_recommendations_{id}.json", anime ? "AnimeDetails" : "MangaDetails", 14);
            }
            catch (Exception)
            {
                //No file
            }
            return null;
        }

        #endregion

        #region RelatedAnime

        public static async void SaveRelatedAnimeData(int id, List<RelatedAnimeData> data, bool anime)
        {
            try
            {
                await Task.Run(async () =>
                {

                    await
                        DataCacheService.SaveData(data, $"related_anime_v3_{id}.json",
                            anime ? "AnimeDetails" : "MangaDetails");
                });
            }
            catch (Exception)
            {
                //magic
            }
        }

        public static async Task<List<RelatedAnimeData>> RetrieveRelatedAnimeData(int animeId, bool anime)
        {
            try
            {
                return
                    await
                        DataCacheService.RetrieveData<List<RelatedAnimeData>>($"related_anime_v3_{animeId}.json",
                            anime ? "AnimeDetails" : "MangaDetails", 14);
            }
            catch (Exception)
            {
                //No file
            }
            return null;
        }

        #endregion

        #region AnimeSerachResults

        public static async Task SaveAnimeSearchResultsData(string id, AnimeGeneralDetailsData data, bool anime)
        {
            try
            {
                await Task.Run(async () =>
                {
                    await
                        DataCacheService.SaveData(data, $"mal_details_v4_{id}.json",
                            anime ? "AnimeDetails" : "MangaDetails");
                });
            }
            catch (Exception)
            {
                //magic
            }
        }

        public static async Task SaveAnimeSearchResultsDataFinal(string id, AnimeGeneralDetailsData data, bool anime)
        {
            try
            {
                await Task.Run(async () =>
                {
                    await
                        DataCacheService.SaveData(data, $"mal_details_final_{id}.json",
                            anime ? "AnimeDetails" : "MangaDetails");
                });
            }
            catch (Exception)
            {
                //magic
            }
        }

        public static async Task SaveGeneralDetailsByStatus(string id, AnimeGeneralDetailsData data, bool anime)
        {
            if (string.Equals(data.Status, "Finished Airing", StringComparison.CurrentCultureIgnoreCase))
                await SaveAnimeSearchResultsDataFinal(id, data, anime);
            else
                await SaveAnimeSearchResultsData(id, data, anime);
        }

        public static async Task<AnimeGeneralDetailsData> RetrieveAnimeSearchResultsData(string animeId, bool anime)
        {
            try
            {
                var final = await DataCacheService.RetrieveData<AnimeGeneralDetailsData>($"mal_details_final_{animeId}.json",
                    anime ? "AnimeDetails" : "MangaDetails", 0);
                if (final != null)
                    return final;
                return await DataCacheService.RetrieveData<AnimeGeneralDetailsData>($"mal_details_v4_{animeId}.json",
                    anime ? "AnimeDetails" : "MangaDetails", 14);
            }
            catch (Exception)
            {
                //No file
            }
            return null;
        }

        public static async Task<AnimeGeneralDetailsData> RetrieveAnimeSearchResultsDataStale(string animeId, bool anime)
        {
            try
            {
                return await DataCacheService.RetrieveData<AnimeGeneralDetailsData>($"mal_details_v4_{animeId}.json",
                    anime ? "AnimeDetails" : "MangaDetails", 0);
            }
            catch (Exception)
            {
                //No file
            }
            return null;
        }

        #endregion

        #region TopAnime

        public static async void SaveTopAnimeData(List<TopAnimeData> data, TopAnimeType type)
        {
            try
            {
                await Task.Run(async () =>
                {
                    await DataCacheService.SaveData(data, $"top_{type}_data.json", "");
                });
            }
            catch (Exception)
            {
                //magic
            }
        }

        public static async Task<List<TopAnimeData>> RetrieveTopAnimeData(TopAnimeType type)
        {
            try
            {
                return await DataCacheService.RetrieveData<List<TopAnimeData>>($"top_{type}_data.json", "", 14);
            }
            catch (Exception)
            {
                //No file
            }
            return null;
        }

        public static async void SaveTopMangaData(List<TopAnimeData> data, MangaTopType type)
        {
            try
            {
                await Task.Run(async () =>
                {
                    await DataCacheService.SaveData(data, $"topmanga_{type}_data_v2.json", "");
                });
            }
            catch (Exception)
            {
                //magic
            }
        }

        public static async Task<List<TopAnimeData>> RetrieveTopMangaData(MangaTopType type)
        {
            try
            {
                return await DataCacheService.RetrieveData<List<TopAnimeData>>($"topmanga_{type}_data_v2.json", "", 14);
            }
            catch (Exception)
            {
                //No file
            }
            return null;
        }

        public static async void SaveAdaptedToAnimeData(List<TopAnimeData> data, MangaAdaptedType type)
        {
            try
            {
                await Task.Run(async () =>
                {
                    await DataCacheService.SaveData(data, $"adapted_{type}_data.json", "");
                });
            }
            catch (Exception)
            {
                //magic
            }
        }

        public static async Task<List<TopAnimeData>> RetrieveAdaptedToAnimeData(MangaAdaptedType type)
        {
            try
            {
                return await DataCacheService.RetrieveData<List<TopAnimeData>>($"adapted_{type}_data.json", "", 14);
            }
            catch (Exception)
            {
                //No file
            }
            return null;
        }

        #endregion

        #region MalToHum

        public static async Task SaveHumMalIdDictionary()
        {
            try
            {
                //await DataCacheService.SaveData(AnimeDetailsHummingbirdQuery.MalToHumId, "mal_to_hum.json", "");
            }
            catch (Exception)
            {
                //ignored
            }
        }

        public static async void RetrieveHumMalIdDictionary()
        {
            var result = new Dictionary<int, int>();
            try
            {
                result = await DataCacheService.RetrieveData<Dictionary<int, int>>("mal_to_hum.json", "", 0);
            }
            catch (Exception)
            {
                result = new Dictionary<int, int>();
            }
            //AnimeDetailsHummingbirdQuery.MalToHumId = result ?? new Dictionary<int, int>();
        }

        #endregion

        #region ProfileData

        public static async void SaveProfileData(string user, ProfileData data)
        {
            try
            {
                await Task.Run(async () =>
                {
                    await DataCacheService.SaveData(data, $"mal_profile_details_{user}.json", "ProfileData");
                });
            }
            catch (Exception)
            {
                //magic
            }
        }

        public static async Task<ProfileData> RetrieveProfileData(string user)
        {
            try
            {
                return
                    await
                        DataCacheService.RetrieveData<ProfileData>($"mal_profile_details_{user}.json", "ProfileData", 1);
            }
            catch (Exception)
            {
                //No file
            }
            return null;
        }

        #endregion

        #region ArticlesIndex

        public static async void SaveArticleIndexData(ArticlePageWorkMode mode, List<MalNewsUnitModel> data)
        {
            try
            {
                await Task.Run(async () =>
                {
                    await
                        DataCacheService.SaveData(data,
                            mode == ArticlePageWorkMode.Articles ? "mal_article_index.json" : "mal_news_index.json",
                            "Articles");
                });
            }
            catch (Exception)
            {
                //magic
            }
        }

        public static async Task<List<MalNewsUnitModel>> RetrieveArticleIndexData(ArticlePageWorkMode mode)
        {
            try
            {
                return await DataCacheService.RetrieveData<List<MalNewsUnitModel>>(mode == ArticlePageWorkMode.Articles
                    ? "mal_article_index.json"
                    : "mal_news_index.json", "Articles", 1);
            }
            catch (Exception)
            {
                //No file
            }
            return null;
        }

        #endregion

        #region ArticlesContent

        public static async void SaveArticleContentData(string title, string htmlData, MalNewsType type)
        {
            try
            {
                await Task.Run(async () =>
                {
                    await
                        DataCacheService.SaveData(htmlData,
                            $"mal_{(type == MalNewsType.Article ? "article" : "news")}_html_{title}.json", "Articles");
                });
            }
            catch (Exception e)
            {
                //magic
            }
        }

        public static async Task<string> RetrieveArticleContentData(string title, MalNewsType type)
        {
            try
            {
                return
                    await
                        DataCacheService.RetrieveData<string>(
                            $"mal_{(type == MalNewsType.Article ? "article" : "news")}_html_{title}.json", "Articles", 7);
            }
            catch (Exception)
            {
                //No file
            }
            return null;
        }

        #endregion
    }
}


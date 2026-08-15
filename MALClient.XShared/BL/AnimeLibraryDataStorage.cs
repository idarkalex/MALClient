using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MALClient.Models.Enums;
using MALClient.Models.Models.Library;
using MALClient.XShared.Comm;
using MALClient.XShared.Delegates;
using MALClient.XShared.Interfaces;
using MALClient.XShared.ViewModels;

namespace MALClient.XShared.BL
{
    public class AnimeLibraryDataStorage : IAnimeLibraryDataStorage
    {       
        public List<AnimeItemAbstraction> AllLoadedAnimeItemAbstractions { get; set; } = new List<AnimeItemAbstraction>();
        public List<AnimeItemAbstraction> AllLoadedMangaItemAbstractions { get; set; } = new List<AnimeItemAbstraction>();

        public Dictionary<string, Tuple<List<AnimeItemAbstraction>, List<AnimeItemAbstraction>>> OthersAbstractions {
            get; } = new Dictionary<string, Tuple<List<AnimeItemAbstraction>, List<AnimeItemAbstraction>>>();


        public List<AnimeItemAbstraction> AllLoadedAuthAnimeItems { get; set; } = new List<AnimeItemAbstraction>();
        public List<AnimeItemAbstraction> AllLoadedAuthMangaItems { get; set; } = new List<AnimeItemAbstraction>();
        public List<AnimeItemAbstraction> AllLoadedSeasonalAnimeItems { get; set; } = new List<AnimeItemAbstraction>();
        public List<AnimeItemAbstraction> AllLoadedSeasonalMangaItems { get; set; } = new List<AnimeItemAbstraction>();

        public void AddAnimeEntry(AnimeItemAbstraction parentAbstraction)
        {
            if (parentAbstraction.RepresentsAnime)
                AllLoadedAuthAnimeItems.Add(parentAbstraction);
            else
                AllLoadedAuthMangaItems.Add(parentAbstraction);
        }

        public void RemoveAnimeEntry(AnimeItemAbstraction parentAbstraction)
        {
            if (parentAbstraction.RepresentsAnime)
                AllLoadedAuthAnimeItems.Remove(parentAbstraction);
            else
                AllLoadedAuthMangaItems.Remove(parentAbstraction);

            AnimeRemoved?.Invoke(parentAbstraction);
        }

        public void Reset()
        {
            AllLoadedAnimeItemAbstractions.Clear();
            AllLoadedAuthAnimeItems.Clear();
            AllLoadedAuthMangaItems.Clear();
            AllLoadedMangaItemAbstractions.Clear();
            AllLoadedSeasonalAnimeItems.Clear();
            AllLoadedSeasonalMangaItems.Clear();
        }

        public async Task EnsureOthersLibraryLoadedAsync(string user)
        {
            lock (OthersAbstractions)
            {
                if (OthersAbstractions.ContainsKey(user))
                    return;
            }

            var data = new List<ILibraryData>();
            await
                Task.Run(
                    async () =>
                        data =
                            await
                                new LibraryListQuery(user, AnimeListWorkModes.Anime)
                                    .GetLibrary(false));

            var abstractions = new List<AnimeItemAbstraction>();
            foreach (var libraryData in data)
                abstractions.Add(new AnimeItemAbstraction(false, libraryData as AnimeLibraryItemData));

            await
                Task.Run(
                    async () =>
                        data =
                            await
                                new LibraryListQuery(user, AnimeListWorkModes.Manga)
                                    .GetLibrary(false));

            var mangaAbstractions = new List<AnimeItemAbstraction>();
            foreach (var libraryData in data)
                mangaAbstractions.Add(new AnimeItemAbstraction(false, libraryData as MangaLibraryItemData));

            lock (OthersAbstractions)
            {
                if (!OthersAbstractions.ContainsKey(user))
                    OthersAbstractions.Add(user,
                        new Tuple<List<AnimeItemAbstraction>, List<AnimeItemAbstraction>>(abstractions,
                            mangaAbstractions));
            }
        }

        public event AnimeAbstractionTransaction AnimeRemoved;
    }
}

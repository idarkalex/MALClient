using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Ioc;
using MALClient.Adapters;
using MALClient.Models.Enums;
using MALClient.Models.Models.Library;
using MALClient.XShared.Comm;
using MALClient.XShared.Comm.Anime;
using MALClient.XShared.Delegates;
using MALClient.XShared.Interfaces;
using MALClient.XShared.Utils;

namespace MALClient.XShared.ViewModels.Main
{
    public class CalendarPivotPage
    {
        public DayOfWeek DayOfWeek { get; set; }
        public string Header { get; set; }
        public string Sub { get; set; }
        public string FullHeader => Utils.Utilities.ShortDayToFullDay(Header); //full name
        public List<AnimeItemViewModel> Items { get; set; } = new List<AnimeItemViewModel>();
    }

    //just to indicate different data type
    public sealed class CalendarSummaryPivotPage : CalendarPivotPage
    {
        public new string Header => "Summary";
        public new string Sub => "";

        public List<Tuple<string, List<AnimeItemViewModel>>> Data { get; set; } =
            new List<Tuple<string, List<AnimeItemViewModel>>>();
    }


    public class CalendarPageViewModel : ViewModelBase
    {
        private readonly IAnimeLibraryDataStorage _animeLibraryDataStorage;
        public event EmptyEventHander PivotSelectedIndexChange;

        public ObservableCollection<CalendarPivotPage> CalendarData { get; set; } =
            new ObservableCollection<CalendarPivotPage>();

        // UI state preservation (scroll positions, expanded panels, etc.) per session
        public Dictionary<string, object> UiState { get; } = new Dictionary<string, object>();

        public static double ItemWidth { get; private set; }

        private int _calendarPivotIndex;

        public int CalendarPivotIndex
        {
            get { return _calendarPivotIndex; }
            set
            {
                _calendarPivotIndex = value;
                PivotSelectedIndexChange?.Invoke();
            }
        }

        public CalendarPivotPage CurrentPivotPage { get; set; }

        private bool _calendarBuildingVisibility = false;

        public bool CalendarBuildingVisibility
        {
            get { return _calendarBuildingVisibility; }
            set
            {
                _calendarBuildingVisibility = value;
                RaisePropertyChanged(() => CalendarBuildingVisibility);
            }
        }

        private bool _calendarVisibility = false;

        public bool CalendarVisibility
        {
            get { return _calendarVisibility; }
            set
            {
                _calendarVisibility = value;
                RaisePropertyChanged(() => CalendarVisibility);
            }
        }

        private int _progressValue;

        public int ProgressValue
        {
            get { return _progressValue; }
            set
            {
                _progressValue = value;
                RaisePropertyChanged(() => ProgressValue);
            }
        }

        private int _maxProgressValue;

        public int MaxProgressValue
        {
            get { return _maxProgressValue; }
            set
            {
                _maxProgressValue = value;
                RaisePropertyChanged(() => MaxProgressValue);
            }
        }

        private ICommand _refreshCalendarCommand;

        public ICommand RefreshCalendarCommand
            => _refreshCalendarCommand ?? (_refreshCalendarCommand = new RelayCommand(() => Init(true)));


        private ICommand _exportToCalendarCommand;

        public ICommand ExportToCalendarCommand
            => _exportToCalendarCommand ?? (_exportToCalendarCommand = new RelayCommand<AnimeItemViewModel>(entry =>
               {
                   try
                   {
                       SimpleIoc.Default.GetInstance<ICalendarExportProvider>().ExportToCalendar(entry);
                   }
                   catch (Exception)
                   {
                       //no calendar on platofirm
                   }
               }));


        private void InitPages()
        {
            CalendarData = new ObservableCollection<CalendarPivotPage>
            {
                new CalendarPivotPage(),
                new CalendarPivotPage(),
                new CalendarPivotPage(),
                new CalendarPivotPage(),
                new CalendarPivotPage(),
                new CalendarPivotPage(),
                new CalendarPivotPage(),
                new CalendarSummaryPivotPage(),
            };
        }

        public CalendarPageViewModel(IAnimeLibraryDataStorage animeLibraryDataStorage)
        {
            _animeLibraryDataStorage = animeLibraryDataStorage;
            ItemWidth = AnimeItemViewModel.MaxWidth - 8;
        }

        private bool _initialized;

        public async Task Init(bool force = false)
        {
            if (_initialized && !force)
            {
                await GoToDesiredTab();
                CalendarVisibility = true;
                return;
            }
            InitPages();
            _initialized = true;
            CalendarBuildingVisibility = true;

            try
            {
                DiagnosticsReporter.Info("Calendar", "calendar init: awaiting provider init");
                await ResourceLocator.AiringInfoProvider.Init(false);
                List<AnimeItemAbstraction> abstractions;
                DiagnosticsReporter.Info("Calendar",
                    $"calendar init: providerReady={ResourceLocator.AiringInfoProvider.InitializationSuccess} allAiring={Settings.CalendarShowAllAiring} force={force} initialized={_initialized}");
                if (Settings.CalendarShowAllAiring)
                {
                    var buildTask = BuildAllAiringAbstractionsAsync();
                    var completed = await Task.WhenAny(buildTask, Task.Delay(25000));
                    if (completed != buildTask)
                    {
                        DiagnosticsReporter.Error("Calendar", "airing build: TIMEOUT 25s - proceeding with fallback", null);
                        abstractions = new List<AnimeItemAbstraction>();
                    }
                    else
                        abstractions = buildTask.Result;
                }
                else
                {
                    var buildTask = BuildMyListAbstractionsAsync();
                    var completed = await Task.WhenAny(buildTask, Task.Delay(25000));
                    if (completed != buildTask)
                    {
                        DiagnosticsReporter.Error("Calendar", "my list build: TIMEOUT 25s - proceeding with provider entries", null);
                        abstractions = await Task.Run(() => ProviderMyListAbstractions());
                    }
                    else
                        abstractions = buildTask.Result;
                }

                foreach (var abstraction in abstractions)
                {
                    try
                    {
                        var nowUtc = DateTime.UtcNow;

                        if (ResourceLocator.AiringInfoProvider.TryGetNextAirDate(abstraction.Id, nowUtc, out var nextAirDate) &&
                            (nextAirDate - nowUtc).TotalDays >= 7)
                            continue;

                        if (ResourceLocator.AiringInfoProvider.TryGetAiringDay(abstraction.Id, out DayOfWeek dayOfWeek))
                        {
                            int day = (int) dayOfWeek;
                            if (day >= 0 && day <= 7)
                                CalendarData[day].Items.Add(abstraction.ViewModel);
                        }
                        else if (abstraction.RepresentsAnime &&
                                 DataCache.TryRetrieveDataForId(abstraction.Id, out var volatileData) &&
                                 volatileData.NextAirUtc.HasValue &&
                                 (volatileData.NextAirUtc.Value > nowUtc || AirTimeUtils.IsInAiringWindow(volatileData.NextAirUtc.Value, nowUtc)) &&
                                 (volatileData.NextAirUtc.Value - nowUtc).TotalDays < 7)
                        {
                            int day = (int) volatileData.NextAirUtc.Value.DayOfWeek;
                            if (day >= 0 && day <= 7)
                                CalendarData[day].Items.Add(abstraction.ViewModel);
                        }
                    }
                    catch (Exception e)
                    {
                        //there are some numm ref crashes and I don't know really know where
                        // probably MAL returns some odd stuff and we cannot get details
                    }
                }

                DiagnosticsReporter.Info("Calendar", $"calendar init: items assigned={CalendarData.Sum(p => p.Items.Count)}");

                if (Settings.CalendarSwitchMonSun)
                {
                    CalendarData.Move(0, 6);
                    CalendarData[0].Header = Utilities.DayToString(DayOfWeek.Monday, true);
                    CalendarData[0].DayOfWeek = DayOfWeek.Monday;
                    CalendarData[6].Header = Utilities.DayToString(DayOfWeek.Sunday, true);
                    CalendarData[6].DayOfWeek = DayOfWeek.Sunday;
                    for (int i = 1; i < 6; i++)
                    {
                        CalendarData[i].Header = Utilities.DayToString((DayOfWeek) i + 1, true);
                        CalendarData[i].DayOfWeek = (DayOfWeek) i + 1;
                    }
                }
                else
                {
                    for (int i = 0; i < 7; i++)
                    {
                        CalendarData[i].Header = Utilities.DayToString((DayOfWeek) i, true);
                        CalendarData[i].DayOfWeek = (DayOfWeek) i;
                    }
                }

                var emptyPages = new List<CalendarPivotPage>();
                foreach (var calendarPivotPage in CalendarData.Take(CalendarData.Count - 1))
                {
                    if (calendarPivotPage.Items.Count > 0)
                        calendarPivotPage.Sub = calendarPivotPage.Items.Count.ToString();
                    else
                    {
                        if (Settings.CalendarRemoveEmptyDays)
                            emptyPages.Add(calendarPivotPage);
                        else
                            calendarPivotPage.Sub = "-";
                    }
                    if (calendarPivotPage.Items.Count != 0)
                        (CalendarData[7] as CalendarSummaryPivotPage).Data.Add(
                            new Tuple<string, List<AnimeItemViewModel>>(calendarPivotPage.FullHeader,
                                calendarPivotPage.Items));
                }
                foreach (var emptyPage in emptyPages)
                    CalendarData.Remove(emptyPage);

                DiagnosticsReporter.Info("Calendar",
                    $"calendar build: allAiring={Settings.CalendarShowAllAiring} providerOk={ResourceLocator.AiringInfoProvider.InitializationSuccess}");

                RaisePropertyChanged(() => CalendarData);
                await GoToDesiredTab();
            }
            catch (Exception ex)
            {
                DiagnosticsReporter.Error("Calendar", "Calendar Init failed", ex);
            }
            finally
            {
                CalendarBuildingVisibility = false;
                CalendarVisibility = true;
                DiagnosticsReporter.Info("Calendar", "calendar init: overlay cleared");
            }
        }

        private List<AnimeItemAbstraction> ProviderMyListAbstractions()
        {
            var abstractions = _animeLibraryDataStorage.AllLoadedAuthAnimeItems.Where(abstraction =>
                ResourceLocator.AiringInfoProvider.HasAiringEntry(abstraction.Id)).Where(abstraction =>
            {
                if (DataCache.TryRetrieveDataForId(abstraction.Id, out var vd) && !string.IsNullOrEmpty(vd.LastKnownStatus) && !AirTimeUtils.IsCurrentlyAiringStatus(vd.LastKnownStatus))
                    return false;
                return true;
            }).Where(
                abstraction => abstraction.Type == (int)AnimeType.TV && (
                    (Settings.CalendarIncludePlanned &&
                     abstraction.MyStatus == AnimeStatus.PlanToWatch) ||
                    (Settings.CalendarIncludeWatching && abstraction.MyStatus == AnimeStatus.Watching))).ToList();

            //Limit items to at most the configured amount
            var maxItems = Math.Max(1, Settings.CalendarMaxItems);
            if (abstractions.Count > maxItems)
            {
                var watchingCount = abstractions.Count(abstraction => abstraction.MyStatus == AnimeStatus.Watching);
                //with currently watched ones having most priority
                if (watchingCount > maxItems)
                    abstractions = abstractions.Where(abstraction => abstraction.MyStatus == AnimeStatus.Watching).Take(maxItems).ToList();
                else
                {
                    //take all watching and add ptw to make at most maxItems entries
                    abstractions = abstractions.Where(abstraction => abstraction.MyStatus == AnimeStatus.Watching)
                        .Concat(abstractions.Where(abstraction => abstraction.MyStatus == AnimeStatus.PlanToWatch)
                            .Take(maxItems - watchingCount)).ToList();
                }
            }

            return abstractions;
        }

        private async Task<List<AnimeItemAbstraction>> BuildMyListAbstractionsAsync()
        {
            var abstractions = await Task.Run(() => ProviderMyListAbstractions());
            var watched = _animeLibraryDataStorage.AllLoadedAuthAnimeItems;
            var nowUtc = DateTime.UtcNow;

            //defensive: currently-airing library entries NOT in the provider (e.g. partial cache)
            //are resolved through the exact same countdown chain so the calendar never drops them.
            var missing = watched.Where(abstraction =>
                    abstraction.RepresentsAnime &&
                    !ResourceLocator.AiringInfoProvider.HasAiringEntry(abstraction.Id) &&
                    abstraction.Type == (int) AnimeType.TV &&
                    ((Settings.CalendarIncludePlanned &&
                      abstraction.MyStatus == AnimeStatus.PlanToWatch) ||
                     (Settings.CalendarIncludeWatching && abstraction.MyStatus == AnimeStatus.Watching)))
                .ToList();

            var maxItems = Math.Max(1, Settings.CalendarMaxItems);
            var budget = maxItems - abstractions.Count;
            if (budget > 0)
            {
                foreach (var abstraction in missing.Take(budget))
                {
                    try
                    {
                        var nextAir = await abstraction.ViewModel.GetTimeTillNextAirAsync(null);
                        if (nextAir.HasValue &&
                            (nextAir.Value > nowUtc || AirTimeUtils.IsInAiringWindow(nextAir.Value, nowUtc)) &&
                            (nextAir.Value - nowUtc).TotalDays < 7)
                        {
                            abstraction.ViewModel.SetNextAirCache(nextAir);
                            abstractions.Add(abstraction);
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return abstractions;
        }

        private async Task<List<AnimeItemAbstraction>> BuildAllAiringAbstractionsAsync()
        {
            return await Task.Run(() =>
            {
                var result = new List<AnimeItemAbstraction>();
                var nowUtc = DateTime.UtcNow;
                List<int> ids;
                try
                {
                    ids = ResourceLocator.AiringInfoProvider.GetAllAiringIds()
                        .Select(id => (Id: id, Next: ResourceLocator.AiringInfoProvider.TryGetNextAirDate(id, nowUtc, out var d) ? (DateTime?)d : null))
                        .Where(x => x.Next.HasValue && (x.Next.Value - nowUtc).TotalDays < 7)
                        .OrderBy(x => x.Next.Value)
                        .Select(x => x.Id)
                        .Distinct()
                        .ToList();
                }
                catch (Exception e)
                {
                    DiagnosticsReporter.Error("Calendar", "airing ids enumeration failed", e);
                    ids = new List<int>();
                }

                foreach (var id in ids)
                {
                    try
                    {
                        if (!ResourceLocator.AiringInfoProvider.TryGetEntry(id, out var entry))
                            continue;

                        result.Add(new AnimeItemAbstraction(false, new AnimeLibraryItemData
                        {
                            Id = entry.MalId,
                            MalId = entry.MalId,
                            Title = entry.Title,
                            ImgUrl = entry.ImgUrl,
                            AllEpisodes = entry.AllEpisodes,
                            Type = entry.Type,
                            AlternateTitle = entry.Title
                        }));
                    }
                    catch (Exception)
                    {
                        // skip failed entry
                    }
                }
                DiagnosticsReporter.Info("Calendar", $"airing build: ids={ids.Count} items={result.Count}");
                return result;
            });
        }

        public async Task GoToDesiredTab()
        {
            await Task.Delay(10);
            if (Settings.CalendarStartOnToday)
            {
                //we have to find it because it may have been removed
                //we will do this by comparing header string
                string today = Utils.Utilities.DayToString(DateTime.Now.DayOfWeek, true);
                int index = CalendarData.Count - 1;
                for (int i = 0; i < CalendarData.Count - 1; i++)
                {
                    if (CalendarData[i].Header == today)
                    {
                        index = i;
                        break;
                    }
                }
                CalendarPivotIndex = index;
            }
            else
                CalendarPivotIndex = CalendarData.Count - 1;
        }
        
    }
}
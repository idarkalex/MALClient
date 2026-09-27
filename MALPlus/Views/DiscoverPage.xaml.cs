using System.Text.Json;
using MALClient.Models.Models.Misc;
using MALClient.XShared.Comm.Discovery;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALPlus.Services;

namespace MALPlus.Views;

public partial class DiscoverPage : ContentPage
{
    private const string FilterPrefsKey = "MALPlus_DiscoveryRandomFilter_v1";

    private bool _busy;

    public DiscoverPage()
    {
        InitializeComponent();
    }

    

    private async Task<bool> GuardAsync()
    {
        if (_busy)
            return false;
        _busy = true;
        HeaderSpinner.IsRunning = true;
        return true;
    }

    private void Release()
    {
        _busy = false;
        HeaderSpinner.IsRunning = false;
    }

    // ---------- simple destinations ----------

    private async void OnSearchTapped(object sender, TappedEventArgs e)
    {
        try { await Shell.Current.GoToAsync("search"); } catch { }
    }

    private async void OnNewsTapped(object sender, TappedEventArgs e)
    {
        try { await Shell.Current.GoToAsync("articles?mode=News"); } catch { }
    }

    private async void OnUpcomingAllTapped(object sender, TappedEventArgs e)
    {
        try { await Shell.Current.GoToAsync("calendar?mode=global"); } catch { }
    }

    private async void OnUpcomingMineTapped(object sender, TappedEventArgs e)
    {
        try { await Shell.Current.GoToAsync("calendar?mode=mine"); } catch { }
    }

    // ---------- multi result sheets ----------

    private async void OnTrendingTapped(object sender, TappedEventArgs e)
    {
        if (!await GuardAsync()) return;
        try
        {
            var items = await AniListTrendingQuery.GetAsync();
            if (items.Count == 0)
            {
                global::Android.Util.Log.Warn("MALPlus Discover",
                    "AniList trending failed: " + AniListTrendingQuery.LastError);
                // AniList is the only real trending source; fall back to Tenrai's
                // currently-airing-by-favourites ranking rather than showing nothing.
                items = await DiscoveryRandomQuery.GetTopAiringAsync(20);
            }
            if (items.Count == 0)
                await Alert("Could not load trending.");
            else
                await ShowResults("Trending", items);
        }
        catch { }
        finally { Release(); }
    }

    private async void OnBecauseYouLikedTapped(object sender, TappedEventArgs e)
    {
        if (!await GuardAsync()) return;
        try
        {
            var anchors = new List<Tuple<int, string, int>>();
            var inLibrary = new HashSet<int>();
            var library = ResourceLocator.AnimeLibraryDataStorage?.AllLoadedAuthAnimeItems
                          ?? new List<AnimeItemAbstraction>();
            foreach (var abstraction in library)
            {
                if (abstraction?.EntryData is not MALClient.Models.Models.Library.AnimeLibraryItemData data)
                    continue;
                inLibrary.Add(abstraction.MalId);
                if (data.MyStatus == MALClient.Models.Enums.AnimeStatus.Completed && data.MyScore >= 8)
                    anchors.Add(Tuple.Create(abstraction.MalId, data.Title, (int)data.MyScore));
            }
            if (anchors.Count == 0)
            {
                await Alert("You need at least one completed anime scored 8 or more in your list.");
                return;
            }

            var result = await BecauseYouLikedQuery.GetAsync(anchors, inLibrary);
            if (result.Item2.Count == 0)
                await Alert("No recommendations found for your list.");
            else
                await ShowResults("Because you liked " + result.Item1, result.Item2);
        }
        catch { }
        finally { Release(); }
    }

    private async void OnSeasonThemesTapped(object sender, TappedEventArgs e)
    {
        if (!await GuardAsync()) return;
        try
        {
            var items = await SeasonThemesQuery.GetAsync();
            if (items.Count == 0)
                await Alert("No OP/ED found for the current season.");
            else
                await ShowResults("Season OP/ED", items);
        }
        catch { }
        finally { Release(); }
    }

    private async void OnGemTapped(object sender, TappedEventArgs e)
    {
        if (!await GuardAsync()) return;
        try
        {
            // The community thresholds (members/popularity) are ignored server side by
            // Tenrai, so the ceiling is applied client side after a filtered page.
            var items = await DiscoveryRandomQuery.GetAsync(false, null, 75, 1500, 20);
            if (items.Count == 0)
                await Alert("No gems matched those filters.");
            else
                await ShowResults("Random gem", items);
        }
        catch { }
        finally { Release(); }
    }

    // ---------- single item jumps ----------

    private async void OnRandomAnimeTapped(object sender, TappedEventArgs e)
    {
        if (!await GuardAsync()) return;
        try { await JumpToRandomAsync(false); }
        finally { Release(); }
    }

    private async void OnRandomMangaTapped(object sender, TappedEventArgs e)
    {
        if (!await GuardAsync()) return;
        try { await JumpToRandomAsync(true); }
        finally { Release(); }
    }

    private async Task JumpToRandomAsync(bool manga)
    {
        var filter = await LoadFilterAsync(manga);
        var items = await DiscoveryRandomQuery.GetAsync(manga, filter, 0, 0, 1);
        if (items.Count == 0)
        {
            await Alert("Nothing matched those filters.");
            return;
        }
        var qs = manga ? "&manga=true" : "";
        await Shell.Current.GoToAsync($"animedetails?id={items[0].Id}&title={Uri.EscapeDataString(items[0].Title ?? "")}{qs}");
    }

    private async void OnRandomThemeTapped(object sender, TappedEventArgs e)
    {
        if (!await GuardAsync()) return;
        try
        {
            var theme = await AnimeThemesHelper.GetRandomThemeAsync();
            if (string.IsNullOrEmpty(theme?.Url))
            {
                await Alert("AnimeThemes returned no theme.");
                return;
            }
            var item = new DiscoveryItem
            {
                Id = 0,
                Title = theme.SongTitle ?? "Theme",
                Subtitle = theme.Type + " " + theme.Sequence,
                ThemeUrl = theme.Url
            };
            await ShowResults("Random theme", new List<DiscoveryItem> { item });
        }
        catch { }
        finally { Release(); }
    }

    // ---------- random filters ----------

    private async void OnRandomAnimeSettingsTapped(object sender, EventArgs e)
        => await EditFilterAsync(false);

    private async void OnRandomMangaSettingsTapped(object sender, EventArgs e)
        => await EditFilterAsync(true);

    private async Task EditFilterAsync(bool manga)
    {
        var filter = await LoadFilterAsync(manga);
        var title = manga ? "Manga filters" : "Anime filters";

        var formats = manga
            ? new[] { "", "manga", "novel", "lightnovel", "oneshot", "doujinshi", "manhwa", "manhua" }
            : new[] { "", "tv", "movie", "ova", "special", "ona", "music" };
        var formatLabels = formats
            .Select(f => string.IsNullOrEmpty(f) ? "All" : char.ToUpperInvariant(f[0]) + f.Substring(1))
            .ToArray();

        var score = await DisplayActionSheet(title, "Cancel", null,
            formatLabels.Concat(new[]
            {
                "Min score: " + (filter.MinScore <= 0 ? "any" : filter.MinScore.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)),
                "Years: " + YearLabel(filter),
                "Genres: " + (filter.GenreIds.Count == 0 ? "any" : filter.GenreIds.Count + "chosen"),
                "Reset"
            }).ToArray());
        if (string.IsNullOrEmpty(score) || score == "Cancel")
            return;

        if (score == "Reset")
        {
            await SaveFilterAsync(manga, new RandomFilter());
        }
        else if (score.StartsWith("Min score"))
        {
            var raw = await DisplayActionSheet("Minimum score", "Cancel", null,
                "0 (any)", "6", "7", "7.5", "8", "8.5", "9");
            if (!string.IsNullOrEmpty(raw) && raw != "Cancel")
                filter.MinScore = raw.StartsWith("0") ? 0 : double.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
            await SaveFilterAsync(manga, filter);
        }
        else if (score.StartsWith("Years"))
        {
            var raw = await DisplayActionSheet("Year range", "Cancel", null,
                "Any", "2015+", "2010+", "2000+", "Before 2000");
            if (!string.IsNullOrEmpty(raw) && raw != "Cancel")
            {
                filter.MinYear = raw switch
                {
                    "2015+" => 2015,
                    "2010+" => 2010,
                    "2000+" => 2000,
                    "Before 2000" => 1960,
                    _ => 0
                };
                filter.MaxYear = 0;
            }
            await SaveFilterAsync(manga, filter);
        }
        else if (score.StartsWith("Genres"))
        {
            var chosen = await PickGenresAsync(filter.GenreIds, manga);
            if (chosen != null)
            {
                filter.GenreIds = chosen;
                await SaveFilterAsync(manga, filter);
            }
        }
        else
        {
            var index = Array.IndexOf(formatLabels, score);
            if (index >= 0)
            {
                filter.Format = formats[index];
                await SaveFilterAsync(manga, filter);
            }
        }
        await RefreshFilterSummaries();
    }

    private static string YearLabel(RandomFilter filter) =>
        filter.MinYear <= 0 ? "any" : filter.MinYear + "+";

    private async Task<List<int>> PickGenresAsync(List<int> current, bool manga)
    {
        var all = System.Enum.GetValues(typeof(MALClient.Models.Enums.AnimeGenreSearch)).Cast<int>().ToList();
        var selected = new List<int>(current);
        // The chip row runs out of space fast, so this is a two-step picker.
        while (true)
        {
            var labels = all
                .Select(id => (id == 0 ? "Done" : DescribeGenre(id)) + (selected.Contains(id) ? " ✓" : ""))
                .ToArray();
            var pick = await DisplayActionSheet("Genres", "Cancel", null, labels);
            if (string.IsNullOrEmpty(pick) || pick == "Cancel")
                return null;
            if (pick == "Done")
                return selected;
            var index = Array.IndexOf(labels, pick);
            if (index < 0)
                continue;
            var id = all[index];
            if (selected.Contains(id))
                selected.Remove(id);
            else
                selected.Add(id);
        }
    }

    private static string DescribeGenre(int id)
    {
        try
        {
            var info = System.Enum.GetName(typeof(MALClient.Models.Enums.AnimeGenreSearch), id);
            if (info == null)
                return "Genre " + id;
            var attr = typeof(MALClient.Models.Enums.AnimeGenreSearch)
                .GetField(info)?.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
            if (attr is { Length: > 0 } && attr[0] is System.ComponentModel.DescriptionAttribute d)
                return d.Description;
            return info.Replace("_", " ");
        }
        catch
        {
            return "Genre " + id;
        }
    }

    private async Task<RandomFilter> LoadFilterAsync(bool manga)
    {
        var key = FilterPrefsKey + (manga ? "_manga" : "_anime");
        try
        {
            var stored = await ResourceLocator.DataCacheService
                .RetrieveDataRoaming<RoamingData<RandomFilter>>(key, -1);
            return stored?.Data ?? new RandomFilter();
        }
        catch
        {
            return new RandomFilter();
        }
    }

    private async Task SaveFilterAsync(bool manga, RandomFilter filter)
    {
        var key = FilterPrefsKey + (manga ? "_manga" : "_anime");
        try
        {
            await ResourceLocator.DataCacheService.SaveDataRoaming(new RoamingData<RandomFilter>
            {
                Data = filter
            }, key);
        }
        catch
        {
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshFilterSummaries();
    }

    private async Task RefreshFilterSummaries()
    {
        RandomAnimeSummary.Text = Summarize(await LoadFilterAsync(false));
        RandomMangaSummary.Text = Summarize(await LoadFilterAsync(true));
    }

    private static string Summarize(RandomFilter filter)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(filter.Format))
            parts.Add(char.ToUpperInvariant(filter.Format[0]) + filter.Format.Substring(1));
        if (filter.MinScore > 0)
            parts.Add("★" + filter.MinScore.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
        if (filter.MinYear > 0)
            parts.Add(filter.MinYear + "+");
        if (filter.GenreIds.Count > 0)
            parts.Add(filter.GenreIds.Count + " genres");
        return parts.Count == 0 ? "No filters" : string.Join(" · ", parts);
    }

    // ---------- helpers ----------

    private async Task ShowResults(string header, List<DiscoveryItem> items)
    {
        try
        {
            var json = JsonSerializer.Serialize(items);
            var page = new DiscoveryResultsPage
            {
                HeaderText = header,
                ItemsJson = json
            };
            await Navigation.PushModalAsync(page);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("MALPlus Discover", "show results: " + ex.GetType().Name);
        }
    }

    private async Task Alert(string message)
    {
        try
        {
            await DisplayAlert("", message, "OK");
        }
        catch
        {
        }
    }
}

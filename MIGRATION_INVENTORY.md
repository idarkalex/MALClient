# MALPlus v2 → .NET MAUI Migration Inventory (Phase 0)

**Branch:** `maui-migration` (from `v2`)  
**Date:** 2026-09-05  
**Source:** Native Xamarin.Android (Fragment/MvvmLight/MVVM) — **NOT Xamarin.Forms**  
**Target:** .NET MAUI (.NET 8/9) Single Project — Android + Windows (WinUI 3)  
**Excluded:** Discover page/tab — **EXCLUDED** (to be completely rebuilt post-migration)

---

## 1. Project Structure

| Project | Type | Target Framework | Status | Action |
|---|---|---|---|---|
| `MALClient.Android` | Xamarin.Android | v13.0 (API 33) | ✅ Active | Migrate → MAUI Android |
| `MALClient.XShared` | .NET Standard 2.0 | netstandard2.0 | ✅ Core | Migrate → `MALPlus.Core` (net8.0/net9.0) |
| `MALClient.Models` | .NET Standard 2.0 | netstandard2.0 | ✅ Models | Merge into `MALPlus.Core` |
| `MALClient.Adapters` | .NET Standard 2.0 | netstandard2.0 | ✅ Adapters | Migrate → MAUI Handlers |
| `MALClient.Android.Adapters` | Xamarin.Android | v13.0 | ✅ Adapters | Migrate → MAUI Handlers |
| `MALClient.Utilities.BBCode` | .NET Standard 2.0 | netstandard2.0 | ✅ Utils | Merge into `MALPlus.Core` |
| `Binding_*` (4) | Android Bindings | v9.0-v12.0 | ⚠️ Bindings | Evaluate: drop or rebind via MAUI Handlers |
| `MALClient.Utilities.BBCode` | .NET Standard 2.0 | netstandard2.0 | ✅ Utils | Merge into `MALPlus.Core` |
| `MALClient.XShared` | .NET Standard 2.0 | netstandard2.0 | ✅ Core | → `MALPlus.Core` (net8.0/net9.0) |
| **MISSING** | — | — | — | **Add:** `MALPlus` (MAUI App, net8.0-android/net8.0-windows) |
| **MISSING** | — | — | — | **Add:** `MALPlus.Core` (net8.0;net9.0) |
| **MISSING** | — | — | — | **Add:** `MALPlus.Windows` (WinUI 3) |

**No UWP project exists** — confirmed. No Windows project to delete.

---

## 2. Renderer & Image Loading Inventory (CRITICAL)

**Key Finding:** This is a **native Xamarin.Android app** — **NO Xamarin.Forms** (0 `ViewRenderer`, 0 `CachedImage`, 0 `FFImageLoading.Forms`). All UI is Fragment-based with MvvmLight.

| Category | Files | Unique Classes | Migration Risk | MAUI Replacement |
|---|---|---|---|---|
| **IMAGE_LOADING** | 55 | — | 🔴 **CRITICAL** | MAUI `Image` + `IImageLoader` (CommunityToolkit.Maui / SkiaSharp) |
| **IMAGE_EFFECT** | 4 (2 unique) | 1 `OnDraw` | 🟠 High | `GraphicsView` + `SKCanvas` (SkiaSharp) |
| **WEBVIEW** | 3 | 3 | 🟠 High | `WebViewHandler` / `IPlatformWebViewClient` |
| **LIST** | 5 | 5 | 🟢 Low | `CollectionView` / `CarouselView` |
| **CUSTOM_CONTROL** | 7 | 7 | 🟠 High | MAUI Handlers / `ContentView` |
| **OTHER** | 2 | 2 | 🟢 Low | `ScrollView` / `RefreshView` |

### IMAGE_LOADING — 55 files (CRITICAL PATH)
**Central chokepoint:** `MALClient.Android/Utilities/AnimeImageExtensions.cs` — static helpers `AnimeInto`, `AnimeIntoIfLoaded`, `GetImgUrl` used everywhere. **Replacing its internals rewires all 55 consumers.**

Key files using FFImageLoading (`ImageViewAsync`, `ImageService.LoadUrl(...).Into(...)`):
- **Dialogs:** `PinnedUsersDialog`, `KeyImageDialog`, `ChangelogDialog`, `AnimeDetailsPageDialogBuilder`, `AddFriendDialog`
- **ViewHolders:** `CommentViewHolder`, `AnimeGridItem`, `AnimeListItem`, `FavouriteItem`, `ForumTopicItem`, `ForumIndexItem`, `RecommendationItemFragment`
- **Adapters/Mediation:** `GenericAdBanner`, `DakimakuriAdBanner`, `CuddlyOctopusAdBanner`
- **Fragments:** `WallpapersPageFragment`, `SearchPageFragment`, `SearchEverywherePageFragment`, `AnimeTypeSearchFragment`, `AnimeSearchPageFragment`, `RecommendationItemFragment`, `PromoVideosPageFragment`, `ProfilePageGeneralTabFragment`, `MorePageFragment`, `MessagingDetailsPageFragment`, `ListComparisonPageFragment`, `HistoryPageTabFragment`, `FriendsPageTabFragment`, `FriendsPageRequestsTabFragment`, `FriendsFeedsPageFragment`, `ForumIndexPageFragmentRecentsTabFragment`, `ClubIndexTabFragmentBase`, `ClubDetailsPageRelationsTabFragment`, `ClubDetailsPageGeneralTabFragment`, `ClubDetailsPageCommentsTabFragment`, `DiscoverPageFragment` ⚠️ **EXCLUDED**, `ArticlesPageTabFragment`, `PersonDetailsPageVaTabFragment`, `PersonDetailsPageProdTabFragment`, `PersonDetailsPageFragment`, `CharacterDetailsPageFragment`, `AnimeDetailsPageFragment`, `AnimeDetailsPageTabs/*` (Staff, Reviews, Related, Recoms, Characters), `SettingsAboutFragment`
- **Utils:** `AnimeImageExtensions.cs` (central helper), `CustomScrollListener.cs` (pause FF on fling), `FlingCollectionsHelper.cs` (AbsListView perf), `MemoryWatcher.cs` (invalidate cache)

**FFImageLoading Packages (MALClient.Android.csproj only):**
- `Xamarin.FFImageLoading` 2.4.11.982 (line 373)
- `Xamarin.FFImageLoading.Transformations` 2.4.11.982 (line 376)

### IMAGE_EFFECT — 4 files (2 unique)
- `PagerSlidingTabStrip.cs` (line 666: `OnDraw`) — duplicated under `AndroidBindings/PagerSlidingTabStrip/PagerSlidingTabStrip.cs`
- **Transformations:** `CircleTransformation`, `BlurredTransformation` applied at `.Into(...)` call time via FFImageLoading — **NO custom BitmapDrawable subclasses**

### WEBVIEW — 3 files
- `InlineVideoWebViewClient.cs` — rewrites YouTube URLs to `/embed/{id}`
- `ListenableWebClient.cs` — page-ready event + navigation intercept (article reader)
- `DataJavascriptInterface.cs` — `[JavascriptInterface]` bridge for article/news WebView

### LIST — 5 custom views
- `PriorityListView`, `ExpandableGridView`, `HeightAdjustingListView`, `HeightAdjustingGridView`, `HeightAdjustingViewPager`
- All measure/layout overrides for content sizing → **MAUI `CollectionView`/`CarouselView` handles natively**

### CUSTOM_CONTROL — 7 files
- `ZoomableImageView` (pinch-zoom/pan) — trickiest: needs `GraphicsView`/`SKCanvas`
- `FavouriteButton` (heart toggle)
- `BBCodeEditor` (toolbar + editor)
- `UserControlBase` (abstract FrameLayout + MVVM bind contract)
- 3 AdBanners (`GenericAdBanner`, `DakimakuriAdBanner`, `CuddlyOctopusAdBanner`) — FFImageLoading consumers
- `ScrollableSwipeToRefreshLayout` → MAUI `RefreshView`

---

## 3. DependencyService / Platform Services

**No Xamarin.Forms `DependencyService` used.** The codebase uses **MvvmLight `SimpleIoc`** via `ResourceLocator` / `ViewModelLocator` static facades.

| Call Site / Service | Current Implementation | MAUI Replacement |
|---|---|---|
| `SimpleIoc.Default.GetInstance<Activity>()` — 12 call sites | `MainActivity` registration in `MainActivity.cs` | Constructor injection / `IServiceProvider` |
| `SimpleIoc.Default.GetInstance<MainViewModel>()` | `MainViewModel` registration | Constructor injection |
| `IPinTileService` / `ICalendarExportProvider` | **DEAD** — never registered | Remove or implement |

**Platform services to implement as MAUI Services (DI):**
| Interface | Current Android Implementation | MAUI Replacement |
|---|---|---|
| `IFileService` | `FileService_Android` (implied) | `FileSystem` (Essentials) |
| `IShareService` | `ShareProvider.cs` | `Share.RequestAsync` (Essentials) |
| `INotificationService` | (Not found) | `LocalNotifications` plugin / `Microsoft.Maui.ApplicationModel` |
| `IToastService` | `SnackbarProvider.cs`, `MessageDialogProvider.cs` | `Toast` (CommunityToolkit) |
| `IShareService` | `ShareProvider.cs` | `Share.RequestAsync` |
| `IPermissionService` | (Not found) | `Permissions` (Essentials) |
| `IFilePickerService` | (Not found) | `FilePicker` (Essentials) |
| `IBiometricService` | (Not found) | Custom handler / platform |
| `ICssManager` | `CssManager.cs` (XShared) | Keep as-is (cross-platform) |
| `IDispatcherAdapter` | `DispatcherAdapter.cs` (Android) | `MainThread` / `Dispatcher` (MAUI) |

**Registration pattern in `MainActivity.cs`:**
```csharp
SimpleIoc.Default.Unregister<Activity>();
SimpleIoc.Default.Register<Activity>(() => this);
```

---

## 4. Platform-Specific Code (`#if` blocks)

**Only `#if DEBUG` directives exist.** **No `#if __ANDROID__`, `WINDOWS_UWP`, `__IOS__`** — this is a single-platform (Android) codebase.

| Symbol | Files | Entries | Notes |
|---|---|---|---|
| `DEBUG` | 2 files | 7 entries | Logging, debug-only assertions |
| `ANDROID_API` (runtime checks) | 33 files | 50 entries | `Build.VERSION.SdkInt` runtime checks for API levels |

**Android-specific patterns (203 C# files in MALClient.Android):**
- `MainActivity.CurrentContext` — 24 files (context, Toasts, Resources, LayoutInflater, system services, UI thread, orientation, IoC)
- `Android.App.Application.Context` — 1 file fallback
- `Android.Runtime.Preserve` — 7 files (linker protection)
- Direct `Android.*` namespaces throughout
- **No** `Xamarin.Forms.Device.RuntimePlatform`, `OnPlatform`, `OnIdiom`
- **No** `DependencyAttribute` registrations

---

## 5. NuGet Packages Inventory

| Category | Unique Packages | Refs | MAUI Status | Action |
|---|---|---|---|---|
| **MVVM** | 1 | 2 | **NEEDS_REPLACEMENT** | `MvvmLightLibsStd10` → `CommunityToolkit.Mvvm` |
| **IMAGE** | 2 | 2 | **NEEDS_REPLACEMENT** | `Xamarin.FFImageLoading`, `Xamarin.FFImageLoading.Transformations` → MAUI Image + SkiaSharp |
| **UI_COMPONENTS** | 8 | 8 | **NEEDS_REPLACEMENT** | `FastAdapter`, `MaterialDrawer`, `AndroidX` libs → MAUI `CollectionView`, `Material` libs |
| **NOTIFICATIONS** | 5 | 5 | **NEEDS_REPLACEMENT** | `AppCenter` (3), `GooglePlayServices.Ads` (2) → Firebase/Sentry |
| **PLATFORM_ANDROID** | 2 | 2 | **OBSOLETE_DELETE** | `Xamarin.Android.Support.*`, `AndroidX.Legacy` → **DELETE** |
| **MVVM** | 1 | 2 | **NEEDS_REPLACEMENT** | `MvvmLightLibsStd10` → `CommunityToolkit.Mvvm` |
| **JSON** | 2 | 4 | **MIGRATE_DIRECT** | `Newtonsoft.Json`, `System.Text.Json` — keep |
| **WEB** | 1 | 1 | **MIGRATE_DIRECT** | `HtmlAgilityPack` — keep |
| **OTHER** | 1 | 1 | **MIGRATE_DIRECT** | `morelinq` — keep |

**Binding projects** (MoPub, Dialogplus, etc.) use AAR/JAR references — evaluate for MAUI Handler rebinding.

---

## 6. Custom Controls / AXML Templates

| Type | Count | Notes |
|---|---|---|
| **Layout XML (layout/)** | 143 | All Fragment/Dialog/Item layouts — rewrite as MAUI XAML |
| **Drawable XML** | 142 | Shapes, selectors, gradients, borders — migrate to MAUI `Drawable` / `GraphicsView` / `Border` |
| **Styles/Values** | 7 files | `colors.xml`, `dimens.xml`, `fonts.xml`, `Strings.xml`, `styles.xml`, `attrs.xml`, `ic_launcher_background.xml` |
| **Fonts** | 5 `.ttf` | `Inter` (4 weights) + `FontAwesome` — `Resources/Fonts/` + `ConfigureFonts` |
| **Animations** | 4 `.xml` | `Resources/animator/` — MAUI `Animation` API |
| **Menu XML** | 3 files | `Resources/menu/` — MAUI `MenuBar` / `Toolbar` |
| **XML Config** | 5 files | `calendar_widget_info_*.xml`, `debug_ca.xml`, `file_paths.xml`, `shortcuts.xml` |
| **Custom View Classes** | 23+ | `UserControls/*`, `LandscapeHelpers/*`, `ZoomableImageView`, `FavouriteButton`, `BBCodeEditor`, `PriorityListView`, `ExpandableGridView`, `HeightAdjusting*`, `OrientationDependentScrollView`, `PriorityListView`, `ZoomableImageView`, `FavouriteButton`, `BBCodeEditor`, `FavouriteItem`, `PriorityListView`, `ToggleableScrollView`, `ExpandableGridView`, `HeightAdjustingGridView`, `HeightAdjustingListView`, `HeightAdjustingViewPager`, `OrientationDependentScrollView`, `PriorityScrollView`, `CustomScrollListener` |

**Custom View Classes (need MAUI Handlers):**
| Class | Base | Key Overrides | MAUI Replacement |
|---|---|---|---|
| `ZoomableImageView` | `ImageViewAsync` | `OnMeasure`, `OnTouchEvent` (pinch-zoom) | `GraphicsView` + `SKCanvas` |
| `FavouriteButton` | `FrameLayout` | Heart toggle | `Button` + `Image` |
| `BBCodeEditor` | `LinearLayout` | Toolbar + editor | `ContentView` + `Editor` + toolbar |
| `PriorityListView` | `ListView` | Accessibility priority | `CollectionView` |
| `ExpandableGridView` | `GridView` | `OnMeasure` (content sizing) | `CollectionView` |
| `HeightAdjustingListView` | `ListView` | `OnMeasure` | `CollectionView` |
| `HeightAdjustingGridView` | `GridView` | `OnMeasure` | `CollectionView` |
| `HeightAdjustingViewPager` | `ViewPager` | `OnMeasure` | `CarouselView` / `TabView` |
| `OrientationDependentScrollView` | `ScrollView` | Orientation aware | `ScrollView` |
| `PriorityScrollView` | `ScrollView` | Priority drawing | `ScrollView` |
| `ZoomableImageView` | `ImageViewAsync` | Pinch-zoom | `GraphicsView` + `SKCanvas` |
| `FavouriteButton` | `FrameLayout` | Heart toggle | `Button` |
| `BBCodeEditor` | `LinearLayout` | Toolbar + editor | `Editor` + toolbar |
| `FavouriteItem` | `FrameLayout` | Compound view | `ContentView` |
| `PriorityListView` | `ListView` | Priority | `CollectionView` |
| `ExpandableGridView` | `GridView` | Content sizing | `CollectionView` |
| `HeightAdjustingGridView` | `GridView` | Content sizing | `CollectionView` |
| `HeightAdjustingListView` | `ListView` | Content sizing | `CollectionView` |
| `HeightAdjustingViewPager` | `ViewPager` | Content sizing | `CarouselView` |
| `OrientationDependentScrollView` | `ScrollView` | Orientation | `ScrollView` |
| `ToggleableScrollView` | `ScrollView` | Priority | `ScrollView` |
| `ZoomableImageView` | `ImageViewAsync` | Pinch-zoom | `GraphicsView` + `SKCanvas` |
| `CustomScrollListener` | `RecyclerView.OnScrollListener` | Pause FFImage on fling | **DELETE** (MAUI virtualization) |

---

## 7. ViewModels / Navigation / State Preservation (Already Implemented)

**All ViewModels in `MALClient.XShared/ViewModels/` — 50 files.**

| VM | State Preserved | UiState Keys | Migration Notes |
|---|---|---|---|
| `SearchPageViewModel` | ✅ | `LastSearchPageIndex`, `GenreFilterQuery`, `StudioFilterQuery`, `CatalogueScrollPosition/Offset`, `GenreListScrollPosition`, `StudioListScrollPosition`, `CatalogueIsGenre`, `ActiveCatalogueTitle` | Preserve `UiState` dict |
| `AnimeListViewModel` | ✅ | `UiState` (dict), `CurrentIndexPosition`, `ScrollIntoViewRequested` | Add `ListScrollPosition` per WorkMode |
| `AnimeDetailsPageViewModel` | ✅ | `DetailsPivotSelectedIndex`, `_loaded*`, `LastAired`, `_timeTillNextAirCache`, per-tab loaded flags | Per-tab scroll via `UiState` |
| `ProfilePageViewModel` | ✅ | `CurrentPivotIndex`, `FavAnime`, `FavManga`, `RecentAnime`, `RecentManga` | Per-tab scroll via `UiState` |
| `CalendarPageViewModel` | ✅ | `CalendarPivotIndex`, `UiState["CalendarTab_<idx>"]` | Already implemented per-tab scroll |
| `AnimeDetailsPageViewModel.ui.cs` | — | Hero zoom, tab index | Per-tab scroll |
| `RecommendationsViewModel` | ✅ | `PivotItemIndex` | Add scroll |
| `ProfilePageViewModel` | ✅ | `CurrentPivotIndex` | Per-tab scroll |
| `CalendarPageViewModel` | ✅ | `CalendarPivotIndex`, `UiState["CalendarTab_<idx>"]` | Done |
| `SearchPageViewModel` | ✅ | `CatalogueScrollPosition/Offset`, `GenreFilterQuery`, `StudioFilterQuery`, `GenreListScrollPosition`, `StudioListScrollPosition` | Done |
| `FragmentUiState` | Static dicts | Per-page scroll/state | Use `MauiProgram` DI |

**Navigation:**
- `INavMgr` / `NavMgr` — back-nav stack (`_randomNavigationStackMain`)
- `MainViewModelBase.Navigate()` — central dispatcher
- `NavigateDetails` — deep links with `AnimeDetailsPageNavigationArgs`
- Back-nav restoration via `RegisterBackNav` / `DeregisterBackNav` / `RegisterOneTimeMainOverride`

**State Preservation Pattern (already implemented in v2):**
- `UiState` dictionary per VM
- `OnPause`/`OnResume` save/restore scroll via `ScrollStateHelper`
- `FragmentUiState` static dicts for VM-less fragments

---

## 8. Assets / Resources / Themes / Fonts

| Asset Type | Count | Source | MAUI Location |
|---|---|---|---|
| **Layout XML** | 143 | `Resources/layout/` | `Resources/Layouts/` (optional) / inline XAML |
| **Drawable XML** | 142 | `Resources/drawable*/` | `Resources/Raw/` or inline `GraphicsView`/`Border` |
| **Styles/Values** | 7 files | `Resources/values/` | `Resources/Styles/Colors.xaml`, `Styles.xaml` |
| **Fonts** | 5 `.ttf` | `Resources/font/` | `Resources/Fonts/` + `ConfigureFonts` |
| **Animations** | 4 | `Resources/animator/` | MAUI `Animation` API |
| **Menu XML** | 3 | `Resources/menu/` | MAUI `MenuBar`/`Toolbar` |
| **XML Config** | 5 | `Resources/xml/` | Platform-specific (keep in `Platforms/Android/Resources/xml/`) |
| **Fonts (.ttf)** | 5 | `Resources/font/` | `Inter` (4 weights) + `FontAwesome` |
| **App Icons** | 5 densities | `mipmap-*/ic_launcher.png` | `Resources/AppIcon/appicon.svg` → `MauiIcon` |
| **Splash** | ? | `drawable*/` | `Resources/Splash/splash.svg` → `MauiSplashScreen` |

**Theme/Style files to migrate:**
- `colors.xml` → `Resources/Styles/Colors.xaml`
- `dimens.xml` → `Resources/Styles/Dimensions.xaml` (or inline)
- `fonts.xml` → `ConfigureFonts` in `MauiProgram.cs`
- `styles.xml` → `Resources/Styles/Styles.xaml`
- `attrs.xml` → Custom `BindableProperty` on controls

---

## 9. ViewModels Summary (50 total)

**Core/Main:**
- `ViewModelLocator.cs` — static facade (→ DI in `MauiProgram`)
- `ResourceLocator.cs` — static facade (→ DI)
- `MainViewModelBase.cs` — base navigation
- `MainViewModel.cs` (in `MainActivity`) — root nav

**Feature VMs (47):**
| Feature | VMs |
|---|---|
| Anime List/Manga/Seasonal/Top | `AnimeListViewModel`, `AnimeListViewModels.ui` |
| Search | `SearchPageViewModel`, `SearchEverywhereViewModel`, `CharacterSearchViewModel` |
| Anime Details | `AnimeDetailsPageViewModel`, `AnimeDetailsPageViewModel.ui` |
| Profile | `ProfilePageViewModel` |
| Calendar | `CalendarPageViewModel` |
| Articles/News | `MalArticlesViewModel` |
| Recommendations | `RecommendationsViewModel` |
| Wallpapers | `WallpapersViewModel` |
| Clubs | `ClubDetailsViewModel`, `ClubIndexViewModel` |
| Forums | `ForumsMainViewModel`, `ForumIndexViewModel`, `ForumBoardViewModel`, `ForumTopicViewModel`, `ForumNewTopicViewModel`, `ForumsStarredMessagesViewModel` |
| History | `HistoryViewModel` |
| Wallpapers | `WallpapersViewModel` |
| Feeds | `FriendsFeedsViewModel` |
| Notifications | `NotificationsHubViewModel` |
| Popular Videos | `PopularVideosViewModel` |
| Messaging | `MalMessagingViewModel`, `MalMessageDetailsViewModel` |
| Profile | `ProfilePageViewModel` |
| Log In | `LogInViewModel` |
| List Comparison | `ListComparisonViewModel` |
| Friends | `FriendsPageViewModel`, `FriendsFeedsViewModel` |
| Character/Staff | `CharacterDetailsViewModel`, `StaffDetailsViewModel` |
| Settings | `SettingsViewModelBase` |
| Comparison | `ListComparisonViewModel`, `ComparisonItemViewModel` |
| Profile | `FavouriteViewModel` |
| Comparison | `ComparisonItemViewModel` |

---

## 9. Migration Effort Estimation (Updated)

| Phase | Duration | Notes |
|---|---|---|
| **Phase 0: Inventory & Analysis** | ✅ Done | This document |
| **Phase 1: Core Library (`MALPlus.Core`)** | 2-3 weeks | `XShared` → net8.0/net9.0, SQLite, DI setup |
| **Phase 2: MAUI Shell + DI + Services** | 1 week | `MauiProgram`, Handlers registration, Platform services |
| **Phase 3A: Image Loading Migration (Spike)** | 1-2 weeks | **CRITICAL** — FFImageLoading → MAUI Image + SkiaSharp |
| **Phase 3B: Core UI Screens (13 screens)** | 10-12 weeks | Discover ⚠️ **EXCLUDED** |
| **Phase 4: Windows / WinUI 3** | 2-3 weeks | WinUI 3 desktop, WebView2 video |
| **Phase 5: Testing & Polish** | 3-4 weeks | Device testing, performance, offline, seed import |

**Total: ~22-24 weeks (5-6 months) for solo dev**

---

## 10. Excluded from Migration

| Item | Reason |
|---|---|
| **Discover Page / Tab** | ⚠️ **EXCLUDED** — User will rebuild completely post-migration. Leave as empty placeholder in `TabView`. |
| UWP Project | Already removed (never existed) |
| MoPub Ads | Deprecated — remove |
| `PagerSlidingTabStrip` binding | Replace with MAUI `TabView` |

---

## 11. Spike Recommendations (Do First)

| Spike | Duration | Goal |
|---|---|---|
| **Spike 1: FFImageLoading → MAUI Image** | 1 week | Port `AnimeImageExtensions.AnimeInto` to MAUI `Image` + SkiaSharp (Circle/Blur). Validate 55 consumer sites. |
| **Spike 2: WebView YouTube Embed** | 3 days | `WebViewHandler` + `IPlatformWebViewClient` rewrite URL → `/embed/`, test Android + Windows WebView2 |
| **Spike 3: CollectionView Performance** | 3 days | 1000+ items, infinite scroll, FFImageLoading replacement, compare to current `FlingCollectionsHelper` perf |

---

## 12. Next Steps

1. ✅ Phase 0 Inventory complete — this document
2. 🔜 **Phase 1:** Create `MALPlus.Core` (net8.0/net9.0) from `XShared` + `Models` + `Utilities.BBCode`
3. 🔜 **Spike 1:** FFImageLoading → MAUI Image + SkiaSharp (validate on `AnimeGridItem`)
4. 🔜 **Phase 2:** MAUI App shell (`MauiProgram`, DI, Handlers)
4. 🔜 **Phase 3:** Screen-by-screen UI migration (start with `Discover` placeholder → `AnimeList` → `AnimeDetails` → `Search` → `Profile` → `Calendar` → `More` → others)

---

**Branch:** `maui-migration`  
**Status:** Phase 0 complete — ready for Phase 1
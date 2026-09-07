using GalaSoft.MvvmLight.Ioc;
using MALClient.Adapters;
using MALClient.Adapters.Credentials;
using MALClient.XShared.Interfaces;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;

namespace MALPlus.Services;

public static class MauiViewModelLocator
{
    public static void RegisterDependencies()
    {
        ViewModelLocator.Mobile = true;

        ViewModelLocator.RegisterBase();

        SimpleIoc.Default.Register<MauiMainViewModel>();
        SimpleIoc.Default.Register<MainViewModelBase>(() => SimpleIoc.Default.GetInstance<MauiMainViewModel>());
        SimpleIoc.Default.Register<IHamburgerViewModel, MauiHamburgerViewModel>();
        SimpleIoc.Default.Register<INavMgr, MauiNavMgr>();

        SimpleIoc.Default.Register<IDataCache, MauiDataCache>();
        SimpleIoc.Default.Register<IPasswordVault, MauiPasswordVault>();
        SimpleIoc.Default.Register<IApplicationDataService, MauiApplicationDataService>();
        SimpleIoc.Default.Register<IClipboardProvider, MauiClipboardProvider>();
        SimpleIoc.Default.Register<ISystemControlsLauncherService, MauiSystemControlsLauncher>();
        SimpleIoc.Default.Register<IMessageDialogProvider, MauiMessageDialogProvider>();
        SimpleIoc.Default.Register<IImageDownloaderService, MauiImageDownloaderService>();
        SimpleIoc.Default.Register<ITelemetryProvider, MauiTelemetryProvider>();
        SimpleIoc.Default.Register<INotificationsTaskManager, MauiNotificationsTaskManager>();
        SimpleIoc.Default.Register<ISchdeuledJobsManger, MauiScheduledJobsManager>();
        SimpleIoc.Default.Register<ICssManager, MauiCssManager>();
        SimpleIoc.Default.Register<IChangeLogProvider, MauiChangeLogProvider>();
        SimpleIoc.Default.Register<IMalHttpContextProvider, MauiMalHttpContextProvider>();
        SimpleIoc.Default.Register<ISnackbarProvider, MauiSnackbarProvider>();
        SimpleIoc.Default.Register<IConnectionInfoProvider, MauiConnectionInfoProvider>();
        SimpleIoc.Default.Register<IDispatcherAdapter, MauiDispatcherAdapter>();
        SimpleIoc.Default.Register<IAiringNotificationsAdapter, MauiAiringNotificationsAdapter>();
        SimpleIoc.Default.Register<IDialogsProvider, MauiDialogsProvider>();
        SimpleIoc.Default.Register<IShareProvider, MauiShareProvider>();
        SimpleIoc.Default.Register<ILiveTilesManager, MauiLiveTilesManager>();
        SimpleIoc.Default.Register<IPinTileService, MauiPinTileService>();
        SimpleIoc.Default.Register<ICalendarExportProvider, MauiCalendarExportProvider>();
        SimpleIoc.Default.Register<MauiSettingsViewModel>();
        SimpleIoc.Default.Register<MALClient.XShared.ViewModels.SettingsViewModelBase>(
            () => SimpleIoc.Default.GetInstance<MauiSettingsViewModel>());
    }
}

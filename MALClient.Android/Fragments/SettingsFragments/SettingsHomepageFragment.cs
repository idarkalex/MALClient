using System;
using System.Collections.Generic;
using System.Linq;

using Android.App;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using MALClient.Android.Resources;
using MALClient.Models.Enums;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;

namespace MALClient.Android.Fragments.SettingsFragments
{
    public class SettingsHomepageFragment : SettingsFragmentBase
    {
        private List<object> _items;

        protected override void InitBindings()
        {
            var pages = ViewModel.SettingsPages
                .Where(entry => entry.PageType != SettingsPageIndex.Caching &&
                                entry.PageType != SettingsPageIndex.Articles &&
                                (Credentials.Authenticated || entry.PageType != SettingsPageIndex.Ads)).ToList();

            _items = new List<object>();

            // General section
            _items.Add("GENERAL");
            _items.Add(pages.First(p => p.PageType == SettingsPageIndex.General));
            _items.Add(pages.First(p => p.PageType == SettingsPageIndex.Calendar));

            // Account section
            _items.Add("ACCOUNT");
            _items.Add(pages.First(p => p.PageType == SettingsPageIndex.LogIn));
            if (Credentials.Authenticated)
                _items.Add(pages.First(p => p.PageType == SettingsPageIndex.Ads));

            // Social section
            _items.Add("SOCIAL");
            var feedsEntry = pages.FirstOrDefault(p => p.PageType == SettingsPageIndex.Feeds);
            if (feedsEntry != null) _items.Add(feedsEntry);
            var notifEntry = pages.FirstOrDefault(p => p.PageType == SettingsPageIndex.Notifications);
            if (notifEntry != null) _items.Add(notifEntry);

            // About section
            _items.Add("ABOUT");
            _items.Add(pages.First(p => p.PageType == SettingsPageIndex.About));
            _items.Add(pages.First(p => p.PageType == SettingsPageIndex.Misc));

            // Fun section
            _items.Add("FUN");
            _items.Add(new SettingsPageEntry
            {
                Header = "Dakimakura Guide",
                PageType = SettingsPageIndex.Daki,
                Subtitle = "Make your life comfier and avoid filthy thieves and bootleggers!",
                Symbol = SettingsSymbolsEnum.Rocket,
            });
            _items.Add(new SettingsPageEntry
            {
                Header = "Did you know?",
                PageType = SettingsPageIndex.Info,
                Subtitle = "Me explaining this UI...",
                Symbol = SettingsSymbolsEnum.Lightbulb,
            });

            BuildLayout();
        }

        private void BuildLayout()
        {
            SettingsPageHomepageList.RemoveAllViews();
            var inflater = Activity.LayoutInflater;

            foreach (var item in _items)
            {
                if (item is string headerText)
                {
                    var header = inflater.Inflate(Resource.Layout.SettingsSectionHeader, SettingsPageHomepageList, false);
                    header.FindViewById<TextView>(Resource.Id.SettingsSectionHeaderText).Text = headerText;
                    SettingsPageHomepageList.AddView(header);
                }
                else if (item is SettingsPageEntry entry)
                {
                    var view = inflater.Inflate(Resource.Layout.SettingsPageItem, SettingsPageHomepageList, false);
                    view.FindViewById<TextView>(Resource.Id.SettingsPageItemHeader).Text = entry.Header;
                    var img = view.FindViewById<ImageView>(Resource.Id.SettingsPageItemIcon);
                    img.SetImageResource(GetIcon(entry.Symbol));

                    if (entry.PageType == SettingsPageIndex.Daki)
                    {
                        img.ImageTintList = null;
                    }
                    else
                    {
                        img.ImageTintList = ColorStateList.ValueOf(new Color(ResourceExtension.BrushTextSecondary));
                    }

                    // Descriptions removed: rows match the More page style
                    view.FindViewById<TextView>(Resource.Id.SettingsPageItemSubtitle).Visibility = ViewStates.Gone;

                    view.Tag = entry.Wrap();
                    view.Click += OnItemClick;
                    SettingsPageHomepageList.AddView(view);
                }
            }
        }

        private void OnItemClick(object sender, EventArgs e)
        {
            var view = (View)sender;
            var entry = view.Tag.Unwrap<SettingsPageEntry>();
            ViewModel.RequestNavigationCommand.Execute(entry.PageType);
        }

        private int GetIcon(SettingsSymbolsEnum symbol)
        {
            return symbol switch
            {
                SettingsSymbolsEnum.Setting => Resource.Drawable.icon_settings,
                SettingsSymbolsEnum.SaveLocal => Resource.Drawable.icon_save_local,
                SettingsSymbolsEnum.CalendarWeek => Resource.Drawable.icon_calendar,
                SettingsSymbolsEnum.PreviewLink => Resource.Drawable.icon_newspaper,
                SettingsSymbolsEnum.PostUpdate => Resource.Drawable.icon_newspaper,
                SettingsSymbolsEnum.Manage => Resource.Drawable.icon_info,
                SettingsSymbolsEnum.Contact => Resource.Drawable.icon_account,
                SettingsSymbolsEnum.Placeholder => Resource.Drawable.icon_placeholder,
                SettingsSymbolsEnum.Important => Resource.Drawable.icon_notification,
                SettingsSymbolsEnum.SwitchApps => Resource.Drawable.icon_ads,
                SettingsSymbolsEnum.ContactInfo => Resource.Drawable.icon_feeds,
                SettingsSymbolsEnum.Lightbulb => Resource.Drawable.icon_bulb,
                SettingsSymbolsEnum.Rocket => Resource.Drawable.octo,
                _ => Resource.Drawable.icon_settings
            };
        }

        public override int LayoutResourceId => Resource.Layout.SettingsPageHomepage;

        #region Views

        private LinearLayout _settingsPageHomepageList;

        public LinearLayout SettingsPageHomepageList => GetView(ref _settingsPageHomepageList, Resource.Id.SettingsPageHomepageList);

        #endregion
    }
}

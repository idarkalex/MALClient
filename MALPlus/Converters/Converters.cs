using System.Globalization;
using MALClient.XShared.Utils;

namespace MALPlus.Converters;

public class StringToImageSourceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string url && !string.IsNullOrWhiteSpace(url))
            return new UriImageSource { Uri = new Uri(url), CachingEnabled = true, CacheValidity = TimeSpan.FromDays(30) };
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class ScoreVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is float f && f > 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class TabColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int currentTab && parameter is string tabParam && int.TryParse(tabParam, out int tabIndex))
        {
            return currentTab == tabIndex ? "#FF6B00" : "#FFFFFF";
        }
        return "#FFFFFF";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            if (parameter is string param && param == "nonempty")
                return !string.IsNullOrWhiteSpace(s);
            if (parameter is string param2 && param2 == "hasitems")
            {
                if (value is System.Collections.IEnumerable list)
                {
                    foreach (var _ in list)
                        return true;
                }
                return false;
            }
            return !string.IsNullOrEmpty(s);
        }
        if (value is bool b)
        {
            if (parameter is string param3 && param3 == "invert")
                return !b;
            return b;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class IntEqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int current && parameter is string param && int.TryParse(param, out int target))
            return current == target;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class AirCountdownVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isAnime && parameter is string p && p == "airing")
            return isAnime;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class BoolTabColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool current && parameter is string param && bool.TryParse(param, out bool target))
        {
            return current == target ? "#FF6B00" : "#FFFFFF";
        }
        return "#FFFFFF";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class HtmlSourceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string html && !string.IsNullOrWhiteSpace(html))
        {
            return new HtmlWebViewSource { Html = html };
        }
        return new HtmlWebViewSource { Html = "" };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class AiringStatusVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return AirTimeUtils.IsCurrentlyAiringStatus(value as string);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class CountdownTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MALClient.XShared.ViewModels.AnimeItemViewModel vm)
        {
            var _ = vm.AirDayBrush;
            return vm.AirDayTillBind ?? "";
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class CountdownVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MALClient.XShared.ViewModels.AnimeItemViewModel vm)
        {
            var _ = vm.AirDayBrush;
            return !string.IsNullOrEmpty(vm.AirDayTillBind);
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

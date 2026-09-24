using System.Globalization;
using MALClient.Models.Enums;
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
                if (parameter is string param3 && param3 == "longtext")
                    return s.Length > 200;
                if (parameter is string param4 && param4 == "spoiler")
                    return s.Contains("spoiler", StringComparison.OrdinalIgnoreCase) || s.Contains("spoil", StringComparison.OrdinalIgnoreCase);
                if (parameter is string param5 && param5 == "voice")
                    return s.Contains("voice", StringComparison.OrdinalIgnoreCase) || s.Contains("seiyuu", StringComparison.OrdinalIgnoreCase) || s.Contains("voice actor", StringComparison.OrdinalIgnoreCase) || s.Contains("seiyu", StringComparison.OrdinalIgnoreCase);
                return !string.IsNullOrEmpty(s);
            }
            if (value is bool b)
            {
                if (parameter is string param3 && param3 == "invert")
                    return !b;
                return b;
            }
            if (value is int count)
            {
                if (parameter is string param && param == "hasitems")
                    return count > 0;
                return count != 0;
            }
            if (value is System.Enum en)
            {
                // RelatedItemType.Anime=0 (default) → hidden; Manga/Unknown (nonzero) → visible
                return System.Convert.ToInt32(en) != 0;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

public class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var v = value is int i ? i : 0;
        if (parameter is string p && p == "zero") return v <= 0;
        return v > 0;
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

public class MultiAndConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        return values.All(v => v is bool b && b);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class RelationTypeToBadgeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is RelatedItemType rt)
        {
            return rt switch
            {
                RelatedItemType.Manga => "Manga",
                RelatedItemType.Anime => "",
                _ => ""
            };
        }
        if (value is string rel && !string.IsNullOrWhiteSpace(rel))
        {
            var lower = rel.ToLowerInvariant();
            return lower switch
            {
                "prequel" => "Prequel",
                "sequel" => "Sequel",
                "parent story" => "Parent Story",
                "side story" => "Side Story",
                "alternative version" => "Alternative Version",
                "alternative setting" => "Alternative Setting",
                "full story" => "Full Story",
                "summary" => "Summary",
                "other" => "Other",
                _ => rel
            };
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class RelationBadgeColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return "#0066FF";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

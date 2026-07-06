using System.Globalization;

namespace FolderGate.Core.Formatting;

public static class LocalTimeFormatter
{
    public const string DisplayFormat = "yyyy-MM-dd HH:mm:ss";

    public static string FormatLocal(DateTimeOffset utcValue)
    {
        return FormatLocal(utcValue, TimeZoneInfo.Local, "Local");
    }

    public static string FormatLocal(DateTimeOffset utcValue, TimeZoneInfo timeZone, string timeZoneLabel)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        DateTimeOffset localValue = TimeZoneInfo.ConvertTime(utcValue.ToUniversalTime(), timeZone);
        string label = string.IsNullOrWhiteSpace(timeZoneLabel) ? "Local" : timeZoneLabel.Trim();
        string localText = localValue.ToString(DisplayFormat, CultureInfo.InvariantCulture);
        return string.Create(CultureInfo.InvariantCulture, $"{localText} ({label})");
    }
}

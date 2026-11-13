using System.Globalization;

namespace WinToolbar;

public static class Formatting
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Bytes(long bytes, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        // B dan KB tanpa desimal; MB ke atas makin presisi saat angkanya kecil.
        var format = unit < 2 || value >= 100 ? "0" : value >= 10 ? "0.0" : "0.00";
        return value.ToString(format, culture) + " " + Units[unit];
    }

    public static string Speed(double bytesPerSecond, CultureInfo? culture = null) =>
        Bytes((long)Math.Round(bytesPerSecond), culture) + "/s";

    public static string Duration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
        return $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}";
    }
}

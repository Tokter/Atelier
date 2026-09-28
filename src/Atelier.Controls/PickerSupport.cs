using System;
using System.Globalization;
using System.Text;

namespace Atelier.Controls;

/// <summary>
/// The style keys of the parts of the date and time pickers, so themes can color them (for example MD3 uses
/// on-surface-variant for the small title and outline-variant for the divider).
/// </summary>
public static class PickerStyleKeys
{
    /// <summary>The small title at the top of a picker dialog, such as "Select date" (a <see cref="TextBlock"/>).</summary>
    public const string Title = "PickerTitle";

    /// <summary>The large headline of a picker dialog, such as "Mon, Aug 17" (a <see cref="TextBlock"/>).</summary>
    public const string Headline = "PickerHeadline";

    /// <summary>The divider below a picker dialog's header (a <see cref="Layout.Border"/> 1 px high).</summary>
    public const string Divider = "PickerDivider";

    /// <summary>The icon buttons and menu buttons of the pickers' headers (a <see cref="Button"/>).</summary>
    public const string HeaderButton = Calendar.HeaderButtonStyleKey;

    /// <summary>The popup of a docked date picker (a <see cref="Popup"/>).</summary>
    public const string DockedPopup = "DockedDatePickerPopup";
}

/// <summary>Formats and parses the dates and times shown by the pickers.</summary>
public static class PickerFormat
{
    /// <summary>
    /// Gets the numeric date pattern of <paramref name="culture"/> with a two-digit day and month and a four-digit year,
    /// such as <c>MM/dd/yyyy</c> (en-US) or <c>dd.MM.yyyy</c> (de-DE). Cultures whose short date names the month fall back
    /// to <c>yyyy-MM-dd</c>.
    /// </summary>
    public static string GetDatePattern(CultureInfo? culture = null)
    {
        string pattern = (culture ?? CultureInfo.CurrentCulture).DateTimeFormat.ShortDatePattern;
        var result = new StringBuilder();
        for (int i = 0; i < pattern.Length;)
        {
            char c = pattern[i];
            int run = 1;
            while (i + run < pattern.Length && pattern[i + run] == c) run++;

            switch (c)
            {
                case 'M' when run <= 2: result.Append("MM"); break;
                case 'd' when run <= 2: result.Append("dd"); break;
                case 'y': result.Append("yyyy"); break;
                case 'M' or 'd' or 'g': return "yyyy-MM-dd"; // month or day names, eras
                case '\'' or '"':
                    int end = pattern.IndexOf(c, i + 1);
                    end = end < 0 ? pattern.Length - 1 : end;
                    result.Append(pattern, i, end - i + 1);
                    i = end + 1;
                    continue;
                default: result.Append(c, run); break;
            }
            i += run;
        }
        return result.ToString();
    }

    /// <summary>Gets the placeholder for a date pattern, such as <c>MM/DD/YYYY</c> for <c>MM/dd/yyyy</c>.</summary>
    public static string GetDatePlaceholder(string pattern) => pattern.Replace("'", "").ToUpperInvariant();

    /// <summary>
    /// Parses a date typed as <paramref name="pattern"/>; the culture's other date formats are accepted too, so
    /// <c>7/4/2026</c> works with <c>MM/dd/yyyy</c>.
    /// </summary>
    public static bool TryParseDate(string text, string pattern, out DateOnly date, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        text = text.Trim();
        return DateOnly.TryParseExact(text, [pattern, culture.DateTimeFormat.ShortDatePattern], culture, DateTimeStyles.AllowWhiteSpaces, out date)
               || DateOnly.TryParse(text, culture, DateTimeStyles.AllowWhiteSpaces, out date);
    }

    /// <summary>Gets whether <paramref name="culture"/> shows times on a 24-hour clock.</summary>
    public static bool Uses24HourClock(CultureInfo? culture = null) =>
        (culture ?? CultureInfo.CurrentCulture).DateTimeFormat.ShortTimePattern.Contains('H');

    /// <summary>Gets the AM designator of <paramref name="culture"/>, or "AM" when it has none.</summary>
    public static string GetAmDesignator(CultureInfo? culture = null) =>
        (culture ?? CultureInfo.CurrentCulture).DateTimeFormat.AMDesignator is { Length: > 0 } am ? am : "AM";

    /// <summary>Gets the PM designator of <paramref name="culture"/>, or "PM" when it has none.</summary>
    public static string GetPmDesignator(CultureInfo? culture = null) =>
        (culture ?? CultureInfo.CurrentCulture).DateTimeFormat.PMDesignator is { Length: > 0 } pm ? pm : "PM";

    /// <summary>Formats a time like the time picker field: <c>7:05 PM</c> or <c>19:05</c>.</summary>
    public static string FormatTime(TimeOnly time, bool is24Hour, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (is24Hour)
        {
            return time.ToString("HH:mm", culture);
        }
        int hour = time.Hour % 12 == 0 ? 12 : time.Hour % 12;
        string period = time.Hour < 12 ? GetAmDesignator(culture) : GetPmDesignator(culture);
        return string.Create(culture, $"{hour}:{time.Minute:00} {period}");
    }

    /// <summary>
    /// Parses a typed time such as <c>7:05 PM</c>, <c>7:05p</c>, <c>19:05</c>, <c>1905</c> or <c>7 pm</c>.
    /// </summary>
    public static bool TryParseTime(string text, out TimeOnly time, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        time = default;
        string s = text.Trim();
        if (s.Length == 0) return false;

        // A trailing period designator: the culture's, or a/am/p/pm.
        bool? pm = null;
        string lower = s.ToLowerInvariant();
        foreach (var (designator, isPm) in new[] { (GetPmDesignator(culture), true), (GetAmDesignator(culture), false), ("pm", true), ("am", false), ("p", true), ("a", false) })
        {
            string d = designator.ToLowerInvariant();
            if (d.Length > 0 && lower.EndsWith(d, StringComparison.Ordinal))
            {
                pm = isPm;
                s = s[..^d.Length].Trim().TrimEnd('.');
                break;
            }
        }

        int hour, minute = 0;
        int colon = s.IndexOfAny([':', '.', 'h']);
        if (colon >= 0)
        {
            if (!int.TryParse(s[..colon], NumberStyles.None, culture, out hour)) return false;
            string rest = s[(colon + 1)..].Trim();
            if (rest.Length > 0 && !int.TryParse(rest, NumberStyles.None, culture, out minute)) return false;
        }
        else if (s.Length is 3 or 4 && int.TryParse(s, NumberStyles.None, culture, out int digits))
        {
            hour = digits / 100;
            minute = digits % 100;
        }
        else if (!int.TryParse(s, NumberStyles.None, culture, out hour))
        {
            return false;
        }

        if (minute is < 0 or > 59) return false;
        if (pm is { } isAfternoon)
        {
            if (hour is < 1 or > 12) return false;
            hour = hour % 12 + (isAfternoon ? 12 : 0);
        }
        else if (hour is < 0 or > 23)
        {
            return false;
        }

        time = new TimeOnly(hour, minute);
        return true;
    }
}

using System;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// A day of a <see cref="Calendar"/>'s month grid: a 40×40 button showing the day number.
/// </summary>
/// <remarks>
/// Themes draw the selected day filled (MD3: primary), today outlined, and the other days as plain text. Days outside
/// the calendar's date range are disabled.
/// </remarks>
public class CalendarDayButton : ButtonBase
{
    private readonly TextBlock _label = new() { FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private bool _isSelected;
    private bool _isToday;

    /// <summary>Initializes a day button.</summary>
    public CalendarDayButton()
    {
        Width = 40;
        Height = 40;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        CornerRadius = new CornerRadius(20);
        Content = _label;
    }

    /// <summary>Gets the date the button stands for.</summary>
    public DateOnly Date { get; private set; }

    /// <summary>Gets whether this is the selected date.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        internal set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            InvalidateVisual();
        }
    }

    /// <summary>Gets whether this is today's date.</summary>
    public bool IsToday
    {
        get => _isToday;
        internal set
        {
            if (_isToday == value) return;
            _isToday = value;
            InvalidateVisual();
        }
    }

    internal void SetDate(DateOnly date, string text)
    {
        Date = date;
        _label.Text = text;
    }
}

/// <summary>A year in a <see cref="Calendar"/>'s year grid: a 72×36 pill showing the year.</summary>
/// <remarks>Themes draw the selected year filled and the current year outlined.</remarks>
public class CalendarYearButton : ButtonBase
{
    private bool _isSelected;
    private bool _isCurrent;

    /// <summary>Initializes a year button for <paramref name="year"/>.</summary>
    public CalendarYearButton(int year)
    {
        Year = year;
        Width = 72;
        Height = 36;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        CornerRadius = new CornerRadius(18);
        Content = new TextBlock { Text = year.ToString(System.Globalization.CultureInfo.CurrentCulture), FontSize = 16 };
    }

    /// <summary>Gets the year.</summary>
    public int Year { get; }

    /// <summary>Gets whether this is the year of the selected date (or of the shown month without a selection).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        internal set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            InvalidateVisual();
        }
    }

    /// <summary>Gets whether this is the current year.</summary>
    public bool IsCurrent
    {
        get => _isCurrent;
        internal set
        {
            if (_isCurrent == value) return;
            _isCurrent = value;
            InvalidateVisual();
        }
    }
}

/// <summary>
/// An entry of a docked <see cref="Calendar"/>'s month or year menu: a 48 px high row with the text, and a check mark
/// before the selected entry.
/// </summary>
public class CalendarMenuItem : ButtonBase
{
    private readonly Icon _check = new(MaterialIconKind.Check, 24) { Visibility = Visibility.Hidden, VerticalAlignment = VerticalAlignment.Center };
    private bool _isSelected;

    /// <summary>Initializes a menu entry showing <paramref name="text"/>, for <paramref name="value"/>.</summary>
    public CalendarMenuItem(string text, int value)
    {
        Value = value;
        Height = 48;
        Padding = new Thickness(16, 0);
        VerticalContentAlignment = VerticalAlignment.Center;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        row.Add(_check);
        row.Add(new TextBlock { Text = text, FontSize = 16, VerticalAlignment = VerticalAlignment.Center });
        Content = row;
    }

    /// <summary>Gets the month (1–12) or year the entry stands for.</summary>
    public int Value { get; }

    /// <summary>Gets whether the entry is the current choice; it shows a check mark.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        internal set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            _check.Visibility = value ? Visibility.Visible : Visibility.Hidden;
            InvalidateVisual();
        }
    }
}

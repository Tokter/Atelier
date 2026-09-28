using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;
using Calendar = Atelier.Controls.Calendar;

namespace Atelier.Tests;

/// <summary>A clock fixed at one local time (UTC), for "today" and "now".</summary>
internal sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>Runs posted continuations at once on the posting thread, like the UI thread's context does later.</summary>
internal sealed class InlineSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state) => d(state);
}

/// <summary>Sets en-US, a fixed today (Sunday, September 27, 2026 at 15:20) and an inline sync context for a test.</summary>
public abstract class PickerTestBase : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly TimeProvider _time = Calendar.TimeProvider;
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    protected static readonly DateOnly Today = new(2026, 9, 27);

    protected PickerTestBase()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        Calendar.TimeProvider = new FixedTimeProvider(new DateTime(2026, 9, 27, 15, 20, 0, DateTimeKind.Utc));
        SynchronizationContext.SetSynchronizationContext(new InlineSynchronizationContext());
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        Calendar.TimeProvider = _time;
        SynchronizationContext.SetSynchronizationContext(_context);
        GC.SuppressFinalize(this);
    }

    protected static void Layout(UIElement root, float width = 800, float height = 700)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
    }

    protected static KeyEventArgs Key(Key key, ModifierKeys modifiers = ModifierKeys.None) => new(key, 0, modifiers, true);

    // A key press on an element, tunneling and bubbling like real input.
    protected static void Press(UIElement target, Key key, ModifierKeys modifiers = ModifierKeys.None) =>
        target.DispatchKeyEvent(Key(key, modifiers), static (el, a) => el.OnPreviewKeyDown(a), static (el, a) => el.OnKeyDown(a));

    protected static void Type(UIElement target, string text) =>
        target.DispatchKeyEvent(new TextInputEventArgs(text), static (el, a) => el.OnPreviewTextInput(a), static (el, a) => el.OnTextInput(a));
}

public class PickerFormatTests : PickerTestBase
{
    [Theory]
    [InlineData("en-US", "MM/dd/yyyy")]
    [InlineData("de-DE", "dd.MM.yyyy")]
    [InlineData("en-GB", "dd/MM/yyyy")]
    [InlineData("ja-JP", "yyyy/MM/dd")]
    public void DatePattern_HasTwoDigitDayAndMonth(string culture, string expected) =>
        Assert.Equal(expected, PickerFormat.GetDatePattern(CultureInfo.GetCultureInfo(culture)));

    [Fact]
    public void ParsesDates_InThePatternAndLoosely()
    {
        Assert.True(PickerFormat.TryParseDate("07/04/2026", "MM/dd/yyyy", out var date));
        Assert.Equal(new DateOnly(2026, 7, 4), date);
        Assert.True(PickerFormat.TryParseDate("7/4/2026", "MM/dd/yyyy", out date));
        Assert.Equal(new DateOnly(2026, 7, 4), date);
        Assert.False(PickerFormat.TryParseDate("13/45/2026", "MM/dd/yyyy", out _));
    }

    [Theory]
    [InlineData("7:05 PM", 19, 5)]
    [InlineData("7:05pm", 19, 5)]
    [InlineData("7p", 19, 0)]
    [InlineData("12 am", 0, 0)]
    [InlineData("12:30 PM", 12, 30)]
    [InlineData("19:05", 19, 5)]
    [InlineData("1905", 19, 5)]
    [InlineData("905", 9, 5)]
    [InlineData("0", 0, 0)]
    public void ParsesTimes(string text, int hour, int minute)
    {
        Assert.True(PickerFormat.TryParseTime(text, out var time));
        Assert.Equal(new TimeOnly(hour, minute), time);
    }

    [Theory]
    [InlineData("25:00")]
    [InlineData("13 pm")]
    [InlineData("7:60")]
    [InlineData("abc")]
    public void RejectsInvalidTimes(string text) => Assert.False(PickerFormat.TryParseTime(text, out _));

    [Fact]
    public void FormatsTimes()
    {
        Assert.Equal("7:05 PM", PickerFormat.FormatTime(new TimeOnly(19, 5), is24Hour: false));
        Assert.Equal("12:00 AM", PickerFormat.FormatTime(new TimeOnly(0, 0), is24Hour: false));
        Assert.Equal("19:05", PickerFormat.FormatTime(new TimeOnly(19, 5), is24Hour: true));
    }
}

public class CalendarTests : PickerTestBase
{
    private static Calendar Create(Action<Calendar>? setup = null)
    {
        var calendar = new Calendar();
        setup?.Invoke(calendar);
        calendar.AttachToHost();
        Layout(calendar);
        return calendar;
    }

    [Fact]
    public void ShowsTheCurrentMonth_WithTodayMarked_AndTheWeekStartingOnSunday()
    {
        var calendar = Create();
        Assert.Equal(new DateOnly(2026, 9, 1), calendar.DisplayDate);

        // September 1, 2026 is a Tuesday: the third cell of the first row.
        var first = calendar.GetDayButton(new DateOnly(2026, 9, 1))!;
        var grid = (UniformGrid)first.Parent!;
        Assert.Equal(2, grid.Children.ToList().IndexOf(first));
        Assert.True(calendar.GetDayButton(Today)!.IsToday);
        Assert.Null(calendar.GetDayButton(new DateOnly(2026, 10, 1)));
        calendar.DetachFromHost();
    }

    [Fact]
    public void FirstDayOfWeek_ShiftsTheGrid()
    {
        var calendar = Create(c => c.FirstDayOfWeek = DayOfWeek.Monday);
        var first = calendar.GetDayButton(new DateOnly(2026, 9, 1))!;
        Assert.Equal(1, ((UniformGrid)first.Parent!).Children.ToList().IndexOf(first));
        calendar.DetachFromHost();
    }

    [Fact]
    public void ClickingADay_SelectsIt()
    {
        var calendar = Create();
        DateOnly? changed = null;
        calendar.SelectedDateChanged += (_, d) => changed = d;

        var day = calendar.GetDayButton(new DateOnly(2026, 9, 15))!;
        day.OnPointerEntered(new PointerEventArgs(Point.Zero, Point.Zero));
        day.OnPointerPressed(new PointerEventArgs(new Point(5, 5), new Point(5, 5), PointerButtons.Left));
        day.OnPointerReleased(new PointerEventArgs(new Point(5, 5), new Point(5, 5), PointerButtons.Left));

        Assert.Equal(new DateOnly(2026, 9, 15), calendar.SelectedDate);
        Assert.Equal(calendar.SelectedDate, changed);
        Assert.True(day.IsSelected);
        calendar.DetachFromHost();
    }

    [Fact]
    public void ArrowKeys_MoveTheFocus_AcrossMonths_AndEnterSelects()
    {
        var calendar = Create(c => c.SelectedDate = new DateOnly(2026, 9, 29));
        calendar.MoveFocusTo(calendar.FocusDate);
        Assert.True(calendar.GetDayButton(new DateOnly(2026, 9, 29))!.IsFocused);

        Press(calendar.GetDayButton(calendar.FocusDate)!, Core.Events.Key.Down);
        Assert.Equal(new DateOnly(2026, 10, 6), calendar.FocusDate);
        Assert.Equal(new DateOnly(2026, 10, 1), calendar.DisplayDate);
        Assert.True(calendar.GetDayButton(new DateOnly(2026, 10, 6))!.IsFocused);

        Press(calendar.GetDayButton(calendar.FocusDate)!, Core.Events.Key.PageDown, ModifierKeys.Shift);
        Assert.Equal(new DateOnly(2027, 10, 6), calendar.FocusDate);

        Press(calendar.GetDayButton(calendar.FocusDate)!, Core.Events.Key.Enter);
        Press(calendar.GetDayButton(calendar.FocusDate)!, Core.Events.Key.Enter);
        Assert.Equal(new DateOnly(2027, 10, 6), calendar.SelectedDate);

        // Only the focused day is a tab stop.
        Assert.Single(Enumerable.Range(1, 31).Select(d => calendar.GetDayButton(new DateOnly(2027, 10, d))), b => b is { IsFocusable: true });
        calendar.DetachFromHost();
    }

    [Fact]
    public void DatesOutsideTheRange_AreDisabled_AndTheMonthStaysInRange()
    {
        var calendar = Create(c =>
        {
            c.MinDate = new DateOnly(2026, 9, 10);
            c.MaxDate = new DateOnly(2026, 10, 20);
        });

        Assert.False(calendar.GetDayButton(new DateOnly(2026, 9, 9))!.IsEnabled);
        Assert.True(calendar.GetDayButton(new DateOnly(2026, 9, 10))!.IsEnabled);

        calendar.DisplayDate = new DateOnly(2030, 1, 1);
        Assert.Equal(new DateOnly(2026, 10, 1), calendar.DisplayDate);
        Assert.False(calendar.GetDayButton(new DateOnly(2026, 10, 21))!.IsEnabled);
        calendar.DetachFromHost();
    }

    [Fact]
    public void YearGrid_PicksTheYear_AndReturnsToTheDays()
    {
        var calendar = Create(c => c.SelectedDate = new DateOnly(2026, 3, 14));
        calendar.View = CalendarView.Years;
        var year = calendar.YearButtons.Single(b => b.Year == 2031);
        Assert.True(calendar.YearButtons.Single(b => b.Year == 2026).IsSelected);

        year.OnPointerEntered(new PointerEventArgs(Point.Zero, Point.Zero));
        year.OnPointerPressed(new PointerEventArgs(new Point(5, 5), new Point(5, 5), PointerButtons.Left));
        year.OnPointerReleased(new PointerEventArgs(new Point(5, 5), new Point(5, 5), PointerButtons.Left));

        Assert.Equal(CalendarView.Days, calendar.View);
        Assert.Equal(new DateOnly(2031, 3, 1), calendar.DisplayDate);
        Assert.Equal(new DateOnly(2026, 3, 14), calendar.SelectedDate); // picking a year only navigates
        calendar.DetachFromHost();
    }
}

public class DatePickerTests : PickerTestBase
{
    [Fact]
    public void TypedDates_AreAppliedOnEnter_AndInvalidOnesShowAnError()
    {
        var picker = new DatePicker();
        picker.AttachToHost();
        Assert.Equal("MM/DD/YYYY", picker.TextBox.Placeholder);

        picker.TextBox.Text = "7/4/2026";
        Press(picker.TextBox, Core.Events.Key.Enter);
        Assert.Equal(new DateOnly(2026, 7, 4), picker.SelectedDate);
        Assert.Equal("07/04/2026", picker.TextBox.Text);

        picker.TextBox.Text = "31/31/2026";
        Press(picker.TextBox, Core.Events.Key.Enter);
        Assert.True(Validation.GetHasError(picker.TextBox));
        Assert.Equal(new DateOnly(2026, 7, 4), picker.SelectedDate);

        picker.TextBox.Text = "";
        Assert.False(Validation.GetHasError(picker.TextBox));
        Press(picker.TextBox, Core.Events.Key.Enter);
        Assert.Null(picker.SelectedDate);
        picker.DetachFromHost();
    }

    [Fact]
    public void Docked_OpensBelowTheField_AndOkApplies()
    {
        var picker = new DatePicker { SelectedDate = new DateOnly(2026, 5, 1) };
        var root = new StackPanel().WithChildrenForTest(picker);
        root.AttachToHost();
        Layout(root);

        picker.OpenPicker();
        Assert.True(picker.IsDockedPickerOpen);
        Assert.Equal(new DateOnly(2026, 5, 1), picker.DockedCalendar.DisplayDate);

        picker.DockedCalendar.SelectedDate = new DateOnly(2026, 5, 20);
        Assert.Equal(new DateOnly(2026, 5, 1), picker.SelectedDate); // not before OK

        var ok = FindButton(picker.DockedCalendar.Parent!, "OK");
        ok.OnPointerEntered(new PointerEventArgs(Point.Zero, Point.Zero));
        ok.OnPointerPressed(new PointerEventArgs(new Point(5, 5), new Point(5, 5), PointerButtons.Left));
        ok.OnPointerReleased(new PointerEventArgs(new Point(5, 5), new Point(5, 5), PointerButtons.Left));
        Assert.Equal(new DateOnly(2026, 5, 20), picker.SelectedDate);
        Assert.False(picker.IsDockedPickerOpen);
        root.DetachFromHost();
    }

    [Theory]
    [InlineData(DatePickerMode.Modal, DatePickerInputMode.Calendar)]
    [InlineData(DatePickerMode.ModalInput, DatePickerInputMode.Input)]
    public void ModalModes_OpenADialog_AndOkApplies(DatePickerMode mode, DatePickerInputMode expectedInputMode)
    {
        var picker = new DatePicker { Mode = mode };
        var host = new DialogHost { Content = picker };
        host.AttachToHost();
        Layout(host);

        picker.OpenPicker();
        var dialog = picker.OpenDialog!;
        Assert.Equal(expectedInputMode, dialog.InputMode);

        if (mode == DatePickerMode.Modal)
        {
            dialog.Calendar.SelectedDate = new DateOnly(2026, 9, 3);
        }
        else
        {
            dialog.InputBox.Text = "09/03/2026";
        }
        dialog.Confirm();

        Assert.Null(picker.OpenDialog);
        Assert.Equal(new DateOnly(2026, 9, 3), picker.SelectedDate);
        Assert.Equal("09/03/2026", picker.TextBox.Text);
        host.DetachFromHost();
    }

    private static Button FindButton(VisualNode root, string text) =>
        Descendants(root).OfType<Button>().First(b => b.Content is TextBlock { Text: var t } && t == text);

    private static System.Collections.Generic.IEnumerable<VisualNode> Descendants(VisualNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}

public class DatePickerDialogTests : PickerTestBase
{
    private static (DialogHost Host, DatePickerDialog Dialog, System.Threading.Tasks.Task<DialogResponse> Task) Show(DatePickerDialog dialog)
    {
        var host = new DialogHost { Content = new Border() };
        host.AttachToHost();
        Layout(host);
        return (host, dialog, dialog.ShowAsync(host));
    }

    [Fact]
    public void Headline_ShowsTheChosenDate_AndTheModeButtonSwitchesToInput()
    {
        var (host, dialog, _) = Show(new DatePickerDialog { SelectedDate = new DateOnly(2026, 8, 17) });
        var headline = (TextBlock)FindByStyleKey(dialog, PickerStyleKeys.Headline);
        Assert.Equal("Mon, Aug 17", headline.Text);

        dialog.Calendar.SelectedDate = new DateOnly(2026, 8, 20);
        Assert.Equal("Thu, Aug 20", headline.Text);

        dialog.InputMode = DatePickerInputMode.Input;
        Assert.Equal("Enter date", headline.Text);
        Assert.Equal("08/20/2026", dialog.InputBox.Text);
        Assert.Equal(new DateOnly(2026, 8, 17), dialog.SelectedDate); // not before OK
        host.DetachFromHost();
    }

    [Fact]
    public async System.Threading.Tasks.Task InvalidOrOutOfRangeInput_KeepsTheDialogOpen_WithAnError()
    {
        var (host, dialog, task) = Show(new DatePickerDialog { InputMode = DatePickerInputMode.Input, MaxDate = new DateOnly(2026, 12, 31) });

        dialog.InputBox.Text = "02/30/2026";
        dialog.Confirm();
        Assert.True(dialog.IsOpen);
        Assert.StartsWith("Invalid format", Validation.GetErrors(dialog.InputBox)[0].ToString());

        dialog.InputBox.Text = "01/01/2027";
        Assert.False(Validation.GetHasError(dialog.InputBox));
        dialog.Confirm();
        Assert.Equal("Date out of range", Validation.GetErrors(dialog.InputBox)[0].ToString());

        dialog.InputBox.Text = "12/24/2026";
        Press(dialog.InputBox, Core.Events.Key.Enter);
        Assert.False(dialog.IsOpen);
        Assert.Equal(DialogResult.Ok, (await task).Result);
        Assert.Equal(new DateOnly(2026, 12, 24), dialog.SelectedDate);
        host.DetachFromHost();
    }

    [Fact]
    public async System.Threading.Tasks.Task Cancel_LeavesTheDate()
    {
        var (host, dialog, task) = Show(new DatePickerDialog { SelectedDate = Today });
        dialog.Calendar.SelectedDate = Today.AddDays(3);
        Press(dialog, Core.Events.Key.Escape);
        Assert.Equal(DialogResult.Cancel, (await task).Result);
        Assert.Equal(Today, dialog.SelectedDate);
        host.DetachFromHost();
    }

    private static UIElement FindByStyleKey(VisualNode root, string key)
    {
        foreach (var node in root.Children)
        {
            if (node is UIElement { StyleKey: var k } e && k == key) return e;
            if (node.Children.Count > 0)
            {
                try { return FindByStyleKey(node, key); } catch (InvalidOperationException) { }
            }
        }
        throw new InvalidOperationException(key);
    }
}

public class TimePickerTests : PickerTestBase
{
    private static ClockDial CreateDial(Action<ClockDial>? setup = null)
    {
        var dial = new ClockDial();
        setup?.Invoke(dial);
        dial.AttachToHost();
        Layout(dial, 256, 256);
        return dial;
    }

    [Fact]
    public void Dial_PicksHours_KeepingAmOrPm_AndMinutesExactly()
    {
        var dial = CreateDial(d => d.Value = 15);
        dial.PickAt(dial.GetPosition(90, isInner: false)); // 3 o'clock
        Assert.Equal(15, dial.Value);
        dial.PickAt(dial.GetPosition(300, isInner: false)); // 10 o'clock, still PM
        Assert.Equal(22, dial.Value);
        dial.PickAt(dial.GetPosition(0, isInner: false));
        Assert.Equal(12, dial.Value);

        dial.Mode = ClockDialMode.Minutes;
        dial.PickAt(dial.GetPosition(17 * 6, isInner: false));
        Assert.Equal(17, dial.Value);
        Assert.True(dial.IsHandleBetweenLabels);
        dial.DetachFromHost();
    }

    [Fact]
    public void Dial24_UsesTheInnerRingFor13To00()
    {
        var dial = CreateDial(d => d.Is24Hour = true);
        Assert.Equal(24, dial.Labels.Length);

        dial.PickAt(dial.GetPosition(90, isInner: true));
        Assert.Equal(15, dial.Value);
        Assert.True(dial.IsHandleInner);
        dial.PickAt(dial.GetPosition(0, isInner: true));
        Assert.Equal(0, dial.Value);
        dial.PickAt(dial.GetPosition(90, isInner: false));
        Assert.Equal(3, dial.Value);
        Assert.False(dial.IsHandleInner);
        dial.DetachFromHost();
    }

    [Fact]
    public void Dial_Keyboard_WrapsWithinTheHalfDay()
    {
        var dial = CreateDial(d => d.Value = 23);
        dial.OnKeyDown(Key(Core.Events.Key.Up));
        Assert.Equal(12, dial.Value); // 11 PM -> 12 PM on a 12-hour dial
        dial.OnKeyDown(Key(Core.Events.Key.Down));
        Assert.Equal(23, dial.Value);
        dial.DetachFromHost();
    }

    private static (DialogHost Host, TimePickerDialog Dialog, System.Threading.Tasks.Task<DialogResponse> Task) Show(TimePickerDialog dialog)
    {
        var host = new DialogHost { Content = new Border() };
        host.AttachToHost();
        Layout(host);
        return (host, dialog, dialog.ShowAsync(host));
    }

    [Fact]
    public async System.Threading.Tasks.Task DialMode_PicksTheHourThenTheMinute()
    {
        var (host, dialog, task) = Show(new TimePickerDialog { SelectedTime = new TimeOnly(7, 30), ClockFormat = ClockFormat.TwelveHour });
        Assert.Equal("07", dialog.HourBox.Text);
        Assert.True(dialog.HourBox.IsSelected);
        Assert.False(dialog.PeriodSelector.IsPm);

        var dial = dialog.Dial;
        dial.PickAt(dial.GetPosition(90, false));
        dial.OnPointerReleased(new PointerEventArgs(Point.Zero, Point.Zero, PointerButtons.Left)); // not dragging: ignored
        Assert.False(dialog.IsEditingMinutes);

        dial.OnPointerPressed(new PointerEventArgs(dial.GetPosition(90, false), dial.GetPosition(90, false), PointerButtons.Left));
        dial.OnPointerReleased(new PointerEventArgs(Point.Zero, Point.Zero, PointerButtons.Left));
        Assert.True(dialog.IsEditingMinutes);
        Assert.Equal(ClockDialMode.Minutes, dial.Mode);
        Assert.Equal(30, dial.Value);

        dial.PickAt(dial.GetPosition(20 * 6, false));
        dialog.PeriodSelector.IsPm = true;
        Assert.Equal(new TimeOnly(15, 20), dialog.CurrentTime);
        Assert.Equal("03", dialog.HourBox.Text);

        dialog.Confirm();
        Assert.Equal(DialogResult.Ok, (await task).Result);
        Assert.Equal(new TimeOnly(15, 20), dialog.SelectedTime);
        host.DetachFromHost();
    }

    [Fact]
    public void InputMode_TypesTheHour_MovesToTheMinute_AndRejectsInvalidHours()
    {
        var (host, dialog, _) = Show(new TimePickerDialog { SelectedTime = new TimeOnly(8, 0), InputMode = TimePickerInputMode.Input, ClockFormat = ClockFormat.TwelveHour });
        Assert.True(dialog.HourBox.IsEditable);

        dialog.HourBox.Focus();
        Type(dialog.HourBox, "1");
        Assert.Equal("1", dialog.HourBox.Text);
        Assert.True(dialog.HourBox.IsFocused); // 1 can still become 10, 11 or 12
        Type(dialog.HourBox, "3");
        Assert.Equal("03", dialog.HourBox.Text); // 13 isn't an hour on a 12-hour clock: starts over with 3, shown as 03 once done
        Assert.True(dialog.MinuteBox.IsFocused); // 3 can't be extended: moves on

        Type(dialog.MinuteBox, "45");
        Assert.Equal(new TimeOnly(3, 45), dialog.CurrentTime);

        Press(dialog.MinuteBox, Core.Events.Key.Up);
        Assert.Equal(new TimeOnly(3, 46), dialog.CurrentTime);
        host.DetachFromHost();
    }

    [Fact]
    public void Dialog_StartsAtTheCurrentTime_WithoutATime()
    {
        var dialog = new TimePickerDialog { ClockFormat = ClockFormat.TwentyFourHour };
        Assert.Equal(new TimeOnly(15, 20), dialog.CurrentTime);
        Assert.Equal("15", dialog.HourBox.Text);
        Assert.Equal(Visibility.Collapsed, dialog.PeriodSelector.Visibility);
    }

    [Fact]
    public void Field_ParsesTypedTimes_AndOpensTheDialog()
    {
        var picker = new TimePicker { ClockFormat = ClockFormat.TwelveHour };
        var host = new DialogHost { Content = picker };
        host.AttachToHost();
        Layout(host);

        picker.TextBox.Text = "7p";
        Press(picker.TextBox, Core.Events.Key.Enter);
        Assert.Equal(new TimeOnly(19, 0), picker.SelectedTime);
        Assert.Equal("7:00 PM", picker.TextBox.Text);

        picker.TextBox.Text = "nope";
        Press(picker.TextBox, Core.Events.Key.Enter);
        Assert.True(Validation.GetHasError(picker.TextBox));

        picker.OpenPicker();
        var dialog = picker.OpenDialog!;
        Assert.Equal(new TimeOnly(19, 0), dialog.CurrentTime);
        dialog.Dial.Value = 21;
        dialog.Confirm();
        Assert.Equal(new TimeOnly(21, 0), picker.SelectedTime);
        Assert.Equal("9:00 PM", picker.TextBox.Text);

        picker.ClockFormat = ClockFormat.TwentyFourHour;
        Assert.Equal("21:00", picker.TextBox.Text);
        host.DetachFromHost();
    }
}

public class TextBoxTrailingIconTests
{
    [Fact]
    public void ClickingTheTrailingIcon_RaisesTheEvent_WithoutFocusing()
    {
        var box = new TextBox { TrailingIconKind = MaterialIconKind.CalendarToday, Width = 200 };
        box.AttachToHost();
        box.Measure(new Size(200, 100));
        box.Arrange(new Rect(0, 0, 200, box.DesiredSize.Height));
        int clicks = 0;
        box.TrailingIconClick += (_, _) => clicks++;

        var icon = box.GetTrailingIconBounds();
        Assert.Equal(200 - 24, icon.X + icon.Width / 2);
        var center = new Point(icon.X + icon.Width / 2, icon.Y + icon.Height / 2);

        box.OnPointerMoved(new PointerEventArgs(center, center));
        Assert.True(box.IsTrailingIconHovered);
        box.OnPointerPressed(new PointerEventArgs(center, center, PointerButtons.Left));
        Assert.True(box.IsTrailingIconPressed);
        box.OnPointerReleased(new PointerEventArgs(center, center, PointerButtons.Left));

        Assert.Equal(1, clicks);
        Assert.False(box.IsFocused);
        Assert.True(box.GetViewportWidth() <= 200 - 16 - 16 - 36);

        // A press on the text still focuses and places the caret.
        box.OnPointerPressed(new PointerEventArgs(new Point(30, center.Y), new Point(30, center.Y), PointerButtons.Left));
        box.OnPointerReleased(new PointerEventArgs(new Point(30, center.Y), new Point(30, center.Y), PointerButtons.Left));
        Assert.True(box.IsFocused);
        Assert.Equal(1, clicks);
        box.DetachFromHost();
    }
}

internal static class TestPanelExtensions
{
    public static StackPanel WithChildrenForTest(this StackPanel panel, params UIElement[] children)
    {
        foreach (var child in children) panel.Add(child);
        return panel;
    }
}

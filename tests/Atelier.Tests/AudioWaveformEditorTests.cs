using Atelier.Audio;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Rendering;
using Atelier.Theming;
using Atelier.Theming.Material;

namespace Atelier.Tests;

public class WaveformGainTests
{
    [Fact]
    public void Fades_RiseAndFall_OverTheirStretch_InTheirCurve()
    {
        var linear = new WaveformGain(2, 1, 2, 4, 6, FadeCurve.Linear);
        Assert.Equal(0, linear.At(0.5));
        Assert.Equal(1, linear.At(1.5), 5); // half of the fade in, at a gain of 2
        Assert.Equal(2, linear.At(3));
        Assert.Equal(1, linear.At(5), 5);
        Assert.Equal(0, linear.At(6.5));

        var smooth = linear with { Curve = FadeCurve.Smooth, Gain = 1 };
        Assert.Equal(Math.Sqrt(0.5), smooth.At(1.5), 5); // equal power at the middle
        Assert.Equal(1, WaveformGain.Identity.At(-100));
        Assert.Equal(0.5, WaveformGain.FromDecibels(-6.0206f), 3);
        Assert.Equal(6.0206, WaveformGain.ToDecibels(2), 3);
    }
}

public class AudioWaveformEditorTests
{
    // A 4 s sine placed at 1 s on a timeline at 100 px/s (a beat is 50 px at 120 BPM), in an editor 800 × 120:
    // the sound is at x 100 to 500.
    private static (AudioWaveformEditor Editor, TimelineContext Song) Editor()
    {
        var samples = new float[48000 * 4];
        for (int i = 0; i < samples.Length; i++) samples[i] = (float)(0.8 * Math.Sin(2 * Math.PI * 110 * i / 48000));
        var song = new TimelineContext { PixelsPerSecond = 100 };
        var editor = new AudioWaveformEditor { Timeline = song, Source = new WaveformData([samples], 48000), StartTime = TimelinePosition.Seconds(1) };
        editor.Measure(new Size(800, 200));
        editor.Arrange(new Rect(0, 0, 800, editor.DesiredSize.Height));
        return (editor, song);
    }

    private static IDragOperation Drag(DragCommand command, AudioWaveformEditor editor, float x, float y) =>
        command.BeginDrag(new DragStart(editor, editor, editor, new Point(x, y), PointerButtons.Left, ModifierKeys.None))!;

    private static void Point(AudioWaveformEditor editor, float x, float y, int clicks = 1) =>
        editor.OnPreviewPointerPressed(new PointerEventArgs(new Point(x, y), new Point(x, y), PointerButtons.Left, clickCount: clicks));

    [Fact]
    public void TheEditor_IsItsDefaultHeight_AndFindsItsParts()
    {
        var (editor, _) = Editor();
        Assert.Equal(AudioWaveformEditor.DefaultHeight, editor.DesiredSize.Height);
        editor.FadeIn = 0.5;
        editor.FadeOut = 1;
        Assert.Equal((1.0, 5.0), (editor.AudibleStart, editor.AudibleEnd));
        Assert.Equal(AudioEditorPart.TrimStart, editor.PartAt(new Point(102, 60)));
        Assert.Equal(AudioEditorPart.TrimEnd, editor.PartAt(new Point(498, 60)));
        Assert.Equal(AudioEditorPart.Body, editor.PartAt(new Point(300, 60)));
        Assert.Equal(AudioEditorPart.None, editor.PartAt(new Point(700, 60)));
        Assert.Equal(AudioEditorPart.FadeIn, editor.PartAt(new Point(150, 8)));
        Assert.Equal(AudioEditorPart.FadeOut, editor.PartAt(new Point(400, 8)));
        Assert.Equal(AudioEditorPart.Gain, editor.PartAt(new Point(300, 8)));
        Assert.Equal(CursorType.SizeNorthSouth, editor.CursorAt(new Point(300, 8)));
        Assert.Equal(CursorType.IBeam, editor.CursorAt(new Point(300, 60)));
        Assert.Equal(CursorType.SizeWestEast, editor.CursorAt(new Point(102, 60)));
    }

    [Fact]
    public void DraggingTheEdges_TrimsTheSound_WhereItPlays_AndFadesShrinkToFit()
    {
        var (editor, _) = Editor();
        editor.FadeIn = 1;
        editor.FadeOut = 1.5;
        var drag = Drag(TimelineCommands.EditHandle, editor, 101, 60);
        drag.Update(new Point(201, 60), ModifierKeys.None);
        Assert.Equal(1, editor.TrimStart, 9); // snapped to 2 s on the timeline, 1 s into the sound
        Assert.Equal(1, editor.SoundStartSeconds); // the audio stays where it plays
        drag.Update(new Point(460, 60), ModifierKeys.None); // 4.6 s snaps to 4.625 s: 0.375 s left for 2.5 s of fades
        Assert.Equal(0.375, editor.FadeIn + editor.FadeOut, 6);
        Assert.Equal(1.5 / 2.5, editor.FadeOut / 0.375, 6); // both shrink alike
        drag.Cancel();
        Assert.Equal((0.0, 1.0, 1.5), (editor.TrimStart, editor.FadeIn, editor.FadeOut));

        drag = Drag(TimelineCommands.EditHandle, editor, 499, 60);
        drag.Update(new Point(750, 60), ModifierKeys.None); // not past the sound's end
        Assert.Equal(4, editor.EffectiveTrimEnd);
        drag.Update(new Point(351, 60), ModifierKeys.None);
        Assert.Equal(2.5, editor.TrimEnd, 9);
    }

    [Fact]
    public void DraggingTheHandles_SetsTheFades_AndTheGain()
    {
        var (editor, _) = Editor();
        var drag = Drag(TimelineCommands.EditHandle, editor, 100, 8); // the fade in's handle, at the start without a fade
        Assert.Equal(AudioEditorPart.FadeIn, editor.HighlightedPart);
        drag.Update(new Point(250, 8), ModifierKeys.None);
        Assert.Equal(1.5, editor.FadeIn, 9);

        drag = Drag(TimelineCommands.EditHandle, editor, 500, 8); // the fade out's handle
        drag.Update(new Point(450, 8), ModifierKeys.None);
        Assert.Equal(0.5, editor.FadeOut, 9);

        drag = Drag(TimelineCommands.EditHandle, editor, 300, 8); // the gain chip
        drag.Update(new Point(300, -12), ModifierKeys.None);
        Assert.Equal(5, editor.Gain, 3);
        Assert.Equal("+5.0 dB", editor.GainText);
        drag.Update(new Point(300, -12), ModifierKeys.Shift);
        Assert.Equal(0.5, editor.Gain, 3);
        drag.Update(new Point(300, 2000), ModifierKeys.None);
        Assert.Equal(AudioWaveformEditor.MinGain, editor.Gain);
        drag.Cancel();
        Assert.Equal(0, editor.Gain);
        Assert.Equal(new WaveformGain(1, 0, 1.5, 3.5, 4, FadeCurve.Linear), editor.GetGain());
    }

    [Fact]
    public void DraggingAcrossTheSound_SelectsIt_AndTrimToSelectionTrimsToIt()
    {
        var (editor, song) = Editor();
        Assert.Null(TimelineCommands.SelectRange.BeginDrag(new DragStart(editor, editor, editor, new Point(700, 60), PointerButtons.Left, ModifierKeys.None)));
        var drag = Drag(TimelineCommands.SelectRange, editor, 199, 60);
        drag.Complete(new Point(302, 60), ModifierKeys.None);
        Assert.Equal(new TimelineRange(TimelinePosition.Beats(4), TimelinePosition.Beats(6)), editor.Selection); // 2 s to 3 s
        Assert.Equal((1.0, 2.0), editor.GetSelectionInSource());

        Assert.True(TimelineCommands.TrimToSelection.CanExecute(editor));
        TimelineCommands.TrimToSelection.Execute(editor);
        Assert.Equal((1.0, 2.0), (editor.TrimStart, editor.TrimEnd));
        Assert.Null(editor.Selection);
        Assert.False(TimelineCommands.TrimToSelection.CanExecute(editor));
    }

    [Fact]
    public void Clicks_SetThePlayStart_SelectAll_OrReset()
    {
        var (editor, song) = Editor();
        editor.Selection = new TimelineRange(TimelinePosition.Seconds(2), TimelinePosition.Seconds(3));
        Point(editor, 301, 60);
        TimelineCommands.EditorClick.Execute(editor);
        Assert.Null(editor.Selection);
        Assert.Equal(TimelinePosition.Beats(6), song.PlayStart); // 3.01 s snapped to 3 s

        Point(editor, 300, 60, clicks: 2);
        TimelineCommands.EditorDoubleClick.Execute(editor);
        Assert.Equal(new TimelineRange(TimelinePosition.Beats(2), TimelinePosition.Beats(10)), editor.Selection);

        editor.Gain = 3;
        Point(editor, 300, 8, clicks: 2);
        TimelineCommands.EditorDoubleClick.Execute(editor);
        Assert.Equal(0, editor.Gain);

        TimelineCommands.ClearSelection.Execute(editor);
        Assert.Null(editor.Selection);
        TimelineCommands.SelectAllSound.Execute(editor);
        Assert.NotNull(editor.Selection);
    }

    [Fact]
    public void TheMenu_OffersTheEdits()
    {
        var (editor, _) = Editor();
        var items = editor.CreateContextMenu().Items.OfType<MenuItem>().ToList();
        Assert.Contains(items, i => i.Command == TimelineCommands.TrimToSelection);
        Assert.Contains(items, i => i.Header as string == "Smooth fades");
        Assert.False(items.Single(i => i.Header as string == "Reset gain").IsEnabled);
    }

    [Fact]
    public void TheWaveform_IsDrawnAtItsLevel_AndTheTrimmedPartsDimmed()
    {
        using var _ = ActiveTheme.Use(MaterialTheme.CreateDark());
        var (editor, _) = Editor();
        editor.Foreground = Color.White;
        editor.Background = Color.Black;
        editor.ShowRms = false;
        editor.TrimStart = 1;
        editor.FadeIn = 2; // 2 s to 4 s on the timeline: x 200 to 400

        using var bitmap = new SkiaSharp.SKBitmap(800, 120);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        using var paints = new PaintRegistry();
        var context = new DrawingContext(canvas, paints);
        VisualTreeRenderer.Render(editor, ref context, ThemeVisualPresenter.Instance);

        // How high the waveform reaches above the center in a column (bright pixels only).
        int Reach(int x, int threshold)
        {
            float center = AudioWaveformEditor.HandleStripHeight + (120 - AudioWaveformEditor.HandleStripHeight - 2) / 2f;
            int reach = 0;
            for (int y = (int)center; y > AudioWaveformEditor.HandleStripHeight; y--)
            {
                if (bitmap.GetPixel(x, y).Red > threshold) reach = (int)center - y;
            }
            return reach;
        }
        Assert.True(Reach(250, 200) < Reach(350, 200), "the fade in rises");
        Assert.True(Reach(450, 200) > Reach(350, 200) - 3, "full level after the fade");
        Assert.Equal(0, Reach(150, 200)); // trimmed away: not bright ...
        Assert.True(Reach(150, 30) > 20, "... but dimmed");
    }
}

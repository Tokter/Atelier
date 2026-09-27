using System.Linq;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class KeybindingView : GalleryPage
{
    private readonly KeybindingViewModel _vm;

    public KeybindingView(KeybindingViewModel viewModel)
        : base(MaterialIconKind.Keyboard, "Keybindings",
            "Keyboard shortcuts are declared with [Keybinding] attributes and registered at compile time by a source " +
            "generator. A KeybindingHandler runs the shortcuts of its group while the focus is inside it; keys it doesn't " +
            "handle bubble up to handlers further out, such as the window's \"Global\" handler.")
    {
        _vm = viewModel;

        // Global shortcuts declared on this view model (F5, Ctrl+Shift+L) find it through the data context.
        DataContext = _vm;

        Settings(
            new Button("Reset demos (F5)").Variant(ButtonVariant.Tonal).Command(_vm.ResetDemosCommand),
            new Button("Clear log (Ctrl+Shift+L)").Variant(ButtonVariant.Text).Command(_vm.ClearLogCommand));

        Sections(ScopesSection(), ProbeSection(), CatalogSection());
    }

    private UIElement ScopesSection() => Ui.Section("Scoped keybindings",
        "Each demo is wrapped in a KeybindingHandler with its own group. Click into a demo and use its shortcuts; F1 and " +
        "F5 still work there because unhandled keys bubble up to the Global handler.",
        Ui.Columns(360, EditorDemo(), PlayerDemo()),
        Ui.Demo("Executed keybindings",
            LogView(),
            Ui.Note("Every command writes a line here. F1 comes from a command class, the others from [RelayCommand] methods.")),
        Ui.Code("[RelayCommand]\n" +
                "[property: Keybinding(\"SaveDocument\", \"Editor\", \"Ctrl+S\")]\n" +
                "private void SaveDocument() { ... }\n\n" +
                "new KeybindingHandler(\"Editor\", editorContent).DataContext(editorViewModel)"));

    private UIElement EditorDemo()
    {
        var editor = _vm.Editor;
        var handler = new KeybindingHandler()
            .Group("Editor")
            .DataContext(editor)
            .Content(Ui.Stack(
                new TextBox()
                    .Label("Document")
                    .BindText(editor, e => e.DocumentText, (e, text) => e.DocumentText = text),
                new TextBlock()
                    .TextWrapping()
                    .BindText(editor, e => e.DocumentText.Length == 0 ? "(empty)" : e.DocumentText)
                    .BindBold(editor, e => e.IsBold)
                    .BindItalic(editor, e => e.IsItalic),
                Ui.Row(
                    Ui.Readout(editor, e => $"Saves: {e.SaveCount}"),
                    Ui.Readout(_vm, v => $"Pending chord: {v.PendingChord}"))));

        // PendingChord is updated while the handler processes a key, so read it once the key is released.
        handler.OnKeyUp((_, _) => _vm.PendingChord = handler.PendingChord ?? "none");

        return Ui.Demo("Editor (group \"Editor\")",
            Ui.Note("Click into the text field, then press Ctrl+S, Ctrl+B, Ctrl+I or Ctrl+Shift+K. Chords: Ctrl+K then " +
                    "Ctrl+U (upper case) or Ctrl+K then Ctrl+L (lower case)."),
            handler,
            Ui.SliderSetting("Chord timeout (s)", _vm, v => v.ChordTimeoutSeconds, (v, s) => v.ChordTimeoutSeconds = s, 1, 10, "0", 1));
    }

    private UIElement PlayerDemo()
    {
        var player = _vm.Player;
        var content = Ui.Stack(
            new Grid()
                .Columns(GridLength.Auto, GridLength.Star, GridLength.Auto)
                .ColumnSpacing(16)
                .Children(
                    new Icon(MaterialIconKind.PlayCircle, 40)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Themed(Control.ForegroundProperty, c => c.Primary)
                        .BindKind(player, p => p.IsPlaying ? MaterialIconKind.PauseCircle : MaterialIconKind.PlayCircle),
                    new StackPanel().Spacing(2).Column(1).VerticalAlignment(VerticalAlignment.Center).Children(
                        new TextBlock("Symphony No. 5").TitleSmall(),
                        new TextBlock("Ludwig van Beethoven").BodySmall().Muted()),
                    new Icon(MaterialIconKind.VolumeUp, 24)
                        .Column(2)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .BindKind(player, p => p.IsMuted ? MaterialIconKind.VolumeOff : MaterialIconKind.VolumeUp)),
            new ProgressBar().BindValue(player, p => p.Progress),
            new TextBlock().BodySmall().Muted().BindText(player, p => p.PositionDisplay));

        var frame = FocusFrame(content);
        return Ui.Demo("Player (group \"Player\")",
            Ui.Note("Click the player to focus it, then press Space (play/pause), Left/Right (seek), R (restart) or M (mute)."),
            new KeybindingHandler().Group("Player").DataContext(player).Content(frame));
    }

    private UIElement ProbeSection()
    {
        var probe = FocusFrame(Ui.Stack(
            new TextBlock("Click here and press any key combination").TitleSmall(),
            Ui.Row(
                Ui.Readout(_vm, v => $"Gesture: {v.ProbeGesture}"),
                Ui.Readout(_vm, v => $"Match: {v.ProbeMatch}"))));
        probe.OnKeyDown((_, e) => _vm.Probe(e.Key, e.Modifiers));

        return Ui.Section("Gesture probe",
            "KeybindingManager looks up keybindings by group and gesture. The probe shows which group, if any, would handle " +
            "a key press. Commands can also be run from code with KeybindingManager.TryExecuteGesture.",
            probe,
            Ui.Row(
                new Button("Run Ctrl+B on the editor").Variant(ButtonVariant.Outlined)
                    .OnClick(() => KeybindingManager.TryExecuteGesture("Editor", Key.B, ModifierKeys.Control, _vm.Editor)),
                new Button("Run Space on the player").Variant(ButtonVariant.Outlined)
                    .OnClick(() => KeybindingManager.TryExecuteGesture("Player", Key.Space, ModifierKeys.None, _vm.Player))));
    }

    private UIElement CatalogSection()
    {
        var bindings = KeybindingManager.RegisteredKeybindings.Values
            .SelectMany(group => group.Values)
            .OrderBy(b => b.Group == "Global" ? 0 : 1)
            .ThenBy(b => b.Group)
            .ThenBy(b => b.Name)
            .ToList();

        var table = new Grid()
            .Columns(GridLength.Auto, GridLength.Auto, GridLength.Star)
            .Spacing(32, 10)
            .Rows(Enumerable.Repeat(GridLength.Auto, bindings.Count + 1).ToArray());

        table.Add(new TextBlock("Group").LabelLarge().Muted());
        table.Add(new TextBlock("Command").LabelLarge().Muted().Column(1));
        table.Add(new TextBlock("Gesture").LabelLarge().Muted().Column(2));
        for (int i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            table.Add(new TextBlock(binding.Group).Row(i + 1));
            table.Add(new TextBlock(binding.Name).Row(i + 1).Column(1));
            table.Add(KeyCaps(binding.Keybinding).Row(i + 1).Column(2));
        }

        var conflicts = KeybindingManager.GetConflicts();
        string conflictText = conflicts.Count == 0
            ? "No conflicts: no gesture is bound twice or both alone and as a chord prefix in the same group."
            : "Conflicts: " + string.Join("; ", conflicts.Select(c =>
                $"{c.First.Name} and {c.Second.Name} in {c.Group}{(c.IsPrefix ? " (one is a prefix of the other)" : " (same gesture)")}"));

        return Ui.Section("Registered keybindings",
            "All keybindings the source generator registered, read from KeybindingManager.RegisteredKeybindings. " +
            "GetConflicts reports gestures bound twice within a group.",
            table,
            Ui.Note(conflictText));
    }

    private UIElement LogView() =>
        new Border()
            .Padding(12, 8)
            .MinHeight(96)
            .CornerRadius(8)
            .Themed(Border.BackgroundProperty, c => c.SurfaceContainerHigh)
            .Child(new ItemsControl()
                .BindItemsSource(_vm, v => v.Log)
                .WithItemTemplate((string line) => new TextBlock(line)
                    .FontFamily(Ui.MonospaceFont)
                    .FontSize(12)
                    .LineHeight(18)
                    .Themed(TextBlock.ForegroundProperty, c => c.OnSurfaceVariant)));

    /// <summary>A focusable frame whose outline turns to the primary color while it has the keyboard focus.</summary>
    private static Border FocusFrame(UIElement content)
    {
        var frame = new Border()
            .IsFocusable()
            .Padding(16)
            .CornerRadius(12)
            .BorderThickness(2)
            .Themed(Border.BackgroundProperty, c => c.SurfaceContainerLow)
            .Themed(Border.BorderBrushProperty, c => c.OutlineVariant)
            .Child(content);
        frame.OnPropertyChanged(UIElement.IsFocusedProperty, (_, _, focused) =>
            frame.BorderBrush = focused ? ThemeColors.Current.Primary : ThemeColors.Current.OutlineVariant);
        return frame;
    }

    /// <summary>A gesture shown as key caps, e.g. "Ctrl" "K".</summary>
    private static UIElement KeyCaps(string gesture)
    {
        var row = new WrapPanel().Spacing(4, 4);
        string[] strokes = gesture.Split(',', System.StringSplitOptions.TrimEntries | System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < strokes.Length; i++)
        {
            if (i > 0)
            {
                row.Add(new TextBlock("then").BodySmall().Muted().Margin(4, 0).VerticalAlignment(VerticalAlignment.Center));
            }

            foreach (string key in strokes[i].Split('+', System.StringSplitOptions.TrimEntries | System.StringSplitOptions.RemoveEmptyEntries))
            {
                row.Add(new Border()
                    .Padding(8, 2)
                    .CornerRadius(6)
                    .BorderThickness(1)
                    .Themed(Border.BorderBrushProperty, c => c.OutlineVariant)
                    .Themed(Border.BackgroundProperty, c => c.SurfaceContainerHighest)
                    .Child(new TextBlock(key).FontFamily(Ui.MonospaceFont).FontSize(12)));
            }
        }
        return row;
    }
}

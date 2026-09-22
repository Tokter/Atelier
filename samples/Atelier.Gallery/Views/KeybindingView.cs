using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Theming;
using Atelier.Theming.Material;

namespace Atelier.Gallery.Views;

public class KeybindingView : KeybindingHandler
{
    private readonly KeybindingViewModel _viewModel;

    // Editor visual elements
    private TextBlock _editorFormattedPreview = null!;
    private TextBlock _editorStatusText = null!;
    private Border _editorBoldBadge = null!;
    private Border _editorItalicBadge = null!;
    private Border _editorCaseBadge = null!;
    private TextBlock _editorSaveCountBadge = null!;

    // Player visual elements
    private TextBlock _playerStatusText = null!;
    private TextBlock _playerTimeText = null!;
    private Border _playerPlayStateBadge = null!;
    private TextBlock _playerPlayStateText = null!;
    private Slider _playerProgressSlider = null!;
    private Button _playerPlayBtn = null!;
    private Button _playerMuteBtn = null!;

    // Probe visual elements
    private TextBlock _probeKeyText = null!;
    private TextBlock _probeModifiersText = null!;
    private TextBlock _probeGestureText = null!;
    private TextBlock _probeScopeText = null!;
    private TextBlock _probeCommandText = null!;
    private Border _testerFocusBorder = null!;

    // Log elements
    private StackPanel _logListPanel = null!;

    // Banner message element
    private TextBlock _bannerHelpText = null!;

    public KeybindingView() : this(new KeybindingViewModel())
    {
    }

    public KeybindingView(KeybindingViewModel viewModel) : base("Global")
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        var rootGrid = new Grid();
        rootGrid.Rows(GridLength.Auto, GridLength.Star);
        rootGrid.RowSpacing(16);

        // 1. Master Header Banner
        rootGrid.Add(CreateMasterBanner().Row(0));

        // 2. Scrollable 2-Column Content Layout
        var scrollViewer = new ScrollViewer();
        var mainGrid = new Grid()
            .Columns(GridLength.Stars(1.15f), GridLength.Stars(1.0f))
            .ColumnSpacing(16);

        // Left Column: Scoped Interactive Zones
        var leftStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 16 };
        leftStack.Add(CreateEditorScopeCard());
        leftStack.Add(CreatePlayerScopeCard());
        leftStack.Add(CreateBubblingArchitectureCard());
        mainGrid.Add(leftStack.Column(0));

        // Right Column: Probe, Catalog, and Audit Log
        var rightStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 16 };
        rightStack.Add(CreateKeystrokeProbeCard());
        rightStack.Add(CreateCatalogCard());
        rightStack.Add(CreateAuditLogCard());
        mainGrid.Add(rightStack.Column(1));

        scrollViewer.Content = mainGrid;
        rootGrid.Add(scrollViewer.Row(1));

        this.Content = rootGrid;

        // Wire change notifications
        _viewModel.EditorScope.PropertyChanged += (s, e) => UpdateEditorVisuals();
        _viewModel.PlayerScope.PropertyChanged += (s, e) => UpdatePlayerVisuals();
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(KeybindingViewModel.HelpBannerMessage))
            {
                _bannerHelpText.Text = _viewModel.HelpBannerMessage;
            }
            else if (e.PropertyName?.StartsWith("Probe") == true)
            {
                UpdateProbeVisuals();
            }
        };

        ((INotifyCollectionChanged)_viewModel.Logs).CollectionChanged += (s, e) => UpdateLogVisuals();

        // Initial sync
        UpdateEditorVisuals();
        UpdatePlayerVisuals();
        UpdateProbeVisuals();
        UpdateLogVisuals();
    }

    private UIElement CreateMasterBanner()
    {
        var card = new Card(CardVariant.Filled)
            .Padding(20)
            .CornerRadius(14);

        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Keyboard, 28, foreground: Color.FromHex("#1E88E5")) { VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("KeybindingHandler & Declarative [Keybinding]")
                    .TitleLarge()
                    .Bold()
                    .VerticalAlign(VerticalAlignment.Center),
                CreatePillBadge("Roslyn Generator", Color.FromHex("#10B981")),
                CreatePillBadge("Scoped Bubbling", Color.FromHex("#8B5CF6")),
                CreatePillBadge("Native AOT Ready", Color.FromHex("#F59E0B"))
            );

        var descText = new TextBlock(
            "Atelier provides a compile-time declarative keyboard shortcut architecture. Methods or command properties marked with [property: Keybinding] or classes implementing [Keybinding] are registered at compile-time by Roslyn generators with zero reflection. KeybindingHandler controls intercept unhandled bubbling keyboard events matching their scope group (e.g. 'Editor', 'Player', 'Global').")
        { TextWrapping = TextWrapping.Wrap }
            .BodyMedium()
            .Foreground(Color.FromHex("#9E9E9E"));

        _bannerHelpText = new TextBlock(_viewModel.HelpBannerMessage)
            .BodySmall()
            .Bold()
            .Foreground(Color.FromHex("#1E88E5"))
            .VerticalAlign(VerticalAlignment.Center);

        var hintBox = new Border
        {
            Background = Color.FromHex("#1E88E5").WithAlpha(0.08f),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#1E88E5").WithAlpha(0.25f),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
                .Children(
                    new Icon(MaterialIconKind.Info, 18, foreground: Color.FromHex("#1E88E5")) { VerticalAlignment = VerticalAlignment.Center },
                    _bannerHelpText
                )
        };

        var bannerStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 }
            .Children(titleStack, descText, hintBox);

        card.Child(bannerStack);
        return card;
    }

    private UIElement CreateEditorScopeCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(18)
            .CornerRadius(12);

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.EditNote, 22, foreground: Color.FromHex("#10B981")) { VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Editor Scope").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center),
                CreatePillBadge("KeybindingHandler(\"Editor\")", Color.FromHex("#10B981"))
            );

        var scopeExpl = new TextBlock("Commands registered with Group = \"Editor\" are intercepted exclusively when focus is within this container. Typing standard text writes into the box, while gestures like Ctrl+S, Ctrl+B, Ctrl+I, Ctrl+K, and Ctrl+U trigger scoped commands.") { TextWrapping = TextWrapping.Wrap }
            .BodySmall()
            .Foreground(Color.FromHex("#757575"));

        // Interactive Toolbar
        var btnSave = new Button("Save (Ctrl+S)") { Variant = ButtonVariant.Filled, Height = 32 };
        btnSave.Click += (s, e) => _viewModel.EditorScope.SaveDocumentCommand.Execute(null);

        var btnBold = new Button("Bold (Ctrl+B)") { Variant = ButtonVariant.Outlined, Height = 32 };
        btnBold.Click += (s, e) => _viewModel.EditorScope.ToggleBoldCommand.Execute(null);

        var btnItalic = new Button("Italic (Ctrl+I)") { Variant = ButtonVariant.Outlined, Height = 32 };
        btnItalic.Click += (s, e) => _viewModel.EditorScope.ToggleItalicCommand.Execute(null);

        var btnCase = new Button("Case (Ctrl+U)") { Variant = ButtonVariant.Tonal, Height = 32 };
        btnCase.Click += (s, e) => _viewModel.EditorScope.ToggleCaseCommand.Execute(null);

        var btnClear = new Button("Clear (Ctrl+K)") { Variant = ButtonVariant.Text, Height = 32 };
        btnClear.Click += (s, e) => _viewModel.EditorScope.ClearDocumentCommand.Execute(null);

        var toolbar = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalSpacing = 6, VerticalSpacing = 6 }
            .Children(btnSave, btnBold, btnItalic, btnCase, btnClear);

        // Interactive TextBox bound to DocumentText
        var textBox = new TextBox
        {
            Placeholder = "Focus here and press Ctrl+S, Ctrl+B, Ctrl+I, Ctrl+K, Ctrl+U (or F5 / F1)...",
            Height = 36,
            Padding = new Thickness(12, 8),
            DataContext = _viewModel.EditorScope
        }.BindText(_viewModel.EditorScope, x => x.DocumentText, (vm, text) => vm.DocumentText = text);

        // Wrap textBox in KeybindingHandler("Editor")
        var editorHandler = new KeybindingHandler("Editor", textBox)
        {
            DataContext = _viewModel.EditorScope
        };

        // Formatted Document Live Preview
        _editorFormattedPreview = new TextBlock(_viewModel.EditorScope.DocumentText) { TextWrapping = TextWrapping.Wrap }
            .BodyMedium();

        _editorBoldBadge = CreatePillBadge("BOLD: OFF", Color.FromHex("#757575"));
        _editorItalicBadge = CreatePillBadge("ITALIC: OFF", Color.FromHex("#757575"));
        _editorCaseBadge = CreatePillBadge("STANDARD", Color.FromHex("#757575"));
        _editorSaveCountBadge = new TextBlock("Saves: 0").FontSize(11).Bold().Foreground(Color.FromHex("#10B981"));

        var statsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new TextBlock("Styling Output:").FontSize(11).Bold().VerticalAlign(VerticalAlignment.Center),
                _editorBoldBadge,
                _editorItalicBadge,
                _editorCaseBadge,
                _editorSaveCountBadge
            );

        var previewBox = new Border
        {
            Background = Color.FromHex("#212121").WithAlpha(0.04f),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#757575").WithAlpha(0.25f),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Child = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 }
                .Children(statsRow, _editorFormattedPreview)
        };

        _editorStatusText = new TextBlock(_viewModel.EditorScope.LastActionStatus)
            .FontSize(11)
            .Bold()
            .Foreground(Color.FromHex("#10B981"));

        var editorContent = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 }
            .Children(headerRow, scopeExpl, toolbar, editorHandler, previewBox, _editorStatusText);

        card.Child(editorContent);
        return card;
    }

    private void UpdateEditorVisuals()
    {
        if (_editorFormattedPreview == null) return;

        _editorFormattedPreview.Text = string.IsNullOrEmpty(_viewModel.EditorScope.DocumentText)
            ? "(Document is empty. Focus above and type, or click Reset)"
            : _viewModel.EditorScope.DocumentText;

        _editorFormattedPreview.Bold = _viewModel.EditorScope.IsBold;
        _editorFormattedPreview.Italic = _viewModel.EditorScope.IsItalic;

        // Update badges
        UpdatePillBadge(_editorBoldBadge, _viewModel.EditorScope.IsBold ? "BOLD: ON" : "BOLD: OFF",
            _viewModel.EditorScope.IsBold ? Color.FromHex("#10B981") : Color.FromHex("#757575"));

        UpdatePillBadge(_editorItalicBadge, _viewModel.EditorScope.IsItalic ? "ITALIC: ON" : "ITALIC: OFF",
            _viewModel.EditorScope.IsItalic ? Color.FromHex("#10B981") : Color.FromHex("#757575"));

        UpdatePillBadge(_editorCaseBadge, _viewModel.EditorScope.IsUppercase ? "UPPERCASE" : "Standard",
            _viewModel.EditorScope.IsUppercase ? Color.FromHex("#3B82F6") : Color.FromHex("#757575"));

        _editorSaveCountBadge.Text = $"Saves: {_viewModel.EditorScope.SaveCount}";
        _editorStatusText.Text = _viewModel.EditorScope.LastActionStatus;
    }

    private UIElement CreatePlayerScopeCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(18)
            .CornerRadius(12);

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.PlayCircle, 22, foreground: Color.FromHex("#3B82F6")) { VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Media Player Scope").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center),
                CreatePillBadge("KeybindingHandler(\"Player\")", Color.FromHex("#3B82F6"))
            );

        var playerExpl = new TextBlock("Demonstrates single-key and arrow-key gestures (Space, Left, Right, R, M) scoped to a focusable media container. Click inside the player card to focus it and trigger playback shortcuts.") { TextWrapping = TextWrapping.Wrap }
            .BodySmall()
            .Foreground(Color.FromHex("#757575"));

        // Player Info Row
        _playerPlayStateText = new TextBlock("⏸ PAUSED").FontSize(11).Bold().Foreground(Color.FromHex("#757575"));
        _playerPlayStateBadge = new Border
        {
            Background = Color.FromHex("#757575").WithAlpha(0.15f),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8, 3),
            Child = _playerPlayStateText
        };

        var trackHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new TextBlock("🎵 " + _viewModel.PlayerScope.TrackTitle).Bold().FontSize(13).VerticalAlign(VerticalAlignment.Center),
                _playerPlayStateBadge
            );

        var artistText = new TextBlock(_viewModel.PlayerScope.Artist).FontSize(11).Foreground(Color.FromHex("#757575"));

        // Progress Slider
        _playerTimeText = new TextBlock(_viewModel.PlayerScope.PositionDisplay).FontSize(12).Bold();
        _playerProgressSlider = new Slider
        {
            Minimum = 0f,
            Maximum = _viewModel.PlayerScope.DurationSeconds,
            Value = _viewModel.PlayerScope.PositionSeconds,
            Height = 32
        };

        _playerProgressSlider.ValueChanged += (s, val) =>
        {
            _viewModel.PlayerScope.PositionSeconds = val;
        };

        // Transport Controls
        var btnRewind = new Button("⏪ -5s [Left]") { Variant = ButtonVariant.Outlined, Height = 32 };
        btnRewind.Click += (s, e) => _viewModel.PlayerScope.SeekBackCommand.Execute(null);

        _playerPlayBtn = new Button("▶ Play [Space]") { Variant = ButtonVariant.Filled, Height = 32 };
        _playerPlayBtn.Click += (s, e) => _viewModel.PlayerScope.TogglePlayCommand.Execute(null);

        var btnReset = new Button("⏹ Reset [R]") { Variant = ButtonVariant.Tonal, Height = 32 };
        btnReset.Click += (s, e) => _viewModel.PlayerScope.ResetTrackCommand.Execute(null);

        var btnForward = new Button("⏩ +5s [Right]") { Variant = ButtonVariant.Outlined, Height = 32 };
        btnForward.Click += (s, e) => _viewModel.PlayerScope.SeekForwardCommand.Execute(null);

        _playerMuteBtn = new Button("🔊 Mute [M]") { Variant = ButtonVariant.Text, Height = 32 };
        _playerMuteBtn.Click += (s, e) => _viewModel.PlayerScope.ToggleMuteCommand.Execute(null);

        var transportRow = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalSpacing = 6, VerticalSpacing = 6 }
            .Children(btnRewind, _playerPlayBtn, btnReset, btnForward, _playerMuteBtn);

        _playerStatusText = new TextBlock(_viewModel.PlayerScope.StatusMessage)
            .FontSize(11)
            .Bold()
            .Foreground(Color.FromHex("#3B82F6"));

        var playerPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
            .Children(
                trackHeader,
                artistText,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
                    .Children(new TextBlock("Position:").FontSize(11).VerticalAlign(VerticalAlignment.Center), _playerTimeText),
                _playerProgressSlider,
                transportRow,
                _playerStatusText
            );

        // Wrap the player in a focusable Border and then in KeybindingHandler("Player")
        var focusablePlayerCard = new FocusableContainer
        {
            Background = Color.FromHex("#3B82F6").WithAlpha(0.04f),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#3B82F6").WithAlpha(0.20f),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14),
            Child = playerPanel,
            DataContext = _viewModel.PlayerScope
        };

        var playerHandler = new KeybindingHandler("Player", focusablePlayerCard)
        {
            DataContext = _viewModel.PlayerScope
        };

        var containerStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 }
            .Children(headerRow, playerExpl, playerHandler);

        card.Child(containerStack);
        return card;
    }

    private void UpdatePlayerVisuals()
    {
        if (_playerStatusText == null) return;

        _playerStatusText.Text = _viewModel.PlayerScope.StatusMessage;
        _playerTimeText.Text = _viewModel.PlayerScope.PositionDisplay;
        _playerProgressSlider.Value = _viewModel.PlayerScope.PositionSeconds;

        if (_viewModel.PlayerScope.IsPlaying)
        {
            _playerPlayBtn.Variant = ButtonVariant.Filled;
            _playerPlayBtn.Content = new TextBlock("⏸ Pause [Space]");
            _playerPlayStateText.Text = "▶ PLAYING";
            _playerPlayStateText.Foreground = Color.FromHex("#10B981");
            _playerPlayStateBadge.Background = Color.FromHex("#10B981").WithAlpha(0.15f);
        }
        else
        {
            _playerPlayBtn.Variant = ButtonVariant.Filled;
            _playerPlayBtn.Content = new TextBlock("▶ Play [Space]");
            _playerPlayStateText.Text = "⏸ PAUSED";
            _playerPlayStateText.Foreground = Color.FromHex("#757575");
            _playerPlayStateBadge.Background = Color.FromHex("#757575").WithAlpha(0.15f);
        }

        _playerMuteBtn.Content = new TextBlock(_viewModel.PlayerScope.IsMuted ? "🔇 Unmute [M]" : "🔊 Mute [M]");
    }

    private UIElement CreateBubblingArchitectureCard()
    {
        var card = new Card(CardVariant.Filled)
            .Padding(16)
            .CornerRadius(12);

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.AltRoute, 22, foreground: Color.FromHex("#8B5CF6")) { VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Hierarchical Visual Tree Bubbling").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center),
                CreatePillBadge("Group = \"Global\"", Color.FromHex("#8B5CF6"))
            );

        var descText = new TextBlock("When a key event is triggered inside the Editor or Player, the local KeybindingHandler checks its registered group. If unmatched, the event continues bubbling up the visual tree until caught by an ancestor handler (such as the outer window or Global handler). Try pressing F5 (Reset All) or F1 (Help) while typing in the editor above!")
            .BodySmall()
            .Foreground(Color.FromHex("#9E9E9E"));

        var btnResetAll = new Button("Global Reset (F5)") { Variant = ButtonVariant.Tonal, Height = 32 };
        btnResetAll.Click += (s, e) => _viewModel.ResetAllDemosCommand.Execute(null);

        var btnHelp = new Button("Shortcuts Help (F1)") { Variant = ButtonVariant.Outlined, Height = 32 };
        btnHelp.Click += (s, e) => KeybindingManager.TryExecuteGesture("Global", Key.F1, ModifierKeys.None);

        var btnClearLogs = new Button("Clear Logs (Ctrl+Shift+L)") { Variant = ButtonVariant.Text, Height = 32 };
        btnClearLogs.Click += (s, e) => _viewModel.ClearLogsCommand.Execute(null);

        var actionsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }
            .Children(btnResetAll, btnHelp, btnClearLogs);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
            .Children(headerRow, descText, actionsRow);

        card.Child(stack);
        return card;
    }

    private UIElement CreateKeystrokeProbeCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(18)
            .CornerRadius(12);

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Radar, 22, foreground: Color.FromHex("#F59E0B")) { VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Keystroke Probe & Simulator").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center),
                CreatePillBadge("Live Diagnostics", Color.FromHex("#F59E0B"))
            );

        var probeDesc = new TextBlock("Click the tester box below and press keys on your keyboard to inspect gesture parsing and target scope routing in real time. Or click the simulator chips below.") { TextWrapping = TextWrapping.Wrap }
            .BodySmall()
            .Foreground(Color.FromHex("#757575"));

        // Interactive Key Probe Target Box
        var testerPrompt = new TextBlock("🎯 Click here to focus & test physical keyboard shortcuts...")
            .FontSize(12)            
            .Foreground(Color.FromHex("#F59E0B"))
            .HorizontalAlign(HorizontalAlignment.Center)
            .VerticalAlign(VerticalAlignment.Center);

        _testerFocusBorder = new FocusableTesterBorder(e =>
        {
            _viewModel.UpdateProbe(e.Key, e.Modifiers);
        })
        {
            Background = Color.FromHex("#F59E0B").WithAlpha(0.08f),
            BorderThickness = new Thickness(2),
            BorderBrush = Color.FromHex("#F59E0B").WithAlpha(0.40f),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12),
            Height = 44,
            DataContext = _viewModel,
            Child = testerPrompt
        };

        // Diagnostic Grid
        _probeKeyText = new TextBlock(_viewModel.ProbeKey).Bold().FontSize(12);
        _probeModifiersText = new TextBlock(_viewModel.ProbeModifiers).Bold().FontSize(12);
        _probeGestureText = new TextBlock(_viewModel.ProbeGesture).Bold().FontSize(13).Foreground(Color.FromHex("#F59E0B"));
        _probeScopeText = new TextBlock(_viewModel.ProbeScope).Bold().FontSize(12).Foreground(Color.FromHex("#8B5CF6"));
        _probeCommandText = new TextBlock(_viewModel.ProbeMatchedCommand).Bold().FontSize(12).Foreground(Color.FromHex("#10B981"));

        var diagGrid = new Grid()
            .Columns(GridLength.Stars(1f), GridLength.Stars(1f))
            .Rows(GridLength.Auto, GridLength.Auto, GridLength.Auto)
            .RowSpacing(6)
            .ColumnSpacing(8);

        diagGrid.Add(CreateDiagItem("Parsed Gesture:", _probeGestureText).Column(0).Row(0));
        diagGrid.Add(CreateDiagItem("Target Scope:", _probeScopeText).Column(1).Row(0));
        diagGrid.Add(CreateDiagItem("Key & Modifiers:", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 }.Children(_probeKeyText, new TextBlock("+"), _probeModifiersText)).Column(0).Row(1));
        diagGrid.Add(CreateDiagItem("Matched Command:", _probeCommandText).Column(1).Row(1));

        // Quick Simulate Gesture Chips
        var simHeader = new TextBlock("Instant Gesture Simulator (Click to Trigger):").FontSize(11).Bold().Foreground(Color.FromHex("#757575"));

        var chipCtrlS = CreateSimButton("Ctrl+S", "Editor", Key.S, ModifierKeys.Control);
        var chipCtrlB = CreateSimButton("Ctrl+B", "Editor", Key.B, ModifierKeys.Control);
        var chipCtrlI = CreateSimButton("Ctrl+I", "Editor", Key.I, ModifierKeys.Control);
        var chipCtrlU = CreateSimButton("Ctrl+U", "Editor", Key.U, ModifierKeys.Control);
        var chipCtrlK = CreateSimButton("Ctrl+K", "Editor", Key.K, ModifierKeys.Control);
        var chipSpace = CreateSimButton("Space", "Player", Key.Space, ModifierKeys.None);
        var chipSeekBack = CreateSimButton("Left (-5s)", "Player", Key.Left, ModifierKeys.None);
        var chipSeekFwd = CreateSimButton("Right (+5s)", "Player", Key.Right, ModifierKeys.None);
        var chipReset = CreateSimButton("R (Reset)", "Player", Key.R, ModifierKeys.None);
        var chipMute = CreateSimButton("M (Mute)", "Player", Key.M, ModifierKeys.None);
        var chipF5 = CreateSimButton("F5 (Reset All)", "Global", Key.F5, ModifierKeys.None);
        var chipF1 = CreateSimButton("F1 (Help)", "Global", Key.F1, ModifierKeys.None);
        var chipClearLogs = CreateSimButton("Ctrl+Shift+L (Clear Logs)", "Global", Key.L, ModifierKeys.Control | ModifierKeys.Shift);

        var simChipsRow1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 }
            .Children(chipCtrlS, chipCtrlB, chipCtrlI, chipCtrlU, chipCtrlK);

        var simChipsRow2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 }
            .Children(chipSpace, chipSeekBack, chipSeekFwd, chipReset, chipMute);

        var simChipsRow3 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 }
            .Children(chipF5, chipF1, chipClearLogs);

        var probeStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
            .Children(_testerFocusBorder, diagGrid, simHeader, simChipsRow1, simChipsRow2, simChipsRow3);

        card.Child(new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 }.Children(headerRow, probeDesc, probeStack));
        return card;
    }

    private Button CreateSimButton(string label, string group, Key key, ModifierKeys modifiers)
    {
        var btn = new Button(label) { Variant = ButtonVariant.Outlined, Height = 28 };
        btn.Click += (s, e) => _viewModel.SimulateGesture(group, key, modifiers);
        return btn;
    }

    private UIElement CreateDiagItem(string title, UIElement content)
    {
        return new Border
        {
            Background = Color.FromHex("#757575").WithAlpha(0.06f),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6),
            Child = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
                .Children(new TextBlock(title).FontSize(10).Foreground(Color.FromHex("#757575")), content)
        };
    }

    private void UpdateProbeVisuals()
    {
        if (_probeKeyText == null) return;

        _probeKeyText.Text = _viewModel.ProbeKey;
        _probeModifiersText.Text = _viewModel.ProbeModifiers;
        _probeGestureText.Text = _viewModel.ProbeGesture;
        _probeScopeText.Text = _viewModel.ProbeScope;
        _probeCommandText.Text = _viewModel.ProbeMatchedCommand;
    }

    private UIElement CreateCatalogCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(18)
            .CornerRadius(12);

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.ListAlt, 22, foreground: Color.FromHex("#06B6D4")) { VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Keybinding Registry Catalog").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center),
                CreatePillBadge("Live Descriptors", Color.FromHex("#06B6D4"))
            );

        var catalogDesc = new TextBlock("Queried directly from KeybindingManager.RegisteredKeybindings, populated at compile time by Roslyn KeybindingGenerator:") { TextWrapping = TextWrapping.Wrap }
            .BodySmall()
            .Foreground(Color.FromHex("#757575"));

        var tableStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 };

        // Headers
        var tableHeader = new Grid()
            .Columns(GridLength.Pixels(80), GridLength.Pixels(130), GridLength.Pixels(110), GridLength.Star);

        tableHeader.Add(new TextBlock("Group").Bold().FontSize(11).Foreground(Color.FromHex("#757575")).Column(0).Margin(8, 4));
        tableHeader.Add(new TextBlock("Command").Bold().FontSize(11).Foreground(Color.FromHex("#757575")).Column(1).Margin(8, 4));
        tableHeader.Add(new TextBlock("Gesture").Bold().FontSize(11).Foreground(Color.FromHex("#757575")).Column(2).Margin(8, 4));
        tableHeader.Add(new TextBlock("Target / Type").Bold().FontSize(11).Foreground(Color.FromHex("#757575")).Column(3).Margin(8, 4));

        tableStack.Add(tableHeader);

        // Fetch registered entries
        foreach (var groupPair in KeybindingManager.RegisteredKeybindings.OrderBy(g => g.Key))
        {
            string groupName = groupPair.Key;
            Color groupColor = groupName switch
            {
                "Editor" => Color.FromHex("#10B981"),
                "Player" => Color.FromHex("#3B82F6"),
                "Global" => Color.FromHex("#8B5CF6"),
                _ => Color.FromHex("#F59E0B")
            };

            foreach (var descriptorPair in groupPair.Value.OrderBy(d => d.Key))
            {
                var descriptor = descriptorPair.Value;
                string commandType = descriptor.Command.GetType().Name;
                if (commandType.StartsWith("PropertyKeybindingCommand"))
                {
                    commandType = "PropertyKeybindingCommand";
                }

                var row = new Grid()
                    .Columns(GridLength.Pixels(80), GridLength.Pixels(130), GridLength.Pixels(110), GridLength.Star);

                var rowBorder = new Border
                {
                    Background = Color.FromHex("#757575").WithAlpha(0.04f),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 6),
                    Child = row
                };

                row.Add(CreatePillBadge(groupName, groupColor).Column(0).VerticalAlign(VerticalAlignment.Center));
                row.Add(new TextBlock(descriptor.Name).Bold().FontSize(12).VerticalAlign(VerticalAlignment.Center).Column(1));
                row.Add(new TextBlock(descriptor.Keybinding).FontSize(12).Bold().Foreground(Color.FromHex("#F59E0B")).VerticalAlign(VerticalAlignment.Center).Column(2));
                row.Add(new TextBlock(commandType).FontSize(11).Foreground(Color.FromHex("#757575")).VerticalAlign(VerticalAlignment.Center).Column(3));

                tableStack.Add(rowBorder);
            }
        }

        var scrollContainer = new ScrollViewer
        {
            Height = 220,
            Content = tableStack
        };

        card.Child(new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }.Children(headerRow, catalogDesc, scrollContainer));
        return card;
    }

    private UIElement CreateAuditLogCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(18)
            .CornerRadius(12);

        var btnClear = new Button("Clear Log") { Variant = ButtonVariant.Text, Height = 28 };
        btnClear.Click += (s, e) => _viewModel.ClearLogsCommand.Execute(null);

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.History, 22, foreground: Color.FromHex("#EC4899")) { VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Real-Time Intercept & Audit Log").TitleMedium().Bold().VerticalAlign(VerticalAlignment.Center),
                CreatePillBadge("Live Feed", Color.FromHex("#EC4899"))
            );

        var headerWithAction = new DockPanel { LastChildFill = true }
            .Children(
                btnClear.Dock(Dock.Right),
                headerRow.Dock(Dock.Left)
            );

        _logListPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };

        var logScroll = new ScrollViewer
        {
            Height = 180,
            Content = _logListPanel
        };

        card.Child(new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }.Children(headerWithAction, logScroll));
        return card;
    }

    private void UpdateLogVisuals()
    {
        if (_logListPanel == null) return;

        _logListPanel.Clear();

        foreach (var entry in _viewModel.Logs)
        {
            Color groupColor = entry.Group switch
            {
                "Editor" => Color.FromHex("#10B981"),
                "Player" => Color.FromHex("#3B82F6"),
                "Global" => Color.FromHex("#8B5CF6"),
                _ => Color.FromHex("#F59E0B")
            };

            var row = new Grid()
                .Columns(GridLength.Pixels(60), GridLength.Pixels(70), GridLength.Pixels(85), GridLength.Pixels(130), GridLength.Star);

            var rowBorder = new Border
            {
                Padding = new Thickness(6, 4),
                Child = row
            };

            row.Add(new TextBlock(entry.Time).FontSize(10).Foreground(Color.FromHex("#757575")).VerticalAlign(VerticalAlignment.Center).Column(0));
            row.Add(CreatePillBadge(entry.Group, groupColor).Column(1).VerticalAlign(VerticalAlignment.Center));
            row.Add(new TextBlock(entry.Gesture).FontSize(11).Bold().Foreground(Color.FromHex("#F59E0B")).VerticalAlign(VerticalAlignment.Center).Column(2));
            row.Add(new TextBlock(entry.Command).FontSize(11).Bold().VerticalAlign(VerticalAlignment.Center).Column(3));
            row.Add(new TextBlock(entry.Status).FontSize(11).Foreground(Color.FromHex("#9E9E9E")).VerticalAlign(VerticalAlignment.Center).Column(4));

            _logListPanel.Add(rowBorder);
        }
    }

    private static Border CreatePillBadge(string text, Color color)
    {
        var tb = new TextBlock(text)
        {
            FontSize = 10,
            Foreground = color,
            VerticalAlignment = VerticalAlignment.Center
        }.Bold();

        return new Border
        {
            Background = color.WithAlpha(0.14f),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = tb
        };
    }

    private static void UpdatePillBadge(Border badge, string text, Color color)
    {
        badge.Background = color.WithAlpha(0.14f);
        if (badge.Child is TextBlock tb)
        {
            tb.Text = text;
            tb.Foreground = color;
        }
    }
}

/// <summary>
/// A focusable container for the Player card to capture keyboard shortcuts when clicked.
/// </summary>
internal class FocusableContainer : Border
{
    public FocusableContainer()
    {
        IsFocusable = true;
    }
}

/// <summary>
/// Focusable border for the keystroke probe tester that intercepts all OnKeyDown events.
/// </summary>
internal class FocusableTesterBorder : Border
{
    private readonly Action<KeyEventArgs> _onKeyDown;

    public FocusableTesterBorder(Action<KeyEventArgs> onKeyDown)
    {
        _onKeyDown = onKeyDown;
        IsFocusable = true;
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _onKeyDown(e);
    }
}

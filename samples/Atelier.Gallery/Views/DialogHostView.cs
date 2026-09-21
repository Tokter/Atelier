using System;
using System.Threading.Tasks;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Theming;
using Atelier.Theming.Material;

namespace Atelier.Gallery.Views;

public class DialogHostView : Grid
{
    private readonly DialogHostViewModel _viewModel;
    private readonly DialogHost _localDialogHost;
    private Button? _btnScopeGlobal;
    private Button? _btnScopeLocal;
    private Button? _btnOpacity25;
    private Button? _btnOpacity50;
    private Button? _btnOpacity75;
    private TextBlock? _formResultText;

    public DialogHostView() : this(new DialogHostViewModel())
    {
    }

    public DialogHostView(DialogHostViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        this.Rows(GridLength.Auto, GridLength.Star);
        this.RowSpacing(16);

        // 1. Create Local DialogHost wrapping its local workspace card
        _localDialogHost = new DialogHost
        {
            Identifier = "LocalGalleryHost",
            CloseOnClickAway = _viewModel.CloseOnClickAway,
            OverlayColor = _viewModel.CurrentOverlayColor,
            Content = CreateLocalHostContent()
        };

        // 2. Master Controls Banner
        this.Add(CreateMasterBanner().Row(0));

        // 3. Scrollable Showcase Content
        var scrollViewer = new ScrollViewer();
        var mainStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 16
        };

        // 2-Column Grid: Catalog on left, Local Host on right
        var showcaseGrid = new Grid()
            .Columns(GridLength.Pixels(480), GridLength.Star)
            .ColumnSpacing(16);

        showcaseGrid.Add(CreateDialogCatalogCard().Column(0));
        showcaseGrid.Add(CreateLocalHostCard().Column(1));
        mainStack.Add(showcaseGrid);

        // Interaction & Result Log Card
        mainStack.Add(CreateInteractionLogCard());

        scrollViewer.Content = mainStack;
        this.Add(scrollViewer.Row(1));

        UpdateControlsVisuals();
    }

    private void UpdateControlsVisuals()
    {
        if (_btnScopeGlobal != null)
            _btnScopeGlobal.Variant = _viewModel.IsGlobalTarget ? ButtonVariant.Filled : ButtonVariant.Outlined;
        if (_btnScopeLocal != null)
            _btnScopeLocal.Variant = !_viewModel.IsGlobalTarget ? ButtonVariant.Filled : ButtonVariant.Outlined;

        if (_btnOpacity25 != null)
            _btnOpacity25.Variant = MathF.Abs(_viewModel.OverlayOpacity - 0.25f) < 0.05f ? ButtonVariant.Filled : ButtonVariant.Tonal;
        if (_btnOpacity50 != null)
            _btnOpacity50.Variant = MathF.Abs(_viewModel.OverlayOpacity - 0.50f) < 0.05f ? ButtonVariant.Filled : ButtonVariant.Tonal;
        if (_btnOpacity75 != null)
            _btnOpacity75.Variant = MathF.Abs(_viewModel.OverlayOpacity - 0.75f) < 0.05f ? ButtonVariant.Filled : ButtonVariant.Tonal;

        _localDialogHost.CloseOnClickAway = _viewModel.CloseOnClickAway;
        _localDialogHost.OverlayColor = _viewModel.CurrentOverlayColor;
    }

    private UIElement CreateMasterBanner()
    {
        var card = new Card(CardVariant.Filled)
            .Padding(20)
            .CornerRadius(14);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        // Title and description
        stack.Add(new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 }
            .Children(
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
                    .Children(
                        new Icon(MaterialIconKind.WebAsset, 28) { Foreground = Color.FromHex("#1E88E5"), VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock("DialogHost & Modal Dialog Showcase").TitleLarge()
                    ),
                new TextBlock("Modal dialogs with backdrop darkening scrims, input blocking, global and scoped local host resolution, and CloseOnClickAway click-away dismissal.")
                    .Subtext()
            )
        );

        // Controls Row
        var controlsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, VerticalAlignment = VerticalAlignment.Center };

        // 1. Host Scope Switcher
        _btnScopeGlobal = new Button("Global Host (RootHost)")
            .Variant(ButtonVariant.Filled)
            .OnClick(() =>
            {
                _viewModel.IsGlobalTarget = true;
                UpdateControlsVisuals();
                _viewModel.Log("Target set to Global Window Host ('RootHost'). Dialogs will cover the entire application.");
            });

        _btnScopeLocal = new Button("Local Host (LocalGalleryHost)")
            .Variant(ButtonVariant.Outlined)
            .OnClick(() =>
            {
                _viewModel.IsGlobalTarget = false;
                UpdateControlsVisuals();
                _viewModel.Log("Target set to Local Workspace Host ('LocalGalleryHost'). Dialogs will be bounded inside the card.");
            });

        var scopeGroup = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 }
            .Children(
                new TextBlock("Active Target Host:").LabelSmall().Bold(),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 }
                    .Children(_btnScopeGlobal, _btnScopeLocal)
            );

        // 2. CloseOnClickAway Switch
        var clickAwaySwitch = new Switch("Close On Click-Away")
            .BindIsChecked(_viewModel, vm => vm.CloseOnClickAway, (vm, v) =>
            {
                vm.CloseOnClickAway = v;
                UpdateControlsVisuals();
                vm.Log($"CloseOnClickAway set to {v}. Scrim clicks will {(v ? "dismiss dialog" : "be swallowed")}.");
            })
            .VerticalAlign(VerticalAlignment.Center);

        var clickAwayGroup = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 }
            .Children(
                new TextBlock("Backdrop Scrim Behavior:").LabelSmall().Bold(),
                clickAwaySwitch
            );

        // 3. Scrim Opacity Presets
        _btnOpacity25 = new Button("25%").Variant(ButtonVariant.Tonal).OnClick(() => { _viewModel.SetOpacity25Command.Execute(null); UpdateControlsVisuals(); });
        _btnOpacity50 = new Button("50%").Variant(ButtonVariant.Filled).OnClick(() => { _viewModel.SetOpacity50Command.Execute(null); UpdateControlsVisuals(); });
        _btnOpacity75 = new Button("75%").Variant(ButtonVariant.Tonal).OnClick(() => { _viewModel.SetOpacity75Command.Execute(null); UpdateControlsVisuals(); });

        var opacityGroup = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 }
            .Children(
                new TextBlock("Overlay Darkening:").LabelSmall().Bold(),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 }
                    .Children(_btnOpacity25, _btnOpacity50, _btnOpacity75)
            );

        controlsRow.Add(scopeGroup);
        controlsRow.Add(clickAwayGroup);
        controlsRow.Add(opacityGroup);
        stack.Add(controlsRow);

        card.Child = stack;
        return card;
    }

    private UIElement CreateDialogCatalogCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(20)
            .CornerRadius(12);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        stack.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.DashboardCustomize, 20) { Foreground = Color.FromHex("#1E88E5"), VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Dialog Types Catalog").TitleMedium()
            )
        );

        stack.Add(new TextBlock("Launch any dialog into the currently selected host (Global or Local). Click the backdrop scrim when CloseOnClickAway is active to dismiss.") { TextWrapping = TextWrapping.Wrap }
            .Caption()
            .Muted()
        );

        // Launcher Rows
        stack.Add(CreateLauncherRow(
            icon: MaterialIconKind.Info,
            iconColor: Color.FromHex("#1E88E5"),
            title: "Information / Alert Dialog",
            desc: "Standard notification with single OK button. Informs user of completed operations.",
            buttonText: "Show Alert",
            action: () => ShowAlertSample()
        ));

        stack.Add(CreateLauncherRow(
            icon: MaterialIconKind.WarningAmber,
            iconColor: Color.FromHex("#EF4444"),
            title: "Confirmation Dialog",
            desc: "Action verification with OK & Cancel. Used before executing destructive actions.",
            buttonText: "Show Confirm",
            action: () => ShowConfirmSample()
        ));

        stack.Add(CreateLauncherRow(
            icon: MaterialIconKind.HelpOutline,
            iconColor: Color.FromHex("#8B5CF6"),
            title: "Choice / 3-Button Dialog",
            desc: "Triple option decision with Yes, No, and Cancel (Preset: YesNoCancel).",
            buttonText: "Show Choice",
            action: () => ShowChoiceSample()
        ));

        stack.Add(CreateLauncherRow(
            icon: MaterialIconKind.EditNote,
            iconColor: Color.FromHex("#10B981"),
            title: "Custom Form Input Dialog",
            desc: "Embedded custom UI: TextBox, Switch, and Slider inside dialog body with validation.",
            buttonText: "Show Form",
            action: () => ShowFormSample()
        ));

        stack.Add(CreateLauncherRow(
            icon: MaterialIconKind.HourglassTop,
            iconColor: Color.FromHex("#F59E0B"),
            title: "Async Progress Dialog",
            desc: "Modal progress host with determinate ProgressBar advancing over time, simulating async work.",
            buttonText: "Show Progress",
            action: () => ShowProgressSample()
        ));

        stack.Add(CreateLauncherRow(
            icon: MaterialIconKind.Stars,
            iconColor: Color.FromHex("#EC4899"),
            title: "Custom Material 3 Rich Dialog",
            desc: "Full custom layout: decorative header banner, item badges, and styled action row.",
            buttonText: "Show Custom",
            action: () => ShowCustomRichSample()
        ));

        card.Child = stack;
        return card;
    }

    private UIElement CreateLauncherRow(
        MaterialIconKind icon,
        Color iconColor,
        string title,
        string desc,
        string buttonText,
        Action action)
    {
        var rowGrid = new Grid()
            .Columns(GridLength.Star, GridLength.Auto)
            .ColumnSpacing(12);

        var textStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
            .Children(
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center }
                    .Children(
                        new Icon(icon, 16) { Foreground = iconColor, VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock(title).Bold().FontSize(13)
                    ),
                new TextBlock(desc) { TextWrapping = TextWrapping.Wrap }.Caption().Muted()
            ).Column(0);

        var btn = new Button(buttonText)
            .Variant(ButtonVariant.Filled)
            .Padding(12, 6)
            .VerticalAlign(VerticalAlignment.Center)
            .OnClick(action)
            .Column(1);

        rowGrid.Add(textStack);
        rowGrid.Add(btn);

        var border = new Border
        {
            Padding = new Thickness(12, 10),
            CornerRadius = new CornerRadius(8),
            Background = Color.FromRgb(128, 128, 128).WithAlpha(0.04f),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromRgb(128, 128, 128).WithAlpha(0.10f),
            Child = rowGrid
        };

        return border;
    }

    private UIElement CreateLocalHostCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(16)
            .CornerRadius(12);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 };

        // Header with Scoped Badge
        var badge = new Border
        {
            Background = Color.FromHex("#10B981").WithAlpha(0.18f),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2),
            Child = new TextBlock("Scoped Modal Host")
                .Caption()
                .Bold()
                .Foreground(Color.FromHex("#10B981"))
        };

        var titleRow = new Grid()
            .Columns(GridLength.Star, GridLength.Auto)
            .Children(
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
                    .Children(
                        new Icon(MaterialIconKind.FolderSpecial, 20) { Foreground = Color.FromHex("#10B981"), VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock("Local Host Workspace").TitleMedium()
                    ).Column(0),
                badge.Column(1)
            );

        stack.Add(titleRow);

        stack.Add(new TextBlock("This container hosts its own local DialogHost ('LocalGalleryHost'). When a dialog is opened here, ONLY this card is dimmed and blocked. The navigation sidebar and the rest of the application remain active!") { TextWrapping = TextWrapping.Wrap }
            .Caption()
            .Muted()
        );

        // Embed the local DialogHost container
        var localContainerBorder = new Border
        {
            MinHeight = 360,
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(2),
            BorderBrush = Color.FromHex("#10B981").WithAlpha(0.40f),
            Background = Color.FromHex("#10B981").WithAlpha(0.03f),
            Child = _localDialogHost
        };

        stack.Add(localContainerBorder);

        card.Child = stack;
        return card;
    }

    private UIElement CreateLocalHostContent()
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 };

        // Project Info
        stack.Add(new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
            .Children(
                new TextBlock("Project Orion - Production Node").TitleSmall().Bold(),
                new TextBlock("Active Region: US-East-1 (Primary Cluster)").Caption().Muted()
            )
        );

        // Interactive control 1: Counter button
        var clickBtn = new Button("Click Local Control")
            .Variant(ButtonVariant.Filled)
            .Padding(14, 6)
            .Command(_viewModel.IncrementLocalCardClicksCommand);

        var clickCounterText = new TextBlock()
            .Caption()
            .Bold()
            .VerticalAlign(VerticalAlignment.Center)
            .BindText(_viewModel, vm => $"Clicks: {vm.LocalCardClickCount}");

        var clickRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
            .Children(clickBtn, clickCounterText);
        stack.Add(clickRow);

        // Interactive control 2: Switch
        var syncSwitch = new Switch("Auto-sync local metrics")
            .BindIsChecked(_viewModel, vm => vm.IsLocalAutoSyncEnabled, (vm, v) => vm.IsLocalAutoSyncEnabled = v);
        stack.Add(syncSwitch);

        // Interactive control 3: Direct local trigger button
        var localTriggerBtn = new Button("Trigger Scoped Dialog Inside This Card")
            .Variant(ButtonVariant.Tonal)
            .Padding(14, 8)
            .OnClick(() => ShowLocalScopedDialog());

        stack.Add(localTriggerBtn);

        // Explanation callout
        var callout = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8),
            Background = Color.FromRgb(128, 128, 128).WithAlpha(0.06f),
            Child = new TextBlock("Test: Click 'Trigger Scoped Dialog Inside This Card'. Notice the scrim covers ONLY this green-bordered card. The Left Column launchers and sidebar navigation remain completely clickable.") { TextWrapping = TextWrapping.Wrap }
                .Caption()
        };
        stack.Add(callout);

        return new Border
        {
            Padding = new Thickness(16),
            Child = stack
        };
    }

    private UIElement CreateInteractionLogCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(16)
            .CornerRadius(12);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 };

        var titleRow = new Grid()
            .Columns(GridLength.Star, GridLength.Auto)
            .Children(
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
                    .Children(
                        new Icon(MaterialIconKind.ReceiptLong, 18) { Foreground = Color.FromHex("#1E88E5"), VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock("Dialog Lifecycle & Event Audit Log").TitleSmall()
                    ).Column(0),
                new Button("Clear Log")
                    .Variant(ButtonVariant.Text)
                    .Padding(8, 2)
                    .Command(_viewModel.ClearLogCommand)
                    .Column(1)
            );

        stack.Add(titleRow);

        var logBorder = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 8),
            Background = Color.FromRgb(128, 128, 128).WithAlpha(0.08f),
            Child = new TextBlock()
                .LabelMedium()
                .Bold()
                .BindText(_viewModel, vm => vm.InteractionLog)
        };
        stack.Add(logBorder);

        _formResultText = new TextBlock()
            .Caption()
            .Muted()
            .BindText(_viewModel, vm => $"Last Submitted Payload: {vm.LastSubmittedFormResult}");
        stack.Add(_formResultText);

        card.Child = stack;
        return card;
    }

    // ========================================================
    // Dialog Launching & Event Handling
    // ========================================================

    private async Task ShowDialogAsync(Dialog dialog, bool? globalOverride = null)
    {
        bool isGlobal = globalOverride ?? _viewModel.IsGlobalTarget;
        string targetHostId = isGlobal ? "RootHost" : "LocalGalleryHost";
        string targetLabel = isGlobal ? "Global Window (RootHost)" : "Local Card (LocalGalleryHost)";

        var host = isGlobal
            ? DialogHost.FindNearestHost(null, targetHostId)
            : _localDialogHost;

        if (host == null)
        {
            _viewModel.Log($"Error: Could not locate DialogHost with Identifier '{targetHostId}'.");
            return;
        }

        // Apply dynamic settings to target host
        host.CloseOnClickAway = _viewModel.CloseOnClickAway;
        host.OverlayColor = _viewModel.CurrentOverlayColor;

        _viewModel.Log($"Showing '{dialog.Title}' in [{targetLabel}] with CloseOnClickAway = {host.CloseOnClickAway}...");

        var response = await dialog.ShowAsync(host);

        if (response.Result == DialogResult.Cancel && response.Button == null)
        {
            _viewModel.Log($"[{targetLabel}] Dialog dismissed via click-away on backdrop scrim (Result: Cancel).");
        }
        else
        {
            string btnText = response.ButtonText != null ? $"'{response.ButtonText}'" : "None";
            string tagInfo = response.Tag != null ? $", Tag: '{response.Tag}'" : string.Empty;
            _viewModel.Log($"[{targetLabel}] Dialog closed with Result: {response.Result}, Button: {btnText}{tagInfo}.");
        }
    }

    private void ShowAlertSample()
    {
        var dlg = new Dialog(
            "System Update Ready",
            "Atelier UI engine v2.5 components are initialized. GPU shaders, Skia canvas contexts, and layout pipelines are operational.",
            DialogButtons.Ok);

        _ = ShowDialogAsync(dlg);
    }

    private void ShowConfirmSample()
    {
        var dlg = new Dialog("Delete Dataset?", "Are you sure you want to permanently delete 'Production-Metrics-2026.parquet'? This operation cannot be undone.");
        dlg.AddButton("Cancel", DialogResult.Cancel, isCancel: true, variant: ButtonVariant.Text);
        dlg.AddButton("Delete Record", DialogResult.Ok, isDefault: true, variant: ButtonVariant.Filled, tag: "delete_confirmed");

        _ = ShowDialogAsync(dlg);
    }

    private void ShowChoiceSample()
    {
        var dlg = new Dialog(
            "Unsaved Document Changes",
            "You have unsaved modifications in 'WorkspaceConfig.json'. Would you like to save before switching pages?",
            DialogButtons.YesNoCancel);

        _ = ShowDialogAsync(dlg);
    }

    private async void ShowFormSample()
    {
        var nameBox = new TextBox("api-gateway-node") { Width = 280, Padding = new Thickness(10, 6) };
        var portBox = new TextBox("8080") { Width = 280, Padding = new Thickness(10, 6) };
        var sslSwitch = new Switch("Enable TLS / SSL") { IsChecked = true };
        var quotaSlider = new Slider { Minimum = 10, Maximum = 100, Value = 50, Width = 280 };
        var quotaLabel = new TextBlock("Connection Pool: 50").Caption();
        quotaSlider.ValueChanged += (s, v) => quotaLabel.Text = $"Connection Pool: {v:F0}";

        var formStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 }
            .Children(
                new TextBlock("Node Identifier").LabelSmall().Bold(),
                nameBox,
                new TextBlock("Service Port").LabelSmall().Bold(),
                portBox,
                sslSwitch,
                quotaLabel,
                quotaSlider
            );

        var dlg = new Dialog("Configure Service Node")
        {
            Content = formStack
        };

        dlg.AddButton("Cancel", DialogResult.Cancel, isCancel: true, variant: ButtonVariant.Text);
        dlg.AddButton("Save Node", DialogResult.Ok, isDefault: true, variant: ButtonVariant.Filled, tag: "save_node");

        await ShowDialogAsync(dlg);
        _viewModel.LastSubmittedFormResult = $"Node: '{nameBox.Text}', Port: '{portBox.Text}', SSL: {sslSwitch.IsChecked}, Pool: {quotaSlider.Value:F0}";
    }

    private async void ShowProgressSample()
    {
        var progressBar = new ProgressBar
        {
            IsIndeterminate = false,
            Minimum = 0f,
            Maximum = 100f,
            Value = 0f,
            Width = 320
        };

        var statusText = new TextBlock("Initializing archive export...")
            .Caption()
            .Muted();

        var percentText = new TextBlock("0%")
            .Caption()
            .Bold();

        var progressInfoRow = new Grid()
            .Columns(GridLength.Star, GridLength.Auto)
            .Children(
                statusText.Column(0),
                percentText.Column(1)
            );

        var progressStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 }
            .Children(
                new TextBlock("Exporting high-resolution archive to disk. Progress updates continuously as stages complete.").Subtext(),
                progressBar,
                progressInfoRow
            );

        var dlg = new Dialog("Exporting Archive")
        {
            Content = progressStack,
            CloseOnEscape = false
        };

        dlg.AddButton("Cancel Operation", DialogResult.Cancel, isCancel: true, variant: ButtonVariant.Text);

        var dialogTask = ShowDialogAsync(dlg);

        // Simulate continuous progress over time (resumes on UI thread via SynchronizationContext)
        for (int progress = 1; progress <= 100; progress++)
        {
            await Task.Delay(35);
            if (dialogTask.IsCompleted) break;

            progressBar.Value = progress;
            percentText.Text = $"{progress}%";

            if (progress == 1)
                statusText.Text = "Scanning visual tree hierarchy...";
            else if (progress == 20)
                statusText.Text = "Compressing visual cache buffers...";
            else if (progress == 55)
                statusText.Text = "Writing metadata manifests and checksums...";
            else if (progress == 85)
                statusText.Text = "Finalizing archive package...";
            else if (progress == 100)
                statusText.Text = "Export completed successfully!";
        }

        if (!dialogTask.IsCompleted)
        {
            await Task.Delay(500);
            if (!dialogTask.IsCompleted)
            {
                dlg.Close(DialogResult.Ok);
            }
        }

        await dialogTask;
    }

    private void ShowCustomRichSample()
    {
        var banner = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14),
            Background = Color.FromHex("#1E88E5").WithAlpha(0.12f),
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#1E88E5").WithAlpha(0.30f),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
                .Children(
                    new Icon(MaterialIconKind.AutoAwesome, 26) { Foreground = Color.FromHex("#1E88E5"), VerticalAlignment = VerticalAlignment.Center },
                    new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
                        .Children(
                            new TextBlock("Atelier Professional Plan").Bold().FontSize(13),
                            new TextBlock("Includes multi-host DialogHost routing, custom scrim shaders, and real-time layout transform expansion.").Caption().Muted()
                        )
                )
        };

        var featureList = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 }
            .Children(
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center }
                    .Children(new Icon(MaterialIconKind.Check, 16) { Foreground = Color.FromHex("#10B981") }, new TextBlock("Arbitrary 2D affine matrix pipelines").Caption()),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center }
                    .Children(new Icon(MaterialIconKind.Check, 16) { Foreground = Color.FromHex("#10B981") }, new TextBlock("Sub-pixel inverse hit-testing").Caption()),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center }
                    .Children(new Icon(MaterialIconKind.Check, 16) { Foreground = Color.FromHex("#10B981") }, new TextBlock("Modal focus trapping & keyboard bubbling").Caption())
            );

        var contentStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 }
            .Children(banner, featureList);

        var dlg = new Dialog("Upgrade Workspace")
        {
            Content = contentStack
        };

        dlg.AddButton("Later", DialogResult.Cancel, isCancel: true, variant: ButtonVariant.Text);
        dlg.AddButton("Activate Plan", DialogResult.Ok, isDefault: true, variant: ButtonVariant.Filled, tag: "pro_activated");

        _ = ShowDialogAsync(dlg);
    }

    private void ShowLocalScopedDialog()
    {
        var dlg = new Dialog(
            "Scoped Node Confirmation",
            "This dialog is hosted inside 'LocalGalleryHost'. Notice that the rest of the application remains completely unblocked.",
            DialogButtons.OkCancel);

        _ = ShowDialogAsync(dlg, globalOverride: false);
    }
}

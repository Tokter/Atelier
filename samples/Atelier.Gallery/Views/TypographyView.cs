using System;
using System.Collections.Generic;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Theming.Material;
using Atelier.Gallery.ViewModels;

namespace Atelier.Gallery.Views;

public class TypographyView : Grid
{
    private readonly TypographyViewModel _viewModel;
    private readonly ScrollViewer _scrollViewer;

    public TypographyView() : this(new TypographyViewModel())
    {
    }

    public TypographyView(TypographyViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        this.Rows(GridLength.Auto, GridLength.Star);
        this.RowSpacing(16);

        // 1. Fixed Header Master Banner
        this.Add(CreateMasterBanner().Row(0));

        // 2. Scrollable Showcase Content
        var contentStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 16
        }.Children(
            CreatePlaygroundSection(),
            CreateMd3ScaleSection(),
            CreateTextBlockFeaturesSection(),
            CreateCompositionSection()
        );

        contentStack.Margin = new Thickness(0, 0, 10, 20);

        _scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = contentStack
        }.Row(1);

        this.Add(_scrollViewer);
    }

    public override void OnPointerWheel(PointerWheelEventArgs e)
    {
        base.OnPointerWheel(e);
        if (!e.Handled && _scrollViewer != null)
        {
            _scrollViewer.OnPointerWheel(e);
        }
    }

    #region Master Header Banner

    private UIElement CreateMasterBanner()
    {
        var card = new Card(CardVariant.Filled)
            .Padding(20)
            .CornerRadius(14);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        // Header text
        stack.Add(new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 }
            .Children(
                new TextBlock("Typography & Text Styling").TitleLarge(),
                new TextBlock("Material Design 3 typography type scale (Display, Headline, Title, Body, Label), common headings & subtext styles, and rich TextBlock styling with Bold, Italic, FontFamily, Muted, and colors.")
                    .Subtext()
            )
        );

        // Action row
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Button("Toggle Bold")
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ToggleBoldCommand)
                    .BindVariant(_viewModel, vm => vm.IsBold ? ButtonVariant.Filled : ButtonVariant.Outlined),

                new Button("Toggle Italic")
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ToggleItalicCommand)
                    .BindVariant(_viewModel, vm => vm.IsItalic ? ButtonVariant.Filled : ButtonVariant.Outlined),

                new Button("Toggle Muted")
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ToggleMutedCommand)
                    .BindVariant(_viewModel, vm => vm.IsMuted ? ButtonVariant.Filled : ButtonVariant.Outlined),

                new Button("Reset All")
                    .Variant(ButtonVariant.Tonal)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ResetPlaygroundCommand)
            );

        stack.Add(actionRow);
        card.Child = stack;
        return card;
    }

    #endregion

    #region Section 1: Live Interactive Typography Playground

    private UIElement CreatePlaygroundSection()
    {
        var grid = new Grid()
            .Columns(new GridLength(1.1f, GridUnitType.Star), new GridLength(1.0f, GridUnitType.Star))
            .ColumnSpacing(16);

        // --- Left Column: Live Preview Card ---
        var previewCard = new Card(CardVariant.Filled)
            .Padding(20)
            .CornerRadius(14);

        var previewStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        var previewHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Visibility, 20, foreground: Color.FromHex("#3B82F6")),
                new TextBlock("Live Typography Preview").TitleSmall()
            );

        // Sub-preview 1: Pure Style-Driven Preview (Bound directly to StyleKey)
        var styleHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Style, 16, foreground: Color.FromHex("#10B981")),
                new TextBlock("1. Material Design 3 Style Resolution (Pure StyleKey)").TitleSmall()
            );

        var stylePreviewBox = new Card(CardVariant.Outlined)
            .Padding(16)
            .CornerRadius(10);

        var styleDrivenTextBlock = new TextBlock()
            .BindStyleKey(_viewModel, vm => vm.SelectedStyleKey)
            .BindText(_viewModel, vm => vm.SampleText)
            .BindFontFamily(_viewModel, vm => vm.FontFamily)
            .BindItalic(_viewModel, vm => vm.IsItalic)
            .BindTextAlignment(_viewModel, vm => vm.TextAlignment)
            .BindTextWrapping(_viewModel, vm => vm.TextWrapping);

        stylePreviewBox.Child = styleDrivenTextBlock;

        var styleBadgeText = new TextBlock()
            .Caption();

        void UpdateStyleBadge()
        {
            var style = MaterialTypography.GetRegisteredStyle(_viewModel.SelectedStyleKey);
            float fs = 0f;
            bool bold = false;
            bool muted = false;
            if (style != null)
            {
                foreach (var setter in style.Setters)
                {
                    if (setter.Property == TextBlock.FontSizeProperty && setter.Value is float f) fs = f;
                    else if (setter.Property == TextBlock.BoldProperty && setter.Value is bool b) bold = b;
                    else if (setter.Property == TextBlock.MutedProperty && setter.Value is bool m) muted = m;
                }
            }
            string weight = bold ? "Bold" : "Regular";
            string muteStr = muted ? ", Muted" : "";
            styleBadgeText.Text = $"StyleKey: \"{_viewModel.SelectedStyleKey}\" (Resolved from StyleManager.GlobalStyles • {fs:F0}pt, {weight}{muteStr})";
        }

        UpdateStyleBadge();

        var styleSpecsCard = new Card(CardVariant.Filled)
            .Padding(8, 8)
            .CornerRadius(6)
            .Child(styleBadgeText);

        // Sub-preview 2: Interactive Overrides Preview (Demonstrates 4-Tier Precedence)
        var overrideHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Tune, 16, foreground: Color.FromHex("#F59E0B")),
                new TextBlock("2. Interactive Local Overrides (4-Tier Precedence)").TitleSmall()
            );

        var overridePreviewBox = new Card(CardVariant.Outlined)
            .Padding(16)
            .CornerRadius(10);

        var overrideTextBlock = new TextBlock()
            .BindStyleKey(_viewModel, vm => vm.SelectedStyleKey)
            .BindText(_viewModel, vm => vm.SampleText)
            .BindFontSize(_viewModel, vm => vm.FontSize)
            .BindFontFamily(_viewModel, vm => vm.FontFamily)
            .BindBold(_viewModel, vm => vm.IsBold)
            .BindItalic(_viewModel, vm => vm.IsItalic)
            .BindMuted(_viewModel, vm => vm.IsMuted)
            .BindForeground(_viewModel, vm => vm.SelectedColor)
            .BindTextAlignment(_viewModel, vm => vm.TextAlignment)
            .BindTextWrapping(_viewModel, vm => vm.TextWrapping);

        overridePreviewBox.Child = overrideTextBlock;

        // Status badge readout
        var specsText = new TextBlock()
            .Caption();

        void UpdateSpecs()
        {
            string slant = _viewModel.IsItalic ? "Italic" : "Upright";
            string weight = _viewModel.IsBold ? "Bold" : "Regular";
            string muted = _viewModel.IsMuted ? "Muted" : "Normal";
            string align = _viewModel.TextAlignment.ToString();
            string wrap = _viewModel.IsWrapping ? "Wrap" : "NoWrap";
            string color = _viewModel.SelectedColor == Color.Transparent ? "Theme Default" : "Custom";
            specsText.Text = $"Interactive: {_viewModel.FontFamily} | {_viewModel.FontSize:F0}pt | {weight}, {slant} | {muted} | Color: {color} | Align: {align} | {wrap}";
        }

        UpdateSpecs();
        _viewModel.PropertyChanged += (s, e) =>
        {
            UpdateStyleBadge();
            UpdateSpecs();
        };

        var specsCard = new Card(CardVariant.Filled)
            .Padding(8, 8)
            .CornerRadius(6)
            .Child(specsText);

        previewStack.Children(
            previewHeader,
            overrideHeader,
            overridePreviewBox,
            specsCard,
            styleHeader,
            stylePreviewBox,
            styleSpecsCard
        );
        previewCard.Child = previewStack;
        grid.Add(previewCard.Column(0));

        // --- Right Column: Interactive Controls ---
        var controlsCard = new Card(CardVariant.Outlined)
            .Padding(20)
            .CornerRadius(14);

        var controlsStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        // 1. Sample Text Input
        var textLabel = new TextBlock("Sample Text").LabelMedium();
        var sampleInput = new TextBox()
            .LeadingIcon(MaterialIconKind.Edit)
            .Placeholder("Type sample text here...")
            .BindText(_viewModel, vm => vm.SampleText, (vm, v) => vm.SampleText = v);

        // 2. Preset Buttons Row
        var presetLabel = new TextBlock("Material 3 Style Presets").LabelMedium();
        var presetWrap = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalSpacing = 6, VerticalSpacing = 6 };

        void AddPresetButton(string label, string key)
        {
            var btn = new Button(label)
                .Variant(ButtonVariant.Outlined);
            btn.Click += (s, e) => _viewModel.ApplyPreset(key);
            presetWrap.Add(btn);
        }

        AddPresetButton("Heading 1 (32pt)", MaterialTypography.Heading1Key);
        AddPresetButton("Heading 2 (28pt)", MaterialTypography.Heading2Key);
        AddPresetButton("Heading 3 (24pt)", MaterialTypography.Heading3Key);
        AddPresetButton("Title Large (22pt)", MaterialTypography.TitleLargeKey);
        AddPresetButton("Normal Text (14pt)", MaterialTypography.NormalTextKey);
        AddPresetButton("Subtext (12pt)", MaterialTypography.SubtextKey);
        AddPresetButton("Display Large (57pt)", MaterialTypography.DisplayLargeKey);

        // 3. Font Size Slider (10 to 60pt)
        var sizeHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        var sizeLabel = new TextBlock("Font Size:").LabelMedium();
        var sizeValueText = new TextBlock($"{_viewModel.FontSize:F0}pt").LabelMedium().Foreground(Color.FromHex("#3B82F6"));
        sizeHeader.Children(sizeLabel, sizeValueText);

        var sizeSlider = new Slider { Minimum = 10, Maximum = 60, Value = _viewModel.FontSize };
        sizeSlider.ValueChanged += (s, v) =>
        {
            _viewModel.FontSize = v;
            sizeValueText.Text = $"{v:F0}pt";
        };
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TypographyViewModel.FontSize))
            {
                sizeSlider.Value = _viewModel.FontSize;
                sizeValueText.Text = $"{_viewModel.FontSize:F0}pt";
            }
        };

        // 4. Font Family Selector
        var fontLabel = new TextBlock("Font Family").LabelMedium();
        var fontWrap = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalSpacing = 6, VerticalSpacing = 6 };

        foreach (var font in TypographyViewModel.AvailableFonts)
        {
            string f = font;
            var btn = new Button(f)
                .Variant(ButtonVariant.Outlined);
            btn.Click += (s, e) => _viewModel.FontFamily = f;
            fontWrap.Add(btn);
        }

        // 5. Switches (Bold, Italic, Muted, Word Wrap)
        var switchesRow = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalSpacing = 14, VerticalSpacing = 8 };

        var boldSwitch = new Switch("Bold")
            .BindIsChecked(_viewModel, vm => vm.IsBold, (vm, v) => vm.IsBold = v);

        var italicSwitch = new Switch("Italic")
            .BindIsChecked(_viewModel, vm => vm.IsItalic, (vm, v) => vm.IsItalic = v);

        var mutedSwitch = new Switch("Muted")
            .BindIsChecked(_viewModel, vm => vm.IsMuted, (vm, v) => vm.IsMuted = v);

        var wrapSwitch = new Switch("Word Wrap")
            .BindIsChecked(_viewModel, vm => vm.IsWrapping, (vm, v) => vm.IsWrapping = v);

        switchesRow.Children(boldSwitch, italicSwitch, mutedSwitch, wrapSwitch);

        // 6. Text Alignment Row
        var alignLabel = new TextBlock("Alignment").LabelMedium();
        var alignRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        void AddAlignButton(string label, TextAlignment alignment)
        {
            var btn = new Button(label)
                .Variant(ButtonVariant.Outlined);
            btn.Click += (s, e) => _viewModel.TextAlignment = alignment;
            alignRow.Add(btn);
        }

        AddAlignButton("Left", TextAlignment.Left);
        AddAlignButton("Center", TextAlignment.Center);
        AddAlignButton("Right", TextAlignment.Right);

        // 7. Color Swatches Row
        var colorLabel = new TextBlock("Foreground Color").LabelMedium();
        var colorRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        void AddColorButton(string name, Color color)
        {
            var btn = new Button(name)
                .Variant(ButtonVariant.Filled);
            btn.Click += (s, e) => _viewModel.SelectedColor = color;
            colorRow.Add(btn);
        }

        AddColorButton("Theme Default", Color.Transparent);
        AddColorButton("Primary", Color.FromHex("#3B82F6"));
        AddColorButton("Emerald", Color.FromHex("#10B981"));
        AddColorButton("Amber", Color.FromHex("#F59E0B"));
        AddColorButton("Rose", Color.FromHex("#E11D48"));
        AddColorButton("Purple", Color.FromHex("#8B5CF6"));

        controlsStack.Children(
            textLabel, sampleInput,
            presetLabel, presetWrap,
            sizeHeader, sizeSlider,
            fontLabel, fontWrap,
            switchesRow,
            alignLabel, alignRow,
            colorLabel, colorRow
        );

        controlsCard.Child = controlsStack;
        grid.Add(controlsCard.Column(1));

        return CreateSectionCard(
            "Interactive Typography Playground",
            "Experiment with font sizes, weights, slants, alignments, font families, and color tokens in real-time.",
            grid
        );
    }

    #endregion

    #region Section 2: Material Design 3 Type Scale & Common Headings

    private static UIElement CreateMd3ScaleSection()
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 };

        stack.Add(CreateTypeScaleRow(
            "Display Large",
            "57pt • Regular • Line-Height 64",
            new TextBlock("Display Large").DisplayLarge(),
            "DisplayLargeKey • 57pt"
        ));

        stack.Add(CreateTypeScaleRow(
            "Display Medium",
            "45pt • Regular • Line-Height 52",
            new TextBlock("Display Medium").DisplayMedium(),
            "DisplayMediumKey • 45pt"
        ));

        stack.Add(CreateTypeScaleRow(
            "Display Small",
            "36pt • Regular • Line-Height 44",
            new TextBlock("Display Small").DisplaySmall(),
            "DisplaySmallKey • 36pt"
        ));

        stack.Add(CreateTypeScaleRow(
            "Headline Large / Heading 1",
            "32pt • Bold • Primary Heading",
            new TextBlock("Heading 1 / Headline Large").Heading1(),
            "Heading1Key / HeadlineLargeKey • 32pt"
        ));

        stack.Add(CreateTypeScaleRow(
            "Headline Medium / Heading 2",
            "28pt • Bold • Secondary Heading",
            new TextBlock("Heading 2 / Headline Medium").Heading2(),
            "Heading2Key / HeadlineMediumKey • 28pt"
        ));

        stack.Add(CreateTypeScaleRow(
            "Headline Small / Heading 3",
            "24pt • Bold • Section Heading",
            new TextBlock("Heading 3 / Headline Small").Heading3(),
            "Heading3Key / HeadlineSmallKey • 24pt"
        ));

        stack.Add(CreateTypeScaleRow(
            "Title Large",
            "22pt • Bold • Component Title",
            new TextBlock("Title Large").TitleLarge(),
            "TitleLargeKey • 22pt"
        ));

        stack.Add(CreateTypeScaleRow(
            "Title Medium",
            "16pt • Bold • Card Subhead",
            new TextBlock("Title Medium").TitleMedium(),
            "TitleMediumKey • 16pt"
        ));

        stack.Add(CreateTypeScaleRow(
            "Title Small",
            "14pt • Bold • Compact Title",
            new TextBlock("Title Small").TitleSmall(),
            "TitleSmallKey • 14pt"
        ));

        stack.Add(CreateTypeScaleRow(
            "Body Large",
            "16pt • Regular • Editorial Copy",
            new TextBlock("Body Large provides comfortable reading for long-form articles, paragraphs, and reading flows.").BodyLarge(),
            "BodyLargeKey • 16pt"
        ));

        stack.Add(CreateTypeScaleRow(
            "Body Medium / Normal Text",
            "14pt • Regular • Default Application Body",
            new TextBlock("Normal Text / Body Medium is the primary readable text style for user interfaces, descriptions, and list items.").NormalText(),
            "NormalTextKey / BodyMediumKey • 14pt"
        ));

        stack.Add(CreateTypeScaleRow(
            "Body Small / Subtext",
            "12pt • Muted • Secondary Footnote & Caption",
            new TextBlock("Subtext / Body Small is used for auxiliary labels, timestamps, metadata, and helper text.").Subtext(),
            "SubtextKey / BodySmallKey • 12pt"
        ));

        stack.Add(CreateTypeScaleRow(
            "Caption",
            "11pt • Muted • Legal & Compact Disclaimers",
            new TextBlock("Caption style for fine print, disclaimers, and badge annotations.").Caption(),
            "CaptionKey • 11pt"
        ));

        return CreateSectionCard(
            "Material Design 3 Type Scale & Common Headings",
            "Standardized typography styles available globally via Atelier.Theming.Material. Compatible with StyleKey and fluent extensions.",
            stack
        );
    }

    private static UIElement CreateTypeScaleRow(string name, string specs, UIElement sample, string badge)
    {
        var card = new Card(CardVariant.Filled)
            .Padding(16, 14)
            .CornerRadius(10);

        var grid = new Grid()
            .Columns(new GridLength(220, GridUnitType.Pixel), GridLength.Star, GridLength.Auto)
            .ColumnSpacing(16);

        // Metadata column
        var metaStack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
            .Children(
                new TextBlock(name).TitleSmall(),
                new TextBlock(specs).Caption()
            );

        // Badge column
        var badgeCard = new Card(CardVariant.Outlined)
            .Padding(8, 4)
            .CornerRadius(6)
            .VerticalAlign(VerticalAlignment.Center)
            .Child(new TextBlock(badge).Caption());

        grid.Add(metaStack.Column(0));
        grid.Add(sample.Column(1).VerticalAlign(VerticalAlignment.Center));
        grid.Add(badgeCard.Column(2));

        card.Child = grid;
        return card;
    }

    #endregion

    #region Section 3: TextBlock Feature Demonstrations

    private static UIElement CreateTextBlockFeaturesSection()
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 16 };

        // 1. Weight & Slant Matrix (Regular, Bold, Italic, Bold Italic)
        var fontVariantsHeader = new TextBlock("Font Slants & Weight Combinations").TitleSmall();
        var fontVariantsGrid = new Grid()
            .Columns(GridLength.Star, GridLength.Star, GridLength.Star, GridLength.Star)
            .ColumnSpacing(10);

        fontVariantsGrid.Add(CreateFeatureCard("Regular", new TextBlock("Typography in Atelier").FontSize(15)).Column(0));
        fontVariantsGrid.Add(CreateFeatureCard("Bold", new TextBlock("Typography in Atelier").FontSize(15).Bold()).Column(1));
        fontVariantsGrid.Add(CreateFeatureCard("Italic", new TextBlock("Typography in Atelier").FontSize(15).Italic()).Column(2));
        fontVariantsGrid.Add(CreateFeatureCard("Bold Italic", new TextBlock("Typography in Atelier").FontSize(15).Bold().Italic()).Column(3));

        // 2. Font Families Showcase
        var familiesHeader = new TextBlock("Font Family Rendering").TitleSmall();
        var familiesGrid = new Grid()
            .Columns(GridLength.Star, GridLength.Star, GridLength.Star)
            .ColumnSpacing(10)
            .Rows(GridLength.Auto, GridLength.Auto)
            .RowSpacing(10);

        familiesGrid.Add(CreateFeatureCard("Segoe UI (Sans)", new TextBlock("Clean modern UI text").FontFamily("Segoe UI").FontSize(15)).Column(0).Row(0));
        familiesGrid.Add(CreateFeatureCard("Arial (Sans)", new TextBlock("Clean modern UI text").FontFamily("Arial").FontSize(15)).Column(1).Row(0));
        familiesGrid.Add(CreateFeatureCard("Georgia (Serif)", new TextBlock("Elegant editorial serif").FontFamily("Georgia").FontSize(15)).Column(2).Row(0));
        familiesGrid.Add(CreateFeatureCard("Consolas (Monospace)", new TextBlock("Code & monospace 0123").FontFamily("Consolas").FontSize(14)).Column(0).Row(1));
        familiesGrid.Add(CreateFeatureCard("Trebuchet MS (Geometric)", new TextBlock("Dynamic geometric sans").FontFamily("Trebuchet MS").FontSize(15)).Column(1).Row(1));
        familiesGrid.Add(CreateFeatureCard("Verdana (Legible)", new TextBlock("High readability text").FontFamily("Verdana").FontSize(14)).Column(2).Row(1));

        // 3. Color & Emphasis
        var colorsHeader = new TextBlock("Color Tokens & Semantic Emphasis").TitleSmall();
        var colorsGrid = new Grid()
            .Columns(GridLength.Star, GridLength.Star, GridLength.Star, GridLength.Star)
            .ColumnSpacing(10);

        colorsGrid.Add(CreateFeatureCard("Standard", new TextBlock("Default OnSurface text").FontSize(13)).Column(0));
        colorsGrid.Add(CreateFeatureCard("Muted / Secondary", new TextBlock("De-emphasized text").FontSize(13).Muted()).Column(1));
        colorsGrid.Add(CreateFeatureCard("Primary Accent", new TextBlock("Primary colored text").FontSize(13).Bold().Foreground(Color.FromHex("#3B82F6"))).Column(2));
        colorsGrid.Add(CreateFeatureCard("Alert / Error", new TextBlock("Important error warning").FontSize(13).Bold().Foreground(Color.FromHex("#EF4444"))).Column(3));

        // 4. Multi-Line Text Wrapping & Alignment
        var wrapHeader = new TextBlock("Text Wrapping & Multi-Line Alignment").TitleSmall();
        var wrapGrid = new Grid()
            .Columns(GridLength.Star, GridLength.Star, GridLength.Star)
            .ColumnSpacing(10);

        var leftText = new TextBlock("Left aligned multi-line paragraph. Text flows naturally with uniform left margin.")
            .FontSize(12)
            .TextAlignment(TextAlignment.Left)
            .TextWrapping(TextWrapping.Wrap);

        var centerText = new TextBlock("Center aligned paragraph. Every line is balanced horizontally around the center axis.")
            .FontSize(12)
            .TextAlignment(TextAlignment.Center)
            .TextWrapping(TextWrapping.Wrap);

        var rightText = new TextBlock("Right aligned paragraph. Used for numeric data, right-to-left indicators, and captions.")
            .FontSize(12)
            .TextAlignment(TextAlignment.Right)
            .TextWrapping(TextWrapping.Wrap);

        wrapGrid.Add(CreateFeatureCard("Align: Left", leftText).Column(0));
        wrapGrid.Add(CreateFeatureCard("Align: Center", centerText).Column(1));
        wrapGrid.Add(CreateFeatureCard("Align: Right", rightText).Column(2));

        stack.Children(
            fontVariantsHeader, fontVariantsGrid,
            familiesHeader, familiesGrid,
            colorsHeader, colorsGrid,
            wrapHeader, wrapGrid
        );

        return CreateSectionCard(
            "TextBlock Core Capabilities",
            "Demonstration of Bold, Italic, FontFamily, Muted, Foreground colors, TextAlignment, and responsive TextWrapping.",
            stack
        );
    }

    private static UIElement CreateFeatureCard(string title, UIElement content)
    {
        var card = new Card(CardVariant.Filled)
            .Padding(14)
            .CornerRadius(8);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 }
            .Children(
                new TextBlock(title).LabelSmall().Foreground(Color.FromHex("#3B82F6")),
                content
            );

        card.Child = stack;
        return card;
    }

    #endregion

    #region Section 4: Content Composition Pattern

    private static UIElement CreateCompositionSection()
    {
        var articleCard = new Card(CardVariant.Filled)
            .Padding(24)
            .CornerRadius(14);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        // Overline / Tag
        var overline = new TextBlock("MATERIAL DESIGN 3 • ARCHITECTURE")
            .LabelSmall()
            .Foreground(Color.FromHex("#3B82F6"));

        // Headline 1
        var headline = new TextBlock("Harmonious Typography in Cross-Platform UI Frameworks")
            .Heading1();

        // Byline / Subtext with Italic
        var bylineRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.AccountCircle, 18, foreground: Color.FromHex("#6B7280")),
                new TextBlock("By Google DeepMind Engineering").Subtext().Italic(),
                new TextBlock("•").Subtext(),
                new TextBlock("Updated September 2026").Subtext()
            );

        // Body paragraph 1
        var body1 = new TextBlock("Typography provides the visual foundation of any user interface. By combining a calibrated type scale with variable font axes and precise typographic hierarchy, applications achieve immediate clarity, aesthetic harmony, and seamless readability across all display densities.")
            .NormalText()
            .TextWrapping(TextWrapping.Wrap);

        // Pullquote Card with Left Accent Border
        var quoteCard = new Card(CardVariant.Outlined)
            .Padding(16, 12)
            .CornerRadius(8);

        var quoteText = new TextBlock("\"Good typography is like glass: it allows the content to shine through with effortless clarity, while subtle adjustments in weight and grade create depth without distraction.\"")
            .BodyMedium()
            .Italic()
            .TextWrapping(TextWrapping.Wrap);

        quoteCard.Child = quoteText;

        // Body paragraph 2
        var body2 = new TextBlock("Atelier's styling system resolves Material Design 3 type scales globally while honoring local property overrides. Controls like TextBlock support fluent chainability, reactive two-way data bindings, and hardware-accelerated Skia font caching.")
            .NormalText()
            .TextWrapping(TextWrapping.Wrap);

        // Footnote / Disclaimer
        var footnote = new TextBlock("Note: Material Design 3 type scales are automatically registered into StyleManager.GlobalStyles whenever a MaterialTheme is initialized.")
            .Caption();

        stack.Children(overline, headline, bylineRow, body1, quoteCard, body2, footnote);
        articleCard.Child = stack;

        return CreateSectionCard(
            "Real-World Content Composition",
            "An editorial layout demonstrating how Overline, Heading1, Subtext, NormalText, Italic pullquotes, and Captions harmonize together.",
            articleCard
        );
    }

    #endregion

    private static UIElement CreateSectionCard(string title, string description, UIElement content)
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(20)
            .CornerRadius(12);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        stack.Add(new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 }
            .Children(
                new TextBlock(title).TitleMedium(),
                new TextBlock(description).Subtext()
            )
        );

        stack.Add(content);
        card.Child = stack;
        return card;
    }
}

using System;
using System.Linq;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class TransitionsView : GalleryPage
{
    private readonly TransitionsViewModel _vm;

    public TransitionsView(TransitionsViewModel viewModel)
        : base(MaterialIconKind.Animation, "Transitions",
            "A TransitioningContentControl animates between its old and new content whenever the content changes: " +
            "sliding, fading, zooming or a combination, with an adjustable duration and easing curve.")
    {
        _vm = viewModel;
        Sections(PlaygroundSection(), GallerySection());
    }

    private UIElement PlaygroundSection()
    {
        var host = new TransitioningContentControl()
            .Height(200)
            .ClipToBounds()
            .BindContent(_vm, v => v.CurrentCard)
            .WithContentTemplate((TransitionCard card) => CardView(card))
            .Bind(TransitioningContentControl.TransitionProperty, _vm, v => v.Transition)
            .Bind(TransitioningContentControl.DurationProperty, _vm, v => (TimeSpan?)v.Duration)
            .Bind(TransitioningContentControl.EasingProperty, _vm, v => (Func<float, float>?)v.SelectedEasing.Curve)
            .OnTransitionStarted(_vm.OnTransitionStarted)
            .OnTransitionCompleted(_vm.OnTransitionCompleted);

        return Ui.Section("Playground",
            "The content is a view model; the content template turns it into a card. Pick a transition, duration and " +
            "easing, then switch the content.",
            Ui.Columns(240,
                Ui.Labeled("Transition", new ComboBox()
                    .ItemsSource(_vm.Transitions)
                    .BindSelectedItem(_vm, v => v.SelectedTransition, (v, t) => v.SelectedTransition = t ?? v.Transitions[0])),
                Ui.Labeled("Easing", new ComboBox()
                    .ItemsSource(_vm.Easings)
                    .BindSelectedItem(_vm, v => v.SelectedEasing, (v, e) => v.SelectedEasing = e ?? v.Easings[0])),
                Ui.SliderSetting("Duration (ms)", _vm, v => v.DurationMs, (v, x) => v.DurationMs = x, 100, 2000)),
            new Border()
                .Padding(16)
                .CornerRadius(12)
                .Themed(Border.BackgroundProperty, c => c.SurfaceContainer)
                .Child(host),
            Ui.Row(
                new Button().Variant(ButtonVariant.Outlined).Command(_vm.PreviousCommand),
                new Button().Command(_vm.NextCommand),
                Ui.Readout(_vm, v => $"Card {v.CardIndex + 1} of {v.Cards.Count}"),
                Ui.Readout(_vm, v => v.Status)),
            Ui.Code("new TransitioningContentControl()\n" +
                    "    .Transition(SlideTransition.Left()).Duration(TimeSpan.FromMilliseconds(400)).WithEasing(Easing.Emphasized)\n" +
                    "    .WithContentTemplate((TransitionCard card) => CardView(card))\n" +
                    "    .BindContent(vm, v => v.CurrentCard)"));
    }

    private UIElement GallerySection() => Ui.Section("Built-in transitions",
        "Every transition the framework ships with, including a composite of two. Press a button to play it.",
        Ui.Columns(200, _vm.Transitions.Select(TransitionTile).ToArray()));

    private UIElement TransitionTile(TransitionOption option)
    {
        int state = 0;
        var host = new TransitioningContentControl()
            .Height(96)
            .ClipToBounds()
            .Transition(option.Create())
            .Content(Face(0));

        return new StackPanel().Spacing(8).Children(
            new Border()
                .Padding(8)
                .CornerRadius(12)
                .Themed(Border.BackgroundProperty, c => c.SurfaceContainer)
                .Child(host),
            Ui.IconButton(MaterialIconKind.PlayArrow, option.Name, ButtonVariant.Tonal)
                .HorizontalAlignment(HorizontalAlignment.Stretch)
                .OnClick(() => host.Content = Face(++state)));
    }

    private static UIElement Face(int index) =>
        new Border()
            .CornerRadius(8)
            .Themed(Border.BackgroundProperty, c => (index % 2) == 0 ? c.PrimaryContainer : c.TertiaryContainer)
            .Child(new TextBlock((index % 2) == 0 ? "A" : "B")
                .HeadlineMedium()
                .Center()
                .Themed(TextBlock.ForegroundProperty, c => (index % 2) == 0 ? c.OnPrimaryContainer : c.OnTertiaryContainer));

    private static UIElement CardView(TransitionCard card) =>
        new Border()
            .Padding(24)
            .CornerRadius(16)
            .Themed(Border.BackgroundProperty, c => card.Tone switch { 0 => c.PrimaryContainer, 1 => c.SecondaryContainer, _ => c.TertiaryContainer })
            .Child(new Grid()
                .Columns(GridLength.Auto, GridLength.Star)
                .ColumnSpacing(20)
                .Children(
                    new Icon(card.Icon, 56)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Themed(Control.ForegroundProperty, c => card.Tone switch { 0 => c.OnPrimaryContainer, 1 => c.OnSecondaryContainer, _ => c.OnTertiaryContainer }),
                    new StackPanel().Spacing(6).Column(1).VerticalAlignment(VerticalAlignment.Center).Children(
                        new TextBlock(card.Title).HeadlineSmall()
                            .Themed(TextBlock.ForegroundProperty, c => card.Tone switch { 0 => c.OnPrimaryContainer, 1 => c.OnSecondaryContainer, _ => c.OnTertiaryContainer }),
                        new TextBlock(card.Subtitle).BodyLarge().TextWrapping()
                            .Themed(TextBlock.ForegroundProperty, c => card.Tone switch { 0 => c.OnPrimaryContainer, 1 => c.OnSecondaryContainer, _ => c.OnTertiaryContainer }))));
}

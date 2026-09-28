using System;
using System.Collections.Generic;
using Atelier.Controls;
using Atelier.Core.Animation;
using Atelier.Core.Keybinding;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

/// <summary>A built-in transition the page can use, with a factory so every use gets its own instance.</summary>
public sealed record TransitionOption(string Name, Func<ITransition> Create)
{
    public override string ToString() => Name;
}

/// <summary>A named easing curve.</summary>
public sealed record EasingOption(string Name, Func<float, float> Curve)
{
    public override string ToString() => Name;
}

/// <summary>A card shown by the transition playground.</summary>
public sealed record TransitionCard(string Title, string Subtitle, MaterialIconKind Icon, int Tone);

public partial class TransitionsViewModel : PageViewModel
{
    public IReadOnlyList<TransitionOption> Transitions { get; } =
    [
        new("Slide left", () => SlideTransition.Left()),
        new("Slide right", () => SlideTransition.Right()),
        new("Slide up", () => SlideTransition.Up()),
        new("Slide down", () => SlideTransition.Down()),
        new("Fade", () => FadeTransition.Default()),
        new("Zoom in", () => ZoomTransition.In()),
        new("Zoom out", () => ZoomTransition.Out()),
        new("Slide and fade left", () => SlideFadeTransition.Left()),
        new("Slide and fade right", () => SlideFadeTransition.Right()),
        new("Slide and fade up", () => SlideFadeTransition.Up()),
        new("Slide and fade down", () => SlideFadeTransition.Down()),
        new("Fade + zoom (composite)", () => new CompositeTransition(FadeTransition.Default(), ZoomTransition.In())),
    ];

    public IReadOnlyList<EasingOption> Easings { get; } =
    [
        new("Emphasized", Easing.Emphasized),
        new("Emphasized decelerate", Easing.EmphasizedDecelerate),
        new("Emphasized accelerate", Easing.EmphasizedAccelerate),
        new("Ease out cubic", Easing.EaseOutCubic),
        new("Ease in-out quad", Easing.EaseInOutQuad),
        new("Linear", Easing.Linear),
    ];

    public IReadOnlyList<TransitionCard> Cards { get; } =
    [
        new("Performance", "60 fps · 2.1 ms per frame · 14 draw calls", MaterialIconKind.Speed, 0),
        new("Profile", "Signed in with two-factor authentication", MaterialIconKind.AccountCircle, 1),
        new("Now playing", "Track 4 of 12 · 48 kHz FLAC", MaterialIconKind.MusicNote, 2),
        new("Theme", "Primary tone 40, container tone 90", MaterialIconKind.Palette, 0),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Transition))]
    private TransitionOption _selectedTransition;

    [ObservableProperty]
    private EasingOption _selectedEasing;

    [ObservableProperty]
    private float _durationMs = 400;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCard))]
    private int _cardIndex;

    [ObservableProperty]
    private string _status = "Press Next or Previous to switch the content";

    private int _started;
    private int _completed;

    public TransitionsViewModel()
    {
        PageTitle = "Transitions";
        PageIcon = MaterialIconKind.Animation;
        Keywords = "transition transitioningcontentcontrol animation slide fade zoom easing duration";
        _selectedTransition = Transitions[0];
        _selectedEasing = Easings[0];
    }

    /// <summary>A new instance of the selected transition (a transition is applied to one control at a time).</summary>
    public ITransition Transition => SelectedTransition.Create();

    public TransitionCard CurrentCard => Cards[CardIndex];

    public TimeSpan Duration => TimeSpan.FromMilliseconds(DurationMs);

    partial void OnDurationMsChanged(float value) => OnPropertyChanged(nameof(Duration));

    [RelayCommand]
    [property: Command("Next", "Transitions", Icon = MaterialIcons.ArrowForward, Description = "Show the next card with the chosen transition")]
    private void Next() => CardIndex = (CardIndex + 1) % Cards.Count;

    [RelayCommand]
    [property: Command("Previous", "Transitions", Icon = MaterialIcons.ArrowBack, Description = "Show the previous card with the chosen transition")]
    private void Previous() => CardIndex = (CardIndex + Cards.Count - 1) % Cards.Count;

    public void OnTransitionStarted() => Status = $"Started: {++_started} · completed: {_completed}";

    public void OnTransitionCompleted() => Status = $"Started: {_started} · completed: {++_completed}";
}

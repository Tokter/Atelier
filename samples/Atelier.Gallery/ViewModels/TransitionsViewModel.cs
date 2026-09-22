using System;
using System.Collections.Generic;
using Atelier.Controls;
using Atelier.Core.Animation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public enum TransitionType
{
    CrossFade,
    SlideLeft,
    SlideRight,
    SlideUp,
    SlideDown,
    ZoomIn,
    ZoomOut,
    SlideFadeLeft,
    SlideFadeRight
}

public class TransitionCardItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public MaterialIconKind Icon { get; set; }
    public string Category { get; set; } = string.Empty;
    public string DetailText { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#6750A4";
}

public class TransitionTypeItem
{
    public TransitionType Type { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public MaterialIconKind Icon { get; init; }

    public override string ToString() => Name;
}

public class EasingItem
{
    public int Index { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    public override string ToString() => Name;
}

public partial class TransitionsViewModel : PageViewModel
{
    [ObservableProperty]
    private TransitionType _selectedTransitionType = TransitionType.SlideLeft;

    [ObservableProperty]
    private TransitionTypeItem _selectedTransitionItem;

    [ObservableProperty]
    private float _durationMs = 400f;

    [ObservableProperty]
    private int _selectedEasingIndex = 0; // 0 = Emphasized, 1 = EaseOutCubic, 2 = EaseInOutQuad, 3 = Linear

    [ObservableProperty]
    private EasingItem _selectedEasingItem;

    [ObservableProperty]
    private int _currentCardIndex = 0;

    [ObservableProperty]
    private TransitionCardItem _currentCard;

    [ObservableProperty]
    private string _statusMessage = "Ready. Change cards or transitions to preview.";

    public List<TransitionTypeItem> TransitionTypes { get; } = new()
    {
        new TransitionTypeItem { Type = TransitionType.SlideLeft, Name = "Slide Left", Description = "Horizontal slide moving to the left", Icon = MaterialIconKind.ArrowBack },
        new TransitionTypeItem { Type = TransitionType.SlideRight, Name = "Slide Right", Description = "Horizontal slide moving to the right", Icon = MaterialIconKind.ArrowForward },
        new TransitionTypeItem { Type = TransitionType.SlideUp, Name = "Slide Up", Description = "Vertical slide moving upwards", Icon = MaterialIconKind.ArrowUpward },
        new TransitionTypeItem { Type = TransitionType.SlideDown, Name = "Slide Down", Description = "Vertical slide moving downwards", Icon = MaterialIconKind.ArrowDownward },
        new TransitionTypeItem { Type = TransitionType.CrossFade, Name = "Cross Fade", Description = "Smooth opacity dissolve transition", Icon = MaterialIconKind.BlurOn },
        new TransitionTypeItem { Type = TransitionType.ZoomIn, Name = "Zoom In", Description = "Scale magnification entrance", Icon = MaterialIconKind.ZoomIn },
        new TransitionTypeItem { Type = TransitionType.ZoomOut, Name = "Zoom Out", Description = "Scale demagnification exit", Icon = MaterialIconKind.ZoomOut },
        new TransitionTypeItem { Type = TransitionType.SlideFadeLeft, Name = "Slide & Fade (Left)", Description = "Combined slide left and cross-fade", Icon = MaterialIconKind.CompareArrows },
        new TransitionTypeItem { Type = TransitionType.SlideFadeRight, Name = "Slide & Fade (Right)", Description = "Combined slide right and cross-fade", Icon = MaterialIconKind.CompareArrows }
    };

    public List<EasingItem> EasingCurves { get; } = new()
    {
        new EasingItem { Index = 0, Name = "Emphasized (M3 Standard)", Description = "Expressive cubic bezier for focal elements" },
        new EasingItem { Index = 1, Name = "EaseOutCubic (Decelerate)", Description = "Gentle deceleration into resting position" },
        new EasingItem { Index = 2, Name = "EaseInOutQuad (Smooth)", Description = "Symmetrical acceleration and deceleration" },
        new EasingItem { Index = 3, Name = "Linear (Constant)", Description = "Even rate of change across duration" }
    };

    public List<TransitionCardItem> Cards { get; } = new()
    {
        new TransitionCardItem
        {
            Id = 0,
            Title = "Real-Time Telemetry",
            Subtitle = "Engine telemetry & GPU frame pipeline",
            Icon = MaterialIconKind.Speed,
            Category = "Performance Monitor",
            DetailText = "60.0 FPS • Frame latency: 2.1ms • GPU Draw calls: 14 • Memory: 42.8 MB",
            AccentColor = "#006A6A"
        },
        new TransitionCardItem
        {
            Id = 1,
            Title = "User Profile & Security",
            Subtitle = "Account settings and identity preferences",
            Icon = MaterialIconKind.AccountCircle,
            Category = "User Management",
            DetailText = "Authenticated as Administrator. Two-factor authentication enabled via Security Key.",
            AccentColor = "#6750A4"
        },
        new TransitionCardItem
        {
            Id = 2,
            Title = "Now Playing: Atelier Symphony",
            Subtitle = "High-fidelity audio stream with Skia visuals",
            Icon = MaterialIconKind.MusicNote,
            Category = "Media Center",
            DetailText = "Track 4 of 12 • 48 kHz / 24-bit FLAC • Spatial Audio Enabled • Volume 85%",
            AccentColor = "#984061"
        },
        new TransitionCardItem
        {
            Id = 3,
            Title = "Material Design 3 Palette",
            Subtitle = "Dynamic tone generation & accessible contrast",
            Icon = MaterialIconKind.Palette,
            Category = "Theme Studio",
            DetailText = "Current tone primary 40, container 90, on-container 10. WCAG AAA Compliant.",
            AccentColor = "#7D5260"
        }
    };

    public TransitionsViewModel()
    {
        PageTitle = "Transitions";
        PageIcon = MaterialIconKind.Animation;
        _currentCard = Cards[0];
        _selectedTransitionItem = TransitionTypes[0]; // SlideLeft
        _selectedTransitionType = TransitionTypes[0].Type;
        _selectedEasingItem = EasingCurves[0]; // Emphasized
        _selectedEasingIndex = 0;
    }

    public ITransition CreateCurrentTransition()
    {
        var duration = TimeSpan.FromMilliseconds(DurationMs);
        Func<float, float> easing = SelectedEasingIndex switch
        {
            1 => Easing.EaseOutCubic,
            2 => Easing.EaseInOutQuad,
            3 => Easing.Linear,
            _ => Easing.Emphasized
        };

        return SelectedTransitionType switch
        {
            TransitionType.CrossFade => new FadeTransition(duration, easing),
            TransitionType.SlideLeft => new SlideTransition(SlideDirection.Left, duration, easing),
            TransitionType.SlideRight => new SlideTransition(SlideDirection.Right, duration, easing),
            TransitionType.SlideUp => new SlideTransition(SlideDirection.Up, duration, easing),
            TransitionType.SlideDown => new SlideTransition(SlideDirection.Down, duration, easing),
            TransitionType.ZoomIn => new ZoomTransition(ZoomMode.In, duration, easing),
            TransitionType.ZoomOut => new ZoomTransition(ZoomMode.Out, duration, easing),
            TransitionType.SlideFadeLeft => new SlideFadeTransition(SlideDirection.Left, duration, easing),
            TransitionType.SlideFadeRight => new SlideFadeTransition(SlideDirection.Right, duration, easing),
            _ => new FadeTransition(duration, easing)
        };
    }

    [RelayCommand]
    public void NextCard()
    {
        CurrentCardIndex = (CurrentCardIndex + 1) % Cards.Count;
        CurrentCard = Cards[CurrentCardIndex];
        StatusMessage = $"Transitioned to card #{CurrentCardIndex + 1}: {CurrentCard.Title} ({SelectedTransitionType})";
    }

    [RelayCommand]
    public void PreviousCard()
    {
        CurrentCardIndex = (CurrentCardIndex - 1 + Cards.Count) % Cards.Count;
        CurrentCard = Cards[CurrentCardIndex];
        StatusMessage = $"Transitioned to card #{CurrentCardIndex + 1}: {CurrentCard.Title} ({SelectedTransitionType})";
    }

    [RelayCommand]
    public void SelectCard(int index)
    {
        if (index >= 0 && index < Cards.Count)
        {
            CurrentCardIndex = index;
            CurrentCard = Cards[CurrentCardIndex];
            StatusMessage = $"Jumped to card #{CurrentCardIndex + 1}: {CurrentCard.Title} ({SelectedTransitionType})";
        }
    }

    [RelayCommand]
    public void SetTransitionType(TransitionType type)
    {
        SelectedTransitionType = type;
        var item = TransitionTypes.Find(t => t.Type == type);
        if (item != null && SelectedTransitionItem != item)
        {
            SelectedTransitionItem = item;
        }
        StatusMessage = $"Active transition mode set to: {item?.Name ?? type.ToString()}";
    }

    partial void OnSelectedTransitionItemChanged(TransitionTypeItem value)
    {
        if (value != null && SelectedTransitionType != value.Type)
        {
            SelectedTransitionType = value.Type;
            StatusMessage = $"Active transition mode set to: {value.Name}";
        }
    }

    partial void OnSelectedTransitionTypeChanged(TransitionType value)
    {
        var item = TransitionTypes.Find(t => t.Type == value);
        if (item != null && SelectedTransitionItem != item)
        {
            SelectedTransitionItem = item;
        }
    }

    [RelayCommand]
    public void SetEasing(int easingIndex)
    {
        SelectedEasingIndex = easingIndex;
        var item = EasingCurves.Find(e => e.Index == easingIndex);
        if (item != null && SelectedEasingItem != item)
        {
            SelectedEasingItem = item;
        }
        string name = item?.Name ?? "Emphasized (M3 Standard)";
        StatusMessage = $"Easing curve set to: {name}";
    }

    partial void OnSelectedEasingItemChanged(EasingItem value)
    {
        if (value != null && SelectedEasingIndex != value.Index)
        {
            SelectedEasingIndex = value.Index;
            StatusMessage = $"Easing curve set to: {value.Name}";
        }
    }

    partial void OnSelectedEasingIndexChanged(int value)
    {
        var item = EasingCurves.Find(e => e.Index == value);
        if (item != null && SelectedEasingItem != item)
        {
            SelectedEasingItem = item;
        }
    }
}

using Atelier.Core.Primitives;

namespace Atelier.NodeEditor;

/// <summary>A connection from an output to an input. Create links with <see cref="NodeGraphViewModel.Connect"/>.</summary>
public sealed class LinkViewModel : NodeGraphObject
{
    private bool _isSelected;

    internal LinkViewModel(OutputSocketViewModel from, InputSocketViewModel to)
    {
        From = from;
        To = to;
    }

    /// <summary>Gets the output the link starts at.</summary>
    public OutputSocketViewModel From { get; }

    /// <summary>Gets the input the link feeds.</summary>
    public InputSocketViewModel To { get; }

    /// <summary>Gets the color the link is drawn in: its output's type color.</summary>
    public Color Color => From.Type.Color;

    /// <summary>Gets or sets whether the link is selected (not recorded for undo).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <inheritdoc/>
    public override string ToString() => $"{From} -> {To}";
}

using Atelier.Core.Primitives;

namespace Atelier.Controls;

/// <summary>
/// Defines the configuration for an action button within a <see cref="Dialog"/>.
/// </summary>
public class DialogButton
{
    /// <summary>
    /// Gets or sets the label text displayed on the button.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the <see cref="DialogResult"/> returned when this button is clicked.
    /// </summary>
    public DialogResult Result { get; set; } = DialogResult.Ok;

    /// <summary>
    /// Gets or sets whether this button is triggered when the user presses Enter.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets whether this button is triggered when the user presses Escape.
    /// </summary>
    public bool IsCancel { get; set; }

    /// <summary>
    /// Gets or sets the visual variant for this button (e.g. Filled, Outlined, Text).
    /// </summary>
    public ButtonVariant Variant { get; set; } = ButtonVariant.Text;

    /// <summary>
    /// Gets or sets optional custom user data associated with this button.
    /// </summary>
    public object? Tag { get; set; }

    /// <summary>Initializes a new <see cref="DialogButton"/> with empty text and <see cref="DialogResult.Ok"/>.</summary>
    public DialogButton()
    {
    }

    /// <summary>Initializes a new <see cref="DialogButton"/>.</summary>
    /// <param name="text">The label text.</param>
    /// <param name="result">The result returned when the button is clicked.</param>
    /// <param name="isDefault">Whether Enter triggers the button.</param>
    /// <param name="isCancel">Whether Escape triggers the button.</param>
    /// <param name="variant">The button's visual variant.</param>
    /// <param name="tag">Optional user data returned in <see cref="DialogResponse.Tag"/>.</param>
    public DialogButton(string text, DialogResult result = DialogResult.Ok, bool isDefault = false, bool isCancel = false, ButtonVariant variant = ButtonVariant.Text, object? tag = null)
    {
        Text = text;
        Result = result;
        IsDefault = isDefault;
        IsCancel = isCancel;
        Variant = variant;
        Tag = tag;
    }
}

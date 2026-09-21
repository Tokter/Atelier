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

    public DialogButton()
    {
    }

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

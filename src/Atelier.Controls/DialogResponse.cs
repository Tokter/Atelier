using Atelier.Core.Primitives;

namespace Atelier.Controls;

/// <summary>
/// Encapsulates the response information when a <see cref="Dialog"/> is dismissed or a button is clicked.
/// Supports implicit conversion to <see cref="DialogResult"/>.
/// </summary>
public class DialogResponse
{
    /// <summary>
    /// Gets the resulting action code of the dialog.
    /// </summary>
    public DialogResult Result { get; }

    /// <summary>
    /// Gets the specific <see cref="DialogButton"/> that was pressed, or null if dismissed via scrim click or external close.
    /// </summary>
    public DialogButton? Button { get; }

    /// <summary>
    /// Gets the label of the pressed button, if available.
    /// </summary>
    public string? ButtonText => Button?.Text;

    /// <summary>
    /// Gets any custom payload or data attached to the pressed button.
    /// </summary>
    public object? Tag => Button?.Tag;

    public DialogResponse(DialogResult result, DialogButton? button = null)
    {
        Result = result;
        Button = button;
    }

    /// <summary>
    /// Implicitly converts a <see cref="DialogResponse"/> to its <see cref="DialogResult"/> for concise conditional checks.
    /// </summary>
    public static implicit operator DialogResult(DialogResponse response) => response?.Result ?? DialogResult.None;

    public override string ToString() => $"DialogResponse({Result}, Button='{ButtonText}')";
}

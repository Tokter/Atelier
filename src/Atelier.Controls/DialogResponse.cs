using System;
using System.ComponentModel;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;

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

    /// <summary>Initializes a new <see cref="DialogResponse"/>.</summary>
    /// <param name="result">The result code.</param>
    /// <param name="button">The pressed button, or <c>null</c> when the dialog was dismissed otherwise.</param>
    public DialogResponse(DialogResult result, DialogButton? button = null)
    {
        Result = result;
        Button = button;
    }

    /// <summary>
    /// Implicitly converts a <see cref="DialogResponse"/> to its <see cref="DialogResult"/> for concise conditional checks.
    /// </summary>
    public static implicit operator DialogResult(DialogResponse response) => response?.Result ?? DialogResult.None;

    /// <summary>Returns a description of the result and the pressed button's text.</summary>
    public override string ToString() => $"DialogResponse({Result}, Button='{ButtonText}')";
}

/// <summary>
/// Provides data for <see cref="Dialog.Closing"/>. Set <see cref="CancelEventArgs.Cancel"/> to keep the dialog open.
/// </summary>
public class DialogClosingEventArgs : CancelEventArgs
{
    /// <summary>Initializes a new <see cref="DialogClosingEventArgs"/>.</summary>
    /// <param name="response">The response the dialog would complete with.</param>
    public DialogClosingEventArgs(DialogResponse response)
    {
        Response = response;
    }

    /// <summary>Gets the response the dialog would complete with.</summary>
    public DialogResponse Response { get; }
}

/// <summary>
/// Provides data for <see cref="Dialog.Closed"/>.
/// </summary>
public class DialogClosedEventArgs : EventArgs
{
    /// <summary>Initializes a new <see cref="DialogClosedEventArgs"/>.</summary>
    /// <param name="response">The response the dialog completed with.</param>
    public DialogClosedEventArgs(DialogResponse response)
    {
        Response = response;
    }

    /// <summary>Gets the response the dialog completed with (<see cref="DialogResult.None"/> when it was removed without a result).</summary>
    public DialogResponse Response { get; }
}

/// <summary>
/// Provides data for <see cref="DialogHost.DialogOpened"/> and <see cref="DialogHost.DialogClosed"/>.
/// </summary>
public class DialogHostEventArgs : EventArgs
{
    /// <summary>Initializes a new <see cref="DialogHostEventArgs"/>.</summary>
    /// <param name="dialog">The dialog element that was shown or removed.</param>
    /// <param name="response">The response it closed with, or <c>null</c> when it opened.</param>
    public DialogHostEventArgs(UIElement dialog, DialogResponse? response)
    {
        Dialog = dialog;
        Response = response;
    }

    /// <summary>Gets the dialog element that was shown or removed (a <see cref="Controls.Dialog"/> or any other element).</summary>
    public UIElement Dialog { get; }

    /// <summary>Gets the response the dialog closed with, or <c>null</c> for <see cref="DialogHost.DialogOpened"/>.</summary>
    public DialogResponse? Response { get; }
}

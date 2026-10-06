using System.ComponentModel;

namespace Atelier.Core.Platform;

/// <summary>
/// Provides data for a window's <c>Closing</c> event, raised when the user or the app asks the window to close: the
/// title bar's close button, Alt+F4, the taskbar or Dock, the system menu, or a call to <c>Close()</c>.
/// </summary>
/// <remarks>
/// <para>
/// Set <see cref="CancelEventArgs.Cancel"/> to keep the window open. To ask the user first (for example whether to save
/// unsaved changes), pass the answer to <see cref="Defer(Task{bool})"/>: the window stays open while the task runs and
/// closes once it completes with <c>true</c>. Close requests that arrive while a deferred decision is still open are
/// ignored, so pressing Alt+F4 again doesn't stack a second prompt.
/// </para>
/// <para>Closing the window with <c>Close(force: true)</c> doesn't raise the event.</para>
/// </remarks>
/// <example>
/// <code>
/// window.Closing += (s, e) =>
/// {
///     if (document.HasUnsavedChanges)
///     {
///         e.Defer(AskToSaveAsync()); // true closes the window, false keeps it open
///     }
/// };
///
/// async Task&lt;bool&gt; AskToSaveAsync()
/// {
///     var dialog = new Dialog("Save changes?", "Your changes will be lost if you don't save them.")
///         .AddButton("Cancel", DialogResult.Cancel, isCancel: true)
///         .AddButton("Don't save", DialogResult.No)
///         .AddButton("Save", DialogResult.Yes, isDefault: true);
///     var result = (await dialog.ShowAsync(root)).Result;
///     if (result == DialogResult.Yes) document.Save();
///     return result is DialogResult.Yes or DialogResult.No;
/// }
/// </code>
/// </example>
public class WindowClosingEventArgs : CancelEventArgs
{
    private List<Task<bool>>? _deferrals;

    /// <summary>Gets the decisions passed to <see cref="Defer(Task{bool})"/>, in the order they were added.</summary>
    public IReadOnlyList<Task<bool>> Deferrals => (IReadOnlyList<Task<bool>>?)_deferrals ?? [];

    /// <summary>
    /// Keeps the window open until <paramref name="canClose"/> completes, then closes it if every deferred decision is
    /// <c>true</c> and no handler set <see cref="CancelEventArgs.Cancel"/>. A faulted or canceled task keeps the window
    /// open.
    /// </summary>
    /// <param name="canClose">Completes with whether the window may close, for example after asking the user.</param>
    public void Defer(Task<bool> canClose)
    {
        ArgumentNullException.ThrowIfNull(canClose);
        (_deferrals ??= new List<Task<bool>>()).Add(canClose);
    }
}

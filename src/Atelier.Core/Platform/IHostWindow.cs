namespace Atelier.Core.Platform;

/// <summary>
/// A top-level window that displays an element tree, as seen from the elements inside it.
/// </summary>
/// <remarks>
/// A window passes itself to <see cref="Tree.VisualNode.AttachToHost(IHostWindow?)"/> for its content, and any element
/// in that tree can reach it through <see cref="Tree.VisualNode.Host"/>. This lets controls such as a custom title bar
/// act on their own window when several windows are open.
/// </remarks>
public interface IHostWindow
{
    /// <summary>Gets whether the window is maximized.</summary>
    bool IsMaximized { get; }

    /// <summary>
    /// Occurs on the UI thread after the window is maximized, minimized or restored, however that happened (title bar
    /// buttons, keyboard shortcuts, snapping).
    /// </summary>
    /// <remarks>The default implementation never raises the event; windows that can change state should implement it.</remarks>
    event EventHandler? WindowStateChanged
    {
        add { }
        remove { }
    }

    /// <summary>Minimizes the window.</summary>
    void Minimize();

    /// <summary>Maximizes the window, or restores it if it is maximized.</summary>
    void ToggleMaximize();

    /// <summary>Closes the window.</summary>
    void Close();

    /// <summary>Starts moving the window with the pointer (call while a pointer button is pressed).</summary>
    void DragMove();
}

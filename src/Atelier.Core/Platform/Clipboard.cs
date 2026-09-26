namespace Atelier.Core.Platform;

/// <summary>
/// Abstraction over the system clipboard, implemented by the platform layer.
/// </summary>
public interface IClipboard
{
    /// <summary>Gets the clipboard text, or <c>null</c> if it holds no text.</summary>
    string? GetText();

    /// <summary>Replaces the clipboard content with <paramref name="text"/>.</summary>
    void SetText(string? text);

    /// <summary>Determines whether the clipboard holds non-empty text.</summary>
    bool ContainsText();
}

/// <summary>
/// Provides access to the current <see cref="IClipboard"/> implementation.
/// </summary>
public static class Clipboard
{
    private static IClipboard? _current;

    /// <summary>
    /// Gets or sets the clipboard implementation. Defaults to <see cref="NullClipboard"/>, an in-process buffer used
    /// when no platform clipboard has been installed (for example in tests).
    /// </summary>
    public static IClipboard Current
    {
        get => _current ?? NullClipboard.Instance;
        set => _current = value;
    }

    /// <summary>Gets the clipboard text, or <c>null</c> if it holds no text.</summary>
    public static string? GetText() => Current.GetText();

    /// <summary>Replaces the clipboard content with <paramref name="text"/>.</summary>
    public static void SetText(string? text) => Current.SetText(text);

    /// <summary>Determines whether the clipboard holds non-empty text.</summary>
    public static bool ContainsText() => Current.ContainsText();

    /// <summary>
    /// An in-process clipboard that is not shared with other applications.
    /// </summary>
    public class NullClipboard : IClipboard
    {
        /// <summary>The shared instance.</summary>
        public static readonly NullClipboard Instance = new();

        private string? _buffer;

        /// <inheritdoc/>
        public string? GetText() => _buffer;

        /// <inheritdoc/>
        public void SetText(string? text) => _buffer = text;

        /// <inheritdoc/>
        public bool ContainsText() => !string.IsNullOrEmpty(_buffer);
    }
}

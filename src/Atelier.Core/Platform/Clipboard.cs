using System;

namespace Atelier.Core.Platform;

public interface IClipboard
{
    string? GetText();
    void SetText(string? text);
    bool ContainsText();
}

public static class Clipboard
{
    private static IClipboard? _current;

    public static IClipboard Current
    {
        get => _current ?? NullClipboard.Instance;
        set => _current = value;
    }

    public static string? GetText() => Current.GetText();
    public static void SetText(string? text) => Current.SetText(text);
    public static bool ContainsText() => Current.ContainsText();

    public class NullClipboard : IClipboard
    {
        public static readonly NullClipboard Instance = new();
        private string? _buffer;

        public string? GetText() => _buffer;
        public void SetText(string? text) => _buffer = text;
        public bool ContainsText() => !string.IsNullOrEmpty(_buffer);
    }
}

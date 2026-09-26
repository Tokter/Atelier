using System.Text;

namespace Atelier.Core;

/// <summary>
/// String helpers used by text rendering.
/// </summary>
public static class StringEx
{
    /// <summary>
    /// Determines whether <paramref name="input"/> contains a character from the common emoji and pictograph ranges,
    /// which typically need a color emoji font to render.
    /// </summary>
    /// <remarks>
    /// This is a fast range check, not a full Unicode emoji classification: some symbols in these ranges
    /// (e.g. arrows, ©, ®) are included because fonts commonly render them as emoji.
    /// </remarks>
    /// <param name="input">The text to check.</param>
    /// <returns><c>true</c> if any code point falls in an emoji range.</returns>
    public static bool ContainsEmoji(this string input)
    {
        if (string.IsNullOrEmpty(input)) return false;

        foreach (var rune in input.EnumerateRunes())
        {
            if (IsEmojiRune(rune))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEmojiRune(Rune rune)
    {
        int v = rune.Value;

        return
            (v >= 0x1F300 && v <= 0x1FAFF) || // Misc/Supplemental Symbols and Pictographs
            (v >= 0x1F1E6 && v <= 0x1F1FF) || // Regional indicator symbols (flags)
            (v >= 0x2194 && v <= 0x21AA) ||   // Arrows (↔, ↕, ↖, ↗, ↘, ↙, etc.)
            (v >= 0x2300 && v <= 0x23FF) ||   // Misc Technical: ⏪, ⏩, ⌚, ⌛, ⏰, etc
            (v >= 0x25AA && v <= 0x25FE) ||   // Geometric shapes: ▪, ▫, ▶, ◀, ◻, etc.
            (v >= 0x2600 && v <= 0x26FF) ||   // Misc symbols
            (v >= 0x2700 && v <= 0x27BF) ||   // Dingbats
            (v >= 0x2934 && v <= 0x2935) ||   // ⤴, ⤵
            (v >= 0x2B05 && v <= 0x2B55) ||   // Arrows & symbols: ⭐, ⭕, etc.
            v == 0x00A9 || v == 0x00AE || v == 0x203C || v == 0x2049 ||
            v == 0x2122 || v == 0x2139 || v == 0x3030 || v == 0x303D ||
            v == 0x3297 || v == 0x3299;
    }
}

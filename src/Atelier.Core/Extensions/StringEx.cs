using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Atelier.Core
{
    public static class StringEx
    {
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
}

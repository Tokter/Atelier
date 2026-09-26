using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Atelier.Rendering;

/// <summary>
/// A least-recently-used cache of shaped text (<see cref="SKTextBlob"/>) per (text, font).
/// </summary>
/// <remarks>
/// <c>SKCanvas.DrawText(string, ...)</c> shapes the text into a new native text blob on every call. Drawing through this
/// cache shapes each (text, font) pair once, so re-rendering unchanged text allocates nothing. Keys compare the text by
/// value and the font by reference (fonts come from the shared <see cref="FontCache"/>). At most
/// <see cref="MaxEntries"/> blobs are kept per thread; evicted blobs are disposed.
/// </remarks>
internal static class TextBlobCache
{
    /// <summary>The maximum number of cached blobs per thread.</summary>
    public const int MaxEntries = 2048;

    private readonly record struct Key(string Text, SKFont Font);

    private sealed class Entry(Key key, SKTextBlob? blob)
    {
        public Key Key { get; } = key;
        public SKTextBlob? Blob { get; } = blob;
    }

    // Rendering happens on UI threads; a cache per thread needs no locking.
    [ThreadStatic] private static Dictionary<Key, LinkedListNode<Entry>>? t_entries;
    [ThreadStatic] private static LinkedList<Entry>? t_lru;

    /// <summary>
    /// Returns the cached blob for <paramref name="text"/> shaped with <paramref name="font"/>, creating it on first use.
    /// Returns <c>null</c> for text that produces no glyphs. The blob is owned by the cache: use it right away.
    /// </summary>
    public static SKTextBlob? Get(string text, SKFont font)
    {
        var entries = t_entries ??= new Dictionary<Key, LinkedListNode<Entry>>();
        var lru = t_lru ??= new LinkedList<Entry>();
        var key = new Key(text, font);

        if (entries.TryGetValue(key, out var node))
        {
            if (node != lru.First)
            {
                lru.Remove(node);
                lru.AddFirst(node);
            }
            return node.Value.Blob;
        }

        if (entries.Count >= MaxEntries)
        {
            var last = lru.Last!;
            lru.RemoveLast();
            entries.Remove(last.Value.Key);
            last.Value.Blob?.Dispose();
        }

        var entry = new Entry(key, SKTextBlob.Create(text, font));
        entries[key] = lru.AddFirst(entry);
        return entry.Blob;
    }
}

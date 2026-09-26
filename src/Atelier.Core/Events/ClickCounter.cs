using System;
using Atelier.Core.Primitives;

namespace Atelier.Core.Events;

/// <summary>
/// Counts consecutive clicks (single, double, triple, ...) from pointer presses, for filling
/// <see cref="PointerEventArgs.ClickCount"/>. Used by platform backends.
/// </summary>
/// <remarks>
/// A press continues the current sequence when it uses the same button, happens within the double-click time of the
/// previous press, and lies within the double-click distance of it; otherwise it starts a new sequence at 1.
/// </remarks>
public sealed class ClickCounter
{
    private PointerButtons _lastButton = PointerButtons.None;
    private long _lastTimestampMs;
    private Point _lastPosition;

    /// <summary>Gets the click count of the most recent press, or 0 before the first press.</summary>
    public int Count { get; private set; }

    /// <summary>
    /// Registers a press and returns its click count.
    /// </summary>
    /// <param name="button">The pressed button.</param>
    /// <param name="position">The press position in window coordinates.</param>
    /// <param name="timestampMs">The press time in milliseconds (any monotonic clock).</param>
    /// <param name="doubleClickTimeMs">The maximum time between presses of one sequence, as configured by the OS.</param>
    /// <param name="doubleClickDistance">The maximum distance (per axis) between presses of one sequence.</param>
    /// <returns>1 for a single click, 2 for a double click, and so on.</returns>
    public int RegisterPress(PointerButtons button, Point position, long timestampMs, int doubleClickTimeMs, float doubleClickDistance)
    {
        bool continuesSequence =
            Count > 0 &&
            button == _lastButton &&
            timestampMs - _lastTimestampMs <= doubleClickTimeMs &&
            MathF.Abs(position.X - _lastPosition.X) <= doubleClickDistance &&
            MathF.Abs(position.Y - _lastPosition.Y) <= doubleClickDistance;

        Count = continuesSequence ? Count + 1 : 1;
        _lastButton = button;
        _lastTimestampMs = timestampMs;
        _lastPosition = position;
        return Count;
    }

    /// <summary>
    /// Returns the click count to report for a release of <paramref name="button"/>: the count of the press it ends,
    /// or 1 if the last press used another button.
    /// </summary>
    public int GetReleaseCount(PointerButtons button) => button == _lastButton && Count > 0 ? Count : 1;
}

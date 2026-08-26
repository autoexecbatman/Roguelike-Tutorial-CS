/*
 * What has happened lately, in the order it happened.
 *
 * The log keeps a bounded number of lines and drops the oldest when it overflows, so a long game
 * cannot grow it without limit. Part 7 draws it on screen; this part only fills it.
 *
 * Usage:
 *
 *     MessageLog log = new MessageLog(capacity: 100);
 *
 *     log.Add("You hit the Rat for 3 damage.");
 *     log.Add("Rat dies.");
 *
 *     IReadOnlyList<string> all = log.Messages;       // oldest first
 *     IReadOnlyList<string> last = log.Latest(5);     // the newest five, still oldest first
 *
 * Refuses a capacity below one, a null or blank message, and a negative count.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RogueTutorial;

internal sealed class MessageLog
{
    // The lines held, oldest first. Trimmed from the front when it passes capacity.
    private readonly List<string> _messages = new List<string>();

    /// <summary>The most lines kept. Older ones are dropped when this is passed.</summary>
    public int Capacity { get; }

    /// <summary>Everything currently held, oldest first.</summary>
    public IReadOnlyList<string> Messages => _messages;

    /// <summary>
    /// Creates an empty log holding at most the given number of lines. Throws
    /// ArgumentOutOfRangeException on a capacity below one, since a log that can hold nothing is
    /// a configuration mistake rather than a way of switching logging off.
    /// </summary>
    public MessageLog(int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "A log must hold at least one message.");
        }

        Capacity = capacity;
    }

    /// <summary>
    /// Appends a line, dropping the oldest if that puts the log over capacity. Throws
    /// ArgumentException on a null, empty or whitespace message: a blank line in a log is a
    /// formatting bug somewhere upstream, and silently keeping it hides the cause.
    /// </summary>
    public void Add(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A log message cannot be blank.", nameof(message));
        }

        _messages.Add(message);

        // One line in, at most one line out, so this never needs to be a loop.
        if (_messages.Count > Capacity)
        {
            _messages.RemoveAt(0);
        }

        Debug.Assert(_messages.Count <= Capacity, "The log must never hold more than its capacity.");
    }

    /// <summary>
    /// The newest lines, still oldest first, so they read top to bottom. Returns everything when
    /// fewer than that many have been logged. Throws ArgumentOutOfRangeException on a negative
    /// count; zero legitimately returns nothing.
    /// </summary>
    public IReadOnlyList<string> Latest(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Cannot take a negative number of messages.");
        }

        // Skipping from the front keeps the order rather than reversing it.
        int skip = Math.Max(0, _messages.Count - count);

        return _messages.Skip(skip).ToList();
    }
}

/*
 * Translates pressed keys into a movement offset, with no dependency on a running game.
 *
 * Usage - hand it the keys that are down this frame and get one combined offset back:
 *
 *     Point offset = MovementKeys.OffsetFor(new[] { Keys.Left });              // -> (-1, 0)
 *     Point diagonal = MovementKeys.OffsetFor(new[] { Keys.Left, Keys.Up });   // -> (-1, -1)
 *     Point corner = MovementKeys.OffsetFor(new[] { Keys.NumPad7 });           // -> (-1, -1)
 *     Point none = MovementKeys.OffsetFor(new[] { Keys.A });                   // -> (0, 0), key ignored
 *
 * Refuses a null collection. Opposing keys cancel: Left with Right yields no horizontal move.
 */

using System;
using System.Collections.Generic;
using SadConsole.Input;
using SadRogue.Primitives;

namespace RogueTutorial;

internal static class MovementKeys
{
    // Every key that moves the player, paired with the offset it contributes.
    // Held in one table so the mapping is data rather than a chain of if statements.
    private static readonly IReadOnlyDictionary<Keys, Point> OffsetByKey = new Dictionary<Keys, Point>
    {
        [Keys.Left] = new Point(-1, 0),
        [Keys.Right] = new Point(1, 0),
        [Keys.Up] = new Point(0, -1),
        [Keys.Down] = new Point(0, 1),
        [Keys.NumPad4] = new Point(-1, 0),
        [Keys.NumPad6] = new Point(1, 0),
        [Keys.NumPad8] = new Point(0, -1),
        [Keys.NumPad2] = new Point(0, 1),
        [Keys.NumPad7] = new Point(-1, -1),
        [Keys.NumPad9] = new Point(1, -1),
        [Keys.NumPad1] = new Point(-1, 1),
        [Keys.NumPad3] = new Point(1, 1),
    };

    /// <summary>
    /// Sums the offsets of every movement key in the collection, ignoring keys that do not
    /// move the player. Returns Point.Zero when nothing relevant is pressed, which the caller
    /// reads as "no move this frame". Throws ArgumentNullException on a null collection.
    /// </summary>
    public static Point OffsetFor(IReadOnlyCollection<Keys> pressedKeys)
    {
        // A null collection is a wiring error rather than an empty frame.
        ArgumentNullException.ThrowIfNull(pressedKeys);

        // Summing lets two cardinal keys combine into a diagonal, and opposites cancel.
        Point total = Point.Zero;

        foreach (Keys key in pressedKeys)
        {
            // Keys with no movement meaning contribute nothing rather than being an error.
            if (OffsetByKey.TryGetValue(key, out Point offset))
            {
                total += offset;
            }
        }

        return total;
    }
}

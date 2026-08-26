/*
 * Aiming: where the cursor is, what is being aimed, and where cancelling goes back to.
 *
 * The last part gave the game modes. This is the first one that has to remember where it came
 * from: reading a scroll opens targeting from the pack, and cancelling must return to the pack
 * rather than to the map. A mode that forgets that leaves the player looking at the dungeon with
 * a scroll they thought they had put back.
 *
 * Usage:
 *
 *     Targeting aim = new Targeting(scroll, slot: 2, start: player.Position, radius: 3);
 *
 *     aim.MoveCursor(new Point(1, 0), map);   // one cell right, refused at the map edge
 *     Point at = aim.Cursor;
 *     bool splash = aim.IsAreaEffect;          // true when the scroll hits more than one cell
 *
 * Refuses a null scroll, a scroll with no Consumable, a negative slot, and a radius below zero.
 * A radius of zero is a single-target scroll, which is not the same as no scroll at all.
 */

using System;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class Targeting
{
    /// <summary>The scroll being aimed. It stays in the pack until the shot is confirmed.</summary>
    public Entity Scroll { get; }

    /// <summary>Which pack slot it came from, so cancelling can put the player back there.</summary>
    public int Slot { get; }

    /// <summary>How far the effect spreads from the chosen cell. Zero hits one cell.</summary>
    public int Radius { get; }

    /// <summary>Where the player is currently aiming.</summary>
    public Point Cursor { get; private set; }

    /// <summary>True when the scroll hits more than the cell it lands on.</summary>
    public bool IsAreaEffect => Radius > 0;

    /// <summary>
    /// Begins aiming a scroll. Throws ArgumentNullException on a null scroll, ArgumentException
    /// when it has no Consumable - only an item can be aimed - and ArgumentOutOfRangeException on
    /// a negative slot or radius.
    /// </summary>
    public Targeting(Entity scroll, int slot, Point start, int radius)
    {
        ArgumentNullException.ThrowIfNull(scroll);

        // Aiming something that cannot be used would leave the player stuck in a mode with no
        // way to resolve it.
        if (scroll.Consumable is null)
        {
            throw new ArgumentException($"{scroll.Name} is not an item and cannot be aimed.", nameof(scroll));
        }

        if (slot < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "A pack slot cannot be negative.");
        }

        if (radius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "A blast radius cannot be negative.");
        }

        Scroll = scroll;
        Slot = slot;
        Radius = radius;
        Cursor = start;
    }

    /// <summary>
    /// Moves the cursor by one step, refusing anything that would leave the map. The cursor may
    /// rest on a wall or on darkness: aiming at what you cannot see is a mistake the player is
    /// allowed to make, and the scroll simply finds nothing there. Throws ArgumentNullException
    /// on a null map.
    /// </summary>
    public void MoveCursor(Point offset, GameMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        Point destination = Cursor + offset;

        // Off the map is the one refusal. Everything else is the player's business.
        if (!map.IsInBounds(destination))
        {
            return;
        }

        Cursor = destination;
    }
}

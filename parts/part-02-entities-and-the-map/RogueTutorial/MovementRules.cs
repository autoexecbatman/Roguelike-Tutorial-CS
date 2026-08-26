/*
 * Where a move actually ends up, given the map.
 *
 * This replaces Part 1's clamping. With walls in play, a blocked move must mean staying put
 * rather than sliding to the nearest legal cell - a wall you walk into is not a suggestion to
 * step sideways.
 *
 * Usage:
 *
 *     GameMap map = new GameMap(10, 10);
 *     map.SetTile(new Point(5, 4), TileTypes.Wall);
 *
 *     Point moved = MovementRules.DestinationFor(new Point(4, 4), new Point(0, 1), map);
 *     // -> (4, 5), an ordinary step onto floor
 *
 *     Point blocked = MovementRules.DestinationFor(new Point(4, 4), new Point(1, 0), map);
 *     // -> (4, 4), unchanged, because (5, 4) is a wall
 *
 * Refuses a null map. A zero offset returns the starting position untouched.
 */

using System;
using SadRogue.Primitives;

namespace RogueTutorial;

internal static class MovementRules
{
    /// <summary>
    /// Returns the cell a move ends on. A destination that is a wall, or off the map, yields the
    /// starting position: the move is refused rather than adjusted. Throws ArgumentNullException
    /// on a null map.
    /// </summary>
    public static Point DestinationFor(Point start, Point offset, GameMap map)
    {
        // A null map is a wiring error rather than a blocked move.
        ArgumentNullException.ThrowIfNull(map);

        Point destination = start + offset;

        // IsWalkable answers false off the map too, so one question covers walls and edges.
        if (!map.IsWalkable(destination))
        {
            return start;
        }

        return destination;
    }
}

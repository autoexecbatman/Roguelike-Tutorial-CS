/*
 * Which cells the player can see from where they stand.
 *
 * A cell is visible when a straight line between it and the origin passes through nothing
 * solid. The line is checked in both directions and either one clear is enough, which makes
 * visibility symmetric by construction: if you can see a cell, a viewer standing there can see
 * you, because it is the same pair of lines either way round.
 *
 * Symmetry is not a nicety. Part 5 puts monsters on the map, and a monster that can see you
 * from a cell you cannot see is a bug a player experiences as unfair. Shadowcasting is faster
 * and is what a large map would want, but its symmetry depends on getting the slope arithmetic
 * exactly right; this gets symmetry from the definition and is small enough to check by eye.
 *
 * Usage:
 *
 *     GameMap map = dungeon.Map;
 *     ISet<Point> lit = FieldOfView.From(player.Position, radius: 8, map);
 *
 *     bool canSee = lit.Contains(new Point(12, 7));   // -> true if nothing blocks the line
 *     int howMany = lit.Count;                         // the origin is always included
 *
 * Refuses a null map and a negative radius. A radius of zero lights only the origin. Cells off
 * the map are never returned.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using SadRogue.Primitives;

namespace RogueTutorial;

internal static class FieldOfView
{
    /// <summary>
    /// Returns every cell visible from the origin within the radius, the origin included.
    /// Visibility is symmetric: the result contains a cell exactly when a viewer on that cell
    /// would see the origin. Throws ArgumentNullException on a null map and
    /// ArgumentOutOfRangeException on a negative radius.
    /// </summary>
    public static ISet<Point> From(Point origin, int radius, GameMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        // A negative radius is a caller error; zero is the legitimate "see only yourself" case.
        if (radius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "A sight radius cannot be negative.");
        }

        // You always see the cell you occupy, even standing in a doorway or inside rubble.
        HashSet<Point> visible = new HashSet<Point> { origin };

        // Only the square around the origin can hold anything within the radius, and the
        // round-distance test below trims that square to a circle.
        for (int row = origin.Y - radius; row <= origin.Y + radius; row++)
        {
            for (int col = origin.X - radius; col <= origin.X + radius; col++)
            {
                Point candidate = new Point(col, row);

                // A cell off the map is not somewhere the player can see; skipping it here is
                // what keeps the returned set safe to index the map with.
                if (!map.IsInBounds(candidate))
                {
                    continue;
                }

                // Round rather than square, so sight reaches equally far in every direction
                // instead of further along the diagonals.
                if (DistanceSquared(origin, candidate) > radius * radius)
                {
                    continue;
                }

                if (HasClearLine(origin, candidate, map))
                {
                    visible.Add(candidate);
                }
            }
        }

        // Walls are lit separately, after the floor is settled - see the method for why.
        LightWallsTouchingVisibleFloor(visible, radius, origin, map);

        // Symmetry rests on the origin being in its own set; a viewer must see themselves.
        Debug.Assert(visible.Contains(origin), "The origin must always be visible to itself.");

        return visible;
    }

    /// <summary>
    /// Adds every wall cell that touches a visible floor cell, so a room's outline is drawn
    /// whole rather than with gaps at its corners.
    ///
    /// A room corner has no clear line to the middle of the room - both diagonals clip a wall -
    /// so pure line-of-sight leaves it dark and the room renders with holes in it. Lighting
    /// walls by adjacency instead is cosmetic and cannot affect gameplay, because creatures
    /// stand on floor: the floor-to-floor visibility that Part 5 relies on stays symmetric.
    /// </summary>
    private static void LightWallsTouchingVisibleFloor(
        HashSet<Point> visible, int radius, Point origin, GameMap map)
    {
        // Snapshot first: adding to the set while walking it would let a newly lit wall light
        // its own neighbours, and sight would creep along a wall indefinitely.
        List<Point> visibleFloor = new List<Point>();

        foreach (Point cell in visible)
        {
            if (map.IsTransparent(cell))
            {
                visibleFloor.Add(cell);
            }
        }

        foreach (Point floorCell in visibleFloor)
        {
            // All eight neighbours, so diagonal corners are covered too.
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    Point neighbour = new Point(floorCell.X + offsetX, floorCell.Y + offsetY);

                    // Only walls, only on the map, and only inside the sight radius.
                    if (map.IsInBounds(neighbour)
                        && !map.IsTransparent(neighbour)
                        && DistanceSquared(origin, neighbour) <= radius * radius)
                    {
                        visible.Add(neighbour);
                    }
                }
            }
        }
    }

    /// <summary>
    /// True when sight passes between the two cells. Both directions are tried and either being
    /// clear is enough, which is what makes the relation symmetric: swapping the arguments swaps
    /// the two lines and leaves the answer unchanged.
    /// </summary>
    private static bool HasClearLine(Point from, Point to, GameMap map)
    {
        return IsUnobstructed(from, to, map) || IsUnobstructed(to, from, map);
    }

    /// <summary>
    /// Walks a Bresenham line from one cell to the other and reports whether every cell strictly
    /// between them lets sight through. The endpoints are not tested: you can see a wall, and
    /// standing in one does not blind you.
    /// </summary>
    private static bool IsUnobstructed(Point from, Point to, GameMap map)
    {
        int deltaX = Math.Abs(to.X - from.X);
        int deltaY = Math.Abs(to.Y - from.Y);

        // Step is +1 or -1 per axis, so one loop covers all eight directions.
        int stepX = from.X < to.X ? 1 : -1;
        int stepY = from.Y < to.Y ? 1 : -1;

        // Bresenham's running error term, doubled so it stays in integers.
        int error = deltaX - deltaY;

        Point cell = from;

        while (cell != to)
        {
            int doubledError = error * 2;

            if (doubledError > -deltaY)
            {
                error -= deltaY;
                cell = new Point(cell.X + stepX, cell.Y);
            }
            else
            {
                error += deltaX;
                cell = new Point(cell.X, cell.Y + stepY);
            }

            // Arriving at the destination means nothing in between blocked the way.
            if (cell == to)
            {
                return true;
            }

            // Anything solid strictly between the endpoints stops the line.
            if (!map.IsTransparent(cell))
            {
                return false;
            }
        }

        // Reached only when the endpoints are the same cell.
        return true;
    }

    // Squared distance, so the radius test needs no square root.
    private static int DistanceSquared(Point from, Point to)
    {
        int deltaX = to.X - from.X;
        int deltaY = to.Y - from.Y;

        return (deltaX * deltaX) + (deltaY * deltaY);
    }
}

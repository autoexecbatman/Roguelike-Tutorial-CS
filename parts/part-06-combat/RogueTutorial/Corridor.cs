/*
 * The L-shaped path between two room centres.
 *
 * A corridor runs along one axis and then the other, turning once. Which axis comes first is
 * the caller's choice, and the generator makes it at random so a dungeon does not have every
 * corner bending the same way.
 *
 * Usage:
 *
 *     // horizontal leg first: across to x=5, then down to y=3
 *     IEnumerable<Point> path = Corridor.Between(new Point(1, 1), new Point(5, 3), true);
 *     // -> (1,1) (2,1) (3,1) (4,1) (5,1) (5,2) (5,3)
 *
 *     // vertical leg first: down to y=3, then across to x=5
 *     IEnumerable<Point> other = Corridor.Between(new Point(1, 1), new Point(5, 3), false);
 *     // -> (1,1) (1,2) (1,3) (2,3) (3,3) (4,3) (5,3)
 *
 * Both endpoints are included. Two identical endpoints yield that single cell. The path never
 * repeats a cell, so carving it is one pass with no duplicated work.
 */

using System.Collections.Generic;
using SadRogue.Primitives;

namespace RogueTutorial;

internal static class Corridor
{
    /// <summary>
    /// Returns every cell of the L-shaped path from start to end, both endpoints included.
    /// When horizontalFirst is true the path moves along x before y, otherwise y before x.
    /// The corner cell is yielded once, not twice.
    /// </summary>
    public static IEnumerable<Point> Between(Point start, Point end, bool horizontalFirst)
    {
        // The bend: the cell where the path stops going one way and starts going the other.
        Point corner = horizontalFirst
            ? new Point(end.X, start.Y)
            : new Point(start.X, end.Y);

        // First leg includes the corner; second leg skips it so it is not yielded twice.
        foreach (Point cell in StraightLine(start, corner, includeEnd: true))
        {
            yield return cell;
        }

        foreach (Point cell in StraightLine(corner, end, includeEnd: true, skipFirst: true))
        {
            yield return cell;
        }
    }

    // Walks a horizontal or vertical run of cells. One of the two axes must already match.
    private static IEnumerable<Point> StraightLine(Point from, Point to, bool includeEnd, bool skipFirst = false)
    {
        // Step is -1, 0 or +1 on each axis, so one loop covers both directions.
        int stepX = System.Math.Sign(to.X - from.X);
        int stepY = System.Math.Sign(to.Y - from.Y);

        int length = System.Math.Max(System.Math.Abs(to.X - from.X), System.Math.Abs(to.Y - from.Y));

        // Starting at 1 rather than 0 is what drops the shared corner on the second leg.
        int firstStep = skipFirst ? 1 : 0;

        for (int step = firstStep; step <= length; step++)
        {
            if (step == length && !includeEnd)
            {
                yield break;
            }

            yield return new Point(from.X + (stepX * step), from.Y + (stepY * step));
        }
    }
}

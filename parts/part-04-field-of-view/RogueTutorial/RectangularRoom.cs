/*
 * One rectangular room, before it is carved into a map: where it sits, how big it is, and
 * whether it collides with another.
 *
 * The rectangle includes its own wall. A room at (0,0) that is 5 wide occupies columns 0 to 4,
 * and only columns 1 to 3 are floor - the outermost ring is the wall the player sees.
 *
 * Usage:
 *
 *     RectangularRoom room = new RectangularRoom(10, 5, 7, 6);   // left, top, width, height
 *     Point middle = room.Center;                                // -> (13, 8), where the player spawns
 *     bool clash = room.Intersects(otherRoom);                   // -> true if they overlap at all
 *     foreach (Point cell in room.InnerCells) { ... }             // the floor, wall excluded
 *
 * Refuses a width or height below three, since a smaller rectangle is all wall and encloses
 * nothing.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class RectangularRoom
{
    /// <summary>Column of the room's left wall.</summary>
    public int Left { get; }

    /// <summary>Row of the room's top wall.</summary>
    public int Top { get; }

    /// <summary>Total width including both walls.</summary>
    public int Width { get; }

    /// <summary>Total height including both walls.</summary>
    public int Height { get; }

    /// <summary>Column of the room's right wall.</summary>
    public int Right => Left + Width - 1;

    /// <summary>Row of the room's bottom wall.</summary>
    public int Bottom => Top + Height - 1;

    /// <summary>
    /// The middle of the room, rounded down. Corridors are dug between centres, and the player
    /// starts on the first room's centre, so this is always floor rather than wall.
    /// </summary>
    public Point Center => new Point(Left + (Width / 2), Top + (Height / 2));

    /// <summary>
    /// Every floor cell of the room: the rectangle with its outermost ring removed. Carving a
    /// room means setting exactly these to floor and leaving the ring as wall.
    /// </summary>
    public IEnumerable<Point> InnerCells
    {
        get
        {
            for (int row = Top + 1; row < Bottom; row++)
            {
                for (int col = Left + 1; col < Right; col++)
                {
                    yield return new Point(col, row);
                }
            }
        }
    }

    /// <summary>
    /// Records a room's position and size. Throws ArgumentOutOfRangeException below 3 in either
    /// dimension, because the wall ring would then consume the whole rectangle and the room
    /// would have no floor at all.
    /// </summary>
    public RectangularRoom(int left, int top, int width, int height)
    {
        // A room without an interior is a wall, and generating one is always a caller mistake.
        if (width < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "A room needs at least 3 cells across.");
        }
        if (height < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "A room needs at least 3 cells down.");
        }

        Left = left;
        Top = top;
        Width = width;
        Height = height;

        // Corridors are dug between centres and the player spawns on one, so a centre landing
        // on the wall ring would put both in solid rock.
        Debug.Assert(
            Center.X > Left && Center.X < Right && Center.Y > Top && Center.Y < Bottom,
            "A room's centre must lie inside its walls.");
    }

    /// <summary>
    /// True when this room shares any cell with the other, walls included. Rooms that merely
    /// touch along a wall count as intersecting: sharing a wall would let the player pass
    /// between them without a corridor.
    /// </summary>
    public bool Intersects(RectangularRoom other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // Standard rectangle overlap, inclusive on all four edges so shared walls count.
        return Left <= other.Right
            && Right >= other.Left
            && Top <= other.Bottom
            && Bottom >= other.Top;
    }
}

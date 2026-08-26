/*
 * The rectangle the player may stand on, and the rule for staying inside it.
 *
 * Usage - construct with the grid's size in cells, then clamp any candidate position:
 *
 *     GridBounds bounds = new GridBounds(80, 25);      // 80 cells wide, 25 tall
 *     Point inside = bounds.Clamp(new Point(-3, 40));  // -> (0, 24), pulled to the nearest edge
 *     bool ok = bounds.Contains(new Point(0, 24));     // -> true
 *
 * Refuses a width or height below one, because a grid with no cells has no legal position.
 */

using System;
using System.Diagnostics;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class GridBounds
{
    // Number of cells across, so the largest legal X is Width - 1.
    public int Width { get; }

    // Number of cells down, so the largest legal Y is Height - 1.
    public int Height { get; }

    /// <summary>
    /// Records the grid size in cells. Throws ArgumentOutOfRangeException when either
    /// dimension is below one, since an empty grid admits no legal position at all.
    /// </summary>
    public GridBounds(int width, int height)
    {
        // A zero or negative dimension is a caller error, not a runtime state to tolerate.
        if (width < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Grid width must be at least one cell.");
        }
        if (height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Grid height must be at least one cell.");
        }

        Width = width;
        Height = height;
    }

    /// <summary>
    /// True when the position is a cell of this grid, with (0,0) the top left corner.
    /// </summary>
    public bool Contains(Point position)
    {
        return position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;
    }

    /// <summary>
    /// Returns the nearest position inside the grid, moving each axis independently.
    /// A position already inside comes back unchanged.
    /// </summary>
    public Point Clamp(Point position)
    {
        // Each axis is clamped on its own, so a corner overshoot lands on the corner.
        int clampedX = Math.Clamp(position.X, 0, Width - 1);
        int clampedY = Math.Clamp(position.Y, 0, Height - 1);

        Point result = new Point(clampedX, clampedY);

        // The whole point of this method; if it can produce an outside position, the caller is lied to.
        Debug.Assert(Contains(result), "Clamp must return a position inside the grid.");

        return result;
    }
}

/*
 * What the player can see now, and what they remember seeing.
 *
 * Three states per cell, and the middle one is what makes a dungeon feel explored:
 *
 *     Unseen      never in sight - drawn as nothing at all
 *     Remembered  seen once, not now - drawn dim, from memory
 *     Visible     in sight this turn - drawn lit
 *
 * Remembering is one-way. A cell that has been seen never returns to Unseen, which is why the
 * map fills in behind you as you walk and stays filled in.
 *
 * Usage:
 *
 *     VisibilityMap visibility = new VisibilityMap(map.Width, map.Height);
 *
 *     visibility.Update(FieldOfView.From(player.Position, 8, map));
 *
 *     CellVisibility state = visibility.StateAt(new Point(4, 3));   // -> Visible
 *     bool draw = state != CellVisibility.Unseen;                    // is there anything to draw
 *
 * Refuses a dimension below one, a null cell set, and a query outside the map.
 */

using System;
using System.Collections.Generic;
using SadRogue.Primitives;

namespace RogueTutorial;

/// <summary>How much the player knows about one cell.</summary>
internal enum CellVisibility
{
    /// <summary>Never seen. Nothing is drawn here.</summary>
    Unseen,

    /// <summary>Seen before, not in sight now. Drawn dim, from memory.</summary>
    Remembered,

    /// <summary>In sight this turn. Drawn lit.</summary>
    Visible,
}

internal sealed class VisibilityMap
{
    // The rectangle of legal positions, reused from Part 1.
    private readonly GridBounds _bounds;

    // True once the cell has ever been seen. Never returns to false.
    private readonly bool[] _remembered;

    // True while the cell is in sight this turn. Replaced wholesale on every Update.
    private readonly bool[] _visible;

    /// <summary>Number of cells across.</summary>
    public int Width => _bounds.Width;

    /// <summary>Number of cells down.</summary>
    public int Height => _bounds.Height;

    /// <summary>
    /// Creates a visibility map of the given size with every cell unseen. Throws
    /// ArgumentOutOfRangeException when either dimension is below one.
    /// </summary>
    public VisibilityMap(int width, int height)
    {
        _bounds = new GridBounds(width, height);

        _remembered = new bool[width * height];
        _visible = new bool[width * height];
    }

    /// <summary>
    /// Replaces what is currently visible with the given cells, and adds all of them to what is
    /// remembered. Cells outside the map are ignored rather than rejected, so a field of view
    /// computed against a larger radius than the map can be passed straight in. Throws
    /// ArgumentNullException on a null set.
    /// </summary>
    public void Update(ISet<Point> visibleCells)
    {
        ArgumentNullException.ThrowIfNull(visibleCells);

        // Sight is recomputed from scratch each turn, so last turn's must be cleared first.
        // Memory is not cleared, which is the entire difference between the two arrays.
        Array.Clear(_visible);

        foreach (Point cell in visibleCells)
        {
            // A field of view may legitimately be asked for near an edge; ignore what falls off.
            if (!_bounds.Contains(cell))
            {
                continue;
            }

            int index = IndexOf(cell);

            _visible[index] = true;
            _remembered[index] = true;
        }
    }

    /// <summary>
    /// How much the player knows about the cell. Throws ArgumentOutOfRangeException off the map,
    /// because asking about a cell that does not exist is a caller error rather than a state.
    /// </summary>
    public CellVisibility StateAt(Point position)
    {
        if (!_bounds.Contains(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, "The position is outside the map.");
        }

        int index = IndexOf(position);

        // Visible outranks remembered: a cell in sight is drawn lit, not from memory.
        if (_visible[index])
        {
            return CellVisibility.Visible;
        }

        return _remembered[index] ? CellVisibility.Remembered : CellVisibility.Unseen;
    }

    // Row-major index; the single place this map's storage layout is expressed.
    private int IndexOf(Point position)
    {
        return (position.Y * Width) + position.X;
    }
}

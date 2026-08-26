/*
 * The player's position on the grid, and the only way it is allowed to change.
 *
 * Usage - construct with the grid and a starting cell, then apply one-cell offsets:
 *
 *     GridBounds bounds = new GridBounds(80, 25);
 *     PlayerMover mover = new PlayerMover(bounds, new Point(40, 12));  // start at the centre
 *     mover.Move(Direction.Left);                                      // -> Position is (39, 12)
 *     mover.Move(new Point(-100, 0));                                  // -> Position is (0, 12), clamped
 *
 * Refuses a starting position outside the grid; clamps every move rather than refusing it,
 * so holding a key against a wall stops instead of throwing.
 */

using System;
using System.Diagnostics;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class PlayerMover
{
    // The rectangle the player is confined to; fixed for this object's lifetime.
    private readonly GridBounds _bounds;

    /// <summary>
    /// Where the player stands now. Always a cell inside the bounds given at construction.
    /// </summary>
    public Point Position { get; private set; }

    /// <summary>
    /// Places the player at a starting cell. Throws ArgumentOutOfRangeException when that
    /// cell is outside the grid, because a silent clamp would hide a caller's bad arithmetic.
    /// </summary>
    public PlayerMover(GridBounds bounds, Point startingPosition)
    {
        // A null grid is a wiring error and must fail at the site of the fault.
        ArgumentNullException.ThrowIfNull(bounds);

        // Construction is the one place an outside position is rejected rather than clamped.
        if (!bounds.Contains(startingPosition))
        {
            throw new ArgumentOutOfRangeException(
                nameof(startingPosition),
                startingPosition,
                "The starting position must be a cell inside the grid.");
        }

        _bounds = bounds;
        Position = startingPosition;
    }

    /// <summary>
    /// Shifts the player by the offset, pulling the result back to the nearest legal cell
    /// when it would leave the grid. A zero offset leaves the position untouched.
    /// </summary>
    public void Move(Point offset)
    {
        // Clamping is what turns an illegal destination into a stop at the wall.
        Position = _bounds.Clamp(Position + offset);

        // The class's single invariant, restated where it could be broken.
        Debug.Assert(_bounds.Contains(Position), "The player must never stand outside the grid.");
    }
}

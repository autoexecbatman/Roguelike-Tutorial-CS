/*
 * What happened when the player pressed a movement key.
 *
 * The caller needs to tell three outcomes apart: the player moved and the screen must be
 * repainted, a wall refused the move and nothing changed, or the player walked into a creature -
 * which is the attack command, and Part 6 is where it starts doing damage.
 *
 * Usage:
 *
 *     PlayerAction action = world.MovePlayer(new Point(1, 0));
 *
 *     if (action.Kind == PlayerActionKind.Moved) { redraw(); }
 *
 *     if (action.Kind == PlayerActionKind.Bumped)
 *     {
 *         string message = $"You attack the {action.Target!.Name}.";   // Target is set only here
 *     }
 *
 * Target is null for every kind except Bumped, which is the one case where something was hit.
 */

using System;

namespace RogueTutorial;

/// <summary>The three outcomes of a movement key.</summary>
internal enum PlayerActionKind
{
    /// <summary>No movement key was pressed, or the offset was zero.</summary>
    None,

    /// <summary>The player moved, and the screen needs repainting.</summary>
    Moved,

    /// <summary>A wall refused the move. Nothing changed.</summary>
    BlockedByWall,

    /// <summary>The player walked into a creature. Part 6 makes this an attack.</summary>
    Bumped,
}

internal readonly struct PlayerAction
{
    /// <summary>Which of the three outcomes this was.</summary>
    public PlayerActionKind Kind { get; }

    /// <summary>What was bumped into, or null for every other kind.</summary>
    public Entity? Target { get; }

    private PlayerAction(PlayerActionKind kind, Entity? target)
    {
        Kind = kind;
        Target = target;
    }

    /// <summary>No movement key was pressed.</summary>
    public static PlayerAction None => new PlayerAction(PlayerActionKind.None, null);

    /// <summary>The player moved to a new cell.</summary>
    public static PlayerAction Moved => new PlayerAction(PlayerActionKind.Moved, null);

    /// <summary>A wall refused the move.</summary>
    public static PlayerAction BlockedByWall => new PlayerAction(PlayerActionKind.BlockedByWall, null);

    /// <summary>
    /// The player walked into a creature. Throws ArgumentNullException on a null target, since a
    /// bump with nothing to bump into is a bug in whoever built it.
    /// </summary>
    public static PlayerAction BumpedInto(Entity target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return new PlayerAction(PlayerActionKind.Bumped, target);
    }
}

/*
 * What a monster does with its turn.
 *
 * The rule is short: if it cannot see the player it does nothing, if it is next to the player it
 * attacks, otherwise it takes one step toward them.
 *
 * "Can see the player" is the player's own field of view read backwards, which is only sound
 * because Part 4's visibility is symmetric. A monster that could see you from a cell you cannot
 * see into would shoot from the dark, and a player experiences that as the game cheating.
 *
 * Usage:
 *
 *     string? message = MonsterTurn.Act(rat, world);
 *
 *     // message is null when the monster did nothing worth reporting - it could not see the
 *     // player, or it simply stepped closer. Only an attack produces a line.
 *
 * Refuses a null monster or world, and a monster with no Fighter: a corpse does not take turns.
 */

using System;
using SadRogue.Primitives;

namespace RogueTutorial;

internal static class MonsterTurn
{
    /// <summary>
    /// Runs one monster's turn and returns the line to log, or null when nothing worth reporting
    /// happened. Throws ArgumentNullException on a null argument and ArgumentException on a
    /// monster with no Fighter, since a corpse has no turn to take.
    /// </summary>
    public static string? Act(Entity monster, GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(monster);
        ArgumentNullException.ThrowIfNull(world);

        // A corpse in the turn order means the caller is not filtering the dead out.
        if (monster.Fighter is null)
        {
            throw new ArgumentException($"{monster.Name} has no Fighter and cannot take a turn.", nameof(monster));
        }

        // Symmetric visibility is what makes reading the player's own field of view sound here.
        if (world.Visibility.StateAt(monster.Position) != CellVisibility.Visible)
        {
            return null;
        }

        // Chebyshev distance, because movement is eight-way: a diagonal neighbour is adjacent.
        int distance = Math.Max(
            Math.Abs(monster.Position.X - world.Player.Position.X),
            Math.Abs(monster.Position.Y - world.Player.Position.Y));

        if (distance <= 1)
        {
            // The player may already be dead this turn, killed by a monster acting earlier.
            if (world.Player.Fighter is null)
            {
                return null;
            }

            return Combat.Resolve(monster, world.Player).Message;
        }

        StepToward(monster, world);

        // Walking closer is not worth a line; the player can see it happen.
        return null;
    }

    // Moves one cell toward the player, if the cell in that direction is free.
    private static void StepToward(Entity monster, GameWorld world)
    {
        // One step per axis, so the move is a straight line or a diagonal.
        Point step = new Point(
            Math.Sign(world.Player.Position.X - monster.Position.X),
            Math.Sign(world.Player.Position.Y - monster.Position.Y));

        Point destination = monster.Position + step;

        // Walls stop a monster exactly as they stop the player.
        if (!world.Map.IsWalkable(destination))
        {
            return;
        }

        // Another monster in the way blocks the step. There is no pathfinding yet, so a monster
        // behind another simply waits - which is what makes this the naive version.
        if (world.BlockingEntityAt(destination) is not null)
        {
            return;
        }

        monster.MoveTo(destination);
    }
}

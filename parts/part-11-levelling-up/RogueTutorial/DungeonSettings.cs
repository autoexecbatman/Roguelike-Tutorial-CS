/*
 * The numbers that shape a generated dungeon, gathered in one place.
 *
 * These live here rather than as literals inside the generator so that a run can be described
 * by its settings and its seed, and so that changing "how big are the rooms" is one edit in an
 * obvious place rather than a hunt through generation code.
 *
 * Usage:
 *
 *     DungeonSettings settings = new DungeonSettings(
 *         maximumRooms: 30,       // attempts, not a guarantee - see below
 *         minimumRoomSize: 6,     // total width or height, walls included
 *         maximumRoomSize: 10);
 *
 * maximumRooms is a number of attempts. A room that would overlap an existing one is discarded
 * rather than retried, so a dungeon usually holds fewer rooms than this, and that is by design:
 * retrying until the count is met makes generation take unbounded time on a crowded map.
 *
 * Refuses a room count below one, a minimum size below three, and a maximum below the minimum.
 */

using System;

namespace RogueTutorial;

internal sealed class DungeonSettings
{
    /// <summary>How many rooms to attempt. Fewer may be placed; overlaps are discarded.</summary>
    public int MaximumRooms { get; }

    /// <summary>Smallest total width or height a room may have, its walls included.</summary>
    public int MinimumRoomSize { get; }

    /// <summary>Largest total width or height a room may have, its walls included.</summary>
    public int MaximumRoomSize { get; }

    /// <summary>
    /// Records the generation parameters. Throws ArgumentOutOfRangeException when a room count
    /// is below one, when the minimum size is below three - the size at which a room has no
    /// floor inside its walls - or when the maximum is smaller than the minimum.
    /// </summary>
    public DungeonSettings(int maximumRooms, int minimumRoomSize, int maximumRoomSize)
    {
        // A dungeon with no rooms has nowhere to put the player.
        if (maximumRooms < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRooms), maximumRooms, "A dungeon needs at least one room.");
        }

        // Below 3 the wall ring consumes the whole rectangle; RectangularRoom rejects it too.
        if (minimumRoomSize < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumRoomSize), minimumRoomSize, "A room needs at least 3 cells across and down.");
        }

        // An inverted range would make the random size call throw much later and less clearly.
        if (maximumRoomSize < minimumRoomSize)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRoomSize), maximumRoomSize, "The maximum room size cannot be below the minimum.");
        }

        MaximumRooms = maximumRooms;
        MinimumRoomSize = minimumRoomSize;
        MaximumRoomSize = maximumRoomSize;
    }
}

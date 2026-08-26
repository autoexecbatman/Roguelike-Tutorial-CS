/*
 * What a generation run produced: the carved map, the rooms it placed, and where the player
 * starts.
 *
 * The rooms are kept rather than discarded because later parts need them - monsters are placed
 * per room in Part 5, and the stairs down go in the last room in Part 12. Recovering them from
 * the finished map afterwards would mean detecting rectangles in a bitmap.
 *
 * Usage:
 *
 *     GeneratedDungeon dungeon = generator.Generate(80, 43, new Random(12345));
 *     GameMap map = dungeon.Map;                  // hand this to FrameComposer
 *     Point spawn = dungeon.PlayerStart;          // centre of the first room
 *     int placed = dungeon.Rooms.Count;           // how many rooms survived overlap rejection
 *
 * Refuses a null map and an empty room list: a dungeon with no rooms has nowhere to put the
 * player, and constructing one is a generator bug rather than a legal outcome.
 */

using System;
using System.Collections.Generic;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class GeneratedDungeon
{
    /// <summary>The carved map: rock, with rooms and corridors cut into it.</summary>
    public GameMap Map { get; }

    /// <summary>Every room placed, in the order they were generated.</summary>
    public IReadOnlyList<RectangularRoom> Rooms { get; }

    /// <summary>Where the player begins: the centre of the first room.</summary>
    public Point PlayerStart => Rooms[0].Center;

    /// <summary>
    /// Wraps the result of one generation run. Throws ArgumentNullException on a null argument
    /// and ArgumentException on an empty room list, because PlayerStart would then have no
    /// answer and the failure would surface far from its cause.
    /// </summary>
    public GeneratedDungeon(GameMap map, IReadOnlyList<RectangularRoom> rooms)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(rooms);

        // A generator that placed nothing has failed; saying so here beats an index error later.
        if (rooms.Count == 0)
        {
            throw new ArgumentException("A dungeon must contain at least one room.", nameof(rooms));
        }

        Map = map;
        Rooms = rooms;
    }
}

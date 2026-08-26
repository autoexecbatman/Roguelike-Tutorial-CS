/*
 * What lives in the dungeon, and how many of them turn up in a room.
 *
 * The kinds and their weights are data rather than code, so adding a monster is a line in a list
 * and adjusting how common one is does not mean touching placement logic. Part 12 makes the
 * weights vary with depth; this holds one table for the whole dungeon.
 *
 * Usage:
 *
 *     MonsterTable table = MonsterTable.Standard;
 *     IReadOnlyList<Entity> placed = table.PopulateRoom(room, map, new Random(12345));
 *
 *     // or a table of your own, for a test that wants exactly one kind of monster:
 *     MonsterTable rats = new MonsterTable(
 *         new[] { new MonsterKind("Rat", 'r', Color.Brown, weight: 1,
 *                     maximumHitPoints: 4, attack: 3, defence: 0) },
 *         maximumPerRoom: 2);
 *
 * Placement never stacks two creatures on one cell and never uses a cell a wall occupies, so a
 * room can end up with fewer monsters than the maximum. Refuses an empty kind list, a maximum
 * below zero, and any null argument.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using SadRogue.Primitives;

namespace RogueTutorial;

/// <summary>One kind of monster: how it looks, and how often it turns up.</summary>
internal sealed class MonsterKind
{
    /// <summary>What it is called, for messages.</summary>
    public string Name { get; }

    /// <summary>The character drawn for it.</summary>
    public char Glyph { get; }

    /// <summary>The colour that character is drawn in.</summary>
    public Color Foreground { get; }

    /// <summary>Hit points this kind starts with.</summary>
    public int MaximumHitPoints { get; }

    /// <summary>How hard this kind hits.</summary>
    public int Attack { get; }

    /// <summary>How much damage this kind shrugs off per blow.</summary>
    public int Defence { get; }

    /// <summary>
    /// How likely this kind is relative to the others in its table. A kind with weight 3 turns up
    /// three times as often as one with weight 1; the numbers have no meaning on their own.
    /// </summary>
    public int Weight { get; }

    /// <summary>
    /// Records one monster kind. Throws ArgumentException on a blank name and
    /// ArgumentOutOfRangeException on a weight below one, since a kind that can never be chosen
    /// is a table entry somebody meant to delete.
    /// </summary>
    public MonsterKind(string name, char glyph, Color foreground, int weight, int maximumHitPoints, int attack, int defence)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A monster kind needs a name.", nameof(name));
        }

        if (weight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "A weight below one can never be chosen.");
        }

        Name = name;
        Glyph = glyph;
        Foreground = foreground;
        Weight = weight;
        MaximumHitPoints = maximumHitPoints;
        Attack = attack;
        Defence = defence;

        // Constructing a Fighter here would throw on bad numbers far from this call site, so
        // the same rules are enforced where the kind is declared instead.
        _ = new Fighter(maximumHitPoints, attack, defence);
    }
}

internal sealed class MonsterTable
{
    // The kinds that may be placed, with their relative weights.
    private readonly IReadOnlyList<MonsterKind> _kinds;

    // The sum of every weight, computed once because it is needed on every roll.
    private readonly int _totalWeight;

    /// <summary>The most monsters that may be placed in one room.</summary>
    public int MaximumPerRoom { get; }

    /// <summary>
    /// Records the kinds available and how crowded a room may get. Throws ArgumentNullException
    /// on a null list, ArgumentException on an empty one, and ArgumentOutOfRangeException when
    /// the maximum is negative. A maximum of zero is legal and means an empty dungeon.
    /// </summary>
    public MonsterTable(IReadOnlyList<MonsterKind> kinds, int maximumPerRoom)
    {
        ArgumentNullException.ThrowIfNull(kinds);

        // A table with nothing in it cannot answer "which kind", so reject it here rather than
        // failing on the first roll.
        if (kinds.Count == 0)
        {
            throw new ArgumentException("A monster table needs at least one kind.", nameof(kinds));
        }

        if (maximumPerRoom < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPerRoom), maximumPerRoom, "A room cannot hold a negative number of monsters.");
        }

        _kinds = kinds;
        _totalWeight = kinds.Sum(kind => kind.Weight);
        MaximumPerRoom = maximumPerRoom;
    }

    /// <summary>
    /// The table the game uses: rats are common, kobolds less so. Weights are relative, so a rat
    /// turns up three times as often as a kobold.
    /// </summary>
    public static MonsterTable Standard => new MonsterTable(
        new[]
        {
            new MonsterKind("Rat", 'r', new Color(150, 120, 90), weight: 3,
                maximumHitPoints: 4, attack: 3, defence: 0),
            new MonsterKind("Kobold", 'k', new Color(120, 180, 90), weight: 1,
                maximumHitPoints: 8, attack: 4, defence: 1),
        },
        maximumPerRoom: 2);

    /// <summary>
    /// Rolls a number of monsters for the room and places them on distinct walkable cells inside
    /// its walls. Returns fewer than the maximum when the room is small or a roll repeats a cell,
    /// which is preferred to retrying: a generator that sometimes takes a long time is worse than
    /// one that sometimes places a monster fewer. Throws ArgumentNullException on a null argument.
    /// </summary>
    public IReadOnlyList<Entity> PopulateRoom(RectangularRoom room, GameMap map, Random random)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(random);

        // Next's upper bound is exclusive, so + 1 makes MaximumPerRoom reachable.
        int wanted = random.Next(0, MaximumPerRoom + 1);

        List<Entity> placed = new List<Entity>();
        HashSet<Point> taken = new HashSet<Point>();

        for (int monster = 0; monster < wanted; monster++)
        {
            Point cell = new Point(
                random.Next(room.Left + 1, room.Right),
                random.Next(room.Top + 1, room.Bottom));

            // A repeated cell is dropped rather than rerolled, so this loop always terminates.
            if (!taken.Add(cell))
            {
                continue;
            }

            // A pillar or a corridor wall carved through the room leaves cells nothing can
            // stand on, and the room's interior is not guaranteed to be solid floor.
            if (!map.IsWalkable(cell))
            {
                continue;
            }

            MonsterKind kind = ChooseKind(random);

            Entity placedMonster = new Entity(kind.Name, kind.Glyph, kind.Foreground, cell, blocksMovement: true);

            // The component is what lets it fight; without one it would be scenery.
            placedMonster.Fighter = new Fighter(kind.MaximumHitPoints, kind.Attack, kind.Defence);

            placed.Add(placedMonster);
        }

        return placed;
    }

    // Picks a kind at random, each in proportion to its weight.
    private MonsterKind ChooseKind(Random random)
    {
        // A number in [0, totalWeight) lands in exactly one kind's share of the range.
        int roll = random.Next(_totalWeight);

        foreach (MonsterKind kind in _kinds)
        {
            if (roll < kind.Weight)
            {
                return kind;
            }

            roll -= kind.Weight;
        }

        // Unreachable: the roll is below the total, so some kind's share must contain it.
        throw new InvalidOperationException("The weighted roll fell outside every kind's share.");
    }
}

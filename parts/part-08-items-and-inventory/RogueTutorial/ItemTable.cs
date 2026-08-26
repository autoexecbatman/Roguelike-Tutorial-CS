/*
 * What items lie in the dungeon, and how many turn up in a room.
 *
 * The same shape as MonsterTable, and for the same reasons: the kinds are data, the weights are
 * relative, and every random choice is drawn from a Random the caller supplies so one seed
 * reproduces a whole dungeon - monsters, items and all.
 *
 * Usage:
 *
 *     ItemTable table = ItemTable.Standard;
 *     IReadOnlyList<Entity> dropped = table.PopulateRoom(room, map, new Random(12345));
 *
 * Placement never uses a cell a wall occupies. Two items may share a cell - unlike creatures,
 * things on the floor do not block - so only the top one is drawn and only the top one is picked
 * up, which is a limitation this part keeps. Refuses an empty kind list and any null argument.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using SadRogue.Primitives;

namespace RogueTutorial;

/// <summary>One kind of item: how it looks, what it does, and how often it turns up.</summary>
internal sealed class ItemKind
{
    /// <summary>What it is called, for the log and the pack.</summary>
    public string Name { get; }

    /// <summary>The character drawn for it.</summary>
    public char Glyph { get; }

    /// <summary>The colour that character is drawn in.</summary>
    public Color Foreground { get; }

    /// <summary>How likely this kind is relative to the others in its table.</summary>
    public int Weight { get; }

    /// <summary>What it does when used.</summary>
    public ConsumableKind Effect { get; }

    /// <summary>How much it does it by.</summary>
    public int Power { get; }

    /// <summary>
    /// Records one item kind. Throws ArgumentException on a blank name and
    /// ArgumentOutOfRangeException on a weight below one, which could never be chosen.
    /// </summary>
    public ItemKind(string name, char glyph, Color foreground, int weight, ConsumableKind effect, int power)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("An item kind needs a name.", nameof(name));
        }

        if (weight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "A weight below one can never be chosen.");
        }

        Name = name;
        Glyph = glyph;
        Foreground = foreground;
        Weight = weight;
        Effect = effect;
        Power = power;

        // Constructing the component here would throw far from this call site, so the same rule
        // is enforced where the kind is declared.
        _ = new Consumable(effect, power);
    }
}

internal sealed class ItemTable
{
    // The kinds that may be placed, with their relative weights.
    private readonly IReadOnlyList<ItemKind> _kinds;

    // The sum of every weight, computed once because it is needed on every roll.
    private readonly int _totalWeight;

    /// <summary>The most items that may be placed in one room.</summary>
    public int MaximumPerRoom { get; }

    /// <summary>
    /// Records the kinds available and how many may litter a room. Throws ArgumentNullException
    /// on a null list, ArgumentException on an empty one, and ArgumentOutOfRangeException when
    /// the maximum is negative. Zero is legal and means a dungeon with nothing to find.
    /// </summary>
    public ItemTable(IReadOnlyList<ItemKind> kinds, int maximumPerRoom)
    {
        ArgumentNullException.ThrowIfNull(kinds);

        if (kinds.Count == 0)
        {
            throw new ArgumentException("An item table needs at least one kind.", nameof(kinds));
        }

        if (maximumPerRoom < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPerRoom), maximumPerRoom, "A room cannot hold a negative number of items.");
        }

        _kinds = kinds;
        _totalWeight = kinds.Sum(kind => kind.Weight);
        MaximumPerRoom = maximumPerRoom;
    }

    /// <summary>
    /// The table the game uses. One kind so far; Part 9 adds the scrolls that need a target.
    /// </summary>
    public static ItemTable Standard => new ItemTable(
        new[]
        {
            new ItemKind("healing potion", '!', new Color(200, 80, 200), weight: 1, ConsumableKind.Healing, power: 8),
        },
        maximumPerRoom: 2);

    /// <summary>
    /// Rolls a number of items for the room and places them on walkable cells inside its walls.
    /// Returns fewer than the maximum when a roll lands on rock. Throws ArgumentNullException on
    /// a null argument.
    /// </summary>
    public IReadOnlyList<Entity> PopulateRoom(RectangularRoom room, GameMap map, Random random)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(random);

        int wanted = random.Next(0, MaximumPerRoom + 1);

        List<Entity> placed = new List<Entity>();

        for (int item = 0; item < wanted; item++)
        {
            Point cell = new Point(
                random.Next(room.Left + 1, room.Right),
                random.Next(room.Top + 1, room.Bottom));

            // A pillar, or a corridor carved through the room, leaves cells nothing can lie on.
            if (!map.IsWalkable(cell))
            {
                continue;
            }

            ItemKind kind = ChooseKind(random);

            // Items do not block: you walk over them, and picking up is a separate command.
            Entity dropped = new Entity(kind.Name, kind.Glyph, kind.Foreground, cell, blocksMovement: false);

            dropped.Consumable = new Consumable(kind.Effect, kind.Power);

            placed.Add(dropped);
        }

        return placed;
    }

    // Picks a kind at random, each in proportion to its weight.
    private ItemKind ChooseKind(Random random)
    {
        int roll = random.Next(_totalWeight);

        foreach (ItemKind kind in _kinds)
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

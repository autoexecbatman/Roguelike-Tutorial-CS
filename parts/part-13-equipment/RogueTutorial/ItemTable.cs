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

    /// <summary>What it does when used, ignored when this kind is equipment.</summary>
    public ConsumableKind Effect { get; }

    /// <summary>
    /// Where this is worn, or null when it is something to be used rather than worn. Exactly one
    /// of this and Effect means anything for a given kind.
    /// </summary>
    public EquipmentSlot? Slot { get; }

    /// <summary>What it adds to attack while worn. Zero for anything that is not equipment.</summary>
    public int AttackBonus { get; }

    /// <summary>What it adds to defence while worn.</summary>
    public int DefenceBonus { get; }

    /// <summary>How much it does it by.</summary>
    public int Power { get; }

    /// <summary>How far the effect spreads. Zero for everything that hits one cell.</summary>
    public int Radius { get; }

    /// <summary>The shallowest floor this kind is found on.</summary>
    public int MinimumDepth { get; }

    /// <summary>
    /// Records one item kind. Throws ArgumentException on a blank name and
    /// ArgumentOutOfRangeException on a weight below one, which could never be chosen.
    /// </summary>
    private ItemKind(
        string name, char glyph, Color foreground, int weight, ConsumableKind effect,
        int power, int radius, int minimumDepth, EquipmentSlot? slot, int attackBonus, int defenceBonus)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("An item kind needs a name.", nameof(name));
        }

        if (weight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "A weight below one can never be chosen.");
        }

        // A kind that first appears above floor one could never be placed at all.
        if (minimumDepth < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumDepth), minimumDepth, "The first floor is depth one.");
        }

        Name = name;
        Glyph = glyph;
        Foreground = foreground;
        Weight = weight;
        Effect = effect;
        Power = power;
        Radius = radius;
        MinimumDepth = minimumDepth;
        Slot = slot;
        AttackBonus = attackBonus;
        DefenceBonus = defenceBonus;

        // Constructing the component here would throw far from this call site, so the same rule
        // is enforced where the kind is declared.
        if (slot is null)
        {
            _ = new Consumable(effect, power, radius);
        }
        else
        {
            _ = new Equippable(slot.Value, attackBonus, defenceBonus);
        }
    }

    /// <summary>
    /// A kind that is drunk or read: it has an effect and no slot. Throws the same way the
    /// Consumable component does, at the declaration rather than at the dungeon.
    /// </summary>
    public static ItemKind Usable(
        string name, char glyph, Color foreground, int weight,
        ConsumableKind effect, int power, int radius, int minimumDepth)
    {
        return new ItemKind(
            name, glyph, foreground, weight, effect, power, radius, minimumDepth,
            slot: null, attackBonus: 0, defenceBonus: 0);
    }

    /// <summary>
    /// A kind that is worn or wielded: it has a slot and bonuses, and no effect. Throws the same
    /// way the Equippable component does.
    /// </summary>
    public static ItemKind Wearable(
        string name, char glyph, Color foreground, int weight,
        EquipmentSlot slot, int attackBonus, int defenceBonus, int minimumDepth)
    {
        return new ItemKind(
            name, glyph, foreground, weight, ConsumableKind.Healing, power: 0, radius: 0,
            minimumDepth, slot, attackBonus, defenceBonus);
    }
}

internal sealed class ItemTable
{
    // The kinds that may be placed, with their relative weights.
    private readonly IReadOnlyList<ItemKind> _kinds;

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
        MaximumPerRoom = maximumPerRoom;
    }

    /// <summary>
    /// The table the game uses. Potions are common because a scroll you cannot aim safely is
    /// worth less than health you can always drink. MinimumDepth keeps the greater potion out of
    /// the shallow floors, where it would make the early game trivial.
    /// </summary>
    public static ItemTable Standard => new ItemTable(
        new[]
        {
            ItemKind.Usable("healing potion", '!', new Color(200, 80, 200),
                weight: 4, ConsumableKind.Healing, power: 8, radius: 0, minimumDepth: 1),
            ItemKind.Usable("lightning scroll", '?', new Color(230, 230, 100),
                weight: 2, ConsumableKind.Lightning, power: 12, radius: 0, minimumDepth: 1),
            ItemKind.Usable("fireball scroll", '?', new Color(230, 130, 60),
                weight: 1, ConsumableKind.Fireball, power: 8, radius: 3, minimumDepth: 1),
            ItemKind.Usable("greater healing potion", '!', new Color(240, 120, 240),
                weight: 2, ConsumableKind.Healing, power: 20, radius: 0, minimumDepth: 4),
            ItemKind.Wearable("dagger", '/', new Color(170, 170, 190),
                weight: 2, EquipmentSlot.Weapon, attackBonus: 2, defenceBonus: 0, minimumDepth: 1),
            ItemKind.Wearable("leather armour", '[', new Color(160, 110, 70),
                weight: 2, EquipmentSlot.Armour, attackBonus: 0, defenceBonus: 1, minimumDepth: 1),
            ItemKind.Wearable("sword", '/', new Color(200, 200, 230),
                weight: 1, EquipmentSlot.Weapon, attackBonus: 4, defenceBonus: 0, minimumDepth: 4),
            ItemKind.Wearable("chain mail", '[', new Color(180, 180, 200),
                weight: 1, EquipmentSlot.Armour, attackBonus: 0, defenceBonus: 3, minimumDepth: 6),
        },
        maximumPerRoom: 2);

    /// <summary>
    /// Rolls a number of items for the room and places them on walkable cells inside its walls.
    /// Returns fewer than the maximum when a roll lands on rock. Throws ArgumentNullException on
    /// a null argument.
    /// </summary>
    public IReadOnlyList<Entity> PopulateRoom(RectangularRoom room, GameMap map, Random random, int depth)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(random);

        if (depth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), depth, "The first floor is depth one.");
        }

        // Which kinds this floor may hold. Deeper floors keep everything shallower as well.
        IReadOnlyList<ItemKind> available = AvailableAt(depth);

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

            ItemKind kind = ChooseKind(random, available);

            // Items do not block: you walk over them, and picking up is a separate command.
            Entity dropped = new Entity(
                kind.Name, kind.Glyph, kind.Foreground, cell, blocksMovement: false, RenderLayer.Item);

            // A kind is one thing or the other, which is what the two factories guarantee.
            if (kind.Slot is null)
            {
                dropped.Consumable = new Consumable(kind.Effect, kind.Power, kind.Radius);
            }
            else
            {
                dropped.Equippable = new Equippable(kind.Slot.Value, kind.AttackBonus, kind.DefenceBonus);
            }

            placed.Add(dropped);
        }

        return placed;
    }

    // Every kind shallow enough for this floor.
    private IReadOnlyList<ItemKind> AvailableAt(int depth)
    {
        return _kinds.Where(kind => kind.MinimumDepth <= depth).ToList();
    }

    // Picks a kind at random, each in proportion to its weight.
    private static ItemKind ChooseKind(Random random, IReadOnlyList<ItemKind> available)
    {
        int totalWeight = available.Sum(kind => kind.Weight);

        int roll = random.Next(totalWeight);

        foreach (ItemKind kind in available)
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

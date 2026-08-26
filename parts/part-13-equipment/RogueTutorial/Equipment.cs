/*
 * What one entity is currently wearing and wielding.
 *
 * A component, held by the player and by nothing else: monsters read their Fighter alone. It
 * holds references to items and nothing more - no totals, no cached numbers - so there is
 * nothing here that can fall out of step with what is actually equipped.
 *
 * Usage:
 *
 *     Equipment worn = new Equipment();
 *
 *     Entity? displaced = worn.Equip(sword);      // -> whatever was in that slot, or null
 *     int attack = worn.AttackBonus;              // -> the sum over both slots
 *     Entity? removed = worn.Unequip(EquipmentSlot.Weapon);
 *
 *     bool wielded = worn.IsEquipped(sword);      // -> false once it has been taken off
 *
 * The caller owns what comes back from Equip and Unequip: this class stops referring to it, and
 * dropping it on the floor rather than returning it to the pack would lose it silently.
 *
 * Refuses an item with no Equippable component.
 */

using System;
using System.Collections.Generic;

namespace RogueTutorial;

internal sealed class Equipment
{
    // One item per slot, absent when the slot is empty. A dictionary rather than two fields, so
    // adding a third slot is a line in the enum rather than a line in every method here.
    private readonly Dictionary<EquipmentSlot, Entity> _worn = new Dictionary<EquipmentSlot, Entity>();

    /// <summary>Everything currently equipped, in no particular order.</summary>
    public IReadOnlyCollection<Entity> Worn => _worn.Values;

    /// <summary>What the equipped items add to attack, summed over every slot.</summary>
    public int AttackBonus => SumOf(equippable => equippable.AttackBonus);

    /// <summary>What the equipped items add to defence, summed over every slot.</summary>
    public int DefenceBonus => SumOf(equippable => equippable.DefenceBonus);

    /// <summary>
    /// Puts an item in its slot and returns whatever was displaced, or null when the slot was
    /// empty. The caller is responsible for what comes back - usually putting it in the pack.
    /// Throws ArgumentNullException on null and ArgumentException on an item that is not
    /// equipment, which would otherwise sit in a slot contributing nothing.
    /// </summary>
    public Entity? Equip(Entity item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Equippable is null)
        {
            throw new ArgumentException($"{item.Name} is not equipment.", nameof(item));
        }

        EquipmentSlot slot = item.Equippable.Slot;

        // Read before the write, or the thing being replaced is lost.
        Entity? displaced = _worn.TryGetValue(slot, out Entity? worn) ? worn : null;

        _worn[slot] = item;

        return displaced;
    }

    /// <summary>
    /// Empties a slot and returns what was in it, or null when it was already empty. An empty
    /// slot is not an error: the player pressed a key for something they were not wearing.
    /// </summary>
    public Entity? Unequip(EquipmentSlot slot)
    {
        if (!_worn.TryGetValue(slot, out Entity? worn))
        {
            return null;
        }

        _worn.Remove(slot);

        return worn;
    }

    /// <summary>True when this exact item is in a slot. Used to mark the pack list.</summary>
    public bool IsEquipped(Entity item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return _worn.ContainsValue(item);
    }

    // Adds one bonus across every equipped item. Equippable is non-null by the time an item is
    // in a slot, which Equip is what guarantees.
    private int SumOf(Func<Equippable, int> bonus)
    {
        int total = 0;

        foreach (Entity worn in _worn.Values)
        {
            total += bonus(worn.Equippable!);
        }

        return total;
    }
}

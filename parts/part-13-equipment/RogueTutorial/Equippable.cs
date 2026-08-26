/*
 * What an item does when it is worn or wielded.
 *
 * A component like Consumable and Fighter: an item has one or it is not equipment. The bonuses
 * are read wherever the numbers are needed and are never written into the wearer, so taking
 * something off cannot leave part of it behind.
 *
 * Usage:
 *
 *     Entity sword = new Entity("sword", '/', Color.Gray, cell, blocksMovement: false, RenderLayer.Item);
 *
 *     // A weapon adds attack and nothing else; armour is the same the other way round.
 *     sword.Equippable = new Equippable(EquipmentSlot.Weapon, attackBonus: 3, defenceBonus: 0);
 *
 *     int bonus = sword.Equippable.AttackBonus;   // -> 3
 *
 * Refuses a negative bonus: cursed equipment is a design this tutorial does not have, and a
 * negative here would arrive as an unexplained weakening far from the item that caused it.
 */

using System;

namespace RogueTutorial;

internal sealed class Equippable
{
    /// <summary>Where this is worn.</summary>
    public EquipmentSlot Slot { get; }

    /// <summary>How much this adds to attack while it is equipped.</summary>
    public int AttackBonus { get; }

    /// <summary>How much this adds to defence while it is equipped.</summary>
    public int DefenceBonus { get; }

    /// <summary>
    /// Records what a piece of equipment is worth. Throws ArgumentOutOfRangeException on a
    /// negative bonus, and ArgumentException when both bonuses are zero - equipment that changes
    /// nothing is an item that should not have been made equipment.
    /// </summary>
    public Equippable(EquipmentSlot slot, int attackBonus, int defenceBonus)
    {
        if (attackBonus < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attackBonus), attackBonus, "Equipment does not weaken its wearer.");
        }

        if (defenceBonus < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(defenceBonus), defenceBonus, "Equipment does not weaken its wearer.");
        }

        // Something worth wearing has to be worth something.
        if (attackBonus == 0 && defenceBonus == 0)
        {
            throw new ArgumentException("Equipment must change at least one number.", nameof(attackBonus));
        }

        Slot = slot;
        AttackBonus = attackBonus;
        DefenceBonus = defenceBonus;
    }
}

/*
 * Where a piece of equipment goes.
 *
 * Two slots, because two is enough to make the choice interesting and every extra slot is
 * another thing to draw, save and explain. A third would be added here and nowhere else.
 *
 * Usage:
 *
 *     Equippable sword = new Equippable(EquipmentSlot.Weapon, attackBonus: 3, defenceBonus: 0);
 *
 * Each slot holds at most one thing, so equipping into a full slot displaces what was there.
 */

namespace RogueTutorial;

/// <summary>Where a piece of equipment is worn.</summary>
internal enum EquipmentSlot
{
    /// <summary>Held in the hand. Adds to attack.</summary>
    Weapon,

    /// <summary>Worn on the body. Adds to defence.</summary>
    Armour,
}

/*
 * What an entity needs in order to fight: hit points, and the two numbers that decide damage.
 *
 * This is a component rather than a kind of entity. A corpse and a sword both stop being able to
 * fight, and an object cannot change its own type in C# - so "can fight" is something an entity
 * has or does not have, and death removes it.
 *
 * Usage:
 *
 *     Fighter rat = new Fighter(maximumHitPoints: 4, attack: 3, defence: 0, experienceAwarded: 12);
 *
 *     int dealt = rat.TakeDamage(2);      // -> 2, and HitPoints falls to 2
 *     bool dead = rat.IsDead;             // -> false
 *     rat.TakeDamage(99);                 // HitPoints floors at 0 rather than going negative
 *
 *     int recovered = rat.Heal(3);        // -> how much was actually restored, capped at the maximum
 *     rat.RaiseAttack(1);                 // levelling up; the numbers are fixed until then
 *
 * Refuses a maximum below one, and negative attack, defence, damage or healing.
 */

using System;
using System.Diagnostics;

namespace RogueTutorial;

internal sealed class Fighter
{
    /// <summary>Hit points when undamaged.</summary>
    public int MaximumHitPoints { get; private set; }

    /// <summary>Hit points now. Never below zero, never above the maximum.</summary>
    public int HitPoints { get; private set; }

    /// <summary>How hard this fighter hits, before the target's defence is subtracted.</summary>
    public int Attack { get; private set; }

    /// <summary>How much incoming damage this fighter subtracts from every blow.</summary>
    public int Defence { get; private set; }

    /// <summary>How much experience killing this fighter is worth.</summary>
    public int ExperienceAwarded { get; }

    /// <summary>True once hit points have reached zero.</summary>
    public bool IsDead => HitPoints <= 0;

    /// <summary>
    /// Records a fighter's numbers, starting at full health. Throws ArgumentOutOfRangeException
    /// when the maximum is below one, since a fighter that begins dead is a table entry somebody
    /// meant to delete, or when attack or defence is negative - a negative defence would turn
    /// every blow into a bonus.
    /// </summary>
    public Fighter(int maximumHitPoints, int attack, int defence, int experienceAwarded)
    {
        if (maximumHitPoints < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumHitPoints), maximumHitPoints, "A fighter needs at least one hit point.");
        }

        if (attack < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attack), attack, "Attack cannot be negative.");
        }

        if (defence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(defence), defence, "Defence cannot be negative.");
        }

        if (experienceAwarded < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(experienceAwarded), experienceAwarded, "Experience awarded cannot be negative.");
        }

        MaximumHitPoints = maximumHitPoints;
        HitPoints = maximumHitPoints;
        Attack = attack;
        Defence = defence;
        ExperienceAwarded = experienceAwarded;
    }

    /// <summary>
    /// Raises the maximum and fills in the new hit points as well, so a level felt as an
    /// improvement rather than as a longer bar to refill. Throws ArgumentOutOfRangeException on
    /// a gain below one, which would be a level that changed nothing.
    /// </summary>
    public void RaiseMaximumHitPoints(int gain)
    {
        RejectGainBelowOne(gain);

        MaximumHitPoints += gain;
        HitPoints += gain;

        Debug.Assert(HitPoints <= MaximumHitPoints, "Raising the maximum must not overfill it.");
    }

    /// <summary>Raises attack. Throws ArgumentOutOfRangeException on a gain below one.</summary>
    public void RaiseAttack(int gain)
    {
        RejectGainBelowOne(gain);

        Attack += gain;
    }

    /// <summary>Raises defence. Throws ArgumentOutOfRangeException on a gain below one.</summary>
    public void RaiseDefence(int gain)
    {
        RejectGainBelowOne(gain);

        Defence += gain;
    }

    // A gain of zero or less is a caller mistake: a level up that improves nothing.
    private static void RejectGainBelowOne(int gain)
    {
        if (gain < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(gain), gain, "A gain must be at least one.");
        }
    }

    /// <summary>
    /// Subtracts damage from hit points and returns how much was actually lost, which is less
    /// than asked for when the blow would take the fighter past zero. Hit points floor at zero
    /// rather than going negative, so a corpse is never more dead than another. Throws
    /// ArgumentOutOfRangeException on negative damage.
    /// </summary>
    public int TakeDamage(int damage)
    {
        // Negative damage would heal, and healing has its own path in a later part.
        if (damage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(damage), damage, "Damage cannot be negative.");
        }

        // The blow can only take what is left, which is what makes the return value meaningful.
        int lost = Math.Min(damage, HitPoints);

        HitPoints -= lost;

        Debug.Assert(HitPoints >= 0, "Hit points must never fall below zero.");
        Debug.Assert(HitPoints <= MaximumHitPoints, "Hit points must never exceed the maximum.");

        return lost;
    }

    /// <summary>
    /// Restores hit points up to the maximum and returns how much was actually recovered, which
    /// is less than asked for near full health and zero at it. Throws ArgumentOutOfRangeException
    /// on negative healing, which would be damage arriving by the wrong door.
    /// </summary>
    public int Heal(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Healing cannot be negative.");
        }

        // Only the missing health can be restored, which is what makes the return value the
        // number to report rather than the number asked for.
        int recovered = Math.Min(amount, MaximumHitPoints - HitPoints);

        HitPoints += recovered;

        Debug.Assert(HitPoints <= MaximumHitPoints, "Healing must never exceed the maximum.");

        return recovered;
    }

    /// <summary>
    /// One blow: attack less defence, floored at zero, so a target that out-defends the attacker
    /// takes nothing rather than being healed.
    ///
    /// Static, and given the numbers rather than reading them off two fighters, because from
    /// Part 13 the numbers that matter include equipment and a Fighter does not know what its
    /// owner is wearing. The rule lives here; who supplies the numbers is the caller's business.
    /// </summary>
    public static int DamageFrom(int attack, int defence)
    {
        return Math.Max(0, attack - defence);
    }
}

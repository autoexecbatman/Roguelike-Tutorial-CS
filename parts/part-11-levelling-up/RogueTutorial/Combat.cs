/*
 * One attack: how much damage it did, and what to say about it.
 *
 * Combat is deterministic here - damage is attack less defence, with no dice. That is a
 * deliberate simplification and it buys two things: a fight can be reasoned about, and it can be
 * tested without threading a Random through every call. Part 12 is where variance belongs, once
 * there are numbers worth varying.
 *
 * Usage:
 *
 *     AttackResult result = Combat.Resolve(player, rat);
 *
 *     int dealt = result.DamageDealt;    // -> 3
 *     bool died = result.TargetDied;     // -> true if that took it to zero
 *     string say = result.Message;       // -> "You hit the Rat for 3 damage."
 *
 * Refuses an attacker or target that cannot fight: something with no Fighter has no business in
 * a fight, and reaching here with one is a bug in the caller rather than a case to handle.
 */

using System;

namespace RogueTutorial;

/// <summary>What one attack did.</summary>
internal readonly struct AttackResult
{
    /// <summary>Hit points actually removed, which is zero when defence absorbed the blow.</summary>
    public int DamageDealt { get; }

    /// <summary>True when this attack took the target to zero hit points.</summary>
    public bool TargetDied { get; }

    /// <summary>What to put in the message log.</summary>
    public string Message { get; }

    internal AttackResult(int damageDealt, bool targetDied, string message)
    {
        DamageDealt = damageDealt;
        TargetDied = targetDied;
        Message = message;
    }
}

internal static class Combat
{
    /// <summary>
    /// Resolves one attack: works out the damage, applies it, kills the target if that took it
    /// to zero, and returns what happened along with the line to log. Throws
    /// ArgumentNullException on a null argument and ArgumentException when either side has no
    /// Fighter, since neither can take part in a fight.
    /// </summary>
    public static AttackResult Resolve(Entity attacker, Entity target)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);

        // An attacker with no Fighter means something reached combat that should not have; say
        // so here rather than dereferencing null two lines down.
        if (attacker.Fighter is null)
        {
            throw new ArgumentException($"{attacker.Name} cannot attack: it has no Fighter.", nameof(attacker));
        }

        if (target.Fighter is null)
        {
            throw new ArgumentException($"{target.Name} cannot be attacked: it has no Fighter.", nameof(target));
        }

        int damage = attacker.Fighter.DamageAgainst(target.Fighter);

        // A blow that defence absorbs entirely still happened, and the log should say so.
        if (damage == 0)
        {
            return new AttackResult(0, false, $"{attacker.Name} attacks {target.Name} but does no damage.");
        }

        int dealt = target.Fighter.TakeDamage(damage);

        bool died = target.Fighter.IsDead;

        string message = $"{attacker.Name} hits {target.Name} for {dealt} damage.";

        if (died)
        {
            // Die clears the Fighter, so both the name and the award are read before it is
            // called - afterwards there is nothing left to read them from.
            string targetName = target.Name;
            int award = target.Fighter.ExperienceAwarded;

            target.Die();

            message = $"{message} {targetName} dies.";

            // Only something that collects experience gets any; a monster killing another
            // monster in a later part would earn nothing from it.
            if (attacker.Level is not null && award > 0)
            {
                attacker.Level.Award(award);

                message = $"{message} You gain {award} experience.";
            }
        }

        return new AttackResult(dealt, died, message);
    }
}

/*
 * What an item does when it is used up.
 *
 * A component, exactly as Fighter is: an item is an ordinary Entity that has a Consumable and no
 * Fighter. That keeps items and creatures the same kind of thing, which is what lets one entity
 * list hold both and one draw path draw both.
 *
 * Usage:
 *
 *     Entity potion = new Entity("Healing potion", '!', Color.Magenta, cell, blocksMovement: false);
 *     potion.Consumable = new Consumable(ConsumableKind.Healing, power: 8, radius: 0);
 *
 *     UseResult result = potion.Consumable.UseOn(player);
 *     // -> result.Message  "You drink the Healing potion and recover 6 hit points."
 *     // -> result.Consumed true when the item should be removed from the pack
 *
 * Two of the kinds need somewhere to aim. Those are resolved through UseAt rather than UseOn,
 * and asking for the wrong one throws rather than guessing: a scroll used on the reader instead
 * of on a target is a bug that would look like bad luck.
 *
 * An item that would do nothing is not consumed - drinking a healing potion at full health
 * wastes it, and a roguelike that lets you do that by accident is a roguelike people stop
 * playing. Refuses a power below one and a null user.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using SadRogue.Primitives;

namespace RogueTutorial;

/// <summary>The kinds of thing an item can do.</summary>
internal enum ConsumableKind
{
    /// <summary>Restores hit points to whoever drinks it. Needs no target.</summary>
    Healing,

    /// <summary>Strikes one creature at the chosen cell.</summary>
    Lightning,

    /// <summary>Burns everything within a radius of the chosen cell, the reader included.</summary>
    Fireball,
}

/// <summary>What came of using an item.</summary>
internal readonly struct UseResult
{
    /// <summary>True when the item was spent and should leave the pack.</summary>
    public bool Consumed { get; }

    /// <summary>What to put in the message log.</summary>
    public string Message { get; }

    internal UseResult(bool consumed, string message)
    {
        Consumed = consumed;
        Message = message;
    }
}

internal sealed class Consumable
{
    /// <summary>What this item does.</summary>
    public ConsumableKind Kind { get; }

    /// <summary>
    /// How far the effect spreads from the cell it lands on. Zero for everything that hits one
    /// creature, which is every kind but Fireball.
    /// </summary>
    public int Radius { get; }

    /// <summary>
    /// True when using this needs somewhere to aim. The two that do are resolved through UseAt;
    /// asking for the wrong method throws rather than picking a target on the player's behalf.
    /// </summary>
    public bool NeedsTarget => Kind is ConsumableKind.Lightning or ConsumableKind.Fireball;

    /// <summary>How much it does it by: hit points restored, for a healing item.</summary>
    public int Power { get; }

    /// <summary>
    /// Records what an item does. Throws ArgumentOutOfRangeException on a power below one, since
    /// an item that does nothing measurable is a table entry somebody meant to finish.
    /// </summary>
    public Consumable(ConsumableKind kind, int power, int radius)
    {
        if (power < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(power), power, "A consumable needs a power of at least one.");
        }

        if (radius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "A blast radius cannot be negative.");
        }

        Kind = kind;
        Power = power;
        Radius = radius;
    }

    /// <summary>
    /// Applies this item's effect to the user and reports what happened. An effect that would
    /// change nothing leaves the item unconsumed, so a wasted turn is not also a wasted item.
    /// Throws ArgumentNullException on a null user and ArgumentException when the user cannot be
    /// affected at all - a corpse has no health to restore.
    /// </summary>
    public UseResult UseOn(Entity user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.Fighter is null)
        {
            throw new ArgumentException($"{user.Name} has no Fighter and cannot use an item.", nameof(user));
        }

        // Aiming is the caller's job, and doing it for them would pick a target silently.
        if (NeedsTarget)
        {
            throw new InvalidOperationException($"{Kind} needs a target; use UseAt instead.");
        }

        return Kind switch
        {
            ConsumableKind.Healing => Heal(user),
            _ => throw new InvalidOperationException($"No effect is defined for {Kind}."),
        };
    }

    /// <summary>
    /// Applies this item's effect at a chosen cell and reports what happened. An effect that
    /// finds nothing to hit leaves the item unconsumed, so a miss costs the turn rather than the
    /// scroll. Throws ArgumentNullException on a null argument and InvalidOperationException when
    /// this kind needs no target - a healing potion aimed across the room is a caller error.
    /// </summary>
    public UseResult UseAt(Entity user, Point target, GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(world);

        if (!NeedsTarget)
        {
            throw new InvalidOperationException($"{Kind} needs no target; use UseOn instead.");
        }

        return Kind switch
        {
            ConsumableKind.Lightning => Strike(target, world),
            ConsumableKind.Fireball => Burn(target, world),
            _ => throw new InvalidOperationException($"No aimed effect is defined for {Kind}."),
        };
    }

    // Hits one creature at the chosen cell, if there is one.
    private UseResult Strike(Point target, GameWorld world)
    {
        Entity? victim = world.BlockingEntityAt(target);

        // Aiming at empty floor is a miss, and a miss must not spend the scroll.
        if (victim?.Fighter is null)
        {
            return new UseResult(false, "The lightning strikes nothing.");
        }

        string name = victim.Name;

        int dealt = victim.Fighter.TakeDamage(Power);

        // Read the name before Die renames it, exactly as Combat does.
        string message = $"Lightning strikes the {name} for {dealt} damage.";

        if (victim.Fighter.IsDead)
        {
            victim.Die();
            message = $"{message} {name} dies.";
        }

        return new UseResult(true, message);
    }

    // Burns everything within the radius, including whoever read the scroll.
    private UseResult Burn(Point target, GameWorld world)
    {
        List<string> struck = new List<string>();

        // Snapshotted, because Die converts an entity in place while this walks the list.
        foreach (Entity entity in world.Entities.ToList())
        {
            if (entity.Fighter is null)
            {
                continue;
            }

            // Round rather than square, matching how sight measures: a square blast reads as a
            // bug even when it is deliberate, and the player is aiming by eye.
            int deltaX = entity.Position.X - target.X;
            int deltaY = entity.Position.Y - target.Y;

            if ((deltaX * deltaX) + (deltaY * deltaY) > Radius * Radius)
            {
                continue;
            }

            string name = entity.Name;

            entity.Fighter.TakeDamage(Power);

            if (entity.Fighter.IsDead)
            {
                entity.Die();
                struck.Add($"{name} dies");
            }
            else
            {
                struck.Add($"{name} is burned");
            }
        }

        // A blast that touched nothing is a wasted turn rather than a wasted scroll.
        if (struck.Count == 0)
        {
            return new UseResult(false, "The fireball burns nothing.");
        }

        return new UseResult(true, $"The fireball erupts: {string.Join(", ", struck)}.");
    }

    // Restores health, up to the maximum, and reports how much was actually recovered.
    private UseResult Heal(Entity user)
    {
        Fighter fighter = user.Fighter!;

        int recovered = fighter.Heal(Power);

        // Already at full health: the item is not spent, and the message says why rather than
        // reporting a heal of zero.
        if (recovered == 0)
        {
            return new UseResult(false, "You are already at full health.");
        }

        return new UseResult(true, $"You recover {recovered} hit points.");
    }
}

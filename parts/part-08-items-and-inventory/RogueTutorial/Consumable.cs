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
 *     potion.Consumable = new Consumable(ConsumableKind.Healing, power: 8);
 *
 *     UseResult result = potion.Consumable.UseOn(player);
 *     // -> result.Message  "You drink the Healing potion and recover 6 hit points."
 *     // -> result.Consumed true when the item should be removed from the pack
 *
 * An item that would do nothing is not consumed - drinking a healing potion at full health
 * wastes it, and a roguelike that lets you do that by accident is a roguelike people stop
 * playing. Refuses a power below one and a null user.
 */

using System;

namespace RogueTutorial;

/// <summary>The kinds of thing an item can do. Part 9 adds the ones that need a target.</summary>
internal enum ConsumableKind
{
    /// <summary>Restores hit points to whoever drinks it.</summary>
    Healing,
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

    /// <summary>How much it does it by: hit points restored, for a healing item.</summary>
    public int Power { get; }

    /// <summary>
    /// Records what an item does. Throws ArgumentOutOfRangeException on a power below one, since
    /// an item that does nothing measurable is a table entry somebody meant to finish.
    /// </summary>
    public Consumable(ConsumableKind kind, int power)
    {
        if (power < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(power), power, "A consumable needs a power of at least one.");
        }

        Kind = kind;
        Power = power;
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

        return Kind switch
        {
            ConsumableKind.Healing => Heal(user),
            _ => throw new InvalidOperationException($"No effect is defined for {Kind}."),
        };
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

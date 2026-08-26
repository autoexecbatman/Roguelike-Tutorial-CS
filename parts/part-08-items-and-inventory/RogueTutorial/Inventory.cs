/*
 * What an entity is carrying.
 *
 * A component like Fighter and Consumable, so the player has one and a monster could be given
 * one later without changing what an entity is.
 *
 * The capacity is a real limit rather than decoration. An unbounded pack removes every decision
 * about what to leave behind, which is most of what makes picking things up interesting.
 *
 * Usage:
 *
 *     Inventory pack = new Inventory(capacity: 26);
 *
 *     bool tookIt = pack.TryAdd(potion);      // -> false when the pack is full
 *     Entity? third = pack.At(2);             // -> null when nothing is in that slot
 *     pack.Remove(potion);                    // after it has been used up
 *     int carried = pack.Items.Count;
 *
 * Twenty-six is the usual capacity because the items are chosen with letters, and there are
 * twenty-six of those. Refuses a capacity below one, a null item, and an item added twice.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RogueTutorial;

internal sealed class Inventory
{
    // What is carried, in the order it was picked up. The order is the slot order the player
    // sees, so it must not be sorted underneath them.
    private readonly List<Entity> _items = new List<Entity>();

    /// <summary>The most items that can be carried.</summary>
    public int Capacity { get; }

    /// <summary>What is carried, oldest first.</summary>
    public IReadOnlyList<Entity> Items => _items;

    /// <summary>True when nothing more can be picked up.</summary>
    public bool IsFull => _items.Count >= Capacity;

    /// <summary>
    /// Creates an empty pack. Throws ArgumentOutOfRangeException on a capacity below one, since
    /// a pack that can hold nothing is a configuration mistake rather than a hard mode.
    /// </summary>
    public Inventory(int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "A pack must hold at least one item.");
        }

        Capacity = capacity;
    }

    /// <summary>
    /// Adds an item and reports whether there was room. A full pack answers false rather than
    /// throwing: running out of space is an ordinary thing that happens to a player, not a bug.
    /// Throws ArgumentNullException on a null item, and ArgumentException on one already carried,
    /// which would let the same entity be dropped twice.
    /// </summary>
    public bool TryAdd(Entity item)
    {
        ArgumentNullException.ThrowIfNull(item);

        // The same entity in two slots would be dropped twice and used twice.
        if (_items.Contains(item))
        {
            throw new ArgumentException($"{item.Name} is already in this pack.", nameof(item));
        }

        if (IsFull)
        {
            return false;
        }

        _items.Add(item);

        Debug.Assert(_items.Count <= Capacity, "A pack must never hold more than its capacity.");

        return true;
    }

    /// <summary>
    /// Removes an item. Throws ArgumentNullException on a null item and ArgumentException when it
    /// is not carried, because removing something that was never there means the caller has lost
    /// track of what it holds.
    /// </summary>
    public void Remove(Entity item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!_items.Remove(item))
        {
            throw new ArgumentException($"{item.Name} is not in this pack.", nameof(item));
        }
    }

    /// <summary>
    /// The item in the given slot, or null when the slot is empty or does not exist. Answering
    /// null rather than throwing is what lets a keypress be checked against the pack directly:
    /// pressing 'd' with three items carried is a miss, not an error.
    /// </summary>
    public Entity? At(int slot)
    {
        if (slot < 0 || slot >= _items.Count)
        {
            return null;
        }

        return _items[slot];
    }
}

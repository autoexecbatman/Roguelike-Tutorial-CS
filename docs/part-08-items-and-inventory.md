# Part 8: Items and inventory

- [What it is](#what-it-is)
- [How to use it](#how-to-use-it)
- [How to set it up](#how-to-set-it-up)

---

# What it is

Potions on the floor, a pack to carry them in, and a way to drink them. Press `i` and the pack
opens over the map:

```
+--------------------------------------+########################################
| Pack                                 |########################################
| Esc closes, Shift drops              |########################################
+--------------------------------------+########################################
| a) healing potion                    |##########################.......#######
| b) healing potion                    |###........###############.!.....#######
+--------------------------------------+........r........................#######
#####....!###...r###.k....!.###############........########.######.......#######
#######.#####....###...............................######.!..#####..!....#######
#######.############....r...###############........######....###################
```

## The one-way door is not the items

Until now there was one kind of input: a movement key that spent a turn. Every key meant the same
thing every time.

That ends here. `d` walks nowhere on the map and picks the fourth slot in the pack. Part 9's
targeting cursor and Part 10's prompts need the same thing. So the game gets a **mode**, and the
meaning of a key is worked out before anything acts on it:

```csharp
GameCommand command = CommandReader.Read(pressedKeys, world.Mode, shiftHeld);
```

**The mode lives on `GameWorld`, not on `RootScreen`.** Putting a `bool inventoryOpen` on the
screen class would be the obvious thing and would undo what Part 5 spent its whole budget on:
`RootScreen` cannot be constructed in a test, so nothing on it can be tested. With the mode on
the world, a test opens the pack and presses a letter with no window anywhere:

```csharp
GameCommand command = CommandReader.Read(new[] { Keys.Left }, GameMode.ShowingInventory);

Assert.Equal(GameCommandKind.None, command.Kind);   // the map does not move while the pack is open
```

`RootScreen` is now a router. It reads the keyboard, asks for a command, and calls one method.

## Items are entities, which is why this part is short

An item is an `Entity` with a `Consumable`, no `Fighter`, and `blocksMovement: false`:

```csharp
Entity potion = new Entity("healing potion", '!', magenta, cell, blocksMovement: false);
potion.Consumable = new Consumable(ConsumableKind.Healing, power: 8);
```

Nothing about drawing, generation or the entity list had to change to accommodate them. That is
the return on decisions made three and five parts ago: `blocksMovement` was added in Part 5 for
corpses, and `Fighter` established the component pattern in Part 6.

## A wasted turn must not also be a wasted item

Drinking a healing potion at full health is the case worth getting right:

```csharp
if (recovered == 0)
{
    return new UseResult(false, "You are already at full health.");
}
```

`Consumed` is false, so the potion stays in the pack and no turn is spent. A roguelike that
silently destroys an item because you mistyped is a roguelike people stop playing.

## Capacity is a real limit

```csharp
public bool TryAdd(Entity item)
```

`TryAdd` rather than `Add`, and it answers false rather than throwing: a full pack is an ordinary
thing that happens to a player, not a bug. An unbounded pack would remove every decision about
what to leave behind, which is most of what makes finding things interesting.

Twenty-six slots, because items are chosen by letter and there are twenty-six letters. `At` on an
empty slot answers null rather than throwing, so a keypress can be checked against the pack
directly - pressing `d` while carrying two things is a miss, not an error.

## The pack is an overlay, not a region

`ScreenLayout` divides the window permanently. A panel that appears and vanishes is a different
kind of thing, and giving it a region would fight the tiling assertion Part 7 relies on. So the
pack is drawn over the map after everything else, and framed in plain ASCII so it reads as an
interface rather than as corruption on the dungeon.

## What is deliberately wrong

**One kind of item.** Healing potions and nothing else. Part 9 adds the scrolls that need a
target, which is what the `ConsumableKind` enum is there for.

**Two items can share a cell**, and only the top one is drawn or picked up. Creatures block each
other; things on the floor do not, and sorting that out properly means a stack.

**No item is identified or unidentified.** Everything says what it is.

**Dropping costs a turn but picking up in a fight is still free of risk** beyond the turn itself.

---

# How to use it

## Play it

```
cd parts/part-08-items-and-inventory
dotnet run --project RogueTutorial
```

| Key | Effect |
|---|---|
| Arrows, keypad | Move or attack |
| `g` | Pick up what is underfoot |
| `i` | Open the pack |
| `a` .. `z` | Use that slot |
| Shift + `a` .. `z` | Drop that slot |
| `Esc` or `i` | Close the pack |

Magenta `!` on the floor are healing potions. Take some damage first, or drinking one tells you
so and keeps the potion.

## Run the tests

```
dotnet test                                  # 329 tests
dotnet test --filter "Category!=EndToEnd"    # no window
```

| Test class | Level | Covers |
|---|---|---|
| [`InventoryTests`](../parts/part-08-items-and-inventory/RogueTutorial.Tests/InventoryTests.cs) | unit | capacity, ordering, empty slots |
| [`ItemUseTests`](../parts/part-08-items-and-inventory/RogueTutorial.Tests/ItemUseTests.cs) | unit + integration | using, picking up, dropping, and every key in every mode |
| [`ScreenComposerTests`](../parts/part-08-items-and-inventory/RogueTutorial.Tests/ScreenComposerTests.cs) | unit | the overlay, and that the letters match the keys |

## Prove the tests can fail

| Change | Expect |
|---|---|
| `Consumable`: consume a potion that healed nothing | 1 fails |
| `Inventory`: ignore the capacity | 2 fail |
| `Fighter.Heal`: let healing pass the maximum | 3 fail |
| `CommandReader`: read inventory keys as map keys | 4 fail |
| `CommandReader`: ignore shift | 1 fails |
| `GameWorld`: leave picked-up items on the map | 1 fails |

---

# How to set it up

> **You are in:** your project folder, the one holding `RogueTutorial/` and `RogueTutorial.Tests/`

## Step 1: retitle the window

One line in `RogueTutorial/Program.cs`:

```csharp
const string WindowTitle = "Roguelike Tutorial - Part 8: Items and inventory";
```

## Step 2: the source files

**Each block below is the complete file.** Five are new; `Fighter.cs`, `Entity.cs`,
`GameWorld.cs`, `ScreenComposer.cs` and `RootScreen.cs` already exist and should be replaced
entirely.

**Do not build until every file in this step is in place** - C# compiles a project as a whole,
so a half-finished step fails on files that are perfectly correct.

### [`RogueTutorial/Fighter.cs`](../parts/part-08-items-and-inventory/RogueTutorial/Fighter.cs)

The Part 6 file, with Heal added. Consumable needs it, so it comes first.

<!-- generated-diff -->
**Changed from Part 7.** The complete file follows; this is only what moved:

```diff
--- part-07-log-and-health-bar/Fighter.cs
+++ current/Fighter.cs
@@ -13,8 +13,9 @@
  *     bool dead = rat.IsDead;             // -> false
  *     rat.TakeDamage(99);                 // HitPoints floors at 0 rather than going negative
  *
- * Refuses a maximum below one, and negative attack, defence or damage. Healing arrives in
- * Part 8 with potions; there is nothing here that raises hit points yet.
+ *     int recovered = rat.Heal(3);        // -> how much was actually restored, capped at the maximum
+ *
+ * Refuses a maximum below one, and negative attack, defence, damage or healing.
  */
 
 using System;
@@ -95,6 +96,29 @@
     }
 
     /// <summary>
+    /// Restores hit points up to the maximum and returns how much was actually recovered, which
+    /// is less than asked for near full health and zero at it. Throws ArgumentOutOfRangeException
+    /// on negative healing, which would be damage arriving by the wrong door.
+    /// </summary>
+    public int Heal(int amount)
+    {
+        if (amount < 0)
+        {
+            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Healing cannot be negative.");
+        }
+
+        // Only the missing health can be restored, which is what makes the return value the
+        // number to report rather than the number asked for.
+        int recovered = Math.Min(amount, MaximumHitPoints - HitPoints);
+
+        HitPoints += recovered;
+
+        Debug.Assert(HitPoints <= MaximumHitPoints, "Healing must never exceed the maximum.");
+
+        return recovered;
+    }
+
+    /// <summary>
     /// The damage this fighter deals to the target: attack less the target's defence, floored at
     /// zero, so a target that out-defends the attacker takes nothing rather than being healed.
     /// Throws ArgumentNullException on a null target.
```
<!-- generated-diff -->

```csharp
/*
 * What an entity needs in order to fight: hit points, and the two numbers that decide damage.
 *
 * This is a component rather than a kind of entity. A corpse and a sword both stop being able to
 * fight, and an object cannot change its own type in C# - so "can fight" is something an entity
 * has or does not have, and death removes it.
 *
 * Usage:
 *
 *     Fighter rat = new Fighter(maximumHitPoints: 4, attack: 3, defence: 0);
 *
 *     int dealt = rat.TakeDamage(2);      // -> 2, and HitPoints falls to 2
 *     bool dead = rat.IsDead;             // -> false
 *     rat.TakeDamage(99);                 // HitPoints floors at 0 rather than going negative
 *
 *     int recovered = rat.Heal(3);        // -> how much was actually restored, capped at the maximum
 *
 * Refuses a maximum below one, and negative attack, defence, damage or healing.
 */

using System;
using System.Diagnostics;

namespace RogueTutorial;

internal sealed class Fighter
{
    /// <summary>Hit points when undamaged.</summary>
    public int MaximumHitPoints { get; }

    /// <summary>Hit points now. Never below zero, never above the maximum.</summary>
    public int HitPoints { get; private set; }

    /// <summary>How hard this fighter hits, before the target's defence is subtracted.</summary>
    public int Attack { get; }

    /// <summary>How much incoming damage this fighter subtracts from every blow.</summary>
    public int Defence { get; }

    /// <summary>True once hit points have reached zero.</summary>
    public bool IsDead => HitPoints <= 0;

    /// <summary>
    /// Records a fighter's numbers, starting at full health. Throws ArgumentOutOfRangeException
    /// when the maximum is below one, since a fighter that begins dead is a table entry somebody
    /// meant to delete, or when attack or defence is negative - a negative defence would turn
    /// every blow into a bonus.
    /// </summary>
    public Fighter(int maximumHitPoints, int attack, int defence)
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

        MaximumHitPoints = maximumHitPoints;
        HitPoints = maximumHitPoints;
        Attack = attack;
        Defence = defence;
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
    /// The damage this fighter deals to the target: attack less the target's defence, floored at
    /// zero, so a target that out-defends the attacker takes nothing rather than being healed.
    /// Throws ArgumentNullException on a null target.
    /// </summary>
    public int DamageAgainst(Fighter target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return Math.Max(0, Attack - target.Defence);
    }
}
```

### [`RogueTutorial/Consumable.cs`](../parts/part-08-items-and-inventory/RogueTutorial/Consumable.cs)

What an item does when used up. A component, exactly as Fighter is.

```csharp
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
```

### [`RogueTutorial/Inventory.cs`](../parts/part-08-items-and-inventory/RogueTutorial/Inventory.cs)

What an entity carries, with a capacity that actually bites.

```csharp
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
```

### [`RogueTutorial/Entity.cs`](../parts/part-08-items-and-inventory/RogueTutorial/Entity.cs)

The Part 6 file, with the Consumable and Inventory components.

<!-- generated-diff -->
**Changed from Part 7.** The complete file follows; this is only what moved:

```diff
--- part-07-log-and-health-bar/Entity.cs
+++ current/Entity.cs
@@ -56,6 +56,12 @@
     /// </summary>
     public Fighter? Fighter { get; set; }
 
+    /// <summary>What this entity does when used up, or null when it is not an item.</summary>
+    public Consumable? Consumable { get; set; }
+
+    /// <summary>What this entity is carrying, or null when it carries nothing ever.</summary>
+    public Inventory? Inventory { get; set; }
+
     /// <summary>
     /// Creates an entity at a starting cell. Throws ArgumentException on a blank name, since an
     /// unnamed entity would surface much later as an empty word in a message.
```
<!-- generated-diff -->

```csharp
/*
 * Anything that occupies one cell and is drawn on top of the map: the player, a monster,
 * later an item lying on the floor.
 *
 * Usage:
 *
 *     Entity player = new Entity("Player", '@', Color.White, new Point(40, 12), blocksMovement: true);
 *     Entity corpse = new Entity("Corpse", '%', Color.Red, new Point(41, 12), blocksMovement: false);
 *
 *     rat.Fighter = new Fighter(maximumHitPoints: 4, attack: 3, defence: 0);
 *     bool canFight = rat.Fighter is not null;   // -> true until it dies
 *     player.MoveTo(new Point(41, 12));   // unconditional; see MovementRules for the rules
 *     string who = player.Name;           // -> "Player", for messages in a later part
 *
 * Fighter is the component that lets an entity take part in combat. It is null for anything that
 * cannot fight - an item on the floor, or a corpse, which is a monster whose Fighter was removed
 * when it died. A component rather than a subclass, because an object cannot change its own type
 * in C# and death has to change what an entity is capable of.
 *
 * blocksMovement is explicit at every call: a creature occupies its cell and nothing else may
 * stand there, while an item on the floor is walked over. There is no default, because guessing
 * wrong is silent - you notice when a player walks through a monster.
 *
 * Refuses a null, empty or whitespace name. It applies no movement rules of its own: whether a
 * destination is legal is the map's business, and MovementRules is where the two meet.
 */

using System;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class Entity
{
    /// <summary>What this is called, for messages such as "the Villager blocks the way".</summary>
    public string Name { get; private set; }

    /// <summary>The character drawn for it.</summary>
    public char Glyph { get; private set; }

    /// <summary>The colour that character is drawn in.</summary>
    public Color Foreground { get; private set; }

    /// <summary>The cell it currently occupies.</summary>
    public Point Position { get; private set; }

    /// <summary>
    /// True when nothing else may stand on this entity's cell. Creatures block; items lying on
    /// the floor do not. A corpse stops blocking, which is why this is settable.
    /// </summary>
    public bool BlocksMovement { get; private set; }

    /// <summary>
    /// This entity's combat numbers, or null when it cannot fight. Set to null by Die, which is
    /// what turns a monster into a corpse.
    /// </summary>
    public Fighter? Fighter { get; set; }

    /// <summary>What this entity does when used up, or null when it is not an item.</summary>
    public Consumable? Consumable { get; set; }

    /// <summary>What this entity is carrying, or null when it carries nothing ever.</summary>
    public Inventory? Inventory { get; set; }

    /// <summary>
    /// Creates an entity at a starting cell. Throws ArgumentException on a blank name, since an
    /// unnamed entity would surface much later as an empty word in a message.
    /// </summary>
    public Entity(string name, char glyph, Color foreground, Point startingPosition, bool blocksMovement)
    {
        // A blank name is a construction mistake; fail here rather than in the message log.
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("An entity needs a name.", nameof(name));
        }

        Name = name;
        Glyph = glyph;
        Foreground = foreground;
        Position = startingPosition;
        BlocksMovement = blocksMovement;
    }

    /// <summary>
    /// Puts the entity at the given cell unconditionally. The caller is expected to have decided
    /// the destination is legal; MovementRules.DestinationFor is what makes that decision.
    /// </summary>
    public void MoveTo(Point destination)
    {
        Position = destination;
    }

    /// <summary>
    /// Turns this entity into its own corpse: renamed, drawn as a dark red '%', no longer able
    /// to fight, and no longer blocking the cell it lies on.
    ///
    /// The entity is converted rather than removed, because deleting it would mean editing the
    /// entity list while something is walking it. Throws InvalidOperationException on something
    /// that was never able to fight, since an item cannot die.
    /// </summary>
    public void Die()
    {
        if (Fighter is null)
        {
            throw new InvalidOperationException($"{Name} has no Fighter and cannot die.");
        }

        Name = $"remains of {Name}";
        Glyph = '%';
        Foreground = new Color(110, 20, 20);

        // Losing the Fighter is what makes it a corpse rather than a fighter at zero health.
        Fighter = null;

        // A corpse is walked over, which is the case blocksMovement was introduced for.
        BlocksMovement = false;
    }
}
```

### [`RogueTutorial/GameCommand.cs`](../parts/part-08-items-and-inventory/RogueTutorial/GameCommand.cs)

What a key press means, and the mode that decides it. The part's real content.

```csharp
/*
 * What a key press means, worked out before anything acts on it.
 *
 * Until Part 8 there was one kind of input: a movement key that spent a turn. Now the same key
 * means different things depending on what the player is doing - 'd' walks nowhere on the map
 * and picks slot four in the inventory - so the meaning has to be decided somewhere, and it must
 * not be RootScreen, which no test can construct.
 *
 * Part 9's targeting cursor and Part 10's prompts need exactly this machinery, which is why it
 * is a type rather than a couple of branches inside the keyboard handler.
 *
 * Usage:
 *
 *     GameCommand command = CommandReader.Read(keys, world.Mode);
 *
 *     if (command.Kind == GameCommandKind.Move)  { world.MovePlayer(command.Offset); }
 *     if (command.Kind == GameCommandKind.UseItem) { world.UseItem(command.Slot); }
 *
 * Offset is meaningful only for Move, and Slot only for UseItem and DropItem. Nothing else
 * carries either.
 */

using SadRogue.Primitives;

namespace RogueTutorial;

/// <summary>What the player is doing, which decides what their keys mean.</summary>
internal enum GameMode
{
    /// <summary>Walking the dungeon. Movement keys move, and everything costs a turn.</summary>
    Playing,

    /// <summary>The pack is open. Letters choose an item and Escape closes it.</summary>
    ShowingInventory,
}

/// <summary>The kinds of thing a key press can mean.</summary>
internal enum GameCommandKind
{
    /// <summary>The key means nothing in this mode. Nothing happens and no turn is spent.</summary>
    None,

    /// <summary>Walk or attack in a direction.</summary>
    Move,

    /// <summary>Pick up whatever is underfoot.</summary>
    PickUp,

    /// <summary>Open the pack.</summary>
    OpenInventory,

    /// <summary>Close the pack without doing anything.</summary>
    CloseInventory,

    /// <summary>Use the item in a slot.</summary>
    UseItem,

    /// <summary>Drop the item in a slot.</summary>
    DropItem,
}

internal readonly struct GameCommand
{
    /// <summary>What the key meant.</summary>
    public GameCommandKind Kind { get; }

    /// <summary>Which way to move. Point.Zero for every kind but Move.</summary>
    public Point Offset { get; }

    /// <summary>Which pack slot. Minus one for every kind but UseItem and DropItem.</summary>
    public int Slot { get; }

    private GameCommand(GameCommandKind kind, Point offset, int slot)
    {
        Kind = kind;
        Offset = offset;
        Slot = slot;
    }

    /// <summary>The key meant nothing in this mode.</summary>
    public static GameCommand None => new GameCommand(GameCommandKind.None, Point.Zero, -1);

    /// <summary>Walk or attack in a direction.</summary>
    public static GameCommand Move(Point offset) => new GameCommand(GameCommandKind.Move, offset, -1);

    /// <summary>Pick up whatever is underfoot.</summary>
    public static GameCommand PickUp => new GameCommand(GameCommandKind.PickUp, Point.Zero, -1);

    /// <summary>Open the pack.</summary>
    public static GameCommand OpenInventory => new GameCommand(GameCommandKind.OpenInventory, Point.Zero, -1);

    /// <summary>Close the pack.</summary>
    public static GameCommand CloseInventory => new GameCommand(GameCommandKind.CloseInventory, Point.Zero, -1);

    /// <summary>Use what is in a slot.</summary>
    public static GameCommand UseItem(int slot) => new GameCommand(GameCommandKind.UseItem, Point.Zero, slot);

    /// <summary>Drop what is in a slot.</summary>
    public static GameCommand DropItem(int slot) => new GameCommand(GameCommandKind.DropItem, Point.Zero, slot);
}
```

### [`RogueTutorial/CommandReader.cs`](../parts/part-08-items-and-inventory/RogueTutorial/CommandReader.cs)

Turns keys into one command, given the mode.

```csharp
/*
 * Turns the keys held this frame into one command, given what the player is doing.
 *
 * This replaces MovementKeys as the entry point for input. Movement is still a table lookup and
 * still lives there; what is new is that the same key means different things in different modes,
 * so something has to decide which meaning applies.
 *
 * Usage:
 *
 *     GameCommand walk = CommandReader.Read(new[] { Keys.Left }, GameMode.Playing);
 *     // -> Move, offset (-1, 0)
 *
 *     GameCommand pick = CommandReader.Read(new[] { Keys.A }, GameMode.ShowingInventory);
 *     // -> UseItem, slot 0: 'a' is the first slot, 'b' the second, and so on
 *
 *     GameCommand nothing = CommandReader.Read(new[] { Keys.Left }, GameMode.ShowingInventory);
 *     // -> None: the map does not move while the pack is open
 *
 * Refuses a null key collection. Holding shift with a letter drops rather than uses, which is
 * why the shift state is a separate argument rather than being read from the letter.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using SadConsole.Input;
using SadRogue.Primitives;

namespace RogueTutorial;

internal static class CommandReader
{
    /// <summary>
    /// Works out what the keys mean in the given mode. Throws ArgumentNullException on a null
    /// collection. A key with no meaning in this mode yields None, which costs no turn.
    /// </summary>
    public static GameCommand Read(IReadOnlyCollection<Keys> pressedKeys, GameMode mode, bool shiftHeld)
    {
        ArgumentNullException.ThrowIfNull(pressedKeys);

        return mode switch
        {
            GameMode.Playing => ReadPlaying(pressedKeys),
            GameMode.ShowingInventory => ReadInventory(pressedKeys, shiftHeld),
            _ => GameCommand.None,
        };
    }

    /// <summary>
    /// The convenience form for callers that never hold shift, which is every test about
    /// movement. Equivalent to Read with shiftHeld false.
    /// </summary>
    public static GameCommand Read(IReadOnlyCollection<Keys> pressedKeys, GameMode mode)
    {
        return Read(pressedKeys, mode, shiftHeld: false);
    }

    // Walking the dungeon: movement, picking up, and opening the pack.
    private static GameCommand ReadPlaying(IReadOnlyCollection<Keys> pressedKeys)
    {
        // Movement first, because it is what almost every key press is.
        Point offset = MovementKeys.OffsetFor(pressedKeys);

        if (offset != Point.Zero)
        {
            return GameCommand.Move(offset);
        }

        // 'g' for get, which is the roguelike convention and leaves 'p' free.
        if (pressedKeys.Contains(Keys.G))
        {
            return GameCommand.PickUp;
        }

        if (pressedKeys.Contains(Keys.I))
        {
            return GameCommand.OpenInventory;
        }

        return GameCommand.None;
    }

    // The pack is open: letters choose a slot, Escape closes, and nothing else applies.
    private static GameCommand ReadInventory(IReadOnlyCollection<Keys> pressedKeys, bool shiftHeld)
    {
        if (pressedKeys.Contains(Keys.Escape) || pressedKeys.Contains(Keys.I))
        {
            return GameCommand.CloseInventory;
        }

        foreach (Keys key in pressedKeys)
        {
            // A to Z are contiguous in the key enum, so the letter's distance from A is the slot.
            if (key < Keys.A || key > Keys.Z)
            {
                continue;
            }

            int slot = key - Keys.A;

            // Shift turns choosing into dropping, so one set of letters covers both.
            return shiftHeld ? GameCommand.DropItem(slot) : GameCommand.UseItem(slot);
        }

        return GameCommand.None;
    }
}
```

### [`RogueTutorial/ItemTable.cs`](../parts/part-08-items-and-inventory/RogueTutorial/ItemTable.cs)

What lies in the dungeon, the same shape as MonsterTable.

```csharp
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
```

### [`RogueTutorial/GameWorld.cs`](../parts/part-08-items-and-inventory/RogueTutorial/GameWorld.cs)

The Part 7 file, with the mode and the three item actions.

<!-- generated-diff -->
**Changed from Part 7.** The complete file follows; this is only what moved:

```diff
--- part-07-log-and-health-bar/GameWorld.cs
+++ current/GameWorld.cs
@@ -11,6 +11,8 @@
  *     GameWorld world = GameWorld.Generate(80, 25, new Random(12345), MonsterTable.Standard);
  *
  *     world.MovePlayer(new Point(1, 0));                  // one step right, or an attack
+ *     world.PickUpHere();                                  // take what is underfoot
+ *     world.UseItem(slot: 0);                              // drink the first thing in the pack
  *     bool over = world.IsPlayerDead;                      // the game ends when this is true
  *     IReadOnlyList<string> said = world.Log.Latest(5);    // what just happened
  *     Point where = world.Player.Position;
@@ -50,8 +52,14 @@
     /// <summary>Everything standing in the dungeon, the player included.</summary>
     public IReadOnlyList<Entity> Entities => _entities;
 
-    /// <summary>What has happened lately. Part 7 puts this on screen.</summary>
+    /// <summary>What has happened lately, drawn under the map.</summary>
     public MessageLog Log { get; } = new MessageLog(capacity: 100);
+
+    /// <summary>
+    /// What the player is doing, which decides what their keys mean. Held here rather than on
+    /// the screen class, so a test can open the pack and press a letter without a window.
+    /// </summary>
+    public GameMode Mode { get; private set; } = GameMode.Playing;
 
     /// <summary>
     /// True once the player has been killed. Nothing stops the game yet; Part 10 decides what
@@ -101,10 +109,12 @@
     /// one seed reproduces the whole world - dungeon and monsters alike. Throws
     /// ArgumentNullException on a null argument.
     /// </summary>
-    public static GameWorld Generate(int width, int height, Random random, MonsterTable monsters)
+    public static GameWorld Generate(
+        int width, int height, Random random, MonsterTable monsters, ItemTable items)
     {
         ArgumentNullException.ThrowIfNull(random);
         ArgumentNullException.ThrowIfNull(monsters);
+        ArgumentNullException.ThrowIfNull(items);
 
         DungeonSettings settings = new DungeonSettings(maximumRooms: 30, minimumRoomSize: 6, maximumRoomSize: 10);
 
@@ -116,6 +126,9 @@
         // is an inconvenience rather than a threat.
         player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2);
 
+        // Twenty-six slots, because items are chosen by letter and there are twenty-six letters.
+        player.Inventory = new Inventory(capacity: 26);
+
         List<Entity> entities = new List<Entity> { player };
 
         // The first room is where the player starts, so it is left empty: waking up already
@@ -123,6 +136,8 @@
         for (int roomIndex = 1; roomIndex < dungeon.Rooms.Count; roomIndex++)
         {
             entities.AddRange(monsters.PopulateRoom(dungeon.Rooms[roomIndex], dungeon.Map, random));
+
+            entities.AddRange(items.PopulateRoom(dungeon.Rooms[roomIndex], dungeon.Map, random));
         }
 
         // The player is drawn last so it covers anything sharing its cell.
@@ -248,6 +263,122 @@
     }
 
     /// <summary>
+    /// Opens or closes the pack. Costs no turn: looking at what you are carrying is not an
+    /// action, and monsters do not get a move while a menu is open.
+    /// </summary>
+    public void SetMode(GameMode mode)
+    {
+        Mode = mode;
+    }
+
+    /// <summary>
+    /// Picks up whatever item is lying on the player's cell. Reports what happened through the
+    /// log: there may be nothing there, or the pack may be full, and both are ordinary outcomes
+    /// rather than errors. Picking something up spends a turn; finding nothing does not.
+    /// </summary>
+    public bool PickUpHere()
+    {
+        if (IsPlayerDead || Player.Inventory is null)
+        {
+            return false;
+        }
+
+        // The first item on this cell, ignoring creatures and the player themselves.
+        Entity? item = _entities.FirstOrDefault(
+            entity => entity != Player && entity.Consumable is not null && entity.Position == Player.Position);
+
+        if (item is null)
+        {
+            Log.Add("There is nothing here to pick up.");
+            return false;
+        }
+
+        if (!Player.Inventory.TryAdd(item))
+        {
+            Log.Add("Your pack is full.");
+            return false;
+        }
+
+        // Carried items leave the map, so they stop being drawn and stop being picked up twice.
+        _entities.Remove(item);
+
+        Log.Add($"You pick up the {item.Name}.");
+
+        RunMonsterTurns();
+
+        return true;
+    }
+
+    /// <summary>
+    /// Uses whatever is in the given slot. An empty slot is a miss rather than an error - the
+    /// player pressed a letter for something they are not carrying. An item that would do nothing
+    /// is not consumed and no turn is spent.
+    /// </summary>
+    public bool UseItem(int slot)
+    {
+        if (IsPlayerDead || Player.Inventory is null)
+        {
+            return false;
+        }
+
+        Entity? item = Player.Inventory.At(slot);
+
+        if (item?.Consumable is null)
+        {
+            return false;
+        }
+
+        UseResult result = item.Consumable.UseOn(Player);
+
+        Log.Add(result.Message);
+
+        // An item that changed nothing stays in the pack, and the turn is not spent either.
+        if (!result.Consumed)
+        {
+            return false;
+        }
+
+        Player.Inventory.Remove(item);
+
+        RunMonsterTurns();
+
+        return true;
+    }
+
+    /// <summary>
+    /// Drops whatever is in the given slot onto the player's cell. An empty slot is a miss.
+    /// Dropping spends a turn, which is what makes a full pack a real decision in a fight.
+    /// </summary>
+    public bool DropItem(int slot)
+    {
+        if (IsPlayerDead || Player.Inventory is null)
+        {
+            return false;
+        }
+
+        Entity? item = Player.Inventory.At(slot);
+
+        if (item is null)
+        {
+            return false;
+        }
+
+        Player.Inventory.Remove(item);
+
+        // Back onto the map, where the player stands, so it can be picked up again.
+        item.MoveTo(Player.Position);
+
+        // Items are drawn under creatures, so it goes at the front of the list.
+        _entities.Insert(0, item);
+
+        Log.Add($"You drop the {item.Name}.");
+
+        RunMonsterTurns();
+
+        return true;
+    }
+
+    /// <summary>
     /// Builds the picture the player currently perceives: lit where they can see, dim where they
     /// only remember, blank where they have never been.
     /// </summary>
```
<!-- generated-diff -->

```csharp
/*
 * Everything the game is: the dungeon, who is standing in it, and what the player has seen.
 *
 * This exists because the state had outgrown the screen class. RootScreen cannot be constructed
 * without a graphics host, so anything living on it is beyond the reach of a test - the same
 * boundary Part 1 drew for rules, applied now to state. A GameWorld can be built, driven and
 * inspected in a test process with no window anywhere.
 *
 * Usage:
 *
 *     GameWorld world = GameWorld.Generate(80, 25, new Random(12345), MonsterTable.Standard);
 *
 *     world.MovePlayer(new Point(1, 0));                  // one step right, or an attack
 *     world.PickUpHere();                                  // take what is underfoot
 *     world.UseItem(slot: 0);                              // drink the first thing in the pack
 *     bool over = world.IsPlayerDead;                      // the game ends when this is true
 *     IReadOnlyList<string> said = world.Log.Latest(5);    // what just happened
 *     Point where = world.Player.Position;
 *     RenderedFrame frame = world.ComposeFrame();         // what the player currently perceives
 *     Entity? blocker = world.BlockingEntityAt(where);    // null when the cell is clear
 *
 * Refuses a null argument anywhere. Generation refuses a map too small to hold a room, which is
 * the DungeonGenerator's rule rather than this one.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class GameWorld
{
    // How far the player can see, in cells. Large enough to take in a room, small enough that a
    // corridor stays dark ahead of you.
    private const int PlayerSightRadius = 8;

    // Everything standing in the dungeon, in draw order: later entries cover earlier ones.
    private readonly List<Entity> _entities;

    /// <summary>The dungeon floor.</summary>
    public GameMap Map { get; }

    /// <summary>What the player can see now and what they remember.</summary>
    public VisibilityMap Visibility { get; }

    /// <summary>The entity the keyboard drives. Always present in Entities.</summary>
    public Entity Player { get; }

    /// <summary>Everything standing in the dungeon, the player included.</summary>
    public IReadOnlyList<Entity> Entities => _entities;

    /// <summary>What has happened lately, drawn under the map.</summary>
    public MessageLog Log { get; } = new MessageLog(capacity: 100);

    /// <summary>
    /// What the player is doing, which decides what their keys mean. Held here rather than on
    /// the screen class, so a test can open the pack and press a letter without a window.
    /// </summary>
    public GameMode Mode { get; private set; } = GameMode.Playing;

    /// <summary>
    /// True once the player has been killed. Nothing stops the game yet; Part 10 decides what
    /// happens next, and until then the player simply stops being able to act.
    /// </summary>
    public bool IsPlayerDead => Player.Fighter is null;

    /// <summary>
    /// Builds a world directly from its parts. Generate is the usual way in; this constructor
    /// exists so a test can hand-build a small world with exactly the monsters it cares about.
    /// Throws ArgumentNullException on a null argument, and ArgumentException when the player is
    /// not one of the entities - it must be drawn and moved like any other - or has no Fighter,
    /// since a player who cannot fight would read as already dead.
    /// </summary>
    public GameWorld(GameMap map, IReadOnlyList<Entity> entities, Entity player)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(player);

        // A player outside the entity list would be invisible and would not block anything.
        if (!entities.Contains(player))
        {
            throw new ArgumentException("The player must be one of the entities.", nameof(player));
        }

        // IsPlayerDead reads the Fighter being gone as death, so a player who never had one
        // would start the game already dead. Requiring it here keeps that reading honest.
        if (player.Fighter is null)
        {
            throw new ArgumentException("The player must have a Fighter.", nameof(player));
        }

        Map = map;
        Player = player;
        _entities = entities.ToList();

        Visibility = new VisibilityMap(map.Width, map.Height);

        // Sight is computed before anything is drawn, or the first frame would be blank.
        RecomputeFieldOfView();
    }

    /// <summary>
    /// Generates a dungeon, places the player in the first room and monsters in the rest, and
    /// returns the world that results. Every random choice is drawn from the supplied Random, so
    /// one seed reproduces the whole world - dungeon and monsters alike. Throws
    /// ArgumentNullException on a null argument.
    /// </summary>
    public static GameWorld Generate(
        int width, int height, Random random, MonsterTable monsters, ItemTable items)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(items);

        DungeonSettings settings = new DungeonSettings(maximumRooms: 30, minimumRoomSize: 6, maximumRoomSize: 10);

        GeneratedDungeon dungeon = new DungeonGenerator(settings).Generate(width, height, random);

        Entity player = new Entity("Player", '@', Color.White, dungeon.PlayerStart, blocksMovement: true);

        // The player's numbers: enough health to survive a mistake, enough defence that a rat
        // is an inconvenience rather than a threat.
        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2);

        // Twenty-six slots, because items are chosen by letter and there are twenty-six letters.
        player.Inventory = new Inventory(capacity: 26);

        List<Entity> entities = new List<Entity> { player };

        // The first room is where the player starts, so it is left empty: waking up already
        // surrounded is not a fair opening.
        for (int roomIndex = 1; roomIndex < dungeon.Rooms.Count; roomIndex++)
        {
            entities.AddRange(monsters.PopulateRoom(dungeon.Rooms[roomIndex], dungeon.Map, random));

            entities.AddRange(items.PopulateRoom(dungeon.Rooms[roomIndex], dungeon.Map, random));
        }

        // The player is drawn last so it covers anything sharing its cell.
        entities.Remove(player);
        entities.Add(player);

        return new GameWorld(dungeon.Map, entities, player);
    }

    /// <summary>
    /// The entity blocking the given cell, or null when nothing does. Items lying on the floor
    /// are not blockers and are never returned here.
    /// </summary>
    public Entity? BlockingEntityAt(Point position)
    {
        foreach (Entity entity in _entities)
        {
            if (entity.BlocksMovement && entity.Position == position)
            {
                return entity;
            }
        }

        return null;
    }

    /// <summary>
    /// Moves the player by the offset and reports what happened. A step onto open floor moves
    /// them and recomputes sight; walking into a creature is a bump, which will become an attack
    /// in Part 6 and for now simply does not move them; a wall refuses the move outright.
    /// </summary>
    public PlayerAction MovePlayer(Point offset)
    {
        // A zero offset is not a turn: no key that means "wait" exists yet.
        if (offset == Point.Zero)
        {
            return PlayerAction.None;
        }

        // A dead player has no turns left to take.
        if (IsPlayerDead)
        {
            return PlayerAction.None;
        }

        Point destination = Player.Position + offset;

        // The map decides first. Bumping a monster standing inside a wall is not a thing.
        if (!Map.IsWalkable(destination))
        {
            return PlayerAction.BlockedByWall;
        }

        // Walking into a creature is the attack command; there is no separate key for it.
        Entity? blocker = BlockingEntityAt(destination);
        if (blocker is not null)
        {
            // Something that blocks but cannot fight - a future statue, say - is simply in the
            // way, and swinging at it would produce a message about hitting furniture.
            if (blocker.Fighter is null)
            {
                return PlayerAction.BumpedInto(blocker);
            }

            Log.Add(Combat.Resolve(Player, blocker).Message);

            // Attacking spends the turn, so the monsters get theirs.
            RunMonsterTurns();

            return PlayerAction.Attacked(blocker);
        }

        Player.MoveTo(destination);

        // Sight is recomputed from the new position before anything is drawn, or the player
        // would see one frame of the view from where they used to stand.
        RecomputeFieldOfView();

        // Moving spends the turn too. Everything the monsters do happens after the player acts.
        RunMonsterTurns();

        return PlayerAction.Moved;
    }

    /// <summary>
    /// Gives every living monster one turn, in the order they appear in the entity list. The
    /// list is snapshotted first because a monster may die during the round, and dead ones are
    /// skipped rather than removed.
    /// </summary>
    private void RunMonsterTurns()
    {
        // A dead player takes no more turns, and neither should anything else - the game is over
        // in every sense that matters until Part 10 says what happens next.
        if (IsPlayerDead)
        {
            return;
        }

        // Snapshotting is what makes it safe for a monster to die mid-round: Die converts an
        // entity in place, and this loop must not care.
        foreach (Entity entity in _entities.ToList())
        {
            // The player is not a monster, and a corpse does not act.
            if (entity == Player || entity.Fighter is null)
            {
                continue;
            }

            string? message = MonsterTurn.Act(entity, this);

            if (message is not null)
            {
                Log.Add(message);
            }

            // The player dying ends the round immediately rather than letting the rest pile on.
            if (IsPlayerDead)
            {
                Log.Add("You die.");
                return;
            }
        }
    }

    /// <summary>
    /// Opens or closes the pack. Costs no turn: looking at what you are carrying is not an
    /// action, and monsters do not get a move while a menu is open.
    /// </summary>
    public void SetMode(GameMode mode)
    {
        Mode = mode;
    }

    /// <summary>
    /// Picks up whatever item is lying on the player's cell. Reports what happened through the
    /// log: there may be nothing there, or the pack may be full, and both are ordinary outcomes
    /// rather than errors. Picking something up spends a turn; finding nothing does not.
    /// </summary>
    public bool PickUpHere()
    {
        if (IsPlayerDead || Player.Inventory is null)
        {
            return false;
        }

        // The first item on this cell, ignoring creatures and the player themselves.
        Entity? item = _entities.FirstOrDefault(
            entity => entity != Player && entity.Consumable is not null && entity.Position == Player.Position);

        if (item is null)
        {
            Log.Add("There is nothing here to pick up.");
            return false;
        }

        if (!Player.Inventory.TryAdd(item))
        {
            Log.Add("Your pack is full.");
            return false;
        }

        // Carried items leave the map, so they stop being drawn and stop being picked up twice.
        _entities.Remove(item);

        Log.Add($"You pick up the {item.Name}.");

        RunMonsterTurns();

        return true;
    }

    /// <summary>
    /// Uses whatever is in the given slot. An empty slot is a miss rather than an error - the
    /// player pressed a letter for something they are not carrying. An item that would do nothing
    /// is not consumed and no turn is spent.
    /// </summary>
    public bool UseItem(int slot)
    {
        if (IsPlayerDead || Player.Inventory is null)
        {
            return false;
        }

        Entity? item = Player.Inventory.At(slot);

        if (item?.Consumable is null)
        {
            return false;
        }

        UseResult result = item.Consumable.UseOn(Player);

        Log.Add(result.Message);

        // An item that changed nothing stays in the pack, and the turn is not spent either.
        if (!result.Consumed)
        {
            return false;
        }

        Player.Inventory.Remove(item);

        RunMonsterTurns();

        return true;
    }

    /// <summary>
    /// Drops whatever is in the given slot onto the player's cell. An empty slot is a miss.
    /// Dropping spends a turn, which is what makes a full pack a real decision in a fight.
    /// </summary>
    public bool DropItem(int slot)
    {
        if (IsPlayerDead || Player.Inventory is null)
        {
            return false;
        }

        Entity? item = Player.Inventory.At(slot);

        if (item is null)
        {
            return false;
        }

        Player.Inventory.Remove(item);

        // Back onto the map, where the player stands, so it can be picked up again.
        item.MoveTo(Player.Position);

        // Items are drawn under creatures, so it goes at the front of the list.
        _entities.Insert(0, item);

        Log.Add($"You drop the {item.Name}.");

        RunMonsterTurns();

        return true;
    }

    /// <summary>
    /// Builds the picture the player currently perceives: lit where they can see, dim where they
    /// only remember, blank where they have never been.
    /// </summary>
    public RenderedFrame ComposeFrame()
    {
        return FrameComposer.Compose(Map, _entities, Visibility);
    }

    // Works out what the player can see from where they now stand, and folds it into memory.
    private void RecomputeFieldOfView()
    {
        Visibility.Update(FieldOfView.From(Player.Position, PlayerSightRadius, Map));

        // The player standing somewhere they cannot see would mean sight itself is broken.
        Debug.Assert(
            Visibility.StateAt(Player.Position) == CellVisibility.Visible,
            "The player must always be able to see their own cell.");
    }
}
```

### [`RogueTutorial/ScreenComposer.cs`](../parts/part-08-items-and-inventory/RogueTutorial/ScreenComposer.cs)

The Part 7 file, with the inventory overlay.

<!-- generated-diff -->
**Changed from Part 7.** The complete file follows; this is only what moved:

```diff
--- part-07-log-and-health-bar/ScreenComposer.cs
+++ current/ScreenComposer.cs
@@ -28,6 +28,10 @@
 
 internal static class ScreenComposer
 {
+    // How wide the inventory overlay is. Wide enough for the longest item name plus its letter,
+    // narrow enough to leave the dungeon visible beside it.
+    private const int InventoryWidth = 40;
+
     // How wide the health bar is drawn, caption included. Fixed rather than the window's width:
     // a bar stretched across eighty columns reads as a wall rather than as a gauge, and the rest
     // of the row is where a dungeon level and other status go in a later part.
@@ -79,6 +83,13 @@
         WriteStatusRow(world, layout, glyphs, foregrounds);
         WriteLog(world, layout, glyphs, foregrounds);
 
+        // The pack is drawn over the map rather than beside it. ScreenLayout divides the window
+        // permanently; a panel that comes and goes is a different thing and would fight that.
+        if (world.Mode == GameMode.ShowingInventory)
+        {
+            WriteInventory(world, layout, glyphs, foregrounds);
+        }
+
         return new RenderedFrame(layout.WindowWidth, layout.WindowHeight, glyphs, foregrounds);
     }
 
@@ -142,6 +153,56 @@
         }
     }
 
+    // Draws the pack over the top left of the map, one item per row, lettered from 'a'.
+    private static void WriteInventory(GameWorld world, ScreenLayout layout, char[] glyphs, Color[] foregrounds)
+    {
+        Inventory? pack = world.Player.Inventory;
+
+        List<string> contents = new List<string>();
+
+        if (pack is null || pack.Items.Count == 0)
+        {
+            contents.Add("nothing carried");
+        }
+        else
+        {
+            for (int slot = 0; slot < pack.Items.Count; slot++)
+            {
+                // 'a' is the first slot, matching what CommandReader turns a letter into.
+                contents.Add($"{(char)('a' + slot)}) {pack.Items[slot].Name}");
+            }
+        }
+
+        int width = Math.Min(InventoryWidth, layout.WindowWidth);
+
+        // A frame is what makes this read as a panel rather than as text pasted over the map.
+        // Plain ASCII rather than box-drawing glyphs, so it does not depend on the font.
+        int inner = width - 4;
+
+        List<string> lines = new List<string>
+        {
+            "+" + new string('-', width - 2) + "+",
+            "| " + "Pack".PadRight(inner) + " |",
+            "| " + "Esc closes, Shift drops".PadRight(inner) + " |",
+            "+" + new string('-', width - 2) + "+",
+        };
+
+        foreach (string entry in contents)
+        {
+            // A name longer than the panel is cut rather than wrapped, so the frame stays square.
+            string fitted = entry.Length > inner ? entry.Substring(0, inner) : entry;
+
+            lines.Add("| " + fitted.PadRight(inner) + " |");
+        }
+
+        lines.Add("+" + new string('-', width - 2) + "+");
+
+        for (int line = 0; line < lines.Count && line < layout.MapHeight; line++)
+        {
+            WriteLine(lines[line], line * layout.WindowWidth, layout.WindowWidth, glyphs, foregrounds, PanelText);
+        }
+    }
+
     // Draws the newest log lines, oldest at the top so the newest appears at the bottom.
     private static void WriteLog(GameWorld world, ScreenLayout layout, char[] glyphs, Color[] foregrounds)
     {
```
<!-- generated-diff -->

```csharp
/*
 * The whole screen as data: the dungeon on top, the health bar under it, the message log below
 * that.
 *
 * Part 2 made the map assertable by building it as a RenderedFrame before drawing it. This does
 * the same for the interface, and for the same reason: a health bar that reads the wrong numbers
 * or a log showing the wrong lines is a defect nothing else can see.
 *
 * Usage:
 *
 *     ScreenLayout layout = new ScreenLayout(80, 25, logRows: 5);
 *     RenderedFrame screen = ScreenComposer.Compose(world, layout);
 *
 *     string picture = screen.ToText();          // the map, then the panel, as lines
 *     char here = screen.GlyphAt(new Point(0, 20));
 *
 * The map is composed exactly as before and copied into the top of the frame, so nothing about
 * how the dungeon is drawn changes. Refuses a null argument, and a world whose map is taller
 * than the layout's map area.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using SadRogue.Primitives;

namespace RogueTutorial;

internal static class ScreenComposer
{
    // How wide the inventory overlay is. Wide enough for the longest item name plus its letter,
    // narrow enough to leave the dungeon visible beside it.
    private const int InventoryWidth = 40;

    // How wide the health bar is drawn, caption included. Fixed rather than the window's width:
    // a bar stretched across eighty columns reads as a wall rather than as a gauge, and the rest
    // of the row is where a dungeon level and other status go in a later part.
    private const int HealthBarWidth = 24;

    // Colour of the log text and the health bar caption.
    private static readonly Color PanelText = new Color(200, 200, 200);

    // Colour of the filled part of the health bar. Red, because it is health.
    private static readonly Color HealthFilled = new Color(190, 60, 60);

    // Colour of the lost part. Dark enough to read as absence rather than as more bar.
    private static readonly Color HealthEmpty = new Color(70, 30, 30);

    /// <summary>
    /// Builds the whole screen: the world's map and entities in the map area, the player's health
    /// on the status row, and the newest log lines below. Throws ArgumentNullException on a null
    /// argument and ArgumentException when the map does not fit the layout's map area.
    /// </summary>
    public static RenderedFrame Compose(GameWorld world, ScreenLayout layout)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(layout);

        // A mismatch here means the world was generated against a different layout, and the map
        // would be silently cropped rather than obviously wrong.
        if (world.Map.Height > layout.MapHeight || world.Map.Width > layout.WindowWidth)
        {
            throw new ArgumentException(
                $"A {world.Map.Width}x{world.Map.Height} map does not fit a "
                    + $"{layout.WindowWidth}x{layout.MapHeight} map area.",
                nameof(world));
        }

        int cells = layout.WindowWidth * layout.WindowHeight;

        char[] glyphs = new char[cells];
        Color[] foregrounds = new Color[cells];

        // Everything starts blank, so any region not written below reads as empty rather than
        // as whatever happened to be in memory.
        for (int index = 0; index < cells; index++)
        {
            glyphs[index] = ' ';
            foregrounds[index] = Color.Black;
        }

        CopyMapInto(world, layout, glyphs, foregrounds);
        WriteStatusRow(world, layout, glyphs, foregrounds);
        WriteLog(world, layout, glyphs, foregrounds);

        // The pack is drawn over the map rather than beside it. ScreenLayout divides the window
        // permanently; a panel that comes and goes is a different thing and would fight that.
        if (world.Mode == GameMode.ShowingInventory)
        {
            WriteInventory(world, layout, glyphs, foregrounds);
        }

        return new RenderedFrame(layout.WindowWidth, layout.WindowHeight, glyphs, foregrounds);
    }

    // Draws the dungeon exactly as earlier parts did, into the top of the screen.
    private static void CopyMapInto(GameWorld world, ScreenLayout layout, char[] glyphs, Color[] foregrounds)
    {
        RenderedFrame map = world.ComposeFrame();

        for (int row = 0; row < map.Height; row++)
        {
            for (int col = 0; col < map.Width; col++)
            {
                Point cell = new Point(col, row);

                int index = (row * layout.WindowWidth) + col;

                glyphs[index] = map.GlyphAt(cell);
                foregrounds[index] = map.ForegroundAt(cell);
            }
        }
    }

    // Draws the health bar, or a death notice once the player has none left.
    private static void WriteStatusRow(GameWorld world, ScreenLayout layout, char[] glyphs, Color[] foregrounds)
    {
        int rowStart = layout.StatusRow * layout.WindowWidth;

        // A dead player has no Fighter to read numbers from, so the row says so instead.
        if (world.Player.Fighter is null)
        {
            WriteLine("You are dead.", rowStart, layout.WindowWidth, glyphs, foregrounds, HealthFilled);
            return;
        }

        Fighter fighter = world.Player.Fighter;

        // A narrow window gets whatever it has; a wide one gets the fixed width rather than
        // a bar stretched to fill it.
        int barWidth = Math.Min(HealthBarWidth, layout.WindowWidth);

        string bar = HealthBar.Render(fighter.HitPoints, fighter.MaximumHitPoints, barWidth);

        // The caption is written in the panel colour and the bar itself in health colours, so
        // the numbers stay readable against a nearly empty bar.
        int captionLength = $"HP: {fighter.HitPoints}/{fighter.MaximumHitPoints} ".Length;

        for (int col = 0; col < bar.Length; col++)
        {
            int index = rowStart + col;

            glyphs[index] = bar[col];

            if (col < captionLength)
            {
                foregrounds[index] = PanelText;
            }
            else
            {
                foregrounds[index] = bar[col] == '=' ? HealthFilled : HealthEmpty;
            }
        }
    }

    // Draws the pack over the top left of the map, one item per row, lettered from 'a'.
    private static void WriteInventory(GameWorld world, ScreenLayout layout, char[] glyphs, Color[] foregrounds)
    {
        Inventory? pack = world.Player.Inventory;

        List<string> contents = new List<string>();

        if (pack is null || pack.Items.Count == 0)
        {
            contents.Add("nothing carried");
        }
        else
        {
            for (int slot = 0; slot < pack.Items.Count; slot++)
            {
                // 'a' is the first slot, matching what CommandReader turns a letter into.
                contents.Add($"{(char)('a' + slot)}) {pack.Items[slot].Name}");
            }
        }

        int width = Math.Min(InventoryWidth, layout.WindowWidth);

        // A frame is what makes this read as a panel rather than as text pasted over the map.
        // Plain ASCII rather than box-drawing glyphs, so it does not depend on the font.
        int inner = width - 4;

        List<string> lines = new List<string>
        {
            "+" + new string('-', width - 2) + "+",
            "| " + "Pack".PadRight(inner) + " |",
            "| " + "Esc closes, Shift drops".PadRight(inner) + " |",
            "+" + new string('-', width - 2) + "+",
        };

        foreach (string entry in contents)
        {
            // A name longer than the panel is cut rather than wrapped, so the frame stays square.
            string fitted = entry.Length > inner ? entry.Substring(0, inner) : entry;

            lines.Add("| " + fitted.PadRight(inner) + " |");
        }

        lines.Add("+" + new string('-', width - 2) + "+");

        for (int line = 0; line < lines.Count && line < layout.MapHeight; line++)
        {
            WriteLine(lines[line], line * layout.WindowWidth, layout.WindowWidth, glyphs, foregrounds, PanelText);
        }
    }

    // Draws the newest log lines, oldest at the top so the newest appears at the bottom.
    private static void WriteLog(GameWorld world, ScreenLayout layout, char[] glyphs, Color[] foregrounds)
    {
        IReadOnlyList<string> lines = world.Log.Latest(layout.LogRows);

        for (int line = 0; line < lines.Count; line++)
        {
            int row = layout.LogTopRow + line;

            Debug.Assert(row < layout.WindowHeight, "A log line must not be written past the window.");

            WriteLine(lines[line], row * layout.WindowWidth, layout.WindowWidth, glyphs, foregrounds, PanelText);
        }
    }

    // Writes one line into a row, truncated at the window's width rather than wrapping.
    private static void WriteLine(
        string text, int rowStart, int width, char[] glyphs, Color[] foregrounds, Color colour)
    {
        // Wrapping would push later lines down and change how many fit, so a long message is
        // cut instead. Part 8 gives the log room to wrap properly.
        int length = Math.Min(text.Length, width);

        for (int col = 0; col < length; col++)
        {
            glyphs[rowStart + col] = text[col];
            foregrounds[rowStart + col] = colour;
        }
    }
}
```

### [`RogueTutorial/RootScreen.cs`](../parts/part-08-items-and-inventory/RogueTutorial/RootScreen.cs)

The Part 7 file, now routing every key through CommandReader.

<!-- generated-diff -->
**Changed from Part 7.** The complete file follows; this is only what moved:

```diff
--- part-07-log-and-health-bar/RootScreen.cs
+++ current/RootScreen.cs
@@ -56,7 +56,7 @@
         // No seed is given, so every run is a different dungeon with different monsters. Pass a
         // number to Random's constructor to play the same one repeatedly while debugging.
         _world = GameWorld.Generate(
-            _layout.WindowWidth, _layout.MapHeight, new Random(), MonsterTable.Standard);
+            _layout.WindowWidth, _layout.MapHeight, new Random(), MonsterTable.Standard, ItemTable.Standard);
 
         DrawFrame();
     }
@@ -68,27 +68,61 @@
     /// </summary>
     public override bool ProcessKeyboard(Keyboard keyboard)
     {
-        // Reduce SadConsole's key objects to the bare enum the movement table expects.
+        // Reduce SadConsole's key objects to the bare enum the command reader expects.
         IReadOnlyCollection<Keys> pressedKeys = keyboard.KeysPressed.Select(pressed => pressed.Key).ToArray();
 
-        Point moveOffset = MovementKeys.OffsetFor(pressedKeys);
+        bool shiftHeld = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
 
-        // No movement key was down, so leave the frame alone and let others see the input.
-        if (moveOffset == Point.Zero)
+        // What a key means depends on what the player is doing, and the world knows which.
+        GameCommand command = CommandReader.Read(pressedKeys, _world.Mode, shiftHeld);
+
+        // A key with no meaning in this mode is not consumed, so anything else may see it.
+        if (command.Kind == GameCommandKind.None)
         {
             return false;
         }
 
-        PlayerAction action = _world.MovePlayer(moveOffset);
+        Apply(command);
 
-        // Anything that spends a turn changes the picture: a move redraws the map, and an
-        // attack changes health and adds to the log. A wall changes neither.
-        if (action.Kind == PlayerActionKind.Moved || action.Kind == PlayerActionKind.Attacked)
-        {
-            DrawFrame();
-        }
+        // Every command that reaches here changed the screen: the map moved, the log gained a
+        // line, or the pack opened or closed.
+        DrawFrame();
 
         return true;
+    }
+
+    /// <summary>
+    /// Hands one command to the world. Nothing is decided here - the world knows whether a slot
+    /// holds anything and whether a move is legal, and this only routes.
+    /// </summary>
+    private void Apply(GameCommand command)
+    {
+        switch (command.Kind)
+        {
+            case GameCommandKind.Move:
+                _world.MovePlayer(command.Offset);
+                break;
+
+            case GameCommandKind.PickUp:
+                _world.PickUpHere();
+                break;
+
+            case GameCommandKind.OpenInventory:
+                _world.SetMode(GameMode.ShowingInventory);
+                break;
+
+            case GameCommandKind.CloseInventory:
+                _world.SetMode(GameMode.Playing);
+                break;
+
+            case GameCommandKind.UseItem:
+                _world.UseItem(command.Slot);
+                break;
+
+            case GameCommandKind.DropItem:
+                _world.DropItem(command.Slot);
+                break;
+        }
     }
 
     /// <summary>
```
<!-- generated-diff -->

```csharp
/*
 * The top-level screen: it wires SadConsole's window and keyboard to the game world, and blits
 * the frame ScreenComposer builds - which from Part 7 is the whole screen, interface included,
 * rather than just the map. It owns no rules and, from Part 5, no state either - the map,
 * the entities and what the player has seen all live on GameWorld, which can be built and
 * driven in a test process.
 *
 * Usage - SadConsole constructs this itself, because Program.cs named it as the starting
 * screen, so it needs a public parameterless constructor:
 *
 *     new Builder().SetStartingScreen<RootScreen>()
 *
 * Constructing it in a test process throws: the constructor reads Game.Instance for the grid
 * size, and that requires a live graphics host. Test GameWorld instead.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class RootScreen : ScreenObject
{
    // How many rows of message log are shown. Five is enough to follow a fight without taking
    // so much of the window that the dungeon becomes cramped.
    private const int LogRows = 5;

    // The surface every glyph is drawn onto. One cell per grid position.
    private readonly ScreenSurface _mapSurface;

    // Where the map, the health bar and the log each sit in the window.
    private readonly ScreenLayout _layout;

    // The dungeon, everyone standing in it, and what the player has seen.
    private readonly GameWorld _world;

    /// <summary>
    /// Sizes the surface to the window, generates a world to fill it, and paints the first frame.
    /// </summary>
    public RootScreen()
    {
        // Match the surface to the window so no part of the grid is off screen.
        _mapSurface = new ScreenSurface(Game.Instance.ScreenCellsX, Game.Instance.ScreenCellsY);

        // Children are drawn and updated by the base class once added.
        Children.Add(_mapSurface);

        _layout = new ScreenLayout(
            _mapSurface.Surface.Width, _mapSurface.Surface.Height, logRows: LogRows);

        // The dungeon fills the map area rather than the window: the panel takes the rest.
        // No seed is given, so every run is a different dungeon with different monsters. Pass a
        // number to Random's constructor to play the same one repeatedly while debugging.
        _world = GameWorld.Generate(
            _layout.WindowWidth, _layout.MapHeight, new Random(), MonsterTable.Standard, ItemTable.Standard);

        DrawFrame();
    }

    /// <summary>
    /// Turns the keys held this frame into one move. Returns true whenever a movement key was
    /// pressed, even when a wall or a monster refused the move: the key was considered and
    /// answered, and reporting otherwise would offer it to another screen as unhandled.
    /// </summary>
    public override bool ProcessKeyboard(Keyboard keyboard)
    {
        // Reduce SadConsole's key objects to the bare enum the command reader expects.
        IReadOnlyCollection<Keys> pressedKeys = keyboard.KeysPressed.Select(pressed => pressed.Key).ToArray();

        bool shiftHeld = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

        // What a key means depends on what the player is doing, and the world knows which.
        GameCommand command = CommandReader.Read(pressedKeys, _world.Mode, shiftHeld);

        // A key with no meaning in this mode is not consumed, so anything else may see it.
        if (command.Kind == GameCommandKind.None)
        {
            return false;
        }

        Apply(command);

        // Every command that reaches here changed the screen: the map moved, the log gained a
        // line, or the pack opened or closed.
        DrawFrame();

        return true;
    }

    /// <summary>
    /// Hands one command to the world. Nothing is decided here - the world knows whether a slot
    /// holds anything and whether a move is legal, and this only routes.
    /// </summary>
    private void Apply(GameCommand command)
    {
        switch (command.Kind)
        {
            case GameCommandKind.Move:
                _world.MovePlayer(command.Offset);
                break;

            case GameCommandKind.PickUp:
                _world.PickUpHere();
                break;

            case GameCommandKind.OpenInventory:
                _world.SetMode(GameMode.ShowingInventory);
                break;

            case GameCommandKind.CloseInventory:
                _world.SetMode(GameMode.Playing);
                break;

            case GameCommandKind.UseItem:
                _world.UseItem(command.Slot);
                break;

            case GameCommandKind.DropItem:
                _world.DropItem(command.Slot);
                break;
        }
    }

    /// <summary>
    /// Copies the world's composed frame onto the surface, one cell at a time. Everything drawn
    /// here was already decided by FrameComposer; this only moves it to the screen.
    /// </summary>
    private void DrawFrame()
    {
        RenderedFrame frame = ScreenComposer.Compose(_world, _layout);

        for (int row = 0; row < frame.Height; row++)
        {
            for (int col = 0; col < frame.Width; col++)
            {
                Point cell = new Point(col, row);

                _mapSurface.Surface.SetGlyph(col, row, frame.GlyphAt(cell), frame.ForegroundAt(cell));
            }
        }
    }
}
```

## Step 3: the test files

**Each block below is the complete file.** Two are new; `ScreenComposerTests.cs` and
`GameWorldTests.cs` are carried over and need updating.

### [`RogueTutorial.Tests/InventoryTests.cs`](../parts/part-08-items-and-inventory/RogueTutorial.Tests/InventoryTests.cs)

Capacity, ordering, and what an empty slot answers.

```csharp
/*
 * Unit tests for what an entity carries. The capacity is the point: an unbounded pack removes
 * every decision about what to leave behind.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~InventoryTests
 */

using System;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class InventoryTests
{
    private static Entity Item(string name)
    {
        Entity item = new Entity(name, '!', Color.Magenta, new Point(0, 0), blocksMovement: false);
        item.Consumable = new Consumable(ConsumableKind.Healing, power: 4);
        return item;
    }

    [Fact]
    public void ANewPackIsEmpty()
    {
        Inventory pack = new Inventory(capacity: 5);

        Assert.Empty(pack.Items);
        Assert.False(pack.IsFull);
    }

    [Fact]
    public void AnItemAddedIsCarried()
    {
        Inventory pack = new Inventory(5);
        Entity potion = Item("potion");

        Assert.True(pack.TryAdd(potion));
        Assert.Contains(potion, pack.Items);
    }

    [Fact]
    public void ItemsKeepThePickUpOrder()
    {
        // The order is what the player sees as slots, so it must not be sorted underneath them.
        Inventory pack = new Inventory(5);

        Entity first = Item("first");
        Entity second = Item("second");

        pack.TryAdd(first);
        pack.TryAdd(second);

        Assert.Same(first, pack.At(0));
        Assert.Same(second, pack.At(1));
    }

    [Fact]
    public void AFullPackRefusesRatherThanThrows()
    {
        // Running out of room is an ordinary thing that happens to a player, not a bug.
        Inventory pack = new Inventory(capacity: 2);

        Assert.True(pack.TryAdd(Item("one")));
        Assert.True(pack.TryAdd(Item("two")));
        Assert.False(pack.TryAdd(Item("three")));

        Assert.Equal(2, pack.Items.Count);
        Assert.True(pack.IsFull);
    }

    [Fact]
    public void RemovingMakesRoomAgain()
    {
        Inventory pack = new Inventory(capacity: 1);
        Entity potion = Item("potion");

        pack.TryAdd(potion);
        Assert.True(pack.IsFull);

        pack.Remove(potion);

        Assert.False(pack.IsFull);
        Assert.True(pack.TryAdd(Item("another")));
    }

    [Fact]
    public void AnEmptySlotAnswersNullRatherThanThrowing()
    {
        // A keypress is checked against the pack directly: pressing 'd' with two items carried
        // is a miss, not an error.
        Inventory pack = new Inventory(5);

        pack.TryAdd(Item("only"));

        Assert.Null(pack.At(1));
        Assert.Null(pack.At(25));
        Assert.Null(pack.At(-1));
    }

    [Fact]
    public void TheSameItemCannotBeCarriedTwice()
    {
        // Two slots holding one entity would let it be dropped twice and used twice.
        Inventory pack = new Inventory(5);
        Entity potion = Item("potion");

        pack.TryAdd(potion);

        Assert.Throws<ArgumentException>(() => pack.TryAdd(potion));
    }

    [Fact]
    public void RemovingSomethingNotCarriedIsRejected()
    {
        Inventory pack = new Inventory(5);

        Assert.Throws<ArgumentException>(() => pack.Remove(Item("never added")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void APackThatHoldsNothingIsRejected(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Inventory(capacity));
    }

    [Fact]
    public void ANullItemIsRejected()
    {
        Inventory pack = new Inventory(5);

        Assert.Throws<ArgumentNullException>(() => pack.TryAdd(null!));
        Assert.Throws<ArgumentNullException>(() => pack.Remove(null!));
    }
}
```

### [`RogueTutorial.Tests/ItemUseTests.cs`](../parts/part-08-items-and-inventory/RogueTutorial.Tests/ItemUseTests.cs)

Using, picking up, dropping, and what each key means in each mode.

```csharp
/*
 * Unit tests for using items, and for the command reader that decides what a key means.
 *
 * The rule worth watching is that an item which would change nothing is not consumed: drinking
 * a healing potion at full health must waste the keypress rather than the potion.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~ItemUseTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadConsole.Input;
using SadRogue.Primitives;
using Xunit;

public sealed class ItemUseTests
{
    // An open room with the player in the middle and whatever else the test needs on the floor.
    private static GameWorld WorldWith(params Entity[] onTheFloor)
    {
        GameMap map = new GameMap(9, 9);
        map.Fill(TileTypes.Floor);

        Entity player = new Entity("Player", '@', Color.White, new Point(4, 4), blocksMovement: true);
        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2);
        player.Inventory = new Inventory(capacity: 26);

        List<Entity> entities = new List<Entity>(onTheFloor) { player };

        return new GameWorld(map, entities, player);
    }

    private static Entity Potion(Point at, int power)
    {
        Entity potion = new Entity("healing potion", '!', Color.Magenta, at, blocksMovement: false);
        potion.Consumable = new Consumable(ConsumableKind.Healing, power);
        return potion;
    }

    [Fact]
    public void HealingRestoresHitPoints()
    {
        Fighter fighter = new Fighter(30, 5, 2);
        fighter.TakeDamage(10);

        Assert.Equal(6, fighter.Heal(6));
        Assert.Equal(26, fighter.HitPoints);
    }

    [Fact]
    public void HealingCannotPassTheMaximum()
    {
        Fighter fighter = new Fighter(30, 5, 2);
        fighter.TakeDamage(4);

        // Only the missing four can be restored, whatever the potion promises.
        Assert.Equal(4, fighter.Heal(99));
        Assert.Equal(30, fighter.HitPoints);
    }

    [Fact]
    public void HealingAtFullHealthRecoversNothing()
    {
        Fighter fighter = new Fighter(30, 5, 2);

        Assert.Equal(0, fighter.Heal(10));
    }

    [Fact]
    public void APotionAtFullHealthIsNotWasted()
    {
        // The rule this part exists to get right: a wasted turn must not also be a wasted item.
        GameWorld world = WorldWith();
        Entity potion = Potion(world.Player.Position, power: 8);

        world.Player.Inventory!.TryAdd(potion);

        bool spentATurn = world.UseItem(0);

        Assert.False(spentATurn);
        Assert.Contains(potion, world.Player.Inventory.Items);
        Assert.Contains("already at full health", world.Log.Messages.Last());
    }

    [Fact]
    public void ADrunkPotionLeavesThePack()
    {
        GameWorld world = WorldWith();
        world.Player.Fighter!.TakeDamage(10);

        Entity potion = Potion(world.Player.Position, power: 8);
        world.Player.Inventory!.TryAdd(potion);

        Assert.True(world.UseItem(0));

        Assert.Empty(world.Player.Inventory.Items);
        Assert.Equal(28, world.Player.Fighter.HitPoints);
    }

    [Fact]
    public void UsingAnEmptySlotIsAMissRatherThanAnError()
    {
        GameWorld world = WorldWith();

        Assert.False(world.UseItem(0));
        Assert.False(world.UseItem(25));
    }

    [Fact]
    public void PickingUpTakesTheItemOffTheMap()
    {
        GameWorld world = WorldWith();
        Entity potion = Potion(new Point(4, 4), power: 8);

        GameWorld withPotion = WorldWith(potion);

        Assert.True(withPotion.PickUpHere());

        Assert.Contains(potion, withPotion.Player.Inventory!.Items);
        Assert.DoesNotContain(potion, withPotion.Entities);
    }

    [Fact]
    public void PickingUpNothingSaysSo()
    {
        GameWorld world = WorldWith();

        Assert.False(world.PickUpHere());
        Assert.Contains("nothing here", world.Log.Messages.Last());
    }

    [Fact]
    public void AFullPackCannotPickUp()
    {
        Entity potion = Potion(new Point(4, 4), power: 8);
        GameWorld world = WorldWith(potion);

        // Fill the pack with something other than what is on the floor.
        world.Player.Inventory = new Inventory(capacity: 1);
        world.Player.Inventory.TryAdd(Potion(new Point(0, 0), power: 4));

        Assert.False(world.PickUpHere());
        Assert.Contains("pack is full", world.Log.Messages.Last());
        Assert.Contains(potion, world.Entities);
    }

    [Fact]
    public void DroppingPutsItBackOnTheMap()
    {
        GameWorld world = WorldWith();
        Entity potion = Potion(new Point(0, 0), power: 8);

        world.Player.Inventory!.TryAdd(potion);

        Assert.True(world.DropItem(0));

        Assert.Empty(world.Player.Inventory.Items);
        Assert.Contains(potion, world.Entities);
        Assert.Equal(world.Player.Position, potion.Position);
    }

    [Fact]
    public void ADroppedItemCanBePickedUpAgain()
    {
        GameWorld world = WorldWith();
        Entity potion = Potion(new Point(0, 0), power: 8);

        world.Player.Inventory!.TryAdd(potion);
        world.DropItem(0);

        Assert.True(world.PickUpHere());
        Assert.Contains(potion, world.Player.Inventory.Items);
    }

    [Fact]
    public void OpeningThePackCostsNoTurn()
    {
        // Looking at what you are carrying is not an action, and monsters must not get a move.
        GameWorld world = WorldWith();

        world.SetMode(GameMode.ShowingInventory);

        Assert.Equal(GameMode.ShowingInventory, world.Mode);
    }

    [Fact]
    public void MovementKeysMeanNothingWhileThePackIsOpen()
    {
        GameCommand command = CommandReader.Read(new[] { Keys.Left }, GameMode.ShowingInventory);

        Assert.Equal(GameCommandKind.None, command.Kind);
    }

    [Fact]
    public void LettersChooseSlotsWhileThePackIsOpen()
    {
        Assert.Equal(0, CommandReader.Read(new[] { Keys.A }, GameMode.ShowingInventory).Slot);
        Assert.Equal(3, CommandReader.Read(new[] { Keys.D }, GameMode.ShowingInventory).Slot);
        Assert.Equal(25, CommandReader.Read(new[] { Keys.Z }, GameMode.ShowingInventory).Slot);
    }

    [Fact]
    public void ShiftTurnsChoosingIntoDropping()
    {
        GameCommand use = CommandReader.Read(new[] { Keys.A }, GameMode.ShowingInventory, shiftHeld: false);
        GameCommand drop = CommandReader.Read(new[] { Keys.A }, GameMode.ShowingInventory, shiftHeld: true);

        Assert.Equal(GameCommandKind.UseItem, use.Kind);
        Assert.Equal(GameCommandKind.DropItem, drop.Kind);
        Assert.Equal(drop.Slot, use.Slot);
    }

    [Fact]
    public void LettersMeanNothingOnTheMap()
    {
        // 'd' is a slot in the pack and nothing at all while walking, which is the whole reason
        // the mode exists.
        Assert.Equal(GameCommandKind.None, CommandReader.Read(new[] { Keys.D }, GameMode.Playing).Kind);
    }

    [Fact]
    public void TheMapKeysStillWorkWhilePlaying()
    {
        Assert.Equal(GameCommandKind.Move, CommandReader.Read(new[] { Keys.Left }, GameMode.Playing).Kind);
        Assert.Equal(GameCommandKind.PickUp, CommandReader.Read(new[] { Keys.G }, GameMode.Playing).Kind);
        Assert.Equal(GameCommandKind.OpenInventory, CommandReader.Read(new[] { Keys.I }, GameMode.Playing).Kind);
    }

    [Fact]
    public void EscapeAndIBothCloseThePack()
    {
        Assert.Equal(GameCommandKind.CloseInventory,
            CommandReader.Read(new[] { Keys.Escape }, GameMode.ShowingInventory).Kind);

        Assert.Equal(GameCommandKind.CloseInventory,
            CommandReader.Read(new[] { Keys.I }, GameMode.ShowingInventory).Kind);
    }

    [Fact]
    public void ADeadPlayerCannotUseItems()
    {
        GameWorld world = WorldWith();
        world.Player.Fighter!.TakeDamage(30);
        world.Player.Die();

        Assert.False(world.PickUpHere());
        Assert.False(world.UseItem(0));
        Assert.False(world.DropItem(0));
    }

    [Fact]
    public void AnItemCannotBeUsedBySomethingWithNoFighter()
    {
        Entity item = Potion(new Point(0, 0), power: 4);
        Entity statue = new Entity("statue", 'S', Color.Gray, new Point(1, 1), blocksMovement: true);

        Assert.Throws<ArgumentException>(() => item.Consumable!.UseOn(statue));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AConsumableWithNoPowerIsRejected(int power)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Consumable(ConsumableKind.Healing, power));
    }

    [Fact]
    public void NegativeHealingIsRejected()
    {
        // Damage arriving through the healing door would be a very quiet bug.
        Fighter fighter = new Fighter(30, 5, 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => fighter.Heal(-1));
    }

    [Fact]
    public void ANullArgumentIsRejected()
    {
        Entity item = Potion(new Point(0, 0), power: 4);

        Assert.Throws<ArgumentNullException>(() => item.Consumable!.UseOn(null!));
        Assert.Throws<ArgumentNullException>(() => CommandReader.Read(null!, GameMode.Playing));
    }
}
```

### [`RogueTutorial.Tests/ScreenComposerTests.cs`](../parts/part-08-items-and-inventory/RogueTutorial.Tests/ScreenComposerTests.cs)

The Part 7 file, with the overlay tests added.

<!-- generated-diff -->
**Changed from Part 7.** The complete file follows; this is only what moved:

```diff
--- part-07-log-and-health-bar/ScreenComposerTests.cs
+++ current/ScreenComposerTests.cs
@@ -220,6 +220,71 @@
     }
 
     [Fact]
+    public void ThePackIsNotDrawnWhilePlaying()
+    {
+        ScreenLayout layout = new ScreenLayout(60, 12, logRows: 3);
+        GameWorld world = WorldFor(layout);
+
+        RenderedFrame frame = ScreenComposer.Compose(world, layout);
+
+        Assert.DoesNotContain("Pack", Row(frame, 0));
+    }
+
+    [Fact]
+    public void ThePackIsFramedSoItReadsAsAPanel()
+    {
+        // Without a frame the text sits on the map and reads as corruption rather than as an
+        // interface, which no other check can see.
+        ScreenLayout layout = new ScreenLayout(60, 12, logRows: 3);
+        GameWorld world = WorldFor(layout);
+
+        world.SetMode(GameMode.ShowingInventory);
+
+        RenderedFrame frame = ScreenComposer.Compose(world, layout);
+
+        Assert.StartsWith("+---", Row(frame, 0));
+        Assert.StartsWith("| Pack", Row(frame, 1));
+    }
+
+    [Fact]
+    public void AnEmptyPackSaysSoRatherThanShowingAnEmptyBox()
+    {
+        ScreenLayout layout = new ScreenLayout(60, 12, logRows: 3);
+        GameWorld world = WorldFor(layout);
+
+        world.SetMode(GameMode.ShowingInventory);
+
+        RenderedFrame frame = ScreenComposer.Compose(world, layout);
+
+        Assert.Contains("nothing carried", Row(frame, 4));
+    }
+
+    [Fact]
+    public void CarriedItemsAreLetteredFromA()
+    {
+        // The letters must match what CommandReader turns a key into, or pressing 'b' uses the
+        // wrong potion - which is the kind of bug a player blames on themselves.
+        ScreenLayout layout = new ScreenLayout(60, 12, logRows: 3);
+        GameWorld world = WorldFor(layout);
+
+        world.Player.Inventory = new Inventory(capacity: 26);
+
+        foreach (string name in new[] { "first potion", "second potion" })
+        {
+            Entity item = new Entity(name, '!', Color.Magenta, new Point(0, 0), blocksMovement: false);
+            item.Consumable = new Consumable(ConsumableKind.Healing, 4);
+            world.Player.Inventory.TryAdd(item);
+        }
+
+        world.SetMode(GameMode.ShowingInventory);
+
+        RenderedFrame frame = ScreenComposer.Compose(world, layout);
+
+        Assert.Contains("a) first potion", Row(frame, 4));
+        Assert.Contains("b) second potion", Row(frame, 5));
+    }
+
+    [Fact]
     public void ANullArgumentIsRejected()
     {
         ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);
```
<!-- generated-diff -->

```csharp
/*
 * Unit tests for the whole screen: the dungeon, the health bar and the log together.
 *
 * These are the tests Part 6 could not have. The log filled up and nobody could check what
 * reached the screen; composing the interface as data before drawing it makes a wrong health
 * bar or a missing log line an ordinary assertion.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~ScreenComposerTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class ScreenComposerTests
{
    // A small open room, sized to the layout's map area so the composer accepts it.
    private static GameWorld WorldFor(ScreenLayout layout)
    {
        GameMap map = new GameMap(layout.WindowWidth, layout.MapHeight);
        map.Fill(TileTypes.Floor);

        Entity player = new Entity("Player", '@', Color.White, new Point(2, 1), blocksMovement: true);
        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2);

        return new GameWorld(map, new List<Entity> { player }, player);
    }

    // The text of one row of the composed screen.
    private static string Row(RenderedFrame frame, int row)
    {
        return frame.ToText().Split('\n')[row];
    }

    [Fact]
    public void TheFrameIsTheSizeOfTheWindow()
    {
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);

        RenderedFrame frame = ScreenComposer.Compose(WorldFor(layout), layout);

        Assert.Equal(30, frame.Width);
        Assert.Equal(10, frame.Height);
    }

    [Fact]
    public void TheHealthBarIsOnTheStatusRow()
    {
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);
        GameWorld world = WorldFor(layout);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.StartsWith("HP: 30/30 ", Row(frame, layout.StatusRow));
    }

    [Fact]
    public void TheHealthBarFollowsDamage()
    {
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);
        GameWorld world = WorldFor(layout);

        world.Player.Fighter!.TakeDamage(6);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.StartsWith("HP: 24/30 ", Row(frame, layout.StatusRow));
    }

    [Fact]
    public void TheBarIsAGaugeRatherThanTheWholeRow()
    {
        // A bar stretched across the window reads as a wall rather than as a gauge, and it
        // leaves nowhere to put a dungeon level or anything else on the status row.
        ScreenLayout layout = new ScreenLayout(80, 25, logRows: 5);
        GameWorld world = WorldFor(layout);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        string statusRow = Row(frame, layout.StatusRow);

        Assert.Equal(24, statusRow.TrimEnd().Length);
        Assert.EndsWith("                                                        ", statusRow);
    }

    [Fact]
    public void ANarrowWindowGetsAShorterBarRatherThanAnError()
    {
        ScreenLayout layout = new ScreenLayout(16, 10, logRows: 3);
        GameWorld world = WorldFor(layout);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.Equal("HP: 30/30 ======", Row(frame, layout.StatusRow));
    }

    [Fact]
    public void ADeadPlayerGetsANoticeRatherThanABar()
    {
        // A corpse has no Fighter, so there are no numbers to read; the row must not be blank.
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);
        GameWorld world = WorldFor(layout);

        world.Player.Fighter!.TakeDamage(30);
        world.Player.Die();

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.StartsWith("You are dead.", Row(frame, layout.StatusRow));
    }

    [Fact]
    public void TheNewestLogLineIsAtTheBottom()
    {
        // Oldest first, so the log reads top to bottom and the newest message is nearest the
        // bottom of the screen where the eye already is.
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);
        GameWorld world = WorldFor(layout);

        world.Log.Add("first");
        world.Log.Add("second");
        world.Log.Add("third");

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.StartsWith("first", Row(frame, layout.LogTopRow));
        Assert.StartsWith("second", Row(frame, layout.LogTopRow + 1));
        Assert.StartsWith("third", Row(frame, layout.LogTopRow + 2));
    }

    [Fact]
    public void OnlyTheNewestLinesThatFitAreShown()
    {
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);
        GameWorld world = WorldFor(layout);

        foreach (string message in new[] { "one", "two", "three", "four", "five" })
        {
            world.Log.Add(message);
        }

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.StartsWith("three", Row(frame, layout.LogTopRow));
        Assert.StartsWith("five", Row(frame, layout.LogTopRow + 2));
    }

    [Fact]
    public void AnEmptyLogLeavesItsRowsBlank()
    {
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);

        RenderedFrame frame = ScreenComposer.Compose(WorldFor(layout), layout);

        for (int line = 0; line < layout.LogRows; line++)
        {
            Assert.Equal(new string(' ', 30), Row(frame, layout.LogTopRow + line));
        }
    }

    [Fact]
    public void TheMapIsDrawnInTheMapArea()
    {
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);
        GameWorld world = WorldFor(layout);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        // The player is at (2,1), which is inside the map area and so appears unchanged.
        Assert.Equal('@', frame.GlyphAt(new Point(2, 1)));
    }

    [Fact]
    public void TheMapNeverReachesTheStatusRow()
    {
        // The panel is not part of the dungeon: a map drawn one row too tall would overwrite
        // the health bar, and the bar would simply vanish with nothing else reporting it.
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);
        GameWorld world = WorldFor(layout);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.StartsWith("HP: ", Row(frame, layout.StatusRow));
    }

    [Fact]
    public void ALongMessageIsCutRatherThanWrapped()
    {
        // Wrapping would push later lines down and change how many fit, so the row count would
        // depend on the text. Part 8 gives the log room to wrap properly.
        ScreenLayout layout = new ScreenLayout(20, 10, logRows: 3);
        GameWorld world = WorldFor(layout);

        world.Log.Add("a message far longer than twenty columns");

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.Equal("a message far longer", Row(frame, layout.LogTopRow));
        Assert.Equal(new string(' ', 20), Row(frame, layout.LogTopRow + 1));
    }

    [Fact]
    public void AMapTooTallForTheLayoutIsRejected()
    {
        // A world generated against a different layout would be silently cropped otherwise.
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);

        GameMap tooTall = new GameMap(30, 9);
        tooTall.Fill(TileTypes.Floor);

        Entity player = new Entity("Player", '@', Color.White, new Point(1, 1), blocksMovement: true);
        player.Fighter = new Fighter(30, 5, 2);

        GameWorld world = new GameWorld(tooTall, new List<Entity> { player }, player);

        Assert.Throws<ArgumentException>(() => ScreenComposer.Compose(world, layout));
    }

    [Fact]
    public void ThePackIsNotDrawnWhilePlaying()
    {
        ScreenLayout layout = new ScreenLayout(60, 12, logRows: 3);
        GameWorld world = WorldFor(layout);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.DoesNotContain("Pack", Row(frame, 0));
    }

    [Fact]
    public void ThePackIsFramedSoItReadsAsAPanel()
    {
        // Without a frame the text sits on the map and reads as corruption rather than as an
        // interface, which no other check can see.
        ScreenLayout layout = new ScreenLayout(60, 12, logRows: 3);
        GameWorld world = WorldFor(layout);

        world.SetMode(GameMode.ShowingInventory);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.StartsWith("+---", Row(frame, 0));
        Assert.StartsWith("| Pack", Row(frame, 1));
    }

    [Fact]
    public void AnEmptyPackSaysSoRatherThanShowingAnEmptyBox()
    {
        ScreenLayout layout = new ScreenLayout(60, 12, logRows: 3);
        GameWorld world = WorldFor(layout);

        world.SetMode(GameMode.ShowingInventory);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.Contains("nothing carried", Row(frame, 4));
    }

    [Fact]
    public void CarriedItemsAreLetteredFromA()
    {
        // The letters must match what CommandReader turns a key into, or pressing 'b' uses the
        // wrong potion - which is the kind of bug a player blames on themselves.
        ScreenLayout layout = new ScreenLayout(60, 12, logRows: 3);
        GameWorld world = WorldFor(layout);

        world.Player.Inventory = new Inventory(capacity: 26);

        foreach (string name in new[] { "first potion", "second potion" })
        {
            Entity item = new Entity(name, '!', Color.Magenta, new Point(0, 0), blocksMovement: false);
            item.Consumable = new Consumable(ConsumableKind.Healing, 4);
            world.Player.Inventory.TryAdd(item);
        }

        world.SetMode(GameMode.ShowingInventory);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.Contains("a) first potion", Row(frame, 4));
        Assert.Contains("b) second potion", Row(frame, 5));
    }

    [Fact]
    public void ANullArgumentIsRejected()
    {
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);

        Assert.Throws<ArgumentNullException>(() => ScreenComposer.Compose(null!, layout));
        Assert.Throws<ArgumentNullException>(() => ScreenComposer.Compose(WorldFor(layout), null!));
    }
}
```

### [`RogueTutorial.Tests/GameWorldTests.cs`](../parts/part-08-items-and-inventory/RogueTutorial.Tests/GameWorldTests.cs)

The Part 7 file, updated for Generate's new argument.

<!-- generated-diff -->
**Changed from Part 7.** The complete file follows; this is only what moved:

```diff
--- part-07-log-and-health-bar/GameWorldTests.cs
+++ current/GameWorldTests.cs
@@ -171,8 +171,8 @@
     public void AGeneratedWorldIsReproducibleFromItsSeed()
     {
         // Monsters are drawn from the same Random as the dungeon, so one seed fixes both.
-        GameWorld first = GameWorld.Generate(40, 25, new Random(12345), MonsterTable.Standard);
-        GameWorld second = GameWorld.Generate(40, 25, new Random(12345), MonsterTable.Standard);
+        GameWorld first = GameWorld.Generate(40, 25, new Random(12345), MonsterTable.Standard, ItemTable.Standard);
+        GameWorld second = GameWorld.Generate(40, 25, new Random(12345), MonsterTable.Standard, ItemTable.Standard);
 
         Assert.Equal(first.ComposeFrame().ToText(), second.ComposeFrame().ToText());
 
@@ -186,7 +186,7 @@
     {
         for (int seed = 0; seed < 20; seed++)
         {
-            GameWorld world = GameWorld.Generate(40, 25, new Random(seed), MonsterTable.Standard);
+            GameWorld world = GameWorld.Generate(40, 25, new Random(seed), MonsterTable.Standard, ItemTable.Standard);
 
             foreach (Entity entity in world.Entities)
             {
@@ -202,7 +202,7 @@
     {
         for (int seed = 0; seed < 20; seed++)
         {
-            GameWorld world = GameWorld.Generate(40, 25, new Random(seed), MonsterTable.Standard);
+            GameWorld world = GameWorld.Generate(40, 25, new Random(seed), MonsterTable.Standard, ItemTable.Standard);
 
             List<Point> occupied = world.Entities
                 .Where(entity => entity.BlocksMovement)
@@ -219,7 +219,7 @@
         // The first room is left empty, so the opening move is never a forced fight.
         for (int seed = 0; seed < 20; seed++)
         {
-            GameWorld world = GameWorld.Generate(40, 25, new Random(seed), MonsterTable.Standard);
+            GameWorld world = GameWorld.Generate(40, 25, new Random(seed), MonsterTable.Standard, ItemTable.Standard);
 
             IEnumerable<Entity> others = world.Entities.Where(entity => entity != world.Player);
 
@@ -231,7 +231,7 @@
     public void AGeneratedWorldContainsMonsters()
     {
         // Weak on purpose: how many is random. That there are any at all is not.
-        GameWorld world = GameWorld.Generate(60, 35, new Random(7), MonsterTable.Standard);
+        GameWorld world = GameWorld.Generate(60, 35, new Random(7), MonsterTable.Standard, ItemTable.Standard);
 
         Assert.True(world.Entities.Count > 1, "a dungeon this size should hold at least one monster");
     }
@@ -256,7 +256,8 @@
         Assert.Throws<ArgumentNullException>(() => new GameWorld(null!, new[] { player }, player));
         Assert.Throws<ArgumentNullException>(() => new GameWorld(map, null!, player));
         Assert.Throws<ArgumentNullException>(() => new GameWorld(map, new[] { player }, null!));
-        Assert.Throws<ArgumentNullException>(() => GameWorld.Generate(40, 25, null!, MonsterTable.Standard));
-        Assert.Throws<ArgumentNullException>(() => GameWorld.Generate(40, 25, new Random(1), null!));
+        Assert.Throws<ArgumentNullException>(() => GameWorld.Generate(40, 25, null!, MonsterTable.Standard, ItemTable.Standard));
+        Assert.Throws<ArgumentNullException>(() => GameWorld.Generate(40, 25, new Random(1), null!, ItemTable.Standard));
+        Assert.Throws<ArgumentNullException>(() => GameWorld.Generate(40, 25, new Random(1), MonsterTable.Standard, null!));
     }
 }
```
<!-- generated-diff -->

```csharp
/*
 * Unit tests for the game world: who blocks whom, what a movement key does, and that a
 * generated world is reproducible from its seed.
 *
 * These are the tests Part 4 could not have written. The map, the entities and the visibility
 * lived on RootScreen, which needs a graphics host; moving them onto GameWorld is what makes
 * everything below reachable without a window.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~GameWorldTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class GameWorldTests
{
    // A small open room with the player in the middle, and whatever else a test wants.
    private static GameWorld WorldWith(params Entity[] extraEntities)
    {
        GameMap map = new GameMap(9, 9);
        map.Fill(TileTypes.Wall);

        for (int row = 1; row < 8; row++)
        {
            for (int col = 1; col < 8; col++)
            {
                map.SetTile(new Point(col, row), TileTypes.Floor);
            }
        }

        Entity player = new Entity("Player", '@', Color.White, new Point(4, 4), blocksMovement: true);
        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2);

        List<Entity> entities = new List<Entity>(extraEntities) { player };

        return new GameWorld(map, entities, player);
    }

    [Fact]
    public void TheWorldKnowsWhereThePlayerIs()
    {
        GameWorld world = WorldWith();

        Assert.Equal(new Point(4, 4), world.Player.Position);
    }

    [Fact]
    public void ThePlayerIsOneOfTheEntities()
    {
        // The player is drawn and blocks like anything else, so it must be in the list.
        GameWorld world = WorldWith();

        Assert.Contains(world.Player, world.Entities);
    }

    [Fact]
    public void AStepOntoOpenFloorMovesThePlayer()
    {
        GameWorld world = WorldWith();

        PlayerAction action = world.MovePlayer(new Point(1, 0));

        Assert.Equal(PlayerActionKind.Moved, action.Kind);
        Assert.Equal(new Point(5, 4), world.Player.Position);
    }

    [Fact]
    public void AStepIntoAWallIsRefused()
    {
        GameWorld world = WorldWith();

        // Four steps right from (4,4) reaches the wall at column 8.
        for (int step = 0; step < 3; step++)
        {
            world.MovePlayer(new Point(1, 0));
        }

        PlayerAction action = world.MovePlayer(new Point(1, 0));

        Assert.Equal(PlayerActionKind.BlockedByWall, action.Kind);
        Assert.Equal(new Point(7, 4), world.Player.Position);
    }

    [Fact]
    public void WalkingIntoAMonsterIsABumpRatherThanAMove()
    {
        // Bump to attack: there is no separate key, and Part 6 makes this do damage.
        Entity rat = new Entity("Rat", 'r', Color.Red, new Point(5, 4), blocksMovement: true);
        GameWorld world = WorldWith(rat);

        PlayerAction action = world.MovePlayer(new Point(1, 0));

        Assert.Equal(PlayerActionKind.Bumped, action.Kind);
        Assert.Same(rat, action.Target);

        // A bump costs the move: the player is still where they started.
        Assert.Equal(new Point(4, 4), world.Player.Position);
    }

    [Fact]
    public void AnItemOnTheFloorDoesNotBlock()
    {
        // The distinction BlocksMovement exists for. Part 8 puts real items on the floor.
        Entity corpse = new Entity("Corpse", '%', Color.Red, new Point(5, 4), blocksMovement: false);
        GameWorld world = WorldWith(corpse);

        PlayerAction action = world.MovePlayer(new Point(1, 0));

        Assert.Equal(PlayerActionKind.Moved, action.Kind);
        Assert.Equal(new Point(5, 4), world.Player.Position);
    }

    [Fact]
    public void BlockingEntityAtFindsACreatureAndIgnoresAnItem()
    {
        Entity rat = new Entity("Rat", 'r', Color.Red, new Point(2, 2), blocksMovement: true);
        Entity corpse = new Entity("Corpse", '%', Color.Red, new Point(3, 3), blocksMovement: false);
        GameWorld world = WorldWith(rat, corpse);

        Assert.Same(rat, world.BlockingEntityAt(new Point(2, 2)));
        Assert.Null(world.BlockingEntityAt(new Point(3, 3)));
        Assert.Null(world.BlockingEntityAt(new Point(6, 6)));
    }

    [Fact]
    public void AZeroOffsetIsNotATurn()
    {
        GameWorld world = WorldWith();

        PlayerAction action = world.MovePlayer(Point.Zero);

        Assert.Equal(PlayerActionKind.None, action.Kind);
        Assert.Equal(new Point(4, 4), world.Player.Position);
    }

    [Fact]
    public void AWallIsCheckedBeforeAMonster()
    {
        // A monster standing inside a wall is not something to bump into; the map decides first.
        // Without this ordering a monster left in rock by a later bug would become attackable.
        Entity rat = new Entity("Rat", 'r', Color.Red, new Point(8, 4), blocksMovement: true);
        GameWorld world = WorldWith(rat);

        for (int step = 0; step < 3; step++)
        {
            world.MovePlayer(new Point(1, 0));
        }

        PlayerAction action = world.MovePlayer(new Point(1, 0));

        Assert.Equal(PlayerActionKind.BlockedByWall, action.Kind);
    }

    [Fact]
    public void SightFollowsThePlayer()
    {
        GameWorld world = WorldWith();

        Assert.Equal(CellVisibility.Visible, world.Visibility.StateAt(world.Player.Position));

        world.MovePlayer(new Point(1, 0));

        Assert.Equal(CellVisibility.Visible, world.Visibility.StateAt(world.Player.Position));
    }

    [Fact]
    public void AGeneratedWorldIsReproducibleFromItsSeed()
    {
        // Monsters are drawn from the same Random as the dungeon, so one seed fixes both.
        GameWorld first = GameWorld.Generate(40, 25, new Random(12345), MonsterTable.Standard, ItemTable.Standard);
        GameWorld second = GameWorld.Generate(40, 25, new Random(12345), MonsterTable.Standard, ItemTable.Standard);

        Assert.Equal(first.ComposeFrame().ToText(), second.ComposeFrame().ToText());

        Assert.Equal(
            first.Entities.Select(entity => $"{entity.Name}{entity.Position}"),
            second.Entities.Select(entity => $"{entity.Name}{entity.Position}"));
    }

    [Fact]
    public void AGeneratedWorldPutsMonstersOnWalkableCells()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            GameWorld world = GameWorld.Generate(40, 25, new Random(seed), MonsterTable.Standard, ItemTable.Standard);

            foreach (Entity entity in world.Entities)
            {
                Assert.True(
                    world.Map.IsWalkable(entity.Position),
                    $"seed {seed}: {entity.Name} is standing in rock at {entity.Position}");
            }
        }
    }

    [Fact]
    public void AGeneratedWorldNeverStacksTwoCreaturesOnOneCell()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            GameWorld world = GameWorld.Generate(40, 25, new Random(seed), MonsterTable.Standard, ItemTable.Standard);

            List<Point> occupied = world.Entities
                .Where(entity => entity.BlocksMovement)
                .Select(entity => entity.Position)
                .ToList();

            Assert.Equal(occupied.Count, occupied.Distinct().Count());
        }
    }

    [Fact]
    public void ThePlayerNeverStartsOnAMonster()
    {
        // The first room is left empty, so the opening move is never a forced fight.
        for (int seed = 0; seed < 20; seed++)
        {
            GameWorld world = GameWorld.Generate(40, 25, new Random(seed), MonsterTable.Standard, ItemTable.Standard);

            IEnumerable<Entity> others = world.Entities.Where(entity => entity != world.Player);

            Assert.DoesNotContain(world.Player.Position, others.Select(entity => entity.Position));
        }
    }

    [Fact]
    public void AGeneratedWorldContainsMonsters()
    {
        // Weak on purpose: how many is random. That there are any at all is not.
        GameWorld world = GameWorld.Generate(60, 35, new Random(7), MonsterTable.Standard, ItemTable.Standard);

        Assert.True(world.Entities.Count > 1, "a dungeon this size should hold at least one monster");
    }

    [Fact]
    public void APlayerOutsideTheEntityListIsRejected()
    {
        GameMap map = new GameMap(5, 5);
        Entity player = new Entity("Player", '@', Color.White, new Point(2, 2), blocksMovement: true);
        player.Fighter = new Fighter(10, 1, 0);

        Assert.Throws<ArgumentException>(() => new GameWorld(map, Array.Empty<Entity>(), player));
    }

    [Fact]
    public void ANullArgumentIsRejected()
    {
        GameMap map = new GameMap(5, 5);
        Entity player = new Entity("Player", '@', Color.White, new Point(2, 2), blocksMovement: true);
        player.Fighter = new Fighter(10, 1, 0);

        Assert.Throws<ArgumentNullException>(() => new GameWorld(null!, new[] { player }, player));
        Assert.Throws<ArgumentNullException>(() => new GameWorld(map, null!, player));
        Assert.Throws<ArgumentNullException>(() => new GameWorld(map, new[] { player }, null!));
        Assert.Throws<ArgumentNullException>(() => GameWorld.Generate(40, 25, null!, MonsterTable.Standard, ItemTable.Standard));
        Assert.Throws<ArgumentNullException>(() => GameWorld.Generate(40, 25, new Random(1), null!, ItemTable.Standard));
        Assert.Throws<ArgumentNullException>(() => GameWorld.Generate(40, 25, new Random(1), MonsterTable.Standard, null!));
    }
}
```

## Step 4: build and run

```
dotnet build
dotnet test
dotnet run --project RogueTutorial
```

Expected: a clean build, 329 passing tests, and potions you can pick up and drink.

### If something is wrong

| Symptom | Cause |
|---|---|
| `CS7036: no argument for 'items'` | A `GameWorld.Generate` call not yet updated |
| Movement keys still work with the pack open | `CommandReader` is not switching on the mode |
| Letters move the player | The same, the other way round |
| A potion vanishes when drunk at full health | `UseResult.Consumed` is true when nothing was recovered |
| The pack holds everything you find | `TryAdd` is not checking `IsFull` |
| An item stays on the floor after being picked up | `PickUpHere` is not removing it from the entity list |
| Shift does nothing | `RootScreen` is not passing the shift state to `CommandReader` |
| The pack draws as text on the dungeon | The frame is missing from `WriteInventory` |

## Step 5: regenerate the documentation

Skip this if you did not set up docfx in Part 1. Nothing was deleted this part:

```
dotnet docfx docfx.json --serve --port 8081
```

Expected: `Build succeeded. 0 warning(s) 0 error(s)`, and pages for `Consumable`, `Inventory`,
`ItemTable`, `GameCommand` and `CommandReader` at <http://localhost:8081>.

---

Next: **Part 9, ranged scrolls and targeting.**

# Part 13: Equipment

- [What it is](#what-it-is)
- [How to use it](#how-to-use-it)
- [How to set it up](#how-to-set-it-up)

---

# What it is

Weapons and armour, and the last thing the levelling from Part 11 was missing: somewhere for
your numbers to meet numbers you find.

```
+----------------------------------------+
| a) dagger (equipped)                   |
| b) leather armour (equipped)           |
| c) healing potion                      |
| d) lightning scroll                    |
+----------------------------------------+

HP: 28/30 =============-  Lv 2  XP 12/65  Floor 3
You equip the leather armour.
```

## The numbers are read, never written

This is the whole design, and it is one sentence: **equipping changes what a number comes out
as, without changing the number.**

```csharp
public int EffectiveAttack => (Fighter?.Attack ?? 0) + (Equipment?.AttackBonus ?? 0);
```

The alternative is to add the bonus into `Fighter.Attack` on equip and subtract it on unequip.
That is one line shorter and it is the bug: two writers to one field, where the second has to
remember exactly what the first added. Part 11 already made those numbers mutable so levelling
could raise them, so the equip path and the level-up path would both be writing `Attack`, and any
disagreement between them shows up as a character who is permanently slightly wrong.

Nothing is stored, so nothing can drift. There is a test that says exactly that: after equipping
a sword and mail, `Fighter.Attack` is still 5 and `Fighter.Defence` is still 2.

## What counts as an item is said once

```csharp
public bool IsCarryable => Consumable is not null || Equippable is not null;
```

Before this part there was only one kind of item, so "is this an item" was spelled "does it have
a `Consumable`" - at the one place that needed to know, inside the pick-up code. That was correct
right up until a second kind of item existed, and then **every piece of equipment in the dungeon
became impossible to pick up**: it lay on the floor, drawn, generated, and invisible to the only
key that could collect it.

The fix is not a longer condition at the call site. It is naming the question on `Entity`, where
the answer lives, so the next kind of item answers it by existing rather than by being remembered.

## The damage rule moves, and stays in one place

```csharp
public static int DamageFrom(int attack, int defence)
```

It used to be `attacker.DamageAgainst(target)`, reading both numbers off two `Fighter`s. A
`Fighter` does not know what its owner is wearing and should not - so it stops fetching the
numbers and is handed them instead. The rule is still written once, here; who supplies the
numbers is now the caller's business, and the caller is `Combat`, which can see the entities.

## There is no wear key

Equipment has no other use, so the key that uses an item is the key that wears it:

```csharp
if (item.Equippable is not null)
{
    return ToggleEquipped(item);
}
```

Choosing it again takes it off. A separate wear key would need its own list of what is wearable,
with its own letters, and the player would have to learn which list they were looking at.

**Equipped items stay in the pack.** Wearing something is a way of using it rather than a way of
carrying it, so the letters do not move when you put something on. The list marks what is worn
instead.

**Dropping something you are wearing takes it off first**, or it would lie on the floor still
adding its bonus.

## A kind is one thing or the other

```csharp
ItemKind.Usable("healing potion", '!', ..., ConsumableKind.Healing, power: 8, radius: 0, minimumDepth: 1),
ItemKind.Wearable("dagger", '/', ..., EquipmentSlot.Weapon, attackBonus: 2, defenceBonus: 0, minimumDepth: 1),
```

One constructor taking both an effect and a slot would let you declare a potion you can wield.
Two factories over a private constructor make that unbuildable, and each one validates its own
half at the declaration rather than in the dungeon.

Equipment follows Part 12's depth rule: daggers and leather from floor one, a sword from four,
chain mail from six.

## Version 4

A version 3 save records no equipment, so resuming one would silently disarm the player. What
was worn is restored by calling `Equip` rather than by assignment, so a hand-edited file cannot
produce two weapons in one slot - the same reasoning as Part 11 replaying levels rather than
setting them.

## What is deliberately wrong

**Two slots.** No rings, no helmet, no shield. Each extra slot is another thing to draw, save and
explain, and two is enough for the choice to matter.

**Nothing is cursed and nothing is unique.** Every piece is strictly better than a weaker one, so
the decision is only ever "is this bigger", which is not much of a decision.

**Monsters do not use equipment.** Only the player has an `Equipment` component; a goblin with a
sword would need the same machinery on the monster side and a way to show it.

**Nothing drops what it was carrying.** Killing a monster gives experience and nothing else.

---

# How to use it

## Play it

```
cd parts/part-13-equipment
dotnet run --project RogueTutorial
```

**Any Part 12 save is refused**, reported in the log, and replaced with a new game.

Pick up a dagger with `g`, open the pack with `i`, press its letter to wield it. Press it again
to put it away. The list says what is on.

## Run the tests

```
dotnet test                                  # 470 tests
dotnet test --filter "Category!=EndToEnd"    # no window
```

| Test class | Level | Covers |
|---|---|---|
| [`EquipmentTests`](../parts/part-13-equipment/RogueTutorial.Tests/EquipmentTests.cs) | unit + integration | wearing, wielding, dropping, and combat |
| [`SaveGameTests`](../parts/part-13-equipment/RogueTutorial.Tests/SaveGameTests.cs) | unit | equipment through the round trip |

## Prove the tests can fail

Every change below was applied to this part's code and the suite was run. The count is what
actually failed.

| Change | Expect |
|---|---|
| `Entity.EffectiveAttack`: drop the equipment term | 4 fail |
| `Entity.EffectiveDefence`: drop the equipment term | 2 fail |
| `Combat`: read the bare `Fighter` numbers | 1 fails |
| `Equipment.Equip`: never report what was displaced | 1 fails |
| `Equipment.Unequip`: return it without removing it | 3 fail |
| `GameWorld.UseItem`: fall through to the consumable path | 2 fail |
| `GameWorld.DropItem`: drop it still equipped | 1 fails |
| `ScreenComposer`: stop marking what is worn | 1 fails |
| `SaveGame`: write an empty equipped list | 2 fail |
| `SaveGame`: leave the version at 3 | 1 fails |
| `Equippable`: allow a bonus of nothing | 1 fails |
| `GameWorld.Generate`: give the player no `Equipment` | 3 fail |
| `Entity.IsCarryable`: require a `Consumable` | 2 fail |
| `Entity.IsCarryable`: require an `Equippable` | 4 fail |
| `Entity.IsCarryable`: say everything is carryable | 1 fails |

---

# How to set it up

> **You are in:** your project folder, the one holding `RogueTutorial/` and `RogueTutorial.Tests/`

## Step 1: retitle the window

One line in `RogueTutorial/Program.cs`:

```csharp
const string WindowTitle = "Roguelike Tutorial - Part 13: Equipment";
```

## Step 2: the source files

**Each block below is the complete file.** Three are new; the rest already exist and should be
replaced entirely.

Removing `Fighter.DamageAgainst` breaks its callers, which is the compiler pointing at every
place that was reading combat numbers directly.

**Do not build until every file in this step is in place** - C# compiles a project as a whole, so
a half-finished step fails on files that are perfectly correct.

### [`RogueTutorial/EquipmentSlot.cs`](../parts/part-13-equipment/RogueTutorial/EquipmentSlot.cs)

New. The two places a thing can be worn.

```csharp
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
```

### [`RogueTutorial/Equippable.cs`](../parts/part-13-equipment/RogueTutorial/Equippable.cs)

New. What an item is worth while it is on.

```csharp
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
```

### [`RogueTutorial/Entity.cs`](../parts/part-13-equipment/RogueTutorial/Entity.cs)

The Part 12 file, with the two components and the effective numbers.

<!-- generated-diff -->
**Changed from Part 12.** The complete file follows; this is only what moved:

```diff
--- part-12-deeper-levels/Entity.cs
+++ current/Entity.cs
@@ -69,6 +69,31 @@
     /// <summary>What this entity is carrying, or null when it carries nothing ever.</summary>
     public Inventory? Inventory { get; set; }
 
+    /// <summary>What this item does when worn, or null when it is not equipment.</summary>
+    public Equippable? Equippable { get; set; }
+
+    /// <summary>
+    /// What this entity is wearing, or null when it never wears anything. Only the player has
+    /// one; monsters read their Fighter alone.
+    /// </summary>
+    public Equipment? Equipment { get; set; }
+
+    /// <summary>
+    /// True when this is something the player can carry: a thing with a use, or a thing to wear.
+    /// Stated once here, because "is this an item" was previously spelled "has a Consumable",
+    /// which made every piece of equipment in the dungeon impossible to pick up.
+    /// </summary>
+    public bool IsCarryable => Consumable is not null || Equippable is not null;
+
+    /// <summary>
+    /// Attack after equipment, which is the number combat uses. Nothing is written into Fighter,
+    /// so this is computed on every read rather than kept in step with one.
+    /// </summary>
+    public int EffectiveAttack => (Fighter?.Attack ?? 0) + (Equipment?.AttackBonus ?? 0);
+
+    /// <summary>Defence after equipment. Computed the same way and for the same reason.</summary>
+    public int EffectiveDefence => (Fighter?.Defence ?? 0) + (Equipment?.DefenceBonus ?? 0);
+
     /// <summary>
     /// How far along this entity is, or null when it does not collect experience. Monsters award
     /// it rather than gathering it, so only the player has one.
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
 *     Entity corpse = new Entity("Corpse", '%', Color.Red, new Point(41, 12), blocksMovement: false,
 *         RenderLayer.Corpse);                // what it is drawn under when a cell is shared
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
    /// Which things this is drawn over when they share a cell. Set at construction and lowered
    /// to Corpse by Die, because remains belong under whatever is dropped on them.
    /// </summary>
    public RenderLayer Layer { get; private set; }

    /// <summary>
    /// This entity's combat numbers, or null when it cannot fight. Set to null by Die, which is
    /// what turns a monster into a corpse.
    /// </summary>
    public Fighter? Fighter { get; set; }

    /// <summary>What this entity does when used up, or null when it is not an item.</summary>
    public Consumable? Consumable { get; set; }

    /// <summary>What this entity is carrying, or null when it carries nothing ever.</summary>
    public Inventory? Inventory { get; set; }

    /// <summary>What this item does when worn, or null when it is not equipment.</summary>
    public Equippable? Equippable { get; set; }

    /// <summary>
    /// What this entity is wearing, or null when it never wears anything. Only the player has
    /// one; monsters read their Fighter alone.
    /// </summary>
    public Equipment? Equipment { get; set; }

    /// <summary>
    /// True when this is something the player can carry: a thing with a use, or a thing to wear.
    /// Stated once here, because "is this an item" was previously spelled "has a Consumable",
    /// which made every piece of equipment in the dungeon impossible to pick up.
    /// </summary>
    public bool IsCarryable => Consumable is not null || Equippable is not null;

    /// <summary>
    /// Attack after equipment, which is the number combat uses. Nothing is written into Fighter,
    /// so this is computed on every read rather than kept in step with one.
    /// </summary>
    public int EffectiveAttack => (Fighter?.Attack ?? 0) + (Equipment?.AttackBonus ?? 0);

    /// <summary>Defence after equipment. Computed the same way and for the same reason.</summary>
    public int EffectiveDefence => (Fighter?.Defence ?? 0) + (Equipment?.DefenceBonus ?? 0);

    /// <summary>
    /// How far along this entity is, or null when it does not collect experience. Monsters award
    /// it rather than gathering it, so only the player has one.
    /// </summary>
    public Level? Level { get; set; }

    /// <summary>
    /// Creates an entity at a starting cell. Throws ArgumentException on a blank name, since an
    /// unnamed entity would surface much later as an empty word in a message.
    /// </summary>
    public Entity(
        string name, char glyph, Color foreground, Point startingPosition,
        bool blocksMovement, RenderLayer layer)
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
        Layer = layer;
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

        // Remains sink below anything dropped on them: a potion on a corpse is the one the
        // player can still pick up.
        Layer = RenderLayer.Corpse;
    }
}
```

### [`RogueTutorial/Equipment.cs`](../parts/part-13-equipment/RogueTutorial/Equipment.cs)

New. What one entity is wearing. It reads Entity.Equippable, so it comes after it.

```csharp
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
```

### [`RogueTutorial/Fighter.cs`](../parts/part-13-equipment/RogueTutorial/Fighter.cs)

The Part 11 file. The damage rule becomes static and takes its numbers.

<!-- generated-diff -->
**Changed from Part 12.** The complete file follows; this is only what moved:

```diff
--- part-12-deeper-levels/Fighter.cs
+++ current/Fighter.cs
@@ -170,14 +170,15 @@
     }
 
     /// <summary>
-    /// The damage this fighter deals to the target: attack less the target's defence, floored at
-    /// zero, so a target that out-defends the attacker takes nothing rather than being healed.
-    /// Throws ArgumentNullException on a null target.
+    /// One blow: attack less defence, floored at zero, so a target that out-defends the attacker
+    /// takes nothing rather than being healed.
+    ///
+    /// Static, and given the numbers rather than reading them off two fighters, because from
+    /// Part 13 the numbers that matter include equipment and a Fighter does not know what its
+    /// owner is wearing. The rule lives here; who supplies the numbers is the caller's business.
     /// </summary>
-    public int DamageAgainst(Fighter target)
+    public static int DamageFrom(int attack, int defence)
     {
-        ArgumentNullException.ThrowIfNull(target);
-
-        return Math.Max(0, Attack - target.Defence);
+        return Math.Max(0, attack - defence);
     }
 }
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
```

### [`RogueTutorial/Combat.cs`](../parts/part-13-equipment/RogueTutorial/Combat.cs)

The Part 12 file, reading the effective numbers.

<!-- generated-diff -->
**Changed from Part 12.** The complete file follows; this is only what moved:

```diff
--- part-12-deeper-levels/Combat.cs
+++ current/Combat.cs
@@ -67,7 +67,9 @@
             throw new ArgumentException($"{target.Name} cannot be attacked: it has no Fighter.", nameof(target));
         }
 
-        int damage = attacker.Fighter.DamageAgainst(target.Fighter);
+        // The effective numbers, so equipment reaches combat without anything being written
+        // into either Fighter.
+        int damage = Fighter.DamageFrom(attacker.EffectiveAttack, target.EffectiveDefence);
 
         // A blow that defence absorbs entirely still happened, and the log should say so.
         if (damage == 0)
```
<!-- generated-diff -->

```csharp
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

        // The effective numbers, so equipment reaches combat without anything being written
        // into either Fighter.
        int damage = Fighter.DamageFrom(attacker.EffectiveAttack, target.EffectiveDefence);

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
```

### [`RogueTutorial/ItemTable.cs`](../parts/part-13-equipment/RogueTutorial/ItemTable.cs)

The Part 12 file, with two factories and four pieces of equipment.

<!-- generated-diff -->
**Changed from Part 12.** The complete file follows; this is only what moved:

```diff
--- part-12-deeper-levels/ItemTable.cs
+++ current/ItemTable.cs
@@ -37,8 +37,20 @@
     /// <summary>How likely this kind is relative to the others in its table.</summary>
     public int Weight { get; }
 
-    /// <summary>What it does when used.</summary>
+    /// <summary>What it does when used, ignored when this kind is equipment.</summary>
     public ConsumableKind Effect { get; }
+
+    /// <summary>
+    /// Where this is worn, or null when it is something to be used rather than worn. Exactly one
+    /// of this and Effect means anything for a given kind.
+    /// </summary>
+    public EquipmentSlot? Slot { get; }
+
+    /// <summary>What it adds to attack while worn. Zero for anything that is not equipment.</summary>
+    public int AttackBonus { get; }
+
+    /// <summary>What it adds to defence while worn.</summary>
+    public int DefenceBonus { get; }
 
     /// <summary>How much it does it by.</summary>
     public int Power { get; }
@@ -53,9 +65,9 @@
     /// Records one item kind. Throws ArgumentException on a blank name and
     /// ArgumentOutOfRangeException on a weight below one, which could never be chosen.
     /// </summary>
-    public ItemKind(
+    private ItemKind(
         string name, char glyph, Color foreground, int weight, ConsumableKind effect,
-        int power, int radius, int minimumDepth)
+        int power, int radius, int minimumDepth, EquipmentSlot? slot, int attackBonus, int defenceBonus)
     {
         if (string.IsNullOrWhiteSpace(name))
         {
@@ -82,10 +94,46 @@
         Power = power;
         Radius = radius;
         MinimumDepth = minimumDepth;
+        Slot = slot;
+        AttackBonus = attackBonus;
+        DefenceBonus = defenceBonus;
 
         // Constructing the component here would throw far from this call site, so the same rule
         // is enforced where the kind is declared.
-        _ = new Consumable(effect, power, radius);
+        if (slot is null)
+        {
+            _ = new Consumable(effect, power, radius);
+        }
+        else
+        {
+            _ = new Equippable(slot.Value, attackBonus, defenceBonus);
+        }
+    }
+
+    /// <summary>
+    /// A kind that is drunk or read: it has an effect and no slot. Throws the same way the
+    /// Consumable component does, at the declaration rather than at the dungeon.
+    /// </summary>
+    public static ItemKind Usable(
+        string name, char glyph, Color foreground, int weight,
+        ConsumableKind effect, int power, int radius, int minimumDepth)
+    {
+        return new ItemKind(
+            name, glyph, foreground, weight, effect, power, radius, minimumDepth,
+            slot: null, attackBonus: 0, defenceBonus: 0);
+    }
+
+    /// <summary>
+    /// A kind that is worn or wielded: it has a slot and bonuses, and no effect. Throws the same
+    /// way the Equippable component does.
+    /// </summary>
+    public static ItemKind Wearable(
+        string name, char glyph, Color foreground, int weight,
+        EquipmentSlot slot, int attackBonus, int defenceBonus, int minimumDepth)
+    {
+        return new ItemKind(
+            name, glyph, foreground, weight, ConsumableKind.Healing, power: 0, radius: 0,
+            minimumDepth, slot, attackBonus, defenceBonus);
     }
 }
 
@@ -129,14 +177,22 @@
     public static ItemTable Standard => new ItemTable(
         new[]
         {
-            new ItemKind("healing potion", '!', new Color(200, 80, 200),
+            ItemKind.Usable("healing potion", '!', new Color(200, 80, 200),
                 weight: 4, ConsumableKind.Healing, power: 8, radius: 0, minimumDepth: 1),
-            new ItemKind("lightning scroll", '?', new Color(230, 230, 100),
+            ItemKind.Usable("lightning scroll", '?', new Color(230, 230, 100),
                 weight: 2, ConsumableKind.Lightning, power: 12, radius: 0, minimumDepth: 1),
-            new ItemKind("fireball scroll", '?', new Color(230, 130, 60),
+            ItemKind.Usable("fireball scroll", '?', new Color(230, 130, 60),
                 weight: 1, ConsumableKind.Fireball, power: 8, radius: 3, minimumDepth: 1),
-            new ItemKind("greater healing potion", '!', new Color(240, 120, 240),
+            ItemKind.Usable("greater healing potion", '!', new Color(240, 120, 240),
                 weight: 2, ConsumableKind.Healing, power: 20, radius: 0, minimumDepth: 4),
+            ItemKind.Wearable("dagger", '/', new Color(170, 170, 190),
+                weight: 2, EquipmentSlot.Weapon, attackBonus: 2, defenceBonus: 0, minimumDepth: 1),
+            ItemKind.Wearable("leather armour", '[', new Color(160, 110, 70),
+                weight: 2, EquipmentSlot.Armour, attackBonus: 0, defenceBonus: 1, minimumDepth: 1),
+            ItemKind.Wearable("sword", '/', new Color(200, 200, 230),
+                weight: 1, EquipmentSlot.Weapon, attackBonus: 4, defenceBonus: 0, minimumDepth: 4),
+            ItemKind.Wearable("chain mail", '[', new Color(180, 180, 200),
+                weight: 1, EquipmentSlot.Armour, attackBonus: 0, defenceBonus: 3, minimumDepth: 6),
         },
         maximumPerRoom: 2);
 
@@ -181,7 +237,15 @@
             Entity dropped = new Entity(
                 kind.Name, kind.Glyph, kind.Foreground, cell, blocksMovement: false, RenderLayer.Item);
 
-            dropped.Consumable = new Consumable(kind.Effect, kind.Power, kind.Radius);
+            // A kind is one thing or the other, which is what the two factories guarantee.
+            if (kind.Slot is null)
+            {
+                dropped.Consumable = new Consumable(kind.Effect, kind.Power, kind.Radius);
+            }
+            else
+            {
+                dropped.Equippable = new Equippable(kind.Slot.Value, kind.AttackBonus, kind.DefenceBonus);
+            }
 
             placed.Add(dropped);
         }
```
<!-- generated-diff -->

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
```

### [`RogueTutorial/GameWorld.cs`](../parts/part-13-equipment/RogueTutorial/GameWorld.cs)

The Part 12 file, equipping from the pack and undressing on a drop.

<!-- generated-diff -->
**Changed from Part 12.** The complete file follows; this is only what moved:

```diff
--- part-12-deeper-levels/GameWorld.cs
+++ current/GameWorld.cs
@@ -156,9 +156,53 @@
         // Twenty-six slots, because items are chosen by letter and there are twenty-six letters.
         player.Inventory = new Inventory(capacity: 26);
 
+        // Empty to start: what the player finds is what they wear.
+        player.Equipment = new Equipment();
+
         List<Entity> entities = PopulateRooms(dungeon, player, random, monsters, items, depth);
 
         return new GameWorld(dungeon.Map, entities, player) { Depth = depth };
+    }
+
+    /// <summary>
+    /// Puts an item on, or takes it off if it is already worn. Whatever it displaces goes back to
+    /// the pack, which always has room because the item being equipped just left it. Returns true
+    /// because either way the turn is spent. Throws ArgumentException on something that is not
+    /// equipment, which UseItem is what rules out.
+    /// </summary>
+    private bool ToggleEquipped(Entity item)
+    {
+        ArgumentNullException.ThrowIfNull(item);
+
+        if (Player.Equipment is null)
+        {
+            return false;
+        }
+
+        if (Player.Equipment.IsEquipped(item))
+        {
+            Player.Equipment.Unequip(item.Equippable!.Slot);
+
+            Log.Add($"You take off the {item.Name}.");
+
+            RunMonsterTurns();
+
+            return true;
+        }
+
+        Entity? displaced = Player.Equipment.Equip(item);
+
+        Log.Add($"You equip the {item.Name}.");
+
+        // Both stay in the pack: equipping is not carrying it differently, only using it.
+        if (displaced is not null)
+        {
+            Log.Add($"You put away the {displaced.Name}.");
+        }
+
+        RunMonsterTurns();
+
+        return true;
     }
 
     /// <summary>
@@ -466,7 +510,7 @@
 
         // The first item on this cell, ignoring creatures and the player themselves.
         Entity? item = _entities.FirstOrDefault(
-            entity => entity != Player && entity.Consumable is not null && entity.Position == Player.Position);
+            entity => entity != Player && entity.IsCarryable && entity.Position == Player.Position);
 
         if (item is null)
         {
@@ -504,7 +548,19 @@
 
         Entity? item = Player.Inventory.At(slot);
 
-        if (item?.Consumable is null)
+        if (item is null)
+        {
+            return false;
+        }
+
+        // Equipment has no "use": choosing it from the pack puts it on, or takes it off if it is
+        // already on. One key does both, because a separate wear key would need a separate list.
+        if (item.Equippable is not null)
+        {
+            return ToggleEquipped(item);
+        }
+
+        if (item.Consumable is null)
         {
             return false;
         }
@@ -659,13 +715,22 @@
             return false;
         }
 
+        // Dropping something you are wearing takes it off first, or it would go on lying on
+        // the floor still adding its bonus.
+        if (Player.Equipment is not null && Player.Equipment.IsEquipped(item))
+        {
+            Player.Equipment.Unequip(item.Equippable!.Slot);
+
+            Log.Add($"You take off the {item.Name}.");
+        }
+
         Player.Inventory.Remove(item);
 
         // Back onto the map, where the player stands, so it can be picked up again.
         item.MoveTo(Player.Position);
 
-        // Items are drawn under creatures, so it goes at the front of the list.
-        _entities.Insert(0, item);
+        // RenderLayer decides what covers what, so where this lands in the list does not.
+        _entities.Add(item);
 
         Log.Add($"You drop the {item.Name}.");
 
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

    // Everything standing in the dungeon. The order here is not draw order: RenderLayer decides
    // that, so a monster is never hidden by the item it is standing on.
    private readonly List<Entity> _entities;

    /// <summary>The dungeon floor. Replaced wholesale by Descend.</summary>
    public GameMap Map { get; private set; }

    /// <summary>What the player can see now and what they remember.</summary>
    public VisibilityMap Visibility { get; private set; }

    /// <summary>The entity the keyboard drives. Always present in Entities.</summary>
    public Entity Player { get; }

    /// <summary>Everything standing in the dungeon, the player included.</summary>
    public IReadOnlyList<Entity> Entities => _entities;

    /// <summary>
    /// Which floor this is, counting from one. Deeper floors carry worse monsters, and the
    /// number is what the tables are asked with.
    /// </summary>
    public int Depth { get; private set; } = 1;

    /// <summary>True when the player is standing on the way down.</summary>
    public bool IsPlayerOnStairs => Map.GetTile(Player.Position).Equals(TileTypes.DownStairs);

    /// <summary>What has happened lately, drawn under the map.</summary>
    public MessageLog Log { get; } = new MessageLog(capacity: 100);

    /// <summary>
    /// What the player is doing, which decides what their keys mean. Held here rather than on
    /// the screen class, so a test can open the pack and press a letter without a window.
    /// </summary>
    public GameMode Mode { get; private set; } = GameMode.Playing;

    /// <summary>
    /// What is being aimed, or null when nothing is. Non-null exactly while the mode is
    /// Targeting, which is asserted on every transition rather than merely intended.
    /// </summary>
    public Targeting? Aiming { get; private set; }

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
    /// one seed reproduces the whole world - dungeon and monsters alike. The depth decides what
    /// the tables are allowed to place. Throws ArgumentNullException on a null argument and
    /// ArgumentOutOfRangeException on a depth below one.
    /// </summary>
    public static GameWorld Generate(
        int width, int height, Random random, MonsterTable monsters, ItemTable items, int depth)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(items);

        // Floors count from one. A zero or negative depth would read as a valid table query.
        if (depth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), depth, "The first floor is depth one.");
        }

        DungeonSettings settings = new DungeonSettings(maximumRooms: 30, minimumRoomSize: 6, maximumRoomSize: 10);

        GeneratedDungeon dungeon = new DungeonGenerator(settings).Generate(width, height, random);

        Entity player = new Entity(
            "Player", '@', Color.White, dungeon.PlayerStart, blocksMovement: true, RenderLayer.Player);

        // The player's numbers: enough health to survive a mistake, enough defence that a rat
        // is an inconvenience rather than a threat.
        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2, experienceAwarded: 0);

        // Only the player collects experience; monsters award it.
        player.Level = new Level();

        // Twenty-six slots, because items are chosen by letter and there are twenty-six letters.
        player.Inventory = new Inventory(capacity: 26);

        // Empty to start: what the player finds is what they wear.
        player.Equipment = new Equipment();

        List<Entity> entities = PopulateRooms(dungeon, player, random, monsters, items, depth);

        return new GameWorld(dungeon.Map, entities, player) { Depth = depth };
    }

    /// <summary>
    /// Puts an item on, or takes it off if it is already worn. Whatever it displaces goes back to
    /// the pack, which always has room because the item being equipped just left it. Returns true
    /// because either way the turn is spent. Throws ArgumentException on something that is not
    /// equipment, which UseItem is what rules out.
    /// </summary>
    private bool ToggleEquipped(Entity item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Player.Equipment is null)
        {
            return false;
        }

        if (Player.Equipment.IsEquipped(item))
        {
            Player.Equipment.Unequip(item.Equippable!.Slot);

            Log.Add($"You take off the {item.Name}.");

            RunMonsterTurns();

            return true;
        }

        Entity? displaced = Player.Equipment.Equip(item);

        Log.Add($"You equip the {item.Name}.");

        // Both stay in the pack: equipping is not carrying it differently, only using it.
        if (displaced is not null)
        {
            Log.Add($"You put away the {displaced.Name}.");
        }

        RunMonsterTurns();

        return true;
    }

    /// <summary>
    /// Puts the world back on the floor a save recorded. Only SaveGame needs this: every other
    /// way to reach floor five is to walk down to it. Throws ArgumentOutOfRangeException below
    /// floor one, which is not a floor.
    /// </summary>
    public void RestoreDepth(int depth)
    {
        if (depth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), depth, "The first floor is depth one.");
        }

        Depth = depth;
    }

    /// <summary>
    /// Replaces the floor with the next one down, keeping the player exactly as they are -
    /// health, experience, level and pack all carry over, because the descent is a commitment
    /// rather than a rest. The floor left behind is discarded: there is no way back up.
    ///
    /// Returns false when the player is not standing on the stairs, which is a miss rather than
    /// an error - they pressed the key in the wrong place. Throws ArgumentNullException on a
    /// null argument.
    /// </summary>
    public bool Descend(Random random, MonsterTable monsters, ItemTable items)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(items);

        // Pressing the key anywhere else is a miss, not a mistake worth an exception.
        if (!IsPlayerOnStairs)
        {
            return false;
        }

        // A dead player does not get to leave the floor they died on.
        if (IsPlayerDead)
        {
            return false;
        }

        Depth++;

        DungeonSettings settings = new DungeonSettings(
            maximumRooms: 30, minimumRoomSize: 6, maximumRoomSize: 10);

        GeneratedDungeon dungeon = new DungeonGenerator(settings).Generate(Map.Width, Map.Height, random);

        Player.MoveTo(dungeon.PlayerStart);

        Map = dungeon.Map;

        // Memory belongs to a floor. Carrying it over would show the new map already explored.
        Visibility = new VisibilityMap(Map.Width, Map.Height);

        _entities.Clear();
        _entities.AddRange(PopulateRooms(dungeon, Player, random, monsters, items, Depth));

        RecomputeFieldOfView();

        Log.Add($"You descend to floor {Depth}.");

        return true;
    }

    // The player plus whatever the tables put in every room after the first. The first room is
    // where the player starts, so it is left empty: waking up already surrounded is not a fair
    // opening.
    private static List<Entity> PopulateRooms(
        GeneratedDungeon dungeon, Entity player, Random random,
        MonsterTable monsters, ItemTable items, int depth)
    {
        List<Entity> entities = new List<Entity> { player };

        for (int roomIndex = 1; roomIndex < dungeon.Rooms.Count; roomIndex++)
        {
            entities.AddRange(monsters.PopulateRoom(dungeon.Rooms[roomIndex], dungeon.Map, random, depth));

            entities.AddRange(items.PopulateRoom(dungeon.Rooms[roomIndex], dungeon.Map, random, depth));
        }

        return entities;
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

            OfferLevelUpIfEarned();

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
    /// <summary>
    /// Opens the level up menu if one has been earned. Called after anything that could have
    /// killed something, so the choice arrives on the turn it was paid for rather than whenever
    /// the player next happens to look.
    /// </summary>
    private void OfferLevelUpIfEarned()
    {
        if (IsPlayerDead || Player.Level is null || !Player.Level.CanAdvance)
        {
            return;
        }

        Log.Add($"You have earned level {Player.Level.CurrentLevel + 1}.");

        Mode = GameMode.ChoosingLevelUp;
    }

    /// <summary>
    /// Spends an earned level on one of the three improvements and returns to play. A slot that
    /// is not one of them is a miss rather than an error - the player pressed a letter that is
    /// not on the menu. Returns true when a level was actually spent.
    /// </summary>
    public bool ChooseLevelUp(int slot)
    {
        if (Player.Level is null || !Player.Level.CanAdvance)
        {
            return false;
        }

        if (slot < 0 || slot >= LevelUpChoices.All.Count)
        {
            return false;
        }

        Log.Add(LevelUpChoices.Apply(LevelUpChoices.All[slot], Player.Fighter!));

        Player.Level.Advance();

        // A second level may have been earned by the same kill, so the menu reopens rather than
        // dropping to the map with an unspent level in hand.
        Mode = GameMode.Playing;

        OfferLevelUpIfEarned();

        return true;
    }

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

                // Nothing beyond this point in the round matters, and the run is over: Part 10
                // deletes the save here so a death cannot be undone by reloading.
                return;
            }
        }
    }

    /// <summary>
    /// Fills in what the player remembers, for a world rebuilt from a save. What is visible is
    /// recomputed immediately afterwards, so memory and sight cannot disagree with the map.
    /// Throws ArgumentException when the list is not one entry per cell.
    /// </summary>
    public void RestoreMemory(IReadOnlyList<bool> remembered)
    {
        Visibility.RestoreMemory(remembered);

        RecomputeFieldOfView();
    }

    /// <summary>
    /// Opens or closes the pack. Costs no turn: looking at what you are carrying is not an
    /// action, and monsters do not get a move while a menu is open.
    /// </summary>
    public void SetMode(GameMode mode)
    {
        // Targeting carries state, so it is entered by reading a scroll rather than by asking.
        if (mode == GameMode.Targeting)
        {
            throw new ArgumentException("Targeting is entered by using a scroll, not by SetMode.", nameof(mode));
        }

        // A level up is earned rather than requested, and leaving it by asking would let the
        // player walk away from a decision they have already paid for.
        if (mode == GameMode.ChoosingLevelUp)
        {
            throw new ArgumentException("A level up is offered when it is earned, not by SetMode.", nameof(mode));
        }

        Aiming = null;
        Mode = mode;

        Debug.Assert(
            (Mode == GameMode.Targeting) == (Aiming is not null),
            "Something is being aimed exactly when the mode is Targeting.");
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
            entity => entity != Player && entity.IsCarryable && entity.Position == Player.Position);

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

        if (item is null)
        {
            return false;
        }

        // Equipment has no "use": choosing it from the pack puts it on, or takes it off if it is
        // already on. One key does both, because a separate wear key would need a separate list.
        if (item.Equippable is not null)
        {
            return ToggleEquipped(item);
        }

        if (item.Consumable is null)
        {
            return false;
        }

        // A scroll needs somewhere to point. Rather than using it here, the game changes mode and
        // waits; the item stays in the pack until the shot is confirmed, so cancelling loses
        // nothing.
        if (item.Consumable.NeedsTarget)
        {
            BeginTargeting(item, slot);
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
    /// Starts aiming a scroll from the given slot. The cursor begins on the nearest visible
    /// creature if there is one, and on the player otherwise - aiming almost always means aiming
    /// at something, and starting on empty floor makes the common case slower.
    /// </summary>
    private void BeginTargeting(Entity scroll, int slot)
    {
        Aiming = new Targeting(scroll, slot, NearestVisibleTarget(), scroll.Consumable!.Radius);

        Mode = GameMode.Targeting;

        Log.Add($"Aiming the {scroll.Name}. Move to aim, Enter to fire, Esc to cancel.");
    }

    // The closest creature the player can see, or the player's own cell when there is none.
    private Point NearestVisibleTarget()
    {
        Entity? nearest = null;
        int nearestDistance = int.MaxValue;

        foreach (Entity entity in _entities)
        {
            if (entity == Player || entity.Fighter is null)
            {
                continue;
            }

            if (Visibility.StateAt(entity.Position) != CellVisibility.Visible)
            {
                continue;
            }

            int distance = Math.Max(
                Math.Abs(entity.Position.X - Player.Position.X),
                Math.Abs(entity.Position.Y - Player.Position.Y));

            if (distance < nearestDistance)
            {
                nearest = entity;
                nearestDistance = distance;
            }
        }

        return nearest?.Position ?? Player.Position;
    }

    /// <summary>
    /// Moves the aiming cursor. Does nothing when not aiming, which is what makes a stray key
    /// press harmless rather than an exception.
    /// </summary>
    public void MoveCursor(Point offset)
    {
        Aiming?.MoveCursor(offset, Map);
    }

    /// <summary>
    /// Fires the scroll being aimed at wherever the cursor is. A shot that finds nothing leaves
    /// the scroll in the pack and returns the player to it, so a miss costs the turn rather than
    /// the item. Returns true when the scroll was spent.
    /// </summary>
    public bool ConfirmTarget()
    {
        if (Aiming is null)
        {
            return false;
        }

        Targeting aiming = Aiming;

        UseResult result = aiming.Scroll.Consumable!.UseAt(Player, aiming.Cursor, this);

        Log.Add(result.Message);

        if (!result.Consumed)
        {
            // Back to the pack, not to the map: the player has not put the scroll away.
            CancelTarget();
            return false;
        }

        Player.Inventory!.Remove(aiming.Scroll);

        Aiming = null;
        Mode = GameMode.Playing;

        // A fireball can kill the reader, and a dead player takes no more turns.
        if (!IsPlayerDead)
        {
            RunMonsterTurns();
        }

        OfferLevelUpIfEarned();

        return true;
    }

    /// <summary>
    /// Gives up aiming and returns to the pack, where the scroll still is. Costs no turn: the
    /// player has done nothing but look.
    /// </summary>
    public void CancelTarget()
    {
        Aiming = null;
        Mode = GameMode.ShowingInventory;
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

        // Dropping something you are wearing takes it off first, or it would go on lying on
        // the floor still adding its bonus.
        if (Player.Equipment is not null && Player.Equipment.IsEquipped(item))
        {
            Player.Equipment.Unequip(item.Equippable!.Slot);

            Log.Add($"You take off the {item.Name}.");
        }

        Player.Inventory.Remove(item);

        // Back onto the map, where the player stands, so it can be picked up again.
        item.MoveTo(Player.Position);

        // RenderLayer decides what covers what, so where this lands in the list does not.
        _entities.Add(item);

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

### [`RogueTutorial/ScreenComposer.cs`](../parts/part-13-equipment/RogueTutorial/ScreenComposer.cs)

The Part 12 file, marking what is worn.

<!-- generated-diff -->
**Changed from Part 12.** The complete file follows; this is only what moved:

```diff
--- part-12-deeper-levels/ScreenComposer.cs
+++ current/ScreenComposer.cs
@@ -210,7 +210,15 @@
             for (int slot = 0; slot < pack.Items.Count; slot++)
             {
                 // 'a' is the first slot, matching what CommandReader turns a letter into.
-                contents.Add($"{(char)('a' + slot)}) {pack.Items[slot].Name}");
+                Entity carried = pack.Items[slot];
+
+                // Marked rather than listed separately: one list, and the letters stay stable.
+                bool worn = world.Player.Equipment is not null
+                    && world.Player.Equipment.IsEquipped(carried);
+
+                string marker = worn ? " (equipped)" : string.Empty;
+
+                contents.Add($"{(char)('a' + slot)}) {carried.Name}{marker}");
             }
         }
 
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

    // The aiming cursor. Bright, because it must be findable at a glance.
    private static readonly Color Crosshair = new Color(255, 255, 120);

    // Cells a blast will reach. Dim orange over whatever is underneath.
    private static readonly Color BlastArea = new Color(180, 90, 40);

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

        // The crosshair goes on last so nothing can be drawn over it, and the blast is drawn
        // before it so the cursor stays visible in the middle of its own splash.
        if (world.Aiming is not null)
        {
            WriteTargeting(world.Aiming, layout, glyphs, foregrounds);
        }

        // Over everything, because a question the player has to answer must not be behind
        // anything else on the screen.
        if (world.Mode == GameMode.ConfirmingNewGame)
        {
            WriteConfirmation(layout, glyphs, foregrounds);
        }

        if (world.Mode == GameMode.ChoosingLevelUp)
        {
            WriteLevelUp(world, layout, glyphs, foregrounds);
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

        // The level and progress sit after the bar, which is why the bar is a fixed width and
        // not the window's.
        string progress = world.Player.Level is null
            ? string.Empty
            : $"  Lv {world.Player.Level.CurrentLevel}  XP {world.Player.Level.Experience}/{world.Player.Level.ExperienceForNextLevel}";

        // How deep this is goes last, because it is the number that stops changing between
        // floors and the eye can leave it alone.
        progress += $"  Floor {world.Depth}";

        for (int col = 0; col < progress.Length && HealthBarWidth + col < layout.WindowWidth; col++)
        {
            glyphs[rowStart + HealthBarWidth + col] = progress[col];
            foregrounds[rowStart + HealthBarWidth + col] = PanelText;
        }

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
                Entity carried = pack.Items[slot];

                // Marked rather than listed separately: one list, and the letters stay stable.
                bool worn = world.Player.Equipment is not null
                    && world.Player.Equipment.IsEquipped(carried);

                string marker = worn ? " (equipped)" : string.Empty;

                contents.Add($"{(char)('a' + slot)}) {carried.Name}{marker}");
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

    // Draws the level up menu, with the numbers each choice would change.
    private static void WriteLevelUp(GameWorld world, ScreenLayout layout, char[] glyphs, Color[] foregrounds)
    {
        Fighter? fighter = world.Player.Fighter;

        if (fighter is null)
        {
            return;
        }

        int width = Math.Min(InventoryWidth, layout.WindowWidth);
        int inner = width - 4;

        List<string> lines = new List<string>
        {
            "+" + new string('-', width - 2) + "+",
            "| " + $"Level {world.Player.Level!.CurrentLevel + 1}".PadRight(inner) + " |",
            "| " + "".PadRight(inner) + " |",
        };

        for (int slot = 0; slot < LevelUpChoices.All.Count; slot++)
        {
            // The numbers are shown, not just the names: a menu that says "stronger" without
            // saying how much is asking for a decision with the information withheld.
            string entry = $"{(char)('a' + slot)}) {LevelUpChoices.Describe(LevelUpChoices.All[slot], fighter)}";

            string fitted = entry.Length > inner ? entry.Substring(0, inner) : entry;

            lines.Add("| " + fitted.PadRight(inner) + " |");
        }

        lines.Add("+" + new string('-', width - 2) + "+");

        for (int line = 0; line < lines.Count && line < layout.MapHeight; line++)
        {
            WriteLine(lines[line], line * layout.WindowWidth, layout.WindowWidth, glyphs, foregrounds, Crosshair);
        }
    }

    // Draws the question about abandoning the run, framed like the pack so it reads as a panel.
    private static void WriteConfirmation(ScreenLayout layout, char[] glyphs, Color[] foregrounds)
    {
        int width = Math.Min(InventoryWidth, layout.WindowWidth);
        int inner = width - 4;

        List<string> lines = new List<string>
        {
            "+" + new string('-', width - 2) + "+",
            "| " + "Abandon this run?".PadRight(inner) + " |",
            "| " + "".PadRight(inner) + " |",
            "| " + "y  yes, delete it and start again".PadRight(inner) + " |",
            "| " + "anything else  no".PadRight(inner) + " |",
            "+" + new string('-', width - 2) + "+",
        };

        for (int line = 0; line < lines.Count && line < layout.MapHeight; line++)
        {
            WriteLine(lines[line], line * layout.WindowWidth, layout.WindowWidth, glyphs, foregrounds, Crosshair);
        }
    }

    // Draws the blast area and the crosshair, so the player can see what the shot will do.
    private static void WriteTargeting(Targeting aiming, ScreenLayout layout, char[] glyphs, Color[] foregrounds)
    {
        // Aiming you cannot see the consequences of is guesswork, so an area effect shows its
        // reach before it is fired rather than after.
        if (aiming.IsAreaEffect)
        {
            for (int row = aiming.Cursor.Y - aiming.Radius; row <= aiming.Cursor.Y + aiming.Radius; row++)
            {
                for (int col = aiming.Cursor.X - aiming.Radius; col <= aiming.Cursor.X + aiming.Radius; col++)
                {
                    // Inside the map, inside the map area, and not the cursor's own cell.
                    if (col < 0 || col >= layout.WindowWidth || !layout.IsMapRow(row))
                    {
                        continue;
                    }

                    if (col == aiming.Cursor.X && row == aiming.Cursor.Y)
                    {
                        continue;
                    }

                    // The same round test the blast itself uses, so what is shown is what burns.
                    int deltaX = col - aiming.Cursor.X;
                    int deltaY = row - aiming.Cursor.Y;

                    if ((deltaX * deltaX) + (deltaY * deltaY) > aiming.Radius * aiming.Radius)
                    {
                        continue;
                    }

                    // The tile underneath keeps its glyph and is recoloured, so the player can
                    // still read the dungeon through the blast.
                    foregrounds[(row * layout.WindowWidth) + col] = BlastArea;
                }
            }
        }

        if (aiming.Cursor.X < 0 || aiming.Cursor.X >= layout.WindowWidth || !layout.IsMapRow(aiming.Cursor.Y))
        {
            return;
        }

        int index = (aiming.Cursor.Y * layout.WindowWidth) + aiming.Cursor.X;

        glyphs[index] = 'X';
        foregrounds[index] = Crosshair;
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

### [`RogueTutorial/SaveData.cs`](../parts/part-13-equipment/RogueTutorial/SaveData.cs)

The Part 12 file, with the equippable and what is worn.

<!-- generated-diff -->
**Changed from Part 12.** The complete file follows; this is only what moved:

```diff
--- part-12-deeper-levels/SaveData.cs
+++ current/SaveData.cs
@@ -84,6 +84,19 @@
     public int Radius { get; set; }
 }
 
+/// <summary>What a piece of equipment is worth, by name so the file stays readable.</summary>
+internal sealed class SavedEquippable
+{
+    /// <summary>Where it is worn.</summary>
+    public string Slot { get; set; } = string.Empty;
+
+    /// <summary>What it adds to attack.</summary>
+    public int AttackBonus { get; set; }
+
+    /// <summary>What it adds to defence.</summary>
+    public int DefenceBonus { get; set; }
+}
+
 /// <summary>One entity, with an id so other records can point at it.</summary>
 internal sealed class SavedEntity
 {
@@ -116,6 +129,15 @@
 
     /// <summary>What it does when used, or null.</summary>
     public SavedConsumable? Consumable { get; set; }
+
+    /// <summary>What this does when worn, or null when it is not equipment.</summary>
+    public SavedEquippable? Equippable { get; set; }
+
+    /// <summary>
+    /// The ids of what this entity is wearing, or null when it wears nothing ever. Ids rather
+    /// than records, because the items are already written once as entities.
+    /// </summary>
+    public List<int>? EquippedIds { get; set; }
 
     /// <summary>How much it can carry, or null when it carries nothing ever.</summary>
     public int? InventoryCapacity { get; set; }
```
<!-- generated-diff -->

```csharp
/*
 * The game written down: plain records holding exactly what a save has to remember.
 *
 * These are separate types rather than attributes on the game classes, and that is the whole
 * design. A save format is a promise to a file somebody already has on disk; the game classes
 * change every part. Keeping them apart means a rename inside GameWorld does not silently break
 * every existing save, and it means the format is one file somebody can read to see what is
 * stored.
 *
 * What is not here is as deliberate as what is. The mode, the aiming cursor and the screen
 * layout are how the player is looking at the game rather than what the game is - restore them
 * and a save made mid-aim reopens with a crosshair over a scroll that was never fired.
 *
 * Usage - these are only ever built and read by SaveGame:
 *
 *     SavedWorld saved = SaveGame.Capture(world);
 *     string json = SaveGame.ToJson(saved);
 *     GameWorld restored = SaveGame.Restore(SaveGame.FromJson(json));
 *
 * Entities carry an id because the same entity is referenced from more than one place: the
 * player is in the entity list and named separately, and an item is either in the pack or on the
 * map. Writing the object twice would restore two of it.
 */

using System.Collections.Generic;

namespace RogueTutorial;

/// <summary>One tile, as stored. Only what cannot be recomputed.</summary>
internal sealed class SavedTile
{
    /// <summary>The character drawn for this cell.</summary>
    public char Glyph { get; set; }

    /// <summary>Packed colour, so a tile is four numbers rather than an object.</summary>
    public uint Foreground { get; set; }

    /// <summary>Whether a creature may stand here.</summary>
    public bool IsWalkable { get; set; }

    /// <summary>Whether sight passes through.</summary>
    public bool IsTransparent { get; set; }
}

/// <summary>An entity's combat numbers, or absent when it cannot fight.</summary>
internal sealed class SavedFighter
{
    /// <summary>Hit points when undamaged.</summary>
    public int MaximumHitPoints { get; set; }

    /// <summary>Hit points now.</summary>
    public int HitPoints { get; set; }

    /// <summary>How hard it hits.</summary>
    public int Attack { get; set; }

    /// <summary>How much it shrugs off.</summary>
    public int Defence { get; set; }

    /// <summary>How much killing it is worth.</summary>
    public int ExperienceAwarded { get; set; }
}

/// <summary>How far along a fighter is, or absent when it collects no experience.</summary>
internal sealed class SavedLevel
{
    /// <summary>Levels gained so far.</summary>
    public int CurrentLevel { get; set; }

    /// <summary>Experience earned toward the next one.</summary>
    public int Experience { get; set; }
}

/// <summary>What an item does, or absent when it is not an item.</summary>
internal sealed class SavedConsumable
{
    /// <summary>Which effect, stored by name so a reordered enum does not change meaning.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>How much it does it by.</summary>
    public int Power { get; set; }

    /// <summary>How far the effect spreads.</summary>
    public int Radius { get; set; }
}

/// <summary>What a piece of equipment is worth, by name so the file stays readable.</summary>
internal sealed class SavedEquippable
{
    /// <summary>Where it is worn.</summary>
    public string Slot { get; set; } = string.Empty;

    /// <summary>What it adds to attack.</summary>
    public int AttackBonus { get; set; }

    /// <summary>What it adds to defence.</summary>
    public int DefenceBonus { get; set; }
}

/// <summary>One entity, with an id so other records can point at it.</summary>
internal sealed class SavedEntity
{
    /// <summary>Unique within one save. References elsewhere are these numbers.</summary>
    public int Id { get; set; }

    /// <summary>What it is called.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The character drawn for it.</summary>
    public char Glyph { get; set; }

    /// <summary>Packed colour.</summary>
    public uint Foreground { get; set; }

    /// <summary>Where it stands, as a column.</summary>
    public int X { get; set; }

    /// <summary>Where it stands, as a row.</summary>
    public int Y { get; set; }

    /// <summary>Whether it holds its cell against others.</summary>
    public bool BlocksMovement { get; set; }

    /// <summary>Which layer this is drawn on, by name so the file stays readable.</summary>
    public string Layer { get; set; } = string.Empty;

    /// <summary>Its combat numbers, or null.</summary>
    public SavedFighter? Fighter { get; set; }

    /// <summary>What it does when used, or null.</summary>
    public SavedConsumable? Consumable { get; set; }

    /// <summary>What this does when worn, or null when it is not equipment.</summary>
    public SavedEquippable? Equippable { get; set; }

    /// <summary>
    /// The ids of what this entity is wearing, or null when it wears nothing ever. Ids rather
    /// than records, because the items are already written once as entities.
    /// </summary>
    public List<int>? EquippedIds { get; set; }

    /// <summary>How much it can carry, or null when it carries nothing ever.</summary>
    public int? InventoryCapacity { get; set; }

    /// <summary>The ids of what it carries, in slot order.</summary>
    public List<int> CarriedIds { get; set; } = new List<int>();

    /// <summary>How far along it is, or null when it collects no experience.</summary>
    public SavedLevel? Level { get; set; }
}

/// <summary>A whole game, as stored.</summary>
internal sealed class SavedWorld
{
    /// <summary>
    /// The format's version. A save written by a different version is refused rather than
    /// half-read, because a half-read save is a corrupt game that looks like a working one.
    /// </summary>
    public int Version { get; set; }

    /// <summary>Map width in cells.</summary>
    public int Width { get; set; }

    /// <summary>Map height in cells.</summary>
    public int Height { get; set; }

    /// <summary>Which floor the player was on. Floors count from one.</summary>
    public int Depth { get; set; }

    /// <summary>
    /// The distinct tiles this map uses. A dungeon has two kinds and a thousand cells, so the
    /// kinds are listed once and the cells refer to them by position in this list.
    /// </summary>
    public List<SavedTile> TilePalette { get; set; } = new List<SavedTile>();

    /// <summary>
    /// One character per cell, row-major, each an index into TilePalette offset from 'a'. A
    /// character rather than a number so the map is one line per row in the file, which is what
    /// makes a save something a person can actually read.
    /// </summary>
    public List<string> TileRows { get; set; } = new List<string>();

    /// <summary>
    /// One character per cell, row-major: '#' where the player has been, '.' where they have
    /// not. Stored the same way and for the same reason.
    /// </summary>
    public List<string> RememberedRows { get; set; } = new List<string>();

    /// <summary>Everything in the dungeon and everything carried, in draw order.</summary>
    public List<SavedEntity> Entities { get; set; } = new List<SavedEntity>();

    /// <summary>Which entity the keyboard drives.</summary>
    public int PlayerId { get; set; }

    /// <summary>What has happened lately, oldest first.</summary>
    public List<string> Log { get; set; } = new List<string>();
}
```

### [`RogueTutorial/SaveGame.cs`](../parts/part-13-equipment/RogueTutorial/SaveGame.cs)

The Part 12 file, at format version 4.

<!-- generated-diff -->
**Changed from Part 12.** The complete file follows; this is only what moved:

```diff
--- part-12-deeper-levels/SaveGame.cs
+++ current/SaveGame.cs
@@ -38,7 +38,7 @@
     // Version 2 added experience and levels. A version 1 save has no record of either, so
     // resuming one would silently reset a character - which is exactly the case this constant
     // was put here for in Part 10.
-    private const int CurrentVersion = 3;
+    private const int CurrentVersion = 4;
 
     // Indented, because a save you can read in a text editor is a save you can debug.
     private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
@@ -295,6 +295,25 @@
             byId[entity.Id].Inventory = pack;
         }
 
+        foreach (SavedEntity entity in saved.Entities)
+        {
+            if (entity.EquippedIds is null)
+            {
+                continue;
+            }
+
+            Equipment worn = new Equipment();
+
+            // Re-equipped through Equip rather than assigned, so a save cannot produce a state
+            // the game could not have reached - two weapons in one slot, say.
+            foreach (int equippedId in entity.EquippedIds)
+            {
+                worn.Equip(byId[equippedId]);
+            }
+
+            byId[entity.Id].Equipment = worn;
+        }
+
         // Only what was on the map goes back into the entity list; carried things live in packs.
         HashSet<int> carried = new HashSet<int>(saved.Entities.SelectMany(entity => entity.CarriedIds));
 
@@ -382,6 +401,15 @@
                 Power = entity.Consumable.Power,
                 Radius = entity.Consumable.Radius,
             },
+            Equippable = entity.Equippable is null ? null : new SavedEquippable
+            {
+                Slot = entity.Equippable.Slot.ToString(),
+                AttackBonus = entity.Equippable.AttackBonus,
+                DefenceBonus = entity.Equippable.DefenceBonus,
+            },
+            EquippedIds = entity.Equipment is null
+                ? null
+                : entity.Equipment.Worn.Select(worn => ids[worn]).ToList(),
             InventoryCapacity = entity.Inventory?.Capacity,
             CarriedIds = entity.Inventory is null
                 ? new List<int>()
@@ -445,6 +473,14 @@
                 saved.Consumable.Radius);
         }
 
+        if (saved.Equippable is not null)
+        {
+            entity.Equippable = new Equippable(
+                Enum.Parse<EquipmentSlot>(saved.Equippable.Slot),
+                saved.Equippable.AttackBonus,
+                saved.Equippable.DefenceBonus);
+        }
+
         return entity;
     }
 
```
<!-- generated-diff -->

```csharp
/*
 * Writing a game down and reading it back.
 *
 * Capture and Restore are the pair that matters, and the test that matters is that they compose
 * to the identity: a world, saved and loaded, must draw the frame it drew before. That is the
 * same round-trip argument RenderedFrame.ToText has served since Part 2 - the picture is the
 * thing a player would notice changing, so it is the thing to compare.
 *
 * Usage:
 *
 *     SaveGame.Write(world, "save.json");           // capture and write in one call
 *
 *     if (SaveGame.Exists("save.json"))
 *     {
 *         GameWorld resumed = SaveGame.Read("save.json");
 *     }
 *
 *     SaveGame.Delete("save.json");                 // on death, so the run cannot be replayed
 *
 * Refuses a null argument, a blank path, and a save whose version is not the one this build
 * writes. Reading a file that is not there throws FileNotFoundException rather than returning a
 * fresh game, because silently starting over is the worst possible answer to a missing save.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SadRogue.Primitives;

namespace RogueTutorial;

internal static class SaveGame
{
    // Bumped whenever the shape of SaveData changes. A save from another version is refused.
    //
    // Version 2 added experience and levels. A version 1 save has no record of either, so
    // resuming one would silently reset a character - which is exactly the case this constant
    // was put here for in Part 10.
    private const int CurrentVersion = 4;

    // Indented, because a save you can read in a text editor is a save you can debug.
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        WriteIndented = true,
    };

    /// <summary>True when a save exists at the path. A blank path is simply no save.</summary>
    public static bool Exists(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }

    /// <summary>
    /// Captures a world and writes it to the path, replacing whatever was there. Throws
    /// ArgumentNullException on a null world and ArgumentException on a blank path.
    /// </summary>
    public static void Write(GameWorld world, string path)
    {
        ArgumentNullException.ThrowIfNull(world);
        RejectBlankPath(path);

        File.WriteAllText(path, ToJson(Capture(world)));
    }

    /// <summary>
    /// Reads a save if there is one and it can be read, and returns null otherwise - with
    /// problem describing why, for the log.
    ///
    /// An unreadable save is deleted rather than left, or every start would try and fail on the
    /// same file. Refusing to read it is right; leaving the caller to crash over it is not, and
    /// a player whose save is from an older build would otherwise be unable to start the game
    /// without finding and deleting the file themselves.
    ///
    /// This is separate from Read because it makes a policy decision - throw away what cannot be
    /// read - and policy belongs somewhere a test can reach. Read stays strict for callers that
    /// want the failure.
    /// </summary>
    public static GameWorld? ReadIfReadable(string path, out string? problem)
    {
        problem = null;

        if (!Exists(path))
        {
            return null;
        }

        try
        {
            return Read(path);
        }
        catch (InvalidDataException error)
        {
            problem = error.Message;

            Delete(path);

            return null;
        }
    }

    /// <summary>
    /// Reads a save and rebuilds the world it holds. Throws ArgumentException on a blank path,
    /// FileNotFoundException when there is no save - starting a fresh game instead would silently
    /// discard a run - and InvalidDataException on a save this build cannot read.
    /// </summary>
    public static GameWorld Read(string path)
    {
        RejectBlankPath(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"There is no save at {path}.", path);
        }

        return Restore(FromJson(File.ReadAllText(path)));
    }

    /// <summary>
    /// Removes a save if there is one. Does nothing when there is not, because deleting what is
    /// already gone is the outcome the caller wanted either way.
    /// </summary>
    public static void Delete(string path)
    {
        if (Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Turns a live world into the records that describe it. Throws ArgumentNullException on a
    /// null world.
    /// </summary>
    public static SavedWorld Capture(GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        SavedWorld saved = new SavedWorld
        {
            Version = CurrentVersion,
            Depth = world.Depth,
            Width = world.Map.Width,
            Height = world.Map.Height,
            Log = world.Log.Messages.ToList(),
        };

        // The palette is built as the map is walked: a dungeon uses two kinds of tile across a
        // thousand cells, so listing the kinds once turns the bulk of a save into two characters
        // per cell.
        List<string> paletteKeys = new List<string>();

        for (int row = 0; row < world.Map.Height; row++)
        {
            System.Text.StringBuilder tiles = new System.Text.StringBuilder();
            System.Text.StringBuilder remembered = new System.Text.StringBuilder();

            for (int col = 0; col < world.Map.Width; col++)
            {
                Point cell = new Point(col, row);
                Tile tile = world.Map.GetTile(cell);

                string key = $"{tile.Glyph}{tile.Foreground.PackedValue}{tile.IsWalkable}{tile.IsTransparent}";

                int index = paletteKeys.IndexOf(key);

                if (index < 0)
                {
                    index = paletteKeys.Count;
                    paletteKeys.Add(key);

                    saved.TilePalette.Add(new SavedTile
                    {
                        Glyph = tile.Glyph,
                        Foreground = tile.Foreground.PackedValue,
                        IsWalkable = tile.IsWalkable,
                        IsTransparent = tile.IsTransparent,
                    });
                }

                tiles.Append((char)('a' + index));

                // Only memory is stored. What is visible right now is recomputed on load from
                // where the player is standing, so it can never disagree with the map.
                remembered.Append(
                    world.Visibility.StateAt(cell) != CellVisibility.Unseen ? '#' : '.');
            }

            saved.TileRows.Add(tiles.ToString());
            saved.RememberedRows.Add(remembered.ToString());
        }

        // Ids are assigned here rather than held on Entity, so nothing in the game has to carry
        // a field that exists only for saving.
        Dictionary<Entity, int> ids = new Dictionary<Entity, int>();

        // Everything on the map, then everything carried: a carried item is not in Entities but
        // still has to be written, or the pack comes back empty.
        List<Entity> everything = world.Entities.ToList();

        foreach (Entity carrier in world.Entities)
        {
            if (carrier.Inventory is not null)
            {
                everything.AddRange(carrier.Inventory.Items);
            }
        }

        for (int index = 0; index < everything.Count; index++)
        {
            ids[everything[index]] = index;
        }

        foreach (Entity entity in everything)
        {
            saved.Entities.Add(CaptureEntity(entity, ids));
        }

        saved.PlayerId = ids[world.Player];

        return saved;
    }

    /// <summary>
    /// Rebuilds a world from the records. Throws ArgumentNullException on a null save and
    /// InvalidDataException when the version is not the one this build writes.
    /// </summary>
    public static GameWorld Restore(SavedWorld saved)
    {
        ArgumentNullException.ThrowIfNull(saved);

        // A save from another version is refused rather than half-read. A half-read save is a
        // corrupt game that looks like a working one, which is the worst kind of bug to ship.
        if (saved.Version != CurrentVersion)
        {
            throw new InvalidDataException(
                $"This save is version {saved.Version}; this build reads version {CurrentVersion}.");
        }

        GameMap map = new GameMap(saved.Width, saved.Height);

        // A row of the wrong length would shift the rest of the map by a cell and produce a
        // dungeon that is subtly wrong everywhere rather than obviously wrong once.
        if (saved.TileRows.Count != saved.Height)
        {
            throw new InvalidDataException(
                $"The save holds {saved.TileRows.Count} rows of map for a height of {saved.Height}.");
        }

        for (int row = 0; row < saved.Height; row++)
        {
            string cells = saved.TileRows[row];

            if (cells.Length != saved.Width)
            {
                throw new InvalidDataException(
                    $"Row {row} holds {cells.Length} cells for a width of {saved.Width}.");
            }

            for (int col = 0; col < saved.Width; col++)
            {
                int index = cells[col] - 'a';

                if (index < 0 || index >= saved.TilePalette.Count)
                {
                    throw new InvalidDataException($"Row {row} refers to a tile that is not in the palette.");
                }

                SavedTile tile = saved.TilePalette[index];

                map.SetTile(new Point(col, row), new Tile(
                    tile.Glyph, new Color(tile.Foreground), tile.IsWalkable, tile.IsTransparent));
            }
        }

        // Built before anything references them, so an item can be put into a pack in one pass.
        Dictionary<int, Entity> byId = saved.Entities.ToDictionary(
            entity => entity.Id, RestoreEntity);

        foreach (SavedEntity entity in saved.Entities)
        {
            if (entity.InventoryCapacity is null)
            {
                continue;
            }

            Inventory pack = new Inventory(entity.InventoryCapacity.Value);

            foreach (int carriedId in entity.CarriedIds)
            {
                pack.TryAdd(byId[carriedId]);
            }

            byId[entity.Id].Inventory = pack;
        }

        foreach (SavedEntity entity in saved.Entities)
        {
            if (entity.EquippedIds is null)
            {
                continue;
            }

            Equipment worn = new Equipment();

            // Re-equipped through Equip rather than assigned, so a save cannot produce a state
            // the game could not have reached - two weapons in one slot, say.
            foreach (int equippedId in entity.EquippedIds)
            {
                worn.Equip(byId[equippedId]);
            }

            byId[entity.Id].Equipment = worn;
        }

        // Only what was on the map goes back into the entity list; carried things live in packs.
        HashSet<int> carried = new HashSet<int>(saved.Entities.SelectMany(entity => entity.CarriedIds));

        List<Entity> onTheMap = saved.Entities
            .Where(entity => !carried.Contains(entity.Id))
            .Select(entity => byId[entity.Id])
            .ToList();

        // A save that predates depths would restore as floor zero, which no table accepts.
        if (saved.Depth < 1)
        {
            throw new InvalidDataException($"This save is on floor {saved.Depth}; floors count from one.");
        }

        GameWorld world = new GameWorld(map, onTheMap, byId[saved.PlayerId]);

        world.RestoreDepth(saved.Depth);

        world.RestoreMemory(saved.RememberedRows
            .SelectMany(row => row.Select(cell => cell == '#'))
            .ToList());

        foreach (string message in saved.Log)
        {
            world.Log.Add(message);
        }

        return world;
    }

    /// <summary>Serialises a captured world. Throws ArgumentNullException on null.</summary>
    public static string ToJson(SavedWorld saved)
    {
        ArgumentNullException.ThrowIfNull(saved);

        return JsonSerializer.Serialize(saved, Options);
    }

    /// <summary>
    /// Deserialises a captured world. Throws InvalidDataException on text that is not a save,
    /// rather than letting a JsonException escape from a layer the caller does not know about.
    /// </summary>
    public static SavedWorld FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("The save file is empty.");
        }

        try
        {
            return JsonSerializer.Deserialize<SavedWorld>(json)
                ?? throw new InvalidDataException("The save file holds no game.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("The save file is not readable.", error);
        }
    }

    // One entity, with its components and the ids of whatever it carries.
    private static SavedEntity CaptureEntity(Entity entity, Dictionary<Entity, int> ids)
    {
        return new SavedEntity
        {
            Id = ids[entity],
            Name = entity.Name,
            Glyph = entity.Glyph,
            Foreground = entity.Foreground.PackedValue,
            X = entity.Position.X,
            Y = entity.Position.Y,
            BlocksMovement = entity.BlocksMovement,
            Layer = entity.Layer.ToString(),
            Fighter = entity.Fighter is null ? null : new SavedFighter
            {
                MaximumHitPoints = entity.Fighter.MaximumHitPoints,
                HitPoints = entity.Fighter.HitPoints,
                Attack = entity.Fighter.Attack,
                Defence = entity.Fighter.Defence,
                ExperienceAwarded = entity.Fighter.ExperienceAwarded,
            },
            Consumable = entity.Consumable is null ? null : new SavedConsumable
            {
                Kind = entity.Consumable.Kind.ToString(),
                Power = entity.Consumable.Power,
                Radius = entity.Consumable.Radius,
            },
            Equippable = entity.Equippable is null ? null : new SavedEquippable
            {
                Slot = entity.Equippable.Slot.ToString(),
                AttackBonus = entity.Equippable.AttackBonus,
                DefenceBonus = entity.Equippable.DefenceBonus,
            },
            EquippedIds = entity.Equipment is null
                ? null
                : entity.Equipment.Worn.Select(worn => ids[worn]).ToList(),
            InventoryCapacity = entity.Inventory?.Capacity,
            CarriedIds = entity.Inventory is null
                ? new List<int>()
                : entity.Inventory.Items.Select(item => ids[item]).ToList(),
            Level = entity.Level is null ? null : new SavedLevel
            {
                CurrentLevel = entity.Level.CurrentLevel,
                Experience = entity.Level.Experience,
            },
        };
    }

    // One entity, without its pack: packs are filled once every entity exists.
    private static Entity RestoreEntity(SavedEntity saved)
    {
        Entity entity = new Entity(
            saved.Name,
            saved.Glyph,
            new Color(saved.Foreground),
            new Point(saved.X, saved.Y),
            saved.BlocksMovement,
            Enum.Parse<RenderLayer>(saved.Layer));

        if (saved.Fighter is not null)
        {
            Fighter fighter = new Fighter(
                saved.Fighter.MaximumHitPoints,
                saved.Fighter.Attack,
                saved.Fighter.Defence,
                saved.Fighter.ExperienceAwarded);

            // Constructed at full health, so the difference is applied as damage rather than by
            // reaching past the class and setting the field.
            fighter.TakeDamage(saved.Fighter.MaximumHitPoints - saved.Fighter.HitPoints);

            entity.Fighter = fighter;
        }

        if (saved.Level is not null)
        {
            Level level = new Level();

            // Rebuilt by replaying rather than by reaching past the class: awarding the total
            // and advancing the levels leaves it in a state the class could have reached itself.
            for (int gained = 1; gained < saved.Level.CurrentLevel; gained++)
            {
                level.Award(level.ExperienceForNextLevel);
                level.Advance();
            }

            level.Award(saved.Level.Experience);

            entity.Level = level;
        }

        if (saved.Consumable is not null)
        {
            entity.Consumable = new Consumable(
                Enum.Parse<ConsumableKind>(saved.Consumable.Kind),
                saved.Consumable.Power,
                saved.Consumable.Radius);
        }

        if (saved.Equippable is not null)
        {
            entity.Equippable = new Equippable(
                Enum.Parse<EquipmentSlot>(saved.Equippable.Slot),
                saved.Equippable.AttackBonus,
                saved.Equippable.DefenceBonus);
        }

        return entity;
    }

    // A path is the caller's, and a blank one is a mistake rather than a default.
    private static void RejectBlankPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A save path cannot be blank.", nameof(path));
        }
    }
}
```

## Step 3: the test files

**Each block below is the complete file.** One is new; two are carried over.

### [`RogueTutorial.Tests/EquipmentTests.cs`](../parts/part-13-equipment/RogueTutorial.Tests/EquipmentTests.cs)

New. Wearing, wielding, dropping, and what reaches combat.

```csharp
/*
 * Unit and integration tests for wearing and wielding.
 *
 * The rule worth watching: equipment changes what a fighter's numbers come out as, without ever
 * changing the numbers themselves. Nothing is added to Fighter on equip and subtracted on
 * unequip, so there is no stored total to drift.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~EquipmentTests
 */

using System;
using System.Collections.Generic;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class EquipmentTests
{
    private static Entity Player()
    {
        Entity player = new Entity(
            "Player", '@', Color.White, new Point(4, 4), blocksMovement: true, RenderLayer.Player);

        player.Fighter = new Fighter(30, 5, 2, experienceAwarded: 0);
        player.Inventory = new Inventory(26);
        player.Equipment = new Equipment();

        return player;
    }

    private static Entity Weapon(string name, int attackBonus)
    {
        Entity weapon = new Entity(
            name, '/', Color.Gray, new Point(0, 0), blocksMovement: false, RenderLayer.Item);

        weapon.Equippable = new Equippable(EquipmentSlot.Weapon, attackBonus, defenceBonus: 0);

        return weapon;
    }

    private static Entity Armour(string name, int defenceBonus)
    {
        Entity armour = new Entity(
            name, '[', Color.Gray, new Point(0, 0), blocksMovement: false, RenderLayer.Item);

        armour.Equippable = new Equippable(EquipmentSlot.Armour, attackBonus: 0, defenceBonus);

        return armour;
    }

    [Fact]
    public void NothingEquippedLeavesTheNumbersAlone()
    {
        Entity player = Player();

        Assert.Equal(5, player.EffectiveAttack);
        Assert.Equal(2, player.EffectiveDefence);
    }

    [Fact]
    public void AWieldedWeaponAddsItsAttack()
    {
        Entity player = Player();
        Entity sword = Weapon("sword", attackBonus: 3);

        player.Equipment!.Equip(sword);

        Assert.Equal(8, player.EffectiveAttack);
    }

    [Fact]
    public void TheFightersOwnNumbersNeverChange()
    {
        // The whole design: nothing is written into Fighter, so nothing can drift out of step.
        Entity player = Player();

        player.Equipment!.Equip(Weapon("sword", attackBonus: 3));
        player.Equipment.Equip(Armour("mail", defenceBonus: 4));

        Assert.Equal(5, player.Fighter!.Attack);
        Assert.Equal(2, player.Fighter.Defence);
    }

    [Fact]
    public void TakingSomethingOffRemovesItsBonus()
    {
        Entity player = Player();
        Entity sword = Weapon("sword", attackBonus: 3);

        player.Equipment!.Equip(sword);
        player.Equipment.Unequip(EquipmentSlot.Weapon);

        Assert.Equal(5, player.EffectiveAttack);
    }

    [Fact]
    public void ASecondWeaponReplacesTheFirst()
    {
        // Two hands is a rule this game does not have, so the old one comes back to the pack.
        Entity player = Player();
        Entity dagger = Weapon("dagger", attackBonus: 1);
        Entity sword = Weapon("sword", attackBonus: 3);

        player.Equipment!.Equip(dagger);
        Entity? displaced = player.Equipment.Equip(sword);

        Assert.Same(dagger, displaced);
        Assert.Equal(8, player.EffectiveAttack);
    }

    [Fact]
    public void ArmourAndAWeaponDoNotCompete()
    {
        Entity player = Player();

        player.Equipment!.Equip(Weapon("sword", attackBonus: 3));
        player.Equipment.Equip(Armour("mail", defenceBonus: 4));

        Assert.Equal(8, player.EffectiveAttack);
        Assert.Equal(6, player.EffectiveDefence);
    }

    [Fact]
    public void EquipmentReachesCombat()
    {
        // Armour that does not change what a blow does is decoration.
        Entity attacker = Player();
        Entity target = Player();

        int bare = Combat.Resolve(attacker, target).DamageDealt;

        target.Equipment!.Equip(Armour("mail", defenceBonus: 2));

        int armoured = Combat.Resolve(attacker, target).DamageDealt;

        Assert.Equal(bare - 2, armoured);
    }

    [Fact]
    public void SomethingWithNoEquippableIsRefused()
    {
        Entity player = Player();
        Entity potion = new Entity(
            "potion", '!', Color.Magenta, new Point(0, 0), blocksMovement: false, RenderLayer.Item);

        Assert.Throws<ArgumentException>(() => player.Equipment!.Equip(potion));
    }

    [Fact]
    public void AnEmptySlotUnequipsToNothing()
    {
        Entity player = Player();

        Assert.Null(player.Equipment!.Unequip(EquipmentSlot.Weapon));
    }

    [Fact]
    public void AMonsterWithNoEquipmentStillFights()
    {
        // Only the player has an Equipment component; everything else reads its Fighter alone.
        Entity rat = new Entity(
            "Rat", 'r', Color.Red, new Point(0, 0), blocksMovement: true, RenderLayer.Actor);
        rat.Fighter = new Fighter(4, 3, 0, experienceAwarded: 10);

        Assert.Equal(3, rat.EffectiveAttack);
        Assert.Equal(0, rat.EffectiveDefence);
    }
    // A world holding one player and whatever else is passed, on open floor.
    private static GameWorld WorldWith(Entity player, params Entity[] others)
    {
        GameMap map = new GameMap(9, 9);
        map.Fill(TileTypes.Floor);

        List<Entity> entities = new List<Entity> { player };
        entities.AddRange(others);

        return new GameWorld(map, entities, player);
    }

    [Fact]
    public void ChoosingEquipmentFromThePackPutsItOn()
    {
        // There is no separate wear key: equipment has no other use, so the use key does it.
        Entity player = Player();
        Entity sword = Weapon("sword", attackBonus: 3);

        player.Inventory!.TryAdd(sword);

        GameWorld world = WorldWith(player);

        Assert.True(world.UseItem(0));
        Assert.True(player.Equipment!.IsEquipped(sword));
        Assert.Equal(8, player.EffectiveAttack);
    }

    [Fact]
    public void ChoosingItAgainTakesItOff()
    {
        Entity player = Player();
        Entity sword = Weapon("sword", attackBonus: 3);

        player.Inventory!.TryAdd(sword);

        GameWorld world = WorldWith(player);

        world.UseItem(0);
        world.UseItem(0);

        Assert.False(player.Equipment!.IsEquipped(sword));
        Assert.Equal(5, player.EffectiveAttack);
    }

    [Fact]
    public void EquippingKeepsItInThePack()
    {
        // Wearing something is a way of using it, not a way of carrying it, so the letters stay
        // where they are and nothing has to be re-learned.
        Entity player = Player();
        Entity sword = Weapon("sword", attackBonus: 3);

        player.Inventory!.TryAdd(sword);

        WorldWith(player).UseItem(0);

        Assert.Contains(sword, player.Inventory.Items);
    }

    [Fact]
    public void DroppingSomethingWornTakesItOffFirst()
    {
        // Otherwise it lies on the floor still adding its bonus.
        Entity player = Player();
        Entity sword = Weapon("sword", attackBonus: 3);

        player.Inventory!.TryAdd(sword);

        GameWorld world = WorldWith(player);

        world.UseItem(0);
        world.DropItem(0);

        Assert.False(player.Equipment!.IsEquipped(sword));
        Assert.Equal(5, player.EffectiveAttack);
    }

    [Fact]
    public void ThePackSaysWhatIsWorn()
    {
        Entity player = Player();
        Entity sword = Weapon("sword", attackBonus: 3);
        Entity potion = new Entity(
            "potion", '!', Color.Magenta, new Point(0, 0), blocksMovement: false, RenderLayer.Item);
        potion.Consumable = new Consumable(ConsumableKind.Healing, power: 4, radius: 0);

        player.Inventory!.TryAdd(sword);
        player.Inventory.TryAdd(potion);

        GameWorld world = WorldWith(player);
        world.UseItem(0);
        world.SetMode(GameMode.ShowingInventory);

        ScreenLayout layout = new ScreenLayout(80, 25, logRows: 5);
        string screen = ScreenComposer.Compose(world, layout).ToText();

        Assert.Contains("sword (equipped)", screen);
        Assert.DoesNotContain("potion (equipped)", screen);
    }

    [Fact]
    public void ANewPlayerWearsNothing()
    {
        GameWorld world = GameWorld.Generate(
            60, 30, new Random(9), MonsterTable.Standard, ItemTable.Standard, depth: 1);

        Assert.NotNull(world.Player.Equipment);
        Assert.Empty(world.Player.Equipment!.Worn);
        Assert.Equal(world.Player.Fighter!.Attack, world.Player.EffectiveAttack);
    }

    [Fact]
    public void TheDungeonContainsEquipment()
    {
        // Nothing to find means the component is unreachable in a real game.
        HashSet<string> found = new HashSet<string>();

        for (int seed = 1; seed <= 30; seed++)
        {
            GameWorld world = GameWorld.Generate(
                60, 30, new Random(seed), MonsterTable.Standard, ItemTable.Standard, depth: 1);

            foreach (Entity entity in world.Entities)
            {
                if (entity.Equippable is not null)
                {
                    found.Add(entity.Name);
                }
            }
        }

        Assert.Contains("dagger", found);
        Assert.Contains("leather armour", found);
    }

    [Fact]
    public void BetterEquipmentIsDeeper()
    {
        // The same rule Part 12 gave monsters: a sword on floor one would skip the early game.
        HashSet<string> shallow = new HashSet<string>();

        for (int seed = 1; seed <= 30; seed++)
        {
            GameWorld world = GameWorld.Generate(
                60, 30, new Random(seed), MonsterTable.Standard, ItemTable.Standard, depth: 1);

            foreach (Entity entity in world.Entities)
            {
                shallow.Add(entity.Name);
            }
        }

        Assert.DoesNotContain("sword", shallow);
        Assert.DoesNotContain("chain mail", shallow);
    }

    [Fact]
    public void EquipmentWithNoBonusIsRejected()
    {
        // Something worth wearing has to be worth something.
        Assert.Throws<ArgumentException>(
            () => new Equippable(EquipmentSlot.Weapon, attackBonus: 0, defenceBonus: 0));
    }

    [Fact]
    public void CursedEquipmentIsRejected()
    {
        // A negative bonus would arrive as an unexplained weakening far from the item.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Equippable(EquipmentSlot.Weapon, attackBonus: -1, defenceBonus: 0));
    }
    [Fact]
    public void EquipmentCanBePickedUpOffTheFloor()
    {
        // Everything else in this part is unreachable if this fails: the dungeon places daggers
        // and armour, and the only way into the pack is standing on one and pressing g.
        Entity player = Player();
        Entity armour = Armour("leather armour", defenceBonus: 1);

        armour.MoveTo(player.Position);

        GameWorld world = WorldWith(player, armour);

        Assert.True(world.PickUpHere());
        Assert.Contains(armour, player.Inventory!.Items);
    }

    [Fact]
    public void PickingUpFindsEquipmentAmongItems()
    {
        // The cell holds a potion and a dagger; both must be reachable, one press each.
        Entity player = Player();
        Entity dagger = Weapon("dagger", attackBonus: 2);
        Entity potion = new Entity(
            "potion", '!', Color.Magenta, player.Position, blocksMovement: false, RenderLayer.Item);
        potion.Consumable = new Consumable(ConsumableKind.Healing, power: 4, radius: 0);

        dagger.MoveTo(player.Position);

        GameWorld world = WorldWith(player, dagger, potion);

        world.PickUpHere();
        world.PickUpHere();

        Assert.Equal(2, player.Inventory!.Items.Count);
    }

    [Fact]
    public void ACorpseIsNotAnItem()
    {
        // Corpses lie on the floor and do not block, so they share a cell with the player more
        // often than anything else does. Nothing without a use and nothing to wear is carryable.
        Entity player = Player();

        Entity rat = new Entity(
            "Rat", 'r', Color.Red, player.Position, blocksMovement: true, RenderLayer.Actor);
        rat.Fighter = new Fighter(4, 3, 0, experienceAwarded: 10);
        rat.Die();

        GameWorld world = WorldWith(player, rat);

        Assert.False(world.PickUpHere());
        Assert.Empty(player.Inventory!.Items);
    }

}
```

### [`RogueTutorial.Tests/FighterTests.cs`](../parts/part-13-equipment/RogueTutorial.Tests/FighterTests.cs)

The Part 11 file, updated for the static damage rule.

<!-- generated-diff -->
**Changed from Part 12.** The complete file follows; this is only what moved:

```diff
--- part-12-deeper-levels/FighterTests.cs
+++ current/FighterTests.cs
@@ -83,20 +83,16 @@
     [InlineData(2, 9, 0)]     // out-defended: floored at zero, never negative
     public void DamageIsAttackLessDefenceFlooredAtZero(int attack, int defence, int expected)
     {
-        Fighter attacker = new Fighter(10, attack, 0, experienceAwarded: 0);
-        Fighter target = new Fighter(10, 0, defence, experienceAwarded: 0);
-
-        Assert.Equal(expected, attacker.DamageAgainst(target));
+        Assert.Equal(expected, Fighter.DamageFrom(attack, defence));
     }
 
     [Fact]
     public void AFighterThatOutDefendsIsNeverHealed()
     {
         // The reason for the floor: without it a heavily armoured target would gain health.
-        Fighter weak = new Fighter(10, 1, 0, experienceAwarded: 0);
         Fighter armoured = new Fighter(10, 0, 5, experienceAwarded: 0);
 
-        armoured.TakeDamage(weak.DamageAgainst(armoured));
+        armoured.TakeDamage(Fighter.DamageFrom(attack: 1, defence: 5));
 
         Assert.Equal(10, armoured.HitPoints);
     }
@@ -125,11 +121,4 @@
         Assert.Throws<ArgumentOutOfRangeException>(() => fighter.TakeDamage(-1));
     }
 
-    [Fact]
-    public void ANullTargetIsRejected()
-    {
-        Fighter fighter = new Fighter(10, 3, 1, experienceAwarded: 0);
-
-        Assert.Throws<ArgumentNullException>(() => fighter.DamageAgainst(null!));
-    }
 }
```
<!-- generated-diff -->

```csharp
/*
 * Unit tests for the combat numbers. Expected values are computed from the rule - damage is
 * attack less defence, floored at zero - rather than from what the code returned.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~FighterTests
 */

using System;
using RogueTutorial;
using Xunit;

public sealed class FighterTests
{
    [Fact]
    public void AFighterStartsAtFullHealth()
    {
        Fighter fighter = new Fighter(maximumHitPoints: 10, attack: 3, defence: 1, experienceAwarded: 0);

        Assert.Equal(10, fighter.MaximumHitPoints);
        Assert.Equal(10, fighter.HitPoints);
        Assert.False(fighter.IsDead);
    }

    [Fact]
    public void DamageComesOffHitPoints()
    {
        Fighter fighter = new Fighter(10, 3, 1, experienceAwarded: 0);

        int lost = fighter.TakeDamage(4);

        Assert.Equal(4, lost);
        Assert.Equal(6, fighter.HitPoints);
    }

    [Fact]
    public void HitPointsFloorAtZero()
    {
        // A corpse is never more dead than another, and a negative total would print as one.
        Fighter fighter = new Fighter(10, 3, 1, experienceAwarded: 0);

        int lost = fighter.TakeDamage(99);

        Assert.Equal(10, lost);
        Assert.Equal(0, fighter.HitPoints);
        Assert.True(fighter.IsDead);
    }

    [Fact]
    public void ExactlyLethalDamageKills()
    {
        Fighter fighter = new Fighter(4, 3, 0, experienceAwarded: 0);

        fighter.TakeDamage(4);

        Assert.True(fighter.IsDead);
    }

    [Fact]
    public void OneShortOfLethalDoesNot()
    {
        // The boundary either side of death, which is where an off-by-one would live.
        Fighter fighter = new Fighter(4, 3, 0, experienceAwarded: 0);

        fighter.TakeDamage(3);

        Assert.False(fighter.IsDead);
        Assert.Equal(1, fighter.HitPoints);
    }

    [Fact]
    public void ZeroDamageChangesNothing()
    {
        Fighter fighter = new Fighter(10, 3, 1, experienceAwarded: 0);

        Assert.Equal(0, fighter.TakeDamage(0));
        Assert.Equal(10, fighter.HitPoints);
    }

    [Theory]
    [InlineData(5, 2, 3)]     // ordinary: 5 attack against 2 defence
    [InlineData(5, 0, 5)]     // no defence at all
    [InlineData(3, 3, 0)]     // defence exactly matches attack
    [InlineData(2, 9, 0)]     // out-defended: floored at zero, never negative
    public void DamageIsAttackLessDefenceFlooredAtZero(int attack, int defence, int expected)
    {
        Assert.Equal(expected, Fighter.DamageFrom(attack, defence));
    }

    [Fact]
    public void AFighterThatOutDefendsIsNeverHealed()
    {
        // The reason for the floor: without it a heavily armoured target would gain health.
        Fighter armoured = new Fighter(10, 0, 5, experienceAwarded: 0);

        armoured.TakeDamage(Fighter.DamageFrom(attack: 1, defence: 5));

        Assert.Equal(10, armoured.HitPoints);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AFighterThatBeginsDeadIsRejected(int maximumHitPoints)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fighter(maximumHitPoints, 3, 1, experienceAwarded: 0));
    }

    [Fact]
    public void NegativeAttackOrDefenceIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fighter(10, -1, 0, experienceAwarded: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fighter(10, 0, -1, experienceAwarded: 0));
    }

    [Fact]
    public void NegativeDamageIsRejected()
    {
        // Healing has its own path in a later part; it must not arrive through this one.
        Fighter fighter = new Fighter(10, 3, 1, experienceAwarded: 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => fighter.TakeDamage(-1));
    }

}
```

### [`RogueTutorial.Tests/SaveGameTests.cs`](../parts/part-13-equipment/RogueTutorial.Tests/SaveGameTests.cs)

The Part 12 file, with equipment in the round trip.

<!-- generated-diff -->
**Changed from Part 12.** The complete file follows; this is only what moved:

```diff
--- part-12-deeper-levels/SaveGameTests.cs
+++ current/SaveGameTests.cs
@@ -526,4 +526,56 @@
         Assert.Contains(resumed.Entities, entity => entity.Layer == RenderLayer.Item);
     }
 
+    [Fact]
+    public void WhatIsWornSurvivesTheRoundTrip()
+    {
+        GameWorld original = GeneratedWorld(4242);
+
+        Entity sword = new Entity(
+            "sword", '/', Color.Gray, original.Player.Position, blocksMovement: false, RenderLayer.Item);
+        sword.Equippable = new Equippable(EquipmentSlot.Weapon, attackBonus: 3, defenceBonus: 0);
+
+        original.Player.Inventory!.TryAdd(sword);
+        original.Player.Equipment!.Equip(sword);
+
+        int attack = original.Player.EffectiveAttack;
+
+        GameWorld resumed = SaveGame.Restore(SaveGame.Capture(original));
+
+        Assert.Equal(attack, resumed.Player.EffectiveAttack);
+        Assert.Single(resumed.Player.Equipment!.Worn);
+    }
+
+    [Fact]
+    public void TheWornItemIsTheSameObjectAsThePackedOne()
+    {
+        // Two copies would mean taking it off left a ghost in the pack still adding its bonus.
+        GameWorld original = GeneratedWorld(4242);
+
+        Entity sword = new Entity(
+            "sword", '/', Color.Gray, original.Player.Position, blocksMovement: false, RenderLayer.Item);
+        sword.Equippable = new Equippable(EquipmentSlot.Weapon, attackBonus: 3, defenceBonus: 0);
+
+        original.Player.Inventory!.TryAdd(sword);
+        original.Player.Equipment!.Equip(sword);
+
+        GameWorld resumed = SaveGame.Restore(SaveGame.Capture(original));
+
+        Entity packed = resumed.Player.Inventory!.Items[0];
+
+        Assert.True(resumed.Player.Equipment!.IsEquipped(packed));
+    }
+
+    [Fact]
+    public void AVersionThreeSaveIsRefused()
+    {
+        // Version 3 recorded no equipment, so resuming one would silently disarm the player.
+        GameWorld original = GeneratedWorld(4242);
+
+        SavedWorld saved = SaveGame.Capture(original);
+        saved.Version = 3;
+
+        Assert.Throws<InvalidDataException>(() => SaveGame.Restore(saved));
+    }
+
 }
```
<!-- generated-diff -->

```csharp
/*
 * Unit tests for writing a game down and reading it back.
 *
 * The test that decides whether any of this works is the round trip: save a world, load it, and
 * compare the composed frame. The picture is what a player would notice changing, so it is what
 * to compare - and it is the same argument RenderedFrame.ToText has served since Part 2.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~SaveGameTests
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class SaveGameTests
{
    // A generated world is a better subject than a hand-built one: it has rooms, corridors,
    // monsters, items and a field of view, which is most of what a save has to carry.
    private static GameWorld GeneratedWorld(int seed)
    {
        return GameWorld.Generate(40, 20, new Random(seed), MonsterTable.Standard, ItemTable.Standard, depth: 1);
    }

    private static string Picture(GameWorld world)
    {
        return world.ComposeFrame().ToText();
    }

    [Fact]
    public void AWorldSurvivesTheRoundTrip()
    {
        // The whole part in one assertion.
        GameWorld original = GeneratedWorld(12345);

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(Picture(original), Picture(restored));
    }

    [Fact]
    public void TheRoundTripSurvivesJsonToo()
    {
        // Capture and Restore agreeing is not enough: the text in between is what reaches disk.
        GameWorld original = GeneratedWorld(7);

        string json = SaveGame.ToJson(SaveGame.Capture(original));

        GameWorld restored = SaveGame.Restore(SaveGame.FromJson(json));

        Assert.Equal(Picture(original), Picture(restored));
    }

    [Fact]
    public void ExploringIsRemembered()
    {
        // A save that forgets where you have been sends you back into a dungeon you have already
        // walked, which is the difference between resuming and starting over.
        GameWorld original = GeneratedWorld(3);

        for (int step = 0; step < 30; step++)
        {
            original.MovePlayer(new Point(1, 0));
            original.MovePlayer(new Point(0, 1));
        }

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(Picture(original), Picture(restored));
    }

    [Fact]
    public void DamageIsRemembered()
    {
        GameWorld original = GeneratedWorld(9);
        original.Player.Fighter!.TakeDamage(11);

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(19, restored.Player.Fighter!.HitPoints);
        Assert.Equal(30, restored.Player.Fighter.MaximumHitPoints);
    }

    [Fact]
    public void CarriedItemsComeBackInThePack()
    {
        GameWorld original = GeneratedWorld(5);

        Entity potion = new Entity("potion", '!', Color.Magenta, original.Player.Position, blocksMovement: false, RenderLayer.Item);
        potion.Consumable = new Consumable(ConsumableKind.Healing, power: 8, radius: 0);
        original.Player.Inventory!.TryAdd(potion);

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Single(restored.Player.Inventory!.Items);
        Assert.Equal("potion", restored.Player.Inventory.Items[0].Name);
        Assert.Equal(ConsumableKind.Healing, restored.Player.Inventory.Items[0].Consumable!.Kind);
    }

    [Fact]
    public void ACarriedItemIsNotAlsoOnTheMap()
    {
        // The reason entities carry an id. An item is in the pack or on the floor, never both,
        // and writing it in two places would restore two of it.
        GameWorld original = GeneratedWorld(5);

        Entity potion = new Entity("potion", '!', Color.Magenta, original.Player.Position, blocksMovement: false, RenderLayer.Item);
        potion.Consumable = new Consumable(ConsumableKind.Healing, power: 8, radius: 0);
        original.Player.Inventory!.TryAdd(potion);

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.DoesNotContain(restored.Entities, entity => entity.Name == "potion");
    }

    [Fact]
    public void ThereIsExactlyOnePlayerAfterLoading()
    {
        // The player is in the entity list and named separately. A naive save writes them twice.
        GameWorld original = GeneratedWorld(11);

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Single(restored.Entities, entity => entity.Name == "Player");
        Assert.Contains(restored.Player, restored.Entities);
    }

    [Fact]
    public void MonstersComeBackWhereTheyWere()
    {
        GameWorld original = GeneratedWorld(2);

        string before = string.Join(";", original.Entities.Select(e => $"{e.Name}{e.Position}"));

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(before, string.Join(";", restored.Entities.Select(e => $"{e.Name}{e.Position}")));
    }

    [Fact]
    public void ACorpseStaysACorpse()
    {
        // A corpse is an entity with no Fighter that does not block. Restoring it as a living
        // monster would put a rat back in a room the player had already cleared.
        GameWorld original = GeneratedWorld(4);

        Entity rat = new Entity("Rat", 'r', Color.Red, new Point(1, 1), blocksMovement: true, RenderLayer.Actor);
        rat.Fighter = new Fighter(4, 3, 0, experienceAwarded: 0);
        rat.Die();

        GameWorld world = new GameWorld(original.Map, new List<Entity> { rat, original.Player }, original.Player);

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(world));

        Entity restoredRat = restored.Entities.First(entity => entity.Name.StartsWith("remains"));

        Assert.Null(restoredRat.Fighter);
        Assert.False(restoredRat.BlocksMovement);
    }

    [Fact]
    public void LevelAndExperienceComeBack()
    {
        GameWorld original = GeneratedWorld(21);

        original.Player.Level!.Award(original.Player.Level.ExperienceForNextLevel + 5);
        original.Player.Level.Advance();
        original.Player.Fighter!.RaiseAttack(1);

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(2, restored.Player.Level!.CurrentLevel);
        Assert.Equal(5, restored.Player.Level.Experience);
        Assert.Equal(6, restored.Player.Fighter!.Attack);
    }

    [Fact]
    public void TheNextThresholdComesBackWithTheLevel()
    {
        // Restoring the level but not what the next one costs would make a level-five character
        // advance at a level-one price.
        GameWorld original = GeneratedWorld(22);

        for (int gained = 0; gained < 3; gained++)
        {
            original.Player.Level!.Award(original.Player.Level.ExperienceForNextLevel);
            original.Player.Level.Advance();
        }

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(
            original.Player.Level!.ExperienceForNextLevel,
            restored.Player.Level!.ExperienceForNextLevel);
    }

    [Fact]
    public void MonstersAwardTheSameExperienceAfterLoading()
    {
        GameWorld original = GeneratedWorld(23);

        string before = string.Join(";", original.Entities
            .Where(entity => entity.Fighter is not null)
            .Select(entity => $"{entity.Name}:{entity.Fighter!.ExperienceAwarded}"));

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(before, string.Join(";", restored.Entities
            .Where(entity => entity.Fighter is not null)
            .Select(entity => $"{entity.Name}:{entity.Fighter!.ExperienceAwarded}")));
    }

    [Fact]
    public void APart10SaveIsRefusedRatherThanResettingTheCharacter()
    {
        // The first real use of the version check. A version 1 save has no record of experience
        // or levels, so resuming one would silently return a levelled character to level one.
        SavedWorld saved = SaveGame.Capture(GeneratedWorld(24));
        saved.Version = 1;

        Assert.Throws<InvalidDataException>(() => SaveGame.Restore(saved));
    }

    [Fact]
    public void TheLogComesBack()
    {
        GameWorld original = GeneratedWorld(6);
        original.Log.Add("You hit the Rat for 3 damage.");
        original.Log.Add("Rat dies.");

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(original.Log.Messages, restored.Log.Messages);
    }

    [Fact]
    public void TheAimingCursorIsNotSaved()
    {
        // How the player is looking at the game is not what the game is. A save made mid-aim
        // must not reopen with a crosshair over a scroll that was never fired.
        GameWorld original = GeneratedWorld(8);

        Entity scroll = new Entity("scroll", '?', Color.Yellow, original.Player.Position, blocksMovement: false, RenderLayer.Item);
        scroll.Consumable = new Consumable(ConsumableKind.Lightning, power: 12, radius: 0);
        original.Player.Inventory!.TryAdd(scroll);

        original.UseItem(0);
        Assert.Equal(GameMode.Targeting, original.Mode);

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(GameMode.Playing, restored.Mode);
        Assert.Null(restored.Aiming);

        // The scroll itself is still carried: only the aiming was transient.
        Assert.Single(restored.Player.Inventory!.Items);
    }

    [Fact]
    public void AFileRoundTripsThroughDisk()
    {
        string path = Path.Combine(Path.GetTempPath(), $"roguetutorial-{Guid.NewGuid():N}.json");

        try
        {
            GameWorld original = GeneratedWorld(13);

            Assert.False(SaveGame.Exists(path));

            SaveGame.Write(original, path);

            Assert.True(SaveGame.Exists(path));
            Assert.Equal(Picture(original), Picture(SaveGame.Read(path)));

            SaveGame.Delete(path);

            Assert.False(SaveGame.Exists(path));
        }
        finally
        {
            // The test must not leave a file behind whether it passed or not.
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void AnUnreadableSaveIsReplacedRatherThanThrown()
    {
        // Refusing to read it is right. Crashing over it is not: a player whose save is from an
        // older build would otherwise be unable to start the game without deleting a file they
        // do not know exists.
        string path = Path.Combine(Path.GetTempPath(), $"roguetutorial-{Guid.NewGuid():N}.json");

        try
        {
            SavedWorld saved = SaveGame.Capture(GeneratedWorld(31));
            saved.Version = 1;

            File.WriteAllText(path, SaveGame.ToJson(saved));

            GameWorld? resumed = SaveGame.ReadIfReadable(path, out string? problem);

            Assert.Null(resumed);
            Assert.NotNull(problem);
            Assert.Contains("version 1", problem);

            // Deleted, or every start would try and fail on the same file forever.
            Assert.False(SaveGame.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void NoSaveIsNotAProblemToReport()
    {
        // Nothing to resume is the ordinary first run, not a fault worth a message.
        string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");

        Assert.Null(SaveGame.ReadIfReadable(path, out string? problem));
        Assert.Null(problem);
    }

    [Fact]
    public void AReadableSaveComesBackUnharmed()
    {
        string path = Path.Combine(Path.GetTempPath(), $"roguetutorial-{Guid.NewGuid():N}.json");

        try
        {
            GameWorld original = GeneratedWorld(32);
            SaveGame.Write(original, path);

            GameWorld? resumed = SaveGame.ReadIfReadable(path, out string? problem);

            Assert.NotNull(resumed);
            Assert.Null(problem);
            Assert.Equal(Picture(original), Picture(resumed));

            // A save that read correctly stays on disk.
            Assert.True(SaveGame.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void CorruptTextIsReplacedToo()
    {
        // Not only a version mismatch: a truncated file has to be survivable as well.
        string path = Path.Combine(Path.GetTempPath(), $"roguetutorial-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(path, "{ \"Version\": 2, \"Width\": ");

            Assert.Null(SaveGame.ReadIfReadable(path, out string? problem));
            Assert.NotNull(problem);
            Assert.False(SaveGame.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void DeletingASaveThatIsNotThereIsFine()
    {
        // Deleting what is already gone is the outcome the caller wanted either way.
        SaveGame.Delete(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json"));
    }

    [Fact]
    public void ReadingAMissingSaveThrowsRatherThanStartingOver()
    {
        // Silently starting a fresh game is the worst possible answer: it discards the run the
        // player was asking for and looks like the save never existed.
        string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");

        Assert.Throws<FileNotFoundException>(() => SaveGame.Read(path));
    }

    [Fact]
    public void ASaveFromAnotherVersionIsRefused()
    {
        // A half-read save is a corrupt game that looks like a working one.
        SavedWorld saved = SaveGame.Capture(GeneratedWorld(1));
        saved.Version = 99;

        Assert.Throws<InvalidDataException>(() => SaveGame.Restore(saved));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("this is not json")]
    [InlineData("{ \"Version\": ")]
    public void UnreadableTextIsRefused(string json)
    {
        Assert.Throws<InvalidDataException>(() => SaveGame.FromJson(json));
    }

    [Fact]
    public void TheMapIsStoredAsOneLinePerRow()
    {
        // A save nobody can read is not the format that was chosen. Storing a record per cell
        // put a forty-by-twenty dungeon in five thousand lines; a palette and a row of letters
        // puts it in twenty, and the room shapes are visible in the file.
        SavedWorld saved = SaveGame.Capture(GeneratedWorld(12345));

        Assert.Equal(20, saved.TileRows.Count);
        Assert.All(saved.TileRows, row => Assert.Equal(40, row.Length));

        // Rock, floor and the stairs down: three entries cover every cell of a floor. The stairs
        // needed no save code of their own, which is the whole reason they are a tile.
        Assert.Equal(3, saved.TilePalette.Count);
    }

    [Fact]
    public void AMapWithTheWrongNumberOfRowsIsRefused()
    {
        // A row short would shift the rest of the dungeon by a cell: subtly wrong everywhere
        // rather than obviously wrong once, which is the harder kind of bug to see.
        SavedWorld saved = SaveGame.Capture(GeneratedWorld(1));
        saved.TileRows.RemoveAt(0);

        Assert.Throws<InvalidDataException>(() => SaveGame.Restore(saved));
    }

    [Fact]
    public void ARowOfTheWrongLengthIsRefused()
    {
        SavedWorld saved = SaveGame.Capture(GeneratedWorld(1));
        saved.TileRows[3] = saved.TileRows[3].Substring(1);

        Assert.Throws<InvalidDataException>(() => SaveGame.Restore(saved));
    }

    [Fact]
    public void ACellPointingOutsideThePaletteIsRefused()
    {
        SavedWorld saved = SaveGame.Capture(GeneratedWorld(1));
        saved.TileRows[2] = "z" + saved.TileRows[2].Substring(1);

        Assert.Throws<InvalidDataException>(() => SaveGame.Restore(saved));
    }

    [Fact]
    public void ABlankPathIsRejected()
    {
        GameWorld world = GeneratedWorld(1);

        Assert.Throws<ArgumentException>(() => SaveGame.Write(world, "   "));
        Assert.Throws<ArgumentException>(() => SaveGame.Read(""));
        Assert.False(SaveGame.Exists(""));
    }

    [Fact]
    public void ANullArgumentIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => SaveGame.Capture(null!));
        Assert.Throws<ArgumentNullException>(() => SaveGame.Restore(null!));
        Assert.Throws<ArgumentNullException>(() => SaveGame.ToJson(null!));
        Assert.Throws<ArgumentNullException>(() => SaveGame.Write(null!, "x.json"));
    }
    [Fact]
    public void TheFloorSurvivesTheRoundTrip()
    {
        // Resuming on floor one after walking down to five would undo the whole descent.
        GameWorld original = GeneratedWorld(4242);

        original.RestoreDepth(5);

        GameWorld resumed = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(5, resumed.Depth);
    }

    [Fact]
    public void ASaveWithNoFloorIsRefused()
    {
        // Version 2 had no depth, so it deserialises as zero. Restoring it would hand the tables
        // a floor number they refuse, far from the file that caused it.
        GameWorld original = GeneratedWorld(4242);

        SavedWorld saved = SaveGame.Capture(original);
        saved.Depth = 0;

        Assert.Throws<InvalidDataException>(() => SaveGame.Restore(saved));
    }

    [Fact]
    public void TheDrawLayerSurvivesTheRoundTrip()
    {
        // Otherwise a resumed game goes back to items covering the monsters standing under them.
        GameWorld original = GeneratedWorld(4242);

        Entity potion = new Entity(
            "potion", '!', Color.Magenta, original.Player.Position, blocksMovement: false, RenderLayer.Item);
        potion.Consumable = new Consumable(ConsumableKind.Healing, power: 4, radius: 0);

        GameWorld resumed = SaveGame.Restore(SaveGame.Capture(
            new GameWorld(original.Map, new[] { original.Player, potion }, original.Player)));

        Assert.Equal(RenderLayer.Player, resumed.Player.Layer);
        Assert.Contains(resumed.Entities, entity => entity.Layer == RenderLayer.Item);
    }

    [Fact]
    public void WhatIsWornSurvivesTheRoundTrip()
    {
        GameWorld original = GeneratedWorld(4242);

        Entity sword = new Entity(
            "sword", '/', Color.Gray, original.Player.Position, blocksMovement: false, RenderLayer.Item);
        sword.Equippable = new Equippable(EquipmentSlot.Weapon, attackBonus: 3, defenceBonus: 0);

        original.Player.Inventory!.TryAdd(sword);
        original.Player.Equipment!.Equip(sword);

        int attack = original.Player.EffectiveAttack;

        GameWorld resumed = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(attack, resumed.Player.EffectiveAttack);
        Assert.Single(resumed.Player.Equipment!.Worn);
    }

    [Fact]
    public void TheWornItemIsTheSameObjectAsThePackedOne()
    {
        // Two copies would mean taking it off left a ghost in the pack still adding its bonus.
        GameWorld original = GeneratedWorld(4242);

        Entity sword = new Entity(
            "sword", '/', Color.Gray, original.Player.Position, blocksMovement: false, RenderLayer.Item);
        sword.Equippable = new Equippable(EquipmentSlot.Weapon, attackBonus: 3, defenceBonus: 0);

        original.Player.Inventory!.TryAdd(sword);
        original.Player.Equipment!.Equip(sword);

        GameWorld resumed = SaveGame.Restore(SaveGame.Capture(original));

        Entity packed = resumed.Player.Inventory!.Items[0];

        Assert.True(resumed.Player.Equipment!.IsEquipped(packed));
    }

    [Fact]
    public void AVersionThreeSaveIsRefused()
    {
        // Version 3 recorded no equipment, so resuming one would silently disarm the player.
        GameWorld original = GeneratedWorld(4242);

        SavedWorld saved = SaveGame.Capture(original);
        saved.Version = 3;

        Assert.Throws<InvalidDataException>(() => SaveGame.Restore(saved));
    }

}
```

## Step 4: build and run

```
dotnet build
dotnet test
dotnet run --project RogueTutorial
```

Expected: a clean build, 470 passing tests, and a dagger somewhere on the first floor.

### If something is wrong

| Symptom | Cause |
|---|---|
| `CS1061: no definition for 'DamageAgainst'` | A caller not yet moved to `Fighter.DamageFrom` |
| `CS7036: no argument for 'slot'` | An `ItemKind` construction not yet moved to a factory |
| Equipment does nothing | `Combat` is reading `Fighter.Attack` rather than `EffectiveAttack` |
| Pressing the letter does nothing | `UseItem` is checking `Consumable` before `Equippable` |
| The bonus stays after taking it off | Something is being written into `Fighter` rather than read |
| A dropped weapon still helps | `DropItem` is not unequipping first |
| The pack shows nothing as equipped | `ScreenComposer` is not asking `IsEquipped` |
| Standing on a `/` or `[` says there is nothing here | `PickUpHere` is testing `Consumable` rather than `IsCarryable` |
| Equipment is gone after loading | The version is still 3, or `EquippedIds` is not being captured |

## Step 5: regenerate the documentation

Skip this if you did not set up docfx in Part 1:

```
dotnet docfx docfx.json --serve --port 8081
```

Expected: `Build succeeded. 0 warning(s) 0 error(s)`, and pages for `Equipment`, `Equippable`
and `EquipmentSlot` at <http://localhost:8081>.

---

That is the tutorial. Thirteen parts, and what you have is a roguelike you can descend, fight,
loot and lose in - built so that almost all of it can be tested without a window open.

Where to go next is a design question rather than a coding one: monsters that drop what they
carried, a bottom floor with something on it, equipment worth choosing between rather than
ranking. The machinery for all three is already here.

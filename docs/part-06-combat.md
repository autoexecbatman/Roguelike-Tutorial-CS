# Part 6: Combat

- [What it is](#what-it-is)
- [How to use it](#how-to-use-it)
- [How to set it up](#how-to-set-it-up)

---

# What it is

Bumping a monster now does something. Monsters take turns, walk toward you, and hit back. Things
die, including you.

Four hundred turns of a player walking at random, seed 4:

```
player hp 9  corpses 2
  Rat hits Player for 1 damage.
  Rat hits Player for 1 damage.
  Rat hits Player for 1 damage.
  Rat hits Player for 1 damage.
  Rat hits Player for 1 damage.
  Rat hits Player for 1 damage.
  Rat hits Player for 1 damage.
  Rat hits Player for 1 damage.
  Player hits Rat for 4 damage. Rat dies.
  Player hits Kobold for 4 damage.
  Kobold hits Player for 2 damage.
  Player hits Kobold for 4 damage. Kobold dies.
```

The numbers are checkable, which is the point of making them deterministic. A rat has 3 attack
against the player's 2 defence, so it does 1. A kobold has 4, so it does 2. The player has 5
against a kobold's 1 defence, so 4.

"Player hits Rat for 4 damage" against a rat with 5 expected? The rat had 4 hit points left. A
blow reports what was actually lost, not what was swung.

## Fighting is a component, not a kind of entity

The obvious design is a `Monster` class deriving from `Entity`. It falls apart at the first
death: a corpse is not a monster any more, and an object cannot change its own type in C#.

So `Fighter` is something an entity *has*:

```csharp
public Fighter? Fighter { get; set; }
```

Null for a corpse, null for an item on the floor, null for a future statue. `Die` is the method
that removes it:

```csharp
public void Die()
{
    Name = $"remains of {Name}";
    Glyph = '%';
    Foreground = new Color(110, 20, 20);

    Fighter = null;
    BlocksMovement = false;
}
```

**Death converts the entity rather than deleting it.** Removing it from the list would mean
editing that list while the turn loop is walking it. Converting means the loop can carry on and
simply skip anything with no `Fighter`.

And `BlocksMovement = false` is the moment Part 5's argument earns its keep - the corpse stops
holding its cell, so you can walk over what you killed.

## Damage has no dice in it

```csharp
public int DamageAgainst(Fighter target)
{
    return Math.Max(0, Attack - target.Defence);
}
```

Attack less defence, floored at zero. Two things follow.

**The floor is not decoration.** Without it, a target whose defence exceeds the attack takes
negative damage, which is healing. A rat would restore an armoured player's health by attacking
them.

**No randomness means no `Random` threaded through combat.** Every test in `FighterTests` and
`CombatTests` states an exact expected number. Variance belongs in Part 12, once there are
numbers worth varying.

## A monster's turn is three lines of rule

```
cannot see the player   ->  do nothing
next to the player      ->  attack
otherwise               ->  step one cell closer
```

"Can see the player" is the *player's* field of view, read backwards. That is only sound because
Part 4's visibility is symmetric - and this is where that work pays for itself. A monster that
could see you from a cell you cannot see into would attack out of the dark, and a player reads
that as the game cheating.

Adjacency is Chebyshev distance, not Manhattan, because movement is eight-way. A diagonal
neighbour that stepped instead of attacking would walk into the player's own square.

## The turn cycle

```
player moves or attacks  ->  every living monster gets one turn  ->  redraw
```

The entity list is snapshotted before the monster round, because a monster may die during it and
`Die` edits the entity in place. Dead ones are skipped rather than removed.

The player dying ends the round immediately rather than letting the remaining monsters pile onto
a corpse.

## `IsPlayerDead` forced an invariant

```csharp
public bool IsPlayerDead => Player.Fighter is null;
```

That reads a missing `Fighter` as death - which is wrong for a player who never had one. The
first run of the carried-over tests failed on exactly this: hand-built worlds gave the player no
`Fighter`, so every one of them started the game already dead.

The fix is not a cleverer check. `GameWorld`'s constructor now requires the player to have a
`Fighter`, so the only way to lose one is to die, and the property means what it says.

## What is deliberately wrong

**Monsters walk straight at you.** No pathfinding, so one stuck behind another simply waits, and
a wall between you and it stops it dead. Real chase behaviour needs a Dijkstra map.

**Nothing is on screen.** The log fills up and nobody reads it; you cannot see your health at
all. Part 7 puts both on screen, which is why the log exists now rather than then.

**Death does nothing.** `IsPlayerDead` goes true, the player stops being able to act, and the
window keeps showing the dungeon. Part 10 decides what actually happens.

**Every rat is identical.** No variance in damage, no criticals, no misses.

---

# How to use it

## Play it

```
cd parts/part-06-combat
dotnet run --project RogueTutorial
```

Find a rat and walk into it. You will not see any numbers - that is Part 7 - but the rat will
die after a couple of blows and leave a dark red `%` you can walk over.

Be careful: you have 30 hit points and nothing restores them.

## Run the tests

```
dotnet test                                  # 240 tests
dotnet test --filter "Category!=EndToEnd"    # no window
```

| Test class | Level | Covers |
|---|---|---|
| [`FighterTests`](../parts/part-06-combat/RogueTutorial.Tests/FighterTests.cs) | unit | the damage rule, and the boundary either side of death |
| [`CombatTests`](../parts/part-06-combat/RogueTutorial.Tests/CombatTests.cs) | unit | resolving a blow, and what death does to an entity |
| [`MessageLogTests`](../parts/part-06-combat/RogueTutorial.Tests/MessageLogTests.cs) | unit | that the log stays bounded |
| [`MonsterTurnTests`](../parts/part-06-combat/RogueTutorial.Tests/MonsterTurnTests.cs) | unit + integration | monster behaviour and the turn cycle |

## Prove the tests can fail

| Change | Expect |
|---|---|
| `DamageAgainst`: drop the `Math.Max(0, ...)` | 3 fail |
| `TakeDamage`: subtract the full damage rather than what is left | 8 fail |
| `Combat.Resolve`: do not call `Die` | 6 fail |
| `MonsterTurn`: adjacency `distance <= 0` | 6 fail |
| `Entity.Die`: leave `BlocksMovement` true | 2 fail |
| `MonsterTurn`: act unless the cell is `Unseen` | 1 fails |

**That last row started as a survivor**, and the reason is worth more than the fix.

`AMonsterThatCannotSeeThePlayerDoesNothing` puts a wall between the two, so the monster's cell is
`Unseen`. But `Unseen` and `Remembered` are both "not visible" - and the test only exercised the
first. A monster standing on a cell the player has seen and walked away from would act, chasing
you down corridors you can no longer see into, and every test passed.

`AMonsterOnARememberedCellDoesNotAct` walks the player away until the monster's cell is
`Remembered` rather than `Unseen`, and asserts it still does nothing. That is the case the rule
is actually about.

The pattern is the same one Part 3's overlap test hit and Part 5's doorway test hit: a test that
passes because of a coincidence in its setup rather than because of the rule it names.

---

# How to set it up

> **You are in:** your project folder, the one holding `RogueTutorial/` and `RogueTutorial.Tests/`

## Step 1: retitle the window

One line in `RogueTutorial/Program.cs`:

```csharp
const string WindowTitle = "Roguelike Tutorial - Part 6: Combat";
```

## Step 2: the source files

**Each block below is the complete file.** Four are new; `Entity.cs`, `MonsterTable.cs`,
`PlayerAction.cs` and `GameWorld.cs` already exist and should be replaced entirely.

**Do not build until every file in this step is in place.** C# compiles a project as a whole, so
a half-finished step fails on files that are perfectly correct - paste `Combat.cs` before
`Entity.cs` has its `Fighter` property and you get:

```
error CS1061: 'Entity' does not contain a definition for 'Fighter'
error CS1061: 'Entity' does not contain a definition for 'Die'
```

Nothing is wrong with `Combat.cs`. The files below are ordered so that each one's dependencies
come before it, but the build only means anything once the last block is in.

Adding the combat numbers to `MonsterKind` breaks every construction of one, and requiring a
player `Fighter` breaks any hand-built world that lacks it. Both are the compiler telling you
where the new rules apply.

### [`RogueTutorial/Fighter.cs`](../parts/part-06-combat/RogueTutorial/Fighter.cs)

Hit points, attack, defence - the component an entity needs in order to fight.

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
 * Refuses a maximum below one, and negative attack, defence or damage. Healing arrives in
 * Part 8 with potions; there is nothing here that raises hit points yet.
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

### [`RogueTutorial/Entity.cs`](../parts/part-06-combat/RogueTutorial/Entity.cs)

The Part 5 file, with the Fighter component and Die. Everything below uses these.

<!-- generated-diff -->
**Changed from Part 5.** The complete file follows; this is only what moved:

```diff
--- part-05-placing-monsters/Entity.cs
+++ current/Entity.cs
@@ -6,8 +6,16 @@
  *
  *     Entity player = new Entity("Player", '@', Color.White, new Point(40, 12), blocksMovement: true);
  *     Entity corpse = new Entity("Corpse", '%', Color.Red, new Point(41, 12), blocksMovement: false);
+ *
+ *     rat.Fighter = new Fighter(maximumHitPoints: 4, attack: 3, defence: 0);
+ *     bool canFight = rat.Fighter is not null;   // -> true until it dies
  *     player.MoveTo(new Point(41, 12));   // unconditional; see MovementRules for the rules
  *     string who = player.Name;           // -> "Player", for messages in a later part
+ *
+ * Fighter is the component that lets an entity take part in combat. It is null for anything that
+ * cannot fight - an item on the floor, or a corpse, which is a monster whose Fighter was removed
+ * when it died. A component rather than a subclass, because an object cannot change its own type
+ * in C# and death has to change what an entity is capable of.
  *
  * blocksMovement is explicit at every call: a creature occupies its cell and nothing else may
  * stand there, while an item on the floor is walked over. There is no default, because guessing
@@ -25,22 +33,28 @@
 internal sealed class Entity
 {
     /// <summary>What this is called, for messages such as "the Villager blocks the way".</summary>
-    public string Name { get; }
+    public string Name { get; private set; }
 
     /// <summary>The character drawn for it.</summary>
-    public char Glyph { get; }
+    public char Glyph { get; private set; }
 
     /// <summary>The colour that character is drawn in.</summary>
-    public Color Foreground { get; }
+    public Color Foreground { get; private set; }
 
     /// <summary>The cell it currently occupies.</summary>
     public Point Position { get; private set; }
 
     /// <summary>
     /// True when nothing else may stand on this entity's cell. Creatures block; items lying on
-    /// the floor do not.
+    /// the floor do not. A corpse stops blocking, which is why this is settable.
     /// </summary>
-    public bool BlocksMovement { get; }
+    public bool BlocksMovement { get; private set; }
+
+    /// <summary>
+    /// This entity's combat numbers, or null when it cannot fight. Set to null by Die, which is
+    /// what turns a monster into a corpse.
+    /// </summary>
+    public Fighter? Fighter { get; set; }
 
     /// <summary>
     /// Creates an entity at a starting cell. Throws ArgumentException on a blank name, since an
@@ -69,4 +83,30 @@
     {
         Position = destination;
     }
+
+    /// <summary>
+    /// Turns this entity into its own corpse: renamed, drawn as a dark red '%', no longer able
+    /// to fight, and no longer blocking the cell it lies on.
+    ///
+    /// The entity is converted rather than removed, because deleting it would mean editing the
+    /// entity list while something is walking it. Throws InvalidOperationException on something
+    /// that was never able to fight, since an item cannot die.
+    /// </summary>
+    public void Die()
+    {
+        if (Fighter is null)
+        {
+            throw new InvalidOperationException($"{Name} has no Fighter and cannot die.");
+        }
+
+        Name = $"remains of {Name}";
+        Glyph = '%';
+        Foreground = new Color(110, 20, 20);
+
+        // Losing the Fighter is what makes it a corpse rather than a fighter at zero health.
+        Fighter = null;
+
+        // A corpse is walked over, which is the case blocksMovement was introduced for.
+        BlocksMovement = false;
+    }
 }
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

### [`RogueTutorial/Combat.cs`](../parts/part-06-combat/RogueTutorial/Combat.cs)

One attack: the damage, the death, and the line to log.

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
            // Die clears the Fighter, so the name is read before it is called.
            string targetName = target.Name;

            target.Die();

            message = $"{message} {targetName} dies.";
        }

        return new AttackResult(dealt, died, message);
    }
}
```

### [`RogueTutorial/MessageLog.cs`](../parts/part-06-combat/RogueTutorial/MessageLog.cs)

What has happened lately, bounded so a long game cannot grow it forever.

```csharp
/*
 * What has happened lately, in the order it happened.
 *
 * The log keeps a bounded number of lines and drops the oldest when it overflows, so a long game
 * cannot grow it without limit. Part 7 draws it on screen; this part only fills it.
 *
 * Usage:
 *
 *     MessageLog log = new MessageLog(capacity: 100);
 *
 *     log.Add("You hit the Rat for 3 damage.");
 *     log.Add("Rat dies.");
 *
 *     IReadOnlyList<string> all = log.Messages;       // oldest first
 *     IReadOnlyList<string> last = log.Latest(5);     // the newest five, still oldest first
 *
 * Refuses a capacity below one, a null or blank message, and a negative count.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RogueTutorial;

internal sealed class MessageLog
{
    // The lines held, oldest first. Trimmed from the front when it passes capacity.
    private readonly List<string> _messages = new List<string>();

    /// <summary>The most lines kept. Older ones are dropped when this is passed.</summary>
    public int Capacity { get; }

    /// <summary>Everything currently held, oldest first.</summary>
    public IReadOnlyList<string> Messages => _messages;

    /// <summary>
    /// Creates an empty log holding at most the given number of lines. Throws
    /// ArgumentOutOfRangeException on a capacity below one, since a log that can hold nothing is
    /// a configuration mistake rather than a way of switching logging off.
    /// </summary>
    public MessageLog(int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "A log must hold at least one message.");
        }

        Capacity = capacity;
    }

    /// <summary>
    /// Appends a line, dropping the oldest if that puts the log over capacity. Throws
    /// ArgumentException on a null, empty or whitespace message: a blank line in a log is a
    /// formatting bug somewhere upstream, and silently keeping it hides the cause.
    /// </summary>
    public void Add(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A log message cannot be blank.", nameof(message));
        }

        _messages.Add(message);

        // One line in, at most one line out, so this never needs to be a loop.
        if (_messages.Count > Capacity)
        {
            _messages.RemoveAt(0);
        }

        Debug.Assert(_messages.Count <= Capacity, "The log must never hold more than its capacity.");
    }

    /// <summary>
    /// The newest lines, still oldest first, so they read top to bottom. Returns everything when
    /// fewer than that many have been logged. Throws ArgumentOutOfRangeException on a negative
    /// count; zero legitimately returns nothing.
    /// </summary>
    public IReadOnlyList<string> Latest(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Cannot take a negative number of messages.");
        }

        // Skipping from the front keeps the order rather than reversing it.
        int skip = Math.Max(0, _messages.Count - count);

        return _messages.Skip(skip).ToList();
    }
}
```

### [`RogueTutorial/MonsterTurn.cs`](../parts/part-06-combat/RogueTutorial/MonsterTurn.cs)

What a monster does with its turn.

```csharp
/*
 * What a monster does with its turn.
 *
 * The rule is short: if it cannot see the player it does nothing, if it is next to the player it
 * attacks, otherwise it takes one step toward them.
 *
 * "Can see the player" is the player's own field of view read backwards, which is only sound
 * because Part 4's visibility is symmetric. A monster that could see you from a cell you cannot
 * see into would shoot from the dark, and a player experiences that as the game cheating.
 *
 * Usage:
 *
 *     string? message = MonsterTurn.Act(rat, world);
 *
 *     // message is null when the monster did nothing worth reporting - it could not see the
 *     // player, or it simply stepped closer. Only an attack produces a line.
 *
 * Refuses a null monster or world, and a monster with no Fighter: a corpse does not take turns.
 */

using System;
using SadRogue.Primitives;

namespace RogueTutorial;

internal static class MonsterTurn
{
    /// <summary>
    /// Runs one monster's turn and returns the line to log, or null when nothing worth reporting
    /// happened. Throws ArgumentNullException on a null argument and ArgumentException on a
    /// monster with no Fighter, since a corpse has no turn to take.
    /// </summary>
    public static string? Act(Entity monster, GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(monster);
        ArgumentNullException.ThrowIfNull(world);

        // A corpse in the turn order means the caller is not filtering the dead out.
        if (monster.Fighter is null)
        {
            throw new ArgumentException($"{monster.Name} has no Fighter and cannot take a turn.", nameof(monster));
        }

        // Symmetric visibility is what makes reading the player's own field of view sound here.
        if (world.Visibility.StateAt(monster.Position) != CellVisibility.Visible)
        {
            return null;
        }

        // Chebyshev distance, because movement is eight-way: a diagonal neighbour is adjacent.
        int distance = Math.Max(
            Math.Abs(monster.Position.X - world.Player.Position.X),
            Math.Abs(monster.Position.Y - world.Player.Position.Y));

        if (distance <= 1)
        {
            // The player may already be dead this turn, killed by a monster acting earlier.
            if (world.Player.Fighter is null)
            {
                return null;
            }

            return Combat.Resolve(monster, world.Player).Message;
        }

        StepToward(monster, world);

        // Walking closer is not worth a line; the player can see it happen.
        return null;
    }

    // Moves one cell toward the player, if the cell in that direction is free.
    private static void StepToward(Entity monster, GameWorld world)
    {
        // One step per axis, so the move is a straight line or a diagonal.
        Point step = new Point(
            Math.Sign(world.Player.Position.X - monster.Position.X),
            Math.Sign(world.Player.Position.Y - monster.Position.Y));

        Point destination = monster.Position + step;

        // Walls stop a monster exactly as they stop the player.
        if (!world.Map.IsWalkable(destination))
        {
            return;
        }

        // Another monster in the way blocks the step. There is no pathfinding yet, so a monster
        // behind another simply waits - which is what makes this the naive version.
        if (world.BlockingEntityAt(destination) is not null)
        {
            return;
        }

        monster.MoveTo(destination);
    }
}
```

### [`RogueTutorial/MonsterTable.cs`](../parts/part-06-combat/RogueTutorial/MonsterTable.cs)

The Part 5 file, with combat numbers on each kind.

<!-- generated-diff -->
**Changed from Part 5.** The complete file follows; this is only what moved:

```diff
--- part-05-placing-monsters/MonsterTable.cs
+++ current/MonsterTable.cs
@@ -12,7 +12,8 @@
  *
  *     // or a table of your own, for a test that wants exactly one kind of monster:
  *     MonsterTable rats = new MonsterTable(
- *         new[] { new MonsterKind("Rat", 'r', Color.Brown, weight: 1) },
+ *         new[] { new MonsterKind("Rat", 'r', Color.Brown, weight: 1,
+ *                     maximumHitPoints: 4, attack: 3, defence: 0) },
  *         maximumPerRoom: 2);
  *
  * Placement never stacks two creatures on one cell and never uses a cell a wall occupies, so a
@@ -39,6 +40,15 @@
     /// <summary>The colour that character is drawn in.</summary>
     public Color Foreground { get; }
 
+    /// <summary>Hit points this kind starts with.</summary>
+    public int MaximumHitPoints { get; }
+
+    /// <summary>How hard this kind hits.</summary>
+    public int Attack { get; }
+
+    /// <summary>How much damage this kind shrugs off per blow.</summary>
+    public int Defence { get; }
+
     /// <summary>
     /// How likely this kind is relative to the others in its table. A kind with weight 3 turns up
     /// three times as often as one with weight 1; the numbers have no meaning on their own.
@@ -50,7 +60,7 @@
     /// ArgumentOutOfRangeException on a weight below one, since a kind that can never be chosen
     /// is a table entry somebody meant to delete.
     /// </summary>
-    public MonsterKind(string name, char glyph, Color foreground, int weight)
+    public MonsterKind(string name, char glyph, Color foreground, int weight, int maximumHitPoints, int attack, int defence)
     {
         if (string.IsNullOrWhiteSpace(name))
         {
@@ -66,6 +76,13 @@
         Glyph = glyph;
         Foreground = foreground;
         Weight = weight;
+        MaximumHitPoints = maximumHitPoints;
+        Attack = attack;
+        Defence = defence;
+
+        // Constructing a Fighter here would throw on bad numbers far from this call site, so
+        // the same rules are enforced where the kind is declared instead.
+        _ = new Fighter(maximumHitPoints, attack, defence);
     }
 }
 
@@ -114,8 +131,10 @@
     public static MonsterTable Standard => new MonsterTable(
         new[]
         {
-            new MonsterKind("Rat", 'r', new Color(150, 120, 90), weight: 3),
-            new MonsterKind("Kobold", 'k', new Color(120, 180, 90), weight: 1),
+            new MonsterKind("Rat", 'r', new Color(150, 120, 90), weight: 3,
+                maximumHitPoints: 4, attack: 3, defence: 0),
+            new MonsterKind("Kobold", 'k', new Color(120, 180, 90), weight: 1,
+                maximumHitPoints: 8, attack: 4, defence: 1),
         },
         maximumPerRoom: 2);
 
@@ -158,7 +177,12 @@
 
             MonsterKind kind = ChooseKind(random);
 
-            placed.Add(new Entity(kind.Name, kind.Glyph, kind.Foreground, cell, blocksMovement: true));
+            Entity placedMonster = new Entity(kind.Name, kind.Glyph, kind.Foreground, cell, blocksMovement: true);
+
+            // The component is what lets it fight; without one it would be scenery.
+            placedMonster.Fighter = new Fighter(kind.MaximumHitPoints, kind.Attack, kind.Defence);
+
+            placed.Add(placedMonster);
         }
 
         return placed;
```
<!-- generated-diff -->

```csharp
/*
 * What lives in the dungeon, and how many of them turn up in a room.
 *
 * The kinds and their weights are data rather than code, so adding a monster is a line in a list
 * and adjusting how common one is does not mean touching placement logic. Part 12 makes the
 * weights vary with depth; this holds one table for the whole dungeon.
 *
 * Usage:
 *
 *     MonsterTable table = MonsterTable.Standard;
 *     IReadOnlyList<Entity> placed = table.PopulateRoom(room, map, new Random(12345));
 *
 *     // or a table of your own, for a test that wants exactly one kind of monster:
 *     MonsterTable rats = new MonsterTable(
 *         new[] { new MonsterKind("Rat", 'r', Color.Brown, weight: 1,
 *                     maximumHitPoints: 4, attack: 3, defence: 0) },
 *         maximumPerRoom: 2);
 *
 * Placement never stacks two creatures on one cell and never uses a cell a wall occupies, so a
 * room can end up with fewer monsters than the maximum. Refuses an empty kind list, a maximum
 * below zero, and any null argument.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using SadRogue.Primitives;

namespace RogueTutorial;

/// <summary>One kind of monster: how it looks, and how often it turns up.</summary>
internal sealed class MonsterKind
{
    /// <summary>What it is called, for messages.</summary>
    public string Name { get; }

    /// <summary>The character drawn for it.</summary>
    public char Glyph { get; }

    /// <summary>The colour that character is drawn in.</summary>
    public Color Foreground { get; }

    /// <summary>Hit points this kind starts with.</summary>
    public int MaximumHitPoints { get; }

    /// <summary>How hard this kind hits.</summary>
    public int Attack { get; }

    /// <summary>How much damage this kind shrugs off per blow.</summary>
    public int Defence { get; }

    /// <summary>
    /// How likely this kind is relative to the others in its table. A kind with weight 3 turns up
    /// three times as often as one with weight 1; the numbers have no meaning on their own.
    /// </summary>
    public int Weight { get; }

    /// <summary>
    /// Records one monster kind. Throws ArgumentException on a blank name and
    /// ArgumentOutOfRangeException on a weight below one, since a kind that can never be chosen
    /// is a table entry somebody meant to delete.
    /// </summary>
    public MonsterKind(string name, char glyph, Color foreground, int weight, int maximumHitPoints, int attack, int defence)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A monster kind needs a name.", nameof(name));
        }

        if (weight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "A weight below one can never be chosen.");
        }

        Name = name;
        Glyph = glyph;
        Foreground = foreground;
        Weight = weight;
        MaximumHitPoints = maximumHitPoints;
        Attack = attack;
        Defence = defence;

        // Constructing a Fighter here would throw on bad numbers far from this call site, so
        // the same rules are enforced where the kind is declared instead.
        _ = new Fighter(maximumHitPoints, attack, defence);
    }
}

internal sealed class MonsterTable
{
    // The kinds that may be placed, with their relative weights.
    private readonly IReadOnlyList<MonsterKind> _kinds;

    // The sum of every weight, computed once because it is needed on every roll.
    private readonly int _totalWeight;

    /// <summary>The most monsters that may be placed in one room.</summary>
    public int MaximumPerRoom { get; }

    /// <summary>
    /// Records the kinds available and how crowded a room may get. Throws ArgumentNullException
    /// on a null list, ArgumentException on an empty one, and ArgumentOutOfRangeException when
    /// the maximum is negative. A maximum of zero is legal and means an empty dungeon.
    /// </summary>
    public MonsterTable(IReadOnlyList<MonsterKind> kinds, int maximumPerRoom)
    {
        ArgumentNullException.ThrowIfNull(kinds);

        // A table with nothing in it cannot answer "which kind", so reject it here rather than
        // failing on the first roll.
        if (kinds.Count == 0)
        {
            throw new ArgumentException("A monster table needs at least one kind.", nameof(kinds));
        }

        if (maximumPerRoom < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPerRoom), maximumPerRoom, "A room cannot hold a negative number of monsters.");
        }

        _kinds = kinds;
        _totalWeight = kinds.Sum(kind => kind.Weight);
        MaximumPerRoom = maximumPerRoom;
    }

    /// <summary>
    /// The table the game uses: rats are common, kobolds less so. Weights are relative, so a rat
    /// turns up three times as often as a kobold.
    /// </summary>
    public static MonsterTable Standard => new MonsterTable(
        new[]
        {
            new MonsterKind("Rat", 'r', new Color(150, 120, 90), weight: 3,
                maximumHitPoints: 4, attack: 3, defence: 0),
            new MonsterKind("Kobold", 'k', new Color(120, 180, 90), weight: 1,
                maximumHitPoints: 8, attack: 4, defence: 1),
        },
        maximumPerRoom: 2);

    /// <summary>
    /// Rolls a number of monsters for the room and places them on distinct walkable cells inside
    /// its walls. Returns fewer than the maximum when the room is small or a roll repeats a cell,
    /// which is preferred to retrying: a generator that sometimes takes a long time is worse than
    /// one that sometimes places a monster fewer. Throws ArgumentNullException on a null argument.
    /// </summary>
    public IReadOnlyList<Entity> PopulateRoom(RectangularRoom room, GameMap map, Random random)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(random);

        // Next's upper bound is exclusive, so + 1 makes MaximumPerRoom reachable.
        int wanted = random.Next(0, MaximumPerRoom + 1);

        List<Entity> placed = new List<Entity>();
        HashSet<Point> taken = new HashSet<Point>();

        for (int monster = 0; monster < wanted; monster++)
        {
            Point cell = new Point(
                random.Next(room.Left + 1, room.Right),
                random.Next(room.Top + 1, room.Bottom));

            // A repeated cell is dropped rather than rerolled, so this loop always terminates.
            if (!taken.Add(cell))
            {
                continue;
            }

            // A pillar or a corridor wall carved through the room leaves cells nothing can
            // stand on, and the room's interior is not guaranteed to be solid floor.
            if (!map.IsWalkable(cell))
            {
                continue;
            }

            MonsterKind kind = ChooseKind(random);

            Entity placedMonster = new Entity(kind.Name, kind.Glyph, kind.Foreground, cell, blocksMovement: true);

            // The component is what lets it fight; without one it would be scenery.
            placedMonster.Fighter = new Fighter(kind.MaximumHitPoints, kind.Attack, kind.Defence);

            placed.Add(placedMonster);
        }

        return placed;
    }

    // Picks a kind at random, each in proportion to its weight.
    private MonsterKind ChooseKind(Random random)
    {
        // A number in [0, totalWeight) lands in exactly one kind's share of the range.
        int roll = random.Next(_totalWeight);

        foreach (MonsterKind kind in _kinds)
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

### [`RogueTutorial/PlayerAction.cs`](../parts/part-06-combat/RogueTutorial/PlayerAction.cs)

The Part 5 file, with Attacked added beside Bumped.

<!-- generated-diff -->
**Changed from Part 5.** The complete file follows; this is only what moved:

```diff
--- part-05-placing-monsters/PlayerAction.cs
+++ current/PlayerAction.cs
@@ -11,12 +11,12 @@
  *
  *     if (action.Kind == PlayerActionKind.Moved) { redraw(); }
  *
- *     if (action.Kind == PlayerActionKind.Bumped)
+ *     if (action.Kind == PlayerActionKind.Attacked)
  *     {
- *         string message = $"You attack the {action.Target!.Name}.";   // Target is set only here
+ *         string what = world.Log.Messages[^1];   // what the blow did
  *     }
  *
- * Target is null for every kind except Bumped, which is the one case where something was hit.
+ * Target is null except for Bumped and Attacked, the two kinds where something was in the way.
  */
 
 using System;
@@ -35,8 +35,11 @@
     /// <summary>A wall refused the move. Nothing changed.</summary>
     BlockedByWall,
 
-    /// <summary>The player walked into a creature. Part 6 makes this an attack.</summary>
+    /// <summary>The player walked into something that blocks but cannot fight.</summary>
     Bumped,
+
+    /// <summary>The player attacked a creature. The log holds what came of it.</summary>
+    Attacked,
 }
 
 internal readonly struct PlayerAction
@@ -63,8 +66,9 @@
     public static PlayerAction BlockedByWall => new PlayerAction(PlayerActionKind.BlockedByWall, null);
 
     /// <summary>
-    /// The player walked into a creature. Throws ArgumentNullException on a null target, since a
-    /// bump with nothing to bump into is a bug in whoever built it.
+    /// The player walked into something that blocks but cannot fight. Throws
+    /// ArgumentNullException on a null target, since a bump with nothing to bump into is a bug
+    /// in whoever built it.
     /// </summary>
     public static PlayerAction BumpedInto(Entity target)
     {
@@ -72,4 +76,15 @@
 
         return new PlayerAction(PlayerActionKind.Bumped, target);
     }
+
+    /// <summary>
+    /// The player attacked a creature. What came of it is in the message log. Throws
+    /// ArgumentNullException on a null target.
+    /// </summary>
+    public static PlayerAction Attacked(Entity target)
+    {
+        ArgumentNullException.ThrowIfNull(target);
+
+        return new PlayerAction(PlayerActionKind.Attacked, target);
+    }
 }
```
<!-- generated-diff -->

```csharp
/*
 * What happened when the player pressed a movement key.
 *
 * The caller needs to tell three outcomes apart: the player moved and the screen must be
 * repainted, a wall refused the move and nothing changed, or the player walked into a creature -
 * which is the attack command, and Part 6 is where it starts doing damage.
 *
 * Usage:
 *
 *     PlayerAction action = world.MovePlayer(new Point(1, 0));
 *
 *     if (action.Kind == PlayerActionKind.Moved) { redraw(); }
 *
 *     if (action.Kind == PlayerActionKind.Attacked)
 *     {
 *         string what = world.Log.Messages[^1];   // what the blow did
 *     }
 *
 * Target is null except for Bumped and Attacked, the two kinds where something was in the way.
 */

using System;

namespace RogueTutorial;

/// <summary>The three outcomes of a movement key.</summary>
internal enum PlayerActionKind
{
    /// <summary>No movement key was pressed, or the offset was zero.</summary>
    None,

    /// <summary>The player moved, and the screen needs repainting.</summary>
    Moved,

    /// <summary>A wall refused the move. Nothing changed.</summary>
    BlockedByWall,

    /// <summary>The player walked into something that blocks but cannot fight.</summary>
    Bumped,

    /// <summary>The player attacked a creature. The log holds what came of it.</summary>
    Attacked,
}

internal readonly struct PlayerAction
{
    /// <summary>Which of the three outcomes this was.</summary>
    public PlayerActionKind Kind { get; }

    /// <summary>What was bumped into, or null for every other kind.</summary>
    public Entity? Target { get; }

    private PlayerAction(PlayerActionKind kind, Entity? target)
    {
        Kind = kind;
        Target = target;
    }

    /// <summary>No movement key was pressed.</summary>
    public static PlayerAction None => new PlayerAction(PlayerActionKind.None, null);

    /// <summary>The player moved to a new cell.</summary>
    public static PlayerAction Moved => new PlayerAction(PlayerActionKind.Moved, null);

    /// <summary>A wall refused the move.</summary>
    public static PlayerAction BlockedByWall => new PlayerAction(PlayerActionKind.BlockedByWall, null);

    /// <summary>
    /// The player walked into something that blocks but cannot fight. Throws
    /// ArgumentNullException on a null target, since a bump with nothing to bump into is a bug
    /// in whoever built it.
    /// </summary>
    public static PlayerAction BumpedInto(Entity target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return new PlayerAction(PlayerActionKind.Bumped, target);
    }

    /// <summary>
    /// The player attacked a creature. What came of it is in the message log. Throws
    /// ArgumentNullException on a null target.
    /// </summary>
    public static PlayerAction Attacked(Entity target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return new PlayerAction(PlayerActionKind.Attacked, target);
    }
}
```

### [`RogueTutorial/GameWorld.cs`](../parts/part-06-combat/RogueTutorial/GameWorld.cs)

The Part 5 file, with the turn cycle and the message log.

<!-- generated-diff -->
**Changed from Part 5.** The complete file follows; this is only what moved:

```diff
--- part-05-placing-monsters/GameWorld.cs
+++ current/GameWorld.cs
@@ -10,7 +10,9 @@
  *
  *     GameWorld world = GameWorld.Generate(80, 25, new Random(12345), MonsterTable.Standard);
  *
- *     world.MovePlayer(new Point(1, 0));                  // one step right, or a bump
+ *     world.MovePlayer(new Point(1, 0));                  // one step right, or an attack
+ *     bool over = world.IsPlayerDead;                      // the game ends when this is true
+ *     IReadOnlyList<string> said = world.Log.Latest(5);    // what just happened
  *     Point where = world.Player.Position;
  *     RenderedFrame frame = world.ComposeFrame();         // what the player currently perceives
  *     Entity? blocker = world.BlockingEntityAt(where);    // null when the cell is clear
@@ -48,11 +50,21 @@
     /// <summary>Everything standing in the dungeon, the player included.</summary>
     public IReadOnlyList<Entity> Entities => _entities;
 
+    /// <summary>What has happened lately. Part 7 puts this on screen.</summary>
+    public MessageLog Log { get; } = new MessageLog(capacity: 100);
+
+    /// <summary>
+    /// True once the player has been killed. Nothing stops the game yet; Part 10 decides what
+    /// happens next, and until then the player simply stops being able to act.
+    /// </summary>
+    public bool IsPlayerDead => Player.Fighter is null;
+
     /// <summary>
     /// Builds a world directly from its parts. Generate is the usual way in; this constructor
     /// exists so a test can hand-build a small world with exactly the monsters it cares about.
     /// Throws ArgumentNullException on a null argument, and ArgumentException when the player is
-    /// not one of the entities, since the player must be drawn and moved like any other.
+    /// not one of the entities - it must be drawn and moved like any other - or has no Fighter,
+    /// since a player who cannot fight would read as already dead.
     /// </summary>
     public GameWorld(GameMap map, IReadOnlyList<Entity> entities, Entity player)
     {
@@ -64,6 +76,13 @@
         if (!entities.Contains(player))
         {
             throw new ArgumentException("The player must be one of the entities.", nameof(player));
+        }
+
+        // IsPlayerDead reads the Fighter being gone as death, so a player who never had one
+        // would start the game already dead. Requiring it here keeps that reading honest.
+        if (player.Fighter is null)
+        {
+            throw new ArgumentException("The player must have a Fighter.", nameof(player));
         }
 
         Map = map;
@@ -93,6 +112,10 @@
 
         Entity player = new Entity("Player", '@', Color.White, dungeon.PlayerStart, blocksMovement: true);
 
+        // The player's numbers: enough health to survive a mistake, enough defence that a rat
+        // is an inconvenience rather than a threat.
+        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2);
+
         List<Entity> entities = new List<Entity> { player };
 
         // The first room is where the player starts, so it is left empty: waking up already
@@ -139,6 +162,12 @@
             return PlayerAction.None;
         }
 
+        // A dead player has no turns left to take.
+        if (IsPlayerDead)
+        {
+            return PlayerAction.None;
+        }
+
         Point destination = Player.Position + offset;
 
         // The map decides first. Bumping a monster standing inside a wall is not a thing.
@@ -151,7 +180,19 @@
         Entity? blocker = BlockingEntityAt(destination);
         if (blocker is not null)
         {
-            return PlayerAction.BumpedInto(blocker);
+            // Something that blocks but cannot fight - a future statue, say - is simply in the
+            // way, and swinging at it would produce a message about hitting furniture.
+            if (blocker.Fighter is null)
+            {
+                return PlayerAction.BumpedInto(blocker);
+            }
+
+            Log.Add(Combat.Resolve(Player, blocker).Message);
+
+            // Attacking spends the turn, so the monsters get theirs.
+            RunMonsterTurns();
+
+            return PlayerAction.Attacked(blocker);
         }
 
         Player.MoveTo(destination);
@@ -160,7 +201,50 @@
         // would see one frame of the view from where they used to stand.
         RecomputeFieldOfView();
 
+        // Moving spends the turn too. Everything the monsters do happens after the player acts.
+        RunMonsterTurns();
+
         return PlayerAction.Moved;
+    }
+
+    /// <summary>
+    /// Gives every living monster one turn, in the order they appear in the entity list. The
+    /// list is snapshotted first because a monster may die during the round, and dead ones are
+    /// skipped rather than removed.
+    /// </summary>
+    private void RunMonsterTurns()
+    {
+        // A dead player takes no more turns, and neither should anything else - the game is over
+        // in every sense that matters until Part 10 says what happens next.
+        if (IsPlayerDead)
+        {
+            return;
+        }
+
+        // Snapshotting is what makes it safe for a monster to die mid-round: Die converts an
+        // entity in place, and this loop must not care.
+        foreach (Entity entity in _entities.ToList())
+        {
+            // The player is not a monster, and a corpse does not act.
+            if (entity == Player || entity.Fighter is null)
+            {
+                continue;
+            }
+
+            string? message = MonsterTurn.Act(entity, this);
+
+            if (message is not null)
+            {
+                Log.Add(message);
+            }
+
+            // The player dying ends the round immediately rather than letting the rest pile on.
+            if (IsPlayerDead)
+            {
+                Log.Add("You die.");
+                return;
+            }
+        }
     }
 
     /// <summary>
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

    /// <summary>What has happened lately. Part 7 puts this on screen.</summary>
    public MessageLog Log { get; } = new MessageLog(capacity: 100);

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
    public static GameWorld Generate(int width, int height, Random random, MonsterTable monsters)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(monsters);

        DungeonSettings settings = new DungeonSettings(maximumRooms: 30, minimumRoomSize: 6, maximumRoomSize: 10);

        GeneratedDungeon dungeon = new DungeonGenerator(settings).Generate(width, height, random);

        Entity player = new Entity("Player", '@', Color.White, dungeon.PlayerStart, blocksMovement: true);

        // The player's numbers: enough health to survive a mistake, enough defence that a rat
        // is an inconvenience rather than a threat.
        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2);

        List<Entity> entities = new List<Entity> { player };

        // The first room is where the player starts, so it is left empty: waking up already
        // surrounded is not a fair opening.
        for (int roomIndex = 1; roomIndex < dungeon.Rooms.Count; roomIndex++)
        {
            entities.AddRange(monsters.PopulateRoom(dungeon.Rooms[roomIndex], dungeon.Map, random));
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

## Step 3: the test files

**Each block below is the complete file.** Four are new; `GameWorldTests.cs` and
`MonsterTableTests.cs` are carried over and need updating for the new arguments.

### [`RogueTutorial.Tests/FighterTests.cs`](../parts/part-06-combat/RogueTutorial.Tests/FighterTests.cs)

The damage rule and the boundaries either side of death.

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
        Fighter fighter = new Fighter(maximumHitPoints: 10, attack: 3, defence: 1);

        Assert.Equal(10, fighter.MaximumHitPoints);
        Assert.Equal(10, fighter.HitPoints);
        Assert.False(fighter.IsDead);
    }

    [Fact]
    public void DamageComesOffHitPoints()
    {
        Fighter fighter = new Fighter(10, 3, 1);

        int lost = fighter.TakeDamage(4);

        Assert.Equal(4, lost);
        Assert.Equal(6, fighter.HitPoints);
    }

    [Fact]
    public void HitPointsFloorAtZero()
    {
        // A corpse is never more dead than another, and a negative total would print as one.
        Fighter fighter = new Fighter(10, 3, 1);

        int lost = fighter.TakeDamage(99);

        Assert.Equal(10, lost);
        Assert.Equal(0, fighter.HitPoints);
        Assert.True(fighter.IsDead);
    }

    [Fact]
    public void ExactlyLethalDamageKills()
    {
        Fighter fighter = new Fighter(4, 3, 0);

        fighter.TakeDamage(4);

        Assert.True(fighter.IsDead);
    }

    [Fact]
    public void OneShortOfLethalDoesNot()
    {
        // The boundary either side of death, which is where an off-by-one would live.
        Fighter fighter = new Fighter(4, 3, 0);

        fighter.TakeDamage(3);

        Assert.False(fighter.IsDead);
        Assert.Equal(1, fighter.HitPoints);
    }

    [Fact]
    public void ZeroDamageChangesNothing()
    {
        Fighter fighter = new Fighter(10, 3, 1);

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
        Fighter attacker = new Fighter(10, attack, 0);
        Fighter target = new Fighter(10, 0, defence);

        Assert.Equal(expected, attacker.DamageAgainst(target));
    }

    [Fact]
    public void AFighterThatOutDefendsIsNeverHealed()
    {
        // The reason for the floor: without it a heavily armoured target would gain health.
        Fighter weak = new Fighter(10, 1, 0);
        Fighter armoured = new Fighter(10, 0, 5);

        armoured.TakeDamage(weak.DamageAgainst(armoured));

        Assert.Equal(10, armoured.HitPoints);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AFighterThatBeginsDeadIsRejected(int maximumHitPoints)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fighter(maximumHitPoints, 3, 1));
    }

    [Fact]
    public void NegativeAttackOrDefenceIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fighter(10, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fighter(10, 0, -1));
    }

    [Fact]
    public void NegativeDamageIsRejected()
    {
        // Healing has its own path in a later part; it must not arrive through this one.
        Fighter fighter = new Fighter(10, 3, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => fighter.TakeDamage(-1));
    }

    [Fact]
    public void ANullTargetIsRejected()
    {
        Fighter fighter = new Fighter(10, 3, 1);

        Assert.Throws<ArgumentNullException>(() => fighter.DamageAgainst(null!));
    }
}
```

### [`RogueTutorial.Tests/CombatTests.cs`](../parts/part-06-combat/RogueTutorial.Tests/CombatTests.cs)

Resolving a blow, and what death does to an entity.

```csharp
/*
 * Unit tests for resolving one attack, including what death does to an entity.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~CombatTests
 */

using System;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class CombatTests
{
    private static Entity FighterEntity(string name, int hitPoints, int attack, int defence)
    {
        Entity entity = new Entity(name, name[0], Color.White, new Point(0, 0), blocksMovement: true);
        entity.Fighter = new Fighter(hitPoints, attack, defence);
        return entity;
    }

    [Fact]
    public void AHitRemovesHitPoints()
    {
        Entity attacker = FighterEntity("Player", 30, 5, 2);
        Entity target = FighterEntity("Rat", 10, 3, 1);

        AttackResult result = Combat.Resolve(attacker, target);

        // 5 attack less 1 defence.
        Assert.Equal(4, result.DamageDealt);
        Assert.Equal(6, target.Fighter!.HitPoints);
        Assert.False(result.TargetDied);
    }

    [Fact]
    public void AnAbsorbedBlowStillHappened()
    {
        // Zero damage is not "nothing occurred": the log should say the blow landed and failed.
        Entity attacker = FighterEntity("Rat", 10, 2, 0);
        Entity target = FighterEntity("Knight", 30, 5, 9);

        AttackResult result = Combat.Resolve(attacker, target);

        Assert.Equal(0, result.DamageDealt);
        Assert.False(result.TargetDied);
        Assert.Contains("no damage", result.Message);
        Assert.Equal(30, target.Fighter!.HitPoints);
    }

    [Fact]
    public void ALethalBlowKillsAndSaysSo()
    {
        Entity attacker = FighterEntity("Player", 30, 5, 2);
        Entity target = FighterEntity("Rat", 4, 3, 1);

        AttackResult result = Combat.Resolve(attacker, target);

        Assert.True(result.TargetDied);
        Assert.Contains("dies", result.Message);
    }

    [Fact]
    public void TheMessageNamesTheTargetBeforeItBecomesACorpse()
    {
        // Die renames the entity, so a message built afterwards would read "remains of Rat dies".
        Entity attacker = FighterEntity("Player", 30, 9, 0);
        Entity target = FighterEntity("Rat", 4, 3, 0);

        AttackResult result = Combat.Resolve(attacker, target);

        Assert.Contains("Rat dies", result.Message);
        Assert.DoesNotContain("remains of Rat dies", result.Message);
    }

    [Fact]
    public void DeathTurnsAMonsterIntoACorpse()
    {
        Entity attacker = FighterEntity("Player", 30, 9, 0);
        Entity target = FighterEntity("Rat", 4, 3, 0);

        Combat.Resolve(attacker, target);

        // The three things that make it a corpse rather than a fighter at zero health.
        Assert.Null(target.Fighter);
        Assert.False(target.BlocksMovement);
        Assert.Equal('%', target.Glyph);
        Assert.Equal("remains of Rat", target.Name);
    }

    [Fact]
    public void ACorpseCanBeWalkedOver()
    {
        // The whole reason death converts rather than deletes: the cell must free up.
        Entity attacker = FighterEntity("Player", 30, 9, 0);
        Entity target = FighterEntity("Rat", 4, 3, 0);

        Assert.True(target.BlocksMovement);

        Combat.Resolve(attacker, target);

        Assert.False(target.BlocksMovement);
    }

    [Fact]
    public void ACorpseCannotBeAttackedAgain()
    {
        Entity attacker = FighterEntity("Player", 30, 9, 0);
        Entity target = FighterEntity("Rat", 4, 3, 0);

        Combat.Resolve(attacker, target);

        Assert.Throws<ArgumentException>(() => Combat.Resolve(attacker, target));
    }

    [Fact]
    public void SomethingWithNoFighterCannotAttack()
    {
        Entity item = new Entity("Sword", '/', Color.Gray, new Point(0, 0), blocksMovement: false);
        Entity target = FighterEntity("Rat", 4, 3, 0);

        Assert.Throws<ArgumentException>(() => Combat.Resolve(item, target));
    }

    [Fact]
    public void AnItemCannotDie()
    {
        Entity item = new Entity("Sword", '/', Color.Gray, new Point(0, 0), blocksMovement: false);

        Assert.Throws<InvalidOperationException>(() => item.Die());
    }

    [Fact]
    public void ANullArgumentIsRejected()
    {
        Entity fighter = FighterEntity("Rat", 4, 3, 0);

        Assert.Throws<ArgumentNullException>(() => Combat.Resolve(null!, fighter));
        Assert.Throws<ArgumentNullException>(() => Combat.Resolve(fighter, null!));
    }
}
```

### [`RogueTutorial.Tests/MessageLogTests.cs`](../parts/part-06-combat/RogueTutorial.Tests/MessageLogTests.cs)

Chiefly that the log stays bounded.

```csharp
/*
 * Unit tests for the message log. The property that matters is that it stays bounded: a long
 * game must not grow it without limit.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~MessageLogTests
 */

using System;
using System.Linq;
using RogueTutorial;
using Xunit;

public sealed class MessageLogTests
{
    [Fact]
    public void ANewLogIsEmpty()
    {
        Assert.Empty(new MessageLog(10).Messages);
    }

    [Fact]
    public void MessagesComeBackInTheOrderTheyWereAdded()
    {
        MessageLog log = new MessageLog(10);

        log.Add("first");
        log.Add("second");
        log.Add("third");

        Assert.Equal(new[] { "first", "second", "third" }, log.Messages);
    }

    [Fact]
    public void TheOldestIsDroppedAtCapacity()
    {
        MessageLog log = new MessageLog(capacity: 3);

        log.Add("one");
        log.Add("two");
        log.Add("three");
        log.Add("four");

        Assert.Equal(new[] { "two", "three", "four" }, log.Messages);
    }

    [Fact]
    public void TheLogNeverGrowsPastItsCapacity()
    {
        MessageLog log = new MessageLog(capacity: 5);

        for (int turn = 0; turn < 500; turn++)
        {
            log.Add($"turn {turn}");
        }

        Assert.Equal(5, log.Messages.Count);
        Assert.Equal("turn 499", log.Messages.Last());
    }

    [Fact]
    public void LatestReturnsTheNewestStillOldestFirst()
    {
        // Oldest first, so the caller can print them top to bottom without reversing.
        MessageLog log = new MessageLog(10);

        log.Add("one");
        log.Add("two");
        log.Add("three");

        Assert.Equal(new[] { "two", "three" }, log.Latest(2));
    }

    [Fact]
    public void LatestReturnsEverythingWhenAskedForMoreThanExists()
    {
        MessageLog log = new MessageLog(10);

        log.Add("only");

        Assert.Equal(new[] { "only" }, log.Latest(5));
    }

    [Fact]
    public void LatestOfNoneIsEmpty()
    {
        MessageLog log = new MessageLog(10);

        log.Add("something");

        Assert.Empty(log.Latest(0));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankMessageIsRejected(string? message)
    {
        // A blank line means a formatting bug upstream, and keeping it would hide the cause.
        MessageLog log = new MessageLog(10);

        Assert.Throws<ArgumentException>(() => log.Add(message!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ACapacityThatHoldsNothingIsRejected(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MessageLog(capacity));
    }

    [Fact]
    public void ANegativeCountIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MessageLog(10).Latest(-1));
    }
}
```

### [`RogueTutorial.Tests/MonsterTurnTests.cs`](../parts/part-06-combat/RogueTutorial.Tests/MonsterTurnTests.cs)

Monster behaviour and the whole turn cycle.

```csharp
/*
 * Unit tests for what a monster does with its turn, and for the turn cycle as a whole.
 *
 * Worlds here are hand-built rather than generated, so a monster is put exactly where the test
 * needs it and the outcome is not at the mercy of a seed.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~MonsterTurnTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class MonsterTurnTests
{
    // An open room with the player at (4,4) and whatever monsters the test supplies.
    private static GameWorld WorldWith(Point playerAt, params Entity[] monsters)
    {
        GameMap map = new GameMap(11, 11);
        map.Fill(TileTypes.Wall);

        for (int row = 1; row < 10; row++)
        {
            for (int col = 1; col < 10; col++)
            {
                map.SetTile(new Point(col, row), TileTypes.Floor);
            }
        }

        Entity player = new Entity("Player", '@', Color.White, playerAt, blocksMovement: true);
        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2);

        List<Entity> entities = new List<Entity>(monsters) { player };

        return new GameWorld(map, entities, player);
    }

    private static Entity Monster(string name, Point at, int hitPoints, int attack, int defence)
    {
        Entity monster = new Entity(name, name[0], Color.Red, at, blocksMovement: true);
        monster.Fighter = new Fighter(hitPoints, attack, defence);
        return monster;
    }

    [Fact]
    public void AMonsterStepsTowardThePlayer()
    {
        Entity rat = Monster("Rat", new Point(8, 4), 4, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        MonsterTurn.Act(rat, world);

        Assert.Equal(new Point(7, 4), rat.Position);
    }

    [Fact]
    public void AMonsterStepsDiagonallyWhenThatIsTheDirection()
    {
        Entity rat = Monster("Rat", new Point(7, 7), 4, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        MonsterTurn.Act(rat, world);

        Assert.Equal(new Point(6, 6), rat.Position);
    }

    [Fact]
    public void AnAdjacentMonsterAttacksInsteadOfMoving()
    {
        Entity rat = Monster("Rat", new Point(5, 4), 4, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        string? message = MonsterTurn.Act(rat, world);

        Assert.Equal(new Point(5, 4), rat.Position);
        Assert.NotNull(message);
        Assert.Contains("Rat hits Player", message);

        // 3 attack less 2 defence.
        Assert.Equal(29, world.Player.Fighter!.HitPoints);
    }

    [Fact]
    public void ADiagonallyAdjacentMonsterAlsoAttacks()
    {
        // Movement is eight-way, so adjacency must be Chebyshev rather than Manhattan - a
        // diagonal neighbour that stepped instead of attacking would walk into the player.
        Entity rat = Monster("Rat", new Point(5, 5), 4, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        string? message = MonsterTurn.Act(rat, world);

        Assert.NotNull(message);
        Assert.Equal(new Point(5, 5), rat.Position);
    }

    [Fact]
    public void AMonsterThatCannotSeeThePlayerDoesNothing()
    {
        // A wall between them, so the monster is outside the player's field of view - and by
        // symmetry, cannot see the player either.
        GameMap map = new GameMap(11, 5);
        map.Fill(TileTypes.Wall);

        for (int col = 1; col < 10; col++)
        {
            map.SetTile(new Point(col, 2), TileTypes.Floor);
        }

        map.SetTile(new Point(5, 2), TileTypes.Wall);

        Entity player = new Entity("Player", '@', Color.White, new Point(2, 2), blocksMovement: true);
        player.Fighter = new Fighter(30, 5, 2);

        Entity rat = Monster("Rat", new Point(8, 2), 4, 3, 0);

        GameWorld world = new GameWorld(map, new List<Entity> { rat, player }, player);

        string? message = MonsterTurn.Act(rat, world);

        Assert.Null(message);
        Assert.Equal(new Point(8, 2), rat.Position);
    }

    [Fact]
    public void AMonsterOnARememberedCellDoesNotAct()
    {
        // Stronger than the test above, which uses a cell the player has never seen - there,
        // "not visible" and "never seen" are the same thing. Here the player has seen the cell
        // and walked away, so it is remembered rather than unseen. A monster acting on memory
        // would chase the player through corridors they can no longer see into.
        GameMap map = new GameMap(15, 5);
        map.Fill(TileTypes.Wall);

        for (int col = 1; col < 14; col++)
        {
            map.SetTile(new Point(col, 2), TileTypes.Floor);
        }

        Entity player = new Entity("Player", '@', Color.White, new Point(10, 2), blocksMovement: true);
        player.Fighter = new Fighter(30, 5, 2);

        Entity rat = Monster("Rat", new Point(6, 2), 4, 3, 0);

        GameWorld world = new GameWorld(map, new List<Entity> { rat, player }, player);

        // The rat starts inside the player's sight radius of 8.
        Assert.Equal(CellVisibility.Visible, world.Visibility.StateAt(rat.Position));

        // Walk away until the rat's cell is remembered rather than seen. MovePlayer runs the
        // monsters too, so the rat follows; what matters is the state of the cell it ends on.
        for (int step = 0; step < 3; step++)
        {
            world.MovePlayer(new Point(1, 0));
        }

        Point ratCell = new Point(2, 2);
        rat.MoveTo(ratCell);

        Assert.Equal(CellVisibility.Remembered, world.Visibility.StateAt(ratCell));

        string? message = MonsterTurn.Act(rat, world);

        Assert.Null(message);
        Assert.Equal(ratCell, rat.Position);
    }

    [Fact]
    public void AMonsterWillNotWalkThroughAnother()
    {
        // No pathfinding yet, so a monster behind another simply waits.
        Entity front = Monster("Front", new Point(6, 4), 4, 3, 0);
        Entity behind = Monster("Behind", new Point(7, 4), 4, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), front, behind);

        MonsterTurn.Act(behind, world);

        Assert.Equal(new Point(7, 4), behind.Position);
    }

    [Fact]
    public void AMonsterWillNotWalkIntoAWall()
    {
        // Player directly above the wall row, monster below it: the step is refused.
        GameMap map = new GameMap(7, 7);
        map.Fill(TileTypes.Floor);
        map.SetTile(new Point(3, 3), TileTypes.Wall);

        Entity player = new Entity("Player", '@', Color.White, new Point(3, 2), blocksMovement: true);
        player.Fighter = new Fighter(30, 5, 2);

        Entity rat = Monster("Rat", new Point(3, 4), 4, 3, 0);

        GameWorld world = new GameWorld(map, new List<Entity> { rat, player }, player);

        MonsterTurn.Act(rat, world);

        Assert.Equal(new Point(3, 4), rat.Position);
    }

    [Fact]
    public void ACorpseCannotTakeATurn()
    {
        Entity rat = Monster("Rat", new Point(5, 4), 4, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        rat.Die();

        Assert.Throws<ArgumentException>(() => MonsterTurn.Act(rat, world));
    }

    [Fact]
    public void MonstersActAfterThePlayerMoves()
    {
        // The turn cycle: one player action, then every monster gets one turn.
        Entity rat = Monster("Rat", new Point(6, 4), 4, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        world.MovePlayer(new Point(0, 1));

        // The rat closed the distance during the player's turn.
        Assert.NotEqual(new Point(6, 4), rat.Position);
    }

    [Fact]
    public void AttackingAlsoSpendsTheTurn()
    {
        Entity rat = Monster("Rat", new Point(5, 4), 20, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        world.MovePlayer(new Point(1, 0));

        // The player hit it and it hit back, so both have taken damage.
        Assert.True(rat.Fighter!.HitPoints < 20);
        Assert.True(world.Player.Fighter!.HitPoints < 30);
    }

    [Fact]
    public void ADeadMonsterStopsActing()
    {
        Entity rat = Monster("Rat", new Point(5, 4), 1, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        int healthBefore = world.Player.Fighter!.HitPoints;

        // One blow kills it, so it must not get a turn afterwards.
        world.MovePlayer(new Point(1, 0));

        Assert.Null(rat.Fighter);
        Assert.Equal(healthBefore, world.Player.Fighter!.HitPoints);
    }

    [Fact]
    public void TheGameNoticesWhenThePlayerDies()
    {
        Entity brute = Monster("Brute", new Point(5, 4), 50, 99, 0);
        GameWorld world = WorldWith(new Point(4, 4), brute);

        world.MovePlayer(new Point(0, 1));

        Assert.True(world.IsPlayerDead);
        Assert.Contains("You die.", world.Log.Messages);
    }

    [Fact]
    public void ADeadPlayerTakesNoFurtherTurns()
    {
        Entity brute = Monster("Brute", new Point(5, 4), 50, 99, 0);
        GameWorld world = WorldWith(new Point(4, 4), brute);

        world.MovePlayer(new Point(0, 1));

        Point restingPlace = world.Player.Position;

        PlayerAction action = world.MovePlayer(new Point(1, 0));

        Assert.Equal(PlayerActionKind.None, action.Kind);
        Assert.Equal(restingPlace, world.Player.Position);
    }

    [Fact]
    public void CombatIsWrittenToTheLog()
    {
        Entity rat = Monster("Rat", new Point(5, 4), 20, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        world.MovePlayer(new Point(1, 0));

        Assert.Contains(world.Log.Messages, message => message.Contains("Player hits Rat"));
        Assert.Contains(world.Log.Messages, message => message.Contains("Rat hits Player"));
    }

    [Fact]
    public void ANullArgumentIsRejected()
    {
        Entity rat = Monster("Rat", new Point(5, 4), 4, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        Assert.Throws<ArgumentNullException>(() => MonsterTurn.Act(null!, world));
        Assert.Throws<ArgumentNullException>(() => MonsterTurn.Act(rat, null!));
    }
}
```

### [`RogueTutorial.Tests/GameWorldTests.cs`](../parts/part-06-combat/RogueTutorial.Tests/GameWorldTests.cs)

The Part 5 file, updated for the player now needing a Fighter.

<!-- generated-diff -->
**Changed from Part 5.** The complete file follows; this is only what moved:

```diff
--- part-05-placing-monsters/GameWorldTests.cs
+++ current/GameWorldTests.cs
@@ -33,6 +33,7 @@
         }
 
         Entity player = new Entity("Player", '@', Color.White, new Point(4, 4), blocksMovement: true);
+        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2);
 
         List<Entity> entities = new List<Entity>(extraEntities) { player };
 
@@ -240,6 +241,7 @@
     {
         GameMap map = new GameMap(5, 5);
         Entity player = new Entity("Player", '@', Color.White, new Point(2, 2), blocksMovement: true);
+        player.Fighter = new Fighter(10, 1, 0);
 
         Assert.Throws<ArgumentException>(() => new GameWorld(map, Array.Empty<Entity>(), player));
     }
@@ -249,6 +251,7 @@
     {
         GameMap map = new GameMap(5, 5);
         Entity player = new Entity("Player", '@', Color.White, new Point(2, 2), blocksMovement: true);
+        player.Fighter = new Fighter(10, 1, 0);
 
         Assert.Throws<ArgumentNullException>(() => new GameWorld(null!, new[] { player }, player));
         Assert.Throws<ArgumentNullException>(() => new GameWorld(map, null!, player));
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
        GameWorld first = GameWorld.Generate(40, 25, new Random(12345), MonsterTable.Standard);
        GameWorld second = GameWorld.Generate(40, 25, new Random(12345), MonsterTable.Standard);

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
            GameWorld world = GameWorld.Generate(40, 25, new Random(seed), MonsterTable.Standard);

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
            GameWorld world = GameWorld.Generate(40, 25, new Random(seed), MonsterTable.Standard);

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
            GameWorld world = GameWorld.Generate(40, 25, new Random(seed), MonsterTable.Standard);

            IEnumerable<Entity> others = world.Entities.Where(entity => entity != world.Player);

            Assert.DoesNotContain(world.Player.Position, others.Select(entity => entity.Position));
        }
    }

    [Fact]
    public void AGeneratedWorldContainsMonsters()
    {
        // Weak on purpose: how many is random. That there are any at all is not.
        GameWorld world = GameWorld.Generate(60, 35, new Random(7), MonsterTable.Standard);

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
        Assert.Throws<ArgumentNullException>(() => GameWorld.Generate(40, 25, null!, MonsterTable.Standard));
        Assert.Throws<ArgumentNullException>(() => GameWorld.Generate(40, 25, new Random(1), null!));
    }
}
```

### [`RogueTutorial.Tests/MonsterTableTests.cs`](../parts/part-06-combat/RogueTutorial.Tests/MonsterTableTests.cs)

The Part 5 file, updated for the new MonsterKind arguments.

<!-- generated-diff -->
**Changed from Part 5.** The complete file follows; this is only what moved:

```diff
--- part-05-placing-monsters/MonsterTableTests.cs
+++ current/MonsterTableTests.cs
@@ -31,7 +31,7 @@
     private static MonsterTable RatsOnly(int maximumPerRoom)
     {
         return new MonsterTable(
-            new[] { new MonsterKind("Rat", 'r', Color.Red, weight: 1) },
+            new[] { new MonsterKind("Rat", 'r', Color.Red, weight: 1, maximumHitPoints: 4, attack: 3, defence: 0) },
             maximumPerRoom);
     }
 
@@ -184,8 +184,8 @@
         MonsterTable table = new MonsterTable(
             new[]
             {
-                new MonsterKind("Common", 'c', Color.Red, weight: 3),
-                new MonsterKind("Rare", 'x', Color.Blue, weight: 1),
+                new MonsterKind("Common", 'c', Color.Red, weight: 3, maximumHitPoints: 4, attack: 3, defence: 0),
+                new MonsterKind("Rare", 'x', Color.Blue, weight: 1, maximumHitPoints: 4, attack: 3, defence: 0),
             },
             maximumPerRoom: 2);
 
@@ -213,7 +213,7 @@
     [Fact]
     public void AKindWithNoNameIsRejected()
     {
-        Assert.Throws<ArgumentException>(() => new MonsterKind("  ", 'r', Color.Red, weight: 1));
+        Assert.Throws<ArgumentException>(() => new MonsterKind("  ", 'r', Color.Red, weight: 1, maximumHitPoints: 4, attack: 3, defence: 0));
     }
 
     [Theory]
@@ -221,7 +221,7 @@
     [InlineData(-1)]
     public void AWeightThatCanNeverBeChosenIsRejected(int weight)
     {
-        Assert.Throws<ArgumentOutOfRangeException>(() => new MonsterKind("Rat", 'r', Color.Red, weight));
+        Assert.Throws<ArgumentOutOfRangeException>(() => new MonsterKind("Rat", 'r', Color.Red, weight, maximumHitPoints: 4, attack: 3, defence: 0));
     }
 
     [Fact]
@@ -234,7 +234,7 @@
     public void ANegativeMaximumIsRejected()
     {
         Assert.Throws<ArgumentOutOfRangeException>(
-            () => new MonsterTable(new[] { new MonsterKind("Rat", 'r', Color.Red, 1) }, maximumPerRoom: -1));
+            () => new MonsterTable(new[] { new MonsterKind("Rat", 'r', Color.Red, 1, maximumHitPoints: 4, attack: 3, defence: 0) }, maximumPerRoom: -1));
     }
 
     [Fact]
```
<!-- generated-diff -->

```csharp
/*
 * Unit tests for what lives in the dungeon and how it is placed. Every test passes a seeded
 * Random, so "random" here means arbitrary but repeatable.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~MonsterTableTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class MonsterTableTests
{
    // A room with solid floor inside it, which is what placement expects to find.
    private static GameMap OpenMapFor(RectangularRoom room)
    {
        GameMap map = new GameMap(room.Right + 2, room.Bottom + 2);
        map.Fill(TileTypes.Wall);

        foreach (Point cell in room.InnerCells)
        {
            map.SetTile(cell, TileTypes.Floor);
        }

        return map;
    }

    private static MonsterTable RatsOnly(int maximumPerRoom)
    {
        return new MonsterTable(
            new[] { new MonsterKind("Rat", 'r', Color.Red, weight: 1, maximumHitPoints: 4, attack: 3, defence: 0) },
            maximumPerRoom);
    }

    [Fact]
    public void NoMoreThanTheMaximumArePlaced()
    {
        RectangularRoom room = new RectangularRoom(0, 0, 12, 12);
        GameMap map = OpenMapFor(room);
        MonsterTable table = RatsOnly(maximumPerRoom: 2);

        for (int seed = 0; seed < 50; seed++)
        {
            IReadOnlyList<Entity> placed = table.PopulateRoom(room, map, new Random(seed));

            Assert.True(placed.Count <= 2, $"seed {seed} placed {placed.Count}");
        }
    }

    [Fact]
    public void AMaximumOfZeroPlacesNothing()
    {
        RectangularRoom room = new RectangularRoom(0, 0, 12, 12);
        GameMap map = OpenMapFor(room);

        IReadOnlyList<Entity> placed = RatsOnly(maximumPerRoom: 0).PopulateRoom(room, map, new Random(1));

        Assert.Empty(placed);
    }

    [Fact]
    public void EveryMonsterLandsInsideTheRoomWalls()
    {
        RectangularRoom room = new RectangularRoom(5, 3, 9, 8);
        GameMap map = OpenMapFor(room);
        MonsterTable table = RatsOnly(maximumPerRoom: 2);

        for (int seed = 0; seed < 50; seed++)
        {
            foreach (Entity monster in table.PopulateRoom(room, map, new Random(seed)))
            {
                Assert.True(monster.Position.X > room.Left, $"seed {seed}: {monster.Position} is on the left wall");
                Assert.True(monster.Position.X < room.Right, $"seed {seed}: {monster.Position} is on the right wall");
                Assert.True(monster.Position.Y > room.Top, $"seed {seed}: {monster.Position} is on the top wall");
                Assert.True(monster.Position.Y < room.Bottom, $"seed {seed}: {monster.Position} is on the bottom wall");
            }
        }
    }

    [Fact]
    public void ADoorwayIsStillNotInsideTheRoom()
    {
        // The walkability check alone does not pin the bounds: carve a corridor through a room's
        // wall and that wall cell becomes walkable, so a placement roll allowed to reach the ring
        // would put a monster in the doorway. The room's interior is the contract, so it must be
        // the roll that excludes the ring rather than the map happening to be solid there.
        RectangularRoom room = new RectangularRoom(0, 0, 7, 7);
        GameMap map = OpenMapFor(room);

        // A doorway in each wall, as dungeon generation produces when a corridor meets a room.
        map.SetTile(new Point(3, 0), TileTypes.Floor);
        map.SetTile(new Point(3, 6), TileTypes.Floor);
        map.SetTile(new Point(0, 3), TileTypes.Floor);
        map.SetTile(new Point(6, 3), TileTypes.Floor);

        MonsterTable table = RatsOnly(maximumPerRoom: 4);

        for (int seed = 0; seed < 100; seed++)
        {
            foreach (Entity monster in table.PopulateRoom(room, map, new Random(seed)))
            {
                Assert.True(
                    monster.Position.X > room.Left && monster.Position.X < room.Right
                        && monster.Position.Y > room.Top && monster.Position.Y < room.Bottom,
                    $"seed {seed}: {monster.Name} was placed at {monster.Position}, on the room's wall ring");
            }
        }
    }

    [Fact]
    public void TwoMonstersNeverShareACell()
    {
        RectangularRoom room = new RectangularRoom(0, 0, 5, 5);
        GameMap map = OpenMapFor(room);

        // A 5x5 room has a 3x3 interior, so with four wanted the rolls collide often.
        MonsterTable table = RatsOnly(maximumPerRoom: 4);

        for (int seed = 0; seed < 50; seed++)
        {
            IReadOnlyList<Entity> placed = table.PopulateRoom(room, map, new Random(seed));

            Assert.Equal(placed.Count, placed.Select(monster => monster.Position).Distinct().Count());
        }
    }

    [Fact]
    public void MonstersAreNeverPlacedInRock()
    {
        // A corridor cut through a room, or a pillar, leaves unwalkable cells in its interior.
        RectangularRoom room = new RectangularRoom(0, 0, 8, 8);
        GameMap map = OpenMapFor(room);
        map.SetTile(new Point(3, 3), TileTypes.Wall);
        map.SetTile(new Point(4, 4), TileTypes.Wall);

        MonsterTable table = RatsOnly(maximumPerRoom: 4);

        for (int seed = 0; seed < 50; seed++)
        {
            foreach (Entity monster in table.PopulateRoom(room, map, new Random(seed)))
            {
                Assert.True(map.IsWalkable(monster.Position), $"seed {seed}: {monster.Name} is in rock");
            }
        }
    }

    [Fact]
    public void EveryPlacedMonsterBlocksMovement()
    {
        RectangularRoom room = new RectangularRoom(0, 0, 10, 10);
        GameMap map = OpenMapFor(room);

        IReadOnlyList<Entity> placed = RatsOnly(maximumPerRoom: 2).PopulateRoom(room, map, new Random(3));

        Assert.All(placed, monster => Assert.True(monster.BlocksMovement));
    }

    [Fact]
    public void TheSameSeedPlacesTheSameMonsters()
    {
        RectangularRoom room = new RectangularRoom(0, 0, 10, 10);
        GameMap map = OpenMapFor(room);
        MonsterTable table = MonsterTable.Standard;

        string first = string.Join(";", table.PopulateRoom(room, map, new Random(99))
            .Select(monster => $"{monster.Name}{monster.Position}"));
        string second = string.Join(";", table.PopulateRoom(room, map, new Random(99))
            .Select(monster => $"{monster.Name}{monster.Position}"));

        Assert.Equal(first, second);
    }

    [Fact]
    public void AHeavierKindTurnsUpMoreOften()
    {
        // Weights are relative, so the check is on the ordering rather than on exact counts:
        // a kind weighted 3 against 1 should clearly dominate over many rooms.
        RectangularRoom room = new RectangularRoom(0, 0, 10, 10);
        GameMap map = OpenMapFor(room);

        MonsterTable table = new MonsterTable(
            new[]
            {
                new MonsterKind("Common", 'c', Color.Red, weight: 3, maximumHitPoints: 4, attack: 3, defence: 0),
                new MonsterKind("Rare", 'x', Color.Blue, weight: 1, maximumHitPoints: 4, attack: 3, defence: 0),
            },
            maximumPerRoom: 2);

        int common = 0;
        int rare = 0;

        for (int seed = 0; seed < 300; seed++)
        {
            foreach (Entity monster in table.PopulateRoom(room, map, new Random(seed)))
            {
                if (monster.Name == "Common")
                {
                    common++;
                }
                else
                {
                    rare++;
                }
            }
        }

        Assert.True(common > rare, $"common {common} should outnumber rare {rare}");
    }

    [Fact]
    public void AKindWithNoNameIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new MonsterKind("  ", 'r', Color.Red, weight: 1, maximumHitPoints: 4, attack: 3, defence: 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AWeightThatCanNeverBeChosenIsRejected(int weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MonsterKind("Rat", 'r', Color.Red, weight, maximumHitPoints: 4, attack: 3, defence: 0));
    }

    [Fact]
    public void AnEmptyTableIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new MonsterTable(Array.Empty<MonsterKind>(), maximumPerRoom: 2));
    }

    [Fact]
    public void ANegativeMaximumIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MonsterTable(new[] { new MonsterKind("Rat", 'r', Color.Red, 1, maximumHitPoints: 4, attack: 3, defence: 0) }, maximumPerRoom: -1));
    }

    [Fact]
    public void ANullArgumentIsRejected()
    {
        RectangularRoom room = new RectangularRoom(0, 0, 8, 8);
        GameMap map = OpenMapFor(room);
        MonsterTable table = MonsterTable.Standard;

        Assert.Throws<ArgumentNullException>(() => table.PopulateRoom(null!, map, new Random(1)));
        Assert.Throws<ArgumentNullException>(() => table.PopulateRoom(room, null!, new Random(1)));
        Assert.Throws<ArgumentNullException>(() => table.PopulateRoom(room, map, null!));
        Assert.Throws<ArgumentNullException>(() => new MonsterTable(null!, 2));
    }
}
```

## Step 4: build and run

```
dotnet build
dotnet test
dotnet run --project RogueTutorial
```

Expected: a clean build, 240 passing tests, and monsters that fight back.

### If something is wrong

| Symptom | Cause |
|---|---|
| `CS1061: 'Entity' does not contain a definition for 'Fighter'` or `'Die'` | `Entity.cs` has not been replaced yet - finish Step 2 before building |
| `CS7036: no argument for 'maximumHitPoints'` | A `MonsterKind` construction not yet updated |
| Every world starts with the player dead | The player has no `Fighter`; the constructor should reject that |
| Monsters never move | `RunMonsterTurns` is not being called after a player action |
| Monsters chase you through walls | `MonsterTurn` is checking `Unseen` rather than `Visible` |
| A killed monster still blocks its cell | `Die` is not clearing `BlocksMovement` |
| The log says "remains of Rat dies" | The message is being built after `Die` rather than before |
| An armoured target gains health | The damage floor is missing |
| `InvalidOperationException` from `Die` | Something with no `Fighter` reached combat |

## Step 5: regenerate the documentation

Skip this if you did not set up docfx in Part 1. Nothing was deleted this part:

```
dotnet docfx docfx.json --serve --port 8081
```

Expected: `Build succeeded. 0 warning(s) 0 error(s)`, and pages for `Fighter`, `Combat`,
`AttackResult`, `MessageLog` and `MonsterTurn` at <http://localhost:8081>.

---

Next: **Part 7, the message log and health bar.**

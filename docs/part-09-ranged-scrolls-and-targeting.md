# Part 9: Ranged scrolls and targeting

- [What it is](#what-it-is)
- [How to use it](#how-to-use-it)
- [How to set it up](#how-to-set-it-up)

---

# What it is

Two scrolls that need somewhere to point, and the aiming that lets you point them. Reading a
fireball scroll shows what it will burn before you commit:

```
..............
......#.......
....#####.....
....#####.....
...###X###....
....#####.....
....#####.....
......#.......
..............
```

`X` is the cursor; the shaded cells are what the blast reaches. In the game the blast is an
orange tint over the dungeon rather than a change of glyph, so you can still read what is under
it.

## The mode from Part 8 gets its second user

That is the real content. Part 8 built modal input for the inventory and claimed the targeting
cursor would need the same machinery. This is the test of whether it was built right, and the
answer is mostly yes with one thing that had to be added.

**A mode now has to remember where it came from.** Reading a scroll opens targeting *from the
pack*. Cancelling has to go back to the pack, not to the map:

```csharp
public void CancelTarget()
{
    Aiming = null;
    Mode = GameMode.ShowingInventory;
}
```

Drop to the map instead and the player is looking at the dungeon holding a scroll they thought
they had put away. Part 10's prompts will inherit this, which is why it is a property of the
mode rather than a branch in the keyboard handler.

## The scroll stays in the pack until the shot lands

Reading a scroll does not use it:

```csharp
if (item.Consumable.NeedsTarget)
{
    BeginTargeting(item, slot);
    return false;
}
```

`false` means no turn was spent. The scroll is still in the pack, so cancelling costs nothing at
all - not the item, not the turn, not a free swing from an adjacent monster.

And a shot that finds nothing is a miss rather than a waste:

```csharp
if (victim?.Fighter is null)
{
    return new UseResult(false, "The lightning strikes nothing.");
}
```

Same rule as Part 8's potion at full health. Aiming badly is a mistake the player is allowed to
make; destroying the scroll for it is a punishment out of proportion.

## The cursor starts somewhere useful

```csharp
Aiming = new Targeting(scroll, slot, NearestVisibleTarget(), scroll.Consumable!.Radius);
```

On the nearest creature you can see, or on yourself when there is none. Aiming almost always
means aiming at something, and starting on empty floor makes the common case slower for no
reason.

## Two rules the blast has to get right

**It is round, not square.** Chebyshev distance is what movement uses, and a square fireball
reads as a bug even when it is deliberate. Sight is already round, and the player is aiming by
eye, so the blast matches sight:

```csharp
if ((deltaX * deltaX) + (deltaY * deltaY) > Radius * Radius)
```

**What is shown is what burns.** The drawing code uses the same test as the damage. A shown area
that disagrees with the effect is worse than showing nothing, because the player would learn to
distrust it. There is a test asserting both, since the two live in different files and nothing
else would notice them drifting apart.

## The fireball burns the reader

It does not know who threw it. A player who aims at their own feet finds that out the honest
way, and there is a test for it - the alternative is a scroll that is strictly safe to use at
point-blank range, which removes the only decision it has.

## What is deliberately wrong

**Nothing checks line of sight.** You can aim the cursor into unexplored darkness and hit
whatever happens to be there. Real roguelikes restrict targeting to what you can see, and doing
it properly means deciding what a remembered-but-not-visible cell allows.

**No range limit.** The cursor can travel to the far corner of the map.

**No confirmation on a self-hit.** Aiming a fireball at your own feet fires without comment.

**The cursor cannot be moved with the mouse.** Keys only.

---

# How to use it

## Play it

```
cd parts/part-09-ranged-scrolls-and-targeting
dotnet run --project RogueTutorial
```

| Key | Effect |
|---|---|
| `i` then a letter | Read a scroll, which begins aiming |
| Arrows, keypad | Move the cursor |
| `Enter` | Fire |
| `Esc` | Cancel, back to the pack |

Yellow `?` are lightning scrolls, orange `?` are fireballs. Both are less common than potions,
because a scroll you cannot aim safely is worth less than health you can always drink.

## Run the tests

```
dotnet test                                  # 353 tests
dotnet test --filter "Category!=EndToEnd"    # no window
```

| Test class | Level | Covers |
|---|---|---|
| [`TargetingTests`](../parts/part-09-ranged-scrolls-and-targeting/RogueTutorial.Tests/TargetingTests.cs) | unit + integration | aiming, both scrolls, and where cancelling goes |
| [`ScreenComposerTests`](../parts/part-09-ranged-scrolls-and-targeting/RogueTutorial.Tests/ScreenComposerTests.cs) | unit | the crosshair, and that the shown blast is the real one |

## Prove the tests can fail

| Change | Expect |
|---|---|
| `CancelTarget`: return to `Playing` instead of the pack | 2 fail |
| `UseItem`: use a scroll without aiming it | 16 fail |
| `Strike`: consume the scroll on a miss | 1 fails |
| `Burn`: ignore the radius | 2 fail |
| `Burn`: spare the reader from their own blast | 1 fails |
| `CommandReader`: check Enter before Escape | 2 fail |

---

# How to set it up

> **You are in:** your project folder, the one holding `RogueTutorial/` and `RogueTutorial.Tests/`

## Step 1: retitle the window

One line in `RogueTutorial/Program.cs`:

```csharp
const string WindowTitle = "Roguelike Tutorial - Part 9: Ranged scrolls and targeting";
```

## Step 2: the source files

**Each block below is the complete file.** One is new; the other seven already exist and should
be replaced entirely.

**Do not build until every file in this step is in place** - C# compiles a project as a whole,
so a half-finished step fails on files that are perfectly correct.

Adding the radius to `Consumable` breaks every construction of one, which is the compiler
listing the places the new rule applies.

### [`RogueTutorial/Targeting.cs`](../parts/part-09-ranged-scrolls-and-targeting/RogueTutorial/Targeting.cs)

Where the cursor is, what is being aimed, and which slot to go back to.

```csharp
/*
 * Aiming: where the cursor is, what is being aimed, and where cancelling goes back to.
 *
 * The last part gave the game modes. This is the first one that has to remember where it came
 * from: reading a scroll opens targeting from the pack, and cancelling must return to the pack
 * rather than to the map. A mode that forgets that leaves the player looking at the dungeon with
 * a scroll they thought they had put back.
 *
 * Usage:
 *
 *     Targeting aim = new Targeting(scroll, slot: 2, start: player.Position, radius: 3);
 *
 *     aim.MoveCursor(new Point(1, 0), map);   // one cell right, refused at the map edge
 *     Point at = aim.Cursor;
 *     bool splash = aim.IsAreaEffect;          // true when the scroll hits more than one cell
 *
 * Refuses a null scroll, a scroll with no Consumable, a negative slot, and a radius below zero.
 * A radius of zero is a single-target scroll, which is not the same as no scroll at all.
 */

using System;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class Targeting
{
    /// <summary>The scroll being aimed. It stays in the pack until the shot is confirmed.</summary>
    public Entity Scroll { get; }

    /// <summary>Which pack slot it came from, so cancelling can put the player back there.</summary>
    public int Slot { get; }

    /// <summary>How far the effect spreads from the chosen cell. Zero hits one cell.</summary>
    public int Radius { get; }

    /// <summary>Where the player is currently aiming.</summary>
    public Point Cursor { get; private set; }

    /// <summary>True when the scroll hits more than the cell it lands on.</summary>
    public bool IsAreaEffect => Radius > 0;

    /// <summary>
    /// Begins aiming a scroll. Throws ArgumentNullException on a null scroll, ArgumentException
    /// when it has no Consumable - only an item can be aimed - and ArgumentOutOfRangeException on
    /// a negative slot or radius.
    /// </summary>
    public Targeting(Entity scroll, int slot, Point start, int radius)
    {
        ArgumentNullException.ThrowIfNull(scroll);

        // Aiming something that cannot be used would leave the player stuck in a mode with no
        // way to resolve it.
        if (scroll.Consumable is null)
        {
            throw new ArgumentException($"{scroll.Name} is not an item and cannot be aimed.", nameof(scroll));
        }

        if (slot < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "A pack slot cannot be negative.");
        }

        if (radius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "A blast radius cannot be negative.");
        }

        Scroll = scroll;
        Slot = slot;
        Radius = radius;
        Cursor = start;
    }

    /// <summary>
    /// Moves the cursor by one step, refusing anything that would leave the map. The cursor may
    /// rest on a wall or on darkness: aiming at what you cannot see is a mistake the player is
    /// allowed to make, and the scroll simply finds nothing there. Throws ArgumentNullException
    /// on a null map.
    /// </summary>
    public void MoveCursor(Point offset, GameMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        Point destination = Cursor + offset;

        // Off the map is the one refusal. Everything else is the player's business.
        if (!map.IsInBounds(destination))
        {
            return;
        }

        Cursor = destination;
    }
}
```

### [`RogueTutorial/Consumable.cs`](../parts/part-09-ranged-scrolls-and-targeting/RogueTutorial/Consumable.cs)

The Part 8 file, with the two aimed effects and UseAt.

<!-- generated-diff -->
**Changed from Part 8.** The complete file follows; this is only what moved:

```diff
--- part-08-items-and-inventory/Consumable.cs
+++ current/Consumable.cs
@@ -8,11 +8,15 @@
  * Usage:
  *
  *     Entity potion = new Entity("Healing potion", '!', Color.Magenta, cell, blocksMovement: false);
- *     potion.Consumable = new Consumable(ConsumableKind.Healing, power: 8);
+ *     potion.Consumable = new Consumable(ConsumableKind.Healing, power: 8, radius: 0);
  *
  *     UseResult result = potion.Consumable.UseOn(player);
  *     // -> result.Message  "You drink the Healing potion and recover 6 hit points."
  *     // -> result.Consumed true when the item should be removed from the pack
+ *
+ * Two of the kinds need somewhere to aim. Those are resolved through UseAt rather than UseOn,
+ * and asking for the wrong one throws rather than guessing: a scroll used on the reader instead
+ * of on a target is a bug that would look like bad luck.
  *
  * An item that would do nothing is not consumed - drinking a healing potion at full health
  * wastes it, and a roguelike that lets you do that by accident is a roguelike people stop
@@ -20,14 +24,23 @@
  */
 
 using System;
+using System.Collections.Generic;
+using System.Linq;
+using SadRogue.Primitives;
 
 namespace RogueTutorial;
 
-/// <summary>The kinds of thing an item can do. Part 9 adds the ones that need a target.</summary>
+/// <summary>The kinds of thing an item can do.</summary>
 internal enum ConsumableKind
 {
-    /// <summary>Restores hit points to whoever drinks it.</summary>
+    /// <summary>Restores hit points to whoever drinks it. Needs no target.</summary>
     Healing,
+
+    /// <summary>Strikes one creature at the chosen cell.</summary>
+    Lightning,
+
+    /// <summary>Burns everything within a radius of the chosen cell, the reader included.</summary>
+    Fireball,
 }
 
 /// <summary>What came of using an item.</summary>
@@ -51,6 +64,18 @@
     /// <summary>What this item does.</summary>
     public ConsumableKind Kind { get; }
 
+    /// <summary>
+    /// How far the effect spreads from the cell it lands on. Zero for everything that hits one
+    /// creature, which is every kind but Fireball.
+    /// </summary>
+    public int Radius { get; }
+
+    /// <summary>
+    /// True when using this needs somewhere to aim. The two that do are resolved through UseAt;
+    /// asking for the wrong method throws rather than picking a target on the player's behalf.
+    /// </summary>
+    public bool NeedsTarget => Kind is ConsumableKind.Lightning or ConsumableKind.Fireball;
+
     /// <summary>How much it does it by: hit points restored, for a healing item.</summary>
     public int Power { get; }
 
@@ -58,15 +83,21 @@
     /// Records what an item does. Throws ArgumentOutOfRangeException on a power below one, since
     /// an item that does nothing measurable is a table entry somebody meant to finish.
     /// </summary>
-    public Consumable(ConsumableKind kind, int power)
+    public Consumable(ConsumableKind kind, int power, int radius)
     {
         if (power < 1)
         {
             throw new ArgumentOutOfRangeException(nameof(power), power, "A consumable needs a power of at least one.");
+        }
+
+        if (radius < 0)
+        {
+            throw new ArgumentOutOfRangeException(nameof(radius), radius, "A blast radius cannot be negative.");
         }
 
         Kind = kind;
         Power = power;
+        Radius = radius;
     }
 
     /// <summary>
@@ -84,6 +115,12 @@
             throw new ArgumentException($"{user.Name} has no Fighter and cannot use an item.", nameof(user));
         }
 
+        // Aiming is the caller's job, and doing it for them would pick a target silently.
+        if (NeedsTarget)
+        {
+            throw new InvalidOperationException($"{Kind} needs a target; use UseAt instead.");
+        }
+
         return Kind switch
         {
             ConsumableKind.Healing => Heal(user),
@@ -91,6 +128,104 @@
         };
     }
 
+    /// <summary>
+    /// Applies this item's effect at a chosen cell and reports what happened. An effect that
+    /// finds nothing to hit leaves the item unconsumed, so a miss costs the turn rather than the
+    /// scroll. Throws ArgumentNullException on a null argument and InvalidOperationException when
+    /// this kind needs no target - a healing potion aimed across the room is a caller error.
+    /// </summary>
+    public UseResult UseAt(Entity user, Point target, GameWorld world)
+    {
+        ArgumentNullException.ThrowIfNull(user);
+        ArgumentNullException.ThrowIfNull(world);
+
+        if (!NeedsTarget)
+        {
+            throw new InvalidOperationException($"{Kind} needs no target; use UseOn instead.");
+        }
+
+        return Kind switch
+        {
+            ConsumableKind.Lightning => Strike(target, world),
+            ConsumableKind.Fireball => Burn(target, world),
+            _ => throw new InvalidOperationException($"No aimed effect is defined for {Kind}."),
+        };
+    }
+
+    // Hits one creature at the chosen cell, if there is one.
+    private UseResult Strike(Point target, GameWorld world)
+    {
+        Entity? victim = world.BlockingEntityAt(target);
+
+        // Aiming at empty floor is a miss, and a miss must not spend the scroll.
+        if (victim?.Fighter is null)
+        {
+            return new UseResult(false, "The lightning strikes nothing.");
+        }
+
+        string name = victim.Name;
+
+        int dealt = victim.Fighter.TakeDamage(Power);
+
+        // Read the name before Die renames it, exactly as Combat does.
+        string message = $"Lightning strikes the {name} for {dealt} damage.";
+
+        if (victim.Fighter.IsDead)
+        {
+            victim.Die();
+            message = $"{message} {name} dies.";
+        }
+
+        return new UseResult(true, message);
+    }
+
+    // Burns everything within the radius, including whoever read the scroll.
+    private UseResult Burn(Point target, GameWorld world)
+    {
+        List<string> struck = new List<string>();
+
+        // Snapshotted, because Die converts an entity in place while this walks the list.
+        foreach (Entity entity in world.Entities.ToList())
+        {
+            if (entity.Fighter is null)
+            {
+                continue;
+            }
+
+            // Round rather than square, matching how sight measures: a square blast reads as a
+            // bug even when it is deliberate, and the player is aiming by eye.
+            int deltaX = entity.Position.X - target.X;
+            int deltaY = entity.Position.Y - target.Y;
+
+            if ((deltaX * deltaX) + (deltaY * deltaY) > Radius * Radius)
+            {
+                continue;
+            }
+
+            string name = entity.Name;
+
+            entity.Fighter.TakeDamage(Power);
+
+            if (entity.Fighter.IsDead)
+            {
+                entity.Die();
+                struck.Add($"{name} dies");
+            }
+            else
+            {
+                struck.Add($"{name} is burned");
+            }
+        }
+
+        // A blast that touched nothing is a wasted turn rather than a wasted scroll.
+        if (struck.Count == 0)
+        {
+            return new UseResult(false, "The fireball burns nothing.");
+        }
+
+        return new UseResult(true, $"The fireball erupts: {string.Join(", ", struck)}.");
+    }
+
     // Restores health, up to the maximum, and reports how much was actually recovered.
     private UseResult Heal(Entity user)
     {
```
<!-- generated-diff -->

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
```

### [`RogueTutorial/ItemTable.cs`](../parts/part-09-ranged-scrolls-and-targeting/RogueTutorial/ItemTable.cs)

The Part 8 file, with the two scrolls.

<!-- generated-diff -->
**Changed from Part 8.** The complete file follows; this is only what moved:

```diff
--- part-08-items-and-inventory/ItemTable.cs
+++ current/ItemTable.cs
@@ -43,11 +43,15 @@
     /// <summary>How much it does it by.</summary>
     public int Power { get; }
 
+    /// <summary>How far the effect spreads. Zero for everything that hits one cell.</summary>
+    public int Radius { get; }
+
     /// <summary>
     /// Records one item kind. Throws ArgumentException on a blank name and
     /// ArgumentOutOfRangeException on a weight below one, which could never be chosen.
     /// </summary>
-    public ItemKind(string name, char glyph, Color foreground, int weight, ConsumableKind effect, int power)
+    public ItemKind(
+        string name, char glyph, Color foreground, int weight, ConsumableKind effect, int power, int radius)
     {
         if (string.IsNullOrWhiteSpace(name))
         {
@@ -65,10 +69,11 @@
         Weight = weight;
         Effect = effect;
         Power = power;
+        Radius = radius;
 
         // Constructing the component here would throw far from this call site, so the same rule
         // is enforced where the kind is declared.
-        _ = new Consumable(effect, power);
+        _ = new Consumable(effect, power, radius);
     }
 }
 
@@ -109,12 +114,18 @@
     }
 
     /// <summary>
-    /// The table the game uses. One kind so far; Part 9 adds the scrolls that need a target.
+    /// The table the game uses. Potions are common because a scroll you cannot aim safely is
+    /// worth less than health you can always drink.
     /// </summary>
     public static ItemTable Standard => new ItemTable(
         new[]
         {
-            new ItemKind("healing potion", '!', new Color(200, 80, 200), weight: 1, ConsumableKind.Healing, power: 8),
+            new ItemKind("healing potion", '!', new Color(200, 80, 200),
+                weight: 4, ConsumableKind.Healing, power: 8, radius: 0),
+            new ItemKind("lightning scroll", '?', new Color(230, 230, 100),
+                weight: 2, ConsumableKind.Lightning, power: 12, radius: 0),
+            new ItemKind("fireball scroll", '?', new Color(230, 130, 60),
+                weight: 1, ConsumableKind.Fireball, power: 8, radius: 3),
         },
         maximumPerRoom: 2);
 
@@ -150,7 +161,7 @@
             // Items do not block: you walk over them, and picking up is a separate command.
             Entity dropped = new Entity(kind.Name, kind.Glyph, kind.Foreground, cell, blocksMovement: false);
 
-            dropped.Consumable = new Consumable(kind.Effect, kind.Power);
+            dropped.Consumable = new Consumable(kind.Effect, kind.Power, kind.Radius);
 
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

    /// <summary>What it does when used.</summary>
    public ConsumableKind Effect { get; }

    /// <summary>How much it does it by.</summary>
    public int Power { get; }

    /// <summary>How far the effect spreads. Zero for everything that hits one cell.</summary>
    public int Radius { get; }

    /// <summary>
    /// Records one item kind. Throws ArgumentException on a blank name and
    /// ArgumentOutOfRangeException on a weight below one, which could never be chosen.
    /// </summary>
    public ItemKind(
        string name, char glyph, Color foreground, int weight, ConsumableKind effect, int power, int radius)
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
        Radius = radius;

        // Constructing the component here would throw far from this call site, so the same rule
        // is enforced where the kind is declared.
        _ = new Consumable(effect, power, radius);
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
    /// The table the game uses. Potions are common because a scroll you cannot aim safely is
    /// worth less than health you can always drink.
    /// </summary>
    public static ItemTable Standard => new ItemTable(
        new[]
        {
            new ItemKind("healing potion", '!', new Color(200, 80, 200),
                weight: 4, ConsumableKind.Healing, power: 8, radius: 0),
            new ItemKind("lightning scroll", '?', new Color(230, 230, 100),
                weight: 2, ConsumableKind.Lightning, power: 12, radius: 0),
            new ItemKind("fireball scroll", '?', new Color(230, 130, 60),
                weight: 1, ConsumableKind.Fireball, power: 8, radius: 3),
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

            dropped.Consumable = new Consumable(kind.Effect, kind.Power, kind.Radius);

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

### [`RogueTutorial/GameCommand.cs`](../parts/part-09-ranged-scrolls-and-targeting/RogueTutorial/GameCommand.cs)

The Part 8 file, with the targeting mode and its three commands.

<!-- generated-diff -->
**Changed from Part 8.** The complete file follows; this is only what moved:

```diff
--- part-08-items-and-inventory/GameCommand.cs
+++ current/GameCommand.cs
@@ -32,6 +32,13 @@
 
     /// <summary>The pack is open. Letters choose an item and Escape closes it.</summary>
     ShowingInventory,
+
+    /// <summary>
+    /// A scroll is being aimed. Movement keys move the cursor, Enter fires, Escape goes back to
+    /// the pack rather than to the map - the scroll has not been used yet, so the player is
+    /// still standing in their inventory as far as they are concerned.
+    /// </summary>
+    Targeting,
 }
 
 /// <summary>The kinds of thing a key press can mean.</summary>
@@ -57,6 +64,15 @@
 
     /// <summary>Drop the item in a slot.</summary>
     DropItem,
+
+    /// <summary>Move the aiming cursor.</summary>
+    MoveCursor,
+
+    /// <summary>Fire the scroll at wherever the cursor is.</summary>
+    ConfirmTarget,
+
+    /// <summary>Give up aiming and go back to the pack.</summary>
+    CancelTarget,
 }
 
 internal readonly struct GameCommand
@@ -97,4 +113,13 @@
 
     /// <summary>Drop what is in a slot.</summary>
     public static GameCommand DropItem(int slot) => new GameCommand(GameCommandKind.DropItem, Point.Zero, slot);
+
+    /// <summary>Move the aiming cursor by one step.</summary>
+    public static GameCommand MoveCursor(Point offset) => new GameCommand(GameCommandKind.MoveCursor, offset, -1);
+
+    /// <summary>Fire at wherever the cursor is.</summary>
+    public static GameCommand ConfirmTarget => new GameCommand(GameCommandKind.ConfirmTarget, Point.Zero, -1);
+
+    /// <summary>Give up aiming.</summary>
+    public static GameCommand CancelTarget => new GameCommand(GameCommandKind.CancelTarget, Point.Zero, -1);
 }
```
<!-- generated-diff -->

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

    /// <summary>
    /// A scroll is being aimed. Movement keys move the cursor, Enter fires, Escape goes back to
    /// the pack rather than to the map - the scroll has not been used yet, so the player is
    /// still standing in their inventory as far as they are concerned.
    /// </summary>
    Targeting,
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

    /// <summary>Move the aiming cursor.</summary>
    MoveCursor,

    /// <summary>Fire the scroll at wherever the cursor is.</summary>
    ConfirmTarget,

    /// <summary>Give up aiming and go back to the pack.</summary>
    CancelTarget,
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

    /// <summary>Move the aiming cursor by one step.</summary>
    public static GameCommand MoveCursor(Point offset) => new GameCommand(GameCommandKind.MoveCursor, offset, -1);

    /// <summary>Fire at wherever the cursor is.</summary>
    public static GameCommand ConfirmTarget => new GameCommand(GameCommandKind.ConfirmTarget, Point.Zero, -1);

    /// <summary>Give up aiming.</summary>
    public static GameCommand CancelTarget => new GameCommand(GameCommandKind.CancelTarget, Point.Zero, -1);
}
```

### [`RogueTutorial/CommandReader.cs`](../parts/part-09-ranged-scrolls-and-targeting/RogueTutorial/CommandReader.cs)

The Part 8 file, reading keys while aiming.

<!-- generated-diff -->
**Changed from Part 8.** The complete file follows; this is only what moved:

```diff
--- part-08-items-and-inventory/CommandReader.cs
+++ current/CommandReader.cs
@@ -15,6 +15,9 @@
  *
  *     GameCommand nothing = CommandReader.Read(new[] { Keys.Left }, GameMode.ShowingInventory);
  *     // -> None: the map does not move while the pack is open
+ *
+ *     GameCommand aim = CommandReader.Read(new[] { Keys.Left }, GameMode.Targeting);
+ *     // -> MoveCursor: the same key moves the crosshair instead of the player
  *
  * Refuses a null key collection. Holding shift with a letter drops rather than uses, which is
  * why the shift state is a separate argument rather than being read from the letter.
@@ -42,6 +45,7 @@
         {
             GameMode.Playing => ReadPlaying(pressedKeys),
             GameMode.ShowingInventory => ReadInventory(pressedKeys, shiftHeld),
+            GameMode.Targeting => ReadTargeting(pressedKeys),
             _ => GameCommand.None,
         };
     }
@@ -80,6 +84,31 @@
         return GameCommand.None;
     }
 
+    // Aiming: the movement keys move the cursor instead of the player, and two keys resolve it.
+    private static GameCommand ReadTargeting(IReadOnlyCollection<Keys> pressedKeys)
+    {
+        // Escape first, so a player who panics gets out rather than firing.
+        if (pressedKeys.Contains(Keys.Escape))
+        {
+            return GameCommand.CancelTarget;
+        }
+
+        if (pressedKeys.Contains(Keys.Enter))
+        {
+            return GameCommand.ConfirmTarget;
+        }
+
+        // The same table the player walks with, so aiming needs no new keys to learn.
+        Point offset = MovementKeys.OffsetFor(pressedKeys);
+
+        if (offset != Point.Zero)
+        {
+            return GameCommand.MoveCursor(offset);
+        }
+
+        return GameCommand.None;
+    }
+
     // The pack is open: letters choose a slot, Escape closes, and nothing else applies.
     private static GameCommand ReadInventory(IReadOnlyCollection<Keys> pressedKeys, bool shiftHeld)
     {
```
<!-- generated-diff -->

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
 *     GameCommand aim = CommandReader.Read(new[] { Keys.Left }, GameMode.Targeting);
 *     // -> MoveCursor: the same key moves the crosshair instead of the player
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
            GameMode.Targeting => ReadTargeting(pressedKeys),
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

    // Aiming: the movement keys move the cursor instead of the player, and two keys resolve it.
    private static GameCommand ReadTargeting(IReadOnlyCollection<Keys> pressedKeys)
    {
        // Escape first, so a player who panics gets out rather than firing.
        if (pressedKeys.Contains(Keys.Escape))
        {
            return GameCommand.CancelTarget;
        }

        if (pressedKeys.Contains(Keys.Enter))
        {
            return GameCommand.ConfirmTarget;
        }

        // The same table the player walks with, so aiming needs no new keys to learn.
        Point offset = MovementKeys.OffsetFor(pressedKeys);

        if (offset != Point.Zero)
        {
            return GameCommand.MoveCursor(offset);
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

### [`RogueTutorial/GameWorld.cs`](../parts/part-09-ranged-scrolls-and-targeting/RogueTutorial/GameWorld.cs)

The Part 8 file, with the aiming state and the three transitions.

<!-- generated-diff -->
**Changed from Part 8.** The complete file follows; this is only what moved:

```diff
--- part-08-items-and-inventory/GameWorld.cs
+++ current/GameWorld.cs
@@ -62,6 +62,12 @@
     public GameMode Mode { get; private set; } = GameMode.Playing;
 
     /// <summary>
+    /// What is being aimed, or null when nothing is. Non-null exactly while the mode is
+    /// Targeting, which is asserted on every transition rather than merely intended.
+    /// </summary>
+    public Targeting? Aiming { get; private set; }
+
+    /// <summary>
     /// True once the player has been killed. Nothing stops the game yet; Part 10 decides what
     /// happens next, and until then the player simply stops being able to act.
     /// </summary>
@@ -268,7 +274,18 @@
     /// </summary>
     public void SetMode(GameMode mode)
     {
+        // Targeting carries state, so it is entered by reading a scroll rather than by asking.
+        if (mode == GameMode.Targeting)
+        {
+            throw new ArgumentException("Targeting is entered by using a scroll, not by SetMode.", nameof(mode));
+        }
+
+        Aiming = null;
         Mode = mode;
+
+        Debug.Assert(
+            (Mode == GameMode.Targeting) == (Aiming is not null),
+            "Something is being aimed exactly when the mode is Targeting.");
     }
 
     /// <summary>
@@ -328,6 +345,15 @@
             return false;
         }
 
+        // A scroll needs somewhere to point. Rather than using it here, the game changes mode and
+        // waits; the item stays in the pack until the shot is confirmed, so cancelling loses
+        // nothing.
+        if (item.Consumable.NeedsTarget)
+        {
+            BeginTargeting(item, slot);
+            return false;
+        }
+
         UseResult result = item.Consumable.UseOn(Player);
 
         Log.Add(result.Message);
@@ -343,6 +369,110 @@
         RunMonsterTurns();
 
         return true;
+    }
+
+    /// <summary>
+    /// Starts aiming a scroll from the given slot. The cursor begins on the nearest visible
+    /// creature if there is one, and on the player otherwise - aiming almost always means aiming
+    /// at something, and starting on empty floor makes the common case slower.
+    /// </summary>
+    private void BeginTargeting(Entity scroll, int slot)
+    {
+        Aiming = new Targeting(scroll, slot, NearestVisibleTarget(), scroll.Consumable!.Radius);
+
+        Mode = GameMode.Targeting;
+
+        Log.Add($"Aiming the {scroll.Name}. Move to aim, Enter to fire, Esc to cancel.");
+    }
+
+    // The closest creature the player can see, or the player's own cell when there is none.
+    private Point NearestVisibleTarget()
+    {
+        Entity? nearest = null;
+        int nearestDistance = int.MaxValue;
+
+        foreach (Entity entity in _entities)
+        {
+            if (entity == Player || entity.Fighter is null)
+            {
+                continue;
+            }
+
+            if (Visibility.StateAt(entity.Position) != CellVisibility.Visible)
+            {
+                continue;
+            }
+
+            int distance = Math.Max(
+                Math.Abs(entity.Position.X - Player.Position.X),
+                Math.Abs(entity.Position.Y - Player.Position.Y));
+
+            if (distance < nearestDistance)
+            {
+                nearest = entity;
+                nearestDistance = distance;
+            }
+        }
+
+        return nearest?.Position ?? Player.Position;
+    }
+
+    /// <summary>
+    /// Moves the aiming cursor. Does nothing when not aiming, which is what makes a stray key
+    /// press harmless rather than an exception.
+    /// </summary>
+    public void MoveCursor(Point offset)
+    {
+        Aiming?.MoveCursor(offset, Map);
+    }
+
+    /// <summary>
+    /// Fires the scroll being aimed at wherever the cursor is. A shot that finds nothing leaves
+    /// the scroll in the pack and returns the player to it, so a miss costs the turn rather than
+    /// the item. Returns true when the scroll was spent.
+    /// </summary>
+    public bool ConfirmTarget()
+    {
+        if (Aiming is null)
+        {
+            return false;
+        }
+
+        Targeting aiming = Aiming;
+
+        UseResult result = aiming.Scroll.Consumable!.UseAt(Player, aiming.Cursor, this);
+
+        Log.Add(result.Message);
+
+        if (!result.Consumed)
+        {
+            // Back to the pack, not to the map: the player has not put the scroll away.
+            CancelTarget();
+            return false;
+        }
+
+        Player.Inventory!.Remove(aiming.Scroll);
+
+        Aiming = null;
+        Mode = GameMode.Playing;
+
+        // A fireball can kill the reader, and a dead player takes no more turns.
+        if (!IsPlayerDead)
+        {
+            RunMonsterTurns();
+        }
+
+        return true;
+    }
+
+    /// <summary>
+    /// Gives up aiming and returns to the pack, where the scroll still is. Costs no turn: the
+    /// player has done nothing but look.
+    /// </summary>
+    public void CancelTarget()
+    {
+        Aiming = null;
+        Mode = GameMode.ShowingInventory;
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
        // Targeting carries state, so it is entered by reading a scroll rather than by asking.
        if (mode == GameMode.Targeting)
        {
            throw new ArgumentException("Targeting is entered by using a scroll, not by SetMode.", nameof(mode));
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

### [`RogueTutorial/ScreenComposer.cs`](../parts/part-09-ranged-scrolls-and-targeting/RogueTutorial/ScreenComposer.cs)

The Part 8 file, drawing the crosshair and the blast.

<!-- generated-diff -->
**Changed from Part 8.** The complete file follows; this is only what moved:

```diff
--- part-08-items-and-inventory/ScreenComposer.cs
+++ current/ScreenComposer.cs
@@ -36,6 +36,12 @@
     // a bar stretched across eighty columns reads as a wall rather than as a gauge, and the rest
     // of the row is where a dungeon level and other status go in a later part.
     private const int HealthBarWidth = 24;
+
+    // The aiming cursor. Bright, because it must be findable at a glance.
+    private static readonly Color Crosshair = new Color(255, 255, 120);
+
+    // Cells a blast will reach. Dim orange over whatever is underneath.
+    private static readonly Color BlastArea = new Color(180, 90, 40);
 
     // Colour of the log text and the health bar caption.
     private static readonly Color PanelText = new Color(200, 200, 200);
@@ -90,6 +96,13 @@
             WriteInventory(world, layout, glyphs, foregrounds);
         }
 
+        // The crosshair goes on last so nothing can be drawn over it, and the blast is drawn
+        // before it so the cursor stays visible in the middle of its own splash.
+        if (world.Aiming is not null)
+        {
+            WriteTargeting(world.Aiming, layout, glyphs, foregrounds);
+        }
+
         return new RenderedFrame(layout.WindowWidth, layout.WindowHeight, glyphs, foregrounds);
     }
 
@@ -203,6 +216,55 @@
         }
     }
 
+    // Draws the blast area and the crosshair, so the player can see what the shot will do.
+    private static void WriteTargeting(Targeting aiming, ScreenLayout layout, char[] glyphs, Color[] foregrounds)
+    {
+        // Aiming you cannot see the consequences of is guesswork, so an area effect shows its
+        // reach before it is fired rather than after.
+        if (aiming.IsAreaEffect)
+        {
+            for (int row = aiming.Cursor.Y - aiming.Radius; row <= aiming.Cursor.Y + aiming.Radius; row++)
+            {
+                for (int col = aiming.Cursor.X - aiming.Radius; col <= aiming.Cursor.X + aiming.Radius; col++)
+                {
+                    // Inside the map, inside the map area, and not the cursor's own cell.
+                    if (col < 0 || col >= layout.WindowWidth || !layout.IsMapRow(row))
+                    {
+                        continue;
+                    }
+
+                    if (col == aiming.Cursor.X && row == aiming.Cursor.Y)
+                    {
+                        continue;
+                    }
+
+                    // The same round test the blast itself uses, so what is shown is what burns.
+                    int deltaX = col - aiming.Cursor.X;
+                    int deltaY = row - aiming.Cursor.Y;
+
+                    if ((deltaX * deltaX) + (deltaY * deltaY) > aiming.Radius * aiming.Radius)
+                    {
+                        continue;
+                    }
+
+                    // The tile underneath keeps its glyph and is recoloured, so the player can
+                    // still read the dungeon through the blast.
+                    foregrounds[(row * layout.WindowWidth) + col] = BlastArea;
+                }
+            }
+        }
+
+        if (aiming.Cursor.X < 0 || aiming.Cursor.X >= layout.WindowWidth || !layout.IsMapRow(aiming.Cursor.Y))
+        {
+            return;
+        }
+
+        int index = (aiming.Cursor.Y * layout.WindowWidth) + aiming.Cursor.X;
+
+        glyphs[index] = 'X';
+        foregrounds[index] = Crosshair;
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

### [`RogueTutorial/RootScreen.cs`](../parts/part-09-ranged-scrolls-and-targeting/RogueTutorial/RootScreen.cs)

The Part 8 file, routing the three new commands.

<!-- generated-diff -->
**Changed from Part 8.** The complete file follows; this is only what moved:

```diff
--- part-08-items-and-inventory/RootScreen.cs
+++ current/RootScreen.cs
@@ -122,6 +122,18 @@
             case GameCommandKind.DropItem:
                 _world.DropItem(command.Slot);
                 break;
+
+            case GameCommandKind.MoveCursor:
+                _world.MoveCursor(command.Offset);
+                break;
+
+            case GameCommandKind.ConfirmTarget:
+                _world.ConfirmTarget();
+                break;
+
+            case GameCommandKind.CancelTarget:
+                _world.CancelTarget();
+                break;
         }
     }
 
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

            case GameCommandKind.MoveCursor:
                _world.MoveCursor(command.Offset);
                break;

            case GameCommandKind.ConfirmTarget:
                _world.ConfirmTarget();
                break;

            case GameCommandKind.CancelTarget:
                _world.CancelTarget();
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

**Each block below is the complete file.** One is new; three are carried over and need updating
for the new `Consumable` argument.

### [`RogueTutorial.Tests/TargetingTests.cs`](../parts/part-09-ranged-scrolls-and-targeting/RogueTutorial.Tests/TargetingTests.cs)

Aiming, both scrolls, and where cancelling goes.

```csharp
/*
 * Unit tests for aiming and for the two scrolls that need it.
 *
 * The property worth watching is where cancelling goes. Reading a scroll opens targeting from
 * the pack, so backing out must return to the pack - a mode that forgets where it came from
 * leaves the player looking at the dungeon holding a scroll they thought they had put away.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~TargetingTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadConsole.Input;
using SadRogue.Primitives;
using Xunit;

public sealed class TargetingTests
{
    // An open room with the player in the middle and whatever monsters the test needs.
    private static GameWorld WorldWith(params Entity[] monsters)
    {
        GameMap map = new GameMap(15, 15);
        map.Fill(TileTypes.Floor);

        Entity player = new Entity("Player", '@', Color.White, new Point(7, 7), blocksMovement: true);
        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2);
        player.Inventory = new Inventory(capacity: 26);

        List<Entity> entities = new List<Entity>(monsters) { player };

        return new GameWorld(map, entities, player);
    }

    private static Entity Monster(string name, Point at, int hitPoints)
    {
        Entity monster = new Entity(name, name[0], Color.Red, at, blocksMovement: true);
        monster.Fighter = new Fighter(hitPoints, attack: 3, defence: 0);
        return monster;
    }

    private static Entity Scroll(ConsumableKind kind, int power, int radius)
    {
        Entity scroll = new Entity($"{kind} scroll", '?', Color.Yellow, new Point(0, 0), blocksMovement: false);
        scroll.Consumable = new Consumable(kind, power, radius);
        return scroll;
    }

    [Fact]
    public void ReadingAScrollBeginsAimingRatherThanUsingIt()
    {
        GameWorld world = WorldWith(Monster("Rat", new Point(9, 7), 10));
        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Lightning, 12, 0));

        bool spentATurn = world.UseItem(0);

        Assert.False(spentATurn);
        Assert.Equal(GameMode.Targeting, world.Mode);
        Assert.NotNull(world.Aiming);
    }

    [Fact]
    public void TheScrollStaysInThePackWhileAiming()
    {
        // Nothing has been used yet, so cancelling must be able to lose nothing.
        GameWorld world = WorldWith(Monster("Rat", new Point(9, 7), 10));
        Entity scroll = Scroll(ConsumableKind.Lightning, 12, 0);
        world.Player.Inventory!.TryAdd(scroll);

        world.UseItem(0);

        Assert.Contains(scroll, world.Player.Inventory.Items);
    }

    [Fact]
    public void TheCursorStartsOnTheNearestVisibleCreature()
    {
        // Aiming almost always means aiming at something, and starting on empty floor makes the
        // common case slower for no reason.
        GameWorld world = WorldWith(
            Monster("Far", new Point(12, 7), 10),
            Monster("Near", new Point(9, 7), 10));

        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Lightning, 12, 0));
        world.UseItem(0);

        Assert.Equal(new Point(9, 7), world.Aiming!.Cursor);
    }

    [Fact]
    public void TheCursorStartsOnThePlayerWhenNothingIsVisible()
    {
        GameWorld world = WorldWith();

        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Lightning, 12, 0));
        world.UseItem(0);

        Assert.Equal(world.Player.Position, world.Aiming!.Cursor);
    }

    [Fact]
    public void CancellingReturnsToThePackRatherThanTheMap()
    {
        // The whole reason this mode has to remember where it came from.
        GameWorld world = WorldWith(Monster("Rat", new Point(9, 7), 10));
        Entity scroll = Scroll(ConsumableKind.Lightning, 12, 0);
        world.Player.Inventory!.TryAdd(scroll);

        world.UseItem(0);
        world.CancelTarget();

        Assert.Equal(GameMode.ShowingInventory, world.Mode);
        Assert.Null(world.Aiming);
        Assert.Contains(scroll, world.Player.Inventory.Items);
    }

    [Fact]
    public void CancellingCostsNoTurn()
    {
        // Looking is not acting: a monster must not get a free swing because the player changed
        // their mind about a scroll.
        GameWorld world = WorldWith(Monster("Rat", new Point(8, 7), 10));
        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Lightning, 12, 0));

        int healthBefore = world.Player.Fighter!.HitPoints;

        world.UseItem(0);
        world.CancelTarget();

        Assert.Equal(healthBefore, world.Player.Fighter.HitPoints);
    }

    [Fact]
    public void TheCursorMovesButNotOffTheMap()
    {
        GameWorld world = WorldWith();
        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Lightning, 12, 0));
        world.UseItem(0);

        world.MoveCursor(new Point(1, 0));
        Assert.Equal(new Point(8, 7), world.Aiming!.Cursor);

        // Far enough left to run off the edge, which must simply stop.
        for (int step = 0; step < 20; step++)
        {
            world.MoveCursor(new Point(-1, 0));
        }

        Assert.Equal(0, world.Aiming.Cursor.X);
    }

    [Fact]
    public void LightningHitsWhatTheCursorIsOn()
    {
        Entity rat = Monster("Rat", new Point(9, 7), 20);
        GameWorld world = WorldWith(rat);

        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Lightning, 12, 0));
        world.UseItem(0);

        Assert.True(world.ConfirmTarget());

        Assert.Equal(8, rat.Fighter!.HitPoints);
        Assert.Equal(GameMode.Playing, world.Mode);
    }

    [Fact]
    public void AFiredScrollLeavesThePack()
    {
        Entity rat = Monster("Rat", new Point(9, 7), 20);
        GameWorld world = WorldWith(rat);

        Entity scroll = Scroll(ConsumableKind.Lightning, 12, 0);
        world.Player.Inventory!.TryAdd(scroll);
        world.UseItem(0);
        world.ConfirmTarget();

        Assert.Empty(world.Player.Inventory.Items);
    }

    [Fact]
    public void AMissCostsTheTurnRatherThanTheScroll()
    {
        // Aiming at empty floor is a mistake the player is allowed to make, and destroying the
        // scroll for it would be a punishment out of proportion.
        GameWorld world = WorldWith();

        Entity scroll = Scroll(ConsumableKind.Lightning, 12, 0);
        world.Player.Inventory!.TryAdd(scroll);
        world.UseItem(0);

        // Cursor is on the player, and the player is not a valid lightning target here because
        // BlockingEntityAt finds them - so move it onto empty floor first.
        world.MoveCursor(new Point(3, 3));

        Assert.False(world.ConfirmTarget());

        Assert.Contains(scroll, world.Player.Inventory.Items);
        Assert.Equal(GameMode.ShowingInventory, world.Mode);
    }

    [Fact]
    public void AFireballBurnsEverythingInItsRadius()
    {
        Entity near = Monster("Near", new Point(9, 7), 20);
        Entity alsoNear = Monster("AlsoNear", new Point(9, 8), 20);
        Entity far = Monster("Far", new Point(14, 14), 20);

        GameWorld world = WorldWith(near, alsoNear, far);

        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Fireball, 8, radius: 2));
        world.UseItem(0);

        // Aim at the pair rather than at whatever the cursor picked.
        while (world.Aiming!.Cursor != new Point(9, 7))
        {
            world.MoveCursor(new Point(
                Math.Sign(9 - world.Aiming.Cursor.X),
                Math.Sign(7 - world.Aiming.Cursor.Y)));
        }

        Assert.True(world.ConfirmTarget());

        Assert.Equal(12, near.Fighter!.HitPoints);
        Assert.Equal(12, alsoNear.Fighter!.HitPoints);
        Assert.Equal(20, far.Fighter!.HitPoints);
    }

    [Fact]
    public void AFireballBurnsTheReaderToo()
    {
        // The scroll does not know who threw it, and a player who aims at their own feet should
        // find that out the honest way.
        GameWorld world = WorldWith();

        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Fireball, 8, radius: 2));
        world.UseItem(0);

        // The cursor starts on the player when nothing is visible, which is exactly the case.
        Assert.True(world.ConfirmTarget());

        Assert.Equal(22, world.Player.Fighter!.HitPoints);
    }

    [Fact]
    public void TheBlastIsRoundRatherThanSquare()
    {
        // A square blast reads as a bug even when it is deliberate, and it disagrees with how
        // sight measures distance. The corners of the bounding box must be outside it.
        Entity corner = Monster("Corner", new Point(9, 9), 20);
        Entity edge = Monster("Edge", new Point(9, 7), 20);

        GameWorld world = WorldWith(corner, edge);

        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Fireball, 8, radius: 2));
        world.UseItem(0);

        while (world.Aiming!.Cursor != new Point(7, 7))
        {
            world.MoveCursor(new Point(
                Math.Sign(7 - world.Aiming.Cursor.X),
                Math.Sign(7 - world.Aiming.Cursor.Y)));
        }

        world.ConfirmTarget();

        // (9,7) is two cells away on one axis: inside. (9,9) is two on both, so 8 > 4: outside.
        Assert.Equal(12, edge.Fighter!.HitPoints);
        Assert.Equal(20, corner.Fighter!.HitPoints);
    }

    [Fact]
    public void AHealingPotionCannotBeAimed()
    {
        Entity potion = Scroll(ConsumableKind.Healing, 8, 0);

        GameWorld world = WorldWith();

        Assert.Throws<InvalidOperationException>(
            () => potion.Consumable!.UseAt(world.Player, new Point(1, 1), world));
    }

    [Fact]
    public void AScrollCannotBeDrunk()
    {
        Entity scroll = Scroll(ConsumableKind.Lightning, 12, 0);

        Assert.Throws<InvalidOperationException>(() => scroll.Consumable!.UseOn(new Entity(
            "Player", '@', Color.White, new Point(0, 0), blocksMovement: true)
        {
            Fighter = new Fighter(30, 5, 2),
        }));
    }

    [Fact]
    public void MovementKeysAimWhileTargeting()
    {
        // The same keys the player walks with, so aiming needs nothing new to learn.
        Assert.Equal(GameCommandKind.MoveCursor, CommandReader.Read(new[] { Keys.Left }, GameMode.Targeting).Kind);
        Assert.Equal(GameCommandKind.ConfirmTarget, CommandReader.Read(new[] { Keys.Enter }, GameMode.Targeting).Kind);
        Assert.Equal(GameCommandKind.CancelTarget, CommandReader.Read(new[] { Keys.Escape }, GameMode.Targeting).Kind);
    }

    [Fact]
    public void EscapeBeatsEnterWhenBothAreHeld()
    {
        // A player who panics should get out rather than fire.
        GameCommand command = CommandReader.Read(new[] { Keys.Enter, Keys.Escape }, GameMode.Targeting);

        Assert.Equal(GameCommandKind.CancelTarget, command.Kind);
    }

    [Fact]
    public void TargetingCannotBeEnteredBySettingTheMode()
    {
        // It carries state, so entering it without a scroll would leave Aiming null and the
        // player stuck in a mode nothing can resolve.
        GameWorld world = WorldWith();

        Assert.Throws<ArgumentException>(() => world.SetMode(GameMode.Targeting));
    }

    [Fact]
    public void AimingIsSetExactlyWhileTargeting()
    {
        GameWorld world = WorldWith(Monster("Rat", new Point(9, 7), 10));
        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Lightning, 12, 0));

        Assert.Null(world.Aiming);

        world.UseItem(0);
        Assert.NotNull(world.Aiming);

        world.CancelTarget();
        Assert.Null(world.Aiming);
    }

    [Fact]
    public void AimingWithoutAScrollIsRejected()
    {
        Entity notAnItem = new Entity("rock", '*', Color.Gray, new Point(0, 0), blocksMovement: false);

        Assert.Throws<ArgumentException>(() => new Targeting(notAnItem, 0, new Point(1, 1), 0));
    }

    [Fact]
    public void ANegativeSlotOrRadiusIsRejected()
    {
        Entity scroll = Scroll(ConsumableKind.Lightning, 12, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => new Targeting(scroll, -1, new Point(1, 1), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Targeting(scroll, 0, new Point(1, 1), -1));
    }

    [Fact]
    public void ANullArgumentIsRejected()
    {
        Entity scroll = Scroll(ConsumableKind.Lightning, 12, 0);

        Assert.Throws<ArgumentNullException>(() => new Targeting(null!, 0, new Point(1, 1), 0));
        Assert.Throws<ArgumentNullException>(
            () => new Targeting(scroll, 0, new Point(1, 1), 0).MoveCursor(new Point(1, 0), null!));
    }
}
```

### [`RogueTutorial.Tests/ScreenComposerTests.cs`](../parts/part-09-ranged-scrolls-and-targeting/RogueTutorial.Tests/ScreenComposerTests.cs)

The Part 8 file, with the crosshair and blast tests.

<!-- generated-diff -->
**Changed from Part 8.** The complete file follows; this is only what moved:

```diff
--- part-08-items-and-inventory/ScreenComposerTests.cs
+++ current/ScreenComposerTests.cs
@@ -272,7 +272,7 @@
         foreach (string name in new[] { "first potion", "second potion" })
         {
             Entity item = new Entity(name, '!', Color.Magenta, new Point(0, 0), blocksMovement: false);
-            item.Consumable = new Consumable(ConsumableKind.Healing, 4);
+            item.Consumable = new Consumable(ConsumableKind.Healing, 4, radius: 0);
             world.Player.Inventory.TryAdd(item);
         }
 
@@ -285,6 +285,51 @@
     }
 
     [Fact]
+    public void TheCrosshairIsDrawnWhileAiming()
+    {
+        ScreenLayout layout = new ScreenLayout(30, 12, logRows: 3);
+        GameWorld world = WorldFor(layout);
+
+        world.Player.Inventory = new Inventory(capacity: 26);
+
+        Entity scroll = new Entity("scroll", '?', Color.Yellow, new Point(0, 0), blocksMovement: false);
+        scroll.Consumable = new Consumable(ConsumableKind.Lightning, 12, radius: 0);
+        world.Player.Inventory.TryAdd(scroll);
+
+        world.UseItem(0);
+
+        RenderedFrame frame = ScreenComposer.Compose(world, layout);
+
+        Assert.Equal('X', frame.GlyphAt(world.Aiming!.Cursor));
+    }
+
+    [Fact]
+    public void TheBlastAreaShownIsWhatWillBurn()
+    {
+        // Aiming you cannot see the consequences of is guesswork, and a shown area that
+        // disagrees with the damage is worse than showing nothing at all.
+        ScreenLayout layout = new ScreenLayout(30, 12, logRows: 3);
+        GameWorld world = WorldFor(layout);
+
+        world.Player.Inventory = new Inventory(capacity: 26);
+
+        Entity scroll = new Entity("scroll", '?', Color.Orange, new Point(0, 0), blocksMovement: false);
+        scroll.Consumable = new Consumable(ConsumableKind.Fireball, 8, radius: 2);
+        world.Player.Inventory.TryAdd(scroll);
+
+        world.UseItem(0);
+
+        RenderedFrame frame = ScreenComposer.Compose(world, layout);
+
+        Point cursor = world.Aiming!.Cursor;
+        Color blast = new Color(180, 90, 40);
+
+        // Two cells along an axis is inside; two on both axes is not.
+        Assert.Equal(blast, frame.ForegroundAt(cursor + new Point(2, 0)));
+        Assert.NotEqual(blast, frame.ForegroundAt(cursor + new Point(2, 2)));
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
            item.Consumable = new Consumable(ConsumableKind.Healing, 4, radius: 0);
            world.Player.Inventory.TryAdd(item);
        }

        world.SetMode(GameMode.ShowingInventory);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.Contains("a) first potion", Row(frame, 4));
        Assert.Contains("b) second potion", Row(frame, 5));
    }

    [Fact]
    public void TheCrosshairIsDrawnWhileAiming()
    {
        ScreenLayout layout = new ScreenLayout(30, 12, logRows: 3);
        GameWorld world = WorldFor(layout);

        world.Player.Inventory = new Inventory(capacity: 26);

        Entity scroll = new Entity("scroll", '?', Color.Yellow, new Point(0, 0), blocksMovement: false);
        scroll.Consumable = new Consumable(ConsumableKind.Lightning, 12, radius: 0);
        world.Player.Inventory.TryAdd(scroll);

        world.UseItem(0);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.Equal('X', frame.GlyphAt(world.Aiming!.Cursor));
    }

    [Fact]
    public void TheBlastAreaShownIsWhatWillBurn()
    {
        // Aiming you cannot see the consequences of is guesswork, and a shown area that
        // disagrees with the damage is worse than showing nothing at all.
        ScreenLayout layout = new ScreenLayout(30, 12, logRows: 3);
        GameWorld world = WorldFor(layout);

        world.Player.Inventory = new Inventory(capacity: 26);

        Entity scroll = new Entity("scroll", '?', Color.Orange, new Point(0, 0), blocksMovement: false);
        scroll.Consumable = new Consumable(ConsumableKind.Fireball, 8, radius: 2);
        world.Player.Inventory.TryAdd(scroll);

        world.UseItem(0);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Point cursor = world.Aiming!.Cursor;
        Color blast = new Color(180, 90, 40);

        // Two cells along an axis is inside; two on both axes is not.
        Assert.Equal(blast, frame.ForegroundAt(cursor + new Point(2, 0)));
        Assert.NotEqual(blast, frame.ForegroundAt(cursor + new Point(2, 2)));
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

### [`RogueTutorial.Tests/InventoryTests.cs`](../parts/part-09-ranged-scrolls-and-targeting/RogueTutorial.Tests/InventoryTests.cs)

The Part 8 file, updated for Consumable's radius.

<!-- generated-diff -->
**Changed from Part 8.** The complete file follows; this is only what moved:

```diff
--- part-08-items-and-inventory/InventoryTests.cs
+++ current/InventoryTests.cs
@@ -15,7 +15,7 @@
     private static Entity Item(string name)
     {
         Entity item = new Entity(name, '!', Color.Magenta, new Point(0, 0), blocksMovement: false);
-        item.Consumable = new Consumable(ConsumableKind.Healing, power: 4);
+        item.Consumable = new Consumable(ConsumableKind.Healing, power: 4, radius: 0);
         return item;
     }
 
```
<!-- generated-diff -->

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
        item.Consumable = new Consumable(ConsumableKind.Healing, power: 4, radius: 0);
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

### [`RogueTutorial.Tests/ItemUseTests.cs`](../parts/part-09-ranged-scrolls-and-targeting/RogueTutorial.Tests/ItemUseTests.cs)

The Part 8 file, likewise.

<!-- generated-diff -->
**Changed from Part 8.** The complete file follows; this is only what moved:

```diff
--- part-08-items-and-inventory/ItemUseTests.cs
+++ current/ItemUseTests.cs
@@ -35,7 +35,7 @@
     private static Entity Potion(Point at, int power)
     {
         Entity potion = new Entity("healing potion", '!', Color.Magenta, at, blocksMovement: false);
-        potion.Consumable = new Consumable(ConsumableKind.Healing, power);
+        potion.Consumable = new Consumable(ConsumableKind.Healing, power, radius: 0);
         return potion;
     }
 
@@ -264,7 +264,7 @@
     [InlineData(-1)]
     public void AConsumableWithNoPowerIsRejected(int power)
     {
-        Assert.Throws<ArgumentOutOfRangeException>(() => new Consumable(ConsumableKind.Healing, power));
+        Assert.Throws<ArgumentOutOfRangeException>(() => new Consumable(ConsumableKind.Healing, power, radius: 0));
     }
 
     [Fact]
```
<!-- generated-diff -->

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
        potion.Consumable = new Consumable(ConsumableKind.Healing, power, radius: 0);
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
        Assert.Throws<ArgumentOutOfRangeException>(() => new Consumable(ConsumableKind.Healing, power, radius: 0));
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

## Step 4: build and run

```
dotnet build
dotnet test
dotnet run --project RogueTutorial
```

Expected: a clean build, 353 passing tests, and scrolls you can aim.

### If something is wrong

| Symptom | Cause |
|---|---|
| `CS7036: no argument for 'radius'` | A `Consumable` or `ItemKind` construction not yet updated |
| Reading a scroll uses it immediately | `UseItem` is not checking `NeedsTarget` |
| Cancelling drops you onto the map | `CancelTarget` is setting `Playing` rather than `ShowingInventory` |
| A missed shot destroys the scroll | `Strike` is returning `Consumed` true when it found nothing |
| The fireball is square | The radius test is Chebyshev rather than Euclidean |
| The shown blast does not match what burns | The two tests have drifted apart; they must be the same arithmetic |
| Escape fires the scroll | `CommandReader` is checking Enter first |
| `InvalidOperationException: needs a target` | A scroll reached `UseOn`; aimed items go through `UseAt` |

## Step 5: regenerate the documentation

Skip this if you did not set up docfx in Part 1. Nothing was deleted this part:

```
dotnet docfx docfx.json --serve --port 8081
```

Expected: `Build succeeded. 0 warning(s) 0 error(s)`, and a page for `Targeting` at
<http://localhost:8081>.

---

Next: **Part 10, saving and loading.**

# Part 11: Levelling up

- [What it is](#what-it-is)
- [How to use it](#how-to-use-it)
- [How to set it up](#how-to-set-it-up)

---

# What it is

Killing things earns experience, and a level buys one of three improvements:

```
+--------------------------------------+
| Level 2                              |
|                                      |
| a) tougher    30 -> 50 hit points    |
| b) stronger   attack 99 -> 100       |
| c) sturdier   defence 2 -> 3         |
+--------------------------------------+
```

The status row carries the progress:

```
HP: 43/50 ============--  Lv 2  XP 0/65
Player hits Rat for 1 damage. Rat dies. You gain 40 experience.
You have earned level 2.
```

## Earning a level and spending it are separate

`Award` never advances by itself:

```csharp
public bool Award(int amount)
{
    Experience += amount;

    return CanAdvance;
}
```

It returns whether a level is *available*. Advancing is a second call, made after the player has
chosen what to improve.

That separation is the part. Automatic levelling is one fewer decision the player makes, and
decisions are what this part exists to add. It is also why the menu has no escape: the level is
earned, and letting the player walk away would leave them owed something nothing reminds them
about.

**A second level reopens the menu.** One kill can pay for two, and they are spent one at a time.

**Surplus carries over.** Killing something worth forty when thirty-five was needed leaves five
toward the next level rather than throwing it away.

## The menu shows the numbers

```csharp
LevelUpChoice.Stronger => $"stronger   attack {fighter.Attack} -> {fighter.Attack + AttackGain}",
```

A menu that says "stronger" without saying how much is asking for a decision with the
information withheld. The three options are deliberately not equivalent - twenty hit points,
one attack and one defence are different kinds of durable, and a choice between three versions
of the same thing is not a choice.

**Toughness heals what it adds.** A level that leaves you at the same health is a reward you
cannot feel.

## The save format changes, and that is the point

```csharp
private const int CurrentVersion = 2;
```

Part 10 put a version number in the save and refused anything that did not match. This is the
first time it does anything: a version 1 save has no record of experience or levels, so resuming
one would silently return a levelled character to level one.

Refusing is the right answer and it is not free - a player with a Part 10 save loses it. The
alternative is worse: a character that quietly forgets what it earned, with nothing reporting it.

**Refusing is not the same as crashing, and the first version got that wrong.** `SaveGame.Read`
throws, `RootScreen` called it on start, and a player with an old save met an unhandled exception
before the window opened - unable to play again without finding and deleting a file they did not
know existed.

```csharp
public static GameWorld? ReadIfReadable(string path, out string? problem)
```

Null when there is nothing to resume, null with a reason when there is something unreadable -
and the unreadable file is deleted, or every start would fail on it again. `Read` stays strict
for callers that want the failure.

It lives in `SaveGame` rather than in `RootScreen` because it decides something: throw away what
cannot be read. Policy on the screen class is policy no test can reach, which is the boundary
Part 1 drew and Part 5 spent a whole part restoring.

Levels are rebuilt by replaying rather than by reaching past the class:

```csharp
for (int gained = 1; gained < saved.Level.CurrentLevel; gained++)
{
    level.Award(level.ExperienceForNextLevel);
    level.Advance();
}

level.Award(saved.Level.Experience);
```

Slower than setting two fields, and it leaves the object in a state the class could have reached
on its own. Restoring level five without restoring what level six costs would let a veteran
advance at a beginner's price, and there is a test for exactly that.

## Fighter's numbers stop being immutable

They have been readonly since Part 6, which was right until something had to raise them. The
three setters are narrow on purpose - `RaiseAttack(1)` rather than a public setter - so the only
way a fighter changes is by gaining, never by assignment from anywhere.

Each refuses a gain below one, because a level up that improves nothing is a caller mistake
rather than a modest outcome.

## What is deliberately wrong

**There is nowhere to spend the strength.** You get better at killing the same rats in the same
dungeon. That is the same shape of problem as a cleared level with no stairs, and Part 12 is
where both are answered.

**The thresholds are a formula, not a curve.** Forty, then sixty-five, then ninety. Real games
tune this and it wants playtesting rather than arithmetic.

**Monsters do not level.** A rat on the tenth floor is the rat from the first.

**Nothing can be un-chosen.** No respec, and no way to see what you picked last time.

---

# How to use it

## Play it

```
cd parts/part-11-levelling-up
dotnet run --project RogueTutorial
```

**Any Part 10 save is refused.** The game says so in the log, deletes it and starts a new run -
that is the version check doing its job. You do not need to delete anything yourself.

Kill four rats or two kobolds and the menu appears. `a`, `b` or `c` chooses.

## Run the tests

```
dotnet test                                  # 418 tests
dotnet test --filter "Category!=EndToEnd"    # no window
```

| Test class | Level | Covers |
|---|---|---|
| [`LevelTests`](../parts/part-11-levelling-up/RogueTutorial.Tests/LevelTests.cs) | unit + integration | earning, spending, and the menu |
| [`SaveGameTests`](../parts/part-11-levelling-up/RogueTutorial.Tests/SaveGameTests.cs) | unit | levels through the round trip, and the version refusal |

## Prove the tests can fail

| Change | Expect |
|---|---|
| `Level.Advance`: zero the experience rather than subtracting | 4 fail |
| `Level`: make every level cost the same | 1 fails |
| `Level.Award`: advance automatically | 11 fail |
| `Fighter.RaiseMaximumHitPoints`: do not heal the gain | 1 fails |
| `GameWorld.ConfirmTarget`: never offer a level | 1 fails |
| `Combat`: award nothing on a kill | 5 fail |
| `SaveGame`: leave the version at 1 | 1 fails |
| `ReadIfReadable`: keep the unreadable file | 2 fail |
| `ReadIfReadable`: report no reason | 2 fail |
| `ReadIfReadable`: catch nothing | 2 fail |

---

# How to set it up

> **You are in:** your project folder, the one holding `RogueTutorial/` and `RogueTutorial.Tests/`

## Step 1: retitle the window

One line in `RogueTutorial/Program.cs`:

```csharp
const string WindowTitle = "Roguelike Tutorial - Part 11: Levelling up";
```

## Step 2: the source files

> **This is the largest step in the tutorial: 14 source files and 11 test files.** Adding a
> required argument to `Fighter` breaks every construction of one, and the project cannot
> compile again until all of them are replaced. Building partway through reports errors in files
> that are perfectly correct - `GameWorld.cs`, `SaveGame.cs` and several test files will be named
> long before you reach them.
>
> Work through Steps 2 and 3 without building, then build once.

**Each block below is the complete file.** Two are new; the rest already exist and should be
replaced entirely. Tick them off - the errors will not stop until the last one is done:

| # | File | | # | File | |
|---|---|---|---|---|---|
| 1 | `Fighter.cs` | replace | 8 | `GameCommand.cs` | replace |
| 2 | `Level.cs` | new | 9 | `CommandReader.cs` | replace |
| 3 | `LevelUpChoice.cs` | new | 10 | `SaveData.cs` | replace |
| 4 | `Entity.cs` | replace | 11 | `SaveGame.cs` | replace |
| 5 | `MonsterTable.cs` | replace | 12 | `GameWorld.cs` | replace |
| 6 | `Combat.cs` | replace | 13 | `ScreenComposer.cs` | replace |
| 7 | `Consumable.cs` | replace | 14 | `RootScreen.cs` | replace |

then Step 3's eleven test files.

### [`RogueTutorial/Fighter.cs`](../parts/part-11-levelling-up/RogueTutorial/Fighter.cs)

The Part 8 file, with an experience award and the three raises. Everything below needs it.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/Fighter.cs
+++ current/Fighter.cs
@@ -7,13 +7,14 @@
  *
  * Usage:
  *
- *     Fighter rat = new Fighter(maximumHitPoints: 4, attack: 3, defence: 0);
+ *     Fighter rat = new Fighter(maximumHitPoints: 4, attack: 3, defence: 0, experienceAwarded: 12);
  *
  *     int dealt = rat.TakeDamage(2);      // -> 2, and HitPoints falls to 2
  *     bool dead = rat.IsDead;             // -> false
  *     rat.TakeDamage(99);                 // HitPoints floors at 0 rather than going negative
  *
  *     int recovered = rat.Heal(3);        // -> how much was actually restored, capped at the maximum
+ *     rat.RaiseAttack(1);                 // levelling up; the numbers are fixed until then
  *
  * Refuses a maximum below one, and negative attack, defence, damage or healing.
  */
@@ -26,16 +27,19 @@
 internal sealed class Fighter
 {
     /// <summary>Hit points when undamaged.</summary>
-    public int MaximumHitPoints { get; }
+    public int MaximumHitPoints { get; private set; }
 
     /// <summary>Hit points now. Never below zero, never above the maximum.</summary>
     public int HitPoints { get; private set; }
 
     /// <summary>How hard this fighter hits, before the target's defence is subtracted.</summary>
-    public int Attack { get; }
+    public int Attack { get; private set; }
 
     /// <summary>How much incoming damage this fighter subtracts from every blow.</summary>
-    public int Defence { get; }
+    public int Defence { get; private set; }
+
+    /// <summary>How much experience killing this fighter is worth.</summary>
+    public int ExperienceAwarded { get; }
 
     /// <summary>True once hit points have reached zero.</summary>
     public bool IsDead => HitPoints <= 0;
@@ -46,7 +50,7 @@
     /// meant to delete, or when attack or defence is negative - a negative defence would turn
     /// every blow into a bonus.
     /// </summary>
-    public Fighter(int maximumHitPoints, int attack, int defence)
+    public Fighter(int maximumHitPoints, int attack, int defence, int experienceAwarded)
     {
         if (maximumHitPoints < 1)
         {
@@ -64,10 +68,57 @@
             throw new ArgumentOutOfRangeException(nameof(defence), defence, "Defence cannot be negative.");
         }
 
+        if (experienceAwarded < 0)
+        {
+            throw new ArgumentOutOfRangeException(
+                nameof(experienceAwarded), experienceAwarded, "Experience awarded cannot be negative.");
+        }
+
         MaximumHitPoints = maximumHitPoints;
         HitPoints = maximumHitPoints;
         Attack = attack;
         Defence = defence;
+        ExperienceAwarded = experienceAwarded;
+    }
+
+    /// <summary>
+    /// Raises the maximum and fills in the new hit points as well, so a level felt as an
+    /// improvement rather than as a longer bar to refill. Throws ArgumentOutOfRangeException on
+    /// a gain below one, which would be a level that changed nothing.
+    /// </summary>
+    public void RaiseMaximumHitPoints(int gain)
+    {
+        RejectGainBelowOne(gain);
+
+        MaximumHitPoints += gain;
+        HitPoints += gain;
+
+        Debug.Assert(HitPoints <= MaximumHitPoints, "Raising the maximum must not overfill it.");
+    }
+
+    /// <summary>Raises attack. Throws ArgumentOutOfRangeException on a gain below one.</summary>
+    public void RaiseAttack(int gain)
+    {
+        RejectGainBelowOne(gain);
+
+        Attack += gain;
+    }
+
+    /// <summary>Raises defence. Throws ArgumentOutOfRangeException on a gain below one.</summary>
+    public void RaiseDefence(int gain)
+    {
+        RejectGainBelowOne(gain);
+
+        Defence += gain;
+    }
+
+    // A gain of zero or less is a caller mistake: a level up that improves nothing.
+    private static void RejectGainBelowOne(int gain)
+    {
+        if (gain < 1)
+        {
+            throw new ArgumentOutOfRangeException(nameof(gain), gain, "A gain must be at least one.");
+        }
     }
 
     /// <summary>
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

### [`RogueTutorial/Level.cs`](../parts/part-11-levelling-up/RogueTutorial/Level.cs)

Experience, the level, and what the next one costs.

```csharp
/*
 * How far along a fighter is: their level, the experience they have earned, and how much more
 * the next level costs.
 *
 * A component like Fighter and Inventory. Monsters do not have one - they award experience
 * rather than collecting it - and a corpse stops having one along with everything else.
 *
 * The threshold grows with each level, which is what stops the twentieth level arriving as
 * quickly as the second. The formula is stated once here rather than as a table, so changing
 * how quickly a game levels is one line.
 *
 * Usage:
 *
 *     Level level = new Level();
 *
 *     bool ready = level.Award(35);          // -> true when that took it past the threshold
 *     int needed = level.ExperienceToNext;   // what is still required
 *     level.Advance();                       // spends the threshold and raises CurrentLevel
 *
 * Award never advances by itself. Reaching a level is a decision the player makes - what to
 * improve - and taking it automatically would remove one of the few choices this part is about.
 *
 * Refuses negative experience and an Advance that has not been earned.
 */

using System;
using System.Diagnostics;

namespace RogueTutorial;

internal sealed class Level
{
    // What the first level up costs. Roughly four rats or three kobolds.
    private const int BaseThreshold = 40;

    // Added to the threshold for each level already gained, so progress slows as it goes.
    private const int ThresholdGrowth = 25;

    /// <summary>How many levels have been gained. A new fighter starts at one.</summary>
    public int CurrentLevel { get; private set; } = 1;

    /// <summary>Experience earned toward the next level, not counting what has been spent.</summary>
    public int Experience { get; private set; }

    /// <summary>What the next level costs in total.</summary>
    public int ExperienceForNextLevel => BaseThreshold + ((CurrentLevel - 1) * ThresholdGrowth);

    /// <summary>How much more is needed. Zero once the level has been earned.</summary>
    public int ExperienceToNext => Math.Max(0, ExperienceForNextLevel - Experience);

    /// <summary>True when enough has been earned to advance.</summary>
    public bool CanAdvance => Experience >= ExperienceForNextLevel;

    /// <summary>
    /// Adds experience and reports whether that was enough to advance. Advancing is not done
    /// here: what to improve is the player's choice, and taking it for them removes the decision.
    /// Throws ArgumentOutOfRangeException on negative experience, which would be a loss arriving
    /// through the gaining door.
    /// </summary>
    public bool Award(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Experience cannot be negative.");
        }

        Experience += amount;

        return CanAdvance;
    }

    /// <summary>
    /// Spends the threshold and raises the level, keeping any surplus toward the next one - so a
    /// single large kill is never partly wasted. Throws InvalidOperationException when the level
    /// has not been earned, because advancing without paying is a bug in the caller rather than
    /// a generous outcome.
    /// </summary>
    public void Advance()
    {
        if (!CanAdvance)
        {
            throw new InvalidOperationException(
                $"Level {CurrentLevel + 1} costs {ExperienceForNextLevel} and only {Experience} is earned.");
        }

        Experience -= ExperienceForNextLevel;

        CurrentLevel++;

        // Surplus carries over, so it is never negative and never simply discarded.
        Debug.Assert(Experience >= 0, "Experience must never go negative when a level is spent.");
    }
}
```

### [`RogueTutorial/LevelUpChoice.cs`](../parts/part-11-levelling-up/RogueTutorial/LevelUpChoice.cs)

The three improvements, and applying one.

```csharp
/*
 * What a level buys, and applying it.
 *
 * Three options, each a different kind of durable. More health survives one more mistake, more
 * attack kills things sooner, more defence makes every future hit smaller. They are deliberately
 * not equivalent - a choice between three versions of the same thing is not a choice.
 *
 * Usage:
 *
 *     foreach (LevelUpChoice choice in LevelUpChoices.All)
 *     {
 *         string line = LevelUpChoices.Describe(choice, player.Fighter!);
 *         // -> "a) tougher   30 -> 50 hit points"
 *     }
 *
 *     LevelUpChoices.Apply(LevelUpChoice.Tougher, player.Fighter!);
 *
 * Refuses a null fighter and a choice that is not one of the three.
 */

using System;
using System.Collections.Generic;

namespace RogueTutorial;

/// <summary>What a level up improves.</summary>
internal enum LevelUpChoice
{
    /// <summary>More hit points, and the missing ones filled in as well.</summary>
    Tougher,

    /// <summary>More attack: things die in fewer blows.</summary>
    Stronger,

    /// <summary>More defence: every future hit is smaller.</summary>
    Sturdier,
}

internal static class LevelUpChoices
{
    // How much each option gives. Health is a larger number because a hit point is worth less
    // than a point of attack or defence, which apply to every exchange rather than once.
    private const int HealthGain = 20;
    private const int AttackGain = 1;
    private const int DefenceGain = 1;

    /// <summary>The three options, in the order they are offered and lettered.</summary>
    public static IReadOnlyList<LevelUpChoice> All { get; } = new[]
    {
        LevelUpChoice.Tougher,
        LevelUpChoice.Stronger,
        LevelUpChoice.Sturdier,
    };

    /// <summary>
    /// One line describing what a choice would do, with the numbers it would change - a menu
    /// that says "stronger" without saying how much is asking for a decision with the
    /// information withheld. Throws ArgumentNullException on a null fighter.
    /// </summary>
    public static string Describe(LevelUpChoice choice, Fighter fighter)
    {
        ArgumentNullException.ThrowIfNull(fighter);

        return choice switch
        {
            LevelUpChoice.Tougher =>
                $"tougher    {fighter.MaximumHitPoints} -> {fighter.MaximumHitPoints + HealthGain} hit points",
            LevelUpChoice.Stronger =>
                $"stronger   attack {fighter.Attack} -> {fighter.Attack + AttackGain}",
            LevelUpChoice.Sturdier =>
                $"sturdier   defence {fighter.Defence} -> {fighter.Defence + DefenceGain}",
            _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, "No such level up choice."),
        };
    }

    /// <summary>
    /// Applies a choice to a fighter and returns the line to log. Choosing toughness heals the
    /// new hit points as well as adding them: a level that leaves you at the same health is a
    /// reward you cannot feel. Throws ArgumentNullException on a null fighter.
    /// </summary>
    public static string Apply(LevelUpChoice choice, Fighter fighter)
    {
        ArgumentNullException.ThrowIfNull(fighter);

        switch (choice)
        {
            case LevelUpChoice.Tougher:
                fighter.RaiseMaximumHitPoints(HealthGain);
                return $"You feel tougher. Maximum health is now {fighter.MaximumHitPoints}.";

            case LevelUpChoice.Stronger:
                fighter.RaiseAttack(AttackGain);
                return $"You feel stronger. Attack is now {fighter.Attack}.";

            case LevelUpChoice.Sturdier:
                fighter.RaiseDefence(DefenceGain);
                return $"You feel sturdier. Defence is now {fighter.Defence}.";

            default:
                throw new ArgumentOutOfRangeException(nameof(choice), choice, "No such level up choice.");
        }
    }
}
```

### [`RogueTutorial/Entity.cs`](../parts/part-11-levelling-up/RogueTutorial/Entity.cs)

The Part 8 file, with the Level component.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/Entity.cs
+++ current/Entity.cs
@@ -63,6 +63,12 @@
     public Inventory? Inventory { get; set; }
 
     /// <summary>
+    /// How far along this entity is, or null when it does not collect experience. Monsters award
+    /// it rather than gathering it, so only the player has one.
+    /// </summary>
+    public Level? Level { get; set; }
+
+    /// <summary>
     /// Creates an entity at a starting cell. Throws ArgumentException on a blank name, since an
     /// unnamed entity would surface much later as an empty word in a message.
     /// </summary>
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
    /// How far along this entity is, or null when it does not collect experience. Monsters award
    /// it rather than gathering it, so only the player has one.
    /// </summary>
    public Level? Level { get; set; }

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

### [`RogueTutorial/MonsterTable.cs`](../parts/part-11-levelling-up/RogueTutorial/MonsterTable.cs)

The Part 9 file, with what each kind is worth.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/MonsterTable.cs
+++ current/MonsterTable.cs
@@ -49,6 +49,9 @@
     /// <summary>How much damage this kind shrugs off per blow.</summary>
     public int Defence { get; }
 
+    /// <summary>How much experience killing one is worth.</summary>
+    public int ExperienceAwarded { get; }
+
     /// <summary>
     /// How likely this kind is relative to the others in its table. A kind with weight 3 turns up
     /// three times as often as one with weight 1; the numbers have no meaning on their own.
@@ -60,7 +63,9 @@
     /// ArgumentOutOfRangeException on a weight below one, since a kind that can never be chosen
     /// is a table entry somebody meant to delete.
     /// </summary>
-    public MonsterKind(string name, char glyph, Color foreground, int weight, int maximumHitPoints, int attack, int defence)
+    public MonsterKind(
+        string name, char glyph, Color foreground, int weight,
+        int maximumHitPoints, int attack, int defence, int experienceAwarded)
     {
         if (string.IsNullOrWhiteSpace(name))
         {
@@ -79,10 +84,11 @@
         MaximumHitPoints = maximumHitPoints;
         Attack = attack;
         Defence = defence;
+        ExperienceAwarded = experienceAwarded;
 
         // Constructing a Fighter here would throw on bad numbers far from this call site, so
         // the same rules are enforced where the kind is declared instead.
-        _ = new Fighter(maximumHitPoints, attack, defence);
+        _ = new Fighter(maximumHitPoints, attack, defence, experienceAwarded);
     }
 }
 
@@ -132,9 +138,9 @@
         new[]
         {
             new MonsterKind("Rat", 'r', new Color(150, 120, 90), weight: 3,
-                maximumHitPoints: 4, attack: 3, defence: 0),
+                maximumHitPoints: 4, attack: 3, defence: 0, experienceAwarded: 10),
             new MonsterKind("Kobold", 'k', new Color(120, 180, 90), weight: 1,
-                maximumHitPoints: 8, attack: 4, defence: 1),
+                maximumHitPoints: 8, attack: 4, defence: 1, experienceAwarded: 25),
         },
         maximumPerRoom: 2);
 
@@ -180,7 +186,8 @@
             Entity placedMonster = new Entity(kind.Name, kind.Glyph, kind.Foreground, cell, blocksMovement: true);
 
             // The component is what lets it fight; without one it would be scenery.
-            placedMonster.Fighter = new Fighter(kind.MaximumHitPoints, kind.Attack, kind.Defence);
+            placedMonster.Fighter = new Fighter(
+                kind.MaximumHitPoints, kind.Attack, kind.Defence, kind.ExperienceAwarded);
 
             placed.Add(placedMonster);
         }
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

    /// <summary>How much experience killing one is worth.</summary>
    public int ExperienceAwarded { get; }

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
    public MonsterKind(
        string name, char glyph, Color foreground, int weight,
        int maximumHitPoints, int attack, int defence, int experienceAwarded)
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
        ExperienceAwarded = experienceAwarded;

        // Constructing a Fighter here would throw on bad numbers far from this call site, so
        // the same rules are enforced where the kind is declared instead.
        _ = new Fighter(maximumHitPoints, attack, defence, experienceAwarded);
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
                maximumHitPoints: 4, attack: 3, defence: 0, experienceAwarded: 10),
            new MonsterKind("Kobold", 'k', new Color(120, 180, 90), weight: 1,
                maximumHitPoints: 8, attack: 4, defence: 1, experienceAwarded: 25),
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
            placedMonster.Fighter = new Fighter(
                kind.MaximumHitPoints, kind.Attack, kind.Defence, kind.ExperienceAwarded);

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

### [`RogueTutorial/Combat.cs`](../parts/part-11-levelling-up/RogueTutorial/Combat.cs)

The Part 6 file, awarding experience on a kill.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/Combat.cs
+++ current/Combat.cs
@@ -83,12 +83,23 @@
 
         if (died)
         {
-            // Die clears the Fighter, so the name is read before it is called.
+            // Die clears the Fighter, so both the name and the award are read before it is
+            // called - afterwards there is nothing left to read them from.
             string targetName = target.Name;
+            int award = target.Fighter.ExperienceAwarded;
 
             target.Die();
 
             message = $"{message} {targetName} dies.";
+
+            // Only something that collects experience gets any; a monster killing another
+            // monster in a later part would earn nothing from it.
+            if (attacker.Level is not null && award > 0)
+            {
+                attacker.Level.Award(award);
+
+                message = $"{message} You gain {award} experience.";
+            }
         }
 
         return new AttackResult(dealt, died, message);
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
```

### [`RogueTutorial/Consumable.cs`](../parts/part-11-levelling-up/RogueTutorial/Consumable.cs)

The Part 9 file, awarding it for a scroll kill too.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/Consumable.cs
+++ current/Consumable.cs
@@ -172,8 +172,20 @@
 
         if (victim.Fighter.IsDead)
         {
+            int award = victim.Fighter.ExperienceAwarded;
+
             victim.Die();
+
             message = $"{message} {name} dies.";
+
+            // A scroll kill counts exactly as a melee one does, or the safest way to fight
+            // would also be the slowest way to improve.
+            if (world.Player.Level is not null && award > 0)
+            {
+                world.Player.Level.Award(award);
+
+                message = $"{message} You gain {award} experience.";
+            }
         }
 
         return new UseResult(true, message);
@@ -208,8 +220,20 @@
 
             if (entity.Fighter.IsDead)
             {
+                int award = entity.Fighter.ExperienceAwarded;
+
+                bool wasSomeoneElse = entity != world.Player;
+
                 entity.Die();
+
                 struck.Add($"{name} dies");
+
+                // The reader's own death awards nothing, which would otherwise be a way to
+                // gain experience by killing yourself.
+                if (wasSomeoneElse && world.Player.Level is not null && award > 0)
+                {
+                    world.Player.Level.Award(award);
+                }
             }
             else
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
            int award = victim.Fighter.ExperienceAwarded;

            victim.Die();

            message = $"{message} {name} dies.";

            // A scroll kill counts exactly as a melee one does, or the safest way to fight
            // would also be the slowest way to improve.
            if (world.Player.Level is not null && award > 0)
            {
                world.Player.Level.Award(award);

                message = $"{message} You gain {award} experience.";
            }
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
                int award = entity.Fighter.ExperienceAwarded;

                bool wasSomeoneElse = entity != world.Player;

                entity.Die();

                struck.Add($"{name} dies");

                // The reader's own death awards nothing, which would otherwise be a way to
                // gain experience by killing yourself.
                if (wasSomeoneElse && world.Player.Level is not null && award > 0)
                {
                    world.Player.Level.Award(award);
                }
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

### [`RogueTutorial/GameCommand.cs`](../parts/part-11-levelling-up/RogueTutorial/GameCommand.cs)

The Part 10 file, with the level up mode.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/GameCommand.cs
+++ current/GameCommand.cs
@@ -32,6 +32,12 @@
 
     /// <summary>The pack is open. Letters choose an item and Escape closes it.</summary>
     ShowingInventory,
+
+    /// <summary>
+    /// A level has been earned and is waiting to be spent. The game does not continue until it
+    /// is: an unspent level is a decision the player has already paid for.
+    /// </summary>
+    ChoosingLevelUp,
 
     /// <summary>
     /// The player has asked to abandon this run. One key confirms and anything else does not,
@@ -88,6 +94,9 @@
 
     /// <summary>Think better of it.</summary>
     CancelNewGame,
+
+    /// <summary>Spend an earned level on one of the three improvements.</summary>
+    ChooseLevelUp,
 }
 
 internal readonly struct GameCommand
@@ -146,4 +155,7 @@
 
     /// <summary>Think better of it.</summary>
     public static GameCommand CancelNewGame => new GameCommand(GameCommandKind.CancelNewGame, Point.Zero, -1);
+
+    /// <summary>Spend an earned level. The slot is which of the three was chosen.</summary>
+    public static GameCommand ChooseLevelUp(int slot) => new GameCommand(GameCommandKind.ChooseLevelUp, Point.Zero, slot);
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
    /// A level has been earned and is waiting to be spent. The game does not continue until it
    /// is: an unspent level is a decision the player has already paid for.
    /// </summary>
    ChoosingLevelUp,

    /// <summary>
    /// The player has asked to abandon this run. One key confirms and anything else does not,
    /// because a stray press should never be able to destroy a game somebody is winning.
    /// </summary>
    ConfirmingNewGame,

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

    /// <summary>Ask to abandon this run and start another.</summary>
    AskNewGame,

    /// <summary>Confirm it: the save is deleted and a fresh dungeon generated.</summary>
    ConfirmNewGame,

    /// <summary>Think better of it.</summary>
    CancelNewGame,

    /// <summary>Spend an earned level on one of the three improvements.</summary>
    ChooseLevelUp,
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

    /// <summary>Ask to abandon this run.</summary>
    public static GameCommand AskNewGame => new GameCommand(GameCommandKind.AskNewGame, Point.Zero, -1);

    /// <summary>Confirm abandoning it.</summary>
    public static GameCommand ConfirmNewGame => new GameCommand(GameCommandKind.ConfirmNewGame, Point.Zero, -1);

    /// <summary>Think better of it.</summary>
    public static GameCommand CancelNewGame => new GameCommand(GameCommandKind.CancelNewGame, Point.Zero, -1);

    /// <summary>Spend an earned level. The slot is which of the three was chosen.</summary>
    public static GameCommand ChooseLevelUp(int slot) => new GameCommand(GameCommandKind.ChooseLevelUp, Point.Zero, slot);
}
```

### [`RogueTutorial/CommandReader.cs`](../parts/part-11-levelling-up/RogueTutorial/CommandReader.cs)

The Part 10 file, reading the menu.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/CommandReader.cs
+++ current/CommandReader.cs
@@ -47,6 +47,7 @@
             GameMode.ShowingInventory => ReadInventory(pressedKeys, shiftHeld),
             GameMode.Targeting => ReadTargeting(pressedKeys),
             GameMode.ConfirmingNewGame => ReadConfirmation(pressedKeys),
+            GameMode.ChoosingLevelUp => ReadLevelUp(pressedKeys),
             _ => GameCommand.None,
         };
     }
@@ -87,6 +88,24 @@
         if (pressedKeys.Contains(Keys.N))
         {
             return GameCommand.AskNewGame;
+        }
+
+        return GameCommand.None;
+    }
+
+    // Choosing a level up: a letter picks one of the three, and nothing else applies. There is
+    // deliberately no way out - the level is earned, and leaving it unspent would mean carrying
+    // a menu around while monsters take turns.
+    private static GameCommand ReadLevelUp(IReadOnlyCollection<Keys> pressedKeys)
+    {
+        foreach (Keys key in pressedKeys)
+        {
+            if (key < Keys.A || key > Keys.Z)
+            {
+                continue;
+            }
+
+            return GameCommand.ChooseLevelUp(key - Keys.A);
         }
 
         return GameCommand.None;
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
            GameMode.ConfirmingNewGame => ReadConfirmation(pressedKeys),
            GameMode.ChoosingLevelUp => ReadLevelUp(pressedKeys),
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

        // Abandoning a run is the way out of a cleared dungeon, where nothing can kill you and
        // there is nowhere left to go.
        if (pressedKeys.Contains(Keys.N))
        {
            return GameCommand.AskNewGame;
        }

        return GameCommand.None;
    }

    // Choosing a level up: a letter picks one of the three, and nothing else applies. There is
    // deliberately no way out - the level is earned, and leaving it unspent would mean carrying
    // a menu around while monsters take turns.
    private static GameCommand ReadLevelUp(IReadOnlyCollection<Keys> pressedKeys)
    {
        foreach (Keys key in pressedKeys)
        {
            if (key < Keys.A || key > Keys.Z)
            {
                continue;
            }

            return GameCommand.ChooseLevelUp(key - Keys.A);
        }

        return GameCommand.None;
    }

    // Confirming: one key means yes and everything else means no, which is the safe way round
    // for a question whose yes destroys a run.
    private static GameCommand ReadConfirmation(IReadOnlyCollection<Keys> pressedKeys)
    {
        if (pressedKeys.Contains(Keys.Y))
        {
            return GameCommand.ConfirmNewGame;
        }

        // Any other key backs out. A player who has second thoughts should not have to find the
        // one correct way to say no.
        if (pressedKeys.Count > 0)
        {
            return GameCommand.CancelNewGame;
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

### [`RogueTutorial/SaveData.cs`](../parts/part-11-levelling-up/RogueTutorial/SaveData.cs)

The Part 10 file, with the saved level.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/SaveData.cs
+++ current/SaveData.cs
@@ -56,6 +56,19 @@
 
     /// <summary>How much it shrugs off.</summary>
     public int Defence { get; set; }
+
+    /// <summary>How much killing it is worth.</summary>
+    public int ExperienceAwarded { get; set; }
+}
+
+/// <summary>How far along a fighter is, or absent when it collects no experience.</summary>
+internal sealed class SavedLevel
+{
+    /// <summary>Levels gained so far.</summary>
+    public int CurrentLevel { get; set; }
+
+    /// <summary>Experience earned toward the next one.</summary>
+    public int Experience { get; set; }
 }
 
 /// <summary>What an item does, or absent when it is not an item.</summary>
@@ -106,6 +119,9 @@
 
     /// <summary>The ids of what it carries, in slot order.</summary>
     public List<int> CarriedIds { get; set; } = new List<int>();
+
+    /// <summary>How far along it is, or null when it collects no experience.</summary>
+    public SavedLevel? Level { get; set; }
 }
 
 /// <summary>A whole game, as stored.</summary>
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

    /// <summary>Its combat numbers, or null.</summary>
    public SavedFighter? Fighter { get; set; }

    /// <summary>What it does when used, or null.</summary>
    public SavedConsumable? Consumable { get; set; }

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

### [`RogueTutorial/SaveGame.cs`](../parts/part-11-levelling-up/RogueTutorial/SaveGame.cs)

The Part 10 file, at format version 2.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/SaveGame.cs
+++ current/SaveGame.cs
@@ -34,7 +34,11 @@
 internal static class SaveGame
 {
     // Bumped whenever the shape of SaveData changes. A save from another version is refused.
-    private const int CurrentVersion = 1;
+    //
+    // Version 2 added experience and levels. A version 1 save has no record of either, so
+    // resuming one would silently reset a character - which is exactly the case this constant
+    // was put here for in Part 10.
+    private const int CurrentVersion = 2;
 
     // Indented, because a save you can read in a text editor is a save you can debug.
     private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
@@ -58,6 +62,42 @@
         RejectBlankPath(path);
 
         File.WriteAllText(path, ToJson(Capture(world)));
+    }
+
+    /// <summary>
+    /// Reads a save if there is one and it can be read, and returns null otherwise - with
+    /// problem describing why, for the log.
+    ///
+    /// An unreadable save is deleted rather than left, or every start would try and fail on the
+    /// same file. Refusing to read it is right; leaving the caller to crash over it is not, and
+    /// a player whose save is from an older build would otherwise be unable to start the game
+    /// without finding and deleting the file themselves.
+    ///
+    /// This is separate from Read because it makes a policy decision - throw away what cannot be
+    /// read - and policy belongs somewhere a test can reach. Read stays strict for callers that
+    /// want the failure.
+    /// </summary>
+    public static GameWorld? ReadIfReadable(string path, out string? problem)
+    {
+        problem = null;
+
+        if (!Exists(path))
+        {
+            return null;
+        }
+
+        try
+        {
+            return Read(path);
+        }
+        catch (InvalidDataException error)
+        {
+            problem = error.Message;
+
+            Delete(path);
+
+            return null;
+        }
     }
 
     /// <summary>
@@ -324,6 +364,7 @@
                 HitPoints = entity.Fighter.HitPoints,
                 Attack = entity.Fighter.Attack,
                 Defence = entity.Fighter.Defence,
+                ExperienceAwarded = entity.Fighter.ExperienceAwarded,
             },
             Consumable = entity.Consumable is null ? null : new SavedConsumable
             {
@@ -335,6 +376,11 @@
             CarriedIds = entity.Inventory is null
                 ? new List<int>()
                 : entity.Inventory.Items.Select(item => ids[item]).ToList(),
+            Level = entity.Level is null ? null : new SavedLevel
+            {
+                CurrentLevel = entity.Level.CurrentLevel,
+                Experience = entity.Level.Experience,
+            },
         };
     }
 
@@ -351,13 +397,33 @@
         if (saved.Fighter is not null)
         {
             Fighter fighter = new Fighter(
-                saved.Fighter.MaximumHitPoints, saved.Fighter.Attack, saved.Fighter.Defence);
+                saved.Fighter.MaximumHitPoints,
+                saved.Fighter.Attack,
+                saved.Fighter.Defence,
+                saved.Fighter.ExperienceAwarded);
 
             // Constructed at full health, so the difference is applied as damage rather than by
             // reaching past the class and setting the field.
             fighter.TakeDamage(saved.Fighter.MaximumHitPoints - saved.Fighter.HitPoints);
 
             entity.Fighter = fighter;
+        }
+
+        if (saved.Level is not null)
+        {
+            Level level = new Level();
+
+            // Rebuilt by replaying rather than by reaching past the class: awarding the total
+            // and advancing the levels leaves it in a state the class could have reached itself.
+            for (int gained = 1; gained < saved.Level.CurrentLevel; gained++)
+            {
+                level.Award(level.ExperienceForNextLevel);
+                level.Advance();
+            }
+
+            level.Award(saved.Level.Experience);
+
+            entity.Level = level;
         }
 
         if (saved.Consumable is not null)
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
    private const int CurrentVersion = 2;

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

        // Only what was on the map goes back into the entity list; carried things live in packs.
        HashSet<int> carried = new HashSet<int>(saved.Entities.SelectMany(entity => entity.CarriedIds));

        List<Entity> onTheMap = saved.Entities
            .Where(entity => !carried.Contains(entity.Id))
            .Select(entity => byId[entity.Id])
            .ToList();

        GameWorld world = new GameWorld(map, onTheMap, byId[saved.PlayerId]);

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
            saved.BlocksMovement);

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

### [`RogueTutorial/GameWorld.cs`](../parts/part-11-levelling-up/RogueTutorial/GameWorld.cs)

The Part 10 file, offering and spending levels.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/GameWorld.cs
+++ current/GameWorld.cs
@@ -130,7 +130,10 @@
 
         // The player's numbers: enough health to survive a mistake, enough defence that a rat
         // is an inconvenience rather than a threat.
-        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2);
+        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2, experienceAwarded: 0);
+
+        // Only the player collects experience; monsters award it.
+        player.Level = new Level();
 
         // Twenty-six slots, because items are chosen by letter and there are twenty-six letters.
         player.Inventory = new Inventory(capacity: 26);
@@ -213,6 +216,8 @@
             // Attacking spends the turn, so the monsters get theirs.
             RunMonsterTurns();
 
+            OfferLevelUpIfEarned();
+
             return PlayerAction.Attacked(blocker);
         }
 
@@ -233,6 +238,53 @@
     /// list is snapshotted first because a monster may die during the round, and dead ones are
     /// skipped rather than removed.
     /// </summary>
+    /// <summary>
+    /// Opens the level up menu if one has been earned. Called after anything that could have
+    /// killed something, so the choice arrives on the turn it was paid for rather than whenever
+    /// the player next happens to look.
+    /// </summary>
+    private void OfferLevelUpIfEarned()
+    {
+        if (IsPlayerDead || Player.Level is null || !Player.Level.CanAdvance)
+        {
+            return;
+        }
+
+        Log.Add($"You have earned level {Player.Level.CurrentLevel + 1}.");
+
+        Mode = GameMode.ChoosingLevelUp;
+    }
+
+    /// <summary>
+    /// Spends an earned level on one of the three improvements and returns to play. A slot that
+    /// is not one of them is a miss rather than an error - the player pressed a letter that is
+    /// not on the menu. Returns true when a level was actually spent.
+    /// </summary>
+    public bool ChooseLevelUp(int slot)
+    {
+        if (Player.Level is null || !Player.Level.CanAdvance)
+        {
+            return false;
+        }
+
+        if (slot < 0 || slot >= LevelUpChoices.All.Count)
+        {
+            return false;
+        }
+
+        Log.Add(LevelUpChoices.Apply(LevelUpChoices.All[slot], Player.Fighter!));
+
+        Player.Level.Advance();
+
+        // A second level may have been earned by the same kill, so the menu reopens rather than
+        // dropping to the map with an unspent level in hand.
+        Mode = GameMode.Playing;
+
+        OfferLevelUpIfEarned();
+
+        return true;
+    }
+
     private void RunMonsterTurns()
     {
         // A dead player takes no more turns, and neither should anything else - the game is over
@@ -293,6 +345,13 @@
         if (mode == GameMode.Targeting)
         {
             throw new ArgumentException("Targeting is entered by using a scroll, not by SetMode.", nameof(mode));
+        }
+
+        // A level up is earned rather than requested, and leaving it by asking would let the
+        // player walk away from a decision they have already paid for.
+        if (mode == GameMode.ChoosingLevelUp)
+        {
+            throw new ArgumentException("A level up is offered when it is earned, not by SetMode.", nameof(mode));
         }
 
         Aiming = null;
@@ -477,6 +536,8 @@
             RunMonsterTurns();
         }
 
+        OfferLevelUpIfEarned();
+
         return true;
     }
 
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
        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2, experienceAwarded: 0);

        // Only the player collects experience; monsters award it.
        player.Level = new Level();

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

### [`RogueTutorial/ScreenComposer.cs`](../parts/part-11-levelling-up/RogueTutorial/ScreenComposer.cs)

The Part 10 file, drawing the menu and the progress on the status row.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/ScreenComposer.cs
+++ current/ScreenComposer.cs
@@ -110,6 +110,11 @@
             WriteConfirmation(layout, glyphs, foregrounds);
         }
 
+        if (world.Mode == GameMode.ChoosingLevelUp)
+        {
+            WriteLevelUp(world, layout, glyphs, foregrounds);
+        }
+
         return new RenderedFrame(layout.WindowWidth, layout.WindowHeight, glyphs, foregrounds);
     }
 
@@ -156,6 +161,18 @@
         // the numbers stay readable against a nearly empty bar.
         int captionLength = $"HP: {fighter.HitPoints}/{fighter.MaximumHitPoints} ".Length;
 
+        // The level and progress sit after the bar, which is why the bar is a fixed width and
+        // not the window's.
+        string progress = world.Player.Level is null
+            ? string.Empty
+            : $"  Lv {world.Player.Level.CurrentLevel}  XP {world.Player.Level.Experience}/{world.Player.Level.ExperienceForNextLevel}";
+
+        for (int col = 0; col < progress.Length && HealthBarWidth + col < layout.WindowWidth; col++)
+        {
+            glyphs[rowStart + HealthBarWidth + col] = progress[col];
+            foregrounds[rowStart + HealthBarWidth + col] = PanelText;
+        }
+
         for (int col = 0; col < bar.Length; col++)
         {
             int index = rowStart + col;
@@ -220,6 +237,45 @@
         for (int line = 0; line < lines.Count && line < layout.MapHeight; line++)
         {
             WriteLine(lines[line], line * layout.WindowWidth, layout.WindowWidth, glyphs, foregrounds, PanelText);
+        }
+    }
+
+    // Draws the level up menu, with the numbers each choice would change.
+    private static void WriteLevelUp(GameWorld world, ScreenLayout layout, char[] glyphs, Color[] foregrounds)
+    {
+        Fighter? fighter = world.Player.Fighter;
+
+        if (fighter is null)
+        {
+            return;
+        }
+
+        int width = Math.Min(InventoryWidth, layout.WindowWidth);
+        int inner = width - 4;
+
+        List<string> lines = new List<string>
+        {
+            "+" + new string('-', width - 2) + "+",
+            "| " + $"Level {world.Player.Level!.CurrentLevel + 1}".PadRight(inner) + " |",
+            "| " + "".PadRight(inner) + " |",
+        };
+
+        for (int slot = 0; slot < LevelUpChoices.All.Count; slot++)
+        {
+            // The numbers are shown, not just the names: a menu that says "stronger" without
+            // saying how much is asking for a decision with the information withheld.
+            string entry = $"{(char)('a' + slot)}) {LevelUpChoices.Describe(LevelUpChoices.All[slot], fighter)}";
+
+            string fitted = entry.Length > inner ? entry.Substring(0, inner) : entry;
+
+            lines.Add("| " + fitted.PadRight(inner) + " |");
+        }
+
+        lines.Add("+" + new string('-', width - 2) + "+");
+
+        for (int line = 0; line < lines.Count && line < layout.MapHeight; line++)
+        {
+            WriteLine(lines[line], line * layout.WindowWidth, layout.WindowWidth, glyphs, foregrounds, Crosshair);
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

### [`RogueTutorial/RootScreen.cs`](../parts/part-11-levelling-up/RogueTutorial/RootScreen.cs)

The Part 10 file, routing the choice.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/RootScreen.cs
+++ current/RootScreen.cs
@@ -68,7 +68,7 @@
         // number to Random's constructor to play the same one repeatedly while debugging.
         // A save is resumed rather than replaced. Starting a new dungeon over the top of one
         // somebody is halfway through is the one unrecoverable mistake this class could make.
-        _world = SaveGame.Exists(SavePath) ? SaveGame.Read(SavePath) : NewWorld();
+        _world = ResumeOrStart();
 
         DrawFrame();
     }
@@ -103,6 +103,36 @@
         DrawFrame();
 
         return true;
+    }
+
+    /// <summary>
+    /// Resumes the saved game, or starts a new one when there is nothing to resume.
+    ///
+    /// A save this build cannot read is reported and replaced rather than thrown. SaveGame is
+    /// right to refuse it - a half-read save is a corrupt game that looks like a working one -
+    /// but refusing is not the same as crashing, and a player whose save is from an older build
+    /// would otherwise be unable to start the game at all without finding and deleting a file.
+    /// </summary>
+    private GameWorld ResumeOrStart()
+    {
+        GameWorld? resumed = SaveGame.ReadIfReadable(SavePath, out string? problem);
+
+        if (resumed is not null)
+        {
+            return resumed;
+        }
+
+        GameWorld fresh = NewWorld();
+
+        // Said out loud, because a run vanishing without explanation looks like data loss
+        // rather than a version change.
+        if (problem is not null)
+        {
+            fresh.Log.Add($"The saved game could not be read: {problem}");
+            fresh.Log.Add("Starting a new game.");
+        }
+
+        return fresh;
     }
 
     /// <summary>
@@ -185,6 +215,10 @@
                 _world.SetMode(GameMode.Playing);
                 break;
 
+            case GameCommandKind.ChooseLevelUp:
+                _world.ChooseLevelUp(command.Slot);
+                break;
+
             case GameCommandKind.ConfirmNewGame:
                 // The old run is gone rather than kept beside the new one: this is the same
                 // ending as dying, reached on purpose instead of by accident.
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
 * It also owns the save file: resuming on start, writing after every turn that changed
 * anything, deleting it when the player dies so a run cannot be undone by reloading, and
 * replacing it when the player abandons a run - which is the way out of a cleared dungeon,
 * where nothing can kill you and there is nowhere left to go. That
 * policy lives here rather than in GameWorld because it is about this program's lifetime rather
 * than about the game's rules.
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
    // Where the game is kept between runs. Beside the executable, which is where a player
    // looking for it would think to look.
    private const string SavePath = "savegame.json";

    // How many rows of message log are shown. Five is enough to follow a fight without taking
    // so much of the window that the dungeon becomes cramped.
    private const int LogRows = 5;

    // The surface every glyph is drawn onto. One cell per grid position.
    private readonly ScreenSurface _mapSurface;

    // Where the map, the health bar and the log each sit in the window.
    private readonly ScreenLayout _layout;

    // The dungeon, everyone standing in it, and what the player has seen.
    private GameWorld _world;

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
        // A save is resumed rather than replaced. Starting a new dungeon over the top of one
        // somebody is halfway through is the one unrecoverable mistake this class could make.
        _world = ResumeOrStart();

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

        PersistOrDelete();

        // Every command that reaches here changed the screen: the map moved, the log gained a
        // line, or the pack opened or closed.
        DrawFrame();

        return true;
    }

    /// <summary>
    /// Resumes the saved game, or starts a new one when there is nothing to resume.
    ///
    /// A save this build cannot read is reported and replaced rather than thrown. SaveGame is
    /// right to refuse it - a half-read save is a corrupt game that looks like a working one -
    /// but refusing is not the same as crashing, and a player whose save is from an older build
    /// would otherwise be unable to start the game at all without finding and deleting a file.
    /// </summary>
    private GameWorld ResumeOrStart()
    {
        GameWorld? resumed = SaveGame.ReadIfReadable(SavePath, out string? problem);

        if (resumed is not null)
        {
            return resumed;
        }

        GameWorld fresh = NewWorld();

        // Said out loud, because a run vanishing without explanation looks like data loss
        // rather than a version change.
        if (problem is not null)
        {
            fresh.Log.Add($"The saved game could not be read: {problem}");
            fresh.Log.Add("Starting a new game.");
        }

        return fresh;
    }

    /// <summary>
    /// Generates a fresh dungeon at the layout's map size. No seed is given, so every run is a
    /// different one; pass a number to Random's constructor to replay the same one.
    /// </summary>
    private GameWorld NewWorld()
    {
        return GameWorld.Generate(
            _layout.WindowWidth, _layout.MapHeight, new Random(), MonsterTable.Standard, ItemTable.Standard);
    }

    /// <summary>
    /// Writes the game after every command, or deletes the save once the player is dead.
    ///
    /// Saving every turn rather than on request is what makes the save a resume point rather
    /// than a checkpoint to reload from, and deleting it on death is what stops a death being
    /// undone by quitting. A roguelike where dying is optional is a different game.
    /// </summary>
    private void PersistOrDelete()
    {
        if (_world.IsPlayerDead)
        {
            SaveGame.Delete(SavePath);
            return;
        }

        SaveGame.Write(_world, SavePath);
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

            case GameCommandKind.AskNewGame:
                _world.SetMode(GameMode.ConfirmingNewGame);
                break;

            case GameCommandKind.CancelNewGame:
                _world.SetMode(GameMode.Playing);
                break;

            case GameCommandKind.ChooseLevelUp:
                _world.ChooseLevelUp(command.Slot);
                break;

            case GameCommandKind.ConfirmNewGame:
                // The old run is gone rather than kept beside the new one: this is the same
                // ending as dying, reached on purpose instead of by accident.
                SaveGame.Delete(SavePath);
                _world = NewWorld();
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

**Each block below is the complete file.** One is new; the rest are carried over and need the
new `Fighter` argument.

### [`RogueTutorial.Tests/LevelTests.cs`](../parts/part-11-levelling-up/RogueTutorial.Tests/LevelTests.cs)

Earning, spending, and that the two are separate.

```csharp
/*
 * Unit tests for experience and levelling.
 *
 * The rule worth watching is that earning a level and spending it are separate. Award never
 * advances by itself, because what to improve is the decision this part exists to offer.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~LevelTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadConsole.Input;
using SadRogue.Primitives;
using Xunit;

public sealed class LevelTests
{
    private static GameWorld WorldWith(params Entity[] monsters)
    {
        GameMap map = new GameMap(9, 9);
        map.Fill(TileTypes.Floor);

        Entity player = new Entity("Player", '@', Color.White, new Point(4, 4), blocksMovement: true);
        player.Fighter = new Fighter(30, 99, 2, experienceAwarded: 0);
        player.Inventory = new Inventory(26);
        player.Level = new Level();

        return new GameWorld(map, new List<Entity>(monsters) { player }, player);
    }

    private static Entity Monster(string name, Point at, int award)
    {
        Entity monster = new Entity(name, name[0], Color.Red, at, blocksMovement: true);
        monster.Fighter = new Fighter(1, 1, 0, award);
        return monster;
    }

    [Fact]
    public void ANewFighterStartsAtLevelOne()
    {
        Level level = new Level();

        Assert.Equal(1, level.CurrentLevel);
        Assert.Equal(0, level.Experience);
        Assert.False(level.CanAdvance);
    }

    [Fact]
    public void EarningIsNotAdvancing()
    {
        // The whole point: reaching the threshold offers a choice rather than taking it.
        Level level = new Level();

        Assert.True(level.Award(level.ExperienceForNextLevel));

        Assert.True(level.CanAdvance);
        Assert.Equal(1, level.CurrentLevel);
    }

    [Fact]
    public void AdvancingSpendsTheThreshold()
    {
        Level level = new Level();
        int cost = level.ExperienceForNextLevel;

        level.Award(cost);
        level.Advance();

        Assert.Equal(2, level.CurrentLevel);
        Assert.Equal(0, level.Experience);
    }

    [Fact]
    public void SurplusCarriesOver()
    {
        // A single large kill must not be partly wasted.
        Level level = new Level();
        int cost = level.ExperienceForNextLevel;

        level.Award(cost + 7);
        level.Advance();

        Assert.Equal(7, level.Experience);
    }

    [Fact]
    public void EachLevelCostsMoreThanTheLast()
    {
        // Otherwise the twentieth arrives as quickly as the second.
        Level level = new Level();

        int first = level.ExperienceForNextLevel;

        level.Award(first);
        level.Advance();

        Assert.True(level.ExperienceForNextLevel > first);
    }

    [Fact]
    public void EnoughForTwoLevelsAdvancesOnlyOnce()
    {
        // Each level is a separate decision, so they are spent one at a time.
        Level level = new Level();

        level.Award(1000);
        level.Advance();

        Assert.Equal(2, level.CurrentLevel);
        Assert.True(level.CanAdvance);
    }

    [Fact]
    public void AdvancingWithoutEarningIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new Level().Advance());
    }

    [Fact]
    public void NegativeExperienceIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Level().Award(-1));
    }

    [Fact]
    public void KillingSomethingAwardsItsExperience()
    {
        Entity rat = Monster("Rat", new Point(5, 4), award: 10);
        GameWorld world = WorldWith(rat);

        world.MovePlayer(new Point(1, 0));

        Assert.Equal(10, world.Player.Level!.Experience);
    }

    [Fact]
    public void HittingWithoutKillingAwardsNothing()
    {
        Entity tough = new Entity("Brute", 'B', Color.Red, new Point(5, 4), blocksMovement: true);
        tough.Fighter = new Fighter(500, 1, 0, experienceAwarded: 50);

        GameWorld world = WorldWith(tough);

        world.MovePlayer(new Point(1, 0));

        Assert.Equal(0, world.Player.Level!.Experience);
    }

    [Fact]
    public void EnoughExperienceOpensTheMenu()
    {
        Entity rat = Monster("Rat", new Point(5, 4), award: 40);
        GameWorld world = WorldWith(rat);

        world.MovePlayer(new Point(1, 0));

        Assert.Equal(GameMode.ChoosingLevelUp, world.Mode);
    }

    [Fact]
    public void ChoosingAppliesTheImprovementAndReturnsToPlay()
    {
        Entity rat = Monster("Rat", new Point(5, 4), award: 40);
        GameWorld world = WorldWith(rat);

        world.MovePlayer(new Point(1, 0));

        int attackBefore = world.Player.Fighter!.Attack;

        // 'b' is Stronger, the second option.
        Assert.True(world.ChooseLevelUp(1));

        Assert.Equal(attackBefore + 1, world.Player.Fighter.Attack);
        Assert.Equal(2, world.Player.Level!.CurrentLevel);
        Assert.Equal(GameMode.Playing, world.Mode);
    }

    [Fact]
    public void TougherHealsTheNewHitPointsToo()
    {
        // A level that leaves you at the same health is a reward you cannot feel.
        Fighter fighter = new Fighter(30, 5, 2, experienceAwarded: 0);
        fighter.TakeDamage(10);

        fighter.RaiseMaximumHitPoints(20);

        Assert.Equal(50, fighter.MaximumHitPoints);
        Assert.Equal(40, fighter.HitPoints);
    }

    [Fact]
    public void ASecondEarnedLevelReopensTheMenu()
    {
        // One kill can pay for two. Dropping to the map with an unspent level in hand would
        // leave the player owed a decision nothing reminds them about.
        Entity rat = Monster("Rat", new Point(5, 4), award: 1000);
        GameWorld world = WorldWith(rat);

        world.MovePlayer(new Point(1, 0));
        world.ChooseLevelUp(0);

        Assert.Equal(GameMode.ChoosingLevelUp, world.Mode);
        Assert.Equal(2, world.Player.Level!.CurrentLevel);
    }

    [Fact]
    public void AnUnearnedChoiceIsAMissRatherThanAnError()
    {
        GameWorld world = WorldWith();

        Assert.False(world.ChooseLevelUp(0));
    }

    [Fact]
    public void ALetterOffTheMenuIsAMiss()
    {
        Entity rat = Monster("Rat", new Point(5, 4), award: 40);
        GameWorld world = WorldWith(rat);

        world.MovePlayer(new Point(1, 0));

        Assert.False(world.ChooseLevelUp(9));
        Assert.Equal(GameMode.ChoosingLevelUp, world.Mode);
    }

    [Fact]
    public void TheMenuCannotBeEnteredBySettingTheMode()
    {
        // A level is earned, not requested, and leaving it by asking would let the player walk
        // away from a decision they have already paid for.
        GameWorld world = WorldWith();

        Assert.Throws<ArgumentException>(() => world.SetMode(GameMode.ChoosingLevelUp));
    }

    [Fact]
    public void LettersChooseWhileTheMenuIsUp()
    {
        Assert.Equal(0, CommandReader.Read(new[] { Keys.A }, GameMode.ChoosingLevelUp).Slot);
        Assert.Equal(2, CommandReader.Read(new[] { Keys.C }, GameMode.ChoosingLevelUp).Slot);
    }

    [Fact]
    public void NothingElseWorksWhileTheMenuIsUp()
    {
        // No escape: the level is earned and the game does not continue until it is spent.
        foreach (Keys key in new[] { Keys.Escape, Keys.Left, Keys.Enter })
        {
            Assert.Equal(
                GameCommandKind.None,
                CommandReader.Read(new[] { key }, GameMode.ChoosingLevelUp).Kind);
        }
    }

    [Fact]
    public void EveryChoiceSaysWhatItWouldChange()
    {
        // A menu that says "stronger" without saying how much is asking for a decision with the
        // information withheld.
        Fighter fighter = new Fighter(30, 5, 2, experienceAwarded: 0);

        foreach (LevelUpChoice choice in LevelUpChoices.All)
        {
            string described = LevelUpChoices.Describe(choice, fighter);

            Assert.Contains("->", described);
        }
    }

    [Fact]
    public void AScrollKillCountsTheSame()
    {
        // Otherwise the safest way to fight would also be the slowest way to improve.
        Entity rat = Monster("Rat", new Point(6, 4), award: 10);
        GameWorld world = WorldWith(rat);

        Entity scroll = new Entity("scroll", '?', Color.Yellow, world.Player.Position, blocksMovement: false);
        scroll.Consumable = new Consumable(ConsumableKind.Lightning, power: 50, radius: 0);
        world.Player.Inventory!.TryAdd(scroll);

        world.UseItem(0);
        world.ConfirmTarget();

        Assert.Equal(10, world.Player.Level!.Experience);
    }

    [Fact]
    public void ANullFighterIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => LevelUpChoices.Describe(LevelUpChoice.Tougher, null!));
        Assert.Throws<ArgumentNullException>(() => LevelUpChoices.Apply(LevelUpChoice.Tougher, null!));
    }

    [Fact]
    public void AGainOfNothingIsRejected()
    {
        Fighter fighter = new Fighter(30, 5, 2, experienceAwarded: 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => fighter.RaiseAttack(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => fighter.RaiseDefence(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => fighter.RaiseMaximumHitPoints(0));
    }
}
```

### [`RogueTutorial.Tests/SaveGameTests.cs`](../parts/part-11-levelling-up/RogueTutorial.Tests/SaveGameTests.cs)

The Part 10 file, with levels in the round trip, the version refusal, and recovering from it.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/SaveGameTests.cs
+++ current/SaveGameTests.cs
@@ -148,7 +148,7 @@
         GameWorld original = GeneratedWorld(4);
 
         Entity rat = new Entity("Rat", 'r', Color.Red, new Point(1, 1), blocksMovement: true);
-        rat.Fighter = new Fighter(4, 3, 0);
+        rat.Fighter = new Fighter(4, 3, 0, experienceAwarded: 0);
         rat.Die();
 
         GameWorld world = new GameWorld(original.Map, new List<Entity> { rat, original.Player }, original.Player);
@@ -159,6 +159,69 @@
 
         Assert.Null(restoredRat.Fighter);
         Assert.False(restoredRat.BlocksMovement);
+    }
+
+    [Fact]
+    public void LevelAndExperienceComeBack()
+    {
+        GameWorld original = GeneratedWorld(21);
+
+        original.Player.Level!.Award(original.Player.Level.ExperienceForNextLevel + 5);
+        original.Player.Level.Advance();
+        original.Player.Fighter!.RaiseAttack(1);
+
+        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));
+
+        Assert.Equal(2, restored.Player.Level!.CurrentLevel);
+        Assert.Equal(5, restored.Player.Level.Experience);
+        Assert.Equal(6, restored.Player.Fighter!.Attack);
+    }
+
+    [Fact]
+    public void TheNextThresholdComesBackWithTheLevel()
+    {
+        // Restoring the level but not what the next one costs would make a level-five character
+        // advance at a level-one price.
+        GameWorld original = GeneratedWorld(22);
+
+        for (int gained = 0; gained < 3; gained++)
+        {
+            original.Player.Level!.Award(original.Player.Level.ExperienceForNextLevel);
+            original.Player.Level.Advance();
+        }
+
+        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));
+
+        Assert.Equal(
+            original.Player.Level!.ExperienceForNextLevel,
+            restored.Player.Level!.ExperienceForNextLevel);
+    }
+
+    [Fact]
+    public void MonstersAwardTheSameExperienceAfterLoading()
+    {
+        GameWorld original = GeneratedWorld(23);
+
+        string before = string.Join(";", original.Entities
+            .Where(entity => entity.Fighter is not null)
+            .Select(entity => $"{entity.Name}:{entity.Fighter!.ExperienceAwarded}"));
+
+        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));
+
+        Assert.Equal(before, string.Join(";", restored.Entities
+            .Where(entity => entity.Fighter is not null)
+            .Select(entity => $"{entity.Name}:{entity.Fighter!.ExperienceAwarded}")));
+    }
+
+    [Fact]
+    public void APart10SaveIsRefusedRatherThanResettingTheCharacter()
+    {
+        // The first real use of the version check. A version 1 save has no record of experience
+        // or levels, so resuming one would silently return a levelled character to level one.
+        SavedWorld saved = SaveGame.Capture(GeneratedWorld(24));
+        saved.Version = 1;
+
+        Assert.Throws<InvalidDataException>(() => SaveGame.Restore(saved));
     }
 
     [Fact]
@@ -219,6 +282,100 @@
         finally
         {
             // The test must not leave a file behind whether it passed or not.
+            if (File.Exists(path))
+            {
+                File.Delete(path);
+            }
+        }
+    }
+
+    [Fact]
+    public void AnUnreadableSaveIsReplacedRatherThanThrown()
+    {
+        // Refusing to read it is right. Crashing over it is not: a player whose save is from an
+        // older build would otherwise be unable to start the game without deleting a file they
+        // do not know exists.
+        string path = Path.Combine(Path.GetTempPath(), $"roguetutorial-{Guid.NewGuid():N}.json");
+
+        try
+        {
+            SavedWorld saved = SaveGame.Capture(GeneratedWorld(31));
+            saved.Version = 1;
+
+            File.WriteAllText(path, SaveGame.ToJson(saved));
+
+            GameWorld? resumed = SaveGame.ReadIfReadable(path, out string? problem);
+
+            Assert.Null(resumed);
+            Assert.NotNull(problem);
+            Assert.Contains("version 1", problem);
+
+            // Deleted, or every start would try and fail on the same file forever.
+            Assert.False(SaveGame.Exists(path));
+        }
+        finally
+        {
+            if (File.Exists(path))
+            {
+                File.Delete(path);
+            }
+        }
+    }
+
+    [Fact]
+    public void NoSaveIsNotAProblemToReport()
+    {
+        // Nothing to resume is the ordinary first run, not a fault worth a message.
+        string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");
+
+        Assert.Null(SaveGame.ReadIfReadable(path, out string? problem));
+        Assert.Null(problem);
+    }
+
+    [Fact]
+    public void AReadableSaveComesBackUnharmed()
+    {
+        string path = Path.Combine(Path.GetTempPath(), $"roguetutorial-{Guid.NewGuid():N}.json");
+
+        try
+        {
+            GameWorld original = GeneratedWorld(32);
+            SaveGame.Write(original, path);
+
+            GameWorld? resumed = SaveGame.ReadIfReadable(path, out string? problem);
+
+            Assert.NotNull(resumed);
+            Assert.Null(problem);
+            Assert.Equal(Picture(original), Picture(resumed));
+
+            // A save that read correctly stays on disk.
+            Assert.True(SaveGame.Exists(path));
+        }
+        finally
+        {
+            if (File.Exists(path))
+            {
+                File.Delete(path);
+            }
+        }
+    }
+
+    [Fact]
+    public void CorruptTextIsReplacedToo()
+    {
+        // Not only a version mismatch: a truncated file has to be survivable as well.
+        string path = Path.Combine(Path.GetTempPath(), $"roguetutorial-{Guid.NewGuid():N}.json");
+
+        try
+        {
+            File.WriteAllText(path, "{ \"Version\": 2, \"Width\": ");
+
+            Assert.Null(SaveGame.ReadIfReadable(path, out string? problem));
+            Assert.NotNull(problem);
+            Assert.False(SaveGame.Exists(path));
+        }
+        finally
+        {
             if (File.Exists(path))
             {
                 File.Delete(path);
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
        return GameWorld.Generate(40, 20, new Random(seed), MonsterTable.Standard, ItemTable.Standard);
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

        Entity potion = new Entity("potion", '!', Color.Magenta, original.Player.Position, blocksMovement: false);
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

        Entity potion = new Entity("potion", '!', Color.Magenta, original.Player.Position, blocksMovement: false);
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

        Entity rat = new Entity("Rat", 'r', Color.Red, new Point(1, 1), blocksMovement: true);
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

        Entity scroll = new Entity("scroll", '?', Color.Yellow, original.Player.Position, blocksMovement: false);
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

        // A dungeon is rock and floor, so two entries cover every cell of it.
        Assert.Equal(2, saved.TilePalette.Count);
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
}
```

### [`RogueTutorial.Tests/FighterTests.cs`](../parts/part-11-levelling-up/RogueTutorial.Tests/FighterTests.cs)

The Part 6 file, updated for the award argument.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/FighterTests.cs
+++ current/FighterTests.cs
@@ -14,7 +14,7 @@
     [Fact]
     public void AFighterStartsAtFullHealth()
     {
-        Fighter fighter = new Fighter(maximumHitPoints: 10, attack: 3, defence: 1);
+        Fighter fighter = new Fighter(maximumHitPoints: 10, attack: 3, defence: 1, experienceAwarded: 0);
 
         Assert.Equal(10, fighter.MaximumHitPoints);
         Assert.Equal(10, fighter.HitPoints);
@@ -24,7 +24,7 @@
     [Fact]
     public void DamageComesOffHitPoints()
     {
-        Fighter fighter = new Fighter(10, 3, 1);
+        Fighter fighter = new Fighter(10, 3, 1, experienceAwarded: 0);
 
         int lost = fighter.TakeDamage(4);
 
@@ -36,7 +36,7 @@
     public void HitPointsFloorAtZero()
     {
         // A corpse is never more dead than another, and a negative total would print as one.
-        Fighter fighter = new Fighter(10, 3, 1);
+        Fighter fighter = new Fighter(10, 3, 1, experienceAwarded: 0);
 
         int lost = fighter.TakeDamage(99);
 
@@ -48,7 +48,7 @@
     [Fact]
     public void ExactlyLethalDamageKills()
     {
-        Fighter fighter = new Fighter(4, 3, 0);
+        Fighter fighter = new Fighter(4, 3, 0, experienceAwarded: 0);
 
         fighter.TakeDamage(4);
 
@@ -59,7 +59,7 @@
     public void OneShortOfLethalDoesNot()
     {
         // The boundary either side of death, which is where an off-by-one would live.
-        Fighter fighter = new Fighter(4, 3, 0);
+        Fighter fighter = new Fighter(4, 3, 0, experienceAwarded: 0);
 
         fighter.TakeDamage(3);
 
@@ -70,7 +70,7 @@
     [Fact]
     public void ZeroDamageChangesNothing()
     {
-        Fighter fighter = new Fighter(10, 3, 1);
+        Fighter fighter = new Fighter(10, 3, 1, experienceAwarded: 0);
 
         Assert.Equal(0, fighter.TakeDamage(0));
         Assert.Equal(10, fighter.HitPoints);
@@ -83,8 +83,8 @@
     [InlineData(2, 9, 0)]     // out-defended: floored at zero, never negative
     public void DamageIsAttackLessDefenceFlooredAtZero(int attack, int defence, int expected)
     {
-        Fighter attacker = new Fighter(10, attack, 0);
-        Fighter target = new Fighter(10, 0, defence);
+        Fighter attacker = new Fighter(10, attack, 0, experienceAwarded: 0);
+        Fighter target = new Fighter(10, 0, defence, experienceAwarded: 0);
 
         Assert.Equal(expected, attacker.DamageAgainst(target));
     }
@@ -93,8 +93,8 @@
     public void AFighterThatOutDefendsIsNeverHealed()
     {
         // The reason for the floor: without it a heavily armoured target would gain health.
-        Fighter weak = new Fighter(10, 1, 0);
-        Fighter armoured = new Fighter(10, 0, 5);
+        Fighter weak = new Fighter(10, 1, 0, experienceAwarded: 0);
+        Fighter armoured = new Fighter(10, 0, 5, experienceAwarded: 0);
 
         armoured.TakeDamage(weak.DamageAgainst(armoured));
 
@@ -106,21 +106,21 @@
     [InlineData(-1)]
     public void AFighterThatBeginsDeadIsRejected(int maximumHitPoints)
     {
-        Assert.Throws<ArgumentOutOfRangeException>(() => new Fighter(maximumHitPoints, 3, 1));
+        Assert.Throws<ArgumentOutOfRangeException>(() => new Fighter(maximumHitPoints, 3, 1, experienceAwarded: 0));
     }
 
     [Fact]
     public void NegativeAttackOrDefenceIsRejected()
     {
-        Assert.Throws<ArgumentOutOfRangeException>(() => new Fighter(10, -1, 0));
-        Assert.Throws<ArgumentOutOfRangeException>(() => new Fighter(10, 0, -1));
+        Assert.Throws<ArgumentOutOfRangeException>(() => new Fighter(10, -1, 0, experienceAwarded: 0));
+        Assert.Throws<ArgumentOutOfRangeException>(() => new Fighter(10, 0, -1, experienceAwarded: 0));
     }
 
     [Fact]
     public void NegativeDamageIsRejected()
     {
         // Healing has its own path in a later part; it must not arrive through this one.
-        Fighter fighter = new Fighter(10, 3, 1);
+        Fighter fighter = new Fighter(10, 3, 1, experienceAwarded: 0);
 
         Assert.Throws<ArgumentOutOfRangeException>(() => fighter.TakeDamage(-1));
     }
@@ -128,7 +128,7 @@
     [Fact]
     public void ANullTargetIsRejected()
     {
-        Fighter fighter = new Fighter(10, 3, 1);
+        Fighter fighter = new Fighter(10, 3, 1, experienceAwarded: 0);
 
         Assert.Throws<ArgumentNullException>(() => fighter.DamageAgainst(null!));
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
        Fighter attacker = new Fighter(10, attack, 0, experienceAwarded: 0);
        Fighter target = new Fighter(10, 0, defence, experienceAwarded: 0);

        Assert.Equal(expected, attacker.DamageAgainst(target));
    }

    [Fact]
    public void AFighterThatOutDefendsIsNeverHealed()
    {
        // The reason for the floor: without it a heavily armoured target would gain health.
        Fighter weak = new Fighter(10, 1, 0, experienceAwarded: 0);
        Fighter armoured = new Fighter(10, 0, 5, experienceAwarded: 0);

        armoured.TakeDamage(weak.DamageAgainst(armoured));

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

    [Fact]
    public void ANullTargetIsRejected()
    {
        Fighter fighter = new Fighter(10, 3, 1, experienceAwarded: 0);

        Assert.Throws<ArgumentNullException>(() => fighter.DamageAgainst(null!));
    }
}
```

### [`RogueTutorial.Tests/CombatTests.cs`](../parts/part-11-levelling-up/RogueTutorial.Tests/CombatTests.cs)

Likewise.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/CombatTests.cs
+++ current/CombatTests.cs
@@ -14,7 +14,7 @@
     private static Entity FighterEntity(string name, int hitPoints, int attack, int defence)
     {
         Entity entity = new Entity(name, name[0], Color.White, new Point(0, 0), blocksMovement: true);
-        entity.Fighter = new Fighter(hitPoints, attack, defence);
+        entity.Fighter = new Fighter(hitPoints, attack, defence, experienceAwarded: 0);
         return entity;
     }
 
```
<!-- generated-diff -->

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
        entity.Fighter = new Fighter(hitPoints, attack, defence, experienceAwarded: 0);
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

### [`RogueTutorial.Tests/MonsterTurnTests.cs`](../parts/part-11-levelling-up/RogueTutorial.Tests/MonsterTurnTests.cs)

Likewise.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/MonsterTurnTests.cs
+++ current/MonsterTurnTests.cs
@@ -31,7 +31,7 @@
         }
 
         Entity player = new Entity("Player", '@', Color.White, playerAt, blocksMovement: true);
-        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2);
+        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2, experienceAwarded: 0);
 
         List<Entity> entities = new List<Entity>(monsters) { player };
 
@@ -41,7 +41,7 @@
     private static Entity Monster(string name, Point at, int hitPoints, int attack, int defence)
     {
         Entity monster = new Entity(name, name[0], Color.Red, at, blocksMovement: true);
-        monster.Fighter = new Fighter(hitPoints, attack, defence);
+        monster.Fighter = new Fighter(hitPoints, attack, defence, experienceAwarded: 0);
         return monster;
     }
 
@@ -113,7 +113,7 @@
         map.SetTile(new Point(5, 2), TileTypes.Wall);
 
         Entity player = new Entity("Player", '@', Color.White, new Point(2, 2), blocksMovement: true);
-        player.Fighter = new Fighter(30, 5, 2);
+        player.Fighter = new Fighter(30, 5, 2, experienceAwarded: 0);
 
         Entity rat = Monster("Rat", new Point(8, 2), 4, 3, 0);
 
@@ -141,7 +141,7 @@
         }
 
         Entity player = new Entity("Player", '@', Color.White, new Point(10, 2), blocksMovement: true);
-        player.Fighter = new Fighter(30, 5, 2);
+        player.Fighter = new Fighter(30, 5, 2, experienceAwarded: 0);
 
         Entity rat = Monster("Rat", new Point(6, 2), 4, 3, 0);
 
@@ -190,7 +190,7 @@
         map.SetTile(new Point(3, 3), TileTypes.Wall);
 
         Entity player = new Entity("Player", '@', Color.White, new Point(3, 2), blocksMovement: true);
-        player.Fighter = new Fighter(30, 5, 2);
+        player.Fighter = new Fighter(30, 5, 2, experienceAwarded: 0);
 
         Entity rat = Monster("Rat", new Point(3, 4), 4, 3, 0);
 
```
<!-- generated-diff -->

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
        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2, experienceAwarded: 0);

        List<Entity> entities = new List<Entity>(monsters) { player };

        return new GameWorld(map, entities, player);
    }

    private static Entity Monster(string name, Point at, int hitPoints, int attack, int defence)
    {
        Entity monster = new Entity(name, name[0], Color.Red, at, blocksMovement: true);
        monster.Fighter = new Fighter(hitPoints, attack, defence, experienceAwarded: 0);
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
        player.Fighter = new Fighter(30, 5, 2, experienceAwarded: 0);

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
        player.Fighter = new Fighter(30, 5, 2, experienceAwarded: 0);

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
        player.Fighter = new Fighter(30, 5, 2, experienceAwarded: 0);

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

### [`RogueTutorial.Tests/MonsterTableTests.cs`](../parts/part-11-levelling-up/RogueTutorial.Tests/MonsterTableTests.cs)

Likewise.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/MonsterTableTests.cs
+++ current/MonsterTableTests.cs
@@ -31,7 +31,7 @@
     private static MonsterTable RatsOnly(int maximumPerRoom)
     {
         return new MonsterTable(
-            new[] { new MonsterKind("Rat", 'r', Color.Red, weight: 1, maximumHitPoints: 4, attack: 3, defence: 0) },
+            new[] { new MonsterKind("Rat", 'r', Color.Red, weight: 1, maximumHitPoints: 4, attack: 3, defence: 0, experienceAwarded: 0) },
             maximumPerRoom);
     }
 
@@ -184,8 +184,8 @@
         MonsterTable table = new MonsterTable(
             new[]
             {
-                new MonsterKind("Common", 'c', Color.Red, weight: 3, maximumHitPoints: 4, attack: 3, defence: 0),
-                new MonsterKind("Rare", 'x', Color.Blue, weight: 1, maximumHitPoints: 4, attack: 3, defence: 0),
+                new MonsterKind("Common", 'c', Color.Red, weight: 3, maximumHitPoints: 4, attack: 3, defence: 0, experienceAwarded: 0),
+                new MonsterKind("Rare", 'x', Color.Blue, weight: 1, maximumHitPoints: 4, attack: 3, defence: 0, experienceAwarded: 0),
             },
             maximumPerRoom: 2);
 
@@ -213,7 +213,7 @@
     [Fact]
     public void AKindWithNoNameIsRejected()
     {
-        Assert.Throws<ArgumentException>(() => new MonsterKind("  ", 'r', Color.Red, weight: 1, maximumHitPoints: 4, attack: 3, defence: 0));
+        Assert.Throws<ArgumentException>(() => new MonsterKind("  ", 'r', Color.Red, weight: 1, maximumHitPoints: 4, attack: 3, defence: 0, experienceAwarded: 0));
     }
 
     [Theory]
@@ -221,7 +221,7 @@
     [InlineData(-1)]
     public void AWeightThatCanNeverBeChosenIsRejected(int weight)
     {
-        Assert.Throws<ArgumentOutOfRangeException>(() => new MonsterKind("Rat", 'r', Color.Red, weight, maximumHitPoints: 4, attack: 3, defence: 0));
+        Assert.Throws<ArgumentOutOfRangeException>(() => new MonsterKind("Rat", 'r', Color.Red, weight, maximumHitPoints: 4, attack: 3, defence: 0, experienceAwarded: 0));
     }
 
     [Fact]
@@ -234,7 +234,7 @@
     public void ANegativeMaximumIsRejected()
     {
         Assert.Throws<ArgumentOutOfRangeException>(
-            () => new MonsterTable(new[] { new MonsterKind("Rat", 'r', Color.Red, 1, maximumHitPoints: 4, attack: 3, defence: 0) }, maximumPerRoom: -1));
+            () => new MonsterTable(new[] { new MonsterKind("Rat", 'r', Color.Red, 1, maximumHitPoints: 4, attack: 3, defence: 0, experienceAwarded: 0) }, maximumPerRoom: -1));
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
            new[] { new MonsterKind("Rat", 'r', Color.Red, weight: 1, maximumHitPoints: 4, attack: 3, defence: 0, experienceAwarded: 0) },
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
                new MonsterKind("Common", 'c', Color.Red, weight: 3, maximumHitPoints: 4, attack: 3, defence: 0, experienceAwarded: 0),
                new MonsterKind("Rare", 'x', Color.Blue, weight: 1, maximumHitPoints: 4, attack: 3, defence: 0, experienceAwarded: 0),
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
        Assert.Throws<ArgumentException>(() => new MonsterKind("  ", 'r', Color.Red, weight: 1, maximumHitPoints: 4, attack: 3, defence: 0, experienceAwarded: 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AWeightThatCanNeverBeChosenIsRejected(int weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MonsterKind("Rat", 'r', Color.Red, weight, maximumHitPoints: 4, attack: 3, defence: 0, experienceAwarded: 0));
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
            () => new MonsterTable(new[] { new MonsterKind("Rat", 'r', Color.Red, 1, maximumHitPoints: 4, attack: 3, defence: 0, experienceAwarded: 0) }, maximumPerRoom: -1));
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

### [`RogueTutorial.Tests/GameWorldTests.cs`](../parts/part-11-levelling-up/RogueTutorial.Tests/GameWorldTests.cs)

Likewise.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/GameWorldTests.cs
+++ current/GameWorldTests.cs
@@ -33,7 +33,7 @@
         }
 
         Entity player = new Entity("Player", '@', Color.White, new Point(4, 4), blocksMovement: true);
-        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2);
+        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2, experienceAwarded: 0);
 
         List<Entity> entities = new List<Entity>(extraEntities) { player };
 
@@ -241,7 +241,7 @@
     {
         GameMap map = new GameMap(5, 5);
         Entity player = new Entity("Player", '@', Color.White, new Point(2, 2), blocksMovement: true);
-        player.Fighter = new Fighter(10, 1, 0);
+        player.Fighter = new Fighter(10, 1, 0, experienceAwarded: 0);
 
         Assert.Throws<ArgumentException>(() => new GameWorld(map, Array.Empty<Entity>(), player));
     }
@@ -251,7 +251,7 @@
     {
         GameMap map = new GameMap(5, 5);
         Entity player = new Entity("Player", '@', Color.White, new Point(2, 2), blocksMovement: true);
-        player.Fighter = new Fighter(10, 1, 0);
+        player.Fighter = new Fighter(10, 1, 0, experienceAwarded: 0);
 
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
        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2, experienceAwarded: 0);

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
        player.Fighter = new Fighter(10, 1, 0, experienceAwarded: 0);

        Assert.Throws<ArgumentException>(() => new GameWorld(map, Array.Empty<Entity>(), player));
    }

    [Fact]
    public void ANullArgumentIsRejected()
    {
        GameMap map = new GameMap(5, 5);
        Entity player = new Entity("Player", '@', Color.White, new Point(2, 2), blocksMovement: true);
        player.Fighter = new Fighter(10, 1, 0, experienceAwarded: 0);

        Assert.Throws<ArgumentNullException>(() => new GameWorld(null!, new[] { player }, player));
        Assert.Throws<ArgumentNullException>(() => new GameWorld(map, null!, player));
        Assert.Throws<ArgumentNullException>(() => new GameWorld(map, new[] { player }, null!));
        Assert.Throws<ArgumentNullException>(() => GameWorld.Generate(40, 25, null!, MonsterTable.Standard, ItemTable.Standard));
        Assert.Throws<ArgumentNullException>(() => GameWorld.Generate(40, 25, new Random(1), null!, ItemTable.Standard));
        Assert.Throws<ArgumentNullException>(() => GameWorld.Generate(40, 25, new Random(1), MonsterTable.Standard, null!));
    }
}
```

### [`RogueTutorial.Tests/ItemUseTests.cs`](../parts/part-11-levelling-up/RogueTutorial.Tests/ItemUseTests.cs)

Likewise.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/ItemUseTests.cs
+++ current/ItemUseTests.cs
@@ -24,7 +24,7 @@
         map.Fill(TileTypes.Floor);
 
         Entity player = new Entity("Player", '@', Color.White, new Point(4, 4), blocksMovement: true);
-        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2);
+        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2, experienceAwarded: 0);
         player.Inventory = new Inventory(capacity: 26);
 
         List<Entity> entities = new List<Entity>(onTheFloor) { player };
@@ -42,7 +42,7 @@
     [Fact]
     public void HealingRestoresHitPoints()
     {
-        Fighter fighter = new Fighter(30, 5, 2);
+        Fighter fighter = new Fighter(30, 5, 2, experienceAwarded: 0);
         fighter.TakeDamage(10);
 
         Assert.Equal(6, fighter.Heal(6));
@@ -52,7 +52,7 @@
     [Fact]
     public void HealingCannotPassTheMaximum()
     {
-        Fighter fighter = new Fighter(30, 5, 2);
+        Fighter fighter = new Fighter(30, 5, 2, experienceAwarded: 0);
         fighter.TakeDamage(4);
 
         // Only the missing four can be restored, whatever the potion promises.
@@ -63,7 +63,7 @@
     [Fact]
     public void HealingAtFullHealthRecoversNothing()
     {
-        Fighter fighter = new Fighter(30, 5, 2);
+        Fighter fighter = new Fighter(30, 5, 2, experienceAwarded: 0);
 
         Assert.Equal(0, fighter.Heal(10));
     }
@@ -271,7 +271,7 @@
     public void NegativeHealingIsRejected()
     {
         // Damage arriving through the healing door would be a very quiet bug.
-        Fighter fighter = new Fighter(30, 5, 2);
+        Fighter fighter = new Fighter(30, 5, 2, experienceAwarded: 0);
 
         Assert.Throws<ArgumentOutOfRangeException>(() => fighter.Heal(-1));
     }
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
        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2, experienceAwarded: 0);
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
        Fighter fighter = new Fighter(30, 5, 2, experienceAwarded: 0);
        fighter.TakeDamage(10);

        Assert.Equal(6, fighter.Heal(6));
        Assert.Equal(26, fighter.HitPoints);
    }

    [Fact]
    public void HealingCannotPassTheMaximum()
    {
        Fighter fighter = new Fighter(30, 5, 2, experienceAwarded: 0);
        fighter.TakeDamage(4);

        // Only the missing four can be restored, whatever the potion promises.
        Assert.Equal(4, fighter.Heal(99));
        Assert.Equal(30, fighter.HitPoints);
    }

    [Fact]
    public void HealingAtFullHealthRecoversNothing()
    {
        Fighter fighter = new Fighter(30, 5, 2, experienceAwarded: 0);

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
        Fighter fighter = new Fighter(30, 5, 2, experienceAwarded: 0);

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

### [`RogueTutorial.Tests/TargetingTests.cs`](../parts/part-11-levelling-up/RogueTutorial.Tests/TargetingTests.cs)

Likewise.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/TargetingTests.cs
+++ current/TargetingTests.cs
@@ -25,7 +25,7 @@
         map.Fill(TileTypes.Floor);
 
         Entity player = new Entity("Player", '@', Color.White, new Point(7, 7), blocksMovement: true);
-        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2);
+        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2, experienceAwarded: 0);
         player.Inventory = new Inventory(capacity: 26);
 
         List<Entity> entities = new List<Entity>(monsters) { player };
@@ -36,7 +36,7 @@
     private static Entity Monster(string name, Point at, int hitPoints)
     {
         Entity monster = new Entity(name, name[0], Color.Red, at, blocksMovement: true);
-        monster.Fighter = new Fighter(hitPoints, attack: 3, defence: 0);
+        monster.Fighter = new Fighter(hitPoints, attack: 3, defence: 0, experienceAwarded: 0);
         return monster;
     }
 
@@ -289,7 +289,7 @@
         Assert.Throws<InvalidOperationException>(() => scroll.Consumable!.UseOn(new Entity(
             "Player", '@', Color.White, new Point(0, 0), blocksMovement: true)
         {
-            Fighter = new Fighter(30, 5, 2),
+            Fighter = new Fighter(30, 5, 2, experienceAwarded: 0),
         }));
     }
 
```
<!-- generated-diff -->

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
        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2, experienceAwarded: 0);
        player.Inventory = new Inventory(capacity: 26);

        List<Entity> entities = new List<Entity>(monsters) { player };

        return new GameWorld(map, entities, player);
    }

    private static Entity Monster(string name, Point at, int hitPoints)
    {
        Entity monster = new Entity(name, name[0], Color.Red, at, blocksMovement: true);
        monster.Fighter = new Fighter(hitPoints, attack: 3, defence: 0, experienceAwarded: 0);
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
            Fighter = new Fighter(30, 5, 2, experienceAwarded: 0),
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

### [`RogueTutorial.Tests/ScreenComposerTests.cs`](../parts/part-11-levelling-up/RogueTutorial.Tests/ScreenComposerTests.cs)

Likewise.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/ScreenComposerTests.cs
+++ current/ScreenComposerTests.cs
@@ -24,7 +24,7 @@
         map.Fill(TileTypes.Floor);
 
         Entity player = new Entity("Player", '@', Color.White, new Point(2, 1), blocksMovement: true);
-        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2);
+        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2, experienceAwarded: 0);
 
         return new GameWorld(map, new List<Entity> { player }, player);
     }
@@ -212,7 +212,7 @@
         tooTall.Fill(TileTypes.Floor);
 
         Entity player = new Entity("Player", '@', Color.White, new Point(1, 1), blocksMovement: true);
-        player.Fighter = new Fighter(30, 5, 2);
+        player.Fighter = new Fighter(30, 5, 2, experienceAwarded: 0);
 
         GameWorld world = new GameWorld(tooTall, new List<Entity> { player }, player);
 
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
        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2, experienceAwarded: 0);

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
        player.Fighter = new Fighter(30, 5, 2, experienceAwarded: 0);

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

### [`RogueTutorial.Tests/NewGameTests.cs`](../parts/part-11-levelling-up/RogueTutorial.Tests/NewGameTests.cs)

Likewise.

<!-- generated-diff -->
**Changed from Part 10.** The complete file follows; this is only what moved:

```diff
--- part-10-saving-and-loading/NewGameTests.cs
+++ current/NewGameTests.cs
@@ -27,7 +27,7 @@
         map.Fill(TileTypes.Floor);
 
         Entity player = new Entity("Player", '@', Color.White, new Point(4, 4), blocksMovement: true);
-        player.Fighter = new Fighter(30, 5, 2);
+        player.Fighter = new Fighter(30, 5, 2, experienceAwarded: 0);
         player.Inventory = new Inventory(26);
 
         return new GameWorld(map, new List<Entity> { player }, player);
```
<!-- generated-diff -->

```csharp
/*
 * Unit tests for abandoning a run.
 *
 * This exists because of a state the game could otherwise reach and never leave: kill every
 * monster and nothing can hurt you, there is nowhere to descend to, and Part 10's save writes
 * that dead end to disk after every turn. Dying is the only ending, and a cleared dungeon has
 * removed the only thing that could kill you.
 *
 * The confirmation is not politeness. A single key that destroys a run somebody is winning is a
 * worse bug than the one being fixed.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~NewGameTests
 */

using System;
using System.Collections.Generic;
using RogueTutorial;
using SadConsole.Input;
using SadRogue.Primitives;
using Xunit;

public sealed class NewGameTests
{
    private static GameWorld World()
    {
        GameMap map = new GameMap(9, 9);
        map.Fill(TileTypes.Floor);

        Entity player = new Entity("Player", '@', Color.White, new Point(4, 4), blocksMovement: true);
        player.Fighter = new Fighter(30, 5, 2, experienceAwarded: 0);
        player.Inventory = new Inventory(26);

        return new GameWorld(map, new List<Entity> { player }, player);
    }

    [Fact]
    public void NAsksRatherThanActs()
    {
        Assert.Equal(GameCommandKind.AskNewGame, CommandReader.Read(new[] { Keys.N }, GameMode.Playing).Kind);
    }

    [Fact]
    public void YConfirms()
    {
        Assert.Equal(
            GameCommandKind.ConfirmNewGame,
            CommandReader.Read(new[] { Keys.Y }, GameMode.ConfirmingNewGame).Kind);
    }

    [Fact]
    public void AnythingElseCancels()
    {
        // A player having second thoughts should not have to find the one correct way to say no.
        foreach (Keys key in new[] { Keys.Escape, Keys.N, Keys.Left, Keys.Space, Keys.A })
        {
            Assert.Equal(
                GameCommandKind.CancelNewGame,
                CommandReader.Read(new[] { key }, GameMode.ConfirmingNewGame).Kind);
        }
    }

    [Fact]
    public void NoKeyDoesNothing()
    {
        // Holding no keys is not an answer, and must not be read as one.
        Assert.Equal(
            GameCommandKind.None,
            CommandReader.Read(Array.Empty<Keys>(), GameMode.ConfirmingNewGame).Kind);
    }

    [Fact]
    public void TheQuestionCostsNoTurn()
    {
        // Asking is not acting: a monster must not get a swing because the player considered it.
        GameWorld world = World();

        world.SetMode(GameMode.ConfirmingNewGame);

        Assert.Equal(GameMode.ConfirmingNewGame, world.Mode);
        Assert.Equal(30, world.Player.Fighter!.HitPoints);
    }

    [Fact]
    public void BackingOutReturnsToTheMap()
    {
        GameWorld world = World();

        world.SetMode(GameMode.ConfirmingNewGame);
        world.SetMode(GameMode.Playing);

        Assert.Equal(GameMode.Playing, world.Mode);
    }

    [Fact]
    public void MovementKeysDoNotMoveWhileTheQuestionIsUp()
    {
        // Every key means yes or no here, so nothing else can be pressed by accident. A left
        // arrow that both cancelled and moved would be a surprise in the middle of a fight.
        GameCommand command = CommandReader.Read(new[] { Keys.Left }, GameMode.ConfirmingNewGame);

        Assert.Equal(GameCommandKind.CancelNewGame, command.Kind);
        Assert.Equal(Point.Zero, command.Offset);
    }

    [Fact]
    public void NMeansNothingWhileThePackIsOpen()
    {
        // The pack's letters are slots. 'n' there is the fourteenth item, not a new game.
        GameCommand command = CommandReader.Read(new[] { Keys.N }, GameMode.ShowingInventory);

        Assert.Equal(GameCommandKind.UseItem, command.Kind);
        Assert.Equal(13, command.Slot);
    }

    [Fact]
    public void NMeansNothingWhileAiming()
    {
        // Aiming is resolved with Enter or Escape; a stray letter must not abandon the run.
        Assert.Equal(GameCommandKind.None, CommandReader.Read(new[] { Keys.N }, GameMode.Targeting).Kind);
    }
}
```

## Step 4: build and run

```
dotnet build
dotnet test
dotnet run --project RogueTutorial
```

Expected: a clean build, 418 passing tests, and a level up menu after a few kills.

### If something is wrong

| Symptom | Cause |
|---|---|
| `CS7036: no argument for 'experienceAwarded'` | Steps 2 and 3 are not finished. The file named is correct; something later in the list has not been replaced yet |
| The log says the save could not be read | Expected: it is a Part 10 save. A new game has started and the old file is gone |
| Levelling happens without asking | `Award` is calling `Advance` rather than returning whether it could |
| The menu never appears | `OfferLevelUpIfEarned` is not called after the turn |
| Choosing does nothing | `RootScreen` is not routing `ChooseLevelUp` |
| Two levels earned but only one spent | The menu is not reopened after a choice |
| Levels reset when a save is loaded | The version is still 1, or `SavedLevel` is not being captured |
| A levelled character advances at a beginner's price | `Restore` is setting the level rather than replaying it |

## Step 5: regenerate the documentation

Skip this if you did not set up docfx in Part 1. Nothing was deleted this part:

```
dotnet docfx docfx.json --serve --port 8081
```

Expected: `Build succeeded. 0 warning(s) 0 error(s)`, and pages for `Level`, `LevelUpChoice` and
`LevelUpChoices` at <http://localhost:8081>.

---

Next: **Part 12, deeper levels.**

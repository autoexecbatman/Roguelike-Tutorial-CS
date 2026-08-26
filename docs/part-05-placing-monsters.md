# Part 5: Placing monsters

- [What it is](#what-it-is)
- [How to use it](#how-to-use-it)
- [How to set it up](#how-to-set-it-up)

---

# What it is

Monsters in the dungeon, and the thing that makes them real: **they occupy space**. Until now
you could walk through the villager. Now a rat stops you, and walking into it is how you attack
it - Part 6 makes that do damage.

Seed 12345, with the whole map revealed for the sake of the picture (11 monsters):

```
###......#######.#####........................#################.################
###......#######.#####.....#####.##########.....###########.....##r....#########
###.r....###...r...###.....#####.##....####...r.###########.....##....r#########
###.....................k.............r......................@.........#########
###......###.......#############................###########....###.....#########
###......###.......#############.##....####..k..##################.....#########
############.....................##....####.....################################
############.......########################.....################################
############.......#############################################################
############.......#############################################################
```

Rats (`r`) and kobolds (`k`), spread across the rooms. What the player actually sees at the
start is much less:

```

                                                              # #
                                                          #####.#
                                                          #.....##
                                                      #####.....##...
                                                     ........@........
                                                      #####....###...
                                                          ###### #
```

Nothing. The starting room is left empty on purpose - waking up already surrounded is not a
fair opening - and field of view hides the rest.

## The real change: state moves off the screen class

Part 1 drew a boundary for **rules**: nothing that decides anything lives on a SadConsole class,
because `Game.Instance` needs a graphics host and a test process has none. Four parts later the
same argument applies to **state**, and it had been quietly violated the whole time.

`RootScreen` held the map, the entity list and the visibility map. So none of it could be
tested. "Does walking into a monster stop you" was not a question the test suite could ask.

`GameWorld` takes all of it:

```csharp
public GameMap Map { get; }
public VisibilityMap Visibility { get; }
public Entity Player { get; }
public IReadOnlyList<Entity> Entities => _entities;

public PlayerAction MovePlayer(Point offset)
public Entity? BlockingEntityAt(Point position)
public RenderedFrame ComposeFrame()
```

`RootScreen` is now a keyboard, a surface, and a loop that copies glyphs. It decides nothing.

That is what makes the seventeen tests in `GameWorldTests` possible, and none of them opens a
window.

## Bump to attack

Walking into a monster is the attack command. There is no separate key.

That is the roguelike convention and it is worth understanding why: attacking is by far the most
common thing you do, movement keys are already under your fingers, and a dedicated attack key
would need a direction anyway. The cost is that you cannot swap places with a friendly creature
without a modifier, which matters only once friendly creatures exist.

The three outcomes are a type rather than a bool, because the caller has to tell them apart:

```csharp
public enum PlayerActionKind
{
    None,           // no movement key was pressed
    Moved,          // the player moved; the screen needs repainting
    BlockedByWall,  // a wall refused the move; nothing changed
    Bumped,         // the player walked into a creature
}
```

`PlayerAction.Target` is the creature that was bumped, and it is null for every other kind.
Part 6 turns that into "you hit the rat for 3 damage".

**The map is consulted before the entity list.** A monster standing inside a wall is not
something to bump into - if a later bug leaves one there, the wall must still refuse the move
rather than making the monster attackable through stone. There is a test for the ordering.

## Blocking is a property of the entity, not a class of entity

```csharp
Entity rat = new Entity("Rat", 'r', brown, cell, blocksMovement: true);
Entity corpse = new Entity("Corpse", '%', red, cell, blocksMovement: false);
```

A creature occupies its cell; an item lying on the floor is walked over. There is no default -
the argument is required at every call - because guessing wrong is silent. You find out when a
player walks through a monster, several parts later.

## Monsters are a table, not code

```csharp
public static MonsterTable Standard => new MonsterTable(
    new[]
    {
        new MonsterKind("Rat", 'r', brown, weight: 3),
        new MonsterKind("Kobold", 'k', green, weight: 1),
    },
    maximumPerRoom: 2);
```

Adding a monster is a line in a list. Weights are relative: a rat turns up three times as often
as a kobold, and the numbers mean nothing on their own. Part 12 makes the table vary with depth,
which is a change to data rather than to placement.

**Placement drops rather than retries.** A roll that repeats a cell, or lands on a pillar, is
discarded - so a room can hold fewer monsters than the maximum. Retrying until the count is met
can spin for a long time in a small room, and the same argument decided room placement in Part 3.

## What is deliberately wrong

**Monsters do not move.** They stand where they were placed. Part 6 gives them turns, and doing
both in one part would mean debugging placement and behaviour at the same time.

**Bumping does nothing.** No damage, no message, not even a sound. The action is reported and
thrown away.

**Every room gets the same monsters.** No difficulty by depth, and no boss room. Part 12.

**Monsters are drawn even when standing in a corridor you have lit from far away.** They are
hidden by field of view, which is the important half, but a monster in a lit cell twenty squares
off is as visible as one beside you.

---

# How to use it

## Play it

```
cd parts/part-05-placing-monsters
dotnet run --project RogueTutorial
```

Explore, and you will find rats and kobolds. Walk into one and you stop - that is a bump, and
right now nothing comes of it.

To play the same dungeon and monsters repeatedly, seed the Random in
[`RootScreen.cs`](../parts/part-05-placing-monsters/RogueTutorial/RootScreen.cs):

```csharp
new Random()        // a different world every run
new Random(12345)   // the same one every run
```

## Run the tests

```
dotnet test                                  # 185 tests
dotnet test --filter "Category!=EndToEnd"    # no window
```

| Test class | Level | Covers |
|---|---|---|
| [`GameWorldTests`](../parts/part-05-placing-monsters/RogueTutorial.Tests/GameWorldTests.cs) | unit | blocking, bumping, wall-before-monster ordering, reproducibility |
| [`MonsterTableTests`](../parts/part-05-placing-monsters/RogueTutorial.Tests/MonsterTableTests.cs) | unit | placement bounds, no stacking, no rock, weighting |

## Prove the tests can fail

| Change | Expect |
|---|---|
| `BlockingEntityAt`: drop the `BlocksMovement` check | 2 fail |
| `MovePlayer`: remove the blocker check entirely | 1 fails |
| `Generate`: start the room loop at 0 instead of 1 | 2 fail |
| `PopulateRoom`: allow a repeated cell | 2 fail |
| `PopulateRoom`: drop the walkability check | 1 fails |
| `PopulateRoom`: widen the roll to `room.Left` .. `room.Right` | 1 fails |

**That last row started as a survivor.** Widening the placement roll to include the room's wall
ring changed nothing, because the walkability check rejected wall cells anyway - so the test
suite could not tell the two versions apart.

It is not an equivalent mutant, though. Carve a corridor through a room's wall and that cell
becomes walkable, and the widened roll will put a monster in the doorway. The map being solid
there was doing the work the roll was supposed to do.

`ADoorwayIsStillNotInsideTheRoom` builds a room with a doorway in each wall and pins the
contract: the *roll* excludes the ring, not the map. That is the difference between a test that
happens to pass and one that says what it means.

---

# How to set it up

> **You are in:** your project folder, the one holding `RogueTutorial/` and `RogueTutorial.Tests/`

## Step 1: the source files

**Each block below is the complete file.** `Entity.cs` and `RootScreen.cs` already exist -
replace them entirely rather than merging by hand.

Adding the required `blocksMovement` argument to `Entity` breaks every existing construction of
one. That is the point of making it required, and the compiler will list them: every creature
gets `true`.

### [`RogueTutorial/Entity.cs`](../parts/part-05-placing-monsters/RogueTutorial/Entity.cs)

The Part 2 file with `BlocksMovement` added.

<!-- generated-diff -->
**Changed from Part 4.** The complete file follows; this is only what moved:

```diff
--- part-04-field-of-view/Entity.cs
+++ current/Entity.cs
@@ -4,10 +4,14 @@
  *
  * Usage:
  *
- *     Entity player = new Entity("Player", '@', Color.White, new Point(40, 12));
- *     Entity npc = new Entity("Villager", '@', Color.Yellow, new Point(42, 12));
+ *     Entity player = new Entity("Player", '@', Color.White, new Point(40, 12), blocksMovement: true);
+ *     Entity corpse = new Entity("Corpse", '%', Color.Red, new Point(41, 12), blocksMovement: false);
  *     player.MoveTo(new Point(41, 12));   // unconditional; see MovementRules for the rules
  *     string who = player.Name;           // -> "Player", for messages in a later part
+ *
+ * blocksMovement is explicit at every call: a creature occupies its cell and nothing else may
+ * stand there, while an item on the floor is walked over. There is no default, because guessing
+ * wrong is silent - you notice when a player walks through a monster.
  *
  * Refuses a null, empty or whitespace name. It applies no movement rules of its own: whether a
  * destination is legal is the map's business, and MovementRules is where the two meet.
@@ -33,10 +37,16 @@
     public Point Position { get; private set; }
 
     /// <summary>
+    /// True when nothing else may stand on this entity's cell. Creatures block; items lying on
+    /// the floor do not.
+    /// </summary>
+    public bool BlocksMovement { get; }
+
+    /// <summary>
     /// Creates an entity at a starting cell. Throws ArgumentException on a blank name, since an
     /// unnamed entity would surface much later as an empty word in a message.
     /// </summary>
-    public Entity(string name, char glyph, Color foreground, Point startingPosition)
+    public Entity(string name, char glyph, Color foreground, Point startingPosition, bool blocksMovement)
     {
         // A blank name is a construction mistake; fail here rather than in the message log.
         if (string.IsNullOrWhiteSpace(name))
@@ -48,6 +58,7 @@
         Glyph = glyph;
         Foreground = foreground;
         Position = startingPosition;
+        BlocksMovement = blocksMovement;
     }
 
     /// <summary>
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
 *     player.MoveTo(new Point(41, 12));   // unconditional; see MovementRules for the rules
 *     string who = player.Name;           // -> "Player", for messages in a later part
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
    public string Name { get; }

    /// <summary>The character drawn for it.</summary>
    public char Glyph { get; }

    /// <summary>The colour that character is drawn in.</summary>
    public Color Foreground { get; }

    /// <summary>The cell it currently occupies.</summary>
    public Point Position { get; private set; }

    /// <summary>
    /// True when nothing else may stand on this entity's cell. Creatures block; items lying on
    /// the floor do not.
    /// </summary>
    public bool BlocksMovement { get; }

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
}
```

### [`RogueTutorial/PlayerAction.cs`](../parts/part-05-placing-monsters/RogueTutorial/PlayerAction.cs)

What happened when a movement key was pressed: moved, blocked, or bumped.

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
 *     if (action.Kind == PlayerActionKind.Bumped)
 *     {
 *         string message = $"You attack the {action.Target!.Name}.";   // Target is set only here
 *     }
 *
 * Target is null for every kind except Bumped, which is the one case where something was hit.
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

    /// <summary>The player walked into a creature. Part 6 makes this an attack.</summary>
    Bumped,
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
    /// The player walked into a creature. Throws ArgumentNullException on a null target, since a
    /// bump with nothing to bump into is a bug in whoever built it.
    /// </summary>
    public static PlayerAction BumpedInto(Entity target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return new PlayerAction(PlayerActionKind.Bumped, target);
    }
}
```

### [`RogueTutorial/MonsterTable.cs`](../parts/part-05-placing-monsters/RogueTutorial/MonsterTable.cs)

What lives in the dungeon and how it is placed, as data rather than code.

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
 *         new[] { new MonsterKind("Rat", 'r', Color.Brown, weight: 1) },
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
    public MonsterKind(string name, char glyph, Color foreground, int weight)
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
            new MonsterKind("Rat", 'r', new Color(150, 120, 90), weight: 3),
            new MonsterKind("Kobold", 'k', new Color(120, 180, 90), weight: 1),
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

            placed.Add(new Entity(kind.Name, kind.Glyph, kind.Foreground, cell, blocksMovement: true));
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

### [`RogueTutorial/FrameComposer.cs`](../parts/part-05-placing-monsters/RogueTutorial/FrameComposer.cs)

Unchanged apart from its usage block, which now shows the required `blocksMovement` argument.

<!-- generated-diff -->
**Changed from Part 4.** The complete file follows; this is only what moved:

```diff
--- part-04-field-of-view/FrameComposer.cs
+++ current/FrameComposer.cs
@@ -4,7 +4,7 @@
  * Usage:
  *
  *     GameMap map = new GameMap(3, 2);
- *     Entity player = new Entity("Player", '@', Color.White, new Point(1, 1));
+ *     Entity player = new Entity("Player", '@', Color.White, new Point(1, 1), blocksMovement: true);
  *     RenderedFrame frame = FrameComposer.Compose(map, new[] { player });
  *     string picture = frame.ToText();
  *     // -> "...\n.@."
```
<!-- generated-diff -->

```csharp
/*
 * Builds the picture that should be on screen: the map first, then entities over the top.
 *
 * Usage:
 *
 *     GameMap map = new GameMap(3, 2);
 *     Entity player = new Entity("Player", '@', Color.White, new Point(1, 1), blocksMovement: true);
 *     RenderedFrame frame = FrameComposer.Compose(map, new[] { player });
 *     string picture = frame.ToText();
 *     // -> "...\n.@."
 *
 * The overload taking a VisibilityMap is what the game uses from Part 4 on: it dims what the
 * player remembers, blanks what they have never seen, and hides entities standing in the dark.
 *
 * Refuses a null map or null entity list. An entity standing off the map is skipped rather than
 * throwing, because a later part moves entities between levels.
 */

using System;
using System.Collections.Generic;
using SadRogue.Primitives;

namespace RogueTutorial;

internal static class FrameComposer
{
    /// <summary>
    /// Draws every map tile, then every entity over the top in list order, so a later entity
    /// covers an earlier one sharing its cell. Throws ArgumentNullException on a null argument.
    /// </summary>
    /// <summary>
    /// Draws the map and entities as the player currently perceives them: cells in sight at full
    /// colour, cells only remembered dimmed, cells never seen left blank, and entities drawn only
    /// where the player can actually see them. Throws ArgumentNullException on a null argument.
    /// </summary>
    public static RenderedFrame Compose(GameMap map, IReadOnlyList<Entity> entities, VisibilityMap visibility)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(visibility);

        char[] glyphs = new char[map.Width * map.Height];
        Color[] foregrounds = new Color[map.Width * map.Height];

        for (int row = 0; row < map.Height; row++)
        {
            for (int col = 0; col < map.Width; col++)
            {
                Point cell = new Point(col, row);
                int index = (row * map.Width) + col;

                CellVisibility state = visibility.StateAt(cell);

                // Never seen: nothing is drawn, so unexplored dungeon reads as empty space.
                if (state == CellVisibility.Unseen)
                {
                    glyphs[index] = ' ';
                    foregrounds[index] = Color.Black;
                    continue;
                }

                Tile tile = map.GetTile(cell);

                glyphs[index] = tile.Glyph;

                // Remembered cells are drawn from memory, so they are dimmed rather than lit.
                foregrounds[index] = state == CellVisibility.Visible
                    ? tile.Foreground
                    : DimmedForMemory(tile.Foreground);
            }
        }

        foreach (Entity entity in entities)
        {
            if (!map.IsInBounds(entity.Position))
            {
                continue;
            }

            // Creatures are not remembered: an entity is drawn only where it can be seen now,
            // otherwise the player would watch a monster that had long since walked away.
            if (visibility.StateAt(entity.Position) != CellVisibility.Visible)
            {
                continue;
            }

            int index = (entity.Position.Y * map.Width) + entity.Position.X;
            glyphs[index] = entity.Glyph;
            foregrounds[index] = entity.Foreground;
        }

        return new RenderedFrame(map.Width, map.Height, glyphs, foregrounds);
    }

    // A third of full brightness: dark enough to read as memory, light enough to make out.
    private static Color DimmedForMemory(Color lit)
    {
        return new Color(lit.R / 3, lit.G / 3, lit.B / 3);
    }

    public static RenderedFrame Compose(GameMap map, IReadOnlyList<Entity> entities)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(entities);

        char[] glyphs = new char[map.Width * map.Height];
        Color[] foregrounds = new Color[map.Width * map.Height];

        // The map is the background layer, so it goes down first and entities paint over it.
        for (int row = 0; row < map.Height; row++)
        {
            for (int col = 0; col < map.Width; col++)
            {
                Tile tile = map.GetTile(new Point(col, row));

                int index = (row * map.Width) + col;
                glyphs[index] = tile.Glyph;
                foregrounds[index] = tile.Foreground;
            }
        }

        // List order decides who covers whom, so this loop must not be reordered.
        foreach (Entity entity in entities)
        {
            // An entity between levels is legitimately off this map, so skip rather than throw.
            if (!map.IsInBounds(entity.Position))
            {
                continue;
            }

            int index = (entity.Position.Y * map.Width) + entity.Position.X;
            glyphs[index] = entity.Glyph;
            foregrounds[index] = entity.Foreground;
        }

        return new RenderedFrame(map.Width, map.Height, glyphs, foregrounds);
    }
}
```

### [`RogueTutorial/GameWorld.cs`](../parts/part-05-placing-monsters/RogueTutorial/GameWorld.cs)

The new owner of the game state. This is the file the part exists for.

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
 *     world.MovePlayer(new Point(1, 0));                  // one step right, or a bump
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

    /// <summary>
    /// Builds a world directly from its parts. Generate is the usual way in; this constructor
    /// exists so a test can hand-build a small world with exactly the monsters it cares about.
    /// Throws ArgumentNullException on a null argument, and ArgumentException when the player is
    /// not one of the entities, since the player must be drawn and moved like any other.
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
            return PlayerAction.BumpedInto(blocker);
        }

        Player.MoveTo(destination);

        // Sight is recomputed from the new position before anything is drawn, or the player
        // would see one frame of the view from where they used to stand.
        RecomputeFieldOfView();

        return PlayerAction.Moved;
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

### [`RogueTutorial/RootScreen.cs`](../parts/part-05-placing-monsters/RogueTutorial/RootScreen.cs)

Now wiring and drawing only - it owns no state at all.

<!-- generated-diff -->
**Changed from Part 4.** The complete file follows; this is only what moved:

```diff
--- part-04-field-of-view/RootScreen.cs
+++ current/RootScreen.cs
@@ -1,8 +1,8 @@
 /*
- * The top-level screen: it wires SadConsole's window and keyboard to the game, and blits the
- * composed frame. It owns no rules. From Part 4 it also recomputes the player's field of view
- * after every move, so the map is drawn as the player perceives it rather than as it is. The map, the entities, where a move ends up and what the
- * picture should look like are all decided by classes that run without a graphics host.
+ * The top-level screen: it wires SadConsole's window and keyboard to the game world, and blits
+ * the frame the world composes. It owns no rules and, from Part 5, no state either - the map,
+ * the entities and what the player has seen all live on GameWorld, which can be built and
+ * driven in a test process.
  *
  * Usage - SadConsole constructs this itself, because Program.cs named it as the starting
  * screen, so it needs a public parameterless constructor:
@@ -10,7 +10,7 @@
  *     new Builder().SetStartingScreen<RootScreen>()
  *
  * Constructing it in a test process throws: the constructor reads Game.Instance for the grid
- * size, and that requires a live graphics host. Test the rule classes instead.
+ * size, and that requires a live graphics host. Test GameWorld instead.
  */
 
 using System;
@@ -24,28 +24,14 @@
 
 internal sealed class RootScreen : ScreenObject
 {
-    // How far the player can see, in cells. Large enough to take in a room, small enough that
-    // a corridor stays dark ahead of you.
-    private const int PlayerSightRadius = 8;
-
     // The surface every glyph is drawn onto. One cell per grid position.
     private readonly ScreenSurface _mapSurface;
 
-    // The dungeon floor. Fixed for this part; generated for real in Part 3.
-    private readonly GameMap _map;
-
-    // Everything drawn on top of the map, in draw order: later entries cover earlier ones.
-    private readonly List<Entity> _entities;
-
-    // What the player can see now and what they remember, updated after every move.
-    private readonly VisibilityMap _visibility;
-
-    // The entity the keyboard drives. Also present in _entities, so it is drawn like any other.
-    private readonly Entity _player;
+    // The dungeon, everyone standing in it, and what the player has seen.
+    private readonly GameWorld _world;
 
     /// <summary>
-    /// Builds the room, places the player and one villager in it, and paints the first frame.
-    /// The surface is sized to the window configured in Program.cs.
+    /// Sizes the surface to the window, generates a world to fill it, and paints the first frame.
     /// </summary>
     public RootScreen()
     {
@@ -55,41 +41,18 @@
         // Children are drawn and updated by the base class once added.
         Children.Add(_mapSurface);
 
-        // No seed is given, so every run generates a different dungeon. Pass a number to
-        // Random's constructor to play the same one repeatedly while debugging.
-        DungeonGenerator generator = new DungeonGenerator(new DungeonSettings(
-            maximumRooms: 30,
-            minimumRoomSize: 6,
-            maximumRoomSize: 10));
-
-        GeneratedDungeon dungeon = generator.Generate(
-            _mapSurface.Surface.Width, _mapSurface.Surface.Height, new Random());
-
-        _map = dungeon.Map;
-
-        // The generator decides where the player starts: the centre of the first room it placed.
-        _player = new Entity("Player", '@', Color.White, dungeon.PlayerStart);
-
-        // A villager in the last room, so there is a reason to walk the corridors.
-        Entity villager = new Entity(
-            "Villager", '@', Color.Yellow, dungeon.Rooms[dungeon.Rooms.Count - 1].Center);
-
-        // The player is last, so it covers anything standing on the same cell.
-        _entities = new List<Entity> { villager, _player };
-
-        _visibility = new VisibilityMap(_map.Width, _map.Height);
-
-        // Without this the first frame would be drawn before anything had been seen, so the
-        // player would spend one frame staring at an entirely blank screen.
-        RecomputeFieldOfView();
+        // No seed is given, so every run is a different dungeon with different monsters. Pass a
+        // number to Random's constructor to play the same one repeatedly while debugging.
+        _world = GameWorld.Generate(
+            _mapSurface.Surface.Width, _mapSurface.Surface.Height, new Random(), MonsterTable.Standard);
 
         DrawFrame();
     }
 
     /// <summary>
-    /// Turns the keys held this frame into one move for the player. Returns true when a
-    /// movement key was pressed, even if a wall refused the move, so the key is not offered
-    /// to another screen as though nothing had happened.
+    /// Turns the keys held this frame into one move. Returns true whenever a movement key was
+    /// pressed, even when a wall or a monster refused the move: the key was considered and
+    /// answered, and reporting otherwise would offer it to another screen as unhandled.
     /// </summary>
     public override bool ProcessKeyboard(Keyboard keyboard)
     {
@@ -104,17 +67,12 @@
             return false;
         }
 
-        Point destination = MovementRules.DestinationFor(_player.Position, moveOffset, _map);
+        PlayerAction action = _world.MovePlayer(moveOffset);
 
-        // A wall refuses the move, and repainting an unchanged frame is wasted work.
-        if (destination != _player.Position)
+        // Only a move changes the picture. A bump will change it in Part 6, once attacking does
+        // something; a wall never does.
+        if (action.Kind == PlayerActionKind.Moved)
         {
-            _player.MoveTo(destination);
-
-            // Sight is recomputed from the new position before the frame is drawn, or the
-            // player would see one frame of the view from where they used to stand.
-            RecomputeFieldOfView();
-
             DrawFrame();
         }
 
@@ -122,21 +80,12 @@
     }
 
     /// <summary>
-    /// Composes the picture and copies it onto the surface, one cell at a time. Everything
-    /// decided here was already decided by FrameComposer; this only moves it to the screen.
+    /// Copies the world's composed frame onto the surface, one cell at a time. Everything drawn
+    /// here was already decided by FrameComposer; this only moves it to the screen.
     /// </summary>
-    /// <summary>
-    /// Works out what the player can see from where they now stand and folds it into what they
-    /// remember. Called once at construction and after every move that changed the position.
-    /// </summary>
-    private void RecomputeFieldOfView()
-    {
-        _visibility.Update(FieldOfView.From(_player.Position, PlayerSightRadius, _map));
-    }
-
     private void DrawFrame()
     {
-        RenderedFrame frame = FrameComposer.Compose(_map, _entities, _visibility);
+        RenderedFrame frame = _world.ComposeFrame();
 
         for (int row = 0; row < frame.Height; row++)
         {
```
<!-- generated-diff -->

```csharp
/*
 * The top-level screen: it wires SadConsole's window and keyboard to the game world, and blits
 * the frame the world composes. It owns no rules and, from Part 5, no state either - the map,
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
    // The surface every glyph is drawn onto. One cell per grid position.
    private readonly ScreenSurface _mapSurface;

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

        // No seed is given, so every run is a different dungeon with different monsters. Pass a
        // number to Random's constructor to play the same one repeatedly while debugging.
        _world = GameWorld.Generate(
            _mapSurface.Surface.Width, _mapSurface.Surface.Height, new Random(), MonsterTable.Standard);

        DrawFrame();
    }

    /// <summary>
    /// Turns the keys held this frame into one move. Returns true whenever a movement key was
    /// pressed, even when a wall or a monster refused the move: the key was considered and
    /// answered, and reporting otherwise would offer it to another screen as unhandled.
    /// </summary>
    public override bool ProcessKeyboard(Keyboard keyboard)
    {
        // Reduce SadConsole's key objects to the bare enum the movement table expects.
        IReadOnlyCollection<Keys> pressedKeys = keyboard.KeysPressed.Select(pressed => pressed.Key).ToArray();

        Point moveOffset = MovementKeys.OffsetFor(pressedKeys);

        // No movement key was down, so leave the frame alone and let others see the input.
        if (moveOffset == Point.Zero)
        {
            return false;
        }

        PlayerAction action = _world.MovePlayer(moveOffset);

        // Only a move changes the picture. A bump will change it in Part 6, once attacking does
        // something; a wall never does.
        if (action.Kind == PlayerActionKind.Moved)
        {
            DrawFrame();
        }

        return true;
    }

    /// <summary>
    /// Copies the world's composed frame onto the surface, one cell at a time. Everything drawn
    /// here was already decided by FrameComposer; this only moves it to the screen.
    /// </summary>
    private void DrawFrame()
    {
        RenderedFrame frame = _world.ComposeFrame();

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

## Step 1b: retitle the window

One line in `RogueTutorial/Program.cs`, so the window says which part you are running:

```csharp
const string WindowTitle = "Roguelike Tutorial - Part 5: Placing monsters";
```

Nothing else in that file changes.

## Step 2: the test files

**Each block below is the complete file.** Create it in `RogueTutorial.Tests/`.

Three test files carried over from earlier parts also construct entities, so they will not
compile until they carry the new argument. Every entity in them is a creature, so every one gets
`blocksMovement: true`. They are given in full below along with the two new files - replace them
rather than hunting the twelve call sites by hand.

### [`RogueTutorial.Tests/GameWorldTests.cs`](../parts/part-05-placing-monsters/RogueTutorial.Tests/GameWorldTests.cs)

Blocking, bumping, and that a generated world is reproducible from its seed.

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

        Assert.Throws<ArgumentException>(() => new GameWorld(map, Array.Empty<Entity>(), player));
    }

    [Fact]
    public void ANullArgumentIsRejected()
    {
        GameMap map = new GameMap(5, 5);
        Entity player = new Entity("Player", '@', Color.White, new Point(2, 2), blocksMovement: true);

        Assert.Throws<ArgumentNullException>(() => new GameWorld(null!, new[] { player }, player));
        Assert.Throws<ArgumentNullException>(() => new GameWorld(map, null!, player));
        Assert.Throws<ArgumentNullException>(() => new GameWorld(map, new[] { player }, null!));
        Assert.Throws<ArgumentNullException>(() => GameWorld.Generate(40, 25, null!, MonsterTable.Standard));
        Assert.Throws<ArgumentNullException>(() => GameWorld.Generate(40, 25, new Random(1), null!));
    }
}
```

### [`RogueTutorial.Tests/MonsterTableTests.cs`](../parts/part-05-placing-monsters/RogueTutorial.Tests/MonsterTableTests.cs)

Placement bounds, no stacking, no rock, and that weights actually weight.

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
            new[] { new MonsterKind("Rat", 'r', Color.Red, weight: 1) },
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
                new MonsterKind("Common", 'c', Color.Red, weight: 3),
                new MonsterKind("Rare", 'x', Color.Blue, weight: 1),
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
        Assert.Throws<ArgumentException>(() => new MonsterKind("  ", 'r', Color.Red, weight: 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AWeightThatCanNeverBeChosenIsRejected(int weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MonsterKind("Rat", 'r', Color.Red, weight));
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
            () => new MonsterTable(new[] { new MonsterKind("Rat", 'r', Color.Red, 1) }, maximumPerRoom: -1));
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

### [`RogueTutorial.Tests/FrameComposerTests.cs`](../parts/part-05-placing-monsters/RogueTutorial.Tests/FrameComposerTests.cs)

Part 2's file, with `blocksMovement: true` on every construction.

<!-- generated-diff -->
**Changed from Part 4.** The complete file follows; this is only what moved:

```diff
--- part-04-field-of-view/FrameComposerTests.cs
+++ current/FrameComposerTests.cs
@@ -55,7 +55,7 @@
     public void AnEntityDrawsOverTheMap()
     {
         GameMap map = new GameMap(3, 2);
-        Entity player = new Entity("Player", '@', Color.White, new Point(1, 1));
+        Entity player = new Entity("Player", '@', Color.White, new Point(1, 1), blocksMovement: true);
 
         RenderedFrame frame = FrameComposer.Compose(map, new[] { player });
 
@@ -70,8 +70,8 @@
     public void SeveralEntitiesAllDraw()
     {
         GameMap map = new GameMap(4, 2);
-        Entity player = new Entity("Player", '@', Color.White, new Point(0, 0));
-        Entity villager = new Entity("Villager", 'V', Color.Yellow, new Point(3, 1));
+        Entity player = new Entity("Player", '@', Color.White, new Point(0, 0), blocksMovement: true);
+        Entity villager = new Entity("Villager", 'V', Color.Yellow, new Point(3, 1), blocksMovement: true);
 
         RenderedFrame frame = FrameComposer.Compose(map, new[] { player, villager });
 
@@ -86,8 +86,8 @@
     public void ALaterEntityCoversAnEarlierOneOnTheSameCell()
     {
         GameMap map = new GameMap(2, 1);
-        Entity underneath = new Entity("Corpse", '%', Color.Red, new Point(0, 0));
-        Entity onTop = new Entity("Player", '@', Color.White, new Point(0, 0));
+        Entity underneath = new Entity("Corpse", '%', Color.Red, new Point(0, 0), blocksMovement: false);
+        Entity onTop = new Entity("Player", '@', Color.White, new Point(0, 0), blocksMovement: true);
 
         RenderedFrame frame = FrameComposer.Compose(map, new[] { underneath, onTop });
 
@@ -98,7 +98,7 @@
     public void AnEntityOffTheMapIsSkippedRatherThanThrowing()
     {
         GameMap map = new GameMap(2, 1);
-        Entity stray = new Entity("Stray", 'S', Color.Green, new Point(9, 9));
+        Entity stray = new Entity("Stray", 'S', Color.Green, new Point(9, 9), blocksMovement: true);
 
         RenderedFrame frame = FrameComposer.Compose(map, new[] { stray });
 
@@ -109,7 +109,7 @@
     public void TheEntityColourReachesTheFrame()
     {
         GameMap map = new GameMap(2, 1);
-        Entity villager = new Entity("Villager", 'V', Color.Yellow, new Point(1, 0));
+        Entity villager = new Entity("Villager", 'V', Color.Yellow, new Point(1, 0), blocksMovement: true);
 
         RenderedFrame frame = FrameComposer.Compose(map, new[] { villager });
 
```
<!-- generated-diff -->

```csharp
/*
 * Unit tests for what should appear on screen. These are the tests Part 1 could not have:
 * because the frame is data rather than pixels, an expected picture is written here as ASCII
 * and compared as a string.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~FrameComposerTests
 */

using System;
using System.Collections.Generic;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class FrameComposerTests
{
    // Joins expected rows with the same separator ToText uses, so a test reads as a picture.
    private static string Picture(params string[] rows)
    {
        return string.Join("\n", rows);
    }

    [Fact]
    public void AnEmptyMapDrawsAsAllFloor()
    {
        GameMap map = new GameMap(3, 2);

        RenderedFrame frame = FrameComposer.Compose(map, Array.Empty<Entity>());

        Assert.Equal(
            Picture(
                "...",
                "..."),
            frame.ToText());
    }

    [Fact]
    public void WallsDrawWhereTheyWereSet()
    {
        GameMap map = new GameMap(3, 3);
        map.SetTile(new Point(0, 0), TileTypes.Wall);
        map.SetTile(new Point(2, 2), TileTypes.Wall);

        RenderedFrame frame = FrameComposer.Compose(map, Array.Empty<Entity>());

        Assert.Equal(
            Picture(
                "#..",
                "...",
                "..#"),
            frame.ToText());
    }

    [Fact]
    public void AnEntityDrawsOverTheMap()
    {
        GameMap map = new GameMap(3, 2);
        Entity player = new Entity("Player", '@', Color.White, new Point(1, 1), blocksMovement: true);

        RenderedFrame frame = FrameComposer.Compose(map, new[] { player });

        Assert.Equal(
            Picture(
                "...",
                ".@."),
            frame.ToText());
    }

    [Fact]
    public void SeveralEntitiesAllDraw()
    {
        GameMap map = new GameMap(4, 2);
        Entity player = new Entity("Player", '@', Color.White, new Point(0, 0), blocksMovement: true);
        Entity villager = new Entity("Villager", 'V', Color.Yellow, new Point(3, 1), blocksMovement: true);

        RenderedFrame frame = FrameComposer.Compose(map, new[] { player, villager });

        Assert.Equal(
            Picture(
                "@...",
                "...V"),
            frame.ToText());
    }

    [Fact]
    public void ALaterEntityCoversAnEarlierOneOnTheSameCell()
    {
        GameMap map = new GameMap(2, 1);
        Entity underneath = new Entity("Corpse", '%', Color.Red, new Point(0, 0), blocksMovement: false);
        Entity onTop = new Entity("Player", '@', Color.White, new Point(0, 0), blocksMovement: true);

        RenderedFrame frame = FrameComposer.Compose(map, new[] { underneath, onTop });

        Assert.Equal("@.", frame.ToText());
    }

    [Fact]
    public void AnEntityOffTheMapIsSkippedRatherThanThrowing()
    {
        GameMap map = new GameMap(2, 1);
        Entity stray = new Entity("Stray", 'S', Color.Green, new Point(9, 9), blocksMovement: true);

        RenderedFrame frame = FrameComposer.Compose(map, new[] { stray });

        Assert.Equal("..", frame.ToText());
    }

    [Fact]
    public void TheEntityColourReachesTheFrame()
    {
        GameMap map = new GameMap(2, 1);
        Entity villager = new Entity("Villager", 'V', Color.Yellow, new Point(1, 0), blocksMovement: true);

        RenderedFrame frame = FrameComposer.Compose(map, new[] { villager });

        // ToText only carries glyphs, so colour needs its own check.
        Assert.Equal(Color.Yellow, frame.ForegroundAt(new Point(1, 0)));
    }

    [Fact]
    public void TheFrameMatchesTheMapSize()
    {
        GameMap map = new GameMap(7, 3);

        RenderedFrame frame = FrameComposer.Compose(map, Array.Empty<Entity>());

        Assert.Equal(7, frame.Width);
        Assert.Equal(3, frame.Height);
    }

    [Fact]
    public void ANullMapIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => FrameComposer.Compose(null!, Array.Empty<Entity>()));
    }

    [Fact]
    public void ANullEntityListIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => FrameComposer.Compose(new GameMap(2, 2), null!));
    }
}
```

### [`RogueTutorial.Tests/FrameComposerVisibilityTests.cs`](../parts/part-05-placing-monsters/RogueTutorial.Tests/FrameComposerVisibilityTests.cs)

Part 4's file, likewise.

<!-- generated-diff -->
**Changed from Part 4.** The complete file follows; this is only what moved:

```diff
--- part-04-field-of-view/FrameComposerVisibilityTests.cs
+++ current/FrameComposerVisibilityTests.cs
@@ -100,7 +100,7 @@
     {
         GameMap map = OpenMap(3, 1);
         VisibilityMap visibility = new VisibilityMap(3, 1);
-        Entity player = new Entity("Player", '@', Color.White, new Point(1, 0));
+        Entity player = new Entity("Player", '@', Color.White, new Point(1, 0), blocksMovement: true);
 
         visibility.Update(Cells(new Point(0, 0), new Point(1, 0), new Point(2, 0)));
 
@@ -116,7 +116,7 @@
         // where you last saw it, or the player chases a ghost.
         GameMap map = OpenMap(4, 1);
         VisibilityMap visibility = new VisibilityMap(4, 1);
-        Entity monster = new Entity("Rat", 'r', Color.Red, new Point(0, 0));
+        Entity monster = new Entity("Rat", 'r', Color.Red, new Point(0, 0), blocksMovement: true);
 
         visibility.Update(Cells(new Point(0, 0)));
         visibility.Update(Cells(new Point(3, 0)));
@@ -132,7 +132,7 @@
     {
         GameMap map = OpenMap(3, 1);
         VisibilityMap visibility = new VisibilityMap(3, 1);
-        Entity monster = new Entity("Rat", 'r', Color.Red, new Point(2, 0));
+        Entity monster = new Entity("Rat", 'r', Color.Red, new Point(2, 0), blocksMovement: true);
 
         visibility.Update(Cells(new Point(0, 0)));
 
```
<!-- generated-diff -->

```csharp
/*
 * Unit tests for drawing what the player perceives rather than what is there. Expected pictures
 * are written as ASCII, so a failure prints as a shape.
 *
 * A space means never seen. The glyph itself means seen - lit or remembered - and colour is
 * what separates those two, so the tests that care about dimming check the colour directly.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~FrameComposerVisibilityTests
 */

using System;
using System.Collections.Generic;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class FrameComposerVisibilityTests
{
    private static GameMap OpenMap(int width, int height)
    {
        return new GameMap(width, height);
    }

    private static ISet<Point> Cells(params Point[] cells)
    {
        return new HashSet<Point>(cells);
    }

    private static string Picture(params string[] rows)
    {
        return string.Join("\n", rows);
    }

    [Fact]
    public void NothingIsDrawnBeforeAnythingIsSeen()
    {
        GameMap map = OpenMap(3, 2);
        VisibilityMap visibility = new VisibilityMap(3, 2);

        RenderedFrame frame = FrameComposer.Compose(map, Array.Empty<Entity>(), visibility);

        Assert.Equal(
            Picture(
                "   ",
                "   "),
            frame.ToText());
    }

    [Fact]
    public void OnlyWhatHasBeenSeenIsDrawn()
    {
        GameMap map = OpenMap(4, 2);
        VisibilityMap visibility = new VisibilityMap(4, 2);

        visibility.Update(Cells(new Point(0, 0), new Point(1, 0)));

        RenderedFrame frame = FrameComposer.Compose(map, Array.Empty<Entity>(), visibility);

        Assert.Equal(
            Picture(
                "..  ",
                "    "),
            frame.ToText());
    }

    [Fact]
    public void ARememberedCellIsStillDrawn()
    {
        GameMap map = OpenMap(4, 1);
        VisibilityMap visibility = new VisibilityMap(4, 1);

        visibility.Update(Cells(new Point(0, 0)));
        visibility.Update(Cells(new Point(3, 0)));

        RenderedFrame frame = FrameComposer.Compose(map, Array.Empty<Entity>(), visibility);

        // Both ends drawn: one lit, one from memory. The glyph does not distinguish them.
        Assert.Equal(".  .", frame.ToText());
    }

    [Fact]
    public void ARememberedCellIsDimmerThanALitOne()
    {
        GameMap map = OpenMap(4, 1);
        VisibilityMap visibility = new VisibilityMap(4, 1);

        visibility.Update(Cells(new Point(0, 0)));
        visibility.Update(Cells(new Point(3, 0)));

        RenderedFrame frame = FrameComposer.Compose(map, Array.Empty<Entity>(), visibility);

        Color remembered = frame.ForegroundAt(new Point(0, 0));
        Color lit = frame.ForegroundAt(new Point(3, 0));

        Assert.True(remembered.R < lit.R, $"remembered {remembered.R} should be dimmer than lit {lit.R}");
    }

    [Fact]
    public void AnEntityInSightIsDrawn()
    {
        GameMap map = OpenMap(3, 1);
        VisibilityMap visibility = new VisibilityMap(3, 1);
        Entity player = new Entity("Player", '@', Color.White, new Point(1, 0), blocksMovement: true);

        visibility.Update(Cells(new Point(0, 0), new Point(1, 0), new Point(2, 0)));

        RenderedFrame frame = FrameComposer.Compose(map, new[] { player }, visibility);

        Assert.Equal(".@.", frame.ToText());
    }

    [Fact]
    public void AnEntityInTheDarkIsHidden()
    {
        // Creatures are not remembered. A monster you walked away from must not stay painted
        // where you last saw it, or the player chases a ghost.
        GameMap map = OpenMap(4, 1);
        VisibilityMap visibility = new VisibilityMap(4, 1);
        Entity monster = new Entity("Rat", 'r', Color.Red, new Point(0, 0), blocksMovement: true);

        visibility.Update(Cells(new Point(0, 0)));
        visibility.Update(Cells(new Point(3, 0)));

        RenderedFrame frame = FrameComposer.Compose(map, new[] { monster }, visibility);

        // The floor it stood on is remembered and drawn; the monster itself is not.
        Assert.Equal(".  .", frame.ToText());
    }

    [Fact]
    public void AnEntityOnANeverSeenCellIsHidden()
    {
        GameMap map = OpenMap(3, 1);
        VisibilityMap visibility = new VisibilityMap(3, 1);
        Entity monster = new Entity("Rat", 'r', Color.Red, new Point(2, 0), blocksMovement: true);

        visibility.Update(Cells(new Point(0, 0)));

        RenderedFrame frame = FrameComposer.Compose(map, new[] { monster }, visibility);

        Assert.Equal(".  ", frame.ToText());
    }

    [Fact]
    public void WallsAndFloorBothCarryTheirOwnGlyph()
    {
        GameMap map = OpenMap(3, 1);
        map.SetTile(new Point(1, 0), TileTypes.Wall);

        VisibilityMap visibility = new VisibilityMap(3, 1);
        visibility.Update(Cells(new Point(0, 0), new Point(1, 0), new Point(2, 0)));

        RenderedFrame frame = FrameComposer.Compose(map, Array.Empty<Entity>(), visibility);

        Assert.Equal(".#.", frame.ToText());
    }

    [Fact]
    public void ANullVisibilityMapIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => FrameComposer.Compose(OpenMap(3, 3), Array.Empty<Entity>(), null!));
    }
}
```

### [`RogueTutorial.Tests/MovementIntegrationTests.cs`](../parts/part-05-placing-monsters/RogueTutorial.Tests/MovementIntegrationTests.cs)

Part 3's file, likewise.

<!-- generated-diff -->
**Changed from Part 4.** The complete file follows; this is only what moved:

```diff
--- part-04-field-of-view/MovementIntegrationTests.cs
+++ current/MovementIntegrationTests.cs
@@ -35,7 +35,7 @@
     // Walks an entity through a map one frame of key presses at a time, as the game loop does.
     private static Point PositionAfter(GameMap map, Point start, IEnumerable<Keys[]> framesOfKeys)
     {
-        Entity walker = new Entity("Walker", '@', Color.White, start);
+        Entity walker = new Entity("Walker", '@', Color.White, start, blocksMovement: true);
 
         foreach (Keys[] keysThisFrame in framesOfKeys)
         {
@@ -116,7 +116,7 @@
     public void ThePlayerAppearsWhereTheMoveLeftIt()
     {
         GameMap room = WalledRoom(5, 5);
-        Entity player = new Entity("Player", '@', Color.White, new Point(1, 1));
+        Entity player = new Entity("Player", '@', Color.White, new Point(1, 1), blocksMovement: true);
 
         player.MoveTo(MovementRules.DestinationFor(player.Position, MovementKeys.OffsetFor(new[] { Keys.Right }), room));
 
```
<!-- generated-diff -->

```csharp
/*
 * Integration tests: the key table, the map and the movement rule composed, which is the path
 * RootScreen.ProcessKeyboard walks. Unit tests cover each piece; this level catches an axis
 * swap or a wall consulted for the wrong cell, both of which survive every piece being right.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~MovementIntegrationTests
 */

using System.Collections.Generic;
using RogueTutorial;
using SadConsole.Input;
using SadRogue.Primitives;
using Xunit;

public sealed class MovementIntegrationTests
{
    // A hand-built walled room: solid rock with the interior carved out. Built here rather
    // than generated so the expected pictures below stay fixed and readable.
    private static GameMap WalledRoom(int width, int height)
    {
        GameMap room = new GameMap(width, height);
        room.Fill(TileTypes.Wall);

        for (int row = 1; row < height - 1; row++)
        {
            for (int col = 1; col < width - 1; col++)
            {
                room.SetTile(new Point(col, row), TileTypes.Floor);
            }
        }

        return room;
    }

    // Walks an entity through a map one frame of key presses at a time, as the game loop does.
    private static Point PositionAfter(GameMap map, Point start, IEnumerable<Keys[]> framesOfKeys)
    {
        Entity walker = new Entity("Walker", '@', Color.White, start, blocksMovement: true);

        foreach (Keys[] keysThisFrame in framesOfKeys)
        {
            Point offset = MovementKeys.OffsetFor(keysThisFrame);

            walker.MoveTo(MovementRules.DestinationFor(walker.Position, offset, map));
        }

        return walker.Position;
    }

    [Fact]
    public void PressingUpMovesTowardTheTopOfTheScreen()
    {
        // Y grows downward on a console grid, so "up" must decrease Y.
        Point result = PositionAfter(new GameMap(9, 9), new Point(4, 4), new[] { new[] { Keys.Up } });

        Assert.Equal(new Point(4, 3), result);
    }

    [Fact]
    public void FourFramesOfRightMoveFourCells()
    {
        Point result = PositionAfter(
            new GameMap(9, 9),
            new Point(1, 1),
            new[] { new[] { Keys.Right }, new[] { Keys.Right }, new[] { Keys.Right }, new[] { Keys.Right } });

        Assert.Equal(new Point(5, 1), result);
    }

    [Fact]
    public void WalkingIntoAWallStopsWithoutSliding()
    {
        GameMap map = new GameMap(9, 9);
        map.SetTile(new Point(4, 3), TileTypes.Wall);

        // Three presses up from (4,4): the first is refused, and so are the other two.
        Point result = PositionAfter(
            map,
            new Point(4, 4),
            new[] { new[] { Keys.Up }, new[] { Keys.Up }, new[] { Keys.Up } });

        Assert.Equal(new Point(4, 4), result);
    }

    [Fact]
    public void AWalledRoomHoldsThePlayerIn()
    {
        GameMap room = WalledRoom(9, 9);

        // Ten presses left from the interior's left edge; the border must stop every one.
        List<Keys[]> tenPressesLeft = new List<Keys[]>();
        for (int frame = 0; frame < 10; frame++)
        {
            tenPressesLeft.Add(new[] { Keys.Left });
        }

        Point result = PositionAfter(room, new Point(2, 1), tenPressesLeft);

        Assert.Equal(new Point(1, 1), result);
        Assert.True(room.IsWalkable(result));
    }

    [Fact]
    public void AKeypadCornerReachesTheSameCellAsTwoCardinals()
    {
        GameMap map = new GameMap(9, 9);
        Point start = new Point(4, 4);

        Point viaCorner = PositionAfter(map, start, new[] { new[] { Keys.NumPad7 } });
        Point viaCardinals = PositionAfter(map, start, new[] { new[] { Keys.Left, Keys.Up } });

        Assert.Equal(viaCorner, viaCardinals);
    }

    [Fact]
    public void ThePlayerAppearsWhereTheMoveLeftIt()
    {
        GameMap room = WalledRoom(5, 5);
        Entity player = new Entity("Player", '@', Color.White, new Point(1, 1), blocksMovement: true);

        player.MoveTo(MovementRules.DestinationFor(player.Position, MovementKeys.OffsetFor(new[] { Keys.Right }), room));

        // The frame is the end of the whole chain: key -> rule -> entity -> picture.
        // A 5x5 room is one ring of wall around a 3x3 floor, and the player has stepped
        // one cell right of the top-left interior corner.
        Assert.Equal(
            string.Join("\n", "#####", "#.@.#", "#...#", "#...#", "#####"),
            FrameComposer.Compose(room, new[] { player }).ToText());
    }
}
```

## Step 3: build and run

```
dotnet build
dotnet test
dotnet run --project RogueTutorial
```

Expected: a clean build, 185 passing tests, and a dungeon with things in it.

### If something is wrong

| Symptom | Cause |
|---|---|
| `CS7036: no argument for 'blocksMovement'` | An `Entity` construction not yet updated - the compiler lists them all |
| You walk through monsters | `MovePlayer` is not consulting `BlockingEntityAt` |
| You cannot walk over anything at all | `BlockingEntityAt` is ignoring `BlocksMovement` |
| The player starts on top of a monster | The room loop in `Generate` starts at 0 instead of 1 |
| Two monsters on one cell | `PopulateRoom` is not tracking cells it has already used |
| Monsters inside walls | `PopulateRoom` is not checking `IsWalkable` |
| A blank screen | `GameWorld`'s constructor is not computing sight before the first frame |

## Step 4: regenerate the documentation

Skip this if you did not set up docfx in Part 1. Nothing was deleted this part, so there is no
stale metadata to clear:

```
dotnet docfx docfx.json --serve --port 8081
```

Expected: `Build succeeded. 0 warning(s) 0 error(s)`, and pages for `GameWorld`, `MonsterTable`,
`MonsterKind`, `PlayerAction` and `PlayerActionKind` at <http://localhost:8081>.

---

Next: **Part 6, combat.**

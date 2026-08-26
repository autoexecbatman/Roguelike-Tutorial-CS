# Part 10: Saving and loading

- [What it is](#what-it-is)
- [How to use it](#how-to-use-it)
- [How to set it up](#how-to-set-it-up)

---

# What it is

The game is written to disk after every turn and resumed when you start it again. Dying deletes
the save, so a death cannot be undone by quitting.

## The question this part has to answer

*What exactly is the game, such that writing it down and reading it back gives you the same
game?*

Everything else follows from where that line is drawn, and the line is not obvious. The map,
the entities and what you have explored are clearly the game. The screen layout is clearly not.
In between sit the mode you are in and the aiming cursor, and those turn out to be on the wrong
side of it:

```csharp
Assert.Equal(GameMode.Playing, restored.Mode);
Assert.Null(restored.Aiming);
```

**How you are looking at the game is not what the game is.** Save mid-aim and restore the
cursor, and the player reopens holding a crosshair over a scroll they never fired, in a mode
they have to escape out of before they can move. The scroll is still in the pack, because that
part *is* the game.

## The test that decides whether any of this works

```csharp
GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

Assert.Equal(Picture(original), Picture(restored));
```

Save and load must compose to the identity. Comparing the composed frame is the same argument
`RenderedFrame.ToText` has served since Part 2: the picture is what a player would notice
changing, so it is the thing to compare. One assertion covers the map, the entities, the
lighting and what has been explored.

There is a second version of it that goes through the JSON as well, because `Capture` and
`Restore` agreeing with each other is not the same as either agreeing with what reaches disk.

## Entities need ids, and that is not obvious until it breaks

The same entity is referenced from more than one place. The player is in the entity list *and*
named separately as the player. An item is in the pack *or* on the floor.

Write the objects as they lie and you get two players and a potion that is both carried and
lying at your feet. So everything gets a number, and every reference becomes that number:

```csharp
saved.PlayerId = ids[world.Player];

CarriedIds = entity.Inventory.Items.Select(item => ids[item]).ToList(),
```

On load, the entities are built first and the packs filled afterwards, so an item exists before
anything tries to put it in a bag.

## The save format is deliberately separate from the game classes

`SaveData.cs` holds records that nothing else uses. Attributes on `GameWorld` and `Entity` would
be less code, and it would mean that renaming a field breaks every save file somebody already
has on disk. A save format is a promise about a file; the game classes change every part.

It also puts the whole format in one file a person can read to see what is stored.

## The map is stored as a palette and rows of letters

The first version wrote one record per cell. It worked, and a forty-by-twenty dungeon came out
as **5,788 lines and 114,608 characters** of near-identical blocks - which quietly contradicted
the reason for choosing JSON in the first place. A save nobody can read is not a readable
format.

A dungeon uses two kinds of tile across a thousand cells. Listing the kinds once and referring
to them by letter gives **242 lines and 6,170 characters**, and the dungeon is visible in the
file:

```json
"TilePalette": [
  { "Glyph": "#", "IsWalkable": false, "IsTransparent": false },
  { "Glyph": ".", "IsWalkable": true,  "IsTransparent": true  }
],
"TileRows": [
  "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "aaaaaaaaaaaaaaaaaaaaaaaaaabbbbbbaaaaaaaa",
  "aaabbbbbbbaaaaaaaaaaaaaaaabbbbbbaaaaaaaa"
]
```

That is eighteen times smaller and, more to the point, something you can open when a save
misbehaves.

## What a corrupt save is refused for

A save is a file a player might edit, a disk might truncate, or an older build might have
written. Every one of those produces something that looks nearly right:

| Problem | Answer |
|---|---|
| A version this build does not write | `InvalidDataException` |
| Fewer rows than the height claims | `InvalidDataException` |
| A row shorter than the width claims | `InvalidDataException` |
| A cell pointing outside the palette | `InvalidDataException` |
| Text that is not JSON at all | `InvalidDataException` |
| No file at the path | `FileNotFoundException` |

**A half-read save is a corrupt game that looks like a working one**, which is the worst thing
this code could produce. A row one cell short would shift the rest of the dungeon: subtly wrong
everywhere rather than obviously wrong once.

The missing-file case is the one worth arguing about. Returning a fresh game would be
convenient and would silently discard the run the player was asking for.

## Only memory is saved, never what is currently lit

```csharp
saved.Remembered.Add(world.Visibility.StateAt(cell) != CellVisibility.Unseen);
```

What is visible right now is recomputed on load from where the player is standing. Store it and
it can disagree with the map - a lit cell behind a wall that was not there when the save was
written. Recomputing means it cannot.

## The dead end this part created, and the way out

Saving every turn made a state permanent that used to be temporary. Kill every monster and
nothing can hurt you; there are no stairs, so there is nowhere to go; and the save writes that
to disk after every step. Closing the game used to discard it. Now it resumes into it forever.

So `n` abandons the run - and asks first:

```
+--------------------------------------+
| Abandon this run?                    |
|                                      |
| y  yes, delete it and start again    |
| anything else  no                    |
+--------------------------------------+
```

**The confirmation is not politeness.** A single key that destroys a run somebody is winning is
a worse bug than the one being fixed. `y` confirms, anything else backs out, and no key at all
is not an answer - a player having second thoughts should not have to find the one correct way
to say no.

It is the third user of Part 8's modes and the second of Part 9's remembering where it came
from, which is what makes it about twenty lines rather than a feature.

Abandoning is the same ending as dying, reached on purpose: the save is deleted, not kept beside
the new one. That matters, or `n` becomes a way to escape a fight you are losing and come back
to it later.

## Death deletes the save

```csharp
if (_world.IsPlayerDead)
{
    SaveGame.Delete(SavePath);
    return;
}
```

Writing after every turn rather than on request makes the save a resume point instead of a
checkpoint to reload from, and deleting it on death is what stops a death being undone by
quitting. A roguelike where dying is optional is a different game.

That policy lives in `RootScreen` rather than in `GameWorld`, because it is about this
program's lifetime rather than about the game's rules.

## What is deliberately wrong

**One save slot, at a fixed path.** No named games, no listing them.

**No atomic write.** A crash mid-save leaves a truncated file, and the next start refuses it
rather than resuming. Writing to a temporary file and renaming it is the standard fix.

**Nothing is compressed.** Six kilobytes of JSON is nothing, but a bigger dungeon in a later
part would want it.

**A death screen does not exist.** The save is deleted and the window keeps showing the dungeon
with `You are dead.` on the status row.

**There is still nowhere to go.** Abandoning a run is an escape from a cleared dungeon, not
progress through one. Stairs arrive in Part 12.

---

# How to use it

## Play it

```
cd parts/part-10-saving-and-loading
dotnet run --project RogueTutorial
```

Play, close the window, and start it again: you resume where you were. Die and start again: a
new dungeon, because the save is gone.

| Key | Effect |
|---|---|
| `n` | Abandon this run and start another, after confirming |
| `y` | Confirm it |
| any other key | Back out |

The file is `savegame.json` beside the executable. Open it - that is the point of the format.

## Run the tests

```
dotnet test                                  # 378 tests
dotnet test --filter "Category!=EndToEnd"    # no window
```

| Test class | Level | Covers |
|---|---|---|
| [`SaveGameTests`](../parts/part-10-saving-and-loading/RogueTutorial.Tests/SaveGameTests.cs) | unit + integration | the round trip, the format, and every way a save is refused |

## Prove the tests can fail

| Change | Expect |
|---|---|
| `Capture`: store nothing as remembered | 1 fails |
| `RestoreEntity`: skip the damage, so everything loads at full health | 1 fails |
| `Restore`: put carried items back on the map as well | 1 fails |
| `Capture`: never write carried items | 3 fail |
| `Restore`: accept any version | 1 fails |
| `CaptureEntity`: always write `BlocksMovement` as true | 1 fails |

---

# How to set it up

> **You are in:** your project folder, the one holding `RogueTutorial/` and `RogueTutorial.Tests/`

## Step 1: retitle the window

One line in `RogueTutorial/Program.cs`:

```csharp
const string WindowTitle = "Roguelike Tutorial - Part 10: Saving and loading";
```

## Step 2: the source files

**Each block below is the complete file.** Two are new; the other three already exist and should
be replaced entirely.

No new package is needed: `System.Text.Json` is part of the framework.

**Do not build until every file in this step is in place** - C# compiles a project as a whole,
so a half-finished step fails on files that are perfectly correct.

### [`RogueTutorial/GameCommand.cs`](../parts/part-10-saving-and-loading/RogueTutorial/GameCommand.cs)

The Part 9 file, with the confirmation mode and its three commands.

<!-- generated-diff -->
**Changed from Part 9.** The complete file follows; this is only what moved:

```diff
--- part-09-ranged-scrolls-and-targeting/GameCommand.cs
+++ current/GameCommand.cs
@@ -32,6 +32,12 @@
 
     /// <summary>The pack is open. Letters choose an item and Escape closes it.</summary>
     ShowingInventory,
+
+    /// <summary>
+    /// The player has asked to abandon this run. One key confirms and anything else does not,
+    /// because a stray press should never be able to destroy a game somebody is winning.
+    /// </summary>
+    ConfirmingNewGame,
 
     /// <summary>
     /// A scroll is being aimed. Movement keys move the cursor, Enter fires, Escape goes back to
@@ -73,6 +79,15 @@
 
     /// <summary>Give up aiming and go back to the pack.</summary>
     CancelTarget,
+
+    /// <summary>Ask to abandon this run and start another.</summary>
+    AskNewGame,
+
+    /// <summary>Confirm it: the save is deleted and a fresh dungeon generated.</summary>
+    ConfirmNewGame,
+
+    /// <summary>Think better of it.</summary>
+    CancelNewGame,
 }
 
 internal readonly struct GameCommand
@@ -122,4 +137,13 @@
 
     /// <summary>Give up aiming.</summary>
     public static GameCommand CancelTarget => new GameCommand(GameCommandKind.CancelTarget, Point.Zero, -1);
+
+    /// <summary>Ask to abandon this run.</summary>
+    public static GameCommand AskNewGame => new GameCommand(GameCommandKind.AskNewGame, Point.Zero, -1);
+
+    /// <summary>Confirm abandoning it.</summary>
+    public static GameCommand ConfirmNewGame => new GameCommand(GameCommandKind.ConfirmNewGame, Point.Zero, -1);
+
+    /// <summary>Think better of it.</summary>
+    public static GameCommand CancelNewGame => new GameCommand(GameCommandKind.CancelNewGame, Point.Zero, -1);
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
}
```

### [`RogueTutorial/CommandReader.cs`](../parts/part-10-saving-and-loading/RogueTutorial/CommandReader.cs)

The Part 9 file, reading the confirmation.

<!-- generated-diff -->
**Changed from Part 9.** The complete file follows; this is only what moved:

```diff
--- part-09-ranged-scrolls-and-targeting/CommandReader.cs
+++ current/CommandReader.cs
@@ -46,6 +46,7 @@
             GameMode.Playing => ReadPlaying(pressedKeys),
             GameMode.ShowingInventory => ReadInventory(pressedKeys, shiftHeld),
             GameMode.Targeting => ReadTargeting(pressedKeys),
+            GameMode.ConfirmingNewGame => ReadConfirmation(pressedKeys),
             _ => GameCommand.None,
         };
     }
@@ -79,6 +80,32 @@
         if (pressedKeys.Contains(Keys.I))
         {
             return GameCommand.OpenInventory;
+        }
+
+        // Abandoning a run is the way out of a cleared dungeon, where nothing can kill you and
+        // there is nowhere left to go.
+        if (pressedKeys.Contains(Keys.N))
+        {
+            return GameCommand.AskNewGame;
+        }
+
+        return GameCommand.None;
+    }
+
+    // Confirming: one key means yes and everything else means no, which is the safe way round
+    // for a question whose yes destroys a run.
+    private static GameCommand ReadConfirmation(IReadOnlyCollection<Keys> pressedKeys)
+    {
+        if (pressedKeys.Contains(Keys.Y))
+        {
+            return GameCommand.ConfirmNewGame;
+        }
+
+        // Any other key backs out. A player who has second thoughts should not have to find the
+        // one correct way to say no.
+        if (pressedKeys.Count > 0)
+        {
+            return GameCommand.CancelNewGame;
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

### [`RogueTutorial/SaveData.cs`](../parts/part-10-saving-and-loading/RogueTutorial/SaveData.cs)

The format: plain records holding exactly what a save has to remember.

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

### [`RogueTutorial/VisibilityMap.cs`](../parts/part-10-saving-and-loading/RogueTutorial/VisibilityMap.cs)

The Part 4 file, with RestoreMemory. SaveGame calls it, so it comes first.

<!-- generated-diff -->
**Changed from Part 9.** The complete file follows; this is only what moved:

```diff
--- part-09-ranged-scrolls-and-targeting/VisibilityMap.cs
+++ current/VisibilityMap.cs
@@ -100,6 +100,29 @@
     }
 
     /// <summary>
+    /// Marks cells as remembered without making any of them visible, which is what a loaded save
+    /// needs: the map comes back filled in, and what is actually in sight is recomputed from
+    /// where the player is standing. Throws ArgumentNullException on a null list and
+    /// ArgumentException when it is not one entry per cell, since a mismatch would silently
+    /// shift the whole map by a row.
+    /// </summary>
+    public void RestoreMemory(IReadOnlyList<bool> remembered)
+    {
+        ArgumentNullException.ThrowIfNull(remembered);
+
+        if (remembered.Count != _remembered.Length)
+        {
+            throw new ArgumentException(
+                $"Expected {_remembered.Length} cells of memory, got {remembered.Count}.", nameof(remembered));
+        }
+
+        for (int index = 0; index < _remembered.Length; index++)
+        {
+            _remembered[index] = remembered[index];
+        }
+    }
+
+    /// <summary>
     /// How much the player knows about the cell. Throws ArgumentOutOfRangeException off the map,
     /// because asking about a cell that does not exist is a caller error rather than a state.
     /// </summary>
```
<!-- generated-diff -->

```csharp
/*
 * What the player can see now, and what they remember seeing.
 *
 * Three states per cell, and the middle one is what makes a dungeon feel explored:
 *
 *     Unseen      never in sight - drawn as nothing at all
 *     Remembered  seen once, not now - drawn dim, from memory
 *     Visible     in sight this turn - drawn lit
 *
 * Remembering is one-way. A cell that has been seen never returns to Unseen, which is why the
 * map fills in behind you as you walk and stays filled in.
 *
 * Usage:
 *
 *     VisibilityMap visibility = new VisibilityMap(map.Width, map.Height);
 *
 *     visibility.Update(FieldOfView.From(player.Position, 8, map));
 *
 *     CellVisibility state = visibility.StateAt(new Point(4, 3));   // -> Visible
 *     bool draw = state != CellVisibility.Unseen;                    // is there anything to draw
 *
 * Refuses a dimension below one, a null cell set, and a query outside the map.
 */

using System;
using System.Collections.Generic;
using SadRogue.Primitives;

namespace RogueTutorial;

/// <summary>How much the player knows about one cell.</summary>
internal enum CellVisibility
{
    /// <summary>Never seen. Nothing is drawn here.</summary>
    Unseen,

    /// <summary>Seen before, not in sight now. Drawn dim, from memory.</summary>
    Remembered,

    /// <summary>In sight this turn. Drawn lit.</summary>
    Visible,
}

internal sealed class VisibilityMap
{
    // The rectangle of legal positions, reused from Part 1.
    private readonly GridBounds _bounds;

    // True once the cell has ever been seen. Never returns to false.
    private readonly bool[] _remembered;

    // True while the cell is in sight this turn. Replaced wholesale on every Update.
    private readonly bool[] _visible;

    /// <summary>Number of cells across.</summary>
    public int Width => _bounds.Width;

    /// <summary>Number of cells down.</summary>
    public int Height => _bounds.Height;

    /// <summary>
    /// Creates a visibility map of the given size with every cell unseen. Throws
    /// ArgumentOutOfRangeException when either dimension is below one.
    /// </summary>
    public VisibilityMap(int width, int height)
    {
        _bounds = new GridBounds(width, height);

        _remembered = new bool[width * height];
        _visible = new bool[width * height];
    }

    /// <summary>
    /// Replaces what is currently visible with the given cells, and adds all of them to what is
    /// remembered. Cells outside the map are ignored rather than rejected, so a field of view
    /// computed against a larger radius than the map can be passed straight in. Throws
    /// ArgumentNullException on a null set.
    /// </summary>
    public void Update(ISet<Point> visibleCells)
    {
        ArgumentNullException.ThrowIfNull(visibleCells);

        // Sight is recomputed from scratch each turn, so last turn's must be cleared first.
        // Memory is not cleared, which is the entire difference between the two arrays.
        Array.Clear(_visible);

        foreach (Point cell in visibleCells)
        {
            // A field of view may legitimately be asked for near an edge; ignore what falls off.
            if (!_bounds.Contains(cell))
            {
                continue;
            }

            int index = IndexOf(cell);

            _visible[index] = true;
            _remembered[index] = true;
        }
    }

    /// <summary>
    /// Marks cells as remembered without making any of them visible, which is what a loaded save
    /// needs: the map comes back filled in, and what is actually in sight is recomputed from
    /// where the player is standing. Throws ArgumentNullException on a null list and
    /// ArgumentException when it is not one entry per cell, since a mismatch would silently
    /// shift the whole map by a row.
    /// </summary>
    public void RestoreMemory(IReadOnlyList<bool> remembered)
    {
        ArgumentNullException.ThrowIfNull(remembered);

        if (remembered.Count != _remembered.Length)
        {
            throw new ArgumentException(
                $"Expected {_remembered.Length} cells of memory, got {remembered.Count}.", nameof(remembered));
        }

        for (int index = 0; index < _remembered.Length; index++)
        {
            _remembered[index] = remembered[index];
        }
    }

    /// <summary>
    /// How much the player knows about the cell. Throws ArgumentOutOfRangeException off the map,
    /// because asking about a cell that does not exist is a caller error rather than a state.
    /// </summary>
    public CellVisibility StateAt(Point position)
    {
        if (!_bounds.Contains(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, "The position is outside the map.");
        }

        int index = IndexOf(position);

        // Visible outranks remembered: a cell in sight is drawn lit, not from memory.
        if (_visible[index])
        {
            return CellVisibility.Visible;
        }

        return _remembered[index] ? CellVisibility.Remembered : CellVisibility.Unseen;
    }

    // Row-major index; the single place this map's storage layout is expressed.
    private int IndexOf(Point position)
    {
        return (position.Y * Width) + position.X;
    }
}
```

### [`RogueTutorial/GameWorld.cs`](../parts/part-10-saving-and-loading/RogueTutorial/GameWorld.cs)

The Part 9 file, with RestoreMemory.

<!-- generated-diff -->
**Changed from Part 9.** The complete file follows; this is only what moved:

```diff
--- part-09-ranged-scrolls-and-targeting/GameWorld.cs
+++ current/GameWorld.cs
@@ -263,9 +263,24 @@
             if (IsPlayerDead)
             {
                 Log.Add("You die.");
+
+                // Nothing beyond this point in the round matters, and the run is over: Part 10
+                // deletes the save here so a death cannot be undone by reloading.
                 return;
             }
         }
+    }
+
+    /// <summary>
+    /// Fills in what the player remembers, for a world rebuilt from a save. What is visible is
+    /// recomputed immediately afterwards, so memory and sight cannot disagree with the map.
+    /// Throws ArgumentException when the list is not one entry per cell.
+    /// </summary>
+    public void RestoreMemory(IReadOnlyList<bool> remembered)
+    {
+        Visibility.RestoreMemory(remembered);
+
+        RecomputeFieldOfView();
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

### [`RogueTutorial/SaveGame.cs`](../parts/part-10-saving-and-loading/RogueTutorial/SaveGame.cs)

Writing a game down and reading it back.

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
    private const int CurrentVersion = 1;

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
                saved.Fighter.MaximumHitPoints, saved.Fighter.Attack, saved.Fighter.Defence);

            // Constructed at full health, so the difference is applied as damage rather than by
            // reaching past the class and setting the field.
            fighter.TakeDamage(saved.Fighter.MaximumHitPoints - saved.Fighter.HitPoints);

            entity.Fighter = fighter;
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

### [`RogueTutorial/ScreenComposer.cs`](../parts/part-10-saving-and-loading/RogueTutorial/ScreenComposer.cs)

The Part 9 file, drawing the confirmation.

<!-- generated-diff -->
**Changed from Part 9.** The complete file follows; this is only what moved:

```diff
--- part-09-ranged-scrolls-and-targeting/ScreenComposer.cs
+++ current/ScreenComposer.cs
@@ -103,6 +103,13 @@
             WriteTargeting(world.Aiming, layout, glyphs, foregrounds);
         }
 
+        // Over everything, because a question the player has to answer must not be behind
+        // anything else on the screen.
+        if (world.Mode == GameMode.ConfirmingNewGame)
+        {
+            WriteConfirmation(layout, glyphs, foregrounds);
+        }
+
         return new RenderedFrame(layout.WindowWidth, layout.WindowHeight, glyphs, foregrounds);
     }
 
@@ -213,6 +220,28 @@
         for (int line = 0; line < lines.Count && line < layout.MapHeight; line++)
         {
             WriteLine(lines[line], line * layout.WindowWidth, layout.WindowWidth, glyphs, foregrounds, PanelText);
+        }
+    }
+
+    // Draws the question about abandoning the run, framed like the pack so it reads as a panel.
+    private static void WriteConfirmation(ScreenLayout layout, char[] glyphs, Color[] foregrounds)
+    {
+        int width = Math.Min(InventoryWidth, layout.WindowWidth);
+        int inner = width - 4;
+
+        List<string> lines = new List<string>
+        {
+            "+" + new string('-', width - 2) + "+",
+            "| " + "Abandon this run?".PadRight(inner) + " |",
+            "| " + "".PadRight(inner) + " |",
+            "| " + "y  yes, delete it and start again".PadRight(inner) + " |",
+            "| " + "anything else  no".PadRight(inner) + " |",
+            "+" + new string('-', width - 2) + "+",
+        };
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

### [`RogueTutorial/RootScreen.cs`](../parts/part-10-saving-and-loading/RogueTutorial/RootScreen.cs)

The Part 9 file, now owning the save file and able to replace the world.

<!-- generated-diff -->
**Changed from Part 9.** The complete file follows; this is only what moved:

```diff
--- part-09-ranged-scrolls-and-targeting/RootScreen.cs
+++ current/RootScreen.cs
@@ -10,6 +10,13 @@
  *
  *     new Builder().SetStartingScreen<RootScreen>()
  *
+ * It also owns the save file: resuming on start, writing after every turn that changed
+ * anything, deleting it when the player dies so a run cannot be undone by reloading, and
+ * replacing it when the player abandons a run - which is the way out of a cleared dungeon,
+ * where nothing can kill you and there is nowhere left to go. That
+ * policy lives here rather than in GameWorld because it is about this program's lifetime rather
+ * than about the game's rules.
+ *
  * Constructing it in a test process throws: the constructor reads Game.Instance for the grid
  * size, and that requires a live graphics host. Test GameWorld instead.
  */
@@ -25,6 +32,10 @@
 
 internal sealed class RootScreen : ScreenObject
 {
+    // Where the game is kept between runs. Beside the executable, which is where a player
+    // looking for it would think to look.
+    private const string SavePath = "savegame.json";
+
     // How many rows of message log are shown. Five is enough to follow a fight without taking
     // so much of the window that the dungeon becomes cramped.
     private const int LogRows = 5;
@@ -36,7 +47,7 @@
     private readonly ScreenLayout _layout;
 
     // The dungeon, everyone standing in it, and what the player has seen.
-    private readonly GameWorld _world;
+    private GameWorld _world;
 
     /// <summary>
     /// Sizes the surface to the window, generates a world to fill it, and paints the first frame.
@@ -55,8 +66,9 @@
         // The dungeon fills the map area rather than the window: the panel takes the rest.
         // No seed is given, so every run is a different dungeon with different monsters. Pass a
         // number to Random's constructor to play the same one repeatedly while debugging.
-        _world = GameWorld.Generate(
-            _layout.WindowWidth, _layout.MapHeight, new Random(), MonsterTable.Standard, ItemTable.Standard);
+        // A save is resumed rather than replaced. Starting a new dungeon over the top of one
+        // somebody is halfway through is the one unrecoverable mistake this class could make.
+        _world = SaveGame.Exists(SavePath) ? SaveGame.Read(SavePath) : NewWorld();
 
         DrawFrame();
     }
@@ -84,6 +96,8 @@
 
         Apply(command);
 
+        PersistOrDelete();
+
         // Every command that reaches here changed the screen: the map moved, the log gained a
         // line, or the pack opened or closed.
         DrawFrame();
@@ -92,6 +106,34 @@
     }
 
     /// <summary>
+    /// Generates a fresh dungeon at the layout's map size. No seed is given, so every run is a
+    /// different one; pass a number to Random's constructor to replay the same one.
+    /// </summary>
+    private GameWorld NewWorld()
+    {
+        return GameWorld.Generate(
+            _layout.WindowWidth, _layout.MapHeight, new Random(), MonsterTable.Standard, ItemTable.Standard);
+    }
+
+    /// <summary>
+    /// Writes the game after every command, or deletes the save once the player is dead.
+    ///
+    /// Saving every turn rather than on request is what makes the save a resume point rather
+    /// than a checkpoint to reload from, and deleting it on death is what stops a death being
+    /// undone by quitting. A roguelike where dying is optional is a different game.
+    /// </summary>
+    private void PersistOrDelete()
+    {
+        if (_world.IsPlayerDead)
+        {
+            SaveGame.Delete(SavePath);
+            return;
+        }
+
+        SaveGame.Write(_world, SavePath);
+    }
+
+    /// <summary>
     /// Hands one command to the world. Nothing is decided here - the world knows whether a slot
     /// holds anything and whether a move is legal, and this only routes.
     /// </summary>
@@ -133,6 +175,21 @@
 
             case GameCommandKind.CancelTarget:
                 _world.CancelTarget();
+                break;
+
+            case GameCommandKind.AskNewGame:
+                _world.SetMode(GameMode.ConfirmingNewGame);
+                break;
+
+            case GameCommandKind.CancelNewGame:
+                _world.SetMode(GameMode.Playing);
+                break;
+
+            case GameCommandKind.ConfirmNewGame:
+                // The old run is gone rather than kept beside the new one: this is the same
+                // ending as dying, reached on purpose instead of by accident.
+                SaveGame.Delete(SavePath);
+                _world = NewWorld();
                 break;
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
        _world = SaveGame.Exists(SavePath) ? SaveGame.Read(SavePath) : NewWorld();

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

## Step 3: the test file

**This block is the complete file.** Create it in `RogueTutorial.Tests/`.

### [`RogueTutorial.Tests/SaveGameTests.cs`](../parts/part-10-saving-and-loading/RogueTutorial.Tests/SaveGameTests.cs)

The round trip, the format, and what a corrupt save is refused for.

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
        rat.Fighter = new Fighter(4, 3, 0);
        rat.Die();

        GameWorld world = new GameWorld(original.Map, new List<Entity> { rat, original.Player }, original.Player);

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(world));

        Entity restoredRat = restored.Entities.First(entity => entity.Name.StartsWith("remains"));

        Assert.Null(restoredRat.Fighter);
        Assert.False(restoredRat.BlocksMovement);
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

### [`RogueTutorial.Tests/NewGameTests.cs`](../parts/part-10-saving-and-loading/RogueTutorial.Tests/NewGameTests.cs)

Abandoning a run, and why the question is asked before it is done.

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
        player.Fighter = new Fighter(30, 5, 2);
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

Expected: a clean build, 378 passing tests, and a game that resumes.

### If something is wrong

| Symptom | Cause |
|---|---|
| Starting the game always gives a new dungeon | `RootScreen` is generating instead of checking `SaveGame.Exists` |
| Loading gives two players | The player is being written both in the list and separately, without ids |
| Carried items appear on the floor after loading | `Restore` is putting every saved entity into the entity list |
| The pack is empty after loading | `Capture` is not walking inventories, so carried items were never written |
| The dungeon is unexplored after loading | Memory is not being captured, or `RestoreMemory` is not called |
| Everything loads at full health | `RestoreEntity` is not applying the damage |
| A dead run resumes | `PersistOrDelete` is writing rather than deleting when the player is dead |
| `InvalidDataException` on a save you just wrote | The version constant changed without the format changing, or the reverse |

## Step 5: regenerate the documentation

Skip this if you did not set up docfx in Part 1. Nothing was deleted this part:

```
dotnet docfx docfx.json --serve --port 8081
```

Expected: `Build succeeded. 0 warning(s) 0 error(s)`, and pages for `SaveGame`, `SavedWorld` and
the other saved records at <http://localhost:8081>.

---

Next: **Part 11, levelling and character progression.**

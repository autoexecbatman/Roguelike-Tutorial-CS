# Part 2: The entity class and the map

- [What it is](#what-it-is)
- [How to use it](#how-to-use-it)
- [How to set it up](#how-to-set-it-up)

---

# What it is

A room with walls in it, two characters standing in the room, and a rule that stops you walking
through stone. Plus the change that matters most: **what appears on screen becomes something a
test can read.**

Part 1 had one `@` on an empty grid, and nothing checked what was drawn. This part fixes both.

## The picture, before any code

This is the actual frame the game composes at 80x25, printed by a test rather than screenshotted
from a window:

```
################################################################################
#..............................................................................#
#..............................................................................#
#..............................................................................#
#..............................................................................#
#..............................................................................#
#..............................................................................#
#..............................................................................#
#..............................................................................#
#..............................................................................#
#..............................................................................#
#..............................................................................#
#.........................#...........@.@............#.........................#
#..............................................................................#
#..............................................................................#
#..............................................................................#
#..............................................................................#
#..............................................................................#
#..............................................................................#
#..............................................................................#
#..............................................................................#
#..............................................................................#
#..............................................................................#
#..............................................................................#
################################################################################
```

Border of wall, two pillars, a yellow villager and a white player side by side in the middle.
That a *test* can print this is the whole point of the part.

## The big idea: separate what to draw from drawing it

Part 1's `DrawFrame` wrote glyphs straight onto a SadConsole surface. That surface only exists
when a window is open, so nothing about the picture could be checked. The tests covered where
the player *thought* it was, never what a player would *see*.

Part 2 splits that in two:

```
GameMap + entities  ->  FrameComposer.Compose  ->  RenderedFrame  ->  RootScreen blits it
                                                   ^
                                                   tests read this
```

`RenderedFrame` holds glyphs and colours as plain arrays, and `ToText()` turns it into a string.
So an expected picture is written in a test as ASCII and compared:

```csharp
Assert.Equal(
    Picture(
        "#..",
        "...",
        "..#"),
    frame.ToText());
```

That test fails if a wall lands in the wrong cell, if the map is drawn transposed, if an entity
covers the wrong square, or if the frame comes out the wrong size. None of it needs a window,
and all of it runs in milliseconds.

## Clamping is gone, and that is a behaviour change

Part 1 kept the player on the grid with `Math.Clamp`. With walls, clamping becomes wrong:

```
Part 1:  walk into the edge  ->  slide to the nearest legal cell
Part 2:  walk into a wall    ->  do not move at all
```

Clamping adjusts a move. A wall refuses one. The difference shows up in corners — under
clamping, a diagonal into a corner still moves you one axis, which reads as the player sliding
along the stone. `MovementRules.DestinationFor` returns the starting cell unchanged instead:

```csharp
public static Point DestinationFor(Point start, Point offset, GameMap map)
{
    ArgumentNullException.ThrowIfNull(map);

    Point destination = start + offset;

    // IsWalkable answers false off the map too, so one question covers walls and edges.
    if (!map.IsWalkable(destination))
    {
        return start;
    }

    return destination;
}
```

That comment is the design in one line. `GameMap.IsWalkable` answers `false` for a cell outside
the map as well as for a wall, so the edge of the world and a piece of stone are the same
question. Nothing in the movement rule knows the map has edges.

`PlayerMover` is deleted in this part, along with its tests. It held a position and clamped it;
`Entity` holds the position and `MovementRules` decides the move.

## The new types

| Type | What it is | Host-free |
|---|---|---|
| [`Tile`](../parts/part-02-entities-and-the-map/RogueTutorial/Tile.cs) | one cell: glyph, colour, walkable, transparent | yes |
| [`TileTypes`](../parts/part-02-entities-and-the-map/RogueTutorial/TileTypes.cs) | the standard kinds, `Floor` and `Wall` | yes |
| [`GameMap`](../parts/part-02-entities-and-the-map/RogueTutorial/GameMap.cs) | a rectangle of tiles and the questions asked of it | yes |
| [`MapFactory`](../parts/part-02-entities-and-the-map/RogueTutorial/MapFactory.cs) | builds the one room this part uses | yes |
| [`Entity`](../parts/part-02-entities-and-the-map/RogueTutorial/Entity.cs) | anything occupying a cell and drawn over the map | yes |
| [`MovementRules`](../parts/part-02-entities-and-the-map/RogueTutorial/MovementRules.cs) | where a move ends up, given the map | yes |
| [`FrameComposer`](../parts/part-02-entities-and-the-map/RogueTutorial/FrameComposer.cs) | builds the picture | yes |
| [`RenderedFrame`](../parts/part-02-entities-and-the-map/RogueTutorial/RenderedFrame.cs) | the picture, as data | yes |
| [`RootScreen`](../parts/part-02-entities-and-the-map/RogueTutorial/RootScreen.cs) | wiring and blitting only | no |

Everything except `RootScreen` runs in a test process. That is the same rule as Part 1, applied
to twice as much code.

### `Tile` is a readonly struct

```csharp
internal readonly struct Tile
{
    public char Glyph { get; }
    public Color Foreground { get; }
    public bool IsWalkable { get; }
    public bool IsTransparent { get; }
}
```

`readonly` here means something different from Part 1's `readonly` field. On a struct it means
**no member can be modified after construction**, so a tile is a value you replace rather than
an object you edit. `map.SetTile(position, TileTypes.Wall)` writes a copy into the array; there
is no shared wall object that some other code could mutate underneath you.

`IsTransparent` is unused until field of view in Part 4. It is here because it is a property of
what a tile *is*, and adding it later would mean touching every tile kind again.

### `GameMap` refuses two things and permits a third

```csharp
GetTile(position)     // off the map -> throws
SetTile(position, t)  // off the map -> throws
IsWalkable(position)  // off the map -> false
```

Reading or writing a cell that does not exist is a programming error, and it should fail where
the mistake was made. Asking whether you can *stand* somewhere is an ordinary question that
movement code asks every frame, including about cells beyond the edge — so it answers rather
than throws.

That asymmetry is what lets `MovementRules` be four lines long.

### `Entity` applies no rules

```csharp
public void MoveTo(Point destination)
{
    Position = destination;
}
```

No validation, deliberately. An entity that checked the map would need a reference to it, and
then every entity would be tied to one map, and moving between levels in a later part would
mean rewiring all of them. The rule lives in `MovementRules`, the caller applies it, and
`Entity` stays a thing rather than a thing-that-knows-about-maps.

## What is deliberately wrong

**`DrawFrame` still repaints every cell on every move.** On an 80x25 grid that is 2000 writes to
move one character. It is fine at this size and it will not stay fine. The fix is to compose the
new frame, compare it with the previous one, and write only the cells that differ — which is
easy now that a frame is data, and pointless before there was enough on screen to matter.

**The villager does nothing.** It cannot be attacked, it does not move, and you can walk onto
its square. Entities that occupy space arrive in Part 5, and combat in Part 6.

**The map is hardcoded.** `MapFactory.CreateWalledRoom` builds one room with two pillars, the
same every time. Real generation — rooms joined by corridors, placed at random — is Part 3.

---

# How to use it

## Play it

```
cd parts/part-02-entities-and-the-map
dotnet run --project RogueTutorial
```

Same controls as Part 1: arrows or the numeric keypad, keypad corners for diagonals. What is new
is that the walls stop you, and there is somebody else in the room.

Walk into the border and you stop dead rather than sliding along it. Walk diagonally into a
pillar and nothing happens at all — no partial move on one axis.

## Run the tests

```
dotnet test                                  # 87 tests
dotnet test --filter "Category!=EndToEnd"    # no window
```

The suite has grown from 46 to 87. The new classes:

| Test class | Level | Covers |
|---|---|---|
| [`GameMapTests`](../parts/part-02-entities-and-the-map/RogueTutorial.Tests/GameMapTests.cs) | unit | fill, bounds, what throws off the map and what answers false |
| [`MapFactoryTests`](../parts/part-02-entities-and-the-map/RogueTutorial.Tests/MapFactoryTests.cs) | unit | every border cell, the pillar count, determinism |
| [`MovementRulesTests`](../parts/part-02-entities-and-the-map/RogueTutorial.Tests/MovementRulesTests.cs) | unit | steps onto floor, refusals into walls and off the map |
| [`FrameComposerTests`](../parts/part-02-entities-and-the-map/RogueTutorial.Tests/FrameComposerTests.cs) | unit | the picture, as ASCII |
| [`MovementIntegrationTests`](../parts/part-02-entities-and-the-map/RogueTutorial.Tests/MovementIntegrationTests.cs) | integration | key -> rule -> entity -> picture, end to end without a window |

Two habits in those tests are worth copying.

**Walk the whole border, do not sample it.** `EveryBorderCellIsWall` loops every cell of all four
sides rather than checking the corners. A loop that stops one short leaves a single gap the
player can walk out through, and a spot check will not find it.

**Pick a cell whose transpose is also on the map.** `SetTileAndGetTileAgreeOnWhichCellIsWhich`
writes to `(2,0)` on a 4x3 map and checks `(0,2)` is untouched. Both exist, so swapping `x` and
`y` in the index formula corrupts one and the test sees it. Testing a cell on the diagonal would
prove nothing.

## Prove the tests can fail

As in Part 1, break it on purpose:

| Change | Expect |
|---|---|
| `MovementRules`: return `new Point(destination.X, start.Y)` instead of `start` | 7 fail |
| `FrameComposer`: index entities as `(X * map.Height) + Y` | 2 fail |
| `MapFactory`: delete the second pillar | 2 fail |

**Two mutations I tried first survived, and neither was a gap in the tests.** Gating the map
writes on `glyphs[index] == '\0'` changes nothing, because the array starts as all `'\0'` and the
map loop runs first. Stopping the border loop one column short changes nothing either, because
the left-right loop writes column `width - 1` for every row, corners included.

That is worth knowing before you conclude a survivor means a missing test. A mutation that
cannot change the output is called an equivalent mutant, and it tells you about the code rather
than the tests. Check whether your change is observable at all before writing a test to catch
it.

## Look at the frame yourself

Because a frame is data, you can print one from a test rather than squinting at a window:

```csharp
GameMap map = MapFactory.CreateWalledRoom(80, 25);
Entity player = new Entity("Player", '@', Color.White, new Point(map.Width / 2, map.Height / 2));

File.WriteAllText("frame.txt", FrameComposer.Compose(map, new[] { player }).ToText());
```

That is how the picture at the top of this page was produced. It is the quickest way to see what
a map generator is actually doing, and it will be worth much more in Part 3.

---

# How to set it up

If you followed Part 1 you have a working project. This part adds files to it and deletes one.

> **You are in:** your project folder, the one holding `RogueTutorial/` and `RogueTutorial.Tests/`

## Step 1: add the new source files

Eight new files in `RogueTutorial/`, in this order - each depends only on the ones before it.
**Each block below is the complete file.** Create the file and paste the whole block; do not
merge pieces into anything you already have.

### The test files

**Each block below is the complete file.** Create it in `RogueTutorial.Tests/` and paste the
whole thing.

Write each test before the code it covers where you can, and watch it fail first -
[Writing tests](writing-tests.md) explains why that step matters and what a real failure looks
like. `GridBoundsTests`, `MovementKeysTests`, `UntestabilityProof` and `GameStartsEndToEndTests`
carry over from Part 1 unchanged. `PlayerMoverTests` is deleted in Step 2.

### [`RogueTutorial.Tests/GameMapTests.cs`](../parts/part-02-entities-and-the-map/RogueTutorial.Tests/GameMapTests.cs)

The map's fill, its bounds, and the split between what throws off the map and what answers false.

```csharp
/*
 * Unit tests for the dungeon floor. Expected values come from the map specification: every
 * cell starts as floor, reading or writing off the map is a caller error, and asking whether
 * you may stand off the map is an ordinary question with the answer "no".
 *
 * Usage:  dotnet test --filter FullyQualifiedName~GameMapTests
 */

using System;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class GameMapTests
{
    [Fact]
    public void ANewMapIsAllFloor()
    {
        GameMap map = new GameMap(4, 3);

        // Every cell, not a sample: a fill bug that missed one row would pass a spot check.
        for (int row = 0; row < map.Height; row++)
        {
            for (int col = 0; col < map.Width; col++)
            {
                Assert.True(map.IsWalkable(new Point(col, row)), $"cell ({col},{row}) should start walkable");
            }
        }
    }

    [Fact]
    public void TheMapKeepsTheSizeItWasGiven()
    {
        GameMap map = new GameMap(80, 25);

        Assert.Equal(80, map.Width);
        Assert.Equal(25, map.Height);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(3, 2, true)]
    [InlineData(4, 2, false)]
    [InlineData(3, 3, false)]
    [InlineData(-1, 0, false)]
    public void IsInBoundsAcceptsExactlyTheCellsOfTheMap(int x, int y, bool expected)
    {
        GameMap map = new GameMap(4, 3);

        Assert.Equal(expected, map.IsInBounds(new Point(x, y)));
    }

    [Fact]
    public void AWallIsNotWalkable()
    {
        GameMap map = new GameMap(4, 3);

        map.SetTile(new Point(1, 1), TileTypes.Wall);

        Assert.False(map.IsWalkable(new Point(1, 1)));
    }

    [Fact]
    public void SettingOneTileLeavesItsNeighboursAlone()
    {
        GameMap map = new GameMap(4, 3);

        map.SetTile(new Point(1, 1), TileTypes.Wall);

        // A row-major indexing error would most likely show up on the neighbours.
        Assert.True(map.IsWalkable(new Point(0, 1)));
        Assert.True(map.IsWalkable(new Point(2, 1)));
        Assert.True(map.IsWalkable(new Point(1, 0)));
        Assert.True(map.IsWalkable(new Point(1, 2)));
    }

    [Fact]
    public void SetTileAndGetTileAgreeOnWhichCellIsWhich()
    {
        GameMap map = new GameMap(4, 3);

        // (2,0) and its transpose (0,2) are both on a 4x3 map, so swapping x and y
        // in the index would write to the wrong one of them and this would catch it.
        map.SetTile(new Point(2, 0), TileTypes.Wall);

        Assert.Equal('#', map.GetTile(new Point(2, 0)).Glyph);
        Assert.Equal('.', map.GetTile(new Point(0, 2)).Glyph);
    }

    [Fact]
    public void WalkingOffTheMapIsNotPossible()
    {
        GameMap map = new GameMap(4, 3);

        // Off the map answers false rather than throwing, so movement code can ask freely.
        Assert.False(map.IsWalkable(new Point(-1, 0)));
        Assert.False(map.IsWalkable(new Point(4, 0)));
        Assert.False(map.IsWalkable(new Point(0, -1)));
        Assert.False(map.IsWalkable(new Point(0, 3)));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(4, 0)]
    [InlineData(0, 3)]
    public void ReadingOffTheMapIsRejected(int x, int y)
    {
        GameMap map = new GameMap(4, 3);

        Assert.Throws<ArgumentOutOfRangeException>(() => map.GetTile(new Point(x, y)));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(4, 0)]
    [InlineData(0, 3)]
    public void WritingOffTheMapIsRejected(int x, int y)
    {
        GameMap map = new GameMap(4, 3);

        Assert.Throws<ArgumentOutOfRangeException>(() => map.SetTile(new Point(x, y), TileTypes.Wall));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    public void ADimensionBelowOneIsRejected(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameMap(width, height));
    }
}
```

### [`RogueTutorial.Tests/MapFactoryTests.cs`](../parts/part-02-entities-and-the-map/RogueTutorial.Tests/MapFactoryTests.cs)

Every border cell, the pillar count, and that the room is the same every time.

```csharp
/*
 * Unit tests for the one map this part builds. Expected values come from the description of
 * the room: the outermost ring is wall, everything else is floor, and two pillars stand in it.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~MapFactoryTests
 */

using System;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class MapFactoryTests
{
    [Fact]
    public void TheRoomIsTheSizeAsked()
    {
        GameMap room = MapFactory.CreateWalledRoom(80, 25);

        Assert.Equal(80, room.Width);
        Assert.Equal(25, room.Height);
    }

    [Fact]
    public void EveryBorderCellIsWall()
    {
        GameMap room = MapFactory.CreateWalledRoom(10, 6);

        // Walk the whole border rather than sampling corners; a loop that stops one short
        // leaves a gap the player can walk through, and a spot check would miss it.
        for (int col = 0; col < room.Width; col++)
        {
            Assert.False(room.IsWalkable(new Point(col, 0)), $"top border at x={col}");
            Assert.False(room.IsWalkable(new Point(col, room.Height - 1)), $"bottom border at x={col}");
        }

        for (int row = 0; row < room.Height; row++)
        {
            Assert.False(room.IsWalkable(new Point(0, row)), $"left border at y={row}");
            Assert.False(room.IsWalkable(new Point(room.Width - 1, row)), $"right border at y={row}");
        }
    }

    [Fact]
    public void TheRoomHasAWalkableInterior()
    {
        GameMap room = MapFactory.CreateWalledRoom(10, 6);

        // Not every interior cell - two of them are pillars - but the corners of the interior
        // are always open, and a room with no floor at all would be useless.
        Assert.True(room.IsWalkable(new Point(1, 1)));
        Assert.True(room.IsWalkable(new Point(room.Width - 2, room.Height - 2)));
    }

    [Fact]
    public void TheRoomContainsExactlyTwoPillars()
    {
        GameMap room = MapFactory.CreateWalledRoom(20, 10);

        int wallsInsideTheBorder = 0;

        for (int row = 1; row < room.Height - 1; row++)
        {
            for (int col = 1; col < room.Width - 1; col++)
            {
                if (!room.IsWalkable(new Point(col, row)))
                {
                    wallsInsideTheBorder++;
                }
            }
        }

        Assert.Equal(2, wallsInsideTheBorder);
    }

    [Fact]
    public void TheRoomIsTheSameEveryTime()
    {
        // Nothing here is random yet; randomness arrives with generation in Part 3.
        GameMap first = MapFactory.CreateWalledRoom(20, 10);
        GameMap second = MapFactory.CreateWalledRoom(20, 10);

        Assert.Equal(
            FrameComposer.Compose(first, Array.Empty<Entity>()).ToText(),
            FrameComposer.Compose(second, Array.Empty<Entity>()).ToText());
    }

    [Theory]
    [InlineData(2, 5)]
    [InlineData(5, 2)]
    [InlineData(0, 0)]
    public void ARoomTooSmallToHaveAnInsideIsRejected(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MapFactory.CreateWalledRoom(width, height));
    }
}
```

### [`RogueTutorial.Tests/MovementRulesTests.cs`](../parts/part-02-entities-and-the-map/RogueTutorial.Tests/MovementRulesTests.cs)

Steps onto floor land; steps into walls and off the map are refused rather than adjusted.

```csharp
/*
 * Unit tests for where a move ends up. The rule under test is the one that replaced Part 1's
 * clamping: a blocked move returns the starting cell unchanged, rather than sliding to the
 * nearest legal one.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~MovementRulesTests
 */

using System;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class MovementRulesTests
{
    // A 5x5 room of floor with a single wall at (2,2), so every direction can be tried.
    private static GameMap MapWithACentralWall()
    {
        GameMap map = new GameMap(5, 5);
        map.SetTile(new Point(2, 2), TileTypes.Wall);
        return map;
    }

    [Fact]
    public void AStepOntoFloorLands()
    {
        Point destination = MovementRules.DestinationFor(new Point(1, 1), new Point(1, 0), MapWithACentralWall());

        Assert.Equal(new Point(2, 1), destination);
    }

    [Fact]
    public void ADiagonalStepOntoFloorLands()
    {
        Point destination = MovementRules.DestinationFor(new Point(0, 0), new Point(1, 1), MapWithACentralWall());

        Assert.Equal(new Point(1, 1), destination);
    }

    [Theory]
    [InlineData(1, 2, 1, 0)]    // walking right into the wall
    [InlineData(3, 2, -1, 0)]   // walking left into it
    [InlineData(2, 1, 0, 1)]    // walking down into it
    [InlineData(2, 3, 0, -1)]   // walking up into it
    [InlineData(1, 1, 1, 1)]    // walking diagonally into it
    public void AStepIntoAWallIsRefused(int startX, int startY, int offsetX, int offsetY)
    {
        Point start = new Point(startX, startY);

        Point destination = MovementRules.DestinationFor(start, new Point(offsetX, offsetY), MapWithACentralWall());

        // The whole point of the rule: unchanged, not adjusted to a neighbouring cell.
        Assert.Equal(start, destination);
    }

    [Theory]
    [InlineData(0, 1, -1, 0)]   // off the left edge
    [InlineData(4, 1, 1, 0)]    // off the right edge
    [InlineData(1, 0, 0, -1)]   // off the top
    [InlineData(1, 4, 0, 1)]    // off the bottom
    public void AStepOffTheMapIsRefused(int startX, int startY, int offsetX, int offsetY)
    {
        Point start = new Point(startX, startY);

        Point destination = MovementRules.DestinationFor(start, new Point(offsetX, offsetY), MapWithACentralWall());

        Assert.Equal(start, destination);
    }

    [Fact]
    public void ARefusedMoveDoesNotSlideAlongTheWall()
    {
        // Part 1 clamped, which for a diagonal into a corner would have moved one axis anyway.
        // The rule now is all or nothing, so this must not become (1,2) or (2,1).
        Point start = new Point(1, 1);

        Point destination = MovementRules.DestinationFor(start, new Point(1, 1), MapWithACentralWall());

        Assert.Equal(new Point(1, 1), destination);
    }

    [Fact]
    public void AZeroOffsetStaysPut()
    {
        Point destination = MovementRules.DestinationFor(new Point(3, 3), Point.Zero, MapWithACentralWall());

        Assert.Equal(new Point(3, 3), destination);
    }

    [Fact]
    public void ANullMapIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => MovementRules.DestinationFor(Point.Zero, new Point(1, 0), null!));
    }
}
```

### [`RogueTutorial.Tests/FrameComposerTests.cs`](../parts/part-02-entities-and-the-map/RogueTutorial.Tests/FrameComposerTests.cs)

The composed picture, written as ASCII. These are the tests Part 1 could not have.

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
        Entity player = new Entity("Player", '@', Color.White, new Point(1, 1));

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
        Entity player = new Entity("Player", '@', Color.White, new Point(0, 0));
        Entity villager = new Entity("Villager", 'V', Color.Yellow, new Point(3, 1));

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
        Entity underneath = new Entity("Corpse", '%', Color.Red, new Point(0, 0));
        Entity onTop = new Entity("Player", '@', Color.White, new Point(0, 0));

        RenderedFrame frame = FrameComposer.Compose(map, new[] { underneath, onTop });

        Assert.Equal("@.", frame.ToText());
    }

    [Fact]
    public void AnEntityOffTheMapIsSkippedRatherThanThrowing()
    {
        GameMap map = new GameMap(2, 1);
        Entity stray = new Entity("Stray", 'S', Color.Green, new Point(9, 9));

        RenderedFrame frame = FrameComposer.Compose(map, new[] { stray });

        Assert.Equal("..", frame.ToText());
    }

    [Fact]
    public void TheEntityColourReachesTheFrame()
    {
        GameMap map = new GameMap(2, 1);
        Entity villager = new Entity("Villager", 'V', Color.Yellow, new Point(1, 0));

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

### [`RogueTutorial.Tests/MovementIntegrationTests.cs`](../parts/part-02-entities-and-the-map/RogueTutorial.Tests/MovementIntegrationTests.cs)

The whole chain: key press, movement rule, entity, picture. This replaces Part 1's version.

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
    // Walks an entity through a map one frame of key presses at a time, as the game loop does.
    private static Point PositionAfter(GameMap map, Point start, IEnumerable<Keys[]> framesOfKeys)
    {
        Entity walker = new Entity("Walker", '@', Color.White, start);

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
        GameMap room = MapFactory.CreateWalledRoom(9, 9);

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
        GameMap room = MapFactory.CreateWalledRoom(5, 5);
        Entity player = new Entity("Player", '@', Color.White, new Point(1, 1));

        player.MoveTo(MovementRules.DestinationFor(player.Position, MovementKeys.OffsetFor(new[] { Keys.Right }), room));

        // The frame is the end of the whole chain: key -> rule -> entity -> picture.
        // Row 2 holds both pillars: width/3 = 1 and (width*2)/3 = 3 on row height/2 = 2,
        // which on a 5-wide room leaves only the middle cell of that row open.
        Assert.Equal(
            string.Join("\n", "#####", "#.@.#", "##.##", "#...#", "#####"),
            FrameComposer.Compose(room, new[] { player }).ToText());
    }
}
```

## Step 2: delete `PlayerMover`

```
RogueTutorial/PlayerMover.cs              <- delete
RogueTutorial.Tests/PlayerMoverTests.cs   <- delete
```

It clamped, and clamping is the thing this part replaces. Leaving it would give you two answers
to "where does a move end up", and the wrong one would eventually get called.

`GridBounds` stays — `GameMap` uses it for the bounds check, so it is not dead.

## Step 3: rewrite the integration tests

`MovementIntegrationTests` referenced `PlayerMover`, so it will not compile. Almost all of the
rewrite is one helper method; the tests themselves barely change.

The finished file is
[here](../parts/part-02-entities-and-the-map/RogueTutorial.Tests/MovementIntegrationTests.cs).

**OLD — the Part 1 helper**, which drove a `PlayerMover` over a `GridBounds`. Delete it:

```csharp
// Applies a sequence of key presses one frame at a time, as the game loop would.
private static Point PositionAfter(GridBounds bounds, Point start, IEnumerable<Keys[]> framesOfKeys)
{
    PlayerMover mover = new PlayerMover(bounds, start);

    foreach (Keys[] keysThisFrame in framesOfKeys)
    {
        mover.Move(MovementKeys.OffsetFor(keysThisFrame));
    }

    return mover.Position;
}
```

**NEW — the Part 2 helper**, which drives an `Entity` over a `GameMap` and asks `MovementRules`
where each step lands. Write this in its place:

```csharp
// Walks an entity through a map one frame of key presses at a time, as the game loop does.
private static Point PositionAfter(GameMap map, Point start, IEnumerable<Keys[]> framesOfKeys)
{
    Entity walker = new Entity("Walker", '@', Color.White, start);

    foreach (Keys[] keysThisFrame in framesOfKeys)
    {
        Point offset = MovementKeys.OffsetFor(keysThisFrame);

        walker.MoveTo(MovementRules.DestinationFor(walker.Position, offset, map));
    }

    return walker.Position;
}
```

Three differences, and they are the whole part in miniature: the bounds became a map, the mover
became an entity, and the move rule moved out of the thing that holds the position.

**Then update the tests in that same file**,
[`RogueTutorial.Tests/MovementIntegrationTests.cs`](../parts/part-02-entities-and-the-map/RogueTutorial.Tests/MovementIntegrationTests.cs).
It has five tests in Part 1. Three are kept and edited, two are deleted:

| Part 1 test | What happens to it |
|---|---|
| `PressingUpMovesTowardTheTopOfTheScreen` | keep, edit |
| `FourFramesOfRightMoveFourCells` | keep, edit |
| `AKeypadCornerReachesTheSameCellAsTwoCardinals` | keep, edit |
| `HoldingAgainstTheLeftWallStopsRatherThanWrapping` | **delete** |
| `TheBottomRightCornerIsReachableAndIsTheLastCell` | **delete** |

Both deletions tested clamping — being pulled back to a legal cell at the edge of the grid.
There is no clamping any more, so there is nothing for them to assert. `AWalledRoomHoldsThePlayerIn`
below covers what you actually want to know now: the room holds you in.

For the three that stay, the map shrinks and the coordinates move with it. A 9x9 map puts every
edge within a few presses of the middle, which keeps the arithmetic in a test readable:

OLD, from Part 1:

```csharp
Point result = PositionAfter(new GridBounds(80, 25), new Point(40, 12), new[] { new[] { Keys.Up } });

Assert.Equal(new Point(40, 11), result);
```

NEW, for Part 2:

```csharp
Point result = PositionAfter(new GameMap(9, 9), new Point(4, 4), new[] { new[] { Keys.Up } });

Assert.Equal(new Point(4, 3), result);
```

The same three edits in each: `GridBounds` becomes `GameMap`, the start moves to the middle of
the smaller map, and the expected result moves with it. Per test:

| Test | Start, OLD -> NEW | Expected, OLD -> NEW |
|---|---|---|
| `PressingUpMovesTowardTheTopOfTheScreen` | `(40, 12)` -> `(4, 4)` | `(40, 11)` -> `(4, 3)` |
| `FourFramesOfRightMoveFourCells` | `(10, 10)` -> `(1, 1)` | `(14, 10)` -> `(5, 1)` |
| `AKeypadCornerReachesTheSameCellAsTwoCardinals` | `(40, 12)` -> `(4, 4)` | compares two runs, no literal to change |

What the tests *assert* does not change at all. They were always about which key means which
direction, and that did not move.

**Then add the two cases the map makes possible.** Walking into a wall:

```csharp
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
```

Three presses rather than one, because a rule that refused the first move and then let the
second through would pass a single-press test.

And being held inside the room:

```csharp
[Fact]
public void AWalledRoomHoldsThePlayerIn()
{
    GameMap room = MapFactory.CreateWalledRoom(9, 9);

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
```

`(1, 1)` is the first interior cell, since column 0 is border wall.

There is a third test worth adding, and it is the one Part 1 could not have written at all - it
runs the whole chain and checks the *picture*:

```csharp
[Fact]
public void ThePlayerAppearsWhereTheMoveLeftIt()
{
    GameMap room = MapFactory.CreateWalledRoom(5, 5);
    Entity player = new Entity("Player", '@', Color.White, new Point(1, 1));

    player.MoveTo(MovementRules.DestinationFor(player.Position, MovementKeys.OffsetFor(new[] { Keys.Right }), room));

    // Row 2 holds both pillars: width/3 = 1 and (width*2)/3 = 3 on row height/2 = 2,
    // which on a 5-wide room leaves only the middle cell of that row open.
    Assert.Equal(
        string.Join("\n", "#####", "#.@.#", "##.##", "#...#", "#####"),
        FrameComposer.Compose(room, new[] { player }).ToText());
}
```

Work that expected picture out from the rules rather than by running the code and pasting what
it printed. I got it wrong the first time by guessing where the pillars fell, and the failure
message showed the difference immediately - which is the red step doing its job.

## Step 4: rewrite `RootScreen`

**Replace the whole file.** Do not paste pieces into the Part 1 version - it still has
`PlayerMover` fields that no longer exist, and a partial paste leaves a class that references
both and compiles as neither.

[`RogueTutorial/RootScreen.cs`](../parts/part-02-entities-and-the-map/RogueTutorial/RootScreen.cs), in full:

```csharp
/*
 * The top-level screen: it wires SadConsole's window and keyboard to the game, and blits the
 * composed frame. It owns no rules. The map, the entities, where a move ends up and what the
 * picture should look like are all decided by classes that run without a graphics host.
 *
 * Usage - SadConsole constructs this itself, because Program.cs named it as the starting
 * screen, so it needs a public parameterless constructor:
 *
 *     new Builder().SetStartingScreen<RootScreen>()
 *
 * Constructing it in a test process throws: the constructor reads Game.Instance for the grid
 * size, and that requires a live graphics host. Test the rule classes instead.
 */

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

    // The dungeon floor. Fixed for this part; generated for real in Part 3.
    private readonly GameMap _map;

    // Everything drawn on top of the map, in draw order: later entries cover earlier ones.
    private readonly List<Entity> _entities;

    // The entity the keyboard drives. Also present in _entities, so it is drawn like any other.
    private readonly Entity _player;

    /// <summary>
    /// Builds the room, places the player and one villager in it, and paints the first frame.
    /// The surface is sized to the window configured in Program.cs.
    /// </summary>
    public RootScreen()
    {
        // Match the surface to the window so no part of the grid is off screen.
        _mapSurface = new ScreenSurface(Game.Instance.ScreenCellsX, Game.Instance.ScreenCellsY);

        // Children are drawn and updated by the base class once added.
        Children.Add(_mapSurface);

        _map = MapFactory.CreateWalledRoom(_mapSurface.Surface.Width, _mapSurface.Surface.Height);

        // Integer division floors, so an 80x25 room starts the player at (40, 12).
        _player = new Entity("Player", '@', Color.White, new Point(_map.Width / 2, _map.Height / 2));

        // Two cells to the left of centre, which the room's proportions keep clear of a pillar.
        Entity villager = new Entity("Villager", '@', Color.Yellow, new Point((_map.Width / 2) - 2, _map.Height / 2));

        // The player is last, so it covers anything standing on the same cell.
        _entities = new List<Entity> { villager, _player };

        DrawFrame();
    }

    /// <summary>
    /// Turns the keys held this frame into one move for the player. Returns true when a
    /// movement key was pressed, even if a wall refused the move, so the key is not offered
    /// to another screen as though nothing had happened.
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

        Point destination = MovementRules.DestinationFor(_player.Position, moveOffset, _map);

        // A wall refuses the move, and repainting an unchanged frame is wasted work.
        if (destination != _player.Position)
        {
            _player.MoveTo(destination);
            DrawFrame();
        }

        return true;
    }

    /// <summary>
    /// Composes the picture and copies it onto the surface, one cell at a time. Everything
    /// decided here was already decided by FrameComposer; this only moves it to the screen.
    /// </summary>
    private void DrawFrame()
    {
        RenderedFrame frame = FrameComposer.Compose(_map, _entities);

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

Note what is *not* in `DrawFrame`: no decision about what a wall looks like, no check on whether
a move is legal, no knowledge of which entity is the player. All of that was decided before the
method ran.

One subtlety in `ProcessKeyboard`: it returns `true` whenever a movement key was pressed, even
when a wall refused the move. The key *was* consumed - the game considered it and answered "no" -
and reporting otherwise would offer it to another screen as though nothing had happened.

### If you pasted only part of it

| Error | Meaning |
|---|---|
| `CS0246: PlayerMover could not be found` | The old fields are still there; the file was not replaced |
| `CS0103: The name '_map' does not exist` | The new `DrawFrame` was pasted into the old class |
| `CS0103: The name '_entities' does not exist` | Same cause |
| `CS1513: } expected` | The paste landed inside or across a method body |

All four mean one thing: replace the entire file rather than editing it.

## Step 5: build and run

```
dotnet build
dotnet test
dotnet run --project RogueTutorial
```

Expected: a clean build, 87 passing tests, and a room you cannot walk out of.

### If something is wrong

| Symptom | Cause |
|---|---|
| The map draws sideways, or walls are in the wrong place | A transposed index: it is `(y * Width) + x`, not `(x * Height) + y` |
| The player slides along walls instead of stopping | `DestinationFor` is adjusting the move rather than returning `start` |
| The player can walk off the edge | `IsWalkable` is not answering `false` outside the map |
| Entities are invisible | The entity loop runs before the map loop, so the map paints over them |
| `CS0246: PlayerMover could not be found` | Step 2 deleted it; something still refers to it, probably the old integration tests |

## Step 6: regenerate the documentation

Skip this if you did not set up docfx in Part 1.

**Delete the stale metadata first.** docfx writes one `.yml` per type into `api/` and never
removes the ones whose type has gone, so `PlayerMover` would keep a page in the generated site long after
it was deleted from the source. Clear the generated files before rebuilding:

```
del api\*.yml
del api\.manifest
```

`api/index.md` is yours and hand-written, so leave it. Everything else in that folder is output.

Then rebuild:

```
dotnet docfx docfx.json --serve --port 8081
```

Expected: `Build succeeded. 0 warning(s) 0 error(s)`, and pages for `Tile`, `GameMap`, `Entity`, `MovementRules`, `FrameComposer` and the rest at
<http://localhost:8081>.

The pages come from the `///` comments you wrote on each class and method - which is the reason
those comments state what a method refuses as well as what it does. A generated reference is only
worth as much as the comments behind it.

---

Next: **Part 3, dungeon generation.**

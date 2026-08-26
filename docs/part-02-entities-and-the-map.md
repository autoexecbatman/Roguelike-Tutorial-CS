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

Write the tests as you go, red first. [Writing tests](writing-tests.md) covers how.

### `RogueTutorial/Tile.cs`

One cell's appearance and its two rules.

```csharp
/*
 * One cell of the dungeon: what it looks like and what it permits.
 *
 * Usage - tiles are values, so construct them directly or take one of the standard kinds:
 *
 *     Tile wall = TileTypes.Wall;                        // '#', blocks movement and sight
 *     Tile floor = TileTypes.Floor;                      // '.', walkable and see-through
 *     Tile custom = new Tile('~', Color.Cyan, true, true);  // glyph, colour, walkable, transparent
 *
 * Being a readonly struct, a tile cannot be modified after construction; replace it in the
 * map instead. That is what stops one shared wall object from being edited by accident.
 */

using SadRogue.Primitives;

namespace RogueTutorial;

internal readonly struct Tile
{
    // The character drawn for this cell.
    public char Glyph { get; }

    // The colour that character is drawn in.
    public Color Foreground { get; }

    // True when a creature may stand here.
    public bool IsWalkable { get; }

    // True when sight passes through. Unused until field of view in Part 4.
    public bool IsTransparent { get; }

    /// <summary>
    /// Records the appearance and the two rules a tile carries. Every argument is explicit;
    /// there is no default kind of tile, because "the usual one" differs per caller.
    /// </summary>
    public Tile(char glyph, Color foreground, bool isWalkable, bool isTransparent)
    {
        Glyph = glyph;
        Foreground = foreground;
        IsWalkable = isWalkable;
        IsTransparent = isTransparent;
    }
}
```

### `RogueTutorial/TileTypes.cs`

The standard kinds, `Floor` and `Wall`.

```csharp
/*
 * The standard tile kinds, named once so a glyph or colour change happens in one place.
 *
 * Usage:
 *
 *     Tile floor = TileTypes.Floor;   // '.', dark grey, walkable, transparent
 *     Tile wall = TileTypes.Wall;     // '#', light grey, blocks movement and sight
 *
 * Add a kind here rather than constructing a Tile inline at a call site; a literal '#'
 * scattered through map generation is the thing that makes a re-theme painful later.
 */

using SadRogue.Primitives;

namespace RogueTutorial;

internal static class TileTypes
{
    /// <summary>Open ground: a creature may stand on it and see across it.</summary>
    public static Tile Floor { get; } = new Tile('.', new Color(80, 80, 80), true, true);

    /// <summary>Solid rock: blocks both movement and, from Part 4, sight.</summary>
    public static Tile Wall { get; } = new Tile('#', new Color(160, 160, 160), false, false);
}
```

### `RogueTutorial/GameMap.cs`

Tiles over the `GridBounds` you already have.

```csharp
/*
 * The dungeon floor: a rectangle of tiles, and the questions the game asks about it.
 *
 * Usage - build one, fill it, then ask what a position permits:
 *
 *     GameMap map = new GameMap(80, 25);          // every cell starts as floor
 *     map.SetTile(new Point(5, 5), TileTypes.Wall);
 *     bool blocked = map.IsWalkable(new Point(5, 5));   // -> false
 *     bool offMap = map.IsWalkable(new Point(-1, 0));   // -> false, outside is never walkable
 *     Tile tile = map.GetTile(new Point(0, 0));         // -> TileTypes.Floor
 *
 * Refuses a position outside the map in GetTile and SetTile, because reading or writing off
 * the map is a caller error. IsWalkable answers false instead, since asking whether you may
 * step off the edge is an ordinary question.
 */

using System;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class GameMap
{
    // The rectangle of legal positions; reused from Part 1.
    private readonly GridBounds _bounds;

    // Tiles in row-major order, indexed as [y * Width + x].
    private readonly Tile[] _tiles;

    /// <summary>Number of cells across.</summary>
    public int Width => _bounds.Width;

    /// <summary>Number of cells down.</summary>
    public int Height => _bounds.Height;

    /// <summary>
    /// Creates a map of the given size with every cell set to floor. Throws
    /// ArgumentOutOfRangeException when either dimension is below one.
    /// </summary>
    public GameMap(int width, int height)
    {
        _bounds = new GridBounds(width, height);

        _tiles = new Tile[width * height];

        // A map of default-constructed tiles would be unwalkable and invisible, so fill it.
        for (int index = 0; index < _tiles.Length; index++)
        {
            _tiles[index] = TileTypes.Floor;
        }
    }

    /// <summary>True when the position is a cell of this map.</summary>
    public bool IsInBounds(Point position)
    {
        return _bounds.Contains(position);
    }

    /// <summary>
    /// Returns the tile at the position. Throws ArgumentOutOfRangeException when the position
    /// is off the map; use IsInBounds first if that is a possibility.
    /// </summary>
    public Tile GetTile(Point position)
    {
        RejectPositionOffTheMap(position, nameof(position));

        return _tiles[IndexOf(position)];
    }

    /// <summary>
    /// Replaces the tile at the position. Throws ArgumentOutOfRangeException when the position
    /// is off the map, because writing outside the map is always a mistake.
    /// </summary>
    public void SetTile(Point position, Tile tile)
    {
        RejectPositionOffTheMap(position, nameof(position));

        _tiles[IndexOf(position)] = tile;
    }

    /// <summary>
    /// True when a creature may stand at the position. Anything off the map answers false
    /// rather than throwing, so movement code can ask about the cell beyond the edge.
    /// </summary>
    public bool IsWalkable(Point position)
    {
        // Outside the map is not a tile, so there is nothing to stand on.
        if (!IsInBounds(position))
        {
            return false;
        }

        return _tiles[IndexOf(position)].IsWalkable;
    }

    // Row-major index; the single place the storage layout is expressed.
    private int IndexOf(Point position)
    {
        return (position.Y * Width) + position.X;
    }

    // Shared guard for the two methods that have no sensible answer off the map.
    private void RejectPositionOffTheMap(Point position, string parameterName)
    {
        // Reading or writing outside the map is a caller error, so fail where the mistake was made.
        if (!IsInBounds(position))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                position,
                $"The position is outside the {Width}x{Height} map.");
        }
    }
}
```

### `RogueTutorial/MapFactory.cs`

The walled room with two pillars.

```csharp
/*
 * Builds the one map this part uses: a room walled all the way round, with two pillars in it.
 *
 * Real dungeon generation - rooms joined by corridors, placed at random - arrives in Part 3.
 * This exists so there is something for walls to be, and somewhere for a wall to stop you.
 *
 * Usage:
 *
 *     GameMap map = MapFactory.CreateWalledRoom(80, 25);
 *     bool edge = map.IsWalkable(new Point(0, 0));    // -> false, the border is wall
 *     bool inside = map.IsWalkable(new Point(1, 1));  // -> true, floor
 *
 * Refuses any size below 3x3, since a room smaller than that is all border and has no inside.
 */

using System;
using SadRogue.Primitives;

namespace RogueTutorial;

internal static class MapFactory
{
    /// <summary>
    /// Returns a map whose outermost cells are wall and whose interior is floor, with two
    /// pillars placed in the middle third. Throws ArgumentOutOfRangeException below 3x3, because
    /// a smaller room has no walkable interior at all.
    /// </summary>
    public static GameMap CreateWalledRoom(int width, int height)
    {
        // Below 3x3 the border consumes the whole map and there is nowhere to stand.
        if (width < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "A room needs at least 3 cells across.");
        }
        if (height < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "A room needs at least 3 cells down.");
        }

        // Starts as all floor, so only the walls have to be written.
        GameMap room = new GameMap(width, height);

        // Top and bottom rows.
        for (int col = 0; col < width; col++)
        {
            room.SetTile(new Point(col, 0), TileTypes.Wall);
            room.SetTile(new Point(col, height - 1), TileTypes.Wall);
        }

        // Left and right columns; the corners are written twice, which is harmless.
        for (int row = 0; row < height; row++)
        {
            room.SetTile(new Point(0, row), TileTypes.Wall);
            room.SetTile(new Point(width - 1, row), TileTypes.Wall);
        }

        // Two pillars, placed by proportion so they land inside a room of any size.
        room.SetTile(new Point(width / 3, height / 2), TileTypes.Wall);
        room.SetTile(new Point((width * 2) / 3, height / 2), TileTypes.Wall);

        return room;
    }
}
```

### `RogueTutorial/Entity.cs`

Name, glyph, colour, position.

```csharp
/*
 * Anything that occupies one cell and is drawn on top of the map: the player, a monster,
 * later an item lying on the floor.
 *
 * Usage:
 *
 *     Entity player = new Entity("Player", '@', Color.White, new Point(40, 12));
 *     Entity npc = new Entity("Villager", '@', Color.Yellow, new Point(42, 12));
 *     player.MoveTo(new Point(41, 12));   // unconditional; see MovementRules for the rules
 *     string who = player.Name;           // -> "Player", for messages in a later part
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
    /// Creates an entity at a starting cell. Throws ArgumentException on a blank name, since an
    /// unnamed entity would surface much later as an empty word in a message.
    /// </summary>
    public Entity(string name, char glyph, Color foreground, Point startingPosition)
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

### `RogueTutorial/MovementRules.cs`

Where a move ends up, given the map.

```csharp
/*
 * Where a move actually ends up, given the map.
 *
 * This replaces Part 1's clamping. With walls in play, a blocked move must mean staying put
 * rather than sliding to the nearest legal cell - a wall you walk into is not a suggestion to
 * step sideways.
 *
 * Usage:
 *
 *     GameMap map = new GameMap(10, 10);
 *     map.SetTile(new Point(5, 4), TileTypes.Wall);
 *
 *     Point moved = MovementRules.DestinationFor(new Point(4, 4), new Point(0, 1), map);
 *     // -> (4, 5), an ordinary step onto floor
 *
 *     Point blocked = MovementRules.DestinationFor(new Point(4, 4), new Point(1, 0), map);
 *     // -> (4, 4), unchanged, because (5, 4) is a wall
 *
 * Refuses a null map. A zero offset returns the starting position untouched.
 */

using System;
using SadRogue.Primitives;

namespace RogueTutorial;

internal static class MovementRules
{
    /// <summary>
    /// Returns the cell a move ends on. A destination that is a wall, or off the map, yields the
    /// starting position: the move is refused rather than adjusted. Throws ArgumentNullException
    /// on a null map.
    /// </summary>
    public static Point DestinationFor(Point start, Point offset, GameMap map)
    {
        // A null map is a wiring error rather than a blocked move.
        ArgumentNullException.ThrowIfNull(map);

        Point destination = start + offset;

        // IsWalkable answers false off the map too, so one question covers walls and edges.
        if (!map.IsWalkable(destination))
        {
            return start;
        }

        return destination;
    }
}
```

### `RogueTutorial/RenderedFrame.cs`

The picture, as data.

```csharp
/*
 * The picture that should be on screen, as data rather than as pixels.
 *
 * Usage - compose one with FrameComposer, then either inspect it in a test or blit it:
 *
 *     RenderedFrame frame = FrameComposer.Compose(map, new[] { player });
 *     char glyph = frame.GlyphAt(new Point(40, 12));   // -> '@'
 *     string picture = frame.ToText();                 // rows joined by newlines
 *
 * ToText is what makes drawing testable: an expected frame can be written in a test as an
 * ASCII picture and compared as a string.
 *
 * Refuses a null array, and an array whose length disagrees with the dimensions.
 */

using System;
using System.Text;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class RenderedFrame
{
    // Glyphs in row-major order, one per cell.
    private readonly char[] _glyphs;

    // Colours in row-major order, matching _glyphs cell for cell.
    private readonly Color[] _foregrounds;

    /// <summary>Number of cells across.</summary>
    public int Width { get; }

    /// <summary>Number of cells down.</summary>
    public int Height { get; }

    /// <summary>
    /// Wraps the two parallel arrays produced by FrameComposer. Throws ArgumentException when
    /// either length disagrees with the dimensions, which would mean a bug in the composer.
    /// </summary>
    public RenderedFrame(int width, int height, char[] glyphs, Color[] foregrounds)
    {
        ArgumentNullException.ThrowIfNull(glyphs);
        ArgumentNullException.ThrowIfNull(foregrounds);

        // A length mismatch is a programming error in the composer, not a runtime condition.
        if (glyphs.Length != width * height || foregrounds.Length != width * height)
        {
            throw new ArgumentException("Glyph and colour arrays must hold exactly width * height entries.");
        }

        Width = width;
        Height = height;
        _glyphs = glyphs;
        _foregrounds = foregrounds;
    }

    /// <summary>The character at the position. Throws ArgumentOutOfRangeException off the frame.</summary>
    public char GlyphAt(Point position)
    {
        RejectPositionOffTheFrame(position);

        return _glyphs[(position.Y * Width) + position.X];
    }

    /// <summary>The colour at the position. Throws ArgumentOutOfRangeException off the frame.</summary>
    public Color ForegroundAt(Point position)
    {
        RejectPositionOffTheFrame(position);

        return _foregrounds[(position.Y * Width) + position.X];
    }

    /// <summary>
    /// The whole frame as text, one line per row, joined with newlines and with no trailing
    /// newline. This is what tests compare against an expected ASCII picture.
    /// </summary>
    public string ToText()
    {
        StringBuilder text = new StringBuilder();

        for (int row = 0; row < Height; row++)
        {
            // A separator before every row but the first leaves no trailing newline.
            if (row > 0)
            {
                text.Append('\n');
            }

            text.Append(_glyphs, row * Width, Width);
        }

        return text.ToString();
    }

    // Shared guard; reading outside the frame is always a caller error.
    private void RejectPositionOffTheFrame(Point position)
    {
        if (position.X < 0 || position.X >= Width || position.Y < 0 || position.Y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, "The position is outside the frame.");
        }
    }
}
```

### `RogueTutorial/FrameComposer.cs`

What builds the picture.

```csharp
/*
 * Builds the picture that should be on screen: the map first, then entities over the top.
 *
 * Usage:
 *
 *     GameMap map = new GameMap(3, 2);
 *     Entity player = new Entity("Player", '@', Color.White, new Point(1, 1));
 *     RenderedFrame frame = FrameComposer.Compose(map, new[] { player });
 *     string picture = frame.ToText();
 *     // -> "...\n.@."
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

**Then update the tests in that same file**, `RogueTutorial.Tests/MovementIntegrationTests.cs`.
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

`RogueTutorial/RootScreen.cs`, in full:

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

---

Next: **Part 3, dungeon generation.**

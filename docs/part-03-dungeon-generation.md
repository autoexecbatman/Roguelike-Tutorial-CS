# Part 3: Dungeon generation

- [What it is](#what-it-is)
- [How to use it](#how-to-use-it)
- [How to set it up](#how-to-set-it-up)

---

# What it is

Rooms of random size, placed at random, joined by corridors, carved out of solid rock. A
different dungeon every run - and, when you want it, the same one every run.

This is the frame the game composes from seed 12345, printed by a test:

```
################################################################################
#############################################################.....##############
##############....###########################################.....##############
##############....................................................##############
##############....###########################################.....##############
##############....###########......############################.################
##############....###########......############################.################
##############....####.....##......############################.################
##############....####.....##......############################.################
##############....####.....##......############################.################
###......#######.#####........................#################.################
###......#######.#####.....#####.##########.....###########.....##.....#########
###......###.......###.....#####.##....####.....###########.....##.....#########
###..........................................................@.........#########
###......###.......#############................###########....###.....#########
###......###.......#############.##....####.....##################.....#########
############.....................##....####.....################################
############.......########################.....################################
############.......#############################################################
############.......#############################################################
################################################################################
################################################################################
################################################################################
################################################################################
################################################################################
```

Ten rooms, corridors between them, and the player on the centre of the first.

## The problem this part is really about

Everything before now was deterministic: the same input gave the same output, so a test could
state an expected answer. A generator is different. It is *supposed* to produce something new
each time, and that breaks testing in three ways at once:

- You cannot write an expected value, because there is no single right answer.
- A bug appears one run in fifty and is gone before you can look at it.
- "It worked when I ran it" means nothing.

The fix is one decision, and it shapes every class in this part: **the generator never creates
its own `Random`.** One is passed in:

```csharp
GeneratedDungeon Generate(int width, int height, Random random)
```

Give it `new Random(12345)` and you get the same dungeon forever. Give it `new Random()` and you
get a new one each run. The generator does not know or care which - it just draws numbers from
whatever it was handed.

That single change turns an untestable class into an ordinary one:

```csharp
[Fact]
public void TheSameSeedProducesTheSameDungeon()
{
    string first = FrameComposer.Compose(GenerateWithSeed(12345).Map, Array.Empty<Entity>()).ToText();
    string second = FrameComposer.Compose(GenerateWithSeed(12345).Map, Array.Empty<Entity>()).ToText();

    Assert.Equal(first, second);
}
```

Combined with Part 2's `ToText()`, a whole generated dungeon becomes a string you can compare,
print, or paste into a bug report.

**If you take one thing from this part, take this: pass randomness in, never create it inside.**
The same rule applies to monster placement in Part 5 and loot in Part 12.

## What "test a generator" actually means

You cannot assert the dungeon. You *can* assert everything that must be true of every dungeon,
which is a more useful list than it first looks:

| Property | Why it matters |
|---|---|
| The same seed repeats exactly | Without this, nothing below can be trusted |
| Different seeds differ | Catches a generator that ignores its Random |
| No two rooms overlap | Overlapping rooms make unreadable blob shapes |
| Every room lies on the map | Off-map rooms throw when carved |
| Every room interior is walkable | Catches a room placed but never carved |
| The map border is never carved | The player could otherwise walk off the world |
| **Every room is reachable from the start** | The one that matters most |

That last one earns its place. A dungeon whose rooms are carved but never joined passes every
other check and is unplayable - you spawn in a sealed box. The test floods outward from the
player's start across walkable cells, and asserts it reaches every room's centre:

```csharp
HashSet<Point> reached = WalkableCellsReachableFrom(dungeon.PlayerStart, dungeon.Map);

foreach (RectangularRoom room in dungeon.Rooms)
{
    Assert.True(reached.Contains(room.Center), ...);
}
```

Each of these runs across twenty seeds, not one. A single seed can be lucky.

## Rooms include their own walls

A `RectangularRoom` covers its wall ring. A room 5 wide occupies five columns, and only the
middle three are floor:

```
#####
#...#     Left = 0, Right = 4, Width = 5
#...#     InnerCells = the 3x3 in the middle
#...#
#####
```

Two consequences fall out of that, and both remove a whole class of bug:

**The map border is never carved, automatically.** A room flush against the edge has its *wall*
on the edge, not its floor. Nothing has to special-case the map boundary.

**Rooms that merely touch count as overlapping.** If one room's right wall is another's left
wall, the two share a column of stone one cell thick - and carving both interiors leaves them
connected with no corridor. `Intersects` is inclusive on all four edges for exactly that reason.

## Corridors bend once, in a random direction

Two rooms are joined by an L: along one axis, then the other. Which leg comes first is a coin
flip per corridor. It costs one line and stops every corner in the dungeon bending the same way.

`Corridor.Between` returns the cells and carves nothing, so it is testable on its own and the
generator decides what to do with them.

## What is deliberately wrong

**Rooms are joined only to the previous room.** That guarantees connectivity - the rooms form a
chain - but it means no loops, so the dungeon is a tree. Real roguelikes add extra connections so
there is more than one route between two points.

**Overlapping rooms are discarded, not retried.** Ask for 30 rooms and you may get 10. The
alternative, retrying until the count is met, can spin for a long time on a crowded map, and a
generator that sometimes takes a second is worse than one that sometimes makes a small dungeon.

**Everything is uniform random.** No cave systems, no themed levels, no big central chamber. Room
size and position are drawn flat from a range.

**You can still see the whole map.** Walking a dungeon you have already been shown is not
exploring. Part 4 adds field of view, and the dungeon stops being a map and starts being a place.

---

# How to use it

## Play it

```
cd parts/part-03-dungeon-generation
dotnet run --project RogueTutorial
```

A new dungeon every run. The player starts in the first room; a yellow villager stands in the
last, so there is a reason to walk the corridors.

## Play the same dungeon twice

Useful when something looks wrong and you want it again. In `RootScreen`, give `Random` a seed:

```csharp
new Random()        // a different dungeon every run
new Random(12345)   // the same dungeon every run
```

## Run the tests

```
dotnet test                                  # 115 tests
dotnet test --filter "Category!=EndToEnd"    # no window
```

| Test class | Level | Covers |
|---|---|---|
| [`RectangularRoomTests`](../parts/part-03-dungeon-generation/RogueTutorial.Tests/RectangularRoomTests.cs) | unit | edges, centre, interior, and all four sides of the overlap test |
| [`CorridorTests`](../parts/part-03-dungeon-generation/RogueTutorial.Tests/CorridorTests.cs) | unit | both bend directions, contiguity, no repeated cells |
| [`DungeonGeneratorTests`](../parts/part-03-dungeon-generation/RogueTutorial.Tests/DungeonGeneratorTests.cs) | unit | the invariant table above, across twenty seeds |

## Look at what you generated

The fastest way to judge a generator is to look at one:

```csharp
DungeonGenerator generator = new DungeonGenerator(new DungeonSettings(30, 6, 10));
GeneratedDungeon dungeon = generator.Generate(80, 25, new Random(12345));
Entity player = new Entity("Player", '@', Color.White, dungeon.PlayerStart);

File.WriteAllText("dungeon.txt", FrameComposer.Compose(dungeon.Map, new[] { player }).ToText());
```

That is how the picture at the top of this page was made. Change a number in `DungeonSettings`,
print a few seeds, and you can see what it did - far quicker than reasoning about it, and it
catches the failures no invariant describes: dungeons that are technically valid and *boring*.

## Prove the tests can fail

| Change | Expect |
|---|---|
| `DungeonGenerator`: never dig corridors (`if (false)`) | 1 fails - the reachability test |
| `DungeonGenerator`: drop the overlap check | 1 fails |
| `Corridor`: pass `skipFirst: false` on the second leg | 7 fail |
| `RectangularRoom`: any of the four `Intersects` comparisons loses its `=` | 1 fails, for each of the four |

**That last row is a fix, not a boast.** The first version of `RectangularRoomTests` checked
`first.Intersects(touching)` and not the reverse, so two of the four comparisons were never
exercised and a mutation to either survived. Intersection is symmetric; testing it one way tests
half of it. Both directions are now asserted, and a vertically-touching pair was added, which is
what finally caught all four.

The assertions can fire too. Break the placement arithmetic - `mapWidth - roomWidth + 2` instead
of `+ 1` - and the run reports:

```
DebugAssertException : Method Debug.Fail failed with
'A generated room must lie entirely on the map.'
```

---

# How to set it up

> **You are in:** your project folder, the one holding `RogueTutorial/` and `RogueTutorial.Tests/`

## Step 1: add `Fill` to `GameMap`

Generation starts from solid rock and carves out of it, so the map needs to be filled with wall
first. Add this method to [`RogueTutorial/GameMap.cs`](../parts/part-03-dungeon-generation/RogueTutorial/GameMap.cs),
next to the other tile methods. This is an addition to an existing file, not a replacement:

```csharp
    /// <summary>
    /// Sets every cell to the same tile. Generation starts by filling with wall, since carving
    /// rooms out of rock is fewer writes than walling in every space that is not a room.
    /// </summary>
    public void Fill(Tile tile)
    {
        for (int index = 0; index < _tiles.Length; index++)
        {
            _tiles[index] = tile;
        }
    }
```

The constructor still fills with floor, so nothing from Part 2 changes behaviour.

## Step 2: add the five new files

**Each block below is the complete file.** Create the file and paste the whole block.

### [`RogueTutorial/RectangularRoom.cs`](../parts/part-03-dungeon-generation/RogueTutorial/RectangularRoom.cs)

One room's geometry: where it sits, its centre, its floor, and whether it collides.

```csharp
/*
 * One rectangular room, before it is carved into a map: where it sits, how big it is, and
 * whether it collides with another.
 *
 * The rectangle includes its own wall. A room at (0,0) that is 5 wide occupies columns 0 to 4,
 * and only columns 1 to 3 are floor - the outermost ring is the wall the player sees.
 *
 * Usage:
 *
 *     RectangularRoom room = new RectangularRoom(10, 5, 7, 6);   // left, top, width, height
 *     Point middle = room.Center;                                // -> (13, 8), where the player spawns
 *     bool clash = room.Intersects(otherRoom);                   // -> true if they overlap at all
 *     foreach (Point cell in room.InnerCells) { ... }             // the floor, wall excluded
 *
 * Refuses a width or height below three, since a smaller rectangle is all wall and encloses
 * nothing.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class RectangularRoom
{
    /// <summary>Column of the room's left wall.</summary>
    public int Left { get; }

    /// <summary>Row of the room's top wall.</summary>
    public int Top { get; }

    /// <summary>Total width including both walls.</summary>
    public int Width { get; }

    /// <summary>Total height including both walls.</summary>
    public int Height { get; }

    /// <summary>Column of the room's right wall.</summary>
    public int Right => Left + Width - 1;

    /// <summary>Row of the room's bottom wall.</summary>
    public int Bottom => Top + Height - 1;

    /// <summary>
    /// The middle of the room, rounded down. Corridors are dug between centres, and the player
    /// starts on the first room's centre, so this is always floor rather than wall.
    /// </summary>
    public Point Center => new Point(Left + (Width / 2), Top + (Height / 2));

    /// <summary>
    /// Every floor cell of the room: the rectangle with its outermost ring removed. Carving a
    /// room means setting exactly these to floor and leaving the ring as wall.
    /// </summary>
    public IEnumerable<Point> InnerCells
    {
        get
        {
            for (int row = Top + 1; row < Bottom; row++)
            {
                for (int col = Left + 1; col < Right; col++)
                {
                    yield return new Point(col, row);
                }
            }
        }
    }

    /// <summary>
    /// Records a room's position and size. Throws ArgumentOutOfRangeException below 3 in either
    /// dimension, because the wall ring would then consume the whole rectangle and the room
    /// would have no floor at all.
    /// </summary>
    public RectangularRoom(int left, int top, int width, int height)
    {
        // A room without an interior is a wall, and generating one is always a caller mistake.
        if (width < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "A room needs at least 3 cells across.");
        }
        if (height < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "A room needs at least 3 cells down.");
        }

        Left = left;
        Top = top;
        Width = width;
        Height = height;

        // Corridors are dug between centres and the player spawns on one, so a centre landing
        // on the wall ring would put both in solid rock.
        Debug.Assert(
            Center.X > Left && Center.X < Right && Center.Y > Top && Center.Y < Bottom,
            "A room's centre must lie inside its walls.");
    }

    /// <summary>
    /// True when this room shares any cell with the other, walls included. Rooms that merely
    /// touch along a wall count as intersecting: sharing a wall would let the player pass
    /// between them without a corridor.
    /// </summary>
    public bool Intersects(RectangularRoom other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // Standard rectangle overlap, inclusive on all four edges so shared walls count.
        return Left <= other.Right
            && Right >= other.Left
            && Top <= other.Bottom
            && Bottom >= other.Top;
    }
}
```

### [`RogueTutorial/Corridor.cs`](../parts/part-03-dungeon-generation/RogueTutorial/Corridor.cs)

The L-shaped path between two points.

```csharp
/*
 * The L-shaped path between two room centres.
 *
 * A corridor runs along one axis and then the other, turning once. Which axis comes first is
 * the caller's choice, and the generator makes it at random so a dungeon does not have every
 * corner bending the same way.
 *
 * Usage:
 *
 *     // horizontal leg first: across to x=5, then down to y=3
 *     IEnumerable<Point> path = Corridor.Between(new Point(1, 1), new Point(5, 3), true);
 *     // -> (1,1) (2,1) (3,1) (4,1) (5,1) (5,2) (5,3)
 *
 *     // vertical leg first: down to y=3, then across to x=5
 *     IEnumerable<Point> other = Corridor.Between(new Point(1, 1), new Point(5, 3), false);
 *     // -> (1,1) (1,2) (1,3) (2,3) (3,3) (4,3) (5,3)
 *
 * Both endpoints are included. Two identical endpoints yield that single cell. The path never
 * repeats a cell, so carving it is one pass with no duplicated work.
 */

using System.Collections.Generic;
using SadRogue.Primitives;

namespace RogueTutorial;

internal static class Corridor
{
    /// <summary>
    /// Returns every cell of the L-shaped path from start to end, both endpoints included.
    /// When horizontalFirst is true the path moves along x before y, otherwise y before x.
    /// The corner cell is yielded once, not twice.
    /// </summary>
    public static IEnumerable<Point> Between(Point start, Point end, bool horizontalFirst)
    {
        // The bend: the cell where the path stops going one way and starts going the other.
        Point corner = horizontalFirst
            ? new Point(end.X, start.Y)
            : new Point(start.X, end.Y);

        // First leg includes the corner; second leg skips it so it is not yielded twice.
        foreach (Point cell in StraightLine(start, corner, includeEnd: true))
        {
            yield return cell;
        }

        foreach (Point cell in StraightLine(corner, end, includeEnd: true, skipFirst: true))
        {
            yield return cell;
        }
    }

    // Walks a horizontal or vertical run of cells. One of the two axes must already match.
    private static IEnumerable<Point> StraightLine(Point from, Point to, bool includeEnd, bool skipFirst = false)
    {
        // Step is -1, 0 or +1 on each axis, so one loop covers both directions.
        int stepX = System.Math.Sign(to.X - from.X);
        int stepY = System.Math.Sign(to.Y - from.Y);

        int length = System.Math.Max(System.Math.Abs(to.X - from.X), System.Math.Abs(to.Y - from.Y));

        // Starting at 1 rather than 0 is what drops the shared corner on the second leg.
        int firstStep = skipFirst ? 1 : 0;

        for (int step = firstStep; step <= length; step++)
        {
            if (step == length && !includeEnd)
            {
                yield break;
            }

            yield return new Point(from.X + (stepX * step), from.Y + (stepY * step));
        }
    }
}
```

### [`RogueTutorial/DungeonSettings.cs`](../parts/part-03-dungeon-generation/RogueTutorial/DungeonSettings.cs)

The numbers that shape a dungeon, gathered in one place.

```csharp
/*
 * The numbers that shape a generated dungeon, gathered in one place.
 *
 * These live here rather than as literals inside the generator so that a run can be described
 * by its settings and its seed, and so that changing "how big are the rooms" is one edit in an
 * obvious place rather than a hunt through generation code.
 *
 * Usage:
 *
 *     DungeonSettings settings = new DungeonSettings(
 *         maximumRooms: 30,       // attempts, not a guarantee - see below
 *         minimumRoomSize: 6,     // total width or height, walls included
 *         maximumRoomSize: 10);
 *
 * maximumRooms is a number of attempts. A room that would overlap an existing one is discarded
 * rather than retried, so a dungeon usually holds fewer rooms than this, and that is by design:
 * retrying until the count is met makes generation take unbounded time on a crowded map.
 *
 * Refuses a room count below one, a minimum size below three, and a maximum below the minimum.
 */

using System;

namespace RogueTutorial;

internal sealed class DungeonSettings
{
    /// <summary>How many rooms to attempt. Fewer may be placed; overlaps are discarded.</summary>
    public int MaximumRooms { get; }

    /// <summary>Smallest total width or height a room may have, its walls included.</summary>
    public int MinimumRoomSize { get; }

    /// <summary>Largest total width or height a room may have, its walls included.</summary>
    public int MaximumRoomSize { get; }

    /// <summary>
    /// Records the generation parameters. Throws ArgumentOutOfRangeException when a room count
    /// is below one, when the minimum size is below three - the size at which a room has no
    /// floor inside its walls - or when the maximum is smaller than the minimum.
    /// </summary>
    public DungeonSettings(int maximumRooms, int minimumRoomSize, int maximumRoomSize)
    {
        // A dungeon with no rooms has nowhere to put the player.
        if (maximumRooms < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRooms), maximumRooms, "A dungeon needs at least one room.");
        }

        // Below 3 the wall ring consumes the whole rectangle; RectangularRoom rejects it too.
        if (minimumRoomSize < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumRoomSize), minimumRoomSize, "A room needs at least 3 cells across and down.");
        }

        // An inverted range would make the random size call throw much later and less clearly.
        if (maximumRoomSize < minimumRoomSize)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRoomSize), maximumRoomSize, "The maximum room size cannot be below the minimum.");
        }

        MaximumRooms = maximumRooms;
        MinimumRoomSize = minimumRoomSize;
        MaximumRoomSize = maximumRoomSize;
    }
}
```

### [`RogueTutorial/GeneratedDungeon.cs`](../parts/part-03-dungeon-generation/RogueTutorial/GeneratedDungeon.cs)

What a run produced: the map, the rooms, and where the player starts.

```csharp
/*
 * What a generation run produced: the carved map, the rooms it placed, and where the player
 * starts.
 *
 * The rooms are kept rather than discarded because later parts need them - monsters are placed
 * per room in Part 5, and the stairs down go in the last room in Part 12. Recovering them from
 * the finished map afterwards would mean detecting rectangles in a bitmap.
 *
 * Usage:
 *
 *     GeneratedDungeon dungeon = generator.Generate(80, 43, new Random(12345));
 *     GameMap map = dungeon.Map;                  // hand this to FrameComposer
 *     Point spawn = dungeon.PlayerStart;          // centre of the first room
 *     int placed = dungeon.Rooms.Count;           // how many rooms survived overlap rejection
 *
 * Refuses a null map and an empty room list: a dungeon with no rooms has nowhere to put the
 * player, and constructing one is a generator bug rather than a legal outcome.
 */

using System;
using System.Collections.Generic;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class GeneratedDungeon
{
    /// <summary>The carved map: rock, with rooms and corridors cut into it.</summary>
    public GameMap Map { get; }

    /// <summary>Every room placed, in the order they were generated.</summary>
    public IReadOnlyList<RectangularRoom> Rooms { get; }

    /// <summary>Where the player begins: the centre of the first room.</summary>
    public Point PlayerStart => Rooms[0].Center;

    /// <summary>
    /// Wraps the result of one generation run. Throws ArgumentNullException on a null argument
    /// and ArgumentException on an empty room list, because PlayerStart would then have no
    /// answer and the failure would surface far from its cause.
    /// </summary>
    public GeneratedDungeon(GameMap map, IReadOnlyList<RectangularRoom> rooms)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(rooms);

        // A generator that placed nothing has failed; saying so here beats an index error later.
        if (rooms.Count == 0)
        {
            throw new ArgumentException("A dungeon must contain at least one room.", nameof(rooms));
        }

        Map = map;
        Rooms = rooms;
    }
}
```

### [`RogueTutorial/DungeonGenerator.cs`](../parts/part-03-dungeon-generation/RogueTutorial/DungeonGenerator.cs)

The generator itself.

```csharp
/*
 * Builds a dungeon: solid rock, with rooms carved out of it and corridors joining them.
 *
 * The random number generator is passed in and never created here. That is the whole reason
 * this class is testable: the same seed always produces the same dungeon, so a failure can be
 * reproduced, and a test can assert an entire generated map as an ASCII picture.
 *
 * Usage:
 *
 *     DungeonSettings settings = new DungeonSettings(30, 6, 10);
 *     Random random = new Random(12345);                  // any seed; the same one repeats the dungeon
 *     DungeonGenerator generator = new DungeonGenerator(settings);
 *
 *     GeneratedDungeon dungeon = generator.Generate(80, 43, random);
 *     GameMap map = dungeon.Map;                          // rooms and corridors carved into rock
 *     Point spawn = dungeon.PlayerStart;                  // the centre of the first room
 *
 * Refuses a null settings object or a null Random, and a map too small to hold one room.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class DungeonGenerator
{
    // The numbers shaping every dungeon this generator makes.
    private readonly DungeonSettings _settings;

    /// <summary>
    /// Records the settings to generate with. Throws ArgumentNullException on a null settings
    /// object, since there is no sensible default set of room sizes.
    /// </summary>
    public DungeonGenerator(DungeonSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
    }

    /// <summary>
    /// Generates a dungeon of the given size, drawing every random choice from the supplied
    /// Random. Rooms that would touch or overlap an existing room are discarded, so the result
    /// usually holds fewer rooms than the settings ask for. Throws ArgumentNullException on a
    /// null Random, and ArgumentOutOfRangeException when the map cannot fit one smallest room.
    /// </summary>
    public GeneratedDungeon Generate(int width, int height, Random random)
    {
        // The Random is the caller's, so the same seed reproduces the same dungeon exactly.
        ArgumentNullException.ThrowIfNull(random);

        // A map that cannot hold the smallest allowed room can never produce a dungeon, and
        // discovering that after twenty failed attempts would report the wrong problem.
        if (width < _settings.MinimumRoomSize || height < _settings.MinimumRoomSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                $"{width}x{height}",
                $"The map is too small to hold a {_settings.MinimumRoomSize}-cell room.");
        }

        GameMap map = new GameMap(width, height);

        // Everything starts as solid rock; rooms and corridors are then carved out of it.
        map.Fill(TileTypes.Wall);

        List<RectangularRoom> placedRooms = new List<RectangularRoom>();

        for (int attempt = 0; attempt < _settings.MaximumRooms; attempt++)
        {
            RectangularRoom candidate = RandomRoom(width, height, random);

            // Discarded rather than retried: retrying until the count is met can take unbounded
            // time on a crowded map, so a dungeon simply ends up with fewer rooms.
            if (placedRooms.Any(existing => existing.Intersects(candidate)))
            {
                continue;
            }

            // Placement arithmetic is the easiest thing here to get wrong by one, and a room
            // hanging off the map would throw much later inside SetTile.
            Debug.Assert(
                candidate.Left >= 0 && candidate.Top >= 0
                    && candidate.Right < width && candidate.Bottom < height,
                "A generated room must lie entirely on the map.");

            Carve(map, candidate);

            // Every room after the first is joined to the one before it, which is what makes
            // the whole dungeon reachable rather than a set of sealed boxes.
            if (placedRooms.Count > 0)
            {
                DigCorridor(map, placedRooms[placedRooms.Count - 1].Center, candidate.Center, random);
            }

            placedRooms.Add(candidate);
        }

        // The border stays rock because every room carries its own wall ring; if that ever
        // stops being true the player can walk off the map and nothing else would report it.
        Debug.Assert(BorderIsUncarved(map), "Generation must never carve the edge of the map.");

        return new GeneratedDungeon(map, placedRooms);
    }

    // True when no cell of the map's outermost ring is walkable.
    private static bool BorderIsUncarved(GameMap map)
    {
        for (int col = 0; col < map.Width; col++)
        {
            if (map.IsWalkable(new Point(col, 0)) || map.IsWalkable(new Point(col, map.Height - 1)))
            {
                return false;
            }
        }

        for (int row = 0; row < map.Height; row++)
        {
            if (map.IsWalkable(new Point(0, row)) || map.IsWalkable(new Point(map.Width - 1, row)))
            {
                return false;
            }
        }

        return true;
    }

    // Picks a room of a random allowed size at a random position that fits on the map.
    private RectangularRoom RandomRoom(int mapWidth, int mapHeight, Random random)
    {
        // Next's upper bound is exclusive, so + 1 makes MaximumRoomSize reachable.
        int roomWidth = random.Next(_settings.MinimumRoomSize, _settings.MaximumRoomSize + 1);
        int roomHeight = random.Next(_settings.MinimumRoomSize, _settings.MaximumRoomSize + 1);

        // A room larger than the map in one dimension is clamped rather than rejected, so a
        // narrow map still generates instead of discarding every attempt.
        roomWidth = Math.Min(roomWidth, mapWidth);
        roomHeight = Math.Min(roomHeight, mapHeight);

        // The room's own wall ring keeps the map border uncarved, so a room may sit flush
        // against the edge; the largest legal left is the one that puts its right wall last.
        int left = random.Next(0, mapWidth - roomWidth + 1);
        int top = random.Next(0, mapHeight - roomHeight + 1);

        return new RectangularRoom(left, top, roomWidth, roomHeight);
    }

    // Sets a room's interior to floor, leaving its wall ring as rock.
    private static void Carve(GameMap map, RectangularRoom room)
    {
        foreach (Point cell in room.InnerCells)
        {
            map.SetTile(cell, TileTypes.Floor);
        }
    }

    // Cuts an L-shaped corridor between two room centres, bending whichever way the roll says.
    private static void DigCorridor(GameMap map, Point from, Point to, Random random)
    {
        // Alternating the bend keeps a dungeon from having every corner the same shape.
        bool horizontalFirst = random.Next(2) == 0;

        foreach (Point cell in Corridor.Between(from, to, horizontalFirst))
        {
            map.SetTile(cell, TileTypes.Floor);
        }
    }
}
```

### The test files

**Each block below is the complete file.** Create it in `RogueTutorial.Tests/` and paste the
whole thing.

`MapFactoryTests.cs` is deleted in the next step. Everything else carries over from Part 2.

### [`RogueTutorial.Tests/RectangularRoomTests.cs`](../parts/part-03-dungeon-generation/RogueTutorial.Tests/RectangularRoomTests.cs)

The inclusive edges, the centre, the interior, and all four sides of the overlap test in both directions.

```csharp
/*
 * Unit tests for one room's geometry. Expected values are worked out from the definition: the
 * rectangle includes its wall ring, so a room 5 wide has 3 columns of floor.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~RectangularRoomTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class RectangularRoomTests
{
    [Fact]
    public void TheEdgesAreTheRectangleInclusive()
    {
        RectangularRoom room = new RectangularRoom(10, 5, 7, 6);

        Assert.Equal(10, room.Left);
        Assert.Equal(5, room.Top);

        // 10 + 7 - 1: a 7-wide room starting at column 10 ends at column 16, not 17.
        Assert.Equal(16, room.Right);
        Assert.Equal(10, room.Bottom);
    }

    [Fact]
    public void TheCentreIsRoundedDown()
    {
        RectangularRoom room = new RectangularRoom(10, 5, 7, 6);

        // 10 + 7/2 = 13, and 5 + 6/2 = 8; integer division floors both.
        Assert.Equal(new Point(13, 8), room.Center);
    }

    [Fact]
    public void TheCentreIsAlwaysInsideTheWalls()
    {
        // The player spawns on a centre and corridors are dug between them, so a centre landing
        // on the wall ring would be a real bug. Smallest room is where it would show up first.
        RectangularRoom smallest = new RectangularRoom(0, 0, 3, 3);

        Assert.Equal(new Point(1, 1), smallest.Center);
        Assert.Contains(smallest.Center, smallest.InnerCells);
    }

    [Fact]
    public void TheInteriorExcludesTheWallRing()
    {
        RectangularRoom room = new RectangularRoom(0, 0, 5, 4);

        List<Point> interior = room.InnerCells.ToList();

        // 5x4 total, so 3x2 of floor once the ring is removed.
        Assert.Equal(6, interior.Count);
        Assert.All(interior, cell => Assert.True(cell.X >= 1 && cell.X <= 3 && cell.Y >= 1 && cell.Y <= 2));
    }

    [Fact]
    public void TheSmallestRoomHasExactlyOneFloorCell()
    {
        RectangularRoom smallest = new RectangularRoom(4, 4, 3, 3);

        Assert.Equal(new[] { new Point(5, 5) }, smallest.InnerCells.ToArray());
    }

    [Fact]
    public void OverlappingRoomsIntersect()
    {
        RectangularRoom first = new RectangularRoom(0, 0, 5, 5);
        RectangularRoom second = new RectangularRoom(3, 3, 5, 5);

        Assert.True(first.Intersects(second));
        Assert.True(second.Intersects(first));
    }

    [Fact]
    public void RoomsSharingOnlyAWallStillIntersect()
    {
        // First occupies columns 0-4, second starts at 4. Sharing that wall would let the
        // player walk between the rooms with no corridor, so this must count as a collision.
        RectangularRoom first = new RectangularRoom(0, 0, 5, 5);
        RectangularRoom touching = new RectangularRoom(4, 0, 5, 5);

        // Both directions: intersection is symmetric, and testing one way leaves half the
        // comparison unexercised - a mutation to the other half survived until this was added.
        Assert.True(first.Intersects(touching));
        Assert.True(touching.Intersects(first));
    }

    [Fact]
    public void RoomsSharingOnlyAHorizontalWallStillIntersect()
    {
        // The vertical twin of the test above: first occupies rows 0-4, below starts at row 4.
        // Without this, a mutation to the top-versus-bottom half of the comparison survives.
        RectangularRoom first = new RectangularRoom(0, 0, 5, 5);
        RectangularRoom below = new RectangularRoom(0, 4, 5, 5);

        Assert.True(first.Intersects(below));
        Assert.True(below.Intersects(first));
    }

    [Fact]
    public void RoomsOneCellApartDoNotIntersect()
    {
        // First ends at column 4, second starts at 5: one column of rock between them.
        RectangularRoom first = new RectangularRoom(0, 0, 5, 5);
        RectangularRoom clear = new RectangularRoom(5, 0, 5, 5);

        Assert.False(first.Intersects(clear));
        Assert.False(clear.Intersects(first));
    }

    [Fact]
    public void RoomsSeparatedOnlyVerticallyDoNotIntersect()
    {
        RectangularRoom first = new RectangularRoom(0, 0, 5, 5);
        RectangularRoom below = new RectangularRoom(0, 5, 5, 5);

        Assert.False(first.Intersects(below));
        Assert.False(below.Intersects(first));
    }

    [Theory]
    [InlineData(2, 5)]
    [InlineData(5, 2)]
    public void ARoomWithNoInteriorIsRejected(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RectangularRoom(0, 0, width, height));
    }

    [Fact]
    public void ANullComparisonIsRejected()
    {
        RectangularRoom room = new RectangularRoom(0, 0, 5, 5);

        Assert.Throws<ArgumentNullException>(() => room.Intersects(null!));
    }
}
```

### [`RogueTutorial.Tests/CorridorTests.cs`](../parts/part-03-dungeon-generation/RogueTutorial.Tests/CorridorTests.cs)

Both bend directions, every step adjacent, and no cell repeated at the corner.

```csharp
/*
 * Unit tests for the L-shaped path between two points. Expected paths are written out cell by
 * cell from the definition rather than taken from what the code returned.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~CorridorTests
 */

using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class CorridorTests
{
    [Fact]
    public void AHorizontalFirstPathTurnsOnce()
    {
        Point[] path = Corridor.Between(new Point(1, 1), new Point(4, 3), horizontalFirst: true).ToArray();

        Assert.Equal(
            new[]
            {
                new Point(1, 1), new Point(2, 1), new Point(3, 1), new Point(4, 1),
                new Point(4, 2), new Point(4, 3),
            },
            path);
    }

    [Fact]
    public void AVerticalFirstPathTurnsTheOtherWay()
    {
        Point[] path = Corridor.Between(new Point(1, 1), new Point(4, 3), horizontalFirst: false).ToArray();

        Assert.Equal(
            new[]
            {
                new Point(1, 1), new Point(1, 2), new Point(1, 3),
                new Point(2, 3), new Point(3, 3), new Point(4, 3),
            },
            path);
    }

    [Fact]
    public void BothEndpointsAreIncluded()
    {
        Point[] path = Corridor.Between(new Point(2, 7), new Point(9, 1), horizontalFirst: true).ToArray();

        Assert.Equal(new Point(2, 7), path.First());
        Assert.Equal(new Point(9, 1), path.Last());
    }

    [Fact]
    public void ThePathVisitsNoCellTwice()
    {
        // The corner belongs to both legs, so an off-by-one there would duplicate it.
        Point[] path = Corridor.Between(new Point(0, 0), new Point(5, 5), horizontalFirst: true).ToArray();

        Assert.Equal(path.Length, path.Distinct().Count());
    }

    [Fact]
    public void EveryStepIsToAnAdjacentCell()
    {
        // A gap in the path would carve a corridor the player cannot walk down.
        Point[] path = Corridor.Between(new Point(3, 9), new Point(11, 2), horizontalFirst: false).ToArray();

        for (int step = 1; step < path.Length; step++)
        {
            int distance = System.Math.Abs(path[step].X - path[step - 1].X)
                + System.Math.Abs(path[step].Y - path[step - 1].Y);

            Assert.Equal(1, distance);
        }
    }

    [Fact]
    public void APathBackwardsWorksTheSame()
    {
        // Rooms are joined in generation order, which does not sort the coordinates first.
        Point[] path = Corridor.Between(new Point(5, 5), new Point(2, 2), horizontalFirst: true).ToArray();

        Assert.Equal(
            new[]
            {
                new Point(5, 5), new Point(4, 5), new Point(3, 5), new Point(2, 5),
                new Point(2, 4), new Point(2, 3), new Point(2, 2),
            },
            path);
    }

    [Fact]
    public void AStraightPathHasNoCorner()
    {
        // Two centres can share a row; the vertical leg is then empty and must add nothing.
        Point[] path = Corridor.Between(new Point(1, 4), new Point(4, 4), horizontalFirst: true).ToArray();

        Assert.Equal(
            new[] { new Point(1, 4), new Point(2, 4), new Point(3, 4), new Point(4, 4) },
            path);
    }

    [Fact]
    public void IdenticalEndpointsYieldOneCell()
    {
        Point[] path = Corridor.Between(new Point(6, 6), new Point(6, 6), horizontalFirst: true).ToArray();

        Assert.Equal(new[] { new Point(6, 6) }, path);
    }

    [Fact]
    public void BothOrdersReachTheSameEndpoints()
    {
        Point[] horizontal = Corridor.Between(new Point(2, 2), new Point(8, 6), horizontalFirst: true).ToArray();
        Point[] vertical = Corridor.Between(new Point(2, 2), new Point(8, 6), horizontalFirst: false).ToArray();

        Assert.Equal(horizontal.First(), vertical.First());
        Assert.Equal(horizontal.Last(), vertical.Last());

        // Same length, different route: an L either way covers the same number of cells.
        Assert.Equal(horizontal.Length, vertical.Length);
        Assert.NotEqual(horizontal, vertical);
    }
}
```

### [`RogueTutorial.Tests/DungeonGeneratorTests.cs`](../parts/part-03-dungeon-generation/RogueTutorial.Tests/DungeonGeneratorTests.cs)

The invariant table above, across twenty seeds. Reachability is the one that matters most.

```csharp
/*
 * Unit tests for dungeon generation. Every one of these passes a Random with a fixed seed, so
 * "random" here means "arbitrary but repeatable" - the property being tested is never left to
 * chance.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~DungeonGeneratorTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class DungeonGeneratorTests
{
    // The settings most tests use: enough attempts to fill a small map, rooms of a size that
    // fits several times over.
    private static DungeonSettings StandardSettings()
    {
        return new DungeonSettings(maximumRooms: 20, minimumRoomSize: 5, maximumRoomSize: 9);
    }

    private static GeneratedDungeon GenerateWithSeed(int seed)
    {
        return new DungeonGenerator(StandardSettings()).Generate(40, 25, new Random(seed));
    }

    [Fact]
    public void TheSameSeedProducesTheSameDungeon()
    {
        // This is the property the whole design exists to provide. Without it a bad dungeon
        // cannot be reproduced, and none of the tests below could assert anything at all.
        string first = FrameComposer.Compose(GenerateWithSeed(12345).Map, Array.Empty<Entity>()).ToText();
        string second = FrameComposer.Compose(GenerateWithSeed(12345).Map, Array.Empty<Entity>()).ToText();

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentSeedsProduceDifferentDungeons()
    {
        string first = FrameComposer.Compose(GenerateWithSeed(1).Map, Array.Empty<Entity>()).ToText();
        string second = FrameComposer.Compose(GenerateWithSeed(2).Map, Array.Empty<Entity>()).ToText();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void TheDungeonIsTheSizeAsked()
    {
        GeneratedDungeon dungeon = GenerateWithSeed(7);

        Assert.Equal(40, dungeon.Map.Width);
        Assert.Equal(25, dungeon.Map.Height);
    }

    [Fact]
    public void AtLeastOneRoomIsPlaced()
    {
        GeneratedDungeon dungeon = GenerateWithSeed(7);

        Assert.NotEmpty(dungeon.Rooms);
    }

    [Fact]
    public void NoTwoRoomsOverlap()
    {
        // Checked across several seeds: a single seed could be lucky and hide a real collision.
        for (int seed = 0; seed < 20; seed++)
        {
            IReadOnlyList<RectangularRoom> rooms = GenerateWithSeed(seed).Rooms;

            for (int first = 0; first < rooms.Count; first++)
            {
                for (int second = first + 1; second < rooms.Count; second++)
                {
                    Assert.False(
                        rooms[first].Intersects(rooms[second]),
                        $"seed {seed}: rooms {first} and {second} overlap");
                }
            }
        }
    }

    [Fact]
    public void EveryRoomFitsInsideTheMap()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            GeneratedDungeon dungeon = GenerateWithSeed(seed);

            foreach (RectangularRoom room in dungeon.Rooms)
            {
                Assert.True(room.Left >= 0, $"seed {seed}: room starts left of the map");
                Assert.True(room.Top >= 0, $"seed {seed}: room starts above the map");
                Assert.True(room.Right < dungeon.Map.Width, $"seed {seed}: room runs off the right");
                Assert.True(room.Bottom < dungeon.Map.Height, $"seed {seed}: room runs off the bottom");
            }
        }
    }

    [Fact]
    public void EveryRoomInteriorIsWalkable()
    {
        GeneratedDungeon dungeon = GenerateWithSeed(99);

        foreach (RectangularRoom room in dungeon.Rooms)
        {
            foreach (Point cell in room.InnerCells)
            {
                Assert.True(dungeon.Map.IsWalkable(cell), $"room interior at {cell} was not carved");
            }
        }
    }

    [Fact]
    public void ThePlayerStartsInsideTheFirstRoom()
    {
        GeneratedDungeon dungeon = GenerateWithSeed(42);

        Assert.Equal(dungeon.Rooms[0].Center, dungeon.PlayerStart);
        Assert.True(dungeon.Map.IsWalkable(dungeon.PlayerStart));
    }

    [Fact]
    public void EveryRoomIsReachableFromTheStart()
    {
        // The point of corridors. A dungeon whose rooms are carved but not joined passes every
        // other test here and is unplayable, so this walks the floor and checks it is one piece.
        for (int seed = 0; seed < 20; seed++)
        {
            GeneratedDungeon dungeon = GenerateWithSeed(seed);

            HashSet<Point> reached = WalkableCellsReachableFrom(dungeon.PlayerStart, dungeon.Map);

            foreach (RectangularRoom room in dungeon.Rooms)
            {
                Assert.True(
                    reached.Contains(room.Center),
                    $"seed {seed}: a room centre at {room.Center} cannot be reached from the start");
            }
        }
    }

    [Fact]
    public void TheEdgeOfTheMapIsNeverCarved()
    {
        // A room or corridor touching the border would let the player walk off the map, and
        // MovementRules would silently refuse the move rather than reporting a generation bug.
        for (int seed = 0; seed < 20; seed++)
        {
            GameMap map = GenerateWithSeed(seed).Map;

            for (int col = 0; col < map.Width; col++)
            {
                Assert.False(map.IsWalkable(new Point(col, 0)), $"seed {seed}: top edge carved at x={col}");
                Assert.False(map.IsWalkable(new Point(col, map.Height - 1)), $"seed {seed}: bottom edge carved at x={col}");
            }

            for (int row = 0; row < map.Height; row++)
            {
                Assert.False(map.IsWalkable(new Point(0, row)), $"seed {seed}: left edge carved at y={row}");
                Assert.False(map.IsWalkable(new Point(map.Width - 1, row)), $"seed {seed}: right edge carved at y={row}");
            }
        }
    }

    [Fact]
    public void MoreAttemptsPlaceNoFewerRooms()
    {
        // Not "more rooms": overlap rejection means a bigger attempt count can tie. It must
        // never place fewer, which is what a bug in the attempt loop would look like.
        int few = new DungeonGenerator(new DungeonSettings(3, 5, 9)).Generate(40, 25, new Random(5)).Rooms.Count;
        int many = new DungeonGenerator(new DungeonSettings(30, 5, 9)).Generate(40, 25, new Random(5)).Rooms.Count;

        Assert.True(many >= few, $"30 attempts placed {many} rooms, 3 attempts placed {few}");
    }

    [Fact]
    public void ANullRandomIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new DungeonGenerator(StandardSettings()).Generate(40, 25, null!));
    }

    [Fact]
    public void ANullSettingsObjectIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new DungeonGenerator(null!));
    }

    [Fact]
    public void AMapTooSmallForOneRoomIsRejected()
    {
        // Smallest room here is 5x5, so a 4x4 map cannot hold one however lucky the rolls are.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DungeonGenerator(StandardSettings()).Generate(4, 4, new Random(1)));
    }

    // Flood fill across walkable cells, four-directional because corridors never move diagonally.
    private static HashSet<Point> WalkableCellsReachableFrom(Point start, GameMap map)
    {
        HashSet<Point> reached = new HashSet<Point> { start };
        Queue<Point> toVisit = new Queue<Point>();
        toVisit.Enqueue(start);

        Point[] steps = { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) };

        while (toVisit.Count > 0)
        {
            Point cell = toVisit.Dequeue();

            foreach (Point step in steps)
            {
                Point neighbour = cell + step;

                // IsWalkable answers false off the map, so no bounds check is needed here.
                if (map.IsWalkable(neighbour) && reached.Add(neighbour))
                {
                    toVisit.Enqueue(neighbour);
                }
            }
        }

        return reached;
    }
}
```

### [`RogueTutorial.Tests/MovementIntegrationTests.cs`](../parts/part-03-dungeon-generation/RogueTutorial.Tests/MovementIntegrationTests.cs)

Updated for Part 3: the hand-built room replaces MapFactory.

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
        Entity player = new Entity("Player", '@', Color.White, new Point(1, 1));

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

## Step 2b: retitle the window

One line in `RogueTutorial/Program.cs`, so the window says which part you are running:

```csharp
const string WindowTitle = "Roguelike Tutorial - Part 3: Dungeon generation";
```

Nothing else in that file changes.

## Step 3: delete `MapFactory`

```
RogueTutorial/MapFactory.cs              <- delete
RogueTutorial.Tests/MapFactoryTests.cs   <- delete
```

It built the one hardcoded room Part 2 used, and the generator replaces it.

`MovementIntegrationTests` used it in two places, so it will not compile until you replace it
with the version given above, which builds its room in the test file itself.

## Step 4: rewrite `RootScreen`

**Replace the whole file.** [`RogueTutorial/RootScreen.cs`](../parts/part-03-dungeon-generation/RogueTutorial/RootScreen.cs), in full:

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

        // No seed is given, so every run generates a different dungeon. Pass a number to
        // Random's constructor to play the same one repeatedly while debugging.
        DungeonGenerator generator = new DungeonGenerator(new DungeonSettings(
            maximumRooms: 30,
            minimumRoomSize: 6,
            maximumRoomSize: 10));

        GeneratedDungeon dungeon = generator.Generate(
            _mapSurface.Surface.Width, _mapSurface.Surface.Height, new Random());

        _map = dungeon.Map;

        // The generator decides where the player starts: the centre of the first room it placed.
        _player = new Entity("Player", '@', Color.White, dungeon.PlayerStart);

        // A villager in the last room, so there is a reason to walk the corridors.
        Entity villager = new Entity(
            "Villager", '@', Color.Yellow, dungeon.Rooms[dungeon.Rooms.Count - 1].Center);

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

Only the constructor really changed: it asks the generator for a dungeon instead of building one
room, and takes the player's start from it. `ProcessKeyboard` and `DrawFrame` are untouched -
they were already written against a `GameMap`, and a generated map is just a `GameMap`.

## Step 5: build and run

```
dotnet build
dotnet test
dotnet run --project RogueTutorial
```

Expected: a clean build, 115 passing tests, and a different dungeon each time you run it.

### If something is wrong

| Symptom | Cause |
|---|---|
| Every run gives the same dungeon | `new Random(seed)` with a fixed seed in `RootScreen` |
| `CS0103: MapFactory does not exist` | Step 3 deleted it; the integration tests still refer to it |
| Rooms appear but you cannot leave the first one | Corridors are not dug, or are dug before the room is carved |
| `ArgumentOutOfRangeException` from `SetTile` | A room was placed partly off the map |
| The player starts inside a wall | The player is being placed somewhere other than a room centre |
| A dungeon with one room, always | The overlap check is rejecting everything - check `Intersects` |

## Step 6: regenerate the documentation

Skip this if you did not set up docfx in Part 1.

**Delete the stale metadata first.** docfx writes one `.yml` per type into `api/` and never
removes the ones whose type has gone, so `MapFactory` would keep a page in the generated site long after
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

Expected: `Build succeeded. 0 warning(s) 0 error(s)`, and pages for `RectangularRoom`, `Corridor`, `DungeonSettings`, `DungeonGenerator` and `GeneratedDungeon` at
<http://localhost:8081>.

The pages come from the `///` comments you wrote on each class and method - which is the reason
those comments state what a method refuses as well as what it does. A generated reference is only
worth as much as the comments behind it.

---

Next: **Part 4, field of view.**

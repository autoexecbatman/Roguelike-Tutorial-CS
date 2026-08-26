# Part 4: Field of view

- [What it is](#what-it-is)
- [How to use it](#how-to-use-it)
- [How to set it up](#how-to-set-it-up)

---

# What it is

You stop seeing the whole dungeon. You see what is in front of you, you remember where you have
been, and the rest is dark. That single change turns a map into a place.

Standing where you start:

```


                                                              # #
                                                          #####.#
                                                          #.....##
                                                      #####.....##...
                                                     ........@........
                                                      #####....###...
                                                          ###### #



```

After walking twenty-five steps:

```
                                                              #.#
                                                              #.#
                                                              #.#
                                                              #.#
                                                          #####.########
                                                          #.....##.....#
                                                      #####.....##.....#
                                                     ..................#
                                                      #####....###.....#
                                                          ###### #....@#
                                                                 #######



```

The explored part stays drawn - dimmed, from memory - and a second room has appeared. In the
game those remembered cells are drawn at a third brightness; in text there is no way to show
that, which is why the tests check the colour directly rather than the picture.

## Three states, not two

The obvious design is a boolean: visible or not. It is wrong, and the third state is the one
that makes the game feel like exploring:

| State | Drawn as | Meaning |
|---|---|---|
| `Unseen` | nothing at all | never been in sight |
| `Remembered` | the glyph, dimmed | seen before, not now |
| `Visible` | the glyph, lit | in sight this turn |

**Memory is one-way.** A cell that has been seen never goes back to `Unseen`. That is what fills
the map in behind you as you walk, and it is one line of the implementation - `_visible` is
cleared every turn and `_remembered` never is.

**Creatures are not remembered.** A monster is drawn only where you can see it *now*. Remember
a monster and the player chases something that walked away several turns ago.

## Symmetry is the property that matters

If you can see a cell, someone standing on that cell can see you.

That sounds like a nicety. It is not. Part 5 puts monsters in the dungeon, and a monster that
can see you from a cell you cannot see into is a bug the player experiences as unfair - shot at
from somewhere they had no way to look.

Plenty of field-of-view algorithms break this quietly. Shadowcasting is fast and is what a large
map wants, but its symmetry depends on getting the slope arithmetic exactly right, and getting
it wrong produces a field of view that looks fine and is subtly asymmetric.

This part takes the other trade: **symmetry by construction rather than by careful
implementation.**

```csharp
private static bool HasClearLine(Point from, Point to, GameMap map)
{
    return IsUnobstructed(from, to, map) || IsUnobstructed(to, from, map);
}
```

A cell is visible when a straight line between it and you passes through nothing solid, checked
in both directions, either one clear being enough. Swap the arguments and you swap the two
lines - the answer cannot change. Symmetry follows from the definition, not from the code being
right.

The test sweeps every ordered pair of floor cells on a map with pillars and asserts it both
ways round:

```csharp
foreach (Point viewer in floorCells)
{
    ISet<Point> seenByViewer = FieldOfView.From(viewer, radius, map);

    foreach (Point target in floorCells)
    {
        if (!seenByViewer.Contains(target)) { continue; }

        Assert.True(FieldOfView.From(target, radius, map).Contains(viewer),
            $"{viewer} sees {target} but {target} does not see {viewer}");
    }
}
```

The cost is speed: this is O(cells x line length) per turn where shadowcasting is roughly
O(cells). At a radius of 8 on an 80x25 map that is nothing. On a large map it would matter, and
the honest upgrade path is shadowcasting plus this test - which is exactly what the test is for.

## Room corners, and why walls are lit differently

Pure line-of-sight leaves the corners of a room dark. Stand in the middle of a room and the
corner has no clear line to you: both diagonals clip a wall. The room then renders with holes in
it:

```
 ###          #####
#...#   not  #...#
#...#        #...#
 ###          #####
```

The fix is to light walls by adjacency instead: after the floor is settled, any wall touching a
visible floor cell is lit too. That is cosmetic and it **cannot affect gameplay**, because
creatures stand on floor - the floor-to-floor symmetry Part 5 depends on is untouched.

Two things that would break it, both tested:

- Lighting floor by adjacency as well would erase shadows entirely: the cell behind a pillar
  touches a visible cell.
- Lighting walls from other walls would let sight creep along a wall forever, so the visible set
  is snapshotted before the pass rather than grown while walking it.

## What is deliberately wrong

**The sight radius is a constant.** A torch that could be dropped, a spell that widens it, light
sources on the map - none of that exists. `PlayerSightRadius` is 8 in `RootScreen`.

**Everything in sight is equally lit.** Real light falls off with distance.

**Sight is recomputed from scratch every move.** Fine at this size, wasteful later.

**Nothing uses this yet except drawing.** Monsters do not exist, so nothing is hiding from you.
Part 5 changes that, and this part is what makes it fair.

---

# How to use it

## Play it

```
cd parts/part-04-field-of-view
dotnet run --project RogueTutorial
```

You start in a small pool of light. Walk, and the dungeon fills in behind you.

To see more or less at once, change one constant in
[`RootScreen.cs`](../parts/part-04-field-of-view/RogueTutorial/RootScreen.cs):

```csharp
private const int PlayerSightRadius = 8;
```

## Run the tests

```
dotnet test                                  # 153 tests
dotnet test --filter "Category!=EndToEnd"    # no window
```

| Test class | Level | Covers |
|---|---|---|
| [`FieldOfViewTests`](../parts/part-04-field-of-view/RogueTutorial.Tests/FieldOfViewTests.cs) | unit | lit shapes as ASCII, shadows, and the symmetry sweep |
| [`VisibilityMapTests`](../parts/part-04-field-of-view/RogueTutorial.Tests/VisibilityMapTests.cs) | unit | the three states, and that memory is never lost |
| [`FrameComposerVisibilityTests`](../parts/part-04-field-of-view/RogueTutorial.Tests/FrameComposerVisibilityTests.cs) | unit | lit, dimmed, blank, and hidden entities |

The field-of-view tests build a map from ASCII and compare the lit cells against a second
picture, so a failure prints as a shape:

```csharp
Assert.Equal(
    Picture(
        "   *   ",
        " ***** ",
        " ***** ",
        "*******",
        " ***** ",
        " ***** ",
        "   *   "),
    Lit(map, visible));
```

That circle is `dx^2 + dy^2 <= radius^2`, worked out on paper. My first draft of it was a
fatter circle drawn by eye, and the test failed against correct code - which is the red step
catching the *test* rather than the implementation, and is why expected values come from the
rule rather than from what the code printed.

## Prove the tests can fail

| Change | Expect |
|---|---|
| `HasClearLine`: check one direction only | 2 fail, symmetry among them |
| `FieldOfView`: drop the round-radius test | 2 fail |
| Wall lighting: light every neighbour, not just walls | 3 fail |
| `VisibilityMap.Update`: clear `_remembered` as well | 5 fail |
| `FrameComposer`: draw entities on remembered cells too | 1 fails |

---

# How to set it up

> **You are in:** your project folder, the one holding `RogueTutorial/` and `RogueTutorial.Tests/`

## Step 1: the source files

**Each block below is the complete file.** `GameMap.cs`, `FrameComposer.cs` and `RootScreen.cs`
already exist from earlier parts - replace them entirely rather than merging by hand.

### [`RogueTutorial/FieldOfView.cs`](../parts/part-04-field-of-view/RogueTutorial/FieldOfView.cs)

Which cells are visible from a point. The symmetry argument is in the header.

```csharp
/*
 * Which cells the player can see from where they stand.
 *
 * A cell is visible when a straight line between it and the origin passes through nothing
 * solid. The line is checked in both directions and either one clear is enough, which makes
 * visibility symmetric by construction: if you can see a cell, a viewer standing there can see
 * you, because it is the same pair of lines either way round.
 *
 * Symmetry is not a nicety. Part 5 puts monsters on the map, and a monster that can see you
 * from a cell you cannot see is a bug a player experiences as unfair. Shadowcasting is faster
 * and is what a large map would want, but its symmetry depends on getting the slope arithmetic
 * exactly right; this gets symmetry from the definition and is small enough to check by eye.
 *
 * Usage:
 *
 *     GameMap map = dungeon.Map;
 *     ISet<Point> lit = FieldOfView.From(player.Position, radius: 8, map);
 *
 *     bool canSee = lit.Contains(new Point(12, 7));   // -> true if nothing blocks the line
 *     int howMany = lit.Count;                         // the origin is always included
 *
 * Refuses a null map and a negative radius. A radius of zero lights only the origin. Cells off
 * the map are never returned.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using SadRogue.Primitives;

namespace RogueTutorial;

internal static class FieldOfView
{
    /// <summary>
    /// Returns every cell visible from the origin within the radius, the origin included.
    /// Visibility is symmetric: the result contains a cell exactly when a viewer on that cell
    /// would see the origin. Throws ArgumentNullException on a null map and
    /// ArgumentOutOfRangeException on a negative radius.
    /// </summary>
    public static ISet<Point> From(Point origin, int radius, GameMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        // A negative radius is a caller error; zero is the legitimate "see only yourself" case.
        if (radius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "A sight radius cannot be negative.");
        }

        // You always see the cell you occupy, even standing in a doorway or inside rubble.
        HashSet<Point> visible = new HashSet<Point> { origin };

        // Only the square around the origin can hold anything within the radius, and the
        // round-distance test below trims that square to a circle.
        for (int row = origin.Y - radius; row <= origin.Y + radius; row++)
        {
            for (int col = origin.X - radius; col <= origin.X + radius; col++)
            {
                Point candidate = new Point(col, row);

                // A cell off the map is not somewhere the player can see; skipping it here is
                // what keeps the returned set safe to index the map with.
                if (!map.IsInBounds(candidate))
                {
                    continue;
                }

                // Round rather than square, so sight reaches equally far in every direction
                // instead of further along the diagonals.
                if (DistanceSquared(origin, candidate) > radius * radius)
                {
                    continue;
                }

                if (HasClearLine(origin, candidate, map))
                {
                    visible.Add(candidate);
                }
            }
        }

        // Walls are lit separately, after the floor is settled - see the method for why.
        LightWallsTouchingVisibleFloor(visible, radius, origin, map);

        // Symmetry rests on the origin being in its own set; a viewer must see themselves.
        Debug.Assert(visible.Contains(origin), "The origin must always be visible to itself.");

        return visible;
    }

    /// <summary>
    /// Adds every wall cell that touches a visible floor cell, so a room's outline is drawn
    /// whole rather than with gaps at its corners.
    ///
    /// A room corner has no clear line to the middle of the room - both diagonals clip a wall -
    /// so pure line-of-sight leaves it dark and the room renders with holes in it. Lighting
    /// walls by adjacency instead is cosmetic and cannot affect gameplay, because creatures
    /// stand on floor: the floor-to-floor visibility that Part 5 relies on stays symmetric.
    /// </summary>
    private static void LightWallsTouchingVisibleFloor(
        HashSet<Point> visible, int radius, Point origin, GameMap map)
    {
        // Snapshot first: adding to the set while walking it would let a newly lit wall light
        // its own neighbours, and sight would creep along a wall indefinitely.
        List<Point> visibleFloor = new List<Point>();

        foreach (Point cell in visible)
        {
            if (map.IsTransparent(cell))
            {
                visibleFloor.Add(cell);
            }
        }

        foreach (Point floorCell in visibleFloor)
        {
            // All eight neighbours, so diagonal corners are covered too.
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    Point neighbour = new Point(floorCell.X + offsetX, floorCell.Y + offsetY);

                    // Only walls, only on the map, and only inside the sight radius.
                    if (map.IsInBounds(neighbour)
                        && !map.IsTransparent(neighbour)
                        && DistanceSquared(origin, neighbour) <= radius * radius)
                    {
                        visible.Add(neighbour);
                    }
                }
            }
        }
    }

    /// <summary>
    /// True when sight passes between the two cells. Both directions are tried and either being
    /// clear is enough, which is what makes the relation symmetric: swapping the arguments swaps
    /// the two lines and leaves the answer unchanged.
    /// </summary>
    private static bool HasClearLine(Point from, Point to, GameMap map)
    {
        return IsUnobstructed(from, to, map) || IsUnobstructed(to, from, map);
    }

    /// <summary>
    /// Walks a Bresenham line from one cell to the other and reports whether every cell strictly
    /// between them lets sight through. The endpoints are not tested: you can see a wall, and
    /// standing in one does not blind you.
    /// </summary>
    private static bool IsUnobstructed(Point from, Point to, GameMap map)
    {
        int deltaX = Math.Abs(to.X - from.X);
        int deltaY = Math.Abs(to.Y - from.Y);

        // Step is +1 or -1 per axis, so one loop covers all eight directions.
        int stepX = from.X < to.X ? 1 : -1;
        int stepY = from.Y < to.Y ? 1 : -1;

        // Bresenham's running error term, doubled so it stays in integers.
        int error = deltaX - deltaY;

        Point cell = from;

        while (cell != to)
        {
            int doubledError = error * 2;

            if (doubledError > -deltaY)
            {
                error -= deltaY;
                cell = new Point(cell.X + stepX, cell.Y);
            }
            else
            {
                error += deltaX;
                cell = new Point(cell.X, cell.Y + stepY);
            }

            // Arriving at the destination means nothing in between blocked the way.
            if (cell == to)
            {
                return true;
            }

            // Anything solid strictly between the endpoints stops the line.
            if (!map.IsTransparent(cell))
            {
                return false;
            }
        }

        // Reached only when the endpoints are the same cell.
        return true;
    }

    // Squared distance, so the radius test needs no square root.
    private static int DistanceSquared(Point from, Point to)
    {
        int deltaX = to.X - from.X;
        int deltaY = to.Y - from.Y;

        return (deltaX * deltaX) + (deltaY * deltaY);
    }
}
```

### [`RogueTutorial/VisibilityMap.cs`](../parts/part-04-field-of-view/RogueTutorial/VisibilityMap.cs)

Three states per cell, and the one-way memory that makes a dungeon feel explored.

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

### [`RogueTutorial/FrameComposer.cs`](../parts/part-04-field-of-view/RogueTutorial/FrameComposer.cs)

The Part 2 file with one overload added: the same drawing, filtered through what the player knows.

<!-- generated-diff -->
**Changed from Part 3.** The complete file follows; this is only what moved:

```diff
--- part-03-dungeon-generation/FrameComposer.cs
+++ current/FrameComposer.cs
@@ -8,6 +8,9 @@
  *     RenderedFrame frame = FrameComposer.Compose(map, new[] { player });
  *     string picture = frame.ToText();
  *     // -> "...\n.@."
+ *
+ * The overload taking a VisibilityMap is what the game uses from Part 4 on: it dims what the
+ * player remembers, blanks what they have never seen, and hides entities standing in the dark.
  *
  * Refuses a null map or null entity list. An entity standing off the map is skipped rather than
  * throwing, because a later part moves entities between levels.
@@ -25,6 +28,76 @@
     /// Draws every map tile, then every entity over the top in list order, so a later entity
     /// covers an earlier one sharing its cell. Throws ArgumentNullException on a null argument.
     /// </summary>
+    /// <summary>
+    /// Draws the map and entities as the player currently perceives them: cells in sight at full
+    /// colour, cells only remembered dimmed, cells never seen left blank, and entities drawn only
+    /// where the player can actually see them. Throws ArgumentNullException on a null argument.
+    /// </summary>
+    public static RenderedFrame Compose(GameMap map, IReadOnlyList<Entity> entities, VisibilityMap visibility)
+    {
+        ArgumentNullException.ThrowIfNull(map);
+        ArgumentNullException.ThrowIfNull(entities);
+        ArgumentNullException.ThrowIfNull(visibility);
+
+        char[] glyphs = new char[map.Width * map.Height];
+        Color[] foregrounds = new Color[map.Width * map.Height];
+
+        for (int row = 0; row < map.Height; row++)
+        {
+            for (int col = 0; col < map.Width; col++)
+            {
+                Point cell = new Point(col, row);
+                int index = (row * map.Width) + col;
+
+                CellVisibility state = visibility.StateAt(cell);
+
+                // Never seen: nothing is drawn, so unexplored dungeon reads as empty space.
+                if (state == CellVisibility.Unseen)
+                {
+                    glyphs[index] = ' ';
+                    foregrounds[index] = Color.Black;
+                    continue;
+                }
+
+                Tile tile = map.GetTile(cell);
+
+                glyphs[index] = tile.Glyph;
+
+                // Remembered cells are drawn from memory, so they are dimmed rather than lit.
+                foregrounds[index] = state == CellVisibility.Visible
+                    ? tile.Foreground
+                    : DimmedForMemory(tile.Foreground);
+            }
+        }
+
+        foreach (Entity entity in entities)
+        {
+            if (!map.IsInBounds(entity.Position))
+            {
+                continue;
+            }
+
+            // Creatures are not remembered: an entity is drawn only where it can be seen now,
+            // otherwise the player would watch a monster that had long since walked away.
+            if (visibility.StateAt(entity.Position) != CellVisibility.Visible)
+            {
+                continue;
+            }
+
+            int index = (entity.Position.Y * map.Width) + entity.Position.X;
+            glyphs[index] = entity.Glyph;
+            foregrounds[index] = entity.Foreground;
+        }
+
+        return new RenderedFrame(map.Width, map.Height, glyphs, foregrounds);
+    }
+
+    // A third of full brightness: dark enough to read as memory, light enough to make out.
+    private static Color DimmedForMemory(Color lit)
+    {
+        return new Color(lit.R / 3, lit.G / 3, lit.B / 3);
+    }
+
     public static RenderedFrame Compose(GameMap map, IReadOnlyList<Entity> entities)
     {
         ArgumentNullException.ThrowIfNull(map);
```
<!-- generated-diff -->

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

### [`RogueTutorial/GameMap.cs`](../parts/part-04-field-of-view/RogueTutorial/GameMap.cs)

The Part 3 file with `IsTransparent` added.

<!-- generated-diff -->
**Changed from Part 3.** The complete file follows; this is only what moved:

```diff
--- part-03-dungeon-generation/GameMap.cs
+++ current/GameMap.cs
@@ -105,6 +105,21 @@
         return _tiles[IndexOf(position)].IsWalkable;
     }
 
+    /// <summary>
+    /// True when sight passes through the position. Anything off the map answers false, so
+    /// field-of-view code can ask about the cell beyond the edge without a bounds check.
+    /// </summary>
+    public bool IsTransparent(Point position)
+    {
+        // Outside the map is solid rock as far as sight is concerned.
+        if (!IsInBounds(position))
+        {
+            return false;
+        }
+
+        return _tiles[IndexOf(position)].IsTransparent;
+    }
+
     // Row-major index; the single place the storage layout is expressed.
     private int IndexOf(Point position)
     {
```
<!-- generated-diff -->

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

    /// <summary>
    /// True when sight passes through the position. Anything off the map answers false, so
    /// field-of-view code can ask about the cell beyond the edge without a bounds check.
    /// </summary>
    public bool IsTransparent(Point position)
    {
        // Outside the map is solid rock as far as sight is concerned.
        if (!IsInBounds(position))
        {
            return false;
        }

        return _tiles[IndexOf(position)].IsTransparent;
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

### [`RogueTutorial/RootScreen.cs`](../parts/part-04-field-of-view/RogueTutorial/RootScreen.cs)

Recomputes sight after every move that changed the player's position.

<!-- generated-diff -->
**Changed from Part 3.** The complete file follows; this is only what moved:

```diff
--- part-03-dungeon-generation/RootScreen.cs
+++ current/RootScreen.cs
@@ -1,6 +1,7 @@
 /*
  * The top-level screen: it wires SadConsole's window and keyboard to the game, and blits the
- * composed frame. It owns no rules. The map, the entities, where a move ends up and what the
+ * composed frame. It owns no rules. From Part 4 it also recomputes the player's field of view
+ * after every move, so the map is drawn as the player perceives it rather than as it is. The map, the entities, where a move ends up and what the
  * picture should look like are all decided by classes that run without a graphics host.
  *
  * Usage - SadConsole constructs this itself, because Program.cs named it as the starting
@@ -23,6 +24,10 @@
 
 internal sealed class RootScreen : ScreenObject
 {
+    // How far the player can see, in cells. Large enough to take in a room, small enough that
+    // a corridor stays dark ahead of you.
+    private const int PlayerSightRadius = 8;
+
     // The surface every glyph is drawn onto. One cell per grid position.
     private readonly ScreenSurface _mapSurface;
 
@@ -31,6 +36,9 @@
 
     // Everything drawn on top of the map, in draw order: later entries cover earlier ones.
     private readonly List<Entity> _entities;
+
+    // What the player can see now and what they remember, updated after every move.
+    private readonly VisibilityMap _visibility;
 
     // The entity the keyboard drives. Also present in _entities, so it is drawn like any other.
     private readonly Entity _player;
@@ -69,6 +77,12 @@
         // The player is last, so it covers anything standing on the same cell.
         _entities = new List<Entity> { villager, _player };
 
+        _visibility = new VisibilityMap(_map.Width, _map.Height);
+
+        // Without this the first frame would be drawn before anything had been seen, so the
+        // player would spend one frame staring at an entirely blank screen.
+        RecomputeFieldOfView();
+
         DrawFrame();
     }
 
@@ -96,6 +110,11 @@
         if (destination != _player.Position)
         {
             _player.MoveTo(destination);
+
+            // Sight is recomputed from the new position before the frame is drawn, or the
+            // player would see one frame of the view from where they used to stand.
+            RecomputeFieldOfView();
+
             DrawFrame();
         }
 
@@ -106,9 +125,18 @@
     /// Composes the picture and copies it onto the surface, one cell at a time. Everything
     /// decided here was already decided by FrameComposer; this only moves it to the screen.
     /// </summary>
+    /// <summary>
+    /// Works out what the player can see from where they now stand and folds it into what they
+    /// remember. Called once at construction and after every move that changed the position.
+    /// </summary>
+    private void RecomputeFieldOfView()
+    {
+        _visibility.Update(FieldOfView.From(_player.Position, PlayerSightRadius, _map));
+    }
+
     private void DrawFrame()
     {
-        RenderedFrame frame = FrameComposer.Compose(_map, _entities);
+        RenderedFrame frame = FrameComposer.Compose(_map, _entities, _visibility);
 
         for (int row = 0; row < frame.Height; row++)
         {
```
<!-- generated-diff -->

```csharp
/*
 * The top-level screen: it wires SadConsole's window and keyboard to the game, and blits the
 * composed frame. It owns no rules. From Part 4 it also recomputes the player's field of view
 * after every move, so the map is drawn as the player perceives it rather than as it is. The map, the entities, where a move ends up and what the
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
    // How far the player can see, in cells. Large enough to take in a room, small enough that
    // a corridor stays dark ahead of you.
    private const int PlayerSightRadius = 8;

    // The surface every glyph is drawn onto. One cell per grid position.
    private readonly ScreenSurface _mapSurface;

    // The dungeon floor. Fixed for this part; generated for real in Part 3.
    private readonly GameMap _map;

    // Everything drawn on top of the map, in draw order: later entries cover earlier ones.
    private readonly List<Entity> _entities;

    // What the player can see now and what they remember, updated after every move.
    private readonly VisibilityMap _visibility;

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

        _visibility = new VisibilityMap(_map.Width, _map.Height);

        // Without this the first frame would be drawn before anything had been seen, so the
        // player would spend one frame staring at an entirely blank screen.
        RecomputeFieldOfView();

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

            // Sight is recomputed from the new position before the frame is drawn, or the
            // player would see one frame of the view from where they used to stand.
            RecomputeFieldOfView();

            DrawFrame();
        }

        return true;
    }

    /// <summary>
    /// Composes the picture and copies it onto the surface, one cell at a time. Everything
    /// decided here was already decided by FrameComposer; this only moves it to the screen.
    /// </summary>
    /// <summary>
    /// Works out what the player can see from where they now stand and folds it into what they
    /// remember. Called once at construction and after every move that changed the position.
    /// </summary>
    private void RecomputeFieldOfView()
    {
        _visibility.Update(FieldOfView.From(_player.Position, PlayerSightRadius, _map));
    }

    private void DrawFrame()
    {
        RenderedFrame frame = FrameComposer.Compose(_map, _entities, _visibility);

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
const string WindowTitle = "Roguelike Tutorial - Part 4: Field of view";
```

Nothing else in that file changes.

## Step 2: the test files

**Each block below is the complete file.** Create it in `RogueTutorial.Tests/`.

### [`RogueTutorial.Tests/FieldOfViewTests.cs`](../parts/part-04-field-of-view/RogueTutorial.Tests/FieldOfViewTests.cs)

Shapes as ASCII, the shadow behind a pillar, and the symmetry sweep over every pair of floor cells.

```csharp
/*
 * Unit tests for what the player can see. Most of these build a small map as an ASCII picture,
 * compute the field of view from a marked origin, and compare the lit cells against a second
 * picture - so a failure prints as a shape rather than a coordinate.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~FieldOfViewTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class FieldOfViewTests
{
    // Builds a map from rows of text: '#' is wall, anything else is floor.
    private static GameMap MapFrom(params string[] rows)
    {
        GameMap map = new GameMap(rows[0].Length, rows.Length);

        for (int row = 0; row < rows.Length; row++)
        {
            for (int col = 0; col < rows[row].Length; col++)
            {
                map.SetTile(new Point(col, row), rows[row][col] == '#' ? TileTypes.Wall : TileTypes.Floor);
            }
        }

        return map;
    }

    // Renders the lit set back to text, so an expected picture can be written by hand.
    private static string Lit(GameMap map, ISet<Point> visible)
    {
        StringBuilder picture = new StringBuilder();

        for (int row = 0; row < map.Height; row++)
        {
            if (row > 0)
            {
                picture.Append('\n');
            }

            for (int col = 0; col < map.Width; col++)
            {
                picture.Append(visible.Contains(new Point(col, row)) ? '*' : ' ');
            }
        }

        return picture.ToString();
    }

    private static string Picture(params string[] rows)
    {
        return string.Join("\n", rows);
    }

    [Fact]
    public void TheOriginIsAlwaysVisible()
    {
        // True even standing inside a wall, which happens if a later part teleports you badly.
        GameMap map = MapFrom("###", "###", "###");

        ISet<Point> visible = FieldOfView.From(new Point(1, 1), radius: 5, map);

        Assert.Contains(new Point(1, 1), visible);
    }

    [Fact]
    public void ARadiusOfZeroLightsOnlyTheOrigin()
    {
        GameMap map = MapFrom(".....", ".....", ".....");

        ISet<Point> visible = FieldOfView.From(new Point(2, 1), radius: 0, map);

        Assert.Equal(new[] { new Point(2, 1) }, visible.ToArray());
    }

    [Fact]
    public void AnOpenRoomIsLitInACircle()
    {
        // Round rather than square: the corners of the bounding box stay dark, which is what
        // stops sight reaching further along a diagonal than along an axis.
        GameMap map = MapFrom(
            ".......",
            ".......",
            ".......",
            ".......",
            ".......",
            ".......",
            ".......");

        ISet<Point> visible = FieldOfView.From(new Point(3, 3), radius: 3, map);

        Assert.Equal(
            Picture(
                "   *   ",
                " ***** ",
                " ***** ",
                "*******",
                " ***** ",
                " ***** ",
                "   *   "),
            Lit(map, visible));
    }

    [Fact]
    public void AWallCastsAShadowBehindIt()
    {
        // A single pillar directly right of the origin hides the cells behind it.
        GameMap map = MapFrom(
            ".......",
            ".......",
            "...#...",
            ".......",
            ".......");

        ISet<Point> visible = FieldOfView.From(new Point(1, 2), radius: 6, map);

        // The pillar itself is lit; what lies straight behind it is not.
        Assert.Contains(new Point(3, 2), visible);
        Assert.DoesNotContain(new Point(4, 2), visible);
        Assert.DoesNotContain(new Point(5, 2), visible);
        Assert.DoesNotContain(new Point(6, 2), visible);
    }

    [Fact]
    public void SightPassesEitherSideOfAPillar()
    {
        GameMap map = MapFrom(
            ".......",
            ".......",
            "...#...",
            ".......",
            ".......");

        ISet<Point> visible = FieldOfView.From(new Point(1, 2), radius: 6, map);

        // The shadow is a wedge, not a wall: rows above and below stay lit.
        Assert.Contains(new Point(5, 1), visible);
        Assert.Contains(new Point(5, 3), visible);
    }

    [Fact]
    public void AClosedRoomShowsItsOwnWallsAndNothingBeyond()
    {
        GameMap map = MapFrom(
            "#####",
            "#...#",
            "#...#",
            "#...#",
            "#####");

        ISet<Point> visible = FieldOfView.From(new Point(2, 2), radius: 10, map);

        // Every cell of the room, walls included, and nothing outside it - there is nothing
        // outside it here, so the real assertion is that the walls themselves are lit.
        Assert.Equal(
            Picture(
                "*****",
                "*****",
                "*****",
                "*****",
                "*****"),
            Lit(map, visible));
    }

    [Fact]
    public void LightingWallsDoesNotLeakSightAroundCorners()
    {
        // Walls are lit by touching visible floor, which must not let that lighting spread from
        // wall to wall: the far room's inner wall touches no floor the player can see.
        GameMap map = MapFrom(
            "#########",
            "#...#...#",
            "#...#...#",
            "#########");

        ISet<Point> visible = FieldOfView.From(new Point(2, 2), radius: 10, map);

        // The dividing wall is lit from this side, since floor beside it is visible.
        Assert.Contains(new Point(4, 2), visible);

        // Nothing in the far room is, floor or wall.
        Assert.DoesNotContain(new Point(6, 2), visible);
        Assert.DoesNotContain(new Point(8, 2), visible);
    }

    [Fact]
    public void OnlyWallsAreLitByAdjacency()
    {
        // Floor is lit by line of sight alone. If adjacency lit floor too, a cell behind a
        // pillar would light up because its neighbour is visible, and shadows would vanish.
        GameMap map = MapFrom(
            ".......",
            ".......",
            "...#...",
            ".......",
            ".......");

        ISet<Point> visible = FieldOfView.From(new Point(1, 2), radius: 6, map);

        // (4,2) is floor directly behind the pillar, and touches visible floor at (4,1).
        Assert.DoesNotContain(new Point(4, 2), visible);
    }

    [Fact]
    public void YouCannotSeeThroughAClosedDoorway()
    {
        // Two rooms with a solid wall between them: nothing in the far room is lit.
        GameMap map = MapFrom(
            "#######",
            "#..#..#",
            "#..#..#",
            "#######");

        ISet<Point> visible = FieldOfView.From(new Point(1, 1), radius: 10, map);

        Assert.DoesNotContain(new Point(4, 1), visible);
        Assert.DoesNotContain(new Point(5, 2), visible);
    }

    [Fact]
    public void SightReachesThroughAGapInAWall()
    {
        GameMap map = MapFrom(
            "#######",
            "#..#..#",
            "#.....#",
            "#..#..#",
            "#######");

        ISet<Point> visible = FieldOfView.From(new Point(1, 2), radius: 10, map);

        // The gap is the middle row, so the far side of it is lit.
        Assert.Contains(new Point(5, 2), visible);
    }

    [Fact]
    public void VisibilityIsSymmetric()
    {
        // The property the algorithm was chosen for. If A sees B then B must see A, or a
        // monster placed in Part 5 can shoot from a cell the player cannot see into.
        GameMap map = MapFrom(
            "..........",
            "..#....#..",
            "..........",
            "....##....",
            "..........",
            "..#....#..",
            "..........");

        const int radius = 6;

        List<Point> floorCells = new List<Point>();
        for (int row = 0; row < map.Height; row++)
        {
            for (int col = 0; col < map.Width; col++)
            {
                Point cell = new Point(col, row);
                if (map.IsWalkable(cell))
                {
                    floorCells.Add(cell);
                }
            }
        }

        // Every ordered pair of floor cells, checked both ways round.
        foreach (Point viewer in floorCells)
        {
            ISet<Point> seenByViewer = FieldOfView.From(viewer, radius, map);

            foreach (Point target in floorCells)
            {
                if (!seenByViewer.Contains(target))
                {
                    continue;
                }

                ISet<Point> seenByTarget = FieldOfView.From(target, radius, map);

                Assert.True(
                    seenByTarget.Contains(viewer),
                    $"{viewer} sees {target} but {target} does not see {viewer}");
            }
        }
    }

    [Fact]
    public void NothingBeyondTheRadiusIsLit()
    {
        GameMap map = MapFrom(
            "...........",
            "...........",
            "...........",
            "...........",
            "...........");

        ISet<Point> visible = FieldOfView.From(new Point(5, 2), radius: 3, map);

        foreach (Point cell in visible)
        {
            int deltaX = cell.X - 5;
            int deltaY = cell.Y - 2;

            Assert.True((deltaX * deltaX) + (deltaY * deltaY) <= 9, $"{cell} is outside the radius");
        }
    }

    [Fact]
    public void SightStopsAtTheEdgeOfTheMap()
    {
        // Standing in a corner: the radius runs off the map and nothing throws.
        GameMap map = MapFrom("...", "...", "...");

        ISet<Point> visible = FieldOfView.From(new Point(0, 0), radius: 10, map);

        Assert.All(visible, cell => Assert.True(map.IsInBounds(cell), $"{cell} is off the map"));
    }

    [Fact]
    public void ANullMapIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => FieldOfView.From(Point.Zero, 5, null!));
    }

    [Fact]
    public void ANegativeRadiusIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FieldOfView.From(Point.Zero, -1, new GameMap(5, 5)));
    }
}
```

### [`RogueTutorial.Tests/VisibilityMapTests.cs`](../parts/part-04-field-of-view/RogueTutorial.Tests/VisibilityMapTests.cs)

Chiefly that memory is one-way.

```csharp
/*
 * Unit tests for what the player knows about each cell. The property that matters most is that
 * memory is one-way: walking away from a cell must dim it, never blank it.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~VisibilityMapTests
 */

using System;
using System.Collections.Generic;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class VisibilityMapTests
{
    private static ISet<Point> Cells(params Point[] cells)
    {
        return new HashSet<Point>(cells);
    }

    [Fact]
    public void EveryCellStartsUnseen()
    {
        VisibilityMap visibility = new VisibilityMap(4, 3);

        for (int row = 0; row < visibility.Height; row++)
        {
            for (int col = 0; col < visibility.Width; col++)
            {
                Assert.Equal(CellVisibility.Unseen, visibility.StateAt(new Point(col, row)));
            }
        }
    }

    [Fact]
    public void ACellInSightIsVisible()
    {
        VisibilityMap visibility = new VisibilityMap(4, 3);

        visibility.Update(Cells(new Point(1, 1)));

        Assert.Equal(CellVisibility.Visible, visibility.StateAt(new Point(1, 1)));
    }

    [Fact]
    public void ACellLeftBehindIsRemembered()
    {
        // The whole point of the class: walk away and the cell dims rather than disappearing.
        VisibilityMap visibility = new VisibilityMap(4, 3);

        visibility.Update(Cells(new Point(1, 1)));
        visibility.Update(Cells(new Point(3, 1)));

        Assert.Equal(CellVisibility.Remembered, visibility.StateAt(new Point(1, 1)));
        Assert.Equal(CellVisibility.Visible, visibility.StateAt(new Point(3, 1)));
    }

    [Fact]
    public void MemoryIsNeverLost()
    {
        // Ten turns elsewhere must not blank a cell seen on the first.
        VisibilityMap visibility = new VisibilityMap(10, 3);

        visibility.Update(Cells(new Point(0, 0)));

        for (int turn = 0; turn < 10; turn++)
        {
            visibility.Update(Cells(new Point(9, 2)));
        }

        Assert.Equal(CellVisibility.Remembered, visibility.StateAt(new Point(0, 0)));
    }

    [Fact]
    public void ReturningToACellMakesItVisibleAgain()
    {
        VisibilityMap visibility = new VisibilityMap(4, 3);

        visibility.Update(Cells(new Point(1, 1)));
        visibility.Update(Cells(new Point(3, 1)));
        visibility.Update(Cells(new Point(1, 1)));

        Assert.Equal(CellVisibility.Visible, visibility.StateAt(new Point(1, 1)));
    }

    [Fact]
    public void ACellNeverSeenStaysUnseen()
    {
        VisibilityMap visibility = new VisibilityMap(4, 3);

        visibility.Update(Cells(new Point(1, 1)));
        visibility.Update(Cells(new Point(2, 1)));

        Assert.Equal(CellVisibility.Unseen, visibility.StateAt(new Point(3, 2)));
    }

    [Fact]
    public void AnEmptyUpdateClearsSightButNotMemory()
    {
        // Being struck blind should dim the map, not erase it.
        VisibilityMap visibility = new VisibilityMap(4, 3);

        visibility.Update(Cells(new Point(1, 1), new Point(2, 1)));
        visibility.Update(Cells());

        Assert.Equal(CellVisibility.Remembered, visibility.StateAt(new Point(1, 1)));
        Assert.Equal(CellVisibility.Remembered, visibility.StateAt(new Point(2, 1)));
    }

    [Fact]
    public void CellsOutsideTheMapAreIgnoredRatherThanRejected()
    {
        // A field of view near an edge legitimately contains cells past it.
        VisibilityMap visibility = new VisibilityMap(3, 3);

        visibility.Update(Cells(new Point(1, 1), new Point(-1, 0), new Point(9, 9)));

        Assert.Equal(CellVisibility.Visible, visibility.StateAt(new Point(1, 1)));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(3, 0)]
    [InlineData(0, 3)]
    public void AskingAboutACellOffTheMapIsRejected(int x, int y)
    {
        VisibilityMap visibility = new VisibilityMap(3, 3);

        Assert.Throws<ArgumentOutOfRangeException>(() => visibility.StateAt(new Point(x, y)));
    }

    [Fact]
    public void ANullCellSetIsRejected()
    {
        VisibilityMap visibility = new VisibilityMap(3, 3);

        Assert.Throws<ArgumentNullException>(() => visibility.Update(null!));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    public void ADimensionBelowOneIsRejected(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new VisibilityMap(width, height));
    }
}
```

### [`RogueTutorial.Tests/FrameComposerVisibilityTests.cs`](../parts/part-04-field-of-view/RogueTutorial.Tests/FrameComposerVisibilityTests.cs)

What is drawn lit, what is drawn dim, and what is not drawn at all.

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
        Entity player = new Entity("Player", '@', Color.White, new Point(1, 0));

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
        Entity monster = new Entity("Rat", 'r', Color.Red, new Point(0, 0));

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
        Entity monster = new Entity("Rat", 'r', Color.Red, new Point(2, 0));

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

## Step 3: build and run

```
dotnet build
dotnet test
dotnet run --project RogueTutorial
```

Expected: a clean build, 153 passing tests, and a dungeon you have to explore.

### If something is wrong

| Symptom | Cause |
|---|---|
| The screen is entirely blank | `RecomputeFieldOfView` is not called in the constructor |
| The whole map is visible | `FrameComposer.Compose` is being called without the `VisibilityMap` |
| Explored areas vanish behind you | `_remembered` is being cleared in `Update` |
| The view lags one move behind | Sight is recomputed after `DrawFrame` instead of before |
| Rooms have dark corners | The wall-adjacency pass is missing |
| Shadows have disappeared | The adjacency pass is lighting floor as well as walls |
| Monsters visible through walls | `Compose` is drawing entities on remembered cells |

## Step 4: regenerate the documentation

Skip this if you did not set up docfx in Part 1. Nothing was deleted this part, so there is no
stale metadata to clear:

```
dotnet docfx docfx.json --serve --port 8081
```

Expected: `Build succeeded. 0 warning(s) 0 error(s)`, and pages for `FieldOfView`,
`VisibilityMap` and `CellVisibility` at <http://localhost:8081>.

---

Next: **Part 5, placing monsters.**

# Part 7: The message log and health bar

- [What it is](#what-it-is)
- [How to use it](#how-to-use-it)
- [How to set it up](#how-to-set-it-up)

---

# What it is

Part 6 filled a message log that nobody could read and tracked health nobody could see. This
puts both on screen, and divides the window to make room.

```
                                  #######........###############
                                 .....................@..........
                                 #...............................
                                 #....###................#######
                                  ..  # #........####....#
                                        #........#  ######
                                        #........#
                                        ##########





HP: 29/30 =============-
Rat hits Player for 1 damage.
Player hits Rat for 4 damage. Rat dies.



```

## The one-way door: the window is divided once

Everything up to here drew the map across the whole window. From now on the window has regions,
and Part 8 adds an inventory, Part 9 a targeting cursor, Part 11 character stats. If each of
those picks its own rows, they will disagree.

So one class decides, and everything else asks it:

```csharp
rows 0 .. MapHeight-1     the dungeon
row  StatusRow            the health bar
rows LogTopRow ..         the message log
```

`ScreenLayout` computes those from the window size and the number of log rows wanted. The
assertion in its constructor is the one that matters:

```csharp
Debug.Assert(
    MapHeight + 1 + LogRows == WindowHeight,
    "The map, the status row and the log must account for every row of the window.");
```

The regions must **tile** the window - no gaps, no overlap. A gap is a row that never gets
written and shows whatever was there before; an overlap is the map quietly erasing the health
bar, with nothing else to report it.

**The map is now smaller than the window**, so the dungeon is generated at `layout.MapHeight`
rather than the window height. Get that wrong and the map is silently cropped, which is why
`ScreenComposer` refuses a world that does not fit rather than trimming it.

## The interface is composed as data

Part 2 made the map assertable by building a `RenderedFrame` before drawing it. Part 7 does the
same for the interface, for the same reason: a health bar showing the wrong numbers, or a log
showing the wrong lines, is a defect nothing else can see.

```csharp
RenderedFrame screen = ScreenComposer.Compose(world, layout);

Assert.StartsWith("HP: 24/30 ", Row(screen, layout.StatusRow));
Assert.StartsWith("five", Row(screen, layout.LogTopRow + 2));
```

`RootScreen` blits that frame and decides nothing.

## Two rules the bar has to get right

**Round down, so only full health reads full.** At 29 of 30 across ten cells the true figure is
9.67. Rounding up would show a full bar on a player who has been hit, and being able to see that
you have taken damage is the entire job.

**A living player never shows an empty bar.** Rounding down takes 1 of 30 to zero cells, and an
empty bar on someone still standing reads as a bug rather than as danger, so the fill is floored
at one.

That floor needs one exception. A bar with no cells at all - a width exactly equal to the
caption - has nothing to fill, and flooring it at one makes the caller pad by `0 - 1`:

```
System.ArgumentOutOfRangeException : count ('-1') must be a non-negative value.
```

The two rules together are why `FilledCells` is longer than the one line of arithmetic it looks
like it should be.

## What is deliberately wrong

**No separator between the map and the panel.** The bottom row of the dungeon sits directly above
the health bar. A horizontal rule would read more cleanly and would cost a row of dungeon; it is
left out, and it is a one-line change to `ScreenLayout` and `ScreenComposer` if you disagree.

**Long messages are cut, not wrapped.** Wrapping would change how many messages fit, so the
number of visible lines would depend on their length.

**Nothing scrolls.** You see the last five lines and no more; there is no way to look back.

**The status row holds only health.** Dungeon level, experience and equipment arrive in later
parts, and the row is sized to leave them room.

---

# How to use it

## Play it

```
cd parts/part-07-log-and-health-bar
dotnet run --project RogueTutorial
```

The dungeon is nineteen rows now rather than twenty-five. Fight a rat and watch the bar move and
the log fill.

To trade dungeon space for log space, change one constant in
[`RootScreen.cs`](../parts/part-07-log-and-health-bar/RogueTutorial/RootScreen.cs):

```csharp
private const int LogRows = 5;
```

## Run the tests

```
dotnet test                                  # 290 tests
dotnet test --filter "Category!=EndToEnd"    # no window
```

| Test class | Level | Covers |
|---|---|---|
| [`ScreenLayoutTests`](../parts/part-07-log-and-health-bar/RogueTutorial.Tests/ScreenLayoutTests.cs) | unit | that the regions tile the window exactly |
| [`HealthBarTests`](../parts/part-07-log-and-health-bar/RogueTutorial.Tests/HealthBarTests.cs) | unit | the fill fraction and the caption, apart |
| [`ScreenComposerTests`](../parts/part-07-log-and-health-bar/RogueTutorial.Tests/ScreenComposerTests.cs) | unit | what reaches the screen |

## Prove the tests can fail

| Change | Expect |
|---|---|
| `FilledCells`: drop the `Math.Max(1, ...)` floor | 2 fail |
| `FilledCells`: round up instead of down | 1 fails |
| `ScreenLayout`: `StatusRow => MapHeight - 1` | 3 fail |
| `ScreenComposer`: render the bar at the window's width | 1 fails |
| `ScreenComposer`: reverse the log so newest is on top | 2 fail |

---

# How to set it up

> **You are in:** your project folder, the one holding `RogueTutorial/` and `RogueTutorial.Tests/`

## Step 1: retitle the window

One line in `RogueTutorial/Program.cs`:

```csharp
const string WindowTitle = "Roguelike Tutorial - Part 7: The message log and health bar";
```

## Step 2: the source files

**Each block below is the complete file.** Three are new; `RootScreen.cs` already exists and
should be replaced entirely.

**Do not build until every file in this step is in place** - C# compiles a project as a whole,
so a half-finished step fails on files that are perfectly correct.

### [`RogueTutorial/ScreenLayout.cs`](../parts/part-07-log-and-health-bar/RogueTutorial/ScreenLayout.cs)

Where each region sits. Every later part asks this rather than hardcoding a row.

```csharp
/*
 * Where each part of the interface sits on the screen.
 *
 * Until now the map filled the window. From Part 7 the window is divided, and every later part
 * adds something to the interface - an inventory in Part 8, a targeting cursor in Part 9. This
 * is the one place that decides where anything goes, so none of them hardcodes a row number.
 *
 * The window is laid out top to bottom:
 *
 *     rows 0 .. MapHeight-1        the dungeon
 *     row  StatusRow               the health bar
 *     rows LogTopRow ..            the message log, newest at the bottom
 *
 * Usage:
 *
 *     ScreenLayout layout = new ScreenLayout(windowWidth: 80, windowHeight: 25, logRows: 5);
 *
 *     int mapRows = layout.MapHeight;        // -> 19: the window less the panel
 *     int barRow = layout.StatusRow;         // -> 19, the first row below the map
 *     int logStart = layout.LogTopRow;       // -> 20
 *     bool onMap = layout.IsMapRow(3);       // -> true
 *
 * Refuses a window too small to hold a map of at least one row alongside the panel it was asked
 * for, and a log of fewer than one row.
 */

using System;
using System.Diagnostics;

namespace RogueTutorial;

internal sealed class ScreenLayout
{
    /// <summary>Width of the whole window, in cells. The map and the panel share it.</summary>
    public int WindowWidth { get; }

    /// <summary>Height of the whole window, in cells.</summary>
    public int WindowHeight { get; }

    /// <summary>How many rows of message log are shown.</summary>
    public int LogRows { get; }

    /// <summary>Rows the dungeon occupies, starting at row zero.</summary>
    public int MapHeight => WindowHeight - LogRows - 1;

    /// <summary>Row the health bar is drawn on: the first row below the map.</summary>
    public int StatusRow => MapHeight;

    /// <summary>First row of the message log.</summary>
    public int LogTopRow => StatusRow + 1;

    /// <summary>
    /// Divides a window into a map, a status row and a log. Throws ArgumentOutOfRangeException
    /// when the log is smaller than a row, or when the panel would leave no room for the map -
    /// a dungeon of zero rows is not a smaller game, it is an unplayable one.
    /// </summary>
    public ScreenLayout(int windowWidth, int windowHeight, int logRows)
    {
        if (windowWidth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(windowWidth), windowWidth, "The window needs at least one column.");
        }

        if (logRows < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(logRows), logRows, "The log needs at least one row.");
        }

        // One row for the status bar, LogRows for the log, and at least one left for the map.
        if (windowHeight - logRows - 1 < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(windowHeight),
                windowHeight,
                $"A window of {windowHeight} rows cannot hold a {logRows}-row log, a status row and a map.");
        }

        WindowWidth = windowWidth;
        WindowHeight = windowHeight;
        LogRows = logRows;

        // The three regions must tile the window exactly, with nothing lost between them.
        Debug.Assert(
            MapHeight + 1 + LogRows == WindowHeight,
            "The map, the status row and the log must account for every row of the window.");
    }

    /// <summary>True when the row belongs to the map rather than the panel below it.</summary>
    public bool IsMapRow(int row)
    {
        return row >= 0 && row < MapHeight;
    }
}
```

### [`RogueTutorial/HealthBar.cs`](../parts/part-07-log-and-health-bar/RogueTutorial/HealthBar.cs)

The bar as characters. Fill and caption are separate on purpose.

```csharp
/*
 * The health bar, as characters rather than pixels.
 *
 * Two things are asked of it and they are kept apart on purpose: how much of the bar is filled,
 * and what it says. Filling is arithmetic and is tested against exact fractions; the caption is
 * text and is tested as a string. A bar that drew the right length with the wrong numbers, or
 * the reverse, would otherwise pass on the strength of the half that was correct.
 *
 * Usage:
 *
 *     string bar = HealthBar.Render(current: 24, maximum: 30, width: 20);
 *     // -> "HP: 24/30 ========  "   the caption, then filled and empty cells
 *
 *     int filled = HealthBar.FilledCells(current: 24, maximum: 30, barCells: 10);
 *     // -> 8: eight tenths of thirty is twenty-four
 *
 * Refuses a maximum below one, a current outside zero to maximum, and a width too narrow to
 * hold the caption.
 */

using System;
using System.Diagnostics;

namespace RogueTutorial;

internal static class HealthBar
{
    // Drawn for each cell of health remaining.
    private const char FilledCell = '=';

    // Drawn for each cell of health lost. A space would make the bar's end ambiguous.
    private const char EmptyCell = '-';

    /// <summary>
    /// How many cells of the bar are filled: the fraction of health remaining, rounded down, so
    /// a bar only shows full when health is full. A living fighter always shows at least one
    /// cell, since a bar that reads empty while the player is alive is a lie - unless the bar
    /// has no cells at all, in which case there is nothing to show either way.
    /// Throws ArgumentOutOfRangeException on a maximum below one, a negative bar, or a current
    /// outside the range zero to maximum.
    /// </summary>
    public static int FilledCells(int current, int maximum, int barCells)
    {
        RejectBadNumbers(current, maximum);

        if (barCells < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(barCells), barCells, "A bar cannot have negative width.");
        }

        // Dead is empty, and no rounding rule should ever contradict that.
        if (current == 0)
        {
            return 0;
        }

        // A bar with no cells has nothing to fill, and the floor below would otherwise return
        // one cell of a zero-cell bar - which the caller then pads by a negative number.
        if (barCells == 0)
        {
            return 0;
        }

        // Rounding down means the bar reads full only at full health, which is the useful way
        // round: a player at 29 of 30 should be able to see they are not untouched.
        int filled = current * barCells / maximum;

        // Rounding down can reach zero while the fighter is still alive, and an empty bar on a
        // living player reads as a bug rather than as low health.
        return Math.Max(1, Math.Min(filled, barCells));
    }

    /// <summary>
    /// The whole bar as one line: the caption, a space, then the filled and empty cells. The line
    /// is exactly width characters. Throws ArgumentOutOfRangeException when the width cannot hold
    /// the caption, since truncating it would hide the numbers the bar exists to show.
    /// </summary>
    public static string Render(int current, int maximum, int width)
    {
        RejectBadNumbers(current, maximum);

        string caption = $"HP: {current}/{maximum} ";

        // The numbers matter more than the bar; if only one fits, it is the numbers.
        if (width < caption.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width), width, $"A width of {width} cannot hold the caption '{caption.TrimEnd()}'.");
        }

        int barCells = width - caption.Length;
        int filled = FilledCells(current, maximum, barCells);

        string line = caption + new string(FilledCell, filled) + new string(EmptyCell, barCells - filled);

        // The caller writes this into a fixed region, so a line of the wrong length would either
        // overflow into the log or leave stale characters behind.
        Debug.Assert(line.Length == width, "The rendered bar must be exactly the width asked for.");

        return line;
    }

    // Shared guard: the two ways the numbers themselves can be nonsense.
    private static void RejectBadNumbers(int current, int maximum)
    {
        if (maximum < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "Maximum health must be at least one.");
        }

        if (current < 0 || current > maximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(current), current, $"Current health must be between 0 and {maximum}.");
        }
    }
}
```

### [`RogueTutorial/ScreenComposer.cs`](../parts/part-07-log-and-health-bar/RogueTutorial/ScreenComposer.cs)

The whole screen as data - map, bar and log together.

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
    // How wide the health bar is drawn, caption included. Fixed rather than the window's width:
    // a bar stretched across eighty columns reads as a wall rather than as a gauge, and the rest
    // of the row is where a dungeon level and other status go in a later part.
    private const int HealthBarWidth = 24;

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

### [`RogueTutorial/RootScreen.cs`](../parts/part-07-log-and-health-bar/RogueTutorial/RootScreen.cs)

The Part 6 file, now generating the map at the layout's size and blitting the whole screen.

<!-- generated-diff -->
**Changed from Part 6.** The complete file follows; this is only what moved:

```diff
--- part-06-combat/RootScreen.cs
+++ current/RootScreen.cs
@@ -1,6 +1,7 @@
 /*
  * The top-level screen: it wires SadConsole's window and keyboard to the game world, and blits
- * the frame the world composes. It owns no rules and, from Part 5, no state either - the map,
+ * the frame ScreenComposer builds - which from Part 7 is the whole screen, interface included,
+ * rather than just the map. It owns no rules and, from Part 5, no state either - the map,
  * the entities and what the player has seen all live on GameWorld, which can be built and
  * driven in a test process.
  *
@@ -24,8 +25,15 @@
 
 internal sealed class RootScreen : ScreenObject
 {
+    // How many rows of message log are shown. Five is enough to follow a fight without taking
+    // so much of the window that the dungeon becomes cramped.
+    private const int LogRows = 5;
+
     // The surface every glyph is drawn onto. One cell per grid position.
     private readonly ScreenSurface _mapSurface;
+
+    // Where the map, the health bar and the log each sit in the window.
+    private readonly ScreenLayout _layout;
 
     // The dungeon, everyone standing in it, and what the player has seen.
     private readonly GameWorld _world;
@@ -41,10 +49,14 @@
         // Children are drawn and updated by the base class once added.
         Children.Add(_mapSurface);
 
+        _layout = new ScreenLayout(
+            _mapSurface.Surface.Width, _mapSurface.Surface.Height, logRows: LogRows);
+
+        // The dungeon fills the map area rather than the window: the panel takes the rest.
         // No seed is given, so every run is a different dungeon with different monsters. Pass a
         // number to Random's constructor to play the same one repeatedly while debugging.
         _world = GameWorld.Generate(
-            _mapSurface.Surface.Width, _mapSurface.Surface.Height, new Random(), MonsterTable.Standard);
+            _layout.WindowWidth, _layout.MapHeight, new Random(), MonsterTable.Standard);
 
         DrawFrame();
     }
@@ -69,9 +81,9 @@
 
         PlayerAction action = _world.MovePlayer(moveOffset);
 
-        // Only a move changes the picture. A bump will change it in Part 6, once attacking does
-        // something; a wall never does.
-        if (action.Kind == PlayerActionKind.Moved)
+        // Anything that spends a turn changes the picture: a move redraws the map, and an
+        // attack changes health and adds to the log. A wall changes neither.
+        if (action.Kind == PlayerActionKind.Moved || action.Kind == PlayerActionKind.Attacked)
         {
             DrawFrame();
         }
@@ -85,7 +97,7 @@
     /// </summary>
     private void DrawFrame()
     {
-        RenderedFrame frame = _world.ComposeFrame();
+        RenderedFrame frame = ScreenComposer.Compose(_world, _layout);
 
         for (int row = 0; row < frame.Height; row++)
         {
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
            _layout.WindowWidth, _layout.MapHeight, new Random(), MonsterTable.Standard);

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

        // Anything that spends a turn changes the picture: a move redraws the map, and an
        // attack changes health and adds to the log. A wall changes neither.
        if (action.Kind == PlayerActionKind.Moved || action.Kind == PlayerActionKind.Attacked)
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

**Each block below is the complete file.** Create each in `RogueTutorial.Tests/`.

### [`RogueTutorial.Tests/ScreenLayoutTests.cs`](../parts/part-07-log-and-health-bar/RogueTutorial.Tests/ScreenLayoutTests.cs)

That the three regions tile the window exactly.

```csharp
/*
 * Unit tests for how the window is divided. Expected values are worked out from the description
 * - map, then one status row, then the log - rather than from what the code returned.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~ScreenLayoutTests
 */

using System;
using RogueTutorial;
using Xunit;

public sealed class ScreenLayoutTests
{
    [Fact]
    public void TheRegionsTileTheWindowExactly()
    {
        // Nothing lost between them and nothing overlapping: a row belongs to one region.
        ScreenLayout layout = new ScreenLayout(80, 25, logRows: 5);

        Assert.Equal(25, layout.MapHeight + 1 + layout.LogRows);
    }

    [Fact]
    public void TheMapGetsWhatThePanelDoesNotTake()
    {
        ScreenLayout layout = new ScreenLayout(80, 25, logRows: 5);

        // 25 rows less five of log less one of status.
        Assert.Equal(19, layout.MapHeight);
    }

    [Fact]
    public void TheStatusRowIsTheFirstRowBelowTheMap()
    {
        ScreenLayout layout = new ScreenLayout(80, 25, logRows: 5);

        Assert.Equal(19, layout.StatusRow);
        Assert.Equal(20, layout.LogTopRow);
    }

    [Fact]
    public void TheLastLogRowIsTheLastRowOfTheWindow()
    {
        // An off-by-one here writes a log line past the bottom of the screen.
        ScreenLayout layout = new ScreenLayout(80, 25, logRows: 5);

        Assert.Equal(24, layout.LogTopRow + layout.LogRows - 1);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(18, true)]
    [InlineData(19, false)]
    [InlineData(24, false)]
    [InlineData(-1, false)]
    public void IsMapRowAcceptsExactlyTheMapRows(int row, bool expected)
    {
        ScreenLayout layout = new ScreenLayout(80, 25, logRows: 5);

        Assert.Equal(expected, layout.IsMapRow(row));
    }

    [Fact]
    public void ABiggerLogLeavesASmallerMap()
    {
        ScreenLayout small = new ScreenLayout(80, 25, logRows: 3);
        ScreenLayout large = new ScreenLayout(80, 25, logRows: 10);

        Assert.Equal(21, small.MapHeight);
        Assert.Equal(14, large.MapHeight);
    }

    [Fact]
    public void TheSmallestWorkableWindowIsAccepted()
    {
        // One map row, one status row, one log row.
        ScreenLayout layout = new ScreenLayout(1, 3, logRows: 1);

        Assert.Equal(1, layout.MapHeight);
        Assert.Equal(1, layout.StatusRow);
        Assert.Equal(2, layout.LogTopRow);
    }

    [Fact]
    public void AWindowWithNoRoomForAMapIsRejected()
    {
        // Two rows: one status, one log, none left. A map of zero rows is unplayable rather
        // than merely small, so this fails loudly instead of producing it.
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenLayout(80, 2, logRows: 1));
    }

    [Fact]
    public void ALogTakingTheWholeWindowIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenLayout(80, 25, logRows: 24));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ALogOfNoRowsIsRejected(int logRows)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenLayout(80, 25, logRows));
    }

    [Fact]
    public void AWindowWithNoColumnsIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenLayout(0, 25, logRows: 5));
    }
}
```

### [`RogueTutorial.Tests/HealthBarTests.cs`](../parts/part-07-log-and-health-bar/RogueTutorial.Tests/HealthBarTests.cs)

The fill fraction and the caption, tested apart.

```csharp
/*
 * Unit tests for the health bar. The fill fraction and the caption are tested separately: a bar
 * that draws the right length with the wrong numbers, or the reverse, would otherwise pass on
 * the strength of whichever half was correct.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~HealthBarTests
 */

using System;
using RogueTutorial;
using Xunit;

public sealed class HealthBarTests
{
    [Theory]
    [InlineData(30, 30, 10, 10)]    // full health fills the bar
    [InlineData(15, 30, 10, 5)]     // half
    [InlineData(24, 30, 10, 8)]     // eight tenths
    [InlineData(3, 30, 10, 1)]      // a tenth
    [InlineData(0, 30, 10, 0)]      // dead is empty, whatever the rounding
    public void TheFillIsTheFractionOfHealthRemaining(int current, int maximum, int cells, int expected)
    {
        Assert.Equal(expected, HealthBar.FilledCells(current, maximum, cells));
    }

    [Fact]
    public void RoundingDownMeansOnlyFullHealthReadsFull()
    {
        // 29 of 30 across ten cells is 9.67, which must not round up to a full bar: a player
        // who has been hit should be able to see it.
        Assert.Equal(9, HealthBar.FilledCells(29, 30, 10));
        Assert.Equal(10, HealthBar.FilledCells(30, 30, 10));
    }

    [Fact]
    public void ALivingFighterAlwaysShowsAtLeastOneCell()
    {
        // 1 of 30 across ten cells rounds down to zero, and an empty bar on a living player
        // reads as a bug rather than as low health.
        Assert.Equal(1, HealthBar.FilledCells(1, 30, 10));
    }

    [Fact]
    public void DeadIsTheOnlyEmptyBar()
    {
        Assert.Equal(0, HealthBar.FilledCells(0, 30, 10));
        Assert.True(HealthBar.FilledCells(1, 30, 10) > 0);
    }

    [Fact]
    public void TheCaptionCarriesTheRealNumbers()
    {
        string bar = HealthBar.Render(current: 24, maximum: 30, width: 30);

        Assert.StartsWith("HP: 24/30 ", bar);
    }

    [Fact]
    public void TheLineIsExactlyTheWidthAsked()
    {
        // The caller writes into a fixed row, so a short line leaves stale characters behind and
        // a long one overflows into the log.
        foreach (int width in new[] { 20, 40, 80 })
        {
            Assert.Equal(width, HealthBar.Render(24, 30, width).Length);
        }
    }

    [Fact]
    public void TheBarShowsFilledThenEmpty()
    {
        // 15 of 30 with a caption of "HP: 15/30 " (10 characters) leaves 10 bar cells, half full.
        string bar = HealthBar.Render(current: 15, maximum: 30, width: 20);

        Assert.Equal("HP: 15/30 =====-----", bar);
    }

    [Fact]
    public void AFullBarHasNoEmptyCells()
    {
        string bar = HealthBar.Render(current: 30, maximum: 30, width: 20);

        Assert.Equal("HP: 30/30 ==========", bar);
    }

    [Fact]
    public void ADeadPlayersBarHasNoFilledCells()
    {
        string bar = HealthBar.Render(current: 0, maximum: 30, width: 19);

        Assert.DoesNotContain("=", bar);
        Assert.StartsWith("HP: 0/30 ", bar);
    }

    [Fact]
    public void AWidthThatWouldHideTheNumbersIsRejected()
    {
        // The numbers matter more than the bar. Truncating the caption would hide the thing the
        // bar exists to show, so it fails instead.
        Assert.Throws<ArgumentOutOfRangeException>(() => HealthBar.Render(24, 30, width: 5));
    }

    [Fact]
    public void AWidthWithNoRoomForBarCellsIsStillValid()
    {
        // Exactly the caption and nothing else: legal, if useless.
        string bar = HealthBar.Render(24, 30, width: 10);

        Assert.Equal("HP: 24/30 ", bar);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AMaximumBelowOneIsRejected(int maximum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HealthBar.FilledCells(0, maximum, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => HealthBar.Render(0, maximum, 20));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(31)]
    public void HealthOutsideItsRangeIsRejected(int current)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HealthBar.FilledCells(current, 30, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => HealthBar.Render(current, 30, 20));
    }

    [Fact]
    public void ANegativeBarWidthIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HealthBar.FilledCells(15, 30, -1));
    }
}
```

### [`RogueTutorial.Tests/ScreenComposerTests.cs`](../parts/part-07-log-and-health-bar/RogueTutorial.Tests/ScreenComposerTests.cs)

What actually reaches the screen.

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
    public void ANullArgumentIsRejected()
    {
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);

        Assert.Throws<ArgumentNullException>(() => ScreenComposer.Compose(null!, layout));
        Assert.Throws<ArgumentNullException>(() => ScreenComposer.Compose(WorldFor(layout), null!));
    }
}
```

## Step 4: build and run

```
dotnet build
dotnet test
dotnet run --project RogueTutorial
```

Expected: a clean build, 290 passing tests, and a health bar and log beneath the dungeon.

### If something is wrong

| Symptom | Cause |
|---|---|
| The bottom rows of the dungeon are missing | The map is generated at the window height rather than `layout.MapHeight` |
| `ArgumentException` about the map not fitting | The same, caught rather than silently cropped |
| The health bar is not drawn | The map is overwriting the status row - check `MapHeight` |
| The bar stretches across the window | `HealthBarWidth` is not being applied |
| The newest message is at the top of the log | `Latest` returns oldest first; do not reverse it |
| The bar reads full after taking damage | `FilledCells` is rounding up |
| `ArgumentOutOfRangeException: count ('-1')` | The one-cell floor is being applied to a zero-cell bar |

## Step 5: regenerate the documentation

Skip this if you did not set up docfx in Part 1. Nothing was deleted this part:

```
dotnet docfx docfx.json --serve --port 8081
```

Expected: `Build succeeded. 0 warning(s) 0 error(s)`, and pages for `ScreenLayout`, `HealthBar`
and `ScreenComposer` at <http://localhost:8081>.

---

Next: **Part 8, items and inventory.**

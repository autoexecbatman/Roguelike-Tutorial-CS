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

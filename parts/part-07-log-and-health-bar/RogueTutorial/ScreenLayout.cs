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

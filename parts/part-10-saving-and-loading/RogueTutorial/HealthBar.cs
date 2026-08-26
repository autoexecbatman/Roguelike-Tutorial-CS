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

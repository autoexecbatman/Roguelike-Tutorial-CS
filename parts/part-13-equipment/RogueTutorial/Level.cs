/*
 * How far along a fighter is: their level, the experience they have earned, and how much more
 * the next level costs.
 *
 * A component like Fighter and Inventory. Monsters do not have one - they award experience
 * rather than collecting it - and a corpse stops having one along with everything else.
 *
 * The threshold grows with each level, which is what stops the twentieth level arriving as
 * quickly as the second. The formula is stated once here rather than as a table, so changing
 * how quickly a game levels is one line.
 *
 * Usage:
 *
 *     Level level = new Level();
 *
 *     bool ready = level.Award(35);          // -> true when that took it past the threshold
 *     int needed = level.ExperienceToNext;   // what is still required
 *     level.Advance();                       // spends the threshold and raises CurrentLevel
 *
 * Award never advances by itself. Reaching a level is a decision the player makes - what to
 * improve - and taking it automatically would remove one of the few choices this part is about.
 *
 * Refuses negative experience and an Advance that has not been earned.
 */

using System;
using System.Diagnostics;

namespace RogueTutorial;

internal sealed class Level
{
    // What the first level up costs. Roughly four rats or three kobolds.
    private const int BaseThreshold = 40;

    // Added to the threshold for each level already gained, so progress slows as it goes.
    private const int ThresholdGrowth = 25;

    /// <summary>How many levels have been gained. A new fighter starts at one.</summary>
    public int CurrentLevel { get; private set; } = 1;

    /// <summary>Experience earned toward the next level, not counting what has been spent.</summary>
    public int Experience { get; private set; }

    /// <summary>What the next level costs in total.</summary>
    public int ExperienceForNextLevel => BaseThreshold + ((CurrentLevel - 1) * ThresholdGrowth);

    /// <summary>How much more is needed. Zero once the level has been earned.</summary>
    public int ExperienceToNext => Math.Max(0, ExperienceForNextLevel - Experience);

    /// <summary>True when enough has been earned to advance.</summary>
    public bool CanAdvance => Experience >= ExperienceForNextLevel;

    /// <summary>
    /// Adds experience and reports whether that was enough to advance. Advancing is not done
    /// here: what to improve is the player's choice, and taking it for them removes the decision.
    /// Throws ArgumentOutOfRangeException on negative experience, which would be a loss arriving
    /// through the gaining door.
    /// </summary>
    public bool Award(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Experience cannot be negative.");
        }

        Experience += amount;

        return CanAdvance;
    }

    /// <summary>
    /// Spends the threshold and raises the level, keeping any surplus toward the next one - so a
    /// single large kill is never partly wasted. Throws InvalidOperationException when the level
    /// has not been earned, because advancing without paying is a bug in the caller rather than
    /// a generous outcome.
    /// </summary>
    public void Advance()
    {
        if (!CanAdvance)
        {
            throw new InvalidOperationException(
                $"Level {CurrentLevel + 1} costs {ExperienceForNextLevel} and only {Experience} is earned.");
        }

        Experience -= ExperienceForNextLevel;

        CurrentLevel++;

        // Surplus carries over, so it is never negative and never simply discarded.
        Debug.Assert(Experience >= 0, "Experience must never go negative when a level is spent.");
    }
}

/*
 * What a level buys, and applying it.
 *
 * Three options, each a different kind of durable. More health survives one more mistake, more
 * attack kills things sooner, more defence makes every future hit smaller. They are deliberately
 * not equivalent - a choice between three versions of the same thing is not a choice.
 *
 * Usage:
 *
 *     foreach (LevelUpChoice choice in LevelUpChoices.All)
 *     {
 *         string line = LevelUpChoices.Describe(choice, player.Fighter!);
 *         // -> "a) tougher   30 -> 50 hit points"
 *     }
 *
 *     LevelUpChoices.Apply(LevelUpChoice.Tougher, player.Fighter!);
 *
 * Refuses a null fighter and a choice that is not one of the three.
 */

using System;
using System.Collections.Generic;

namespace RogueTutorial;

/// <summary>What a level up improves.</summary>
internal enum LevelUpChoice
{
    /// <summary>More hit points, and the missing ones filled in as well.</summary>
    Tougher,

    /// <summary>More attack: things die in fewer blows.</summary>
    Stronger,

    /// <summary>More defence: every future hit is smaller.</summary>
    Sturdier,
}

internal static class LevelUpChoices
{
    // How much each option gives. Health is a larger number because a hit point is worth less
    // than a point of attack or defence, which apply to every exchange rather than once.
    private const int HealthGain = 20;
    private const int AttackGain = 1;
    private const int DefenceGain = 1;

    /// <summary>The three options, in the order they are offered and lettered.</summary>
    public static IReadOnlyList<LevelUpChoice> All { get; } = new[]
    {
        LevelUpChoice.Tougher,
        LevelUpChoice.Stronger,
        LevelUpChoice.Sturdier,
    };

    /// <summary>
    /// One line describing what a choice would do, with the numbers it would change - a menu
    /// that says "stronger" without saying how much is asking for a decision with the
    /// information withheld. Throws ArgumentNullException on a null fighter.
    /// </summary>
    public static string Describe(LevelUpChoice choice, Fighter fighter)
    {
        ArgumentNullException.ThrowIfNull(fighter);

        return choice switch
        {
            LevelUpChoice.Tougher =>
                $"tougher    {fighter.MaximumHitPoints} -> {fighter.MaximumHitPoints + HealthGain} hit points",
            LevelUpChoice.Stronger =>
                $"stronger   attack {fighter.Attack} -> {fighter.Attack + AttackGain}",
            LevelUpChoice.Sturdier =>
                $"sturdier   defence {fighter.Defence} -> {fighter.Defence + DefenceGain}",
            _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, "No such level up choice."),
        };
    }

    /// <summary>
    /// Applies a choice to a fighter and returns the line to log. Choosing toughness heals the
    /// new hit points as well as adding them: a level that leaves you at the same health is a
    /// reward you cannot feel. Throws ArgumentNullException on a null fighter.
    /// </summary>
    public static string Apply(LevelUpChoice choice, Fighter fighter)
    {
        ArgumentNullException.ThrowIfNull(fighter);

        switch (choice)
        {
            case LevelUpChoice.Tougher:
                fighter.RaiseMaximumHitPoints(HealthGain);
                return $"You feel tougher. Maximum health is now {fighter.MaximumHitPoints}.";

            case LevelUpChoice.Stronger:
                fighter.RaiseAttack(AttackGain);
                return $"You feel stronger. Attack is now {fighter.Attack}.";

            case LevelUpChoice.Sturdier:
                fighter.RaiseDefence(DefenceGain);
                return $"You feel sturdier. Defence is now {fighter.Defence}.";

            default:
                throw new ArgumentOutOfRangeException(nameof(choice), choice, "No such level up choice.");
        }
    }
}

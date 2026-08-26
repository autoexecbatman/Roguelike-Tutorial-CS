/*
 * Unit tests for the combat numbers. Expected values are computed from the rule - damage is
 * attack less defence, floored at zero - rather than from what the code returned.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~FighterTests
 */

using System;
using RogueTutorial;
using Xunit;

public sealed class FighterTests
{
    [Fact]
    public void AFighterStartsAtFullHealth()
    {
        Fighter fighter = new Fighter(maximumHitPoints: 10, attack: 3, defence: 1, experienceAwarded: 0);

        Assert.Equal(10, fighter.MaximumHitPoints);
        Assert.Equal(10, fighter.HitPoints);
        Assert.False(fighter.IsDead);
    }

    [Fact]
    public void DamageComesOffHitPoints()
    {
        Fighter fighter = new Fighter(10, 3, 1, experienceAwarded: 0);

        int lost = fighter.TakeDamage(4);

        Assert.Equal(4, lost);
        Assert.Equal(6, fighter.HitPoints);
    }

    [Fact]
    public void HitPointsFloorAtZero()
    {
        // A corpse is never more dead than another, and a negative total would print as one.
        Fighter fighter = new Fighter(10, 3, 1, experienceAwarded: 0);

        int lost = fighter.TakeDamage(99);

        Assert.Equal(10, lost);
        Assert.Equal(0, fighter.HitPoints);
        Assert.True(fighter.IsDead);
    }

    [Fact]
    public void ExactlyLethalDamageKills()
    {
        Fighter fighter = new Fighter(4, 3, 0, experienceAwarded: 0);

        fighter.TakeDamage(4);

        Assert.True(fighter.IsDead);
    }

    [Fact]
    public void OneShortOfLethalDoesNot()
    {
        // The boundary either side of death, which is where an off-by-one would live.
        Fighter fighter = new Fighter(4, 3, 0, experienceAwarded: 0);

        fighter.TakeDamage(3);

        Assert.False(fighter.IsDead);
        Assert.Equal(1, fighter.HitPoints);
    }

    [Fact]
    public void ZeroDamageChangesNothing()
    {
        Fighter fighter = new Fighter(10, 3, 1, experienceAwarded: 0);

        Assert.Equal(0, fighter.TakeDamage(0));
        Assert.Equal(10, fighter.HitPoints);
    }

    [Theory]
    [InlineData(5, 2, 3)]     // ordinary: 5 attack against 2 defence
    [InlineData(5, 0, 5)]     // no defence at all
    [InlineData(3, 3, 0)]     // defence exactly matches attack
    [InlineData(2, 9, 0)]     // out-defended: floored at zero, never negative
    public void DamageIsAttackLessDefenceFlooredAtZero(int attack, int defence, int expected)
    {
        Fighter attacker = new Fighter(10, attack, 0, experienceAwarded: 0);
        Fighter target = new Fighter(10, 0, defence, experienceAwarded: 0);

        Assert.Equal(expected, attacker.DamageAgainst(target));
    }

    [Fact]
    public void AFighterThatOutDefendsIsNeverHealed()
    {
        // The reason for the floor: without it a heavily armoured target would gain health.
        Fighter weak = new Fighter(10, 1, 0, experienceAwarded: 0);
        Fighter armoured = new Fighter(10, 0, 5, experienceAwarded: 0);

        armoured.TakeDamage(weak.DamageAgainst(armoured));

        Assert.Equal(10, armoured.HitPoints);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AFighterThatBeginsDeadIsRejected(int maximumHitPoints)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fighter(maximumHitPoints, 3, 1, experienceAwarded: 0));
    }

    [Fact]
    public void NegativeAttackOrDefenceIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fighter(10, -1, 0, experienceAwarded: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fighter(10, 0, -1, experienceAwarded: 0));
    }

    [Fact]
    public void NegativeDamageIsRejected()
    {
        // Healing has its own path in a later part; it must not arrive through this one.
        Fighter fighter = new Fighter(10, 3, 1, experienceAwarded: 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => fighter.TakeDamage(-1));
    }

    [Fact]
    public void ANullTargetIsRejected()
    {
        Fighter fighter = new Fighter(10, 3, 1, experienceAwarded: 0);

        Assert.Throws<ArgumentNullException>(() => fighter.DamageAgainst(null!));
    }
}

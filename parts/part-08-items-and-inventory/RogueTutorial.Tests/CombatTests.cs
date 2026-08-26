/*
 * Unit tests for resolving one attack, including what death does to an entity.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~CombatTests
 */

using System;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class CombatTests
{
    private static Entity FighterEntity(string name, int hitPoints, int attack, int defence)
    {
        Entity entity = new Entity(name, name[0], Color.White, new Point(0, 0), blocksMovement: true);
        entity.Fighter = new Fighter(hitPoints, attack, defence);
        return entity;
    }

    [Fact]
    public void AHitRemovesHitPoints()
    {
        Entity attacker = FighterEntity("Player", 30, 5, 2);
        Entity target = FighterEntity("Rat", 10, 3, 1);

        AttackResult result = Combat.Resolve(attacker, target);

        // 5 attack less 1 defence.
        Assert.Equal(4, result.DamageDealt);
        Assert.Equal(6, target.Fighter!.HitPoints);
        Assert.False(result.TargetDied);
    }

    [Fact]
    public void AnAbsorbedBlowStillHappened()
    {
        // Zero damage is not "nothing occurred": the log should say the blow landed and failed.
        Entity attacker = FighterEntity("Rat", 10, 2, 0);
        Entity target = FighterEntity("Knight", 30, 5, 9);

        AttackResult result = Combat.Resolve(attacker, target);

        Assert.Equal(0, result.DamageDealt);
        Assert.False(result.TargetDied);
        Assert.Contains("no damage", result.Message);
        Assert.Equal(30, target.Fighter!.HitPoints);
    }

    [Fact]
    public void ALethalBlowKillsAndSaysSo()
    {
        Entity attacker = FighterEntity("Player", 30, 5, 2);
        Entity target = FighterEntity("Rat", 4, 3, 1);

        AttackResult result = Combat.Resolve(attacker, target);

        Assert.True(result.TargetDied);
        Assert.Contains("dies", result.Message);
    }

    [Fact]
    public void TheMessageNamesTheTargetBeforeItBecomesACorpse()
    {
        // Die renames the entity, so a message built afterwards would read "remains of Rat dies".
        Entity attacker = FighterEntity("Player", 30, 9, 0);
        Entity target = FighterEntity("Rat", 4, 3, 0);

        AttackResult result = Combat.Resolve(attacker, target);

        Assert.Contains("Rat dies", result.Message);
        Assert.DoesNotContain("remains of Rat dies", result.Message);
    }

    [Fact]
    public void DeathTurnsAMonsterIntoACorpse()
    {
        Entity attacker = FighterEntity("Player", 30, 9, 0);
        Entity target = FighterEntity("Rat", 4, 3, 0);

        Combat.Resolve(attacker, target);

        // The three things that make it a corpse rather than a fighter at zero health.
        Assert.Null(target.Fighter);
        Assert.False(target.BlocksMovement);
        Assert.Equal('%', target.Glyph);
        Assert.Equal("remains of Rat", target.Name);
    }

    [Fact]
    public void ACorpseCanBeWalkedOver()
    {
        // The whole reason death converts rather than deletes: the cell must free up.
        Entity attacker = FighterEntity("Player", 30, 9, 0);
        Entity target = FighterEntity("Rat", 4, 3, 0);

        Assert.True(target.BlocksMovement);

        Combat.Resolve(attacker, target);

        Assert.False(target.BlocksMovement);
    }

    [Fact]
    public void ACorpseCannotBeAttackedAgain()
    {
        Entity attacker = FighterEntity("Player", 30, 9, 0);
        Entity target = FighterEntity("Rat", 4, 3, 0);

        Combat.Resolve(attacker, target);

        Assert.Throws<ArgumentException>(() => Combat.Resolve(attacker, target));
    }

    [Fact]
    public void SomethingWithNoFighterCannotAttack()
    {
        Entity item = new Entity("Sword", '/', Color.Gray, new Point(0, 0), blocksMovement: false);
        Entity target = FighterEntity("Rat", 4, 3, 0);

        Assert.Throws<ArgumentException>(() => Combat.Resolve(item, target));
    }

    [Fact]
    public void AnItemCannotDie()
    {
        Entity item = new Entity("Sword", '/', Color.Gray, new Point(0, 0), blocksMovement: false);

        Assert.Throws<InvalidOperationException>(() => item.Die());
    }

    [Fact]
    public void ANullArgumentIsRejected()
    {
        Entity fighter = FighterEntity("Rat", 4, 3, 0);

        Assert.Throws<ArgumentNullException>(() => Combat.Resolve(null!, fighter));
        Assert.Throws<ArgumentNullException>(() => Combat.Resolve(fighter, null!));
    }
}

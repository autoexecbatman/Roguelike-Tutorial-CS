/*
 * Unit tests for experience and levelling.
 *
 * The rule worth watching is that earning a level and spending it are separate. Award never
 * advances by itself, because what to improve is the decision this part exists to offer.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~LevelTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadConsole.Input;
using SadRogue.Primitives;
using Xunit;

public sealed class LevelTests
{
    private static GameWorld WorldWith(params Entity[] monsters)
    {
        GameMap map = new GameMap(9, 9);
        map.Fill(TileTypes.Floor);

        Entity player = new Entity("Player", '@', Color.White, new Point(4, 4), blocksMovement: true, RenderLayer.Player);
        player.Fighter = new Fighter(30, 99, 2, experienceAwarded: 0);
        player.Inventory = new Inventory(26);
        player.Level = new Level();

        return new GameWorld(map, new List<Entity>(monsters) { player }, player);
    }

    private static Entity Monster(string name, Point at, int award)
    {
        Entity monster = new Entity(name, name[0], Color.Red, at, blocksMovement: true, RenderLayer.Actor);
        monster.Fighter = new Fighter(1, 1, 0, award);
        return monster;
    }

    [Fact]
    public void ANewFighterStartsAtLevelOne()
    {
        Level level = new Level();

        Assert.Equal(1, level.CurrentLevel);
        Assert.Equal(0, level.Experience);
        Assert.False(level.CanAdvance);
    }

    [Fact]
    public void EarningIsNotAdvancing()
    {
        // The whole point: reaching the threshold offers a choice rather than taking it.
        Level level = new Level();

        Assert.True(level.Award(level.ExperienceForNextLevel));

        Assert.True(level.CanAdvance);
        Assert.Equal(1, level.CurrentLevel);
    }

    [Fact]
    public void AdvancingSpendsTheThreshold()
    {
        Level level = new Level();
        int cost = level.ExperienceForNextLevel;

        level.Award(cost);
        level.Advance();

        Assert.Equal(2, level.CurrentLevel);
        Assert.Equal(0, level.Experience);
    }

    [Fact]
    public void SurplusCarriesOver()
    {
        // A single large kill must not be partly wasted.
        Level level = new Level();
        int cost = level.ExperienceForNextLevel;

        level.Award(cost + 7);
        level.Advance();

        Assert.Equal(7, level.Experience);
    }

    [Fact]
    public void EachLevelCostsMoreThanTheLast()
    {
        // Otherwise the twentieth arrives as quickly as the second.
        Level level = new Level();

        int first = level.ExperienceForNextLevel;

        level.Award(first);
        level.Advance();

        Assert.True(level.ExperienceForNextLevel > first);
    }

    [Fact]
    public void EnoughForTwoLevelsAdvancesOnlyOnce()
    {
        // Each level is a separate decision, so they are spent one at a time.
        Level level = new Level();

        level.Award(1000);
        level.Advance();

        Assert.Equal(2, level.CurrentLevel);
        Assert.True(level.CanAdvance);
    }

    [Fact]
    public void AdvancingWithoutEarningIsRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new Level().Advance());
    }

    [Fact]
    public void NegativeExperienceIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Level().Award(-1));
    }

    [Fact]
    public void KillingSomethingAwardsItsExperience()
    {
        Entity rat = Monster("Rat", new Point(5, 4), award: 10);
        GameWorld world = WorldWith(rat);

        world.MovePlayer(new Point(1, 0));

        Assert.Equal(10, world.Player.Level!.Experience);
    }

    [Fact]
    public void HittingWithoutKillingAwardsNothing()
    {
        Entity tough = new Entity("Brute", 'B', Color.Red, new Point(5, 4), blocksMovement: true, RenderLayer.Actor);
        tough.Fighter = new Fighter(500, 1, 0, experienceAwarded: 50);

        GameWorld world = WorldWith(tough);

        world.MovePlayer(new Point(1, 0));

        Assert.Equal(0, world.Player.Level!.Experience);
    }

    [Fact]
    public void EnoughExperienceOpensTheMenu()
    {
        Entity rat = Monster("Rat", new Point(5, 4), award: 40);
        GameWorld world = WorldWith(rat);

        world.MovePlayer(new Point(1, 0));

        Assert.Equal(GameMode.ChoosingLevelUp, world.Mode);
    }

    [Fact]
    public void ChoosingAppliesTheImprovementAndReturnsToPlay()
    {
        Entity rat = Monster("Rat", new Point(5, 4), award: 40);
        GameWorld world = WorldWith(rat);

        world.MovePlayer(new Point(1, 0));

        int attackBefore = world.Player.Fighter!.Attack;

        // 'b' is Stronger, the second option.
        Assert.True(world.ChooseLevelUp(1));

        Assert.Equal(attackBefore + 1, world.Player.Fighter.Attack);
        Assert.Equal(2, world.Player.Level!.CurrentLevel);
        Assert.Equal(GameMode.Playing, world.Mode);
    }

    [Fact]
    public void TougherHealsTheNewHitPointsToo()
    {
        // A level that leaves you at the same health is a reward you cannot feel.
        Fighter fighter = new Fighter(30, 5, 2, experienceAwarded: 0);
        fighter.TakeDamage(10);

        fighter.RaiseMaximumHitPoints(20);

        Assert.Equal(50, fighter.MaximumHitPoints);
        Assert.Equal(40, fighter.HitPoints);
    }

    [Fact]
    public void ASecondEarnedLevelReopensTheMenu()
    {
        // One kill can pay for two. Dropping to the map with an unspent level in hand would
        // leave the player owed a decision nothing reminds them about.
        Entity rat = Monster("Rat", new Point(5, 4), award: 1000);
        GameWorld world = WorldWith(rat);

        world.MovePlayer(new Point(1, 0));
        world.ChooseLevelUp(0);

        Assert.Equal(GameMode.ChoosingLevelUp, world.Mode);
        Assert.Equal(2, world.Player.Level!.CurrentLevel);
    }

    [Fact]
    public void AnUnearnedChoiceIsAMissRatherThanAnError()
    {
        GameWorld world = WorldWith();

        Assert.False(world.ChooseLevelUp(0));
    }

    [Fact]
    public void ALetterOffTheMenuIsAMiss()
    {
        Entity rat = Monster("Rat", new Point(5, 4), award: 40);
        GameWorld world = WorldWith(rat);

        world.MovePlayer(new Point(1, 0));

        Assert.False(world.ChooseLevelUp(9));
        Assert.Equal(GameMode.ChoosingLevelUp, world.Mode);
    }

    [Fact]
    public void TheMenuCannotBeEnteredBySettingTheMode()
    {
        // A level is earned, not requested, and leaving it by asking would let the player walk
        // away from a decision they have already paid for.
        GameWorld world = WorldWith();

        Assert.Throws<ArgumentException>(() => world.SetMode(GameMode.ChoosingLevelUp));
    }

    [Fact]
    public void LettersChooseWhileTheMenuIsUp()
    {
        Assert.Equal(0, CommandReader.Read(new[] { Keys.A }, GameMode.ChoosingLevelUp).Slot);
        Assert.Equal(2, CommandReader.Read(new[] { Keys.C }, GameMode.ChoosingLevelUp).Slot);
    }

    [Fact]
    public void NothingElseWorksWhileTheMenuIsUp()
    {
        // No escape: the level is earned and the game does not continue until it is spent.
        foreach (Keys key in new[] { Keys.Escape, Keys.Left, Keys.Enter })
        {
            Assert.Equal(
                GameCommandKind.None,
                CommandReader.Read(new[] { key }, GameMode.ChoosingLevelUp).Kind);
        }
    }

    [Fact]
    public void EveryChoiceSaysWhatItWouldChange()
    {
        // A menu that says "stronger" without saying how much is asking for a decision with the
        // information withheld.
        Fighter fighter = new Fighter(30, 5, 2, experienceAwarded: 0);

        foreach (LevelUpChoice choice in LevelUpChoices.All)
        {
            string described = LevelUpChoices.Describe(choice, fighter);

            Assert.Contains("->", described);
        }
    }

    [Fact]
    public void AScrollKillCountsTheSame()
    {
        // Otherwise the safest way to fight would also be the slowest way to improve.
        Entity rat = Monster("Rat", new Point(6, 4), award: 10);
        GameWorld world = WorldWith(rat);

        Entity scroll = new Entity("scroll", '?', Color.Yellow, world.Player.Position, blocksMovement: false, RenderLayer.Item);
        scroll.Consumable = new Consumable(ConsumableKind.Lightning, power: 50, radius: 0);
        world.Player.Inventory!.TryAdd(scroll);

        world.UseItem(0);
        world.ConfirmTarget();

        Assert.Equal(10, world.Player.Level!.Experience);
    }

    [Fact]
    public void ANullFighterIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => LevelUpChoices.Describe(LevelUpChoice.Tougher, null!));
        Assert.Throws<ArgumentNullException>(() => LevelUpChoices.Apply(LevelUpChoice.Tougher, null!));
    }

    [Fact]
    public void AGainOfNothingIsRejected()
    {
        Fighter fighter = new Fighter(30, 5, 2, experienceAwarded: 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => fighter.RaiseAttack(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => fighter.RaiseDefence(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => fighter.RaiseMaximumHitPoints(0));
    }
}

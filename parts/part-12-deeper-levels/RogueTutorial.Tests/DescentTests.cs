/*
 * Unit and integration tests for going down.
 *
 * The rules worth watching: the stairs are somewhere the player has to walk to, descending keeps
 * everything the player earned, and the floor left behind is gone rather than remembered.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~DescentTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadConsole.Input;
using SadRogue.Primitives;
using Xunit;

public sealed class DescentTests
{
    private const int MapWidth = 60;
    private const int MapHeight = 30;

    private static GameWorld GeneratedWorld(int seed, int depth)
    {
        return GameWorld.Generate(
            MapWidth, MapHeight, new Random(seed), MonsterTable.Standard, ItemTable.Standard, depth);
    }

    [Fact]
    public void EveryFloorHasAWayDown()
    {
        // A floor with no stairs is a dead end, which is what Part 11 shipped.
        for (int seed = 1; seed <= 20; seed++)
        {
            GameWorld world = GeneratedWorld(seed, depth: 1);

            bool anyStairs = false;

            for (int row = 0; row < MapHeight; row++)
            {
                for (int col = 0; col < MapWidth; col++)
                {
                    if (world.Map.GetTile(new Point(col, row)).Equals(TileTypes.DownStairs))
                    {
                        anyStairs = true;
                    }
                }
            }

            Assert.True(anyStairs, $"seed {seed} generated a floor with no stairs");
        }
    }

    [Fact]
    public void TheStairsAreNotWhereThePlayerStarts()
    {
        // Otherwise the floor could be skipped without walking a step of it.
        for (int seed = 1; seed <= 20; seed++)
        {
            GameWorld world = GeneratedWorld(seed, depth: 1);

            Assert.False(world.IsPlayerOnStairs, $"seed {seed} put the stairs under the player");
        }
    }

    [Fact]
    public void TheStairsAreWalkable()
    {
        // A staircase inside rock cannot be reached, and the tile has to be stood on.
        Assert.True(TileTypes.DownStairs.IsWalkable);
        Assert.True(TileTypes.DownStairs.IsTransparent);
    }

    [Fact]
    public void DescendingAnywhereElseIsAMissRatherThanAnError()
    {
        GameWorld world = GeneratedWorld(3, depth: 1);

        Assert.False(world.Descend(new Random(3), MonsterTable.Standard, ItemTable.Standard));
        Assert.Equal(1, world.Depth);
    }

    [Fact]
    public void DescendingFromTheStairsGoesDownAFloor()
    {
        GameWorld world = GeneratedWorld(3, depth: 1);

        world.Player.MoveTo(StairsIn(world));

        Assert.True(world.Descend(new Random(11), MonsterTable.Standard, ItemTable.Standard));
        Assert.Equal(2, world.Depth);
    }

    [Fact]
    public void TheNewFloorIsADifferentMap()
    {
        GameWorld world = GeneratedWorld(3, depth: 1);

        world.Player.MoveTo(StairsIn(world));
        world.Descend(new Random(11), MonsterTable.Standard, ItemTable.Standard);

        // The player is standing in the first room of a freshly carved dungeon, which has its
        // own staircase somewhere else.
        Assert.False(world.IsPlayerOnStairs);
        Assert.True(world.Map.IsWalkable(world.Player.Position));
    }

    [Fact]
    public void WhatThePlayerEarnedComesWithThem()
    {
        // The descent is a commitment, not a rest: nothing is restored and nothing is lost.
        GameWorld world = GeneratedWorld(3, depth: 1);

        world.Player.Fighter!.TakeDamage(7);
        world.Player.Level!.Award(15);

        int hitPoints = world.Player.Fighter.HitPoints;

        world.Player.MoveTo(StairsIn(world));
        world.Descend(new Random(11), MonsterTable.Standard, ItemTable.Standard);

        Assert.Equal(hitPoints, world.Player.Fighter!.HitPoints);
        Assert.Equal(15, world.Player.Level!.Experience);
    }

    [Fact]
    public void ThePlayerIsStillInTheEntityList()
    {
        // Rebuilding the list is where the player is easiest to drop.
        GameWorld world = GeneratedWorld(3, depth: 1);

        world.Player.MoveTo(StairsIn(world));
        world.Descend(new Random(11), MonsterTable.Standard, ItemTable.Standard);

        Assert.Contains(world.Player, world.Entities);
    }

    [Fact]
    public void TheOldFloorIsNotRemembered()
    {
        // Carrying memory across would show the new dungeon already explored.
        GameWorld world = GeneratedWorld(3, depth: 1);

        // Mark the whole old floor as explored first. Descending after walking three steps
        // leaves so little memory that any carry-over would pass unnoticed.
        world.RestoreMemory(Enumerable.Repeat(true, MapWidth * MapHeight).ToList());

        world.Player.MoveTo(StairsIn(world));
        world.Descend(new Random(11), MonsterTable.Standard, ItemTable.Standard);

        int remembered = 0;

        for (int row = 0; row < MapHeight; row++)
        {
            for (int col = 0; col < MapWidth; col++)
            {
                if (world.Visibility.StateAt(new Point(col, row)) != CellVisibility.Unseen)
                {
                    remembered++;
                }
            }
        }

        // Only what the player can see from where they now stand.
        Assert.True(remembered < MapWidth * MapHeight / 4, $"{remembered} cells were already known");
    }

    [Fact]
    public void MonstersLeftBehindAreGone()
    {
        GameWorld world = GeneratedWorld(3, depth: 1);

        List<Entity> before = world.Entities.Where(entity => entity != world.Player).ToList();

        world.Player.MoveTo(StairsIn(world));
        world.Descend(new Random(11), MonsterTable.Standard, ItemTable.Standard);

        foreach (Entity leftBehind in before)
        {
            Assert.DoesNotContain(leftBehind, world.Entities);
        }
    }

    [Fact]
    public void ADeadPlayerDoesNotLeaveTheFloor()
    {
        GameWorld world = GeneratedWorld(3, depth: 1);

        world.Player.MoveTo(StairsIn(world));
        world.Player.Die();

        Assert.False(world.Descend(new Random(11), MonsterTable.Standard, ItemTable.Standard));
        Assert.Equal(1, world.Depth);
    }

    [Fact]
    public void ShiftAndPeriodIsTheDescendKey()
    {
        Assert.Equal(
            GameCommandKind.Descend,
            CommandReader.Read(new[] { Keys.OemPeriod }, GameMode.Playing, shiftHeld: true).Kind);
    }

    [Fact]
    public void PeriodWithoutShiftIsNotTheDescendKey()
    {
        // '.' is a bare period, and a player pressing it has not asked to leave the floor.
        Assert.Equal(
            GameCommandKind.None,
            CommandReader.Read(new[] { Keys.OemPeriod }, GameMode.Playing, shiftHeld: false).Kind);
    }

    [Fact]
    public void FloorZeroIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GeneratedWorld(3, depth: 0));
    }

    [Fact]
    public void ACorpseSinksBelowWhatIsDroppedOnIt()
    {
        // The potion on the remains is the one the player can still pick up, so it is the one
        // that must be visible.
        GameMap map = new GameMap(3, 1);
        Point cell = new Point(1, 0);

        Entity rat = new Entity("Rat", 'r', Color.Red, cell, blocksMovement: true, RenderLayer.Actor);
        rat.Fighter = new Fighter(4, 3, 0, experienceAwarded: 10);
        rat.Die();

        Entity potion = new Entity("potion", '!', Color.Magenta, cell, blocksMovement: false, RenderLayer.Item);

        RenderedFrame frame = FrameComposer.Compose(map, new[] { potion, rat });

        Assert.Equal(".!.", frame.ToText());
    }

    // The one staircase on the floor. Fails the test rather than returning a wrong cell.
    private static Point StairsIn(GameWorld world)
    {
        for (int row = 0; row < MapHeight; row++)
        {
            for (int col = 0; col < MapWidth; col++)
            {
                Point cell = new Point(col, row);

                if (world.Map.GetTile(cell).Equals(TileTypes.DownStairs))
                {
                    return cell;
                }
            }
        }

        throw new InvalidOperationException("The floor has no stairs.");
    }
}

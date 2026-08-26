/*
 * Unit tests for what lives in the dungeon and how it is placed. Every test passes a seeded
 * Random, so "random" here means arbitrary but repeatable.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~MonsterTableTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class MonsterTableTests
{
    // A room with solid floor inside it, which is what placement expects to find.
    private static GameMap OpenMapFor(RectangularRoom room)
    {
        GameMap map = new GameMap(room.Right + 2, room.Bottom + 2);
        map.Fill(TileTypes.Wall);

        foreach (Point cell in room.InnerCells)
        {
            map.SetTile(cell, TileTypes.Floor);
        }

        return map;
    }

    private static MonsterTable RatsOnly(int maximumPerRoom)
    {
        return new MonsterTable(
            new[] { new MonsterKind("Rat", 'r', Color.Red, weight: 1, maximumHitPoints: 4, attack: 3, defence: 0) },
            maximumPerRoom);
    }

    [Fact]
    public void NoMoreThanTheMaximumArePlaced()
    {
        RectangularRoom room = new RectangularRoom(0, 0, 12, 12);
        GameMap map = OpenMapFor(room);
        MonsterTable table = RatsOnly(maximumPerRoom: 2);

        for (int seed = 0; seed < 50; seed++)
        {
            IReadOnlyList<Entity> placed = table.PopulateRoom(room, map, new Random(seed));

            Assert.True(placed.Count <= 2, $"seed {seed} placed {placed.Count}");
        }
    }

    [Fact]
    public void AMaximumOfZeroPlacesNothing()
    {
        RectangularRoom room = new RectangularRoom(0, 0, 12, 12);
        GameMap map = OpenMapFor(room);

        IReadOnlyList<Entity> placed = RatsOnly(maximumPerRoom: 0).PopulateRoom(room, map, new Random(1));

        Assert.Empty(placed);
    }

    [Fact]
    public void EveryMonsterLandsInsideTheRoomWalls()
    {
        RectangularRoom room = new RectangularRoom(5, 3, 9, 8);
        GameMap map = OpenMapFor(room);
        MonsterTable table = RatsOnly(maximumPerRoom: 2);

        for (int seed = 0; seed < 50; seed++)
        {
            foreach (Entity monster in table.PopulateRoom(room, map, new Random(seed)))
            {
                Assert.True(monster.Position.X > room.Left, $"seed {seed}: {monster.Position} is on the left wall");
                Assert.True(monster.Position.X < room.Right, $"seed {seed}: {monster.Position} is on the right wall");
                Assert.True(monster.Position.Y > room.Top, $"seed {seed}: {monster.Position} is on the top wall");
                Assert.True(monster.Position.Y < room.Bottom, $"seed {seed}: {monster.Position} is on the bottom wall");
            }
        }
    }

    [Fact]
    public void ADoorwayIsStillNotInsideTheRoom()
    {
        // The walkability check alone does not pin the bounds: carve a corridor through a room's
        // wall and that wall cell becomes walkable, so a placement roll allowed to reach the ring
        // would put a monster in the doorway. The room's interior is the contract, so it must be
        // the roll that excludes the ring rather than the map happening to be solid there.
        RectangularRoom room = new RectangularRoom(0, 0, 7, 7);
        GameMap map = OpenMapFor(room);

        // A doorway in each wall, as dungeon generation produces when a corridor meets a room.
        map.SetTile(new Point(3, 0), TileTypes.Floor);
        map.SetTile(new Point(3, 6), TileTypes.Floor);
        map.SetTile(new Point(0, 3), TileTypes.Floor);
        map.SetTile(new Point(6, 3), TileTypes.Floor);

        MonsterTable table = RatsOnly(maximumPerRoom: 4);

        for (int seed = 0; seed < 100; seed++)
        {
            foreach (Entity monster in table.PopulateRoom(room, map, new Random(seed)))
            {
                Assert.True(
                    monster.Position.X > room.Left && monster.Position.X < room.Right
                        && monster.Position.Y > room.Top && monster.Position.Y < room.Bottom,
                    $"seed {seed}: {monster.Name} was placed at {monster.Position}, on the room's wall ring");
            }
        }
    }

    [Fact]
    public void TwoMonstersNeverShareACell()
    {
        RectangularRoom room = new RectangularRoom(0, 0, 5, 5);
        GameMap map = OpenMapFor(room);

        // A 5x5 room has a 3x3 interior, so with four wanted the rolls collide often.
        MonsterTable table = RatsOnly(maximumPerRoom: 4);

        for (int seed = 0; seed < 50; seed++)
        {
            IReadOnlyList<Entity> placed = table.PopulateRoom(room, map, new Random(seed));

            Assert.Equal(placed.Count, placed.Select(monster => monster.Position).Distinct().Count());
        }
    }

    [Fact]
    public void MonstersAreNeverPlacedInRock()
    {
        // A corridor cut through a room, or a pillar, leaves unwalkable cells in its interior.
        RectangularRoom room = new RectangularRoom(0, 0, 8, 8);
        GameMap map = OpenMapFor(room);
        map.SetTile(new Point(3, 3), TileTypes.Wall);
        map.SetTile(new Point(4, 4), TileTypes.Wall);

        MonsterTable table = RatsOnly(maximumPerRoom: 4);

        for (int seed = 0; seed < 50; seed++)
        {
            foreach (Entity monster in table.PopulateRoom(room, map, new Random(seed)))
            {
                Assert.True(map.IsWalkable(monster.Position), $"seed {seed}: {monster.Name} is in rock");
            }
        }
    }

    [Fact]
    public void EveryPlacedMonsterBlocksMovement()
    {
        RectangularRoom room = new RectangularRoom(0, 0, 10, 10);
        GameMap map = OpenMapFor(room);

        IReadOnlyList<Entity> placed = RatsOnly(maximumPerRoom: 2).PopulateRoom(room, map, new Random(3));

        Assert.All(placed, monster => Assert.True(monster.BlocksMovement));
    }

    [Fact]
    public void TheSameSeedPlacesTheSameMonsters()
    {
        RectangularRoom room = new RectangularRoom(0, 0, 10, 10);
        GameMap map = OpenMapFor(room);
        MonsterTable table = MonsterTable.Standard;

        string first = string.Join(";", table.PopulateRoom(room, map, new Random(99))
            .Select(monster => $"{monster.Name}{monster.Position}"));
        string second = string.Join(";", table.PopulateRoom(room, map, new Random(99))
            .Select(monster => $"{monster.Name}{monster.Position}"));

        Assert.Equal(first, second);
    }

    [Fact]
    public void AHeavierKindTurnsUpMoreOften()
    {
        // Weights are relative, so the check is on the ordering rather than on exact counts:
        // a kind weighted 3 against 1 should clearly dominate over many rooms.
        RectangularRoom room = new RectangularRoom(0, 0, 10, 10);
        GameMap map = OpenMapFor(room);

        MonsterTable table = new MonsterTable(
            new[]
            {
                new MonsterKind("Common", 'c', Color.Red, weight: 3, maximumHitPoints: 4, attack: 3, defence: 0),
                new MonsterKind("Rare", 'x', Color.Blue, weight: 1, maximumHitPoints: 4, attack: 3, defence: 0),
            },
            maximumPerRoom: 2);

        int common = 0;
        int rare = 0;

        for (int seed = 0; seed < 300; seed++)
        {
            foreach (Entity monster in table.PopulateRoom(room, map, new Random(seed)))
            {
                if (monster.Name == "Common")
                {
                    common++;
                }
                else
                {
                    rare++;
                }
            }
        }

        Assert.True(common > rare, $"common {common} should outnumber rare {rare}");
    }

    [Fact]
    public void AKindWithNoNameIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new MonsterKind("  ", 'r', Color.Red, weight: 1, maximumHitPoints: 4, attack: 3, defence: 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AWeightThatCanNeverBeChosenIsRejected(int weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MonsterKind("Rat", 'r', Color.Red, weight, maximumHitPoints: 4, attack: 3, defence: 0));
    }

    [Fact]
    public void AnEmptyTableIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new MonsterTable(Array.Empty<MonsterKind>(), maximumPerRoom: 2));
    }

    [Fact]
    public void ANegativeMaximumIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MonsterTable(new[] { new MonsterKind("Rat", 'r', Color.Red, 1, maximumHitPoints: 4, attack: 3, defence: 0) }, maximumPerRoom: -1));
    }

    [Fact]
    public void ANullArgumentIsRejected()
    {
        RectangularRoom room = new RectangularRoom(0, 0, 8, 8);
        GameMap map = OpenMapFor(room);
        MonsterTable table = MonsterTable.Standard;

        Assert.Throws<ArgumentNullException>(() => table.PopulateRoom(null!, map, new Random(1)));
        Assert.Throws<ArgumentNullException>(() => table.PopulateRoom(room, null!, new Random(1)));
        Assert.Throws<ArgumentNullException>(() => table.PopulateRoom(room, map, null!));
        Assert.Throws<ArgumentNullException>(() => new MonsterTable(null!, 2));
    }
}

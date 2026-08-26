/*
 * Unit tests for dungeon generation. Every one of these passes a Random with a fixed seed, so
 * "random" here means "arbitrary but repeatable" - the property being tested is never left to
 * chance.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~DungeonGeneratorTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class DungeonGeneratorTests
{
    // The settings most tests use: enough attempts to fill a small map, rooms of a size that
    // fits several times over.
    private static DungeonSettings StandardSettings()
    {
        return new DungeonSettings(maximumRooms: 20, minimumRoomSize: 5, maximumRoomSize: 9);
    }

    private static GeneratedDungeon GenerateWithSeed(int seed)
    {
        return new DungeonGenerator(StandardSettings()).Generate(40, 25, new Random(seed));
    }

    [Fact]
    public void TheSameSeedProducesTheSameDungeon()
    {
        // This is the property the whole design exists to provide. Without it a bad dungeon
        // cannot be reproduced, and none of the tests below could assert anything at all.
        string first = FrameComposer.Compose(GenerateWithSeed(12345).Map, Array.Empty<Entity>()).ToText();
        string second = FrameComposer.Compose(GenerateWithSeed(12345).Map, Array.Empty<Entity>()).ToText();

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentSeedsProduceDifferentDungeons()
    {
        string first = FrameComposer.Compose(GenerateWithSeed(1).Map, Array.Empty<Entity>()).ToText();
        string second = FrameComposer.Compose(GenerateWithSeed(2).Map, Array.Empty<Entity>()).ToText();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void TheDungeonIsTheSizeAsked()
    {
        GeneratedDungeon dungeon = GenerateWithSeed(7);

        Assert.Equal(40, dungeon.Map.Width);
        Assert.Equal(25, dungeon.Map.Height);
    }

    [Fact]
    public void AtLeastOneRoomIsPlaced()
    {
        GeneratedDungeon dungeon = GenerateWithSeed(7);

        Assert.NotEmpty(dungeon.Rooms);
    }

    [Fact]
    public void NoTwoRoomsOverlap()
    {
        // Checked across several seeds: a single seed could be lucky and hide a real collision.
        for (int seed = 0; seed < 20; seed++)
        {
            IReadOnlyList<RectangularRoom> rooms = GenerateWithSeed(seed).Rooms;

            for (int first = 0; first < rooms.Count; first++)
            {
                for (int second = first + 1; second < rooms.Count; second++)
                {
                    Assert.False(
                        rooms[first].Intersects(rooms[second]),
                        $"seed {seed}: rooms {first} and {second} overlap");
                }
            }
        }
    }

    [Fact]
    public void EveryRoomFitsInsideTheMap()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            GeneratedDungeon dungeon = GenerateWithSeed(seed);

            foreach (RectangularRoom room in dungeon.Rooms)
            {
                Assert.True(room.Left >= 0, $"seed {seed}: room starts left of the map");
                Assert.True(room.Top >= 0, $"seed {seed}: room starts above the map");
                Assert.True(room.Right < dungeon.Map.Width, $"seed {seed}: room runs off the right");
                Assert.True(room.Bottom < dungeon.Map.Height, $"seed {seed}: room runs off the bottom");
            }
        }
    }

    [Fact]
    public void EveryRoomInteriorIsWalkable()
    {
        GeneratedDungeon dungeon = GenerateWithSeed(99);

        foreach (RectangularRoom room in dungeon.Rooms)
        {
            foreach (Point cell in room.InnerCells)
            {
                Assert.True(dungeon.Map.IsWalkable(cell), $"room interior at {cell} was not carved");
            }
        }
    }

    [Fact]
    public void ThePlayerStartsInsideTheFirstRoom()
    {
        GeneratedDungeon dungeon = GenerateWithSeed(42);

        Assert.Equal(dungeon.Rooms[0].Center, dungeon.PlayerStart);
        Assert.True(dungeon.Map.IsWalkable(dungeon.PlayerStart));
    }

    [Fact]
    public void EveryRoomIsReachableFromTheStart()
    {
        // The point of corridors. A dungeon whose rooms are carved but not joined passes every
        // other test here and is unplayable, so this walks the floor and checks it is one piece.
        for (int seed = 0; seed < 20; seed++)
        {
            GeneratedDungeon dungeon = GenerateWithSeed(seed);

            HashSet<Point> reached = WalkableCellsReachableFrom(dungeon.PlayerStart, dungeon.Map);

            foreach (RectangularRoom room in dungeon.Rooms)
            {
                Assert.True(
                    reached.Contains(room.Center),
                    $"seed {seed}: a room centre at {room.Center} cannot be reached from the start");
            }
        }
    }

    [Fact]
    public void TheEdgeOfTheMapIsNeverCarved()
    {
        // A room or corridor touching the border would let the player walk off the map, and
        // MovementRules would silently refuse the move rather than reporting a generation bug.
        for (int seed = 0; seed < 20; seed++)
        {
            GameMap map = GenerateWithSeed(seed).Map;

            for (int col = 0; col < map.Width; col++)
            {
                Assert.False(map.IsWalkable(new Point(col, 0)), $"seed {seed}: top edge carved at x={col}");
                Assert.False(map.IsWalkable(new Point(col, map.Height - 1)), $"seed {seed}: bottom edge carved at x={col}");
            }

            for (int row = 0; row < map.Height; row++)
            {
                Assert.False(map.IsWalkable(new Point(0, row)), $"seed {seed}: left edge carved at y={row}");
                Assert.False(map.IsWalkable(new Point(map.Width - 1, row)), $"seed {seed}: right edge carved at y={row}");
            }
        }
    }

    [Fact]
    public void MoreAttemptsPlaceNoFewerRooms()
    {
        // Not "more rooms": overlap rejection means a bigger attempt count can tie. It must
        // never place fewer, which is what a bug in the attempt loop would look like.
        int few = new DungeonGenerator(new DungeonSettings(3, 5, 9)).Generate(40, 25, new Random(5)).Rooms.Count;
        int many = new DungeonGenerator(new DungeonSettings(30, 5, 9)).Generate(40, 25, new Random(5)).Rooms.Count;

        Assert.True(many >= few, $"30 attempts placed {many} rooms, 3 attempts placed {few}");
    }

    [Fact]
    public void ANullRandomIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new DungeonGenerator(StandardSettings()).Generate(40, 25, null!));
    }

    [Fact]
    public void ANullSettingsObjectIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new DungeonGenerator(null!));
    }

    [Fact]
    public void AMapTooSmallForOneRoomIsRejected()
    {
        // Smallest room here is 5x5, so a 4x4 map cannot hold one however lucky the rolls are.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DungeonGenerator(StandardSettings()).Generate(4, 4, new Random(1)));
    }

    // Flood fill across walkable cells, four-directional because corridors never move diagonally.
    private static HashSet<Point> WalkableCellsReachableFrom(Point start, GameMap map)
    {
        HashSet<Point> reached = new HashSet<Point> { start };
        Queue<Point> toVisit = new Queue<Point>();
        toVisit.Enqueue(start);

        Point[] steps = { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) };

        while (toVisit.Count > 0)
        {
            Point cell = toVisit.Dequeue();

            foreach (Point step in steps)
            {
                Point neighbour = cell + step;

                // IsWalkable answers false off the map, so no bounds check is needed here.
                if (map.IsWalkable(neighbour) && reached.Add(neighbour))
                {
                    toVisit.Enqueue(neighbour);
                }
            }
        }

        return reached;
    }
}

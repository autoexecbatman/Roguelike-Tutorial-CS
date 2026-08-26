/*
 * Integration tests: the key table, the map and the movement rule composed, which is the path
 * RootScreen.ProcessKeyboard walks. Unit tests cover each piece; this level catches an axis
 * swap or a wall consulted for the wrong cell, both of which survive every piece being right.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~MovementIntegrationTests
 */

using System.Collections.Generic;
using RogueTutorial;
using SadConsole.Input;
using SadRogue.Primitives;
using Xunit;

public sealed class MovementIntegrationTests
{
    // A hand-built walled room: solid rock with the interior carved out. Built here rather
    // than generated so the expected pictures below stay fixed and readable.
    private static GameMap WalledRoom(int width, int height)
    {
        GameMap room = new GameMap(width, height);
        room.Fill(TileTypes.Wall);

        for (int row = 1; row < height - 1; row++)
        {
            for (int col = 1; col < width - 1; col++)
            {
                room.SetTile(new Point(col, row), TileTypes.Floor);
            }
        }

        return room;
    }

    // Walks an entity through a map one frame of key presses at a time, as the game loop does.
    private static Point PositionAfter(GameMap map, Point start, IEnumerable<Keys[]> framesOfKeys)
    {
        Entity walker = new Entity("Walker", '@', Color.White, start, blocksMovement: true, RenderLayer.Player);

        foreach (Keys[] keysThisFrame in framesOfKeys)
        {
            Point offset = MovementKeys.OffsetFor(keysThisFrame);

            walker.MoveTo(MovementRules.DestinationFor(walker.Position, offset, map));
        }

        return walker.Position;
    }

    [Fact]
    public void PressingUpMovesTowardTheTopOfTheScreen()
    {
        // Y grows downward on a console grid, so "up" must decrease Y.
        Point result = PositionAfter(new GameMap(9, 9), new Point(4, 4), new[] { new[] { Keys.Up } });

        Assert.Equal(new Point(4, 3), result);
    }

    [Fact]
    public void FourFramesOfRightMoveFourCells()
    {
        Point result = PositionAfter(
            new GameMap(9, 9),
            new Point(1, 1),
            new[] { new[] { Keys.Right }, new[] { Keys.Right }, new[] { Keys.Right }, new[] { Keys.Right } });

        Assert.Equal(new Point(5, 1), result);
    }

    [Fact]
    public void WalkingIntoAWallStopsWithoutSliding()
    {
        GameMap map = new GameMap(9, 9);
        map.SetTile(new Point(4, 3), TileTypes.Wall);

        // Three presses up from (4,4): the first is refused, and so are the other two.
        Point result = PositionAfter(
            map,
            new Point(4, 4),
            new[] { new[] { Keys.Up }, new[] { Keys.Up }, new[] { Keys.Up } });

        Assert.Equal(new Point(4, 4), result);
    }

    [Fact]
    public void AWalledRoomHoldsThePlayerIn()
    {
        GameMap room = WalledRoom(9, 9);

        // Ten presses left from the interior's left edge; the border must stop every one.
        List<Keys[]> tenPressesLeft = new List<Keys[]>();
        for (int frame = 0; frame < 10; frame++)
        {
            tenPressesLeft.Add(new[] { Keys.Left });
        }

        Point result = PositionAfter(room, new Point(2, 1), tenPressesLeft);

        Assert.Equal(new Point(1, 1), result);
        Assert.True(room.IsWalkable(result));
    }

    [Fact]
    public void AKeypadCornerReachesTheSameCellAsTwoCardinals()
    {
        GameMap map = new GameMap(9, 9);
        Point start = new Point(4, 4);

        Point viaCorner = PositionAfter(map, start, new[] { new[] { Keys.NumPad7 } });
        Point viaCardinals = PositionAfter(map, start, new[] { new[] { Keys.Left, Keys.Up } });

        Assert.Equal(viaCorner, viaCardinals);
    }

    [Fact]
    public void ThePlayerAppearsWhereTheMoveLeftIt()
    {
        GameMap room = WalledRoom(5, 5);
        Entity player = new Entity("Player", '@', Color.White, new Point(1, 1), blocksMovement: true, RenderLayer.Player);

        player.MoveTo(MovementRules.DestinationFor(player.Position, MovementKeys.OffsetFor(new[] { Keys.Right }), room));

        // The frame is the end of the whole chain: key -> rule -> entity -> picture.
        // A 5x5 room is one ring of wall around a 3x3 floor, and the player has stepped
        // one cell right of the top-left interior corner.
        Assert.Equal(
            string.Join("\n", "#####", "#.@.#", "#...#", "#...#", "#####"),
            FrameComposer.Compose(room, new[] { player }).ToText());
    }
}

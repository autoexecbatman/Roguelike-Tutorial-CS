/*
 * Unit tests for the one map this part builds. Expected values come from the description of
 * the room: the outermost ring is wall, everything else is floor, and two pillars stand in it.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~MapFactoryTests
 */

using System;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class MapFactoryTests
{
    [Fact]
    public void TheRoomIsTheSizeAsked()
    {
        GameMap room = MapFactory.CreateWalledRoom(80, 25);

        Assert.Equal(80, room.Width);
        Assert.Equal(25, room.Height);
    }

    [Fact]
    public void EveryBorderCellIsWall()
    {
        GameMap room = MapFactory.CreateWalledRoom(10, 6);

        // Walk the whole border rather than sampling corners; a loop that stops one short
        // leaves a gap the player can walk through, and a spot check would miss it.
        for (int col = 0; col < room.Width; col++)
        {
            Assert.False(room.IsWalkable(new Point(col, 0)), $"top border at x={col}");
            Assert.False(room.IsWalkable(new Point(col, room.Height - 1)), $"bottom border at x={col}");
        }

        for (int row = 0; row < room.Height; row++)
        {
            Assert.False(room.IsWalkable(new Point(0, row)), $"left border at y={row}");
            Assert.False(room.IsWalkable(new Point(room.Width - 1, row)), $"right border at y={row}");
        }
    }

    [Fact]
    public void TheRoomHasAWalkableInterior()
    {
        GameMap room = MapFactory.CreateWalledRoom(10, 6);

        // Not every interior cell - two of them are pillars - but the corners of the interior
        // are always open, and a room with no floor at all would be useless.
        Assert.True(room.IsWalkable(new Point(1, 1)));
        Assert.True(room.IsWalkable(new Point(room.Width - 2, room.Height - 2)));
    }

    [Fact]
    public void TheRoomContainsExactlyTwoPillars()
    {
        GameMap room = MapFactory.CreateWalledRoom(20, 10);

        int wallsInsideTheBorder = 0;

        for (int row = 1; row < room.Height - 1; row++)
        {
            for (int col = 1; col < room.Width - 1; col++)
            {
                if (!room.IsWalkable(new Point(col, row)))
                {
                    wallsInsideTheBorder++;
                }
            }
        }

        Assert.Equal(2, wallsInsideTheBorder);
    }

    [Fact]
    public void TheRoomIsTheSameEveryTime()
    {
        // Nothing here is random yet; randomness arrives with generation in Part 3.
        GameMap first = MapFactory.CreateWalledRoom(20, 10);
        GameMap second = MapFactory.CreateWalledRoom(20, 10);

        Assert.Equal(
            FrameComposer.Compose(first, Array.Empty<Entity>()).ToText(),
            FrameComposer.Compose(second, Array.Empty<Entity>()).ToText());
    }

    [Theory]
    [InlineData(2, 5)]
    [InlineData(5, 2)]
    [InlineData(0, 0)]
    public void ARoomTooSmallToHaveAnInsideIsRejected(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MapFactory.CreateWalledRoom(width, height));
    }
}

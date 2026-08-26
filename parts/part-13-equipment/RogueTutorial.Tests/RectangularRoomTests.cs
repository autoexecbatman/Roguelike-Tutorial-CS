/*
 * Unit tests for one room's geometry. Expected values are worked out from the definition: the
 * rectangle includes its wall ring, so a room 5 wide has 3 columns of floor.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~RectangularRoomTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class RectangularRoomTests
{
    [Fact]
    public void TheEdgesAreTheRectangleInclusive()
    {
        RectangularRoom room = new RectangularRoom(10, 5, 7, 6);

        Assert.Equal(10, room.Left);
        Assert.Equal(5, room.Top);

        // 10 + 7 - 1: a 7-wide room starting at column 10 ends at column 16, not 17.
        Assert.Equal(16, room.Right);
        Assert.Equal(10, room.Bottom);
    }

    [Fact]
    public void TheCentreIsRoundedDown()
    {
        RectangularRoom room = new RectangularRoom(10, 5, 7, 6);

        // 10 + 7/2 = 13, and 5 + 6/2 = 8; integer division floors both.
        Assert.Equal(new Point(13, 8), room.Center);
    }

    [Fact]
    public void TheCentreIsAlwaysInsideTheWalls()
    {
        // The player spawns on a centre and corridors are dug between them, so a centre landing
        // on the wall ring would be a real bug. Smallest room is where it would show up first.
        RectangularRoom smallest = new RectangularRoom(0, 0, 3, 3);

        Assert.Equal(new Point(1, 1), smallest.Center);
        Assert.Contains(smallest.Center, smallest.InnerCells);
    }

    [Fact]
    public void TheInteriorExcludesTheWallRing()
    {
        RectangularRoom room = new RectangularRoom(0, 0, 5, 4);

        List<Point> interior = room.InnerCells.ToList();

        // 5x4 total, so 3x2 of floor once the ring is removed.
        Assert.Equal(6, interior.Count);
        Assert.All(interior, cell => Assert.True(cell.X >= 1 && cell.X <= 3 && cell.Y >= 1 && cell.Y <= 2));
    }

    [Fact]
    public void TheSmallestRoomHasExactlyOneFloorCell()
    {
        RectangularRoom smallest = new RectangularRoom(4, 4, 3, 3);

        Assert.Equal(new[] { new Point(5, 5) }, smallest.InnerCells.ToArray());
    }

    [Fact]
    public void OverlappingRoomsIntersect()
    {
        RectangularRoom first = new RectangularRoom(0, 0, 5, 5);
        RectangularRoom second = new RectangularRoom(3, 3, 5, 5);

        Assert.True(first.Intersects(second));
        Assert.True(second.Intersects(first));
    }

    [Fact]
    public void RoomsSharingOnlyAWallStillIntersect()
    {
        // First occupies columns 0-4, second starts at 4. Sharing that wall would let the
        // player walk between the rooms with no corridor, so this must count as a collision.
        RectangularRoom first = new RectangularRoom(0, 0, 5, 5);
        RectangularRoom touching = new RectangularRoom(4, 0, 5, 5);

        // Both directions: intersection is symmetric, and testing one way leaves half the
        // comparison unexercised - a mutation to the other half survived until this was added.
        Assert.True(first.Intersects(touching));
        Assert.True(touching.Intersects(first));
    }

    [Fact]
    public void RoomsSharingOnlyAHorizontalWallStillIntersect()
    {
        // The vertical twin of the test above: first occupies rows 0-4, below starts at row 4.
        // Without this, a mutation to the top-versus-bottom half of the comparison survives.
        RectangularRoom first = new RectangularRoom(0, 0, 5, 5);
        RectangularRoom below = new RectangularRoom(0, 4, 5, 5);

        Assert.True(first.Intersects(below));
        Assert.True(below.Intersects(first));
    }

    [Fact]
    public void RoomsOneCellApartDoNotIntersect()
    {
        // First ends at column 4, second starts at 5: one column of rock between them.
        RectangularRoom first = new RectangularRoom(0, 0, 5, 5);
        RectangularRoom clear = new RectangularRoom(5, 0, 5, 5);

        Assert.False(first.Intersects(clear));
        Assert.False(clear.Intersects(first));
    }

    [Fact]
    public void RoomsSeparatedOnlyVerticallyDoNotIntersect()
    {
        RectangularRoom first = new RectangularRoom(0, 0, 5, 5);
        RectangularRoom below = new RectangularRoom(0, 5, 5, 5);

        Assert.False(first.Intersects(below));
        Assert.False(below.Intersects(first));
    }

    [Theory]
    [InlineData(2, 5)]
    [InlineData(5, 2)]
    public void ARoomWithNoInteriorIsRejected(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RectangularRoom(0, 0, width, height));
    }

    [Fact]
    public void ANullComparisonIsRejected()
    {
        RectangularRoom room = new RectangularRoom(0, 0, 5, 5);

        Assert.Throws<ArgumentNullException>(() => room.Intersects(null!));
    }
}

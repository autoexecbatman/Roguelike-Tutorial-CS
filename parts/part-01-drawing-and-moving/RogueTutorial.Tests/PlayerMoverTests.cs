/*
 * Unit tests for the player's position rules. Expected values are computed from the
 * specification: the player occupies one cell, moves by whole cells, and is pulled to
 * the nearest edge rather than leaving the grid.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~PlayerMoverTests
 */

using System;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class PlayerMoverTests
{
    // A grid small enough that every edge is reachable in one move from the centre.
    private static GridBounds SmallGrid()
    {
        return new GridBounds(3, 3);
    }

    [Fact]
    public void TheStartingPositionIsKept()
    {
        PlayerMover mover = new PlayerMover(SmallGrid(), new Point(1, 1));

        Assert.Equal(new Point(1, 1), mover.Position);
    }

    [Fact]
    public void AStartingPositionOutsideTheGridIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerMover(SmallGrid(), new Point(3, 0)));
    }

    [Fact]
    public void AnOrdinaryMoveShiftsByTheOffset()
    {
        PlayerMover mover = new PlayerMover(SmallGrid(), new Point(1, 1));

        mover.Move(new Point(1, 0));

        Assert.Equal(new Point(2, 1), mover.Position);
    }

    [Fact]
    public void ADiagonalMoveShiftsBothAxes()
    {
        PlayerMover mover = new PlayerMover(SmallGrid(), new Point(1, 1));

        mover.Move(new Point(-1, -1));

        Assert.Equal(new Point(0, 0), mover.Position);
    }

    [Theory]
    [InlineData(-5, 0, 0, 1)]
    [InlineData(5, 0, 2, 1)]
    [InlineData(0, -5, 1, 0)]
    [InlineData(0, 5, 1, 2)]
    public void AMoveBeyondAnEdgeStopsAtTheEdge(int offsetX, int offsetY, int expectedX, int expectedY)
    {
        PlayerMover mover = new PlayerMover(SmallGrid(), new Point(1, 1));

        mover.Move(new Point(offsetX, offsetY));

        Assert.Equal(new Point(expectedX, expectedY), mover.Position);
    }

    [Fact]
    public void PushingIntoACornerClampsBothAxes()
    {
        PlayerMover mover = new PlayerMover(SmallGrid(), new Point(1, 1));

        mover.Move(new Point(-9, -9));

        Assert.Equal(new Point(0, 0), mover.Position);
    }

    [Fact]
    public void AZeroOffsetLeavesThePositionAlone()
    {
        PlayerMover mover = new PlayerMover(SmallGrid(), new Point(2, 0));

        mover.Move(Point.Zero);

        Assert.Equal(new Point(2, 0), mover.Position);
    }

    [Fact]
    public void ANullGridIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new PlayerMover(null!, Point.Zero));
    }
}

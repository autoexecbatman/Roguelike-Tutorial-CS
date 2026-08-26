/*
 * Unit tests for the L-shaped path between two points. Expected paths are written out cell by
 * cell from the definition rather than taken from what the code returned.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~CorridorTests
 */

using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class CorridorTests
{
    [Fact]
    public void AHorizontalFirstPathTurnsOnce()
    {
        Point[] path = Corridor.Between(new Point(1, 1), new Point(4, 3), horizontalFirst: true).ToArray();

        Assert.Equal(
            new[]
            {
                new Point(1, 1), new Point(2, 1), new Point(3, 1), new Point(4, 1),
                new Point(4, 2), new Point(4, 3),
            },
            path);
    }

    [Fact]
    public void AVerticalFirstPathTurnsTheOtherWay()
    {
        Point[] path = Corridor.Between(new Point(1, 1), new Point(4, 3), horizontalFirst: false).ToArray();

        Assert.Equal(
            new[]
            {
                new Point(1, 1), new Point(1, 2), new Point(1, 3),
                new Point(2, 3), new Point(3, 3), new Point(4, 3),
            },
            path);
    }

    [Fact]
    public void BothEndpointsAreIncluded()
    {
        Point[] path = Corridor.Between(new Point(2, 7), new Point(9, 1), horizontalFirst: true).ToArray();

        Assert.Equal(new Point(2, 7), path.First());
        Assert.Equal(new Point(9, 1), path.Last());
    }

    [Fact]
    public void ThePathVisitsNoCellTwice()
    {
        // The corner belongs to both legs, so an off-by-one there would duplicate it.
        Point[] path = Corridor.Between(new Point(0, 0), new Point(5, 5), horizontalFirst: true).ToArray();

        Assert.Equal(path.Length, path.Distinct().Count());
    }

    [Fact]
    public void EveryStepIsToAnAdjacentCell()
    {
        // A gap in the path would carve a corridor the player cannot walk down.
        Point[] path = Corridor.Between(new Point(3, 9), new Point(11, 2), horizontalFirst: false).ToArray();

        for (int step = 1; step < path.Length; step++)
        {
            int distance = System.Math.Abs(path[step].X - path[step - 1].X)
                + System.Math.Abs(path[step].Y - path[step - 1].Y);

            Assert.Equal(1, distance);
        }
    }

    [Fact]
    public void APathBackwardsWorksTheSame()
    {
        // Rooms are joined in generation order, which does not sort the coordinates first.
        Point[] path = Corridor.Between(new Point(5, 5), new Point(2, 2), horizontalFirst: true).ToArray();

        Assert.Equal(
            new[]
            {
                new Point(5, 5), new Point(4, 5), new Point(3, 5), new Point(2, 5),
                new Point(2, 4), new Point(2, 3), new Point(2, 2),
            },
            path);
    }

    [Fact]
    public void AStraightPathHasNoCorner()
    {
        // Two centres can share a row; the vertical leg is then empty and must add nothing.
        Point[] path = Corridor.Between(new Point(1, 4), new Point(4, 4), horizontalFirst: true).ToArray();

        Assert.Equal(
            new[] { new Point(1, 4), new Point(2, 4), new Point(3, 4), new Point(4, 4) },
            path);
    }

    [Fact]
    public void IdenticalEndpointsYieldOneCell()
    {
        Point[] path = Corridor.Between(new Point(6, 6), new Point(6, 6), horizontalFirst: true).ToArray();

        Assert.Equal(new[] { new Point(6, 6) }, path);
    }

    [Fact]
    public void BothOrdersReachTheSameEndpoints()
    {
        Point[] horizontal = Corridor.Between(new Point(2, 2), new Point(8, 6), horizontalFirst: true).ToArray();
        Point[] vertical = Corridor.Between(new Point(2, 2), new Point(8, 6), horizontalFirst: false).ToArray();

        Assert.Equal(horizontal.First(), vertical.First());
        Assert.Equal(horizontal.Last(), vertical.Last());

        // Same length, different route: an L either way covers the same number of cells.
        Assert.Equal(horizontal.Length, vertical.Length);
        Assert.NotEqual(horizontal, vertical);
    }
}

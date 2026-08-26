/*
 * Unit tests for the grid rectangle. The boundary cases are the point: the largest legal
 * coordinate is one less than the dimension, which is where off-by-one errors live.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~GridBoundsTests
 */

using System;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class GridBoundsTests
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(79, 24, true)]
    [InlineData(80, 24, false)]
    [InlineData(79, 25, false)]
    [InlineData(-1, 0, false)]
    [InlineData(0, -1, false)]
    public void ContainsAcceptsExactlyTheCellsOfTheGrid(int x, int y, bool expected)
    {
        GridBounds bounds = new GridBounds(80, 25);

        Assert.Equal(expected, bounds.Contains(new Point(x, y)));
    }

    [Fact]
    public void ClampLeavesAnInsidePositionUnchanged()
    {
        GridBounds bounds = new GridBounds(80, 25);

        Assert.Equal(new Point(5, 5), bounds.Clamp(new Point(5, 5)));
    }

    [Fact]
    public void ClampPullsAnOutsidePositionToTheNearestEdge()
    {
        GridBounds bounds = new GridBounds(80, 25);

        Assert.Equal(new Point(79, 0), bounds.Clamp(new Point(200, -7)));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    [InlineData(-1, 5)]
    public void ADimensionBelowOneIsRejected(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridBounds(width, height));
    }
}

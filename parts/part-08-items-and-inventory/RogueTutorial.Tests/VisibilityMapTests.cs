/*
 * Unit tests for what the player knows about each cell. The property that matters most is that
 * memory is one-way: walking away from a cell must dim it, never blank it.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~VisibilityMapTests
 */

using System;
using System.Collections.Generic;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class VisibilityMapTests
{
    private static ISet<Point> Cells(params Point[] cells)
    {
        return new HashSet<Point>(cells);
    }

    [Fact]
    public void EveryCellStartsUnseen()
    {
        VisibilityMap visibility = new VisibilityMap(4, 3);

        for (int row = 0; row < visibility.Height; row++)
        {
            for (int col = 0; col < visibility.Width; col++)
            {
                Assert.Equal(CellVisibility.Unseen, visibility.StateAt(new Point(col, row)));
            }
        }
    }

    [Fact]
    public void ACellInSightIsVisible()
    {
        VisibilityMap visibility = new VisibilityMap(4, 3);

        visibility.Update(Cells(new Point(1, 1)));

        Assert.Equal(CellVisibility.Visible, visibility.StateAt(new Point(1, 1)));
    }

    [Fact]
    public void ACellLeftBehindIsRemembered()
    {
        // The whole point of the class: walk away and the cell dims rather than disappearing.
        VisibilityMap visibility = new VisibilityMap(4, 3);

        visibility.Update(Cells(new Point(1, 1)));
        visibility.Update(Cells(new Point(3, 1)));

        Assert.Equal(CellVisibility.Remembered, visibility.StateAt(new Point(1, 1)));
        Assert.Equal(CellVisibility.Visible, visibility.StateAt(new Point(3, 1)));
    }

    [Fact]
    public void MemoryIsNeverLost()
    {
        // Ten turns elsewhere must not blank a cell seen on the first.
        VisibilityMap visibility = new VisibilityMap(10, 3);

        visibility.Update(Cells(new Point(0, 0)));

        for (int turn = 0; turn < 10; turn++)
        {
            visibility.Update(Cells(new Point(9, 2)));
        }

        Assert.Equal(CellVisibility.Remembered, visibility.StateAt(new Point(0, 0)));
    }

    [Fact]
    public void ReturningToACellMakesItVisibleAgain()
    {
        VisibilityMap visibility = new VisibilityMap(4, 3);

        visibility.Update(Cells(new Point(1, 1)));
        visibility.Update(Cells(new Point(3, 1)));
        visibility.Update(Cells(new Point(1, 1)));

        Assert.Equal(CellVisibility.Visible, visibility.StateAt(new Point(1, 1)));
    }

    [Fact]
    public void ACellNeverSeenStaysUnseen()
    {
        VisibilityMap visibility = new VisibilityMap(4, 3);

        visibility.Update(Cells(new Point(1, 1)));
        visibility.Update(Cells(new Point(2, 1)));

        Assert.Equal(CellVisibility.Unseen, visibility.StateAt(new Point(3, 2)));
    }

    [Fact]
    public void AnEmptyUpdateClearsSightButNotMemory()
    {
        // Being struck blind should dim the map, not erase it.
        VisibilityMap visibility = new VisibilityMap(4, 3);

        visibility.Update(Cells(new Point(1, 1), new Point(2, 1)));
        visibility.Update(Cells());

        Assert.Equal(CellVisibility.Remembered, visibility.StateAt(new Point(1, 1)));
        Assert.Equal(CellVisibility.Remembered, visibility.StateAt(new Point(2, 1)));
    }

    [Fact]
    public void CellsOutsideTheMapAreIgnoredRatherThanRejected()
    {
        // A field of view near an edge legitimately contains cells past it.
        VisibilityMap visibility = new VisibilityMap(3, 3);

        visibility.Update(Cells(new Point(1, 1), new Point(-1, 0), new Point(9, 9)));

        Assert.Equal(CellVisibility.Visible, visibility.StateAt(new Point(1, 1)));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(3, 0)]
    [InlineData(0, 3)]
    public void AskingAboutACellOffTheMapIsRejected(int x, int y)
    {
        VisibilityMap visibility = new VisibilityMap(3, 3);

        Assert.Throws<ArgumentOutOfRangeException>(() => visibility.StateAt(new Point(x, y)));
    }

    [Fact]
    public void ANullCellSetIsRejected()
    {
        VisibilityMap visibility = new VisibilityMap(3, 3);

        Assert.Throws<ArgumentNullException>(() => visibility.Update(null!));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    public void ADimensionBelowOneIsRejected(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new VisibilityMap(width, height));
    }
}

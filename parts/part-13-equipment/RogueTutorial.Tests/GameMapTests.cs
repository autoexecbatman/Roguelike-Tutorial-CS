/*
 * Unit tests for the dungeon floor. Expected values come from the map specification: every
 * cell starts as floor, reading or writing off the map is a caller error, and asking whether
 * you may stand off the map is an ordinary question with the answer "no".
 *
 * Usage:  dotnet test --filter FullyQualifiedName~GameMapTests
 */

using System;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class GameMapTests
{
    [Fact]
    public void ANewMapIsAllFloor()
    {
        GameMap map = new GameMap(4, 3);

        // Every cell, not a sample: a fill bug that missed one row would pass a spot check.
        for (int row = 0; row < map.Height; row++)
        {
            for (int col = 0; col < map.Width; col++)
            {
                Assert.True(map.IsWalkable(new Point(col, row)), $"cell ({col},{row}) should start walkable");
            }
        }
    }

    [Fact]
    public void TheMapKeepsTheSizeItWasGiven()
    {
        GameMap map = new GameMap(80, 25);

        Assert.Equal(80, map.Width);
        Assert.Equal(25, map.Height);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(3, 2, true)]
    [InlineData(4, 2, false)]
    [InlineData(3, 3, false)]
    [InlineData(-1, 0, false)]
    public void IsInBoundsAcceptsExactlyTheCellsOfTheMap(int x, int y, bool expected)
    {
        GameMap map = new GameMap(4, 3);

        Assert.Equal(expected, map.IsInBounds(new Point(x, y)));
    }

    [Fact]
    public void AWallIsNotWalkable()
    {
        GameMap map = new GameMap(4, 3);

        map.SetTile(new Point(1, 1), TileTypes.Wall);

        Assert.False(map.IsWalkable(new Point(1, 1)));
    }

    [Fact]
    public void SettingOneTileLeavesItsNeighboursAlone()
    {
        GameMap map = new GameMap(4, 3);

        map.SetTile(new Point(1, 1), TileTypes.Wall);

        // A row-major indexing error would most likely show up on the neighbours.
        Assert.True(map.IsWalkable(new Point(0, 1)));
        Assert.True(map.IsWalkable(new Point(2, 1)));
        Assert.True(map.IsWalkable(new Point(1, 0)));
        Assert.True(map.IsWalkable(new Point(1, 2)));
    }

    [Fact]
    public void SetTileAndGetTileAgreeOnWhichCellIsWhich()
    {
        GameMap map = new GameMap(4, 3);

        // (2,0) and its transpose (0,2) are both on a 4x3 map, so swapping x and y
        // in the index would write to the wrong one of them and this would catch it.
        map.SetTile(new Point(2, 0), TileTypes.Wall);

        Assert.Equal('#', map.GetTile(new Point(2, 0)).Glyph);
        Assert.Equal('.', map.GetTile(new Point(0, 2)).Glyph);
    }

    [Fact]
    public void WalkingOffTheMapIsNotPossible()
    {
        GameMap map = new GameMap(4, 3);

        // Off the map answers false rather than throwing, so movement code can ask freely.
        Assert.False(map.IsWalkable(new Point(-1, 0)));
        Assert.False(map.IsWalkable(new Point(4, 0)));
        Assert.False(map.IsWalkable(new Point(0, -1)));
        Assert.False(map.IsWalkable(new Point(0, 3)));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(4, 0)]
    [InlineData(0, 3)]
    public void ReadingOffTheMapIsRejected(int x, int y)
    {
        GameMap map = new GameMap(4, 3);

        Assert.Throws<ArgumentOutOfRangeException>(() => map.GetTile(new Point(x, y)));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(4, 0)]
    [InlineData(0, 3)]
    public void WritingOffTheMapIsRejected(int x, int y)
    {
        GameMap map = new GameMap(4, 3);

        Assert.Throws<ArgumentOutOfRangeException>(() => map.SetTile(new Point(x, y), TileTypes.Wall));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    public void ADimensionBelowOneIsRejected(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameMap(width, height));
    }
}

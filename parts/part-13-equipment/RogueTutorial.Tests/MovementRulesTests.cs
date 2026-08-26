/*
 * Unit tests for where a move ends up. The rule under test is the one that replaced Part 1's
 * clamping: a blocked move returns the starting cell unchanged, rather than sliding to the
 * nearest legal one.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~MovementRulesTests
 */

using System;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class MovementRulesTests
{
    // A 5x5 room of floor with a single wall at (2,2), so every direction can be tried.
    private static GameMap MapWithACentralWall()
    {
        GameMap map = new GameMap(5, 5);
        map.SetTile(new Point(2, 2), TileTypes.Wall);
        return map;
    }

    [Fact]
    public void AStepOntoFloorLands()
    {
        Point destination = MovementRules.DestinationFor(new Point(1, 1), new Point(1, 0), MapWithACentralWall());

        Assert.Equal(new Point(2, 1), destination);
    }

    [Fact]
    public void ADiagonalStepOntoFloorLands()
    {
        Point destination = MovementRules.DestinationFor(new Point(0, 0), new Point(1, 1), MapWithACentralWall());

        Assert.Equal(new Point(1, 1), destination);
    }

    [Theory]
    [InlineData(1, 2, 1, 0)]    // walking right into the wall
    [InlineData(3, 2, -1, 0)]   // walking left into it
    [InlineData(2, 1, 0, 1)]    // walking down into it
    [InlineData(2, 3, 0, -1)]   // walking up into it
    [InlineData(1, 1, 1, 1)]    // walking diagonally into it
    public void AStepIntoAWallIsRefused(int startX, int startY, int offsetX, int offsetY)
    {
        Point start = new Point(startX, startY);

        Point destination = MovementRules.DestinationFor(start, new Point(offsetX, offsetY), MapWithACentralWall());

        // The whole point of the rule: unchanged, not adjusted to a neighbouring cell.
        Assert.Equal(start, destination);
    }

    [Theory]
    [InlineData(0, 1, -1, 0)]   // off the left edge
    [InlineData(4, 1, 1, 0)]    // off the right edge
    [InlineData(1, 0, 0, -1)]   // off the top
    [InlineData(1, 4, 0, 1)]    // off the bottom
    public void AStepOffTheMapIsRefused(int startX, int startY, int offsetX, int offsetY)
    {
        Point start = new Point(startX, startY);

        Point destination = MovementRules.DestinationFor(start, new Point(offsetX, offsetY), MapWithACentralWall());

        Assert.Equal(start, destination);
    }

    [Fact]
    public void ARefusedMoveDoesNotSlideAlongTheWall()
    {
        // Part 1 clamped, which for a diagonal into a corner would have moved one axis anyway.
        // The rule now is all or nothing, so this must not become (1,2) or (2,1).
        Point start = new Point(1, 1);

        Point destination = MovementRules.DestinationFor(start, new Point(1, 1), MapWithACentralWall());

        Assert.Equal(new Point(1, 1), destination);
    }

    [Fact]
    public void AZeroOffsetStaysPut()
    {
        Point destination = MovementRules.DestinationFor(new Point(3, 3), Point.Zero, MapWithACentralWall());

        Assert.Equal(new Point(3, 3), destination);
    }

    [Fact]
    public void ANullMapIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => MovementRules.DestinationFor(Point.Zero, new Point(1, 0), null!));
    }
}

/*
 * Unit tests for the key-to-offset table. Expected values come from the movement
 * specification - one cell per cardinal key, two axes at once on a keypad corner -
 * not from what the code currently returns.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~MovementKeysTests
 */

using System;
using System.Collections.Generic;
using RogueTutorial;
using SadConsole.Input;
using SadRogue.Primitives;
using Xunit;

public sealed class MovementKeysTests
{
    [Theory]
    [InlineData(Keys.Left, -1, 0)]
    [InlineData(Keys.Right, 1, 0)]
    [InlineData(Keys.Up, 0, -1)]
    [InlineData(Keys.Down, 0, 1)]
    [InlineData(Keys.NumPad4, -1, 0)]
    [InlineData(Keys.NumPad6, 1, 0)]
    [InlineData(Keys.NumPad8, 0, -1)]
    [InlineData(Keys.NumPad2, 0, 1)]
    public void ACardinalKeyMovesOneCellOnOneAxis(Keys key, int expectedX, int expectedY)
    {
        Assert.Equal(new Point(expectedX, expectedY), MovementKeys.OffsetFor(new[] { key }));
    }

    [Theory]
    [InlineData(Keys.NumPad7, -1, -1)]
    [InlineData(Keys.NumPad9, 1, -1)]
    [InlineData(Keys.NumPad1, -1, 1)]
    [InlineData(Keys.NumPad3, 1, 1)]
    public void AKeypadCornerMovesOneCellOnBothAxes(Keys key, int expectedX, int expectedY)
    {
        Assert.Equal(new Point(expectedX, expectedY), MovementKeys.OffsetFor(new[] { key }));
    }

    [Fact]
    public void TwoCardinalKeysCombineIntoADiagonal()
    {
        Assert.Equal(new Point(-1, -1), MovementKeys.OffsetFor(new[] { Keys.Left, Keys.Up }));
    }

    [Fact]
    public void OpposingKeysCancel()
    {
        Assert.Equal(Point.Zero, MovementKeys.OffsetFor(new[] { Keys.Left, Keys.Right }));
    }

    [Fact]
    public void AKeyWithNoMovementMeaningIsIgnored()
    {
        Assert.Equal(Point.Zero, MovementKeys.OffsetFor(new[] { Keys.A, Keys.Escape }));
    }

    [Fact]
    public void NoKeysMeansNoMove()
    {
        Assert.Equal(Point.Zero, MovementKeys.OffsetFor(Array.Empty<Keys>()));
    }

    [Fact]
    public void ANullCollectionIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => MovementKeys.OffsetFor(null!));
    }
}

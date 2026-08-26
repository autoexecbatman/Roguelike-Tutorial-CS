/*
 * Integration tests: the key table and the position rules composed, which is the path
 * RootScreen.ProcessKeyboard actually walks. Unit tests cover each half; these cover the
 * seam, where a sign convention or an axis swap would survive both halves being right.
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
    // Applies a sequence of key presses one frame at a time, as the game loop would.
    private static Point PositionAfter(GridBounds bounds, Point start, IEnumerable<Keys[]> framesOfKeys)
    {
        PlayerMover mover = new PlayerMover(bounds, start);

        foreach (Keys[] keysThisFrame in framesOfKeys)
        {
            mover.Move(MovementKeys.OffsetFor(keysThisFrame));
        }

        return mover.Position;
    }

    [Fact]
    public void PressingUpMovesTowardTheTopOfTheScreen()
    {
        // Y grows downward on a console grid, so "up" must decrease Y.
        Point result = PositionAfter(new GridBounds(80, 25), new Point(40, 12), new[] { new[] { Keys.Up } });

        Assert.Equal(new Point(40, 11), result);
    }

    [Fact]
    public void FourFramesOfRightMoveFourCells()
    {
        Point result = PositionAfter(
            new GridBounds(80, 25),
            new Point(10, 10),
            new[] { new[] { Keys.Right }, new[] { Keys.Right }, new[] { Keys.Right }, new[] { Keys.Right } });

        Assert.Equal(new Point(14, 10), result);
    }

    [Fact]
    public void HoldingAgainstTheLeftWallStopsRatherThanWrapping()
    {
        // Ten presses from column 2 would reach -8 without the clamp.
        List<Keys[]> tenPressesLeft = new List<Keys[]>();
        for (int frame = 0; frame < 10; frame++)
        {
            tenPressesLeft.Add(new[] { Keys.Left });
        }

        Point result = PositionAfter(new GridBounds(80, 25), new Point(2, 5), tenPressesLeft);

        Assert.Equal(new Point(0, 5), result);
    }

    [Fact]
    public void AKeypadCornerReachesTheSameCellAsTwoCardinals()
    {
        GridBounds bounds = new GridBounds(80, 25);
        Point start = new Point(40, 12);

        Point viaCorner = PositionAfter(bounds, start, new[] { new[] { Keys.NumPad7 } });
        Point viaCardinals = PositionAfter(bounds, start, new[] { new[] { Keys.Left, Keys.Up } });

        Assert.Equal(viaCorner, viaCardinals);
    }

    [Fact]
    public void TheBottomRightCornerIsReachableAndIsTheLastCell()
    {
        GridBounds bounds = new GridBounds(80, 25);

        Point result = PositionAfter(bounds, new Point(40, 12), new[] { new[] { Keys.NumPad3 } , new[] { Keys.NumPad3 } });

        Assert.Equal(new Point(42, 14), result);
        Assert.True(bounds.Contains(result));
    }
}

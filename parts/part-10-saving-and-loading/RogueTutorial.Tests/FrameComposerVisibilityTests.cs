/*
 * Unit tests for drawing what the player perceives rather than what is there. Expected pictures
 * are written as ASCII, so a failure prints as a shape.
 *
 * A space means never seen. The glyph itself means seen - lit or remembered - and colour is
 * what separates those two, so the tests that care about dimming check the colour directly.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~FrameComposerVisibilityTests
 */

using System;
using System.Collections.Generic;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class FrameComposerVisibilityTests
{
    private static GameMap OpenMap(int width, int height)
    {
        return new GameMap(width, height);
    }

    private static ISet<Point> Cells(params Point[] cells)
    {
        return new HashSet<Point>(cells);
    }

    private static string Picture(params string[] rows)
    {
        return string.Join("\n", rows);
    }

    [Fact]
    public void NothingIsDrawnBeforeAnythingIsSeen()
    {
        GameMap map = OpenMap(3, 2);
        VisibilityMap visibility = new VisibilityMap(3, 2);

        RenderedFrame frame = FrameComposer.Compose(map, Array.Empty<Entity>(), visibility);

        Assert.Equal(
            Picture(
                "   ",
                "   "),
            frame.ToText());
    }

    [Fact]
    public void OnlyWhatHasBeenSeenIsDrawn()
    {
        GameMap map = OpenMap(4, 2);
        VisibilityMap visibility = new VisibilityMap(4, 2);

        visibility.Update(Cells(new Point(0, 0), new Point(1, 0)));

        RenderedFrame frame = FrameComposer.Compose(map, Array.Empty<Entity>(), visibility);

        Assert.Equal(
            Picture(
                "..  ",
                "    "),
            frame.ToText());
    }

    [Fact]
    public void ARememberedCellIsStillDrawn()
    {
        GameMap map = OpenMap(4, 1);
        VisibilityMap visibility = new VisibilityMap(4, 1);

        visibility.Update(Cells(new Point(0, 0)));
        visibility.Update(Cells(new Point(3, 0)));

        RenderedFrame frame = FrameComposer.Compose(map, Array.Empty<Entity>(), visibility);

        // Both ends drawn: one lit, one from memory. The glyph does not distinguish them.
        Assert.Equal(".  .", frame.ToText());
    }

    [Fact]
    public void ARememberedCellIsDimmerThanALitOne()
    {
        GameMap map = OpenMap(4, 1);
        VisibilityMap visibility = new VisibilityMap(4, 1);

        visibility.Update(Cells(new Point(0, 0)));
        visibility.Update(Cells(new Point(3, 0)));

        RenderedFrame frame = FrameComposer.Compose(map, Array.Empty<Entity>(), visibility);

        Color remembered = frame.ForegroundAt(new Point(0, 0));
        Color lit = frame.ForegroundAt(new Point(3, 0));

        Assert.True(remembered.R < lit.R, $"remembered {remembered.R} should be dimmer than lit {lit.R}");
    }

    [Fact]
    public void AnEntityInSightIsDrawn()
    {
        GameMap map = OpenMap(3, 1);
        VisibilityMap visibility = new VisibilityMap(3, 1);
        Entity player = new Entity("Player", '@', Color.White, new Point(1, 0), blocksMovement: true);

        visibility.Update(Cells(new Point(0, 0), new Point(1, 0), new Point(2, 0)));

        RenderedFrame frame = FrameComposer.Compose(map, new[] { player }, visibility);

        Assert.Equal(".@.", frame.ToText());
    }

    [Fact]
    public void AnEntityInTheDarkIsHidden()
    {
        // Creatures are not remembered. A monster you walked away from must not stay painted
        // where you last saw it, or the player chases a ghost.
        GameMap map = OpenMap(4, 1);
        VisibilityMap visibility = new VisibilityMap(4, 1);
        Entity monster = new Entity("Rat", 'r', Color.Red, new Point(0, 0), blocksMovement: true);

        visibility.Update(Cells(new Point(0, 0)));
        visibility.Update(Cells(new Point(3, 0)));

        RenderedFrame frame = FrameComposer.Compose(map, new[] { monster }, visibility);

        // The floor it stood on is remembered and drawn; the monster itself is not.
        Assert.Equal(".  .", frame.ToText());
    }

    [Fact]
    public void AnEntityOnANeverSeenCellIsHidden()
    {
        GameMap map = OpenMap(3, 1);
        VisibilityMap visibility = new VisibilityMap(3, 1);
        Entity monster = new Entity("Rat", 'r', Color.Red, new Point(2, 0), blocksMovement: true);

        visibility.Update(Cells(new Point(0, 0)));

        RenderedFrame frame = FrameComposer.Compose(map, new[] { monster }, visibility);

        Assert.Equal(".  ", frame.ToText());
    }

    [Fact]
    public void WallsAndFloorBothCarryTheirOwnGlyph()
    {
        GameMap map = OpenMap(3, 1);
        map.SetTile(new Point(1, 0), TileTypes.Wall);

        VisibilityMap visibility = new VisibilityMap(3, 1);
        visibility.Update(Cells(new Point(0, 0), new Point(1, 0), new Point(2, 0)));

        RenderedFrame frame = FrameComposer.Compose(map, Array.Empty<Entity>(), visibility);

        Assert.Equal(".#.", frame.ToText());
    }

    [Fact]
    public void ANullVisibilityMapIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => FrameComposer.Compose(OpenMap(3, 3), Array.Empty<Entity>(), null!));
    }
}

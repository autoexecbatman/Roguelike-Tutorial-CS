/*
 * Unit tests for what should appear on screen. These are the tests Part 1 could not have:
 * because the frame is data rather than pixels, an expected picture is written here as ASCII
 * and compared as a string.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~FrameComposerTests
 */

using System;
using System.Collections.Generic;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class FrameComposerTests
{
    // Joins expected rows with the same separator ToText uses, so a test reads as a picture.
    private static string Picture(params string[] rows)
    {
        return string.Join("\n", rows);
    }

    [Fact]
    public void AnEmptyMapDrawsAsAllFloor()
    {
        GameMap map = new GameMap(3, 2);

        RenderedFrame frame = FrameComposer.Compose(map, Array.Empty<Entity>());

        Assert.Equal(
            Picture(
                "...",
                "..."),
            frame.ToText());
    }

    [Fact]
    public void WallsDrawWhereTheyWereSet()
    {
        GameMap map = new GameMap(3, 3);
        map.SetTile(new Point(0, 0), TileTypes.Wall);
        map.SetTile(new Point(2, 2), TileTypes.Wall);

        RenderedFrame frame = FrameComposer.Compose(map, Array.Empty<Entity>());

        Assert.Equal(
            Picture(
                "#..",
                "...",
                "..#"),
            frame.ToText());
    }

    [Fact]
    public void AnEntityDrawsOverTheMap()
    {
        GameMap map = new GameMap(3, 2);
        Entity player = new Entity("Player", '@', Color.White, new Point(1, 1), blocksMovement: true);

        RenderedFrame frame = FrameComposer.Compose(map, new[] { player });

        Assert.Equal(
            Picture(
                "...",
                ".@."),
            frame.ToText());
    }

    [Fact]
    public void SeveralEntitiesAllDraw()
    {
        GameMap map = new GameMap(4, 2);
        Entity player = new Entity("Player", '@', Color.White, new Point(0, 0), blocksMovement: true);
        Entity villager = new Entity("Villager", 'V', Color.Yellow, new Point(3, 1), blocksMovement: true);

        RenderedFrame frame = FrameComposer.Compose(map, new[] { player, villager });

        Assert.Equal(
            Picture(
                "@...",
                "...V"),
            frame.ToText());
    }

    [Fact]
    public void ALaterEntityCoversAnEarlierOneOnTheSameCell()
    {
        GameMap map = new GameMap(2, 1);
        Entity underneath = new Entity("Corpse", '%', Color.Red, new Point(0, 0), blocksMovement: false);
        Entity onTop = new Entity("Player", '@', Color.White, new Point(0, 0), blocksMovement: true);

        RenderedFrame frame = FrameComposer.Compose(map, new[] { underneath, onTop });

        Assert.Equal("@.", frame.ToText());
    }

    [Fact]
    public void AnEntityOffTheMapIsSkippedRatherThanThrowing()
    {
        GameMap map = new GameMap(2, 1);
        Entity stray = new Entity("Stray", 'S', Color.Green, new Point(9, 9), blocksMovement: true);

        RenderedFrame frame = FrameComposer.Compose(map, new[] { stray });

        Assert.Equal("..", frame.ToText());
    }

    [Fact]
    public void TheEntityColourReachesTheFrame()
    {
        GameMap map = new GameMap(2, 1);
        Entity villager = new Entity("Villager", 'V', Color.Yellow, new Point(1, 0), blocksMovement: true);

        RenderedFrame frame = FrameComposer.Compose(map, new[] { villager });

        // ToText only carries glyphs, so colour needs its own check.
        Assert.Equal(Color.Yellow, frame.ForegroundAt(new Point(1, 0)));
    }

    [Fact]
    public void TheFrameMatchesTheMapSize()
    {
        GameMap map = new GameMap(7, 3);

        RenderedFrame frame = FrameComposer.Compose(map, Array.Empty<Entity>());

        Assert.Equal(7, frame.Width);
        Assert.Equal(3, frame.Height);
    }

    [Fact]
    public void ANullMapIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => FrameComposer.Compose(null!, Array.Empty<Entity>()));
    }

    [Fact]
    public void ANullEntityListIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => FrameComposer.Compose(new GameMap(2, 2), null!));
    }
}

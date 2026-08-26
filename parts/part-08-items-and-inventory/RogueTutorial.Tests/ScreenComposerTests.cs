/*
 * Unit tests for the whole screen: the dungeon, the health bar and the log together.
 *
 * These are the tests Part 6 could not have. The log filled up and nobody could check what
 * reached the screen; composing the interface as data before drawing it makes a wrong health
 * bar or a missing log line an ordinary assertion.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~ScreenComposerTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class ScreenComposerTests
{
    // A small open room, sized to the layout's map area so the composer accepts it.
    private static GameWorld WorldFor(ScreenLayout layout)
    {
        GameMap map = new GameMap(layout.WindowWidth, layout.MapHeight);
        map.Fill(TileTypes.Floor);

        Entity player = new Entity("Player", '@', Color.White, new Point(2, 1), blocksMovement: true);
        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2);

        return new GameWorld(map, new List<Entity> { player }, player);
    }

    // The text of one row of the composed screen.
    private static string Row(RenderedFrame frame, int row)
    {
        return frame.ToText().Split('\n')[row];
    }

    [Fact]
    public void TheFrameIsTheSizeOfTheWindow()
    {
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);

        RenderedFrame frame = ScreenComposer.Compose(WorldFor(layout), layout);

        Assert.Equal(30, frame.Width);
        Assert.Equal(10, frame.Height);
    }

    [Fact]
    public void TheHealthBarIsOnTheStatusRow()
    {
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);
        GameWorld world = WorldFor(layout);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.StartsWith("HP: 30/30 ", Row(frame, layout.StatusRow));
    }

    [Fact]
    public void TheHealthBarFollowsDamage()
    {
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);
        GameWorld world = WorldFor(layout);

        world.Player.Fighter!.TakeDamage(6);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.StartsWith("HP: 24/30 ", Row(frame, layout.StatusRow));
    }

    [Fact]
    public void TheBarIsAGaugeRatherThanTheWholeRow()
    {
        // A bar stretched across the window reads as a wall rather than as a gauge, and it
        // leaves nowhere to put a dungeon level or anything else on the status row.
        ScreenLayout layout = new ScreenLayout(80, 25, logRows: 5);
        GameWorld world = WorldFor(layout);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        string statusRow = Row(frame, layout.StatusRow);

        Assert.Equal(24, statusRow.TrimEnd().Length);
        Assert.EndsWith("                                                        ", statusRow);
    }

    [Fact]
    public void ANarrowWindowGetsAShorterBarRatherThanAnError()
    {
        ScreenLayout layout = new ScreenLayout(16, 10, logRows: 3);
        GameWorld world = WorldFor(layout);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.Equal("HP: 30/30 ======", Row(frame, layout.StatusRow));
    }

    [Fact]
    public void ADeadPlayerGetsANoticeRatherThanABar()
    {
        // A corpse has no Fighter, so there are no numbers to read; the row must not be blank.
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);
        GameWorld world = WorldFor(layout);

        world.Player.Fighter!.TakeDamage(30);
        world.Player.Die();

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.StartsWith("You are dead.", Row(frame, layout.StatusRow));
    }

    [Fact]
    public void TheNewestLogLineIsAtTheBottom()
    {
        // Oldest first, so the log reads top to bottom and the newest message is nearest the
        // bottom of the screen where the eye already is.
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);
        GameWorld world = WorldFor(layout);

        world.Log.Add("first");
        world.Log.Add("second");
        world.Log.Add("third");

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.StartsWith("first", Row(frame, layout.LogTopRow));
        Assert.StartsWith("second", Row(frame, layout.LogTopRow + 1));
        Assert.StartsWith("third", Row(frame, layout.LogTopRow + 2));
    }

    [Fact]
    public void OnlyTheNewestLinesThatFitAreShown()
    {
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);
        GameWorld world = WorldFor(layout);

        foreach (string message in new[] { "one", "two", "three", "four", "five" })
        {
            world.Log.Add(message);
        }

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.StartsWith("three", Row(frame, layout.LogTopRow));
        Assert.StartsWith("five", Row(frame, layout.LogTopRow + 2));
    }

    [Fact]
    public void AnEmptyLogLeavesItsRowsBlank()
    {
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);

        RenderedFrame frame = ScreenComposer.Compose(WorldFor(layout), layout);

        for (int line = 0; line < layout.LogRows; line++)
        {
            Assert.Equal(new string(' ', 30), Row(frame, layout.LogTopRow + line));
        }
    }

    [Fact]
    public void TheMapIsDrawnInTheMapArea()
    {
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);
        GameWorld world = WorldFor(layout);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        // The player is at (2,1), which is inside the map area and so appears unchanged.
        Assert.Equal('@', frame.GlyphAt(new Point(2, 1)));
    }

    [Fact]
    public void TheMapNeverReachesTheStatusRow()
    {
        // The panel is not part of the dungeon: a map drawn one row too tall would overwrite
        // the health bar, and the bar would simply vanish with nothing else reporting it.
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);
        GameWorld world = WorldFor(layout);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.StartsWith("HP: ", Row(frame, layout.StatusRow));
    }

    [Fact]
    public void ALongMessageIsCutRatherThanWrapped()
    {
        // Wrapping would push later lines down and change how many fit, so the row count would
        // depend on the text. Part 8 gives the log room to wrap properly.
        ScreenLayout layout = new ScreenLayout(20, 10, logRows: 3);
        GameWorld world = WorldFor(layout);

        world.Log.Add("a message far longer than twenty columns");

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.Equal("a message far longer", Row(frame, layout.LogTopRow));
        Assert.Equal(new string(' ', 20), Row(frame, layout.LogTopRow + 1));
    }

    [Fact]
    public void AMapTooTallForTheLayoutIsRejected()
    {
        // A world generated against a different layout would be silently cropped otherwise.
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);

        GameMap tooTall = new GameMap(30, 9);
        tooTall.Fill(TileTypes.Floor);

        Entity player = new Entity("Player", '@', Color.White, new Point(1, 1), blocksMovement: true);
        player.Fighter = new Fighter(30, 5, 2);

        GameWorld world = new GameWorld(tooTall, new List<Entity> { player }, player);

        Assert.Throws<ArgumentException>(() => ScreenComposer.Compose(world, layout));
    }

    [Fact]
    public void ThePackIsNotDrawnWhilePlaying()
    {
        ScreenLayout layout = new ScreenLayout(60, 12, logRows: 3);
        GameWorld world = WorldFor(layout);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.DoesNotContain("Pack", Row(frame, 0));
    }

    [Fact]
    public void ThePackIsFramedSoItReadsAsAPanel()
    {
        // Without a frame the text sits on the map and reads as corruption rather than as an
        // interface, which no other check can see.
        ScreenLayout layout = new ScreenLayout(60, 12, logRows: 3);
        GameWorld world = WorldFor(layout);

        world.SetMode(GameMode.ShowingInventory);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.StartsWith("+---", Row(frame, 0));
        Assert.StartsWith("| Pack", Row(frame, 1));
    }

    [Fact]
    public void AnEmptyPackSaysSoRatherThanShowingAnEmptyBox()
    {
        ScreenLayout layout = new ScreenLayout(60, 12, logRows: 3);
        GameWorld world = WorldFor(layout);

        world.SetMode(GameMode.ShowingInventory);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.Contains("nothing carried", Row(frame, 4));
    }

    [Fact]
    public void CarriedItemsAreLetteredFromA()
    {
        // The letters must match what CommandReader turns a key into, or pressing 'b' uses the
        // wrong potion - which is the kind of bug a player blames on themselves.
        ScreenLayout layout = new ScreenLayout(60, 12, logRows: 3);
        GameWorld world = WorldFor(layout);

        world.Player.Inventory = new Inventory(capacity: 26);

        foreach (string name in new[] { "first potion", "second potion" })
        {
            Entity item = new Entity(name, '!', Color.Magenta, new Point(0, 0), blocksMovement: false);
            item.Consumable = new Consumable(ConsumableKind.Healing, 4);
            world.Player.Inventory.TryAdd(item);
        }

        world.SetMode(GameMode.ShowingInventory);

        RenderedFrame frame = ScreenComposer.Compose(world, layout);

        Assert.Contains("a) first potion", Row(frame, 4));
        Assert.Contains("b) second potion", Row(frame, 5));
    }

    [Fact]
    public void ANullArgumentIsRejected()
    {
        ScreenLayout layout = new ScreenLayout(30, 10, logRows: 3);

        Assert.Throws<ArgumentNullException>(() => ScreenComposer.Compose(null!, layout));
        Assert.Throws<ArgumentNullException>(() => ScreenComposer.Compose(WorldFor(layout), null!));
    }
}

/*
 * Unit tests for abandoning a run.
 *
 * This exists because of a state the game could otherwise reach and never leave: kill every
 * monster and nothing can hurt you, there is nowhere to descend to, and Part 10's save writes
 * that dead end to disk after every turn. Dying is the only ending, and a cleared dungeon has
 * removed the only thing that could kill you.
 *
 * The confirmation is not politeness. A single key that destroys a run somebody is winning is a
 * worse bug than the one being fixed.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~NewGameTests
 */

using System;
using System.Collections.Generic;
using RogueTutorial;
using SadConsole.Input;
using SadRogue.Primitives;
using Xunit;

public sealed class NewGameTests
{
    private static GameWorld World()
    {
        GameMap map = new GameMap(9, 9);
        map.Fill(TileTypes.Floor);

        Entity player = new Entity("Player", '@', Color.White, new Point(4, 4), blocksMovement: true);
        player.Fighter = new Fighter(30, 5, 2);
        player.Inventory = new Inventory(26);

        return new GameWorld(map, new List<Entity> { player }, player);
    }

    [Fact]
    public void NAsksRatherThanActs()
    {
        Assert.Equal(GameCommandKind.AskNewGame, CommandReader.Read(new[] { Keys.N }, GameMode.Playing).Kind);
    }

    [Fact]
    public void YConfirms()
    {
        Assert.Equal(
            GameCommandKind.ConfirmNewGame,
            CommandReader.Read(new[] { Keys.Y }, GameMode.ConfirmingNewGame).Kind);
    }

    [Fact]
    public void AnythingElseCancels()
    {
        // A player having second thoughts should not have to find the one correct way to say no.
        foreach (Keys key in new[] { Keys.Escape, Keys.N, Keys.Left, Keys.Space, Keys.A })
        {
            Assert.Equal(
                GameCommandKind.CancelNewGame,
                CommandReader.Read(new[] { key }, GameMode.ConfirmingNewGame).Kind);
        }
    }

    [Fact]
    public void NoKeyDoesNothing()
    {
        // Holding no keys is not an answer, and must not be read as one.
        Assert.Equal(
            GameCommandKind.None,
            CommandReader.Read(Array.Empty<Keys>(), GameMode.ConfirmingNewGame).Kind);
    }

    [Fact]
    public void TheQuestionCostsNoTurn()
    {
        // Asking is not acting: a monster must not get a swing because the player considered it.
        GameWorld world = World();

        world.SetMode(GameMode.ConfirmingNewGame);

        Assert.Equal(GameMode.ConfirmingNewGame, world.Mode);
        Assert.Equal(30, world.Player.Fighter!.HitPoints);
    }

    [Fact]
    public void BackingOutReturnsToTheMap()
    {
        GameWorld world = World();

        world.SetMode(GameMode.ConfirmingNewGame);
        world.SetMode(GameMode.Playing);

        Assert.Equal(GameMode.Playing, world.Mode);
    }

    [Fact]
    public void MovementKeysDoNotMoveWhileTheQuestionIsUp()
    {
        // Every key means yes or no here, so nothing else can be pressed by accident. A left
        // arrow that both cancelled and moved would be a surprise in the middle of a fight.
        GameCommand command = CommandReader.Read(new[] { Keys.Left }, GameMode.ConfirmingNewGame);

        Assert.Equal(GameCommandKind.CancelNewGame, command.Kind);
        Assert.Equal(Point.Zero, command.Offset);
    }

    [Fact]
    public void NMeansNothingWhileThePackIsOpen()
    {
        // The pack's letters are slots. 'n' there is the fourteenth item, not a new game.
        GameCommand command = CommandReader.Read(new[] { Keys.N }, GameMode.ShowingInventory);

        Assert.Equal(GameCommandKind.UseItem, command.Kind);
        Assert.Equal(13, command.Slot);
    }

    [Fact]
    public void NMeansNothingWhileAiming()
    {
        // Aiming is resolved with Enter or Escape; a stray letter must not abandon the run.
        Assert.Equal(GameCommandKind.None, CommandReader.Read(new[] { Keys.N }, GameMode.Targeting).Kind);
    }
}

/*
 * The top-level screen: it wires SadConsole's window and keyboard to the game world, and blits
 * the frame ScreenComposer builds - which from Part 7 is the whole screen, interface included,
 * rather than just the map. It owns no rules and, from Part 5, no state either - the map,
 * the entities and what the player has seen all live on GameWorld, which can be built and
 * driven in a test process.
 *
 * Usage - SadConsole constructs this itself, because Program.cs named it as the starting
 * screen, so it needs a public parameterless constructor:
 *
 *     new Builder().SetStartingScreen<RootScreen>()
 *
 * Constructing it in a test process throws: the constructor reads Game.Instance for the grid
 * size, and that requires a live graphics host. Test GameWorld instead.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class RootScreen : ScreenObject
{
    // How many rows of message log are shown. Five is enough to follow a fight without taking
    // so much of the window that the dungeon becomes cramped.
    private const int LogRows = 5;

    // The surface every glyph is drawn onto. One cell per grid position.
    private readonly ScreenSurface _mapSurface;

    // Where the map, the health bar and the log each sit in the window.
    private readonly ScreenLayout _layout;

    // The dungeon, everyone standing in it, and what the player has seen.
    private readonly GameWorld _world;

    /// <summary>
    /// Sizes the surface to the window, generates a world to fill it, and paints the first frame.
    /// </summary>
    public RootScreen()
    {
        // Match the surface to the window so no part of the grid is off screen.
        _mapSurface = new ScreenSurface(Game.Instance.ScreenCellsX, Game.Instance.ScreenCellsY);

        // Children are drawn and updated by the base class once added.
        Children.Add(_mapSurface);

        _layout = new ScreenLayout(
            _mapSurface.Surface.Width, _mapSurface.Surface.Height, logRows: LogRows);

        // The dungeon fills the map area rather than the window: the panel takes the rest.
        // No seed is given, so every run is a different dungeon with different monsters. Pass a
        // number to Random's constructor to play the same one repeatedly while debugging.
        _world = GameWorld.Generate(
            _layout.WindowWidth, _layout.MapHeight, new Random(), MonsterTable.Standard, ItemTable.Standard);

        DrawFrame();
    }

    /// <summary>
    /// Turns the keys held this frame into one move. Returns true whenever a movement key was
    /// pressed, even when a wall or a monster refused the move: the key was considered and
    /// answered, and reporting otherwise would offer it to another screen as unhandled.
    /// </summary>
    public override bool ProcessKeyboard(Keyboard keyboard)
    {
        // Reduce SadConsole's key objects to the bare enum the command reader expects.
        IReadOnlyCollection<Keys> pressedKeys = keyboard.KeysPressed.Select(pressed => pressed.Key).ToArray();

        bool shiftHeld = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

        // What a key means depends on what the player is doing, and the world knows which.
        GameCommand command = CommandReader.Read(pressedKeys, _world.Mode, shiftHeld);

        // A key with no meaning in this mode is not consumed, so anything else may see it.
        if (command.Kind == GameCommandKind.None)
        {
            return false;
        }

        Apply(command);

        // Every command that reaches here changed the screen: the map moved, the log gained a
        // line, or the pack opened or closed.
        DrawFrame();

        return true;
    }

    /// <summary>
    /// Hands one command to the world. Nothing is decided here - the world knows whether a slot
    /// holds anything and whether a move is legal, and this only routes.
    /// </summary>
    private void Apply(GameCommand command)
    {
        switch (command.Kind)
        {
            case GameCommandKind.Move:
                _world.MovePlayer(command.Offset);
                break;

            case GameCommandKind.PickUp:
                _world.PickUpHere();
                break;

            case GameCommandKind.OpenInventory:
                _world.SetMode(GameMode.ShowingInventory);
                break;

            case GameCommandKind.CloseInventory:
                _world.SetMode(GameMode.Playing);
                break;

            case GameCommandKind.UseItem:
                _world.UseItem(command.Slot);
                break;

            case GameCommandKind.DropItem:
                _world.DropItem(command.Slot);
                break;
        }
    }

    /// <summary>
    /// Copies the world's composed frame onto the surface, one cell at a time. Everything drawn
    /// here was already decided by FrameComposer; this only moves it to the screen.
    /// </summary>
    private void DrawFrame()
    {
        RenderedFrame frame = ScreenComposer.Compose(_world, _layout);

        for (int row = 0; row < frame.Height; row++)
        {
            for (int col = 0; col < frame.Width; col++)
            {
                Point cell = new Point(col, row);

                _mapSurface.Surface.SetGlyph(col, row, frame.GlyphAt(cell), frame.ForegroundAt(cell));
            }
        }
    }
}

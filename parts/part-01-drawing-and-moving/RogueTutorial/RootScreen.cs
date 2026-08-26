/*
 * The top-level screen: it wires SadConsole's window and keyboard to the movement rules,
 * and owns the drawing surface. All movement logic lives in PlayerMover and MovementKeys,
 * which have no dependency on a running game and are unit tested.
 *
 * Usage - SadConsole constructs this itself, because Program.cs named it as the starting
 * screen, so it needs a public parameterless constructor:
 *
 *     new Builder().SetStartingScreen<RootScreen>()
 *
 * Constructing it in a test process throws: the constructor reads Game.Instance for the
 * grid size, and that requires a live graphics host. Test the movement classes instead.
 */

using System.Collections.Generic;
using System.Linq;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class RootScreen : ScreenObject
{
    // The character drawn for the player. '@' is the roguelike convention for "you".
    private const char PlayerGlyph = '@';

    // The surface every glyph is drawn onto. One cell per grid position.
    private readonly ScreenSurface _mapSurface;

    // Holds the player's position and enforces that it stays on the grid.
    private readonly PlayerMover _playerMover;

    /// <summary>
    /// Creates the drawing surface at the size configured in Program.cs and places the
    /// player at the centre of the grid, rounded down on an odd dimension.
    /// </summary>
    public RootScreen()
    {
        // Match the surface to the window so no part of the grid is off screen.
        _mapSurface = new ScreenSurface(Game.Instance.ScreenCellsX, Game.Instance.ScreenCellsY);

        // Children are drawn and updated by the base class once added.
        Children.Add(_mapSurface);

        GridBounds bounds = new GridBounds(_mapSurface.Surface.Width, _mapSurface.Surface.Height);

        // Integer division floors, so an 80x25 grid starts the player at (40, 12).
        Point startingPosition = new Point(bounds.Width / 2, bounds.Height / 2);

        _playerMover = new PlayerMover(bounds, startingPosition);

        // Nothing has been drawn yet, so paint the first frame.
        DrawFrame();
    }

    /// <summary>
    /// Turns the keys held this frame into one move. Returns true when the player actually
    /// moved, which tells SadConsole the input was consumed; a non-movement key returns
    /// false so other screens still see it.
    /// </summary>
    public override bool ProcessKeyboard(Keyboard keyboard)
    {
        // Reduce SadConsole's key objects to the bare enum the movement table expects.
        IReadOnlyCollection<Keys> pressedKeys = keyboard.KeysPressed.Select(pressed => pressed.Key).ToArray();

        Point moveOffset = MovementKeys.OffsetFor(pressedKeys);

        // No movement key was down, so leave the frame alone and let others see the input.
        if (moveOffset == Point.Zero)
        {
            return false;
        }

        // Remember where the player was, so a move blocked by a wall does not force a repaint.
        Point positionBeforeMove = _playerMover.Position;

        _playerMover.Move(moveOffset);

        // Redrawing only on a real change keeps the wall case free of pointless work.
        if (_playerMover.Position != positionBeforeMove)
        {
            DrawFrame();
        }

        return true;
    }

    /// <summary>
    /// Repaints the whole surface: a blank grid with the player's glyph on it. Clearing
    /// every cell is what erases the glyph's previous position.
    /// </summary>
    private void DrawFrame()
    {
        // Wipe the previous frame; without this the player leaves a trail.
        _mapSurface.Surface.Clear();

        // Draw the player last so it sits on top of anything drawn before it.
        _mapSurface.Surface.SetGlyph(
            _playerMover.Position.X,
            _playerMover.Position.Y,
            PlayerGlyph,
            Color.White);
    }
}

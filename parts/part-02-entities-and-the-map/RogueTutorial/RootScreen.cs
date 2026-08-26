/*
 * The top-level screen: it wires SadConsole's window and keyboard to the game, and blits the
 * composed frame. It owns no rules. The map, the entities, where a move ends up and what the
 * picture should look like are all decided by classes that run without a graphics host.
 *
 * Usage - SadConsole constructs this itself, because Program.cs named it as the starting
 * screen, so it needs a public parameterless constructor:
 *
 *     new Builder().SetStartingScreen<RootScreen>()
 *
 * Constructing it in a test process throws: the constructor reads Game.Instance for the grid
 * size, and that requires a live graphics host. Test the rule classes instead.
 */

using System.Collections.Generic;
using System.Linq;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class RootScreen : ScreenObject
{
    // The surface every glyph is drawn onto. One cell per grid position.
    private readonly ScreenSurface _mapSurface;

    // The dungeon floor. Fixed for this part; generated for real in Part 3.
    private readonly GameMap _map;

    // Everything drawn on top of the map, in draw order: later entries cover earlier ones.
    private readonly List<Entity> _entities;

    // The entity the keyboard drives. Also present in _entities, so it is drawn like any other.
    private readonly Entity _player;

    /// <summary>
    /// Builds the room, places the player and one villager in it, and paints the first frame.
    /// The surface is sized to the window configured in Program.cs.
    /// </summary>
    public RootScreen()
    {
        // Match the surface to the window so no part of the grid is off screen.
        _mapSurface = new ScreenSurface(Game.Instance.ScreenCellsX, Game.Instance.ScreenCellsY);

        // Children are drawn and updated by the base class once added.
        Children.Add(_mapSurface);

        _map = MapFactory.CreateWalledRoom(_mapSurface.Surface.Width, _mapSurface.Surface.Height);

        // Integer division floors, so an 80x25 room starts the player at (40, 12).
        _player = new Entity("Player", '@', Color.White, new Point(_map.Width / 2, _map.Height / 2));

        // Two cells to the left of centre, which the room's proportions keep clear of a pillar.
        Entity villager = new Entity("Villager", '@', Color.Yellow, new Point((_map.Width / 2) - 2, _map.Height / 2));

        // The player is last, so it covers anything standing on the same cell.
        _entities = new List<Entity> { villager, _player };

        DrawFrame();
    }

    /// <summary>
    /// Turns the keys held this frame into one move for the player. Returns true when a
    /// movement key was pressed, even if a wall refused the move, so the key is not offered
    /// to another screen as though nothing had happened.
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

        Point destination = MovementRules.DestinationFor(_player.Position, moveOffset, _map);

        // A wall refuses the move, and repainting an unchanged frame is wasted work.
        if (destination != _player.Position)
        {
            _player.MoveTo(destination);
            DrawFrame();
        }

        return true;
    }

    /// <summary>
    /// Composes the picture and copies it onto the surface, one cell at a time. Everything
    /// decided here was already decided by FrameComposer; this only moves it to the screen.
    /// </summary>
    private void DrawFrame()
    {
        RenderedFrame frame = FrameComposer.Compose(_map, _entities);

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

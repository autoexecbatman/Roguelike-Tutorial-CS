/*
 * The top-level screen: it wires SadConsole's window and keyboard to the game, and blits the
 * composed frame. It owns no rules. From Part 4 it also recomputes the player's field of view
 * after every move, so the map is drawn as the player perceives it rather than as it is. The map, the entities, where a move ends up and what the
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

using System;
using System.Collections.Generic;
using System.Linq;
using SadConsole;
using SadConsole.Input;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class RootScreen : ScreenObject
{
    // How far the player can see, in cells. Large enough to take in a room, small enough that
    // a corridor stays dark ahead of you.
    private const int PlayerSightRadius = 8;

    // The surface every glyph is drawn onto. One cell per grid position.
    private readonly ScreenSurface _mapSurface;

    // The dungeon floor. Fixed for this part; generated for real in Part 3.
    private readonly GameMap _map;

    // Everything drawn on top of the map, in draw order: later entries cover earlier ones.
    private readonly List<Entity> _entities;

    // What the player can see now and what they remember, updated after every move.
    private readonly VisibilityMap _visibility;

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

        // No seed is given, so every run generates a different dungeon. Pass a number to
        // Random's constructor to play the same one repeatedly while debugging.
        DungeonGenerator generator = new DungeonGenerator(new DungeonSettings(
            maximumRooms: 30,
            minimumRoomSize: 6,
            maximumRoomSize: 10));

        GeneratedDungeon dungeon = generator.Generate(
            _mapSurface.Surface.Width, _mapSurface.Surface.Height, new Random());

        _map = dungeon.Map;

        // The generator decides where the player starts: the centre of the first room it placed.
        _player = new Entity("Player", '@', Color.White, dungeon.PlayerStart);

        // A villager in the last room, so there is a reason to walk the corridors.
        Entity villager = new Entity(
            "Villager", '@', Color.Yellow, dungeon.Rooms[dungeon.Rooms.Count - 1].Center);

        // The player is last, so it covers anything standing on the same cell.
        _entities = new List<Entity> { villager, _player };

        _visibility = new VisibilityMap(_map.Width, _map.Height);

        // Without this the first frame would be drawn before anything had been seen, so the
        // player would spend one frame staring at an entirely blank screen.
        RecomputeFieldOfView();

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

            // Sight is recomputed from the new position before the frame is drawn, or the
            // player would see one frame of the view from where they used to stand.
            RecomputeFieldOfView();

            DrawFrame();
        }

        return true;
    }

    /// <summary>
    /// Composes the picture and copies it onto the surface, one cell at a time. Everything
    /// decided here was already decided by FrameComposer; this only moves it to the screen.
    /// </summary>
    /// <summary>
    /// Works out what the player can see from where they now stand and folds it into what they
    /// remember. Called once at construction and after every move that changed the position.
    /// </summary>
    private void RecomputeFieldOfView()
    {
        _visibility.Update(FieldOfView.From(_player.Position, PlayerSightRadius, _map));
    }

    private void DrawFrame()
    {
        RenderedFrame frame = FrameComposer.Compose(_map, _entities, _visibility);

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

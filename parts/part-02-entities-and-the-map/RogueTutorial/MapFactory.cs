/*
 * Builds the one map this part uses: a room walled all the way round, with two pillars in it.
 *
 * Real dungeon generation - rooms joined by corridors, placed at random - arrives in Part 3.
 * This exists so there is something for walls to be, and somewhere for a wall to stop you.
 *
 * Usage:
 *
 *     GameMap map = MapFactory.CreateWalledRoom(80, 25);
 *     bool edge = map.IsWalkable(new Point(0, 0));    // -> false, the border is wall
 *     bool inside = map.IsWalkable(new Point(1, 1));  // -> true, floor
 *
 * Refuses any size below 3x3, since a room smaller than that is all border and has no inside.
 */

using System;
using SadRogue.Primitives;

namespace RogueTutorial;

internal static class MapFactory
{
    /// <summary>
    /// Returns a map whose outermost cells are wall and whose interior is floor, with two
    /// pillars placed in the middle third. Throws ArgumentOutOfRangeException below 3x3, because
    /// a smaller room has no walkable interior at all.
    /// </summary>
    public static GameMap CreateWalledRoom(int width, int height)
    {
        // Below 3x3 the border consumes the whole map and there is nowhere to stand.
        if (width < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "A room needs at least 3 cells across.");
        }
        if (height < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "A room needs at least 3 cells down.");
        }

        // Starts as all floor, so only the walls have to be written.
        GameMap room = new GameMap(width, height);

        // Top and bottom rows.
        for (int col = 0; col < width; col++)
        {
            room.SetTile(new Point(col, 0), TileTypes.Wall);
            room.SetTile(new Point(col, height - 1), TileTypes.Wall);
        }

        // Left and right columns; the corners are written twice, which is harmless.
        for (int row = 0; row < height; row++)
        {
            room.SetTile(new Point(0, row), TileTypes.Wall);
            room.SetTile(new Point(width - 1, row), TileTypes.Wall);
        }

        // Two pillars, placed by proportion so they land inside a room of any size.
        room.SetTile(new Point(width / 3, height / 2), TileTypes.Wall);
        room.SetTile(new Point((width * 2) / 3, height / 2), TileTypes.Wall);

        return room;
    }
}

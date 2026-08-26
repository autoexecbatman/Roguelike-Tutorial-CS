/*
 * The dungeon floor: a rectangle of tiles, and the questions the game asks about it.
 *
 * Usage - build one, fill it, then ask what a position permits:
 *
 *     GameMap map = new GameMap(80, 25);          // every cell starts as floor
 *     map.SetTile(new Point(5, 5), TileTypes.Wall);
 *     bool blocked = map.IsWalkable(new Point(5, 5));   // -> false
 *     bool offMap = map.IsWalkable(new Point(-1, 0));   // -> false, outside is never walkable
 *     Tile tile = map.GetTile(new Point(0, 0));         // -> TileTypes.Floor
 *
 * Refuses a position outside the map in GetTile and SetTile, because reading or writing off
 * the map is a caller error. IsWalkable answers false instead, since asking whether you may
 * step off the edge is an ordinary question.
 */

using System;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class GameMap
{
    // The rectangle of legal positions; reused from Part 1.
    private readonly GridBounds _bounds;

    // Tiles in row-major order, indexed as [y * Width + x].
    private readonly Tile[] _tiles;

    /// <summary>Number of cells across.</summary>
    public int Width => _bounds.Width;

    /// <summary>Number of cells down.</summary>
    public int Height => _bounds.Height;

    /// <summary>
    /// Creates a map of the given size with every cell set to floor. Throws
    /// ArgumentOutOfRangeException when either dimension is below one.
    /// </summary>
    public GameMap(int width, int height)
    {
        _bounds = new GridBounds(width, height);

        _tiles = new Tile[width * height];

        // A map of default-constructed tiles would be unwalkable and invisible, so fill it.
        for (int index = 0; index < _tiles.Length; index++)
        {
            _tiles[index] = TileTypes.Floor;
        }
    }

    /// <summary>True when the position is a cell of this map.</summary>
    public bool IsInBounds(Point position)
    {
        return _bounds.Contains(position);
    }

    /// <summary>
    /// Returns the tile at the position. Throws ArgumentOutOfRangeException when the position
    /// is off the map; use IsInBounds first if that is a possibility.
    /// </summary>
    public Tile GetTile(Point position)
    {
        RejectPositionOffTheMap(position, nameof(position));

        return _tiles[IndexOf(position)];
    }

    /// <summary>
    /// Replaces the tile at the position. Throws ArgumentOutOfRangeException when the position
    /// is off the map, because writing outside the map is always a mistake.
    /// </summary>
    public void SetTile(Point position, Tile tile)
    {
        RejectPositionOffTheMap(position, nameof(position));

        _tiles[IndexOf(position)] = tile;
    }

    /// <summary>
    /// True when a creature may stand at the position. Anything off the map answers false
    /// rather than throwing, so movement code can ask about the cell beyond the edge.
    /// </summary>
    public bool IsWalkable(Point position)
    {
        // Outside the map is not a tile, so there is nothing to stand on.
        if (!IsInBounds(position))
        {
            return false;
        }

        return _tiles[IndexOf(position)].IsWalkable;
    }

    // Row-major index; the single place the storage layout is expressed.
    private int IndexOf(Point position)
    {
        return (position.Y * Width) + position.X;
    }

    // Shared guard for the two methods that have no sensible answer off the map.
    private void RejectPositionOffTheMap(Point position, string parameterName)
    {
        // Reading or writing outside the map is a caller error, so fail where the mistake was made.
        if (!IsInBounds(position))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                position,
                $"The position is outside the {Width}x{Height} map.");
        }
    }
}

/*
 * The standard tile kinds, named once so a glyph or colour change happens in one place.
 *
 * Usage:
 *
 *     Tile floor = TileTypes.Floor;   // '.', dark grey, walkable, transparent
 *     Tile wall = TileTypes.Wall;     // '#', light grey, blocks movement and sight
 *
 * Add a kind here rather than constructing a Tile inline at a call site; a literal '#'
 * scattered through map generation is the thing that makes a re-theme painful later.
 */

using SadRogue.Primitives;

namespace RogueTutorial;

internal static class TileTypes
{
    /// <summary>Open ground: a creature may stand on it and see across it.</summary>
    public static Tile Floor { get; } = new Tile('.', new Color(80, 80, 80), true, true);

    /// <summary>Solid rock: blocks both movement and, from Part 4, sight.</summary>
    public static Tile Wall { get; } = new Tile('#', new Color(160, 160, 160), false, false);
}

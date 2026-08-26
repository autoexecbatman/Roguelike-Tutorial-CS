/*
 * One cell of the dungeon: what it looks like and what it permits.
 *
 * Usage - tiles are values, so construct them directly or take one of the standard kinds:
 *
 *     Tile wall = TileTypes.Wall;                        // '#', blocks movement and sight
 *     Tile floor = TileTypes.Floor;                      // '.', walkable and see-through
 *     Tile custom = new Tile('~', Color.Cyan, true, true);  // glyph, colour, walkable, transparent
 *
 * Being a readonly struct, a tile cannot be modified after construction; replace it in the
 * map instead. That is what stops one shared wall object from being edited by accident.
 */

using SadRogue.Primitives;

namespace RogueTutorial;

internal readonly struct Tile
{
    // The character drawn for this cell.
    public char Glyph { get; }

    // The colour that character is drawn in.
    public Color Foreground { get; }

    // True when a creature may stand here.
    public bool IsWalkable { get; }

    // True when sight passes through. Unused until field of view in Part 4.
    public bool IsTransparent { get; }

    /// <summary>
    /// Records the appearance and the two rules a tile carries. Every argument is explicit;
    /// there is no default kind of tile, because "the usual one" differs per caller.
    /// </summary>
    public Tile(char glyph, Color foreground, bool isWalkable, bool isTransparent)
    {
        Glyph = glyph;
        Foreground = foreground;
        IsWalkable = isWalkable;
        IsTransparent = isTransparent;
    }
}

/*
 * The game written down: plain records holding exactly what a save has to remember.
 *
 * These are separate types rather than attributes on the game classes, and that is the whole
 * design. A save format is a promise to a file somebody already has on disk; the game classes
 * change every part. Keeping them apart means a rename inside GameWorld does not silently break
 * every existing save, and it means the format is one file somebody can read to see what is
 * stored.
 *
 * What is not here is as deliberate as what is. The mode, the aiming cursor and the screen
 * layout are how the player is looking at the game rather than what the game is - restore them
 * and a save made mid-aim reopens with a crosshair over a scroll that was never fired.
 *
 * Usage - these are only ever built and read by SaveGame:
 *
 *     SavedWorld saved = SaveGame.Capture(world);
 *     string json = SaveGame.ToJson(saved);
 *     GameWorld restored = SaveGame.Restore(SaveGame.FromJson(json));
 *
 * Entities carry an id because the same entity is referenced from more than one place: the
 * player is in the entity list and named separately, and an item is either in the pack or on the
 * map. Writing the object twice would restore two of it.
 */

using System.Collections.Generic;

namespace RogueTutorial;

/// <summary>One tile, as stored. Only what cannot be recomputed.</summary>
internal sealed class SavedTile
{
    /// <summary>The character drawn for this cell.</summary>
    public char Glyph { get; set; }

    /// <summary>Packed colour, so a tile is four numbers rather than an object.</summary>
    public uint Foreground { get; set; }

    /// <summary>Whether a creature may stand here.</summary>
    public bool IsWalkable { get; set; }

    /// <summary>Whether sight passes through.</summary>
    public bool IsTransparent { get; set; }
}

/// <summary>An entity's combat numbers, or absent when it cannot fight.</summary>
internal sealed class SavedFighter
{
    /// <summary>Hit points when undamaged.</summary>
    public int MaximumHitPoints { get; set; }

    /// <summary>Hit points now.</summary>
    public int HitPoints { get; set; }

    /// <summary>How hard it hits.</summary>
    public int Attack { get; set; }

    /// <summary>How much it shrugs off.</summary>
    public int Defence { get; set; }

    /// <summary>How much killing it is worth.</summary>
    public int ExperienceAwarded { get; set; }
}

/// <summary>How far along a fighter is, or absent when it collects no experience.</summary>
internal sealed class SavedLevel
{
    /// <summary>Levels gained so far.</summary>
    public int CurrentLevel { get; set; }

    /// <summary>Experience earned toward the next one.</summary>
    public int Experience { get; set; }
}

/// <summary>What an item does, or absent when it is not an item.</summary>
internal sealed class SavedConsumable
{
    /// <summary>Which effect, stored by name so a reordered enum does not change meaning.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>How much it does it by.</summary>
    public int Power { get; set; }

    /// <summary>How far the effect spreads.</summary>
    public int Radius { get; set; }
}

/// <summary>One entity, with an id so other records can point at it.</summary>
internal sealed class SavedEntity
{
    /// <summary>Unique within one save. References elsewhere are these numbers.</summary>
    public int Id { get; set; }

    /// <summary>What it is called.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The character drawn for it.</summary>
    public char Glyph { get; set; }

    /// <summary>Packed colour.</summary>
    public uint Foreground { get; set; }

    /// <summary>Where it stands, as a column.</summary>
    public int X { get; set; }

    /// <summary>Where it stands, as a row.</summary>
    public int Y { get; set; }

    /// <summary>Whether it holds its cell against others.</summary>
    public bool BlocksMovement { get; set; }

    /// <summary>Its combat numbers, or null.</summary>
    public SavedFighter? Fighter { get; set; }

    /// <summary>What it does when used, or null.</summary>
    public SavedConsumable? Consumable { get; set; }

    /// <summary>How much it can carry, or null when it carries nothing ever.</summary>
    public int? InventoryCapacity { get; set; }

    /// <summary>The ids of what it carries, in slot order.</summary>
    public List<int> CarriedIds { get; set; } = new List<int>();

    /// <summary>How far along it is, or null when it collects no experience.</summary>
    public SavedLevel? Level { get; set; }
}

/// <summary>A whole game, as stored.</summary>
internal sealed class SavedWorld
{
    /// <summary>
    /// The format's version. A save written by a different version is refused rather than
    /// half-read, because a half-read save is a corrupt game that looks like a working one.
    /// </summary>
    public int Version { get; set; }

    /// <summary>Map width in cells.</summary>
    public int Width { get; set; }

    /// <summary>Map height in cells.</summary>
    public int Height { get; set; }

    /// <summary>
    /// The distinct tiles this map uses. A dungeon has two kinds and a thousand cells, so the
    /// kinds are listed once and the cells refer to them by position in this list.
    /// </summary>
    public List<SavedTile> TilePalette { get; set; } = new List<SavedTile>();

    /// <summary>
    /// One character per cell, row-major, each an index into TilePalette offset from 'a'. A
    /// character rather than a number so the map is one line per row in the file, which is what
    /// makes a save something a person can actually read.
    /// </summary>
    public List<string> TileRows { get; set; } = new List<string>();

    /// <summary>
    /// One character per cell, row-major: '#' where the player has been, '.' where they have
    /// not. Stored the same way and for the same reason.
    /// </summary>
    public List<string> RememberedRows { get; set; } = new List<string>();

    /// <summary>Everything in the dungeon and everything carried, in draw order.</summary>
    public List<SavedEntity> Entities { get; set; } = new List<SavedEntity>();

    /// <summary>Which entity the keyboard drives.</summary>
    public int PlayerId { get; set; }

    /// <summary>What has happened lately, oldest first.</summary>
    public List<string> Log { get; set; } = new List<string>();
}

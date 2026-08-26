/*
 * Writing a game down and reading it back.
 *
 * Capture and Restore are the pair that matters, and the test that matters is that they compose
 * to the identity: a world, saved and loaded, must draw the frame it drew before. That is the
 * same round-trip argument RenderedFrame.ToText has served since Part 2 - the picture is the
 * thing a player would notice changing, so it is the thing to compare.
 *
 * Usage:
 *
 *     SaveGame.Write(world, "save.json");           // capture and write in one call
 *
 *     if (SaveGame.Exists("save.json"))
 *     {
 *         GameWorld resumed = SaveGame.Read("save.json");
 *     }
 *
 *     SaveGame.Delete("save.json");                 // on death, so the run cannot be replayed
 *
 * Refuses a null argument, a blank path, and a save whose version is not the one this build
 * writes. Reading a file that is not there throws FileNotFoundException rather than returning a
 * fresh game, because silently starting over is the worst possible answer to a missing save.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SadRogue.Primitives;

namespace RogueTutorial;

internal static class SaveGame
{
    // Bumped whenever the shape of SaveData changes. A save from another version is refused.
    //
    // Version 2 added experience and levels. A version 1 save has no record of either, so
    // resuming one would silently reset a character - which is exactly the case this constant
    // was put here for in Part 10.
    private const int CurrentVersion = 3;

    // Indented, because a save you can read in a text editor is a save you can debug.
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        WriteIndented = true,
    };

    /// <summary>True when a save exists at the path. A blank path is simply no save.</summary>
    public static bool Exists(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }

    /// <summary>
    /// Captures a world and writes it to the path, replacing whatever was there. Throws
    /// ArgumentNullException on a null world and ArgumentException on a blank path.
    /// </summary>
    public static void Write(GameWorld world, string path)
    {
        ArgumentNullException.ThrowIfNull(world);
        RejectBlankPath(path);

        File.WriteAllText(path, ToJson(Capture(world)));
    }

    /// <summary>
    /// Reads a save if there is one and it can be read, and returns null otherwise - with
    /// problem describing why, for the log.
    ///
    /// An unreadable save is deleted rather than left, or every start would try and fail on the
    /// same file. Refusing to read it is right; leaving the caller to crash over it is not, and
    /// a player whose save is from an older build would otherwise be unable to start the game
    /// without finding and deleting the file themselves.
    ///
    /// This is separate from Read because it makes a policy decision - throw away what cannot be
    /// read - and policy belongs somewhere a test can reach. Read stays strict for callers that
    /// want the failure.
    /// </summary>
    public static GameWorld? ReadIfReadable(string path, out string? problem)
    {
        problem = null;

        if (!Exists(path))
        {
            return null;
        }

        try
        {
            return Read(path);
        }
        catch (InvalidDataException error)
        {
            problem = error.Message;

            Delete(path);

            return null;
        }
    }

    /// <summary>
    /// Reads a save and rebuilds the world it holds. Throws ArgumentException on a blank path,
    /// FileNotFoundException when there is no save - starting a fresh game instead would silently
    /// discard a run - and InvalidDataException on a save this build cannot read.
    /// </summary>
    public static GameWorld Read(string path)
    {
        RejectBlankPath(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"There is no save at {path}.", path);
        }

        return Restore(FromJson(File.ReadAllText(path)));
    }

    /// <summary>
    /// Removes a save if there is one. Does nothing when there is not, because deleting what is
    /// already gone is the outcome the caller wanted either way.
    /// </summary>
    public static void Delete(string path)
    {
        if (Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Turns a live world into the records that describe it. Throws ArgumentNullException on a
    /// null world.
    /// </summary>
    public static SavedWorld Capture(GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        SavedWorld saved = new SavedWorld
        {
            Version = CurrentVersion,
            Depth = world.Depth,
            Width = world.Map.Width,
            Height = world.Map.Height,
            Log = world.Log.Messages.ToList(),
        };

        // The palette is built as the map is walked: a dungeon uses two kinds of tile across a
        // thousand cells, so listing the kinds once turns the bulk of a save into two characters
        // per cell.
        List<string> paletteKeys = new List<string>();

        for (int row = 0; row < world.Map.Height; row++)
        {
            System.Text.StringBuilder tiles = new System.Text.StringBuilder();
            System.Text.StringBuilder remembered = new System.Text.StringBuilder();

            for (int col = 0; col < world.Map.Width; col++)
            {
                Point cell = new Point(col, row);
                Tile tile = world.Map.GetTile(cell);

                string key = $"{tile.Glyph}{tile.Foreground.PackedValue}{tile.IsWalkable}{tile.IsTransparent}";

                int index = paletteKeys.IndexOf(key);

                if (index < 0)
                {
                    index = paletteKeys.Count;
                    paletteKeys.Add(key);

                    saved.TilePalette.Add(new SavedTile
                    {
                        Glyph = tile.Glyph,
                        Foreground = tile.Foreground.PackedValue,
                        IsWalkable = tile.IsWalkable,
                        IsTransparent = tile.IsTransparent,
                    });
                }

                tiles.Append((char)('a' + index));

                // Only memory is stored. What is visible right now is recomputed on load from
                // where the player is standing, so it can never disagree with the map.
                remembered.Append(
                    world.Visibility.StateAt(cell) != CellVisibility.Unseen ? '#' : '.');
            }

            saved.TileRows.Add(tiles.ToString());
            saved.RememberedRows.Add(remembered.ToString());
        }

        // Ids are assigned here rather than held on Entity, so nothing in the game has to carry
        // a field that exists only for saving.
        Dictionary<Entity, int> ids = new Dictionary<Entity, int>();

        // Everything on the map, then everything carried: a carried item is not in Entities but
        // still has to be written, or the pack comes back empty.
        List<Entity> everything = world.Entities.ToList();

        foreach (Entity carrier in world.Entities)
        {
            if (carrier.Inventory is not null)
            {
                everything.AddRange(carrier.Inventory.Items);
            }
        }

        for (int index = 0; index < everything.Count; index++)
        {
            ids[everything[index]] = index;
        }

        foreach (Entity entity in everything)
        {
            saved.Entities.Add(CaptureEntity(entity, ids));
        }

        saved.PlayerId = ids[world.Player];

        return saved;
    }

    /// <summary>
    /// Rebuilds a world from the records. Throws ArgumentNullException on a null save and
    /// InvalidDataException when the version is not the one this build writes.
    /// </summary>
    public static GameWorld Restore(SavedWorld saved)
    {
        ArgumentNullException.ThrowIfNull(saved);

        // A save from another version is refused rather than half-read. A half-read save is a
        // corrupt game that looks like a working one, which is the worst kind of bug to ship.
        if (saved.Version != CurrentVersion)
        {
            throw new InvalidDataException(
                $"This save is version {saved.Version}; this build reads version {CurrentVersion}.");
        }

        GameMap map = new GameMap(saved.Width, saved.Height);

        // A row of the wrong length would shift the rest of the map by a cell and produce a
        // dungeon that is subtly wrong everywhere rather than obviously wrong once.
        if (saved.TileRows.Count != saved.Height)
        {
            throw new InvalidDataException(
                $"The save holds {saved.TileRows.Count} rows of map for a height of {saved.Height}.");
        }

        for (int row = 0; row < saved.Height; row++)
        {
            string cells = saved.TileRows[row];

            if (cells.Length != saved.Width)
            {
                throw new InvalidDataException(
                    $"Row {row} holds {cells.Length} cells for a width of {saved.Width}.");
            }

            for (int col = 0; col < saved.Width; col++)
            {
                int index = cells[col] - 'a';

                if (index < 0 || index >= saved.TilePalette.Count)
                {
                    throw new InvalidDataException($"Row {row} refers to a tile that is not in the palette.");
                }

                SavedTile tile = saved.TilePalette[index];

                map.SetTile(new Point(col, row), new Tile(
                    tile.Glyph, new Color(tile.Foreground), tile.IsWalkable, tile.IsTransparent));
            }
        }

        // Built before anything references them, so an item can be put into a pack in one pass.
        Dictionary<int, Entity> byId = saved.Entities.ToDictionary(
            entity => entity.Id, RestoreEntity);

        foreach (SavedEntity entity in saved.Entities)
        {
            if (entity.InventoryCapacity is null)
            {
                continue;
            }

            Inventory pack = new Inventory(entity.InventoryCapacity.Value);

            foreach (int carriedId in entity.CarriedIds)
            {
                pack.TryAdd(byId[carriedId]);
            }

            byId[entity.Id].Inventory = pack;
        }

        // Only what was on the map goes back into the entity list; carried things live in packs.
        HashSet<int> carried = new HashSet<int>(saved.Entities.SelectMany(entity => entity.CarriedIds));

        List<Entity> onTheMap = saved.Entities
            .Where(entity => !carried.Contains(entity.Id))
            .Select(entity => byId[entity.Id])
            .ToList();

        // A save that predates depths would restore as floor zero, which no table accepts.
        if (saved.Depth < 1)
        {
            throw new InvalidDataException($"This save is on floor {saved.Depth}; floors count from one.");
        }

        GameWorld world = new GameWorld(map, onTheMap, byId[saved.PlayerId]);

        world.RestoreDepth(saved.Depth);

        world.RestoreMemory(saved.RememberedRows
            .SelectMany(row => row.Select(cell => cell == '#'))
            .ToList());

        foreach (string message in saved.Log)
        {
            world.Log.Add(message);
        }

        return world;
    }

    /// <summary>Serialises a captured world. Throws ArgumentNullException on null.</summary>
    public static string ToJson(SavedWorld saved)
    {
        ArgumentNullException.ThrowIfNull(saved);

        return JsonSerializer.Serialize(saved, Options);
    }

    /// <summary>
    /// Deserialises a captured world. Throws InvalidDataException on text that is not a save,
    /// rather than letting a JsonException escape from a layer the caller does not know about.
    /// </summary>
    public static SavedWorld FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("The save file is empty.");
        }

        try
        {
            return JsonSerializer.Deserialize<SavedWorld>(json)
                ?? throw new InvalidDataException("The save file holds no game.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("The save file is not readable.", error);
        }
    }

    // One entity, with its components and the ids of whatever it carries.
    private static SavedEntity CaptureEntity(Entity entity, Dictionary<Entity, int> ids)
    {
        return new SavedEntity
        {
            Id = ids[entity],
            Name = entity.Name,
            Glyph = entity.Glyph,
            Foreground = entity.Foreground.PackedValue,
            X = entity.Position.X,
            Y = entity.Position.Y,
            BlocksMovement = entity.BlocksMovement,
            Layer = entity.Layer.ToString(),
            Fighter = entity.Fighter is null ? null : new SavedFighter
            {
                MaximumHitPoints = entity.Fighter.MaximumHitPoints,
                HitPoints = entity.Fighter.HitPoints,
                Attack = entity.Fighter.Attack,
                Defence = entity.Fighter.Defence,
                ExperienceAwarded = entity.Fighter.ExperienceAwarded,
            },
            Consumable = entity.Consumable is null ? null : new SavedConsumable
            {
                Kind = entity.Consumable.Kind.ToString(),
                Power = entity.Consumable.Power,
                Radius = entity.Consumable.Radius,
            },
            InventoryCapacity = entity.Inventory?.Capacity,
            CarriedIds = entity.Inventory is null
                ? new List<int>()
                : entity.Inventory.Items.Select(item => ids[item]).ToList(),
            Level = entity.Level is null ? null : new SavedLevel
            {
                CurrentLevel = entity.Level.CurrentLevel,
                Experience = entity.Level.Experience,
            },
        };
    }

    // One entity, without its pack: packs are filled once every entity exists.
    private static Entity RestoreEntity(SavedEntity saved)
    {
        Entity entity = new Entity(
            saved.Name,
            saved.Glyph,
            new Color(saved.Foreground),
            new Point(saved.X, saved.Y),
            saved.BlocksMovement,
            Enum.Parse<RenderLayer>(saved.Layer));

        if (saved.Fighter is not null)
        {
            Fighter fighter = new Fighter(
                saved.Fighter.MaximumHitPoints,
                saved.Fighter.Attack,
                saved.Fighter.Defence,
                saved.Fighter.ExperienceAwarded);

            // Constructed at full health, so the difference is applied as damage rather than by
            // reaching past the class and setting the field.
            fighter.TakeDamage(saved.Fighter.MaximumHitPoints - saved.Fighter.HitPoints);

            entity.Fighter = fighter;
        }

        if (saved.Level is not null)
        {
            Level level = new Level();

            // Rebuilt by replaying rather than by reaching past the class: awarding the total
            // and advancing the levels leaves it in a state the class could have reached itself.
            for (int gained = 1; gained < saved.Level.CurrentLevel; gained++)
            {
                level.Award(level.ExperienceForNextLevel);
                level.Advance();
            }

            level.Award(saved.Level.Experience);

            entity.Level = level;
        }

        if (saved.Consumable is not null)
        {
            entity.Consumable = new Consumable(
                Enum.Parse<ConsumableKind>(saved.Consumable.Kind),
                saved.Consumable.Power,
                saved.Consumable.Radius);
        }

        return entity;
    }

    // A path is the caller's, and a blank one is a mistake rather than a default.
    private static void RejectBlankPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A save path cannot be blank.", nameof(path));
        }
    }
}

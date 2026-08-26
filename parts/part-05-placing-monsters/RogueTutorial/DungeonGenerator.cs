/*
 * Builds a dungeon: solid rock, with rooms carved out of it and corridors joining them.
 *
 * The random number generator is passed in and never created here. That is the whole reason
 * this class is testable: the same seed always produces the same dungeon, so a failure can be
 * reproduced, and a test can assert an entire generated map as an ASCII picture.
 *
 * Usage:
 *
 *     DungeonSettings settings = new DungeonSettings(30, 6, 10);
 *     Random random = new Random(12345);                  // any seed; the same one repeats the dungeon
 *     DungeonGenerator generator = new DungeonGenerator(settings);
 *
 *     GeneratedDungeon dungeon = generator.Generate(80, 43, random);
 *     GameMap map = dungeon.Map;                          // rooms and corridors carved into rock
 *     Point spawn = dungeon.PlayerStart;                  // the centre of the first room
 *
 * Refuses a null settings object or a null Random, and a map too small to hold one room.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class DungeonGenerator
{
    // The numbers shaping every dungeon this generator makes.
    private readonly DungeonSettings _settings;

    /// <summary>
    /// Records the settings to generate with. Throws ArgumentNullException on a null settings
    /// object, since there is no sensible default set of room sizes.
    /// </summary>
    public DungeonGenerator(DungeonSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
    }

    /// <summary>
    /// Generates a dungeon of the given size, drawing every random choice from the supplied
    /// Random. Rooms that would touch or overlap an existing room are discarded, so the result
    /// usually holds fewer rooms than the settings ask for. Throws ArgumentNullException on a
    /// null Random, and ArgumentOutOfRangeException when the map cannot fit one smallest room.
    /// </summary>
    public GeneratedDungeon Generate(int width, int height, Random random)
    {
        // The Random is the caller's, so the same seed reproduces the same dungeon exactly.
        ArgumentNullException.ThrowIfNull(random);

        // A map that cannot hold the smallest allowed room can never produce a dungeon, and
        // discovering that after twenty failed attempts would report the wrong problem.
        if (width < _settings.MinimumRoomSize || height < _settings.MinimumRoomSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                $"{width}x{height}",
                $"The map is too small to hold a {_settings.MinimumRoomSize}-cell room.");
        }

        GameMap map = new GameMap(width, height);

        // Everything starts as solid rock; rooms and corridors are then carved out of it.
        map.Fill(TileTypes.Wall);

        List<RectangularRoom> placedRooms = new List<RectangularRoom>();

        for (int attempt = 0; attempt < _settings.MaximumRooms; attempt++)
        {
            RectangularRoom candidate = RandomRoom(width, height, random);

            // Discarded rather than retried: retrying until the count is met can take unbounded
            // time on a crowded map, so a dungeon simply ends up with fewer rooms.
            if (placedRooms.Any(existing => existing.Intersects(candidate)))
            {
                continue;
            }

            // Placement arithmetic is the easiest thing here to get wrong by one, and a room
            // hanging off the map would throw much later inside SetTile.
            Debug.Assert(
                candidate.Left >= 0 && candidate.Top >= 0
                    && candidate.Right < width && candidate.Bottom < height,
                "A generated room must lie entirely on the map.");

            Carve(map, candidate);

            // Every room after the first is joined to the one before it, which is what makes
            // the whole dungeon reachable rather than a set of sealed boxes.
            if (placedRooms.Count > 0)
            {
                DigCorridor(map, placedRooms[placedRooms.Count - 1].Center, candidate.Center, random);
            }

            placedRooms.Add(candidate);
        }

        // The border stays rock because every room carries its own wall ring; if that ever
        // stops being true the player can walk off the map and nothing else would report it.
        Debug.Assert(BorderIsUncarved(map), "Generation must never carve the edge of the map.");

        return new GeneratedDungeon(map, placedRooms);
    }

    // True when no cell of the map's outermost ring is walkable.
    private static bool BorderIsUncarved(GameMap map)
    {
        for (int col = 0; col < map.Width; col++)
        {
            if (map.IsWalkable(new Point(col, 0)) || map.IsWalkable(new Point(col, map.Height - 1)))
            {
                return false;
            }
        }

        for (int row = 0; row < map.Height; row++)
        {
            if (map.IsWalkable(new Point(0, row)) || map.IsWalkable(new Point(map.Width - 1, row)))
            {
                return false;
            }
        }

        return true;
    }

    // Picks a room of a random allowed size at a random position that fits on the map.
    private RectangularRoom RandomRoom(int mapWidth, int mapHeight, Random random)
    {
        // Next's upper bound is exclusive, so + 1 makes MaximumRoomSize reachable.
        int roomWidth = random.Next(_settings.MinimumRoomSize, _settings.MaximumRoomSize + 1);
        int roomHeight = random.Next(_settings.MinimumRoomSize, _settings.MaximumRoomSize + 1);

        // A room larger than the map in one dimension is clamped rather than rejected, so a
        // narrow map still generates instead of discarding every attempt.
        roomWidth = Math.Min(roomWidth, mapWidth);
        roomHeight = Math.Min(roomHeight, mapHeight);

        // The room's own wall ring keeps the map border uncarved, so a room may sit flush
        // against the edge; the largest legal left is the one that puts its right wall last.
        int left = random.Next(0, mapWidth - roomWidth + 1);
        int top = random.Next(0, mapHeight - roomHeight + 1);

        return new RectangularRoom(left, top, roomWidth, roomHeight);
    }

    // Sets a room's interior to floor, leaving its wall ring as rock.
    private static void Carve(GameMap map, RectangularRoom room)
    {
        foreach (Point cell in room.InnerCells)
        {
            map.SetTile(cell, TileTypes.Floor);
        }
    }

    // Cuts an L-shaped corridor between two room centres, bending whichever way the roll says.
    private static void DigCorridor(GameMap map, Point from, Point to, Random random)
    {
        // Alternating the bend keeps a dungeon from having every corner the same shape.
        bool horizontalFirst = random.Next(2) == 0;

        foreach (Point cell in Corridor.Between(from, to, horizontalFirst))
        {
            map.SetTile(cell, TileTypes.Floor);
        }
    }
}

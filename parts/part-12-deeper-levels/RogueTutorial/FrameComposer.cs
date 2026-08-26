/*
 * Builds the picture that should be on screen: the map first, then entities over the top.
 *
 * Where two entities share a cell only one glyph fits, and RenderLayer decides which - not the
 * order they happen to sit in the entity list.
 *
 * Usage:
 *
 *     GameMap map = new GameMap(3, 2);
 *     Entity player = new Entity("Player", '@', Color.White, new Point(1, 1), blocksMovement: true,
 *         RenderLayer.Player);
 *     RenderedFrame frame = FrameComposer.Compose(map, new[] { player });
 *     string picture = frame.ToText();
 *     // -> "...\n.@."
 *
 * The overload taking a VisibilityMap is what the game uses from Part 4 on: it dims what the
 * player remembers, blanks what they have never seen, and hides entities standing in the dark.
 *
 * Refuses a null map or null entity list. An entity standing off the map is skipped rather than
 * throwing, because a later part moves entities between levels.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using SadRogue.Primitives;

namespace RogueTutorial;

internal static class FrameComposer
{
    /// <summary>
    /// Draws every map tile, then every entity over the top, lowest RenderLayer first, so a
    /// monster covers the item it stands on however the list happens to be ordered. Throws
    /// ArgumentNullException on a null argument.
    /// </summary>
    /// <summary>
    /// Draws the map and entities as the player currently perceives them: cells in sight at full
    /// colour, cells only remembered dimmed, cells never seen left blank, and entities drawn only
    /// where the player can actually see them. Throws ArgumentNullException on a null argument.
    /// </summary>
    public static RenderedFrame Compose(GameMap map, IReadOnlyList<Entity> entities, VisibilityMap visibility)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(visibility);

        char[] glyphs = new char[map.Width * map.Height];
        Color[] foregrounds = new Color[map.Width * map.Height];

        for (int row = 0; row < map.Height; row++)
        {
            for (int col = 0; col < map.Width; col++)
            {
                Point cell = new Point(col, row);
                int index = (row * map.Width) + col;

                CellVisibility state = visibility.StateAt(cell);

                // Never seen: nothing is drawn, so unexplored dungeon reads as empty space.
                if (state == CellVisibility.Unseen)
                {
                    glyphs[index] = ' ';
                    foregrounds[index] = Color.Black;
                    continue;
                }

                Tile tile = map.GetTile(cell);

                glyphs[index] = tile.Glyph;

                // Remembered cells are drawn from memory, so they are dimmed rather than lit.
                foregrounds[index] = state == CellVisibility.Visible
                    ? tile.Foreground
                    : DimmedForMemory(tile.Foreground);
            }
        }

        foreach (Entity entity in LowestLayerFirst(entities))
        {
            if (!map.IsInBounds(entity.Position))
            {
                continue;
            }

            // Creatures are not remembered: an entity is drawn only where it can be seen now,
            // otherwise the player would watch a monster that had long since walked away.
            if (visibility.StateAt(entity.Position) != CellVisibility.Visible)
            {
                continue;
            }

            int index = (entity.Position.Y * map.Width) + entity.Position.X;
            glyphs[index] = entity.Glyph;
            foregrounds[index] = entity.Foreground;
        }

        return new RenderedFrame(map.Width, map.Height, glyphs, foregrounds);
    }

    // A third of full brightness: dark enough to read as memory, light enough to make out.
    private static Color DimmedForMemory(Color lit)
    {
        return new Color(lit.R / 3, lit.G / 3, lit.B / 3);
    }

    // Lowest layer first, so the highest is written last and is the one left visible. OrderBy is
    // stable, so two entities on the same layer keep their list order and the picture does not
    // change from one frame to the next.
    private static IEnumerable<Entity> LowestLayerFirst(IReadOnlyList<Entity> entities)
    {
        return entities.OrderBy(entity => entity.Layer);
    }

    public static RenderedFrame Compose(GameMap map, IReadOnlyList<Entity> entities)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(entities);

        char[] glyphs = new char[map.Width * map.Height];
        Color[] foregrounds = new Color[map.Width * map.Height];

        // The map is the background layer, so it goes down first and entities paint over it.
        for (int row = 0; row < map.Height; row++)
        {
            for (int col = 0; col < map.Width; col++)
            {
                Tile tile = map.GetTile(new Point(col, row));

                int index = (row * map.Width) + col;
                glyphs[index] = tile.Glyph;
                foregrounds[index] = tile.Foreground;
            }
        }

        foreach (Entity entity in LowestLayerFirst(entities))
        {
            // An entity between levels is legitimately off this map, so skip rather than throw.
            if (!map.IsInBounds(entity.Position))
            {
                continue;
            }

            int index = (entity.Position.Y * map.Width) + entity.Position.X;
            glyphs[index] = entity.Glyph;
            foregrounds[index] = entity.Foreground;
        }

        return new RenderedFrame(map.Width, map.Height, glyphs, foregrounds);
    }
}

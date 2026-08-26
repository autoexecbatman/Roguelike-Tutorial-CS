/*
 * Builds the picture that should be on screen: the map first, then entities over the top.
 *
 * Usage:
 *
 *     GameMap map = new GameMap(3, 2);
 *     Entity player = new Entity("Player", '@', Color.White, new Point(1, 1));
 *     RenderedFrame frame = FrameComposer.Compose(map, new[] { player });
 *     string picture = frame.ToText();
 *     // -> "...\n.@."
 *
 * Refuses a null map or null entity list. An entity standing off the map is skipped rather than
 * throwing, because a later part moves entities between levels.
 */

using System;
using System.Collections.Generic;
using SadRogue.Primitives;

namespace RogueTutorial;

internal static class FrameComposer
{
    /// <summary>
    /// Draws every map tile, then every entity over the top in list order, so a later entity
    /// covers an earlier one sharing its cell. Throws ArgumentNullException on a null argument.
    /// </summary>
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

        // List order decides who covers whom, so this loop must not be reordered.
        foreach (Entity entity in entities)
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

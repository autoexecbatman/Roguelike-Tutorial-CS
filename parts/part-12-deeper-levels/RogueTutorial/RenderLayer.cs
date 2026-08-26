/*
 * Which things are drawn on top of which, when two of them share a cell.
 *
 * Only one glyph fits in a cell, so something has to lose. Before this the winner was whichever
 * entity happened to be later in the entity list, which meant items covered monsters: generation
 * fills each room with monsters and then with items, so the item was always added second.
 *
 * The order is by how much the player needs to know the thing is there. A monster about to be
 * walked into outranks the potion it is standing on, and the player outranks everything.
 *
 * Usage:
 *
 *     Entity rat = new Entity("Rat", 'r', Color.Red, at, blocksMovement: true, RenderLayer.Actor);
 *
 *     // FrameComposer sorts by this before drawing, so list order does not matter.
 *
 * The values are ordered lowest-drawn-first, and FrameComposer relies on that ordering rather
 * than on a table, so adding a layer means putting it in the right place in this list.
 */

namespace RogueTutorial;

/// <summary>Draw order for entities sharing a cell, lowest first.</summary>
internal enum RenderLayer
{
    /// <summary>What is left of something that died. Lies under everything.</summary>
    Corpse,

    /// <summary>Something on the floor waiting to be picked up.</summary>
    Item,

    /// <summary>Anything that takes a turn.</summary>
    Actor,

    /// <summary>The player, who is never hidden by anything.</summary>
    Player,
}

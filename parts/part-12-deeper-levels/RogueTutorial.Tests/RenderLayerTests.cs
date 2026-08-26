/*
 * Unit tests for draw order.
 *
 * Two things can stand on one cell and only one glyph fits. Which one wins is a rule - a
 * monster is more urgent than the potion it is standing on - and before this part it was an
 * accident of the order entities happened to be added to the list.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~RenderLayerTests
 */

using System.Collections.Generic;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class RenderLayerTests
{
    private static Entity ItemAt(Point where)
    {
        return new Entity("potion", '!', Color.Magenta, where, blocksMovement: false, RenderLayer.Item);
    }

    private static Entity MonsterAt(Point where)
    {
        Entity monster = new Entity("Rat", 'r', Color.Red, where, blocksMovement: true, RenderLayer.Actor);
        monster.Fighter = new Fighter(4, 3, 0, experienceAwarded: 10);
        return monster;
    }

    [Fact]
    public void AMonsterIsDrawnOverAnItemItStandsOn()
    {
        // A player who cannot see the rat walks into it. The potion can wait.
        GameMap map = new GameMap(3, 1);
        Point shared = new Point(1, 0);

        // Item added last, which is what dungeon generation does: monsters then items, per room.
        List<Entity> entities = new List<Entity> { MonsterAt(shared), ItemAt(shared) };

        RenderedFrame frame = FrameComposer.Compose(map, entities);

        Assert.Equal(".r.", frame.ToText());
    }

    [Fact]
    public void OrderInTheListDoesNotDecideWhatIsSeen()
    {
        // The same two entities the other way round must compose to the same picture, or draw
        // order is being decided by when something was spawned.
        GameMap map = new GameMap(3, 1);
        Point shared = new Point(1, 0);

        RenderedFrame itemFirst = FrameComposer.Compose(
            map, new List<Entity> { ItemAt(shared), MonsterAt(shared) });

        RenderedFrame monsterFirst = FrameComposer.Compose(
            map, new List<Entity> { MonsterAt(shared), ItemAt(shared) });

        Assert.Equal(itemFirst.ToText(), monsterFirst.ToText());
    }
}

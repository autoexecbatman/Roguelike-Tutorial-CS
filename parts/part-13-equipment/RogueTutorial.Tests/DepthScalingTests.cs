/*
 * Unit tests for what each floor is allowed to contain.
 *
 * The rule: a kind appears from its MinimumDepth downward and never disappears again. That is
 * what makes descending mean something and what stops floor one from killing a new player.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~DepthScalingTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class DepthScalingTests
{
    // A room big enough to place things in, on a map that is all floor.
    private static (RectangularRoom Room, GameMap Map) OpenRoom()
    {
        GameMap map = new GameMap(20, 20);
        map.Fill(TileTypes.Floor);

        return (new RectangularRoom(1, 1, 16, 16), map);
    }

    // Every name the table places over many rooms, so a rare kind is not missed by chance.
    private static HashSet<string> NamesPlacedAt(int depth)
    {
        (RectangularRoom room, GameMap map) = OpenRoom();

        HashSet<string> names = new HashSet<string>();

        for (int attempt = 0; attempt < 400; attempt++)
        {
            foreach (Entity placed in MonsterTable.Standard.PopulateRoom(room, map, new Random(attempt), depth))
            {
                names.Add(placed.Name);
            }
        }

        return names;
    }

    [Fact]
    public void TheFirstFloorHoldsOnlyTheShallowKinds()
    {
        // A new player meeting an ogre on floor one is the failure this table prevents.
        HashSet<string> names = NamesPlacedAt(depth: 1);

        Assert.Contains("Rat", names);
        Assert.DoesNotContain("Goblin", names);
        Assert.DoesNotContain("Ogre", names);
    }

    [Fact]
    public void DeeperFloorsAddKinds()
    {
        Assert.Contains("Goblin", NamesPlacedAt(depth: 3));
        Assert.Contains("Ogre", NamesPlacedAt(depth: 5));
    }

    [Fact]
    public void AKindNeverStopsAppearing()
    {
        // A floor of nothing but ogres would be a different game. The shallow kinds stay, which
        // is what keeps a deep floor varied rather than uniformly lethal.
        Assert.Contains("Rat", NamesPlacedAt(depth: 8));
    }

    [Fact]
    public void TheShallowKindsAreStillTheCommonOnes()
    {
        // Weights are relative within a floor, so adding kinds must not invert the mix.
        (RectangularRoom room, GameMap map) = OpenRoom();

        int rats = 0;
        int ogres = 0;

        for (int attempt = 0; attempt < 400; attempt++)
        {
            foreach (Entity placed in MonsterTable.Standard.PopulateRoom(room, map, new Random(attempt), depth: 5))
            {
                if (placed.Name == "Rat") { rats++; }
                if (placed.Name == "Ogre") { ogres++; }
            }
        }

        Assert.True(rats > ogres, $"{rats} rats against {ogres} ogres");
    }

    [Fact]
    public void ItemsScaleTheSameWay()
    {
        (RectangularRoom room, GameMap map) = OpenRoom();

        HashSet<string> shallow = new HashSet<string>();
        HashSet<string> deep = new HashSet<string>();

        for (int attempt = 0; attempt < 400; attempt++)
        {
            foreach (Entity placed in ItemTable.Standard.PopulateRoom(room, map, new Random(attempt), depth: 1))
            {
                shallow.Add(placed.Name);
            }

            foreach (Entity placed in ItemTable.Standard.PopulateRoom(room, map, new Random(attempt), depth: 4))
            {
                deep.Add(placed.Name);
            }
        }

        Assert.DoesNotContain("greater healing potion", shallow);
        Assert.Contains("greater healing potion", deep);
    }

    [Fact]
    public void FloorZeroIsRefusedByBothTables()
    {
        (RectangularRoom room, GameMap map) = OpenRoom();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => MonsterTable.Standard.PopulateRoom(room, map, new Random(1), depth: 0));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ItemTable.Standard.PopulateRoom(room, map, new Random(1), depth: 0));
    }

    [Fact]
    public void AKindThatStartsAboveFloorOneIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MonsterKind(
            "Wraith", 'w', Color.White, weight: 1,
            maximumHitPoints: 5, attack: 1, defence: 0, experienceAwarded: 1, minimumDepth: 0));
    }
}

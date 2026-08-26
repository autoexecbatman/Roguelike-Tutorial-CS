/*
 * Unit tests for what a monster does with its turn, and for the turn cycle as a whole.
 *
 * Worlds here are hand-built rather than generated, so a monster is put exactly where the test
 * needs it and the outcome is not at the mercy of a seed.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~MonsterTurnTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class MonsterTurnTests
{
    // An open room with the player at (4,4) and whatever monsters the test supplies.
    private static GameWorld WorldWith(Point playerAt, params Entity[] monsters)
    {
        GameMap map = new GameMap(11, 11);
        map.Fill(TileTypes.Wall);

        for (int row = 1; row < 10; row++)
        {
            for (int col = 1; col < 10; col++)
            {
                map.SetTile(new Point(col, row), TileTypes.Floor);
            }
        }

        Entity player = new Entity("Player", '@', Color.White, playerAt, blocksMovement: true);
        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2);

        List<Entity> entities = new List<Entity>(monsters) { player };

        return new GameWorld(map, entities, player);
    }

    private static Entity Monster(string name, Point at, int hitPoints, int attack, int defence)
    {
        Entity monster = new Entity(name, name[0], Color.Red, at, blocksMovement: true);
        monster.Fighter = new Fighter(hitPoints, attack, defence);
        return monster;
    }

    [Fact]
    public void AMonsterStepsTowardThePlayer()
    {
        Entity rat = Monster("Rat", new Point(8, 4), 4, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        MonsterTurn.Act(rat, world);

        Assert.Equal(new Point(7, 4), rat.Position);
    }

    [Fact]
    public void AMonsterStepsDiagonallyWhenThatIsTheDirection()
    {
        Entity rat = Monster("Rat", new Point(7, 7), 4, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        MonsterTurn.Act(rat, world);

        Assert.Equal(new Point(6, 6), rat.Position);
    }

    [Fact]
    public void AnAdjacentMonsterAttacksInsteadOfMoving()
    {
        Entity rat = Monster("Rat", new Point(5, 4), 4, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        string? message = MonsterTurn.Act(rat, world);

        Assert.Equal(new Point(5, 4), rat.Position);
        Assert.NotNull(message);
        Assert.Contains("Rat hits Player", message);

        // 3 attack less 2 defence.
        Assert.Equal(29, world.Player.Fighter!.HitPoints);
    }

    [Fact]
    public void ADiagonallyAdjacentMonsterAlsoAttacks()
    {
        // Movement is eight-way, so adjacency must be Chebyshev rather than Manhattan - a
        // diagonal neighbour that stepped instead of attacking would walk into the player.
        Entity rat = Monster("Rat", new Point(5, 5), 4, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        string? message = MonsterTurn.Act(rat, world);

        Assert.NotNull(message);
        Assert.Equal(new Point(5, 5), rat.Position);
    }

    [Fact]
    public void AMonsterThatCannotSeeThePlayerDoesNothing()
    {
        // A wall between them, so the monster is outside the player's field of view - and by
        // symmetry, cannot see the player either.
        GameMap map = new GameMap(11, 5);
        map.Fill(TileTypes.Wall);

        for (int col = 1; col < 10; col++)
        {
            map.SetTile(new Point(col, 2), TileTypes.Floor);
        }

        map.SetTile(new Point(5, 2), TileTypes.Wall);

        Entity player = new Entity("Player", '@', Color.White, new Point(2, 2), blocksMovement: true);
        player.Fighter = new Fighter(30, 5, 2);

        Entity rat = Monster("Rat", new Point(8, 2), 4, 3, 0);

        GameWorld world = new GameWorld(map, new List<Entity> { rat, player }, player);

        string? message = MonsterTurn.Act(rat, world);

        Assert.Null(message);
        Assert.Equal(new Point(8, 2), rat.Position);
    }

    [Fact]
    public void AMonsterOnARememberedCellDoesNotAct()
    {
        // Stronger than the test above, which uses a cell the player has never seen - there,
        // "not visible" and "never seen" are the same thing. Here the player has seen the cell
        // and walked away, so it is remembered rather than unseen. A monster acting on memory
        // would chase the player through corridors they can no longer see into.
        GameMap map = new GameMap(15, 5);
        map.Fill(TileTypes.Wall);

        for (int col = 1; col < 14; col++)
        {
            map.SetTile(new Point(col, 2), TileTypes.Floor);
        }

        Entity player = new Entity("Player", '@', Color.White, new Point(10, 2), blocksMovement: true);
        player.Fighter = new Fighter(30, 5, 2);

        Entity rat = Monster("Rat", new Point(6, 2), 4, 3, 0);

        GameWorld world = new GameWorld(map, new List<Entity> { rat, player }, player);

        // The rat starts inside the player's sight radius of 8.
        Assert.Equal(CellVisibility.Visible, world.Visibility.StateAt(rat.Position));

        // Walk away until the rat's cell is remembered rather than seen. MovePlayer runs the
        // monsters too, so the rat follows; what matters is the state of the cell it ends on.
        for (int step = 0; step < 3; step++)
        {
            world.MovePlayer(new Point(1, 0));
        }

        Point ratCell = new Point(2, 2);
        rat.MoveTo(ratCell);

        Assert.Equal(CellVisibility.Remembered, world.Visibility.StateAt(ratCell));

        string? message = MonsterTurn.Act(rat, world);

        Assert.Null(message);
        Assert.Equal(ratCell, rat.Position);
    }

    [Fact]
    public void AMonsterWillNotWalkThroughAnother()
    {
        // No pathfinding yet, so a monster behind another simply waits.
        Entity front = Monster("Front", new Point(6, 4), 4, 3, 0);
        Entity behind = Monster("Behind", new Point(7, 4), 4, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), front, behind);

        MonsterTurn.Act(behind, world);

        Assert.Equal(new Point(7, 4), behind.Position);
    }

    [Fact]
    public void AMonsterWillNotWalkIntoAWall()
    {
        // Player directly above the wall row, monster below it: the step is refused.
        GameMap map = new GameMap(7, 7);
        map.Fill(TileTypes.Floor);
        map.SetTile(new Point(3, 3), TileTypes.Wall);

        Entity player = new Entity("Player", '@', Color.White, new Point(3, 2), blocksMovement: true);
        player.Fighter = new Fighter(30, 5, 2);

        Entity rat = Monster("Rat", new Point(3, 4), 4, 3, 0);

        GameWorld world = new GameWorld(map, new List<Entity> { rat, player }, player);

        MonsterTurn.Act(rat, world);

        Assert.Equal(new Point(3, 4), rat.Position);
    }

    [Fact]
    public void ACorpseCannotTakeATurn()
    {
        Entity rat = Monster("Rat", new Point(5, 4), 4, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        rat.Die();

        Assert.Throws<ArgumentException>(() => MonsterTurn.Act(rat, world));
    }

    [Fact]
    public void MonstersActAfterThePlayerMoves()
    {
        // The turn cycle: one player action, then every monster gets one turn.
        Entity rat = Monster("Rat", new Point(6, 4), 4, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        world.MovePlayer(new Point(0, 1));

        // The rat closed the distance during the player's turn.
        Assert.NotEqual(new Point(6, 4), rat.Position);
    }

    [Fact]
    public void AttackingAlsoSpendsTheTurn()
    {
        Entity rat = Monster("Rat", new Point(5, 4), 20, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        world.MovePlayer(new Point(1, 0));

        // The player hit it and it hit back, so both have taken damage.
        Assert.True(rat.Fighter!.HitPoints < 20);
        Assert.True(world.Player.Fighter!.HitPoints < 30);
    }

    [Fact]
    public void ADeadMonsterStopsActing()
    {
        Entity rat = Monster("Rat", new Point(5, 4), 1, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        int healthBefore = world.Player.Fighter!.HitPoints;

        // One blow kills it, so it must not get a turn afterwards.
        world.MovePlayer(new Point(1, 0));

        Assert.Null(rat.Fighter);
        Assert.Equal(healthBefore, world.Player.Fighter!.HitPoints);
    }

    [Fact]
    public void TheGameNoticesWhenThePlayerDies()
    {
        Entity brute = Monster("Brute", new Point(5, 4), 50, 99, 0);
        GameWorld world = WorldWith(new Point(4, 4), brute);

        world.MovePlayer(new Point(0, 1));

        Assert.True(world.IsPlayerDead);
        Assert.Contains("You die.", world.Log.Messages);
    }

    [Fact]
    public void ADeadPlayerTakesNoFurtherTurns()
    {
        Entity brute = Monster("Brute", new Point(5, 4), 50, 99, 0);
        GameWorld world = WorldWith(new Point(4, 4), brute);

        world.MovePlayer(new Point(0, 1));

        Point restingPlace = world.Player.Position;

        PlayerAction action = world.MovePlayer(new Point(1, 0));

        Assert.Equal(PlayerActionKind.None, action.Kind);
        Assert.Equal(restingPlace, world.Player.Position);
    }

    [Fact]
    public void CombatIsWrittenToTheLog()
    {
        Entity rat = Monster("Rat", new Point(5, 4), 20, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        world.MovePlayer(new Point(1, 0));

        Assert.Contains(world.Log.Messages, message => message.Contains("Player hits Rat"));
        Assert.Contains(world.Log.Messages, message => message.Contains("Rat hits Player"));
    }

    [Fact]
    public void ANullArgumentIsRejected()
    {
        Entity rat = Monster("Rat", new Point(5, 4), 4, 3, 0);
        GameWorld world = WorldWith(new Point(4, 4), rat);

        Assert.Throws<ArgumentNullException>(() => MonsterTurn.Act(null!, world));
        Assert.Throws<ArgumentNullException>(() => MonsterTurn.Act(rat, null!));
    }
}

/*
 * Unit tests for aiming and for the two scrolls that need it.
 *
 * The property worth watching is where cancelling goes. Reading a scroll opens targeting from
 * the pack, so backing out must return to the pack - a mode that forgets where it came from
 * leaves the player looking at the dungeon holding a scroll they thought they had put away.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~TargetingTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadConsole.Input;
using SadRogue.Primitives;
using Xunit;

public sealed class TargetingTests
{
    // An open room with the player in the middle and whatever monsters the test needs.
    private static GameWorld WorldWith(params Entity[] monsters)
    {
        GameMap map = new GameMap(15, 15);
        map.Fill(TileTypes.Floor);

        Entity player = new Entity("Player", '@', Color.White, new Point(7, 7), blocksMovement: true);
        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2, experienceAwarded: 0);
        player.Inventory = new Inventory(capacity: 26);

        List<Entity> entities = new List<Entity>(monsters) { player };

        return new GameWorld(map, entities, player);
    }

    private static Entity Monster(string name, Point at, int hitPoints)
    {
        Entity monster = new Entity(name, name[0], Color.Red, at, blocksMovement: true);
        monster.Fighter = new Fighter(hitPoints, attack: 3, defence: 0, experienceAwarded: 0);
        return monster;
    }

    private static Entity Scroll(ConsumableKind kind, int power, int radius)
    {
        Entity scroll = new Entity($"{kind} scroll", '?', Color.Yellow, new Point(0, 0), blocksMovement: false);
        scroll.Consumable = new Consumable(kind, power, radius);
        return scroll;
    }

    [Fact]
    public void ReadingAScrollBeginsAimingRatherThanUsingIt()
    {
        GameWorld world = WorldWith(Monster("Rat", new Point(9, 7), 10));
        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Lightning, 12, 0));

        bool spentATurn = world.UseItem(0);

        Assert.False(spentATurn);
        Assert.Equal(GameMode.Targeting, world.Mode);
        Assert.NotNull(world.Aiming);
    }

    [Fact]
    public void TheScrollStaysInThePackWhileAiming()
    {
        // Nothing has been used yet, so cancelling must be able to lose nothing.
        GameWorld world = WorldWith(Monster("Rat", new Point(9, 7), 10));
        Entity scroll = Scroll(ConsumableKind.Lightning, 12, 0);
        world.Player.Inventory!.TryAdd(scroll);

        world.UseItem(0);

        Assert.Contains(scroll, world.Player.Inventory.Items);
    }

    [Fact]
    public void TheCursorStartsOnTheNearestVisibleCreature()
    {
        // Aiming almost always means aiming at something, and starting on empty floor makes the
        // common case slower for no reason.
        GameWorld world = WorldWith(
            Monster("Far", new Point(12, 7), 10),
            Monster("Near", new Point(9, 7), 10));

        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Lightning, 12, 0));
        world.UseItem(0);

        Assert.Equal(new Point(9, 7), world.Aiming!.Cursor);
    }

    [Fact]
    public void TheCursorStartsOnThePlayerWhenNothingIsVisible()
    {
        GameWorld world = WorldWith();

        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Lightning, 12, 0));
        world.UseItem(0);

        Assert.Equal(world.Player.Position, world.Aiming!.Cursor);
    }

    [Fact]
    public void CancellingReturnsToThePackRatherThanTheMap()
    {
        // The whole reason this mode has to remember where it came from.
        GameWorld world = WorldWith(Monster("Rat", new Point(9, 7), 10));
        Entity scroll = Scroll(ConsumableKind.Lightning, 12, 0);
        world.Player.Inventory!.TryAdd(scroll);

        world.UseItem(0);
        world.CancelTarget();

        Assert.Equal(GameMode.ShowingInventory, world.Mode);
        Assert.Null(world.Aiming);
        Assert.Contains(scroll, world.Player.Inventory.Items);
    }

    [Fact]
    public void CancellingCostsNoTurn()
    {
        // Looking is not acting: a monster must not get a free swing because the player changed
        // their mind about a scroll.
        GameWorld world = WorldWith(Monster("Rat", new Point(8, 7), 10));
        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Lightning, 12, 0));

        int healthBefore = world.Player.Fighter!.HitPoints;

        world.UseItem(0);
        world.CancelTarget();

        Assert.Equal(healthBefore, world.Player.Fighter.HitPoints);
    }

    [Fact]
    public void TheCursorMovesButNotOffTheMap()
    {
        GameWorld world = WorldWith();
        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Lightning, 12, 0));
        world.UseItem(0);

        world.MoveCursor(new Point(1, 0));
        Assert.Equal(new Point(8, 7), world.Aiming!.Cursor);

        // Far enough left to run off the edge, which must simply stop.
        for (int step = 0; step < 20; step++)
        {
            world.MoveCursor(new Point(-1, 0));
        }

        Assert.Equal(0, world.Aiming.Cursor.X);
    }

    [Fact]
    public void LightningHitsWhatTheCursorIsOn()
    {
        Entity rat = Monster("Rat", new Point(9, 7), 20);
        GameWorld world = WorldWith(rat);

        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Lightning, 12, 0));
        world.UseItem(0);

        Assert.True(world.ConfirmTarget());

        Assert.Equal(8, rat.Fighter!.HitPoints);
        Assert.Equal(GameMode.Playing, world.Mode);
    }

    [Fact]
    public void AFiredScrollLeavesThePack()
    {
        Entity rat = Monster("Rat", new Point(9, 7), 20);
        GameWorld world = WorldWith(rat);

        Entity scroll = Scroll(ConsumableKind.Lightning, 12, 0);
        world.Player.Inventory!.TryAdd(scroll);
        world.UseItem(0);
        world.ConfirmTarget();

        Assert.Empty(world.Player.Inventory.Items);
    }

    [Fact]
    public void AMissCostsTheTurnRatherThanTheScroll()
    {
        // Aiming at empty floor is a mistake the player is allowed to make, and destroying the
        // scroll for it would be a punishment out of proportion.
        GameWorld world = WorldWith();

        Entity scroll = Scroll(ConsumableKind.Lightning, 12, 0);
        world.Player.Inventory!.TryAdd(scroll);
        world.UseItem(0);

        // Cursor is on the player, and the player is not a valid lightning target here because
        // BlockingEntityAt finds them - so move it onto empty floor first.
        world.MoveCursor(new Point(3, 3));

        Assert.False(world.ConfirmTarget());

        Assert.Contains(scroll, world.Player.Inventory.Items);
        Assert.Equal(GameMode.ShowingInventory, world.Mode);
    }

    [Fact]
    public void AFireballBurnsEverythingInItsRadius()
    {
        Entity near = Monster("Near", new Point(9, 7), 20);
        Entity alsoNear = Monster("AlsoNear", new Point(9, 8), 20);
        Entity far = Monster("Far", new Point(14, 14), 20);

        GameWorld world = WorldWith(near, alsoNear, far);

        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Fireball, 8, radius: 2));
        world.UseItem(0);

        // Aim at the pair rather than at whatever the cursor picked.
        while (world.Aiming!.Cursor != new Point(9, 7))
        {
            world.MoveCursor(new Point(
                Math.Sign(9 - world.Aiming.Cursor.X),
                Math.Sign(7 - world.Aiming.Cursor.Y)));
        }

        Assert.True(world.ConfirmTarget());

        Assert.Equal(12, near.Fighter!.HitPoints);
        Assert.Equal(12, alsoNear.Fighter!.HitPoints);
        Assert.Equal(20, far.Fighter!.HitPoints);
    }

    [Fact]
    public void AFireballBurnsTheReaderToo()
    {
        // The scroll does not know who threw it, and a player who aims at their own feet should
        // find that out the honest way.
        GameWorld world = WorldWith();

        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Fireball, 8, radius: 2));
        world.UseItem(0);

        // The cursor starts on the player when nothing is visible, which is exactly the case.
        Assert.True(world.ConfirmTarget());

        Assert.Equal(22, world.Player.Fighter!.HitPoints);
    }

    [Fact]
    public void TheBlastIsRoundRatherThanSquare()
    {
        // A square blast reads as a bug even when it is deliberate, and it disagrees with how
        // sight measures distance. The corners of the bounding box must be outside it.
        Entity corner = Monster("Corner", new Point(9, 9), 20);
        Entity edge = Monster("Edge", new Point(9, 7), 20);

        GameWorld world = WorldWith(corner, edge);

        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Fireball, 8, radius: 2));
        world.UseItem(0);

        while (world.Aiming!.Cursor != new Point(7, 7))
        {
            world.MoveCursor(new Point(
                Math.Sign(7 - world.Aiming.Cursor.X),
                Math.Sign(7 - world.Aiming.Cursor.Y)));
        }

        world.ConfirmTarget();

        // (9,7) is two cells away on one axis: inside. (9,9) is two on both, so 8 > 4: outside.
        Assert.Equal(12, edge.Fighter!.HitPoints);
        Assert.Equal(20, corner.Fighter!.HitPoints);
    }

    [Fact]
    public void AHealingPotionCannotBeAimed()
    {
        Entity potion = Scroll(ConsumableKind.Healing, 8, 0);

        GameWorld world = WorldWith();

        Assert.Throws<InvalidOperationException>(
            () => potion.Consumable!.UseAt(world.Player, new Point(1, 1), world));
    }

    [Fact]
    public void AScrollCannotBeDrunk()
    {
        Entity scroll = Scroll(ConsumableKind.Lightning, 12, 0);

        Assert.Throws<InvalidOperationException>(() => scroll.Consumable!.UseOn(new Entity(
            "Player", '@', Color.White, new Point(0, 0), blocksMovement: true)
        {
            Fighter = new Fighter(30, 5, 2, experienceAwarded: 0),
        }));
    }

    [Fact]
    public void MovementKeysAimWhileTargeting()
    {
        // The same keys the player walks with, so aiming needs nothing new to learn.
        Assert.Equal(GameCommandKind.MoveCursor, CommandReader.Read(new[] { Keys.Left }, GameMode.Targeting).Kind);
        Assert.Equal(GameCommandKind.ConfirmTarget, CommandReader.Read(new[] { Keys.Enter }, GameMode.Targeting).Kind);
        Assert.Equal(GameCommandKind.CancelTarget, CommandReader.Read(new[] { Keys.Escape }, GameMode.Targeting).Kind);
    }

    [Fact]
    public void EscapeBeatsEnterWhenBothAreHeld()
    {
        // A player who panics should get out rather than fire.
        GameCommand command = CommandReader.Read(new[] { Keys.Enter, Keys.Escape }, GameMode.Targeting);

        Assert.Equal(GameCommandKind.CancelTarget, command.Kind);
    }

    [Fact]
    public void TargetingCannotBeEnteredBySettingTheMode()
    {
        // It carries state, so entering it without a scroll would leave Aiming null and the
        // player stuck in a mode nothing can resolve.
        GameWorld world = WorldWith();

        Assert.Throws<ArgumentException>(() => world.SetMode(GameMode.Targeting));
    }

    [Fact]
    public void AimingIsSetExactlyWhileTargeting()
    {
        GameWorld world = WorldWith(Monster("Rat", new Point(9, 7), 10));
        world.Player.Inventory!.TryAdd(Scroll(ConsumableKind.Lightning, 12, 0));

        Assert.Null(world.Aiming);

        world.UseItem(0);
        Assert.NotNull(world.Aiming);

        world.CancelTarget();
        Assert.Null(world.Aiming);
    }

    [Fact]
    public void AimingWithoutAScrollIsRejected()
    {
        Entity notAnItem = new Entity("rock", '*', Color.Gray, new Point(0, 0), blocksMovement: false);

        Assert.Throws<ArgumentException>(() => new Targeting(notAnItem, 0, new Point(1, 1), 0));
    }

    [Fact]
    public void ANegativeSlotOrRadiusIsRejected()
    {
        Entity scroll = Scroll(ConsumableKind.Lightning, 12, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => new Targeting(scroll, -1, new Point(1, 1), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Targeting(scroll, 0, new Point(1, 1), -1));
    }

    [Fact]
    public void ANullArgumentIsRejected()
    {
        Entity scroll = Scroll(ConsumableKind.Lightning, 12, 0);

        Assert.Throws<ArgumentNullException>(() => new Targeting(null!, 0, new Point(1, 1), 0));
        Assert.Throws<ArgumentNullException>(
            () => new Targeting(scroll, 0, new Point(1, 1), 0).MoveCursor(new Point(1, 0), null!));
    }
}

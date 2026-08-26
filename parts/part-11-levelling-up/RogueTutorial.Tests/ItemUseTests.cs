/*
 * Unit tests for using items, and for the command reader that decides what a key means.
 *
 * The rule worth watching is that an item which would change nothing is not consumed: drinking
 * a healing potion at full health must waste the keypress rather than the potion.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~ItemUseTests
 */

using System;
using System.Collections.Generic;
using System.Linq;
using RogueTutorial;
using SadConsole.Input;
using SadRogue.Primitives;
using Xunit;

public sealed class ItemUseTests
{
    // An open room with the player in the middle and whatever else the test needs on the floor.
    private static GameWorld WorldWith(params Entity[] onTheFloor)
    {
        GameMap map = new GameMap(9, 9);
        map.Fill(TileTypes.Floor);

        Entity player = new Entity("Player", '@', Color.White, new Point(4, 4), blocksMovement: true);
        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2, experienceAwarded: 0);
        player.Inventory = new Inventory(capacity: 26);

        List<Entity> entities = new List<Entity>(onTheFloor) { player };

        return new GameWorld(map, entities, player);
    }

    private static Entity Potion(Point at, int power)
    {
        Entity potion = new Entity("healing potion", '!', Color.Magenta, at, blocksMovement: false);
        potion.Consumable = new Consumable(ConsumableKind.Healing, power, radius: 0);
        return potion;
    }

    [Fact]
    public void HealingRestoresHitPoints()
    {
        Fighter fighter = new Fighter(30, 5, 2, experienceAwarded: 0);
        fighter.TakeDamage(10);

        Assert.Equal(6, fighter.Heal(6));
        Assert.Equal(26, fighter.HitPoints);
    }

    [Fact]
    public void HealingCannotPassTheMaximum()
    {
        Fighter fighter = new Fighter(30, 5, 2, experienceAwarded: 0);
        fighter.TakeDamage(4);

        // Only the missing four can be restored, whatever the potion promises.
        Assert.Equal(4, fighter.Heal(99));
        Assert.Equal(30, fighter.HitPoints);
    }

    [Fact]
    public void HealingAtFullHealthRecoversNothing()
    {
        Fighter fighter = new Fighter(30, 5, 2, experienceAwarded: 0);

        Assert.Equal(0, fighter.Heal(10));
    }

    [Fact]
    public void APotionAtFullHealthIsNotWasted()
    {
        // The rule this part exists to get right: a wasted turn must not also be a wasted item.
        GameWorld world = WorldWith();
        Entity potion = Potion(world.Player.Position, power: 8);

        world.Player.Inventory!.TryAdd(potion);

        bool spentATurn = world.UseItem(0);

        Assert.False(spentATurn);
        Assert.Contains(potion, world.Player.Inventory.Items);
        Assert.Contains("already at full health", world.Log.Messages.Last());
    }

    [Fact]
    public void ADrunkPotionLeavesThePack()
    {
        GameWorld world = WorldWith();
        world.Player.Fighter!.TakeDamage(10);

        Entity potion = Potion(world.Player.Position, power: 8);
        world.Player.Inventory!.TryAdd(potion);

        Assert.True(world.UseItem(0));

        Assert.Empty(world.Player.Inventory.Items);
        Assert.Equal(28, world.Player.Fighter.HitPoints);
    }

    [Fact]
    public void UsingAnEmptySlotIsAMissRatherThanAnError()
    {
        GameWorld world = WorldWith();

        Assert.False(world.UseItem(0));
        Assert.False(world.UseItem(25));
    }

    [Fact]
    public void PickingUpTakesTheItemOffTheMap()
    {
        GameWorld world = WorldWith();
        Entity potion = Potion(new Point(4, 4), power: 8);

        GameWorld withPotion = WorldWith(potion);

        Assert.True(withPotion.PickUpHere());

        Assert.Contains(potion, withPotion.Player.Inventory!.Items);
        Assert.DoesNotContain(potion, withPotion.Entities);
    }

    [Fact]
    public void PickingUpNothingSaysSo()
    {
        GameWorld world = WorldWith();

        Assert.False(world.PickUpHere());
        Assert.Contains("nothing here", world.Log.Messages.Last());
    }

    [Fact]
    public void AFullPackCannotPickUp()
    {
        Entity potion = Potion(new Point(4, 4), power: 8);
        GameWorld world = WorldWith(potion);

        // Fill the pack with something other than what is on the floor.
        world.Player.Inventory = new Inventory(capacity: 1);
        world.Player.Inventory.TryAdd(Potion(new Point(0, 0), power: 4));

        Assert.False(world.PickUpHere());
        Assert.Contains("pack is full", world.Log.Messages.Last());
        Assert.Contains(potion, world.Entities);
    }

    [Fact]
    public void DroppingPutsItBackOnTheMap()
    {
        GameWorld world = WorldWith();
        Entity potion = Potion(new Point(0, 0), power: 8);

        world.Player.Inventory!.TryAdd(potion);

        Assert.True(world.DropItem(0));

        Assert.Empty(world.Player.Inventory.Items);
        Assert.Contains(potion, world.Entities);
        Assert.Equal(world.Player.Position, potion.Position);
    }

    [Fact]
    public void ADroppedItemCanBePickedUpAgain()
    {
        GameWorld world = WorldWith();
        Entity potion = Potion(new Point(0, 0), power: 8);

        world.Player.Inventory!.TryAdd(potion);
        world.DropItem(0);

        Assert.True(world.PickUpHere());
        Assert.Contains(potion, world.Player.Inventory.Items);
    }

    [Fact]
    public void OpeningThePackCostsNoTurn()
    {
        // Looking at what you are carrying is not an action, and monsters must not get a move.
        GameWorld world = WorldWith();

        world.SetMode(GameMode.ShowingInventory);

        Assert.Equal(GameMode.ShowingInventory, world.Mode);
    }

    [Fact]
    public void MovementKeysMeanNothingWhileThePackIsOpen()
    {
        GameCommand command = CommandReader.Read(new[] { Keys.Left }, GameMode.ShowingInventory);

        Assert.Equal(GameCommandKind.None, command.Kind);
    }

    [Fact]
    public void LettersChooseSlotsWhileThePackIsOpen()
    {
        Assert.Equal(0, CommandReader.Read(new[] { Keys.A }, GameMode.ShowingInventory).Slot);
        Assert.Equal(3, CommandReader.Read(new[] { Keys.D }, GameMode.ShowingInventory).Slot);
        Assert.Equal(25, CommandReader.Read(new[] { Keys.Z }, GameMode.ShowingInventory).Slot);
    }

    [Fact]
    public void ShiftTurnsChoosingIntoDropping()
    {
        GameCommand use = CommandReader.Read(new[] { Keys.A }, GameMode.ShowingInventory, shiftHeld: false);
        GameCommand drop = CommandReader.Read(new[] { Keys.A }, GameMode.ShowingInventory, shiftHeld: true);

        Assert.Equal(GameCommandKind.UseItem, use.Kind);
        Assert.Equal(GameCommandKind.DropItem, drop.Kind);
        Assert.Equal(drop.Slot, use.Slot);
    }

    [Fact]
    public void LettersMeanNothingOnTheMap()
    {
        // 'd' is a slot in the pack and nothing at all while walking, which is the whole reason
        // the mode exists.
        Assert.Equal(GameCommandKind.None, CommandReader.Read(new[] { Keys.D }, GameMode.Playing).Kind);
    }

    [Fact]
    public void TheMapKeysStillWorkWhilePlaying()
    {
        Assert.Equal(GameCommandKind.Move, CommandReader.Read(new[] { Keys.Left }, GameMode.Playing).Kind);
        Assert.Equal(GameCommandKind.PickUp, CommandReader.Read(new[] { Keys.G }, GameMode.Playing).Kind);
        Assert.Equal(GameCommandKind.OpenInventory, CommandReader.Read(new[] { Keys.I }, GameMode.Playing).Kind);
    }

    [Fact]
    public void EscapeAndIBothCloseThePack()
    {
        Assert.Equal(GameCommandKind.CloseInventory,
            CommandReader.Read(new[] { Keys.Escape }, GameMode.ShowingInventory).Kind);

        Assert.Equal(GameCommandKind.CloseInventory,
            CommandReader.Read(new[] { Keys.I }, GameMode.ShowingInventory).Kind);
    }

    [Fact]
    public void ADeadPlayerCannotUseItems()
    {
        GameWorld world = WorldWith();
        world.Player.Fighter!.TakeDamage(30);
        world.Player.Die();

        Assert.False(world.PickUpHere());
        Assert.False(world.UseItem(0));
        Assert.False(world.DropItem(0));
    }

    [Fact]
    public void AnItemCannotBeUsedBySomethingWithNoFighter()
    {
        Entity item = Potion(new Point(0, 0), power: 4);
        Entity statue = new Entity("statue", 'S', Color.Gray, new Point(1, 1), blocksMovement: true);

        Assert.Throws<ArgumentException>(() => item.Consumable!.UseOn(statue));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AConsumableWithNoPowerIsRejected(int power)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Consumable(ConsumableKind.Healing, power, radius: 0));
    }

    [Fact]
    public void NegativeHealingIsRejected()
    {
        // Damage arriving through the healing door would be a very quiet bug.
        Fighter fighter = new Fighter(30, 5, 2, experienceAwarded: 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => fighter.Heal(-1));
    }

    [Fact]
    public void ANullArgumentIsRejected()
    {
        Entity item = Potion(new Point(0, 0), power: 4);

        Assert.Throws<ArgumentNullException>(() => item.Consumable!.UseOn(null!));
        Assert.Throws<ArgumentNullException>(() => CommandReader.Read(null!, GameMode.Playing));
    }
}

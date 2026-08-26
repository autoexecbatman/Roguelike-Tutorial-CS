/*
 * Unit tests for what an entity carries. The capacity is the point: an unbounded pack removes
 * every decision about what to leave behind.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~InventoryTests
 */

using System;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class InventoryTests
{
    private static Entity Item(string name)
    {
        Entity item = new Entity(name, '!', Color.Magenta, new Point(0, 0), blocksMovement: false);
        item.Consumable = new Consumable(ConsumableKind.Healing, power: 4);
        return item;
    }

    [Fact]
    public void ANewPackIsEmpty()
    {
        Inventory pack = new Inventory(capacity: 5);

        Assert.Empty(pack.Items);
        Assert.False(pack.IsFull);
    }

    [Fact]
    public void AnItemAddedIsCarried()
    {
        Inventory pack = new Inventory(5);
        Entity potion = Item("potion");

        Assert.True(pack.TryAdd(potion));
        Assert.Contains(potion, pack.Items);
    }

    [Fact]
    public void ItemsKeepThePickUpOrder()
    {
        // The order is what the player sees as slots, so it must not be sorted underneath them.
        Inventory pack = new Inventory(5);

        Entity first = Item("first");
        Entity second = Item("second");

        pack.TryAdd(first);
        pack.TryAdd(second);

        Assert.Same(first, pack.At(0));
        Assert.Same(second, pack.At(1));
    }

    [Fact]
    public void AFullPackRefusesRatherThanThrows()
    {
        // Running out of room is an ordinary thing that happens to a player, not a bug.
        Inventory pack = new Inventory(capacity: 2);

        Assert.True(pack.TryAdd(Item("one")));
        Assert.True(pack.TryAdd(Item("two")));
        Assert.False(pack.TryAdd(Item("three")));

        Assert.Equal(2, pack.Items.Count);
        Assert.True(pack.IsFull);
    }

    [Fact]
    public void RemovingMakesRoomAgain()
    {
        Inventory pack = new Inventory(capacity: 1);
        Entity potion = Item("potion");

        pack.TryAdd(potion);
        Assert.True(pack.IsFull);

        pack.Remove(potion);

        Assert.False(pack.IsFull);
        Assert.True(pack.TryAdd(Item("another")));
    }

    [Fact]
    public void AnEmptySlotAnswersNullRatherThanThrowing()
    {
        // A keypress is checked against the pack directly: pressing 'd' with two items carried
        // is a miss, not an error.
        Inventory pack = new Inventory(5);

        pack.TryAdd(Item("only"));

        Assert.Null(pack.At(1));
        Assert.Null(pack.At(25));
        Assert.Null(pack.At(-1));
    }

    [Fact]
    public void TheSameItemCannotBeCarriedTwice()
    {
        // Two slots holding one entity would let it be dropped twice and used twice.
        Inventory pack = new Inventory(5);
        Entity potion = Item("potion");

        pack.TryAdd(potion);

        Assert.Throws<ArgumentException>(() => pack.TryAdd(potion));
    }

    [Fact]
    public void RemovingSomethingNotCarriedIsRejected()
    {
        Inventory pack = new Inventory(5);

        Assert.Throws<ArgumentException>(() => pack.Remove(Item("never added")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void APackThatHoldsNothingIsRejected(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Inventory(capacity));
    }

    [Fact]
    public void ANullItemIsRejected()
    {
        Inventory pack = new Inventory(5);

        Assert.Throws<ArgumentNullException>(() => pack.TryAdd(null!));
        Assert.Throws<ArgumentNullException>(() => pack.Remove(null!));
    }
}

/*
 * Unit and integration tests for wearing and wielding.
 *
 * The rule worth watching: equipment changes what a fighter's numbers come out as, without ever
 * changing the numbers themselves. Nothing is added to Fighter on equip and subtracted on
 * unequip, so there is no stored total to drift.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~EquipmentTests
 */

using System;
using System.Collections.Generic;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class EquipmentTests
{
    private static Entity Player()
    {
        Entity player = new Entity(
            "Player", '@', Color.White, new Point(4, 4), blocksMovement: true, RenderLayer.Player);

        player.Fighter = new Fighter(30, 5, 2, experienceAwarded: 0);
        player.Inventory = new Inventory(26);
        player.Equipment = new Equipment();

        return player;
    }

    private static Entity Weapon(string name, int attackBonus)
    {
        Entity weapon = new Entity(
            name, '/', Color.Gray, new Point(0, 0), blocksMovement: false, RenderLayer.Item);

        weapon.Equippable = new Equippable(EquipmentSlot.Weapon, attackBonus, defenceBonus: 0);

        return weapon;
    }

    private static Entity Armour(string name, int defenceBonus)
    {
        Entity armour = new Entity(
            name, '[', Color.Gray, new Point(0, 0), blocksMovement: false, RenderLayer.Item);

        armour.Equippable = new Equippable(EquipmentSlot.Armour, attackBonus: 0, defenceBonus);

        return armour;
    }

    [Fact]
    public void NothingEquippedLeavesTheNumbersAlone()
    {
        Entity player = Player();

        Assert.Equal(5, player.EffectiveAttack);
        Assert.Equal(2, player.EffectiveDefence);
    }

    [Fact]
    public void AWieldedWeaponAddsItsAttack()
    {
        Entity player = Player();
        Entity sword = Weapon("sword", attackBonus: 3);

        player.Equipment!.Equip(sword);

        Assert.Equal(8, player.EffectiveAttack);
    }

    [Fact]
    public void TheFightersOwnNumbersNeverChange()
    {
        // The whole design: nothing is written into Fighter, so nothing can drift out of step.
        Entity player = Player();

        player.Equipment!.Equip(Weapon("sword", attackBonus: 3));
        player.Equipment.Equip(Armour("mail", defenceBonus: 4));

        Assert.Equal(5, player.Fighter!.Attack);
        Assert.Equal(2, player.Fighter.Defence);
    }

    [Fact]
    public void TakingSomethingOffRemovesItsBonus()
    {
        Entity player = Player();
        Entity sword = Weapon("sword", attackBonus: 3);

        player.Equipment!.Equip(sword);
        player.Equipment.Unequip(EquipmentSlot.Weapon);

        Assert.Equal(5, player.EffectiveAttack);
    }

    [Fact]
    public void ASecondWeaponReplacesTheFirst()
    {
        // Two hands is a rule this game does not have, so the old one comes back to the pack.
        Entity player = Player();
        Entity dagger = Weapon("dagger", attackBonus: 1);
        Entity sword = Weapon("sword", attackBonus: 3);

        player.Equipment!.Equip(dagger);
        Entity? displaced = player.Equipment.Equip(sword);

        Assert.Same(dagger, displaced);
        Assert.Equal(8, player.EffectiveAttack);
    }

    [Fact]
    public void ArmourAndAWeaponDoNotCompete()
    {
        Entity player = Player();

        player.Equipment!.Equip(Weapon("sword", attackBonus: 3));
        player.Equipment.Equip(Armour("mail", defenceBonus: 4));

        Assert.Equal(8, player.EffectiveAttack);
        Assert.Equal(6, player.EffectiveDefence);
    }

    [Fact]
    public void EquipmentReachesCombat()
    {
        // Armour that does not change what a blow does is decoration.
        Entity attacker = Player();
        Entity target = Player();

        int bare = Combat.Resolve(attacker, target).DamageDealt;

        target.Equipment!.Equip(Armour("mail", defenceBonus: 2));

        int armoured = Combat.Resolve(attacker, target).DamageDealt;

        Assert.Equal(bare - 2, armoured);
    }

    [Fact]
    public void SomethingWithNoEquippableIsRefused()
    {
        Entity player = Player();
        Entity potion = new Entity(
            "potion", '!', Color.Magenta, new Point(0, 0), blocksMovement: false, RenderLayer.Item);

        Assert.Throws<ArgumentException>(() => player.Equipment!.Equip(potion));
    }

    [Fact]
    public void AnEmptySlotUnequipsToNothing()
    {
        Entity player = Player();

        Assert.Null(player.Equipment!.Unequip(EquipmentSlot.Weapon));
    }

    [Fact]
    public void AMonsterWithNoEquipmentStillFights()
    {
        // Only the player has an Equipment component; everything else reads its Fighter alone.
        Entity rat = new Entity(
            "Rat", 'r', Color.Red, new Point(0, 0), blocksMovement: true, RenderLayer.Actor);
        rat.Fighter = new Fighter(4, 3, 0, experienceAwarded: 10);

        Assert.Equal(3, rat.EffectiveAttack);
        Assert.Equal(0, rat.EffectiveDefence);
    }
    // A world holding one player and whatever else is passed, on open floor.
    private static GameWorld WorldWith(Entity player, params Entity[] others)
    {
        GameMap map = new GameMap(9, 9);
        map.Fill(TileTypes.Floor);

        List<Entity> entities = new List<Entity> { player };
        entities.AddRange(others);

        return new GameWorld(map, entities, player);
    }

    [Fact]
    public void ChoosingEquipmentFromThePackPutsItOn()
    {
        // There is no separate wear key: equipment has no other use, so the use key does it.
        Entity player = Player();
        Entity sword = Weapon("sword", attackBonus: 3);

        player.Inventory!.TryAdd(sword);

        GameWorld world = WorldWith(player);

        Assert.True(world.UseItem(0));
        Assert.True(player.Equipment!.IsEquipped(sword));
        Assert.Equal(8, player.EffectiveAttack);
    }

    [Fact]
    public void ChoosingItAgainTakesItOff()
    {
        Entity player = Player();
        Entity sword = Weapon("sword", attackBonus: 3);

        player.Inventory!.TryAdd(sword);

        GameWorld world = WorldWith(player);

        world.UseItem(0);
        world.UseItem(0);

        Assert.False(player.Equipment!.IsEquipped(sword));
        Assert.Equal(5, player.EffectiveAttack);
    }

    [Fact]
    public void EquippingKeepsItInThePack()
    {
        // Wearing something is a way of using it, not a way of carrying it, so the letters stay
        // where they are and nothing has to be re-learned.
        Entity player = Player();
        Entity sword = Weapon("sword", attackBonus: 3);

        player.Inventory!.TryAdd(sword);

        WorldWith(player).UseItem(0);

        Assert.Contains(sword, player.Inventory.Items);
    }

    [Fact]
    public void DroppingSomethingWornTakesItOffFirst()
    {
        // Otherwise it lies on the floor still adding its bonus.
        Entity player = Player();
        Entity sword = Weapon("sword", attackBonus: 3);

        player.Inventory!.TryAdd(sword);

        GameWorld world = WorldWith(player);

        world.UseItem(0);
        world.DropItem(0);

        Assert.False(player.Equipment!.IsEquipped(sword));
        Assert.Equal(5, player.EffectiveAttack);
    }

    [Fact]
    public void ThePackSaysWhatIsWorn()
    {
        Entity player = Player();
        Entity sword = Weapon("sword", attackBonus: 3);
        Entity potion = new Entity(
            "potion", '!', Color.Magenta, new Point(0, 0), blocksMovement: false, RenderLayer.Item);
        potion.Consumable = new Consumable(ConsumableKind.Healing, power: 4, radius: 0);

        player.Inventory!.TryAdd(sword);
        player.Inventory.TryAdd(potion);

        GameWorld world = WorldWith(player);
        world.UseItem(0);
        world.SetMode(GameMode.ShowingInventory);

        ScreenLayout layout = new ScreenLayout(80, 25, logRows: 5);
        string screen = ScreenComposer.Compose(world, layout).ToText();

        Assert.Contains("sword (equipped)", screen);
        Assert.DoesNotContain("potion (equipped)", screen);
    }

    [Fact]
    public void ANewPlayerWearsNothing()
    {
        GameWorld world = GameWorld.Generate(
            60, 30, new Random(9), MonsterTable.Standard, ItemTable.Standard, depth: 1);

        Assert.NotNull(world.Player.Equipment);
        Assert.Empty(world.Player.Equipment!.Worn);
        Assert.Equal(world.Player.Fighter!.Attack, world.Player.EffectiveAttack);
    }

    [Fact]
    public void TheDungeonContainsEquipment()
    {
        // Nothing to find means the component is unreachable in a real game.
        HashSet<string> found = new HashSet<string>();

        for (int seed = 1; seed <= 30; seed++)
        {
            GameWorld world = GameWorld.Generate(
                60, 30, new Random(seed), MonsterTable.Standard, ItemTable.Standard, depth: 1);

            foreach (Entity entity in world.Entities)
            {
                if (entity.Equippable is not null)
                {
                    found.Add(entity.Name);
                }
            }
        }

        Assert.Contains("dagger", found);
        Assert.Contains("leather armour", found);
    }

    [Fact]
    public void BetterEquipmentIsDeeper()
    {
        // The same rule Part 12 gave monsters: a sword on floor one would skip the early game.
        HashSet<string> shallow = new HashSet<string>();

        for (int seed = 1; seed <= 30; seed++)
        {
            GameWorld world = GameWorld.Generate(
                60, 30, new Random(seed), MonsterTable.Standard, ItemTable.Standard, depth: 1);

            foreach (Entity entity in world.Entities)
            {
                shallow.Add(entity.Name);
            }
        }

        Assert.DoesNotContain("sword", shallow);
        Assert.DoesNotContain("chain mail", shallow);
    }

    [Fact]
    public void EquipmentWithNoBonusIsRejected()
    {
        // Something worth wearing has to be worth something.
        Assert.Throws<ArgumentException>(
            () => new Equippable(EquipmentSlot.Weapon, attackBonus: 0, defenceBonus: 0));
    }

    [Fact]
    public void CursedEquipmentIsRejected()
    {
        // A negative bonus would arrive as an unexplained weakening far from the item.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Equippable(EquipmentSlot.Weapon, attackBonus: -1, defenceBonus: 0));
    }
    [Fact]
    public void EquipmentCanBePickedUpOffTheFloor()
    {
        // Everything else in this part is unreachable if this fails: the dungeon places daggers
        // and armour, and the only way into the pack is standing on one and pressing g.
        Entity player = Player();
        Entity armour = Armour("leather armour", defenceBonus: 1);

        armour.MoveTo(player.Position);

        GameWorld world = WorldWith(player, armour);

        Assert.True(world.PickUpHere());
        Assert.Contains(armour, player.Inventory!.Items);
    }

    [Fact]
    public void PickingUpFindsEquipmentAmongItems()
    {
        // The cell holds a potion and a dagger; both must be reachable, one press each.
        Entity player = Player();
        Entity dagger = Weapon("dagger", attackBonus: 2);
        Entity potion = new Entity(
            "potion", '!', Color.Magenta, player.Position, blocksMovement: false, RenderLayer.Item);
        potion.Consumable = new Consumable(ConsumableKind.Healing, power: 4, radius: 0);

        dagger.MoveTo(player.Position);

        GameWorld world = WorldWith(player, dagger, potion);

        world.PickUpHere();
        world.PickUpHere();

        Assert.Equal(2, player.Inventory!.Items.Count);
    }

    [Fact]
    public void ACorpseIsNotAnItem()
    {
        // Corpses lie on the floor and do not block, so they share a cell with the player more
        // often than anything else does. Nothing without a use and nothing to wear is carryable.
        Entity player = Player();

        Entity rat = new Entity(
            "Rat", 'r', Color.Red, player.Position, blocksMovement: true, RenderLayer.Actor);
        rat.Fighter = new Fighter(4, 3, 0, experienceAwarded: 10);
        rat.Die();

        GameWorld world = WorldWith(player, rat);

        Assert.False(world.PickUpHere());
        Assert.Empty(player.Inventory!.Items);
    }

}

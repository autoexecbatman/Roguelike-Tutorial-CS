/*
 * Unit tests for writing a game down and reading it back.
 *
 * The test that decides whether any of this works is the round trip: save a world, load it, and
 * compare the composed frame. The picture is what a player would notice changing, so it is what
 * to compare - and it is the same argument RenderedFrame.ToText has served since Part 2.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~SaveGameTests
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueTutorial;
using SadRogue.Primitives;
using Xunit;

public sealed class SaveGameTests
{
    // A generated world is a better subject than a hand-built one: it has rooms, corridors,
    // monsters, items and a field of view, which is most of what a save has to carry.
    private static GameWorld GeneratedWorld(int seed)
    {
        return GameWorld.Generate(40, 20, new Random(seed), MonsterTable.Standard, ItemTable.Standard, depth: 1);
    }

    private static string Picture(GameWorld world)
    {
        return world.ComposeFrame().ToText();
    }

    [Fact]
    public void AWorldSurvivesTheRoundTrip()
    {
        // The whole part in one assertion.
        GameWorld original = GeneratedWorld(12345);

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(Picture(original), Picture(restored));
    }

    [Fact]
    public void TheRoundTripSurvivesJsonToo()
    {
        // Capture and Restore agreeing is not enough: the text in between is what reaches disk.
        GameWorld original = GeneratedWorld(7);

        string json = SaveGame.ToJson(SaveGame.Capture(original));

        GameWorld restored = SaveGame.Restore(SaveGame.FromJson(json));

        Assert.Equal(Picture(original), Picture(restored));
    }

    [Fact]
    public void ExploringIsRemembered()
    {
        // A save that forgets where you have been sends you back into a dungeon you have already
        // walked, which is the difference between resuming and starting over.
        GameWorld original = GeneratedWorld(3);

        for (int step = 0; step < 30; step++)
        {
            original.MovePlayer(new Point(1, 0));
            original.MovePlayer(new Point(0, 1));
        }

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(Picture(original), Picture(restored));
    }

    [Fact]
    public void DamageIsRemembered()
    {
        GameWorld original = GeneratedWorld(9);
        original.Player.Fighter!.TakeDamage(11);

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(19, restored.Player.Fighter!.HitPoints);
        Assert.Equal(30, restored.Player.Fighter.MaximumHitPoints);
    }

    [Fact]
    public void CarriedItemsComeBackInThePack()
    {
        GameWorld original = GeneratedWorld(5);

        Entity potion = new Entity("potion", '!', Color.Magenta, original.Player.Position, blocksMovement: false, RenderLayer.Item);
        potion.Consumable = new Consumable(ConsumableKind.Healing, power: 8, radius: 0);
        original.Player.Inventory!.TryAdd(potion);

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Single(restored.Player.Inventory!.Items);
        Assert.Equal("potion", restored.Player.Inventory.Items[0].Name);
        Assert.Equal(ConsumableKind.Healing, restored.Player.Inventory.Items[0].Consumable!.Kind);
    }

    [Fact]
    public void ACarriedItemIsNotAlsoOnTheMap()
    {
        // The reason entities carry an id. An item is in the pack or on the floor, never both,
        // and writing it in two places would restore two of it.
        GameWorld original = GeneratedWorld(5);

        Entity potion = new Entity("potion", '!', Color.Magenta, original.Player.Position, blocksMovement: false, RenderLayer.Item);
        potion.Consumable = new Consumable(ConsumableKind.Healing, power: 8, radius: 0);
        original.Player.Inventory!.TryAdd(potion);

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.DoesNotContain(restored.Entities, entity => entity.Name == "potion");
    }

    [Fact]
    public void ThereIsExactlyOnePlayerAfterLoading()
    {
        // The player is in the entity list and named separately. A naive save writes them twice.
        GameWorld original = GeneratedWorld(11);

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Single(restored.Entities, entity => entity.Name == "Player");
        Assert.Contains(restored.Player, restored.Entities);
    }

    [Fact]
    public void MonstersComeBackWhereTheyWere()
    {
        GameWorld original = GeneratedWorld(2);

        string before = string.Join(";", original.Entities.Select(e => $"{e.Name}{e.Position}"));

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(before, string.Join(";", restored.Entities.Select(e => $"{e.Name}{e.Position}")));
    }

    [Fact]
    public void ACorpseStaysACorpse()
    {
        // A corpse is an entity with no Fighter that does not block. Restoring it as a living
        // monster would put a rat back in a room the player had already cleared.
        GameWorld original = GeneratedWorld(4);

        Entity rat = new Entity("Rat", 'r', Color.Red, new Point(1, 1), blocksMovement: true, RenderLayer.Actor);
        rat.Fighter = new Fighter(4, 3, 0, experienceAwarded: 0);
        rat.Die();

        GameWorld world = new GameWorld(original.Map, new List<Entity> { rat, original.Player }, original.Player);

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(world));

        Entity restoredRat = restored.Entities.First(entity => entity.Name.StartsWith("remains"));

        Assert.Null(restoredRat.Fighter);
        Assert.False(restoredRat.BlocksMovement);
    }

    [Fact]
    public void LevelAndExperienceComeBack()
    {
        GameWorld original = GeneratedWorld(21);

        original.Player.Level!.Award(original.Player.Level.ExperienceForNextLevel + 5);
        original.Player.Level.Advance();
        original.Player.Fighter!.RaiseAttack(1);

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(2, restored.Player.Level!.CurrentLevel);
        Assert.Equal(5, restored.Player.Level.Experience);
        Assert.Equal(6, restored.Player.Fighter!.Attack);
    }

    [Fact]
    public void TheNextThresholdComesBackWithTheLevel()
    {
        // Restoring the level but not what the next one costs would make a level-five character
        // advance at a level-one price.
        GameWorld original = GeneratedWorld(22);

        for (int gained = 0; gained < 3; gained++)
        {
            original.Player.Level!.Award(original.Player.Level.ExperienceForNextLevel);
            original.Player.Level.Advance();
        }

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(
            original.Player.Level!.ExperienceForNextLevel,
            restored.Player.Level!.ExperienceForNextLevel);
    }

    [Fact]
    public void MonstersAwardTheSameExperienceAfterLoading()
    {
        GameWorld original = GeneratedWorld(23);

        string before = string.Join(";", original.Entities
            .Where(entity => entity.Fighter is not null)
            .Select(entity => $"{entity.Name}:{entity.Fighter!.ExperienceAwarded}"));

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(before, string.Join(";", restored.Entities
            .Where(entity => entity.Fighter is not null)
            .Select(entity => $"{entity.Name}:{entity.Fighter!.ExperienceAwarded}")));
    }

    [Fact]
    public void APart10SaveIsRefusedRatherThanResettingTheCharacter()
    {
        // The first real use of the version check. A version 1 save has no record of experience
        // or levels, so resuming one would silently return a levelled character to level one.
        SavedWorld saved = SaveGame.Capture(GeneratedWorld(24));
        saved.Version = 1;

        Assert.Throws<InvalidDataException>(() => SaveGame.Restore(saved));
    }

    [Fact]
    public void TheLogComesBack()
    {
        GameWorld original = GeneratedWorld(6);
        original.Log.Add("You hit the Rat for 3 damage.");
        original.Log.Add("Rat dies.");

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(original.Log.Messages, restored.Log.Messages);
    }

    [Fact]
    public void TheAimingCursorIsNotSaved()
    {
        // How the player is looking at the game is not what the game is. A save made mid-aim
        // must not reopen with a crosshair over a scroll that was never fired.
        GameWorld original = GeneratedWorld(8);

        Entity scroll = new Entity("scroll", '?', Color.Yellow, original.Player.Position, blocksMovement: false, RenderLayer.Item);
        scroll.Consumable = new Consumable(ConsumableKind.Lightning, power: 12, radius: 0);
        original.Player.Inventory!.TryAdd(scroll);

        original.UseItem(0);
        Assert.Equal(GameMode.Targeting, original.Mode);

        GameWorld restored = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(GameMode.Playing, restored.Mode);
        Assert.Null(restored.Aiming);

        // The scroll itself is still carried: only the aiming was transient.
        Assert.Single(restored.Player.Inventory!.Items);
    }

    [Fact]
    public void AFileRoundTripsThroughDisk()
    {
        string path = Path.Combine(Path.GetTempPath(), $"roguetutorial-{Guid.NewGuid():N}.json");

        try
        {
            GameWorld original = GeneratedWorld(13);

            Assert.False(SaveGame.Exists(path));

            SaveGame.Write(original, path);

            Assert.True(SaveGame.Exists(path));
            Assert.Equal(Picture(original), Picture(SaveGame.Read(path)));

            SaveGame.Delete(path);

            Assert.False(SaveGame.Exists(path));
        }
        finally
        {
            // The test must not leave a file behind whether it passed or not.
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void AnUnreadableSaveIsReplacedRatherThanThrown()
    {
        // Refusing to read it is right. Crashing over it is not: a player whose save is from an
        // older build would otherwise be unable to start the game without deleting a file they
        // do not know exists.
        string path = Path.Combine(Path.GetTempPath(), $"roguetutorial-{Guid.NewGuid():N}.json");

        try
        {
            SavedWorld saved = SaveGame.Capture(GeneratedWorld(31));
            saved.Version = 1;

            File.WriteAllText(path, SaveGame.ToJson(saved));

            GameWorld? resumed = SaveGame.ReadIfReadable(path, out string? problem);

            Assert.Null(resumed);
            Assert.NotNull(problem);
            Assert.Contains("version 1", problem);

            // Deleted, or every start would try and fail on the same file forever.
            Assert.False(SaveGame.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void NoSaveIsNotAProblemToReport()
    {
        // Nothing to resume is the ordinary first run, not a fault worth a message.
        string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");

        Assert.Null(SaveGame.ReadIfReadable(path, out string? problem));
        Assert.Null(problem);
    }

    [Fact]
    public void AReadableSaveComesBackUnharmed()
    {
        string path = Path.Combine(Path.GetTempPath(), $"roguetutorial-{Guid.NewGuid():N}.json");

        try
        {
            GameWorld original = GeneratedWorld(32);
            SaveGame.Write(original, path);

            GameWorld? resumed = SaveGame.ReadIfReadable(path, out string? problem);

            Assert.NotNull(resumed);
            Assert.Null(problem);
            Assert.Equal(Picture(original), Picture(resumed));

            // A save that read correctly stays on disk.
            Assert.True(SaveGame.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void CorruptTextIsReplacedToo()
    {
        // Not only a version mismatch: a truncated file has to be survivable as well.
        string path = Path.Combine(Path.GetTempPath(), $"roguetutorial-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(path, "{ \"Version\": 2, \"Width\": ");

            Assert.Null(SaveGame.ReadIfReadable(path, out string? problem));
            Assert.NotNull(problem);
            Assert.False(SaveGame.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void DeletingASaveThatIsNotThereIsFine()
    {
        // Deleting what is already gone is the outcome the caller wanted either way.
        SaveGame.Delete(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json"));
    }

    [Fact]
    public void ReadingAMissingSaveThrowsRatherThanStartingOver()
    {
        // Silently starting a fresh game is the worst possible answer: it discards the run the
        // player was asking for and looks like the save never existed.
        string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");

        Assert.Throws<FileNotFoundException>(() => SaveGame.Read(path));
    }

    [Fact]
    public void ASaveFromAnotherVersionIsRefused()
    {
        // A half-read save is a corrupt game that looks like a working one.
        SavedWorld saved = SaveGame.Capture(GeneratedWorld(1));
        saved.Version = 99;

        Assert.Throws<InvalidDataException>(() => SaveGame.Restore(saved));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("this is not json")]
    [InlineData("{ \"Version\": ")]
    public void UnreadableTextIsRefused(string json)
    {
        Assert.Throws<InvalidDataException>(() => SaveGame.FromJson(json));
    }

    [Fact]
    public void TheMapIsStoredAsOneLinePerRow()
    {
        // A save nobody can read is not the format that was chosen. Storing a record per cell
        // put a forty-by-twenty dungeon in five thousand lines; a palette and a row of letters
        // puts it in twenty, and the room shapes are visible in the file.
        SavedWorld saved = SaveGame.Capture(GeneratedWorld(12345));

        Assert.Equal(20, saved.TileRows.Count);
        Assert.All(saved.TileRows, row => Assert.Equal(40, row.Length));

        // Rock, floor and the stairs down: three entries cover every cell of a floor. The stairs
        // needed no save code of their own, which is the whole reason they are a tile.
        Assert.Equal(3, saved.TilePalette.Count);
    }

    [Fact]
    public void AMapWithTheWrongNumberOfRowsIsRefused()
    {
        // A row short would shift the rest of the dungeon by a cell: subtly wrong everywhere
        // rather than obviously wrong once, which is the harder kind of bug to see.
        SavedWorld saved = SaveGame.Capture(GeneratedWorld(1));
        saved.TileRows.RemoveAt(0);

        Assert.Throws<InvalidDataException>(() => SaveGame.Restore(saved));
    }

    [Fact]
    public void ARowOfTheWrongLengthIsRefused()
    {
        SavedWorld saved = SaveGame.Capture(GeneratedWorld(1));
        saved.TileRows[3] = saved.TileRows[3].Substring(1);

        Assert.Throws<InvalidDataException>(() => SaveGame.Restore(saved));
    }

    [Fact]
    public void ACellPointingOutsideThePaletteIsRefused()
    {
        SavedWorld saved = SaveGame.Capture(GeneratedWorld(1));
        saved.TileRows[2] = "z" + saved.TileRows[2].Substring(1);

        Assert.Throws<InvalidDataException>(() => SaveGame.Restore(saved));
    }

    [Fact]
    public void ABlankPathIsRejected()
    {
        GameWorld world = GeneratedWorld(1);

        Assert.Throws<ArgumentException>(() => SaveGame.Write(world, "   "));
        Assert.Throws<ArgumentException>(() => SaveGame.Read(""));
        Assert.False(SaveGame.Exists(""));
    }

    [Fact]
    public void ANullArgumentIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => SaveGame.Capture(null!));
        Assert.Throws<ArgumentNullException>(() => SaveGame.Restore(null!));
        Assert.Throws<ArgumentNullException>(() => SaveGame.ToJson(null!));
        Assert.Throws<ArgumentNullException>(() => SaveGame.Write(null!, "x.json"));
    }
    [Fact]
    public void TheFloorSurvivesTheRoundTrip()
    {
        // Resuming on floor one after walking down to five would undo the whole descent.
        GameWorld original = GeneratedWorld(4242);

        original.RestoreDepth(5);

        GameWorld resumed = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(5, resumed.Depth);
    }

    [Fact]
    public void ASaveWithNoFloorIsRefused()
    {
        // Version 2 had no depth, so it deserialises as zero. Restoring it would hand the tables
        // a floor number they refuse, far from the file that caused it.
        GameWorld original = GeneratedWorld(4242);

        SavedWorld saved = SaveGame.Capture(original);
        saved.Depth = 0;

        Assert.Throws<InvalidDataException>(() => SaveGame.Restore(saved));
    }

    [Fact]
    public void TheDrawLayerSurvivesTheRoundTrip()
    {
        // Otherwise a resumed game goes back to items covering the monsters standing under them.
        GameWorld original = GeneratedWorld(4242);

        Entity potion = new Entity(
            "potion", '!', Color.Magenta, original.Player.Position, blocksMovement: false, RenderLayer.Item);
        potion.Consumable = new Consumable(ConsumableKind.Healing, power: 4, radius: 0);

        GameWorld resumed = SaveGame.Restore(SaveGame.Capture(
            new GameWorld(original.Map, new[] { original.Player, potion }, original.Player)));

        Assert.Equal(RenderLayer.Player, resumed.Player.Layer);
        Assert.Contains(resumed.Entities, entity => entity.Layer == RenderLayer.Item);
    }

    [Fact]
    public void WhatIsWornSurvivesTheRoundTrip()
    {
        GameWorld original = GeneratedWorld(4242);

        Entity sword = new Entity(
            "sword", '/', Color.Gray, original.Player.Position, blocksMovement: false, RenderLayer.Item);
        sword.Equippable = new Equippable(EquipmentSlot.Weapon, attackBonus: 3, defenceBonus: 0);

        original.Player.Inventory!.TryAdd(sword);
        original.Player.Equipment!.Equip(sword);

        int attack = original.Player.EffectiveAttack;

        GameWorld resumed = SaveGame.Restore(SaveGame.Capture(original));

        Assert.Equal(attack, resumed.Player.EffectiveAttack);
        Assert.Single(resumed.Player.Equipment!.Worn);
    }

    [Fact]
    public void TheWornItemIsTheSameObjectAsThePackedOne()
    {
        // Two copies would mean taking it off left a ghost in the pack still adding its bonus.
        GameWorld original = GeneratedWorld(4242);

        Entity sword = new Entity(
            "sword", '/', Color.Gray, original.Player.Position, blocksMovement: false, RenderLayer.Item);
        sword.Equippable = new Equippable(EquipmentSlot.Weapon, attackBonus: 3, defenceBonus: 0);

        original.Player.Inventory!.TryAdd(sword);
        original.Player.Equipment!.Equip(sword);

        GameWorld resumed = SaveGame.Restore(SaveGame.Capture(original));

        Entity packed = resumed.Player.Inventory!.Items[0];

        Assert.True(resumed.Player.Equipment!.IsEquipped(packed));
    }

    [Fact]
    public void AVersionThreeSaveIsRefused()
    {
        // Version 3 recorded no equipment, so resuming one would silently disarm the player.
        GameWorld original = GeneratedWorld(4242);

        SavedWorld saved = SaveGame.Capture(original);
        saved.Version = 3;

        Assert.Throws<InvalidDataException>(() => SaveGame.Restore(saved));
    }

}

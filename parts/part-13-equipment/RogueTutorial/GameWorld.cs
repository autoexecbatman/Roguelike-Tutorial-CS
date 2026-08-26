/*
 * Everything the game is: the dungeon, who is standing in it, and what the player has seen.
 *
 * This exists because the state had outgrown the screen class. RootScreen cannot be constructed
 * without a graphics host, so anything living on it is beyond the reach of a test - the same
 * boundary Part 1 drew for rules, applied now to state. A GameWorld can be built, driven and
 * inspected in a test process with no window anywhere.
 *
 * Usage:
 *
 *     GameWorld world = GameWorld.Generate(80, 25, new Random(12345), MonsterTable.Standard);
 *
 *     world.MovePlayer(new Point(1, 0));                  // one step right, or an attack
 *     world.PickUpHere();                                  // take what is underfoot
 *     world.UseItem(slot: 0);                              // drink the first thing in the pack
 *     bool over = world.IsPlayerDead;                      // the game ends when this is true
 *     IReadOnlyList<string> said = world.Log.Latest(5);    // what just happened
 *     Point where = world.Player.Position;
 *     RenderedFrame frame = world.ComposeFrame();         // what the player currently perceives
 *     Entity? blocker = world.BlockingEntityAt(where);    // null when the cell is clear
 *
 * Refuses a null argument anywhere. Generation refuses a map too small to hold a room, which is
 * the DungeonGenerator's rule rather than this one.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SadRogue.Primitives;

namespace RogueTutorial;

internal sealed class GameWorld
{
    // How far the player can see, in cells. Large enough to take in a room, small enough that a
    // corridor stays dark ahead of you.
    private const int PlayerSightRadius = 8;

    // Everything standing in the dungeon. The order here is not draw order: RenderLayer decides
    // that, so a monster is never hidden by the item it is standing on.
    private readonly List<Entity> _entities;

    /// <summary>The dungeon floor. Replaced wholesale by Descend.</summary>
    public GameMap Map { get; private set; }

    /// <summary>What the player can see now and what they remember.</summary>
    public VisibilityMap Visibility { get; private set; }

    /// <summary>The entity the keyboard drives. Always present in Entities.</summary>
    public Entity Player { get; }

    /// <summary>Everything standing in the dungeon, the player included.</summary>
    public IReadOnlyList<Entity> Entities => _entities;

    /// <summary>
    /// Which floor this is, counting from one. Deeper floors carry worse monsters, and the
    /// number is what the tables are asked with.
    /// </summary>
    public int Depth { get; private set; } = 1;

    /// <summary>True when the player is standing on the way down.</summary>
    public bool IsPlayerOnStairs => Map.GetTile(Player.Position).Equals(TileTypes.DownStairs);

    /// <summary>What has happened lately, drawn under the map.</summary>
    public MessageLog Log { get; } = new MessageLog(capacity: 100);

    /// <summary>
    /// What the player is doing, which decides what their keys mean. Held here rather than on
    /// the screen class, so a test can open the pack and press a letter without a window.
    /// </summary>
    public GameMode Mode { get; private set; } = GameMode.Playing;

    /// <summary>
    /// What is being aimed, or null when nothing is. Non-null exactly while the mode is
    /// Targeting, which is asserted on every transition rather than merely intended.
    /// </summary>
    public Targeting? Aiming { get; private set; }

    /// <summary>
    /// True once the player has been killed. Nothing stops the game yet; Part 10 decides what
    /// happens next, and until then the player simply stops being able to act.
    /// </summary>
    public bool IsPlayerDead => Player.Fighter is null;

    /// <summary>
    /// Builds a world directly from its parts. Generate is the usual way in; this constructor
    /// exists so a test can hand-build a small world with exactly the monsters it cares about.
    /// Throws ArgumentNullException on a null argument, and ArgumentException when the player is
    /// not one of the entities - it must be drawn and moved like any other - or has no Fighter,
    /// since a player who cannot fight would read as already dead.
    /// </summary>
    public GameWorld(GameMap map, IReadOnlyList<Entity> entities, Entity player)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(player);

        // A player outside the entity list would be invisible and would not block anything.
        if (!entities.Contains(player))
        {
            throw new ArgumentException("The player must be one of the entities.", nameof(player));
        }

        // IsPlayerDead reads the Fighter being gone as death, so a player who never had one
        // would start the game already dead. Requiring it here keeps that reading honest.
        if (player.Fighter is null)
        {
            throw new ArgumentException("The player must have a Fighter.", nameof(player));
        }

        Map = map;
        Player = player;
        _entities = entities.ToList();

        Visibility = new VisibilityMap(map.Width, map.Height);

        // Sight is computed before anything is drawn, or the first frame would be blank.
        RecomputeFieldOfView();
    }

    /// <summary>
    /// Generates a dungeon, places the player in the first room and monsters in the rest, and
    /// returns the world that results. Every random choice is drawn from the supplied Random, so
    /// one seed reproduces the whole world - dungeon and monsters alike. The depth decides what
    /// the tables are allowed to place. Throws ArgumentNullException on a null argument and
    /// ArgumentOutOfRangeException on a depth below one.
    /// </summary>
    public static GameWorld Generate(
        int width, int height, Random random, MonsterTable monsters, ItemTable items, int depth)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(items);

        // Floors count from one. A zero or negative depth would read as a valid table query.
        if (depth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), depth, "The first floor is depth one.");
        }

        DungeonSettings settings = new DungeonSettings(maximumRooms: 30, minimumRoomSize: 6, maximumRoomSize: 10);

        GeneratedDungeon dungeon = new DungeonGenerator(settings).Generate(width, height, random);

        Entity player = new Entity(
            "Player", '@', Color.White, dungeon.PlayerStart, blocksMovement: true, RenderLayer.Player);

        // The player's numbers: enough health to survive a mistake, enough defence that a rat
        // is an inconvenience rather than a threat.
        player.Fighter = new Fighter(maximumHitPoints: 30, attack: 5, defence: 2, experienceAwarded: 0);

        // Only the player collects experience; monsters award it.
        player.Level = new Level();

        // Twenty-six slots, because items are chosen by letter and there are twenty-six letters.
        player.Inventory = new Inventory(capacity: 26);

        // Empty to start: what the player finds is what they wear.
        player.Equipment = new Equipment();

        List<Entity> entities = PopulateRooms(dungeon, player, random, monsters, items, depth);

        return new GameWorld(dungeon.Map, entities, player) { Depth = depth };
    }

    /// <summary>
    /// Puts an item on, or takes it off if it is already worn. Whatever it displaces goes back to
    /// the pack, which always has room because the item being equipped just left it. Returns true
    /// because either way the turn is spent. Throws ArgumentException on something that is not
    /// equipment, which UseItem is what rules out.
    /// </summary>
    private bool ToggleEquipped(Entity item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Player.Equipment is null)
        {
            return false;
        }

        if (Player.Equipment.IsEquipped(item))
        {
            Player.Equipment.Unequip(item.Equippable!.Slot);

            Log.Add($"You take off the {item.Name}.");

            RunMonsterTurns();

            return true;
        }

        Entity? displaced = Player.Equipment.Equip(item);

        Log.Add($"You equip the {item.Name}.");

        // Both stay in the pack: equipping is not carrying it differently, only using it.
        if (displaced is not null)
        {
            Log.Add($"You put away the {displaced.Name}.");
        }

        RunMonsterTurns();

        return true;
    }

    /// <summary>
    /// Puts the world back on the floor a save recorded. Only SaveGame needs this: every other
    /// way to reach floor five is to walk down to it. Throws ArgumentOutOfRangeException below
    /// floor one, which is not a floor.
    /// </summary>
    public void RestoreDepth(int depth)
    {
        if (depth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(depth), depth, "The first floor is depth one.");
        }

        Depth = depth;
    }

    /// <summary>
    /// Replaces the floor with the next one down, keeping the player exactly as they are -
    /// health, experience, level and pack all carry over, because the descent is a commitment
    /// rather than a rest. The floor left behind is discarded: there is no way back up.
    ///
    /// Returns false when the player is not standing on the stairs, which is a miss rather than
    /// an error - they pressed the key in the wrong place. Throws ArgumentNullException on a
    /// null argument.
    /// </summary>
    public bool Descend(Random random, MonsterTable monsters, ItemTable items)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(items);

        // Pressing the key anywhere else is a miss, not a mistake worth an exception.
        if (!IsPlayerOnStairs)
        {
            return false;
        }

        // A dead player does not get to leave the floor they died on.
        if (IsPlayerDead)
        {
            return false;
        }

        Depth++;

        DungeonSettings settings = new DungeonSettings(
            maximumRooms: 30, minimumRoomSize: 6, maximumRoomSize: 10);

        GeneratedDungeon dungeon = new DungeonGenerator(settings).Generate(Map.Width, Map.Height, random);

        Player.MoveTo(dungeon.PlayerStart);

        Map = dungeon.Map;

        // Memory belongs to a floor. Carrying it over would show the new map already explored.
        Visibility = new VisibilityMap(Map.Width, Map.Height);

        _entities.Clear();
        _entities.AddRange(PopulateRooms(dungeon, Player, random, monsters, items, Depth));

        RecomputeFieldOfView();

        Log.Add($"You descend to floor {Depth}.");

        return true;
    }

    // The player plus whatever the tables put in every room after the first. The first room is
    // where the player starts, so it is left empty: waking up already surrounded is not a fair
    // opening.
    private static List<Entity> PopulateRooms(
        GeneratedDungeon dungeon, Entity player, Random random,
        MonsterTable monsters, ItemTable items, int depth)
    {
        List<Entity> entities = new List<Entity> { player };

        for (int roomIndex = 1; roomIndex < dungeon.Rooms.Count; roomIndex++)
        {
            entities.AddRange(monsters.PopulateRoom(dungeon.Rooms[roomIndex], dungeon.Map, random, depth));

            entities.AddRange(items.PopulateRoom(dungeon.Rooms[roomIndex], dungeon.Map, random, depth));
        }

        return entities;
    }

    /// <summary>
    /// The entity blocking the given cell, or null when nothing does. Items lying on the floor
    /// are not blockers and are never returned here.
    /// </summary>
    public Entity? BlockingEntityAt(Point position)
    {
        foreach (Entity entity in _entities)
        {
            if (entity.BlocksMovement && entity.Position == position)
            {
                return entity;
            }
        }

        return null;
    }

    /// <summary>
    /// Moves the player by the offset and reports what happened. A step onto open floor moves
    /// them and recomputes sight; walking into a creature is a bump, which will become an attack
    /// in Part 6 and for now simply does not move them; a wall refuses the move outright.
    /// </summary>
    public PlayerAction MovePlayer(Point offset)
    {
        // A zero offset is not a turn: no key that means "wait" exists yet.
        if (offset == Point.Zero)
        {
            return PlayerAction.None;
        }

        // A dead player has no turns left to take.
        if (IsPlayerDead)
        {
            return PlayerAction.None;
        }

        Point destination = Player.Position + offset;

        // The map decides first. Bumping a monster standing inside a wall is not a thing.
        if (!Map.IsWalkable(destination))
        {
            return PlayerAction.BlockedByWall;
        }

        // Walking into a creature is the attack command; there is no separate key for it.
        Entity? blocker = BlockingEntityAt(destination);
        if (blocker is not null)
        {
            // Something that blocks but cannot fight - a future statue, say - is simply in the
            // way, and swinging at it would produce a message about hitting furniture.
            if (blocker.Fighter is null)
            {
                return PlayerAction.BumpedInto(blocker);
            }

            Log.Add(Combat.Resolve(Player, blocker).Message);

            // Attacking spends the turn, so the monsters get theirs.
            RunMonsterTurns();

            OfferLevelUpIfEarned();

            return PlayerAction.Attacked(blocker);
        }

        Player.MoveTo(destination);

        // Sight is recomputed from the new position before anything is drawn, or the player
        // would see one frame of the view from where they used to stand.
        RecomputeFieldOfView();

        // Moving spends the turn too. Everything the monsters do happens after the player acts.
        RunMonsterTurns();

        return PlayerAction.Moved;
    }

    /// <summary>
    /// Gives every living monster one turn, in the order they appear in the entity list. The
    /// list is snapshotted first because a monster may die during the round, and dead ones are
    /// skipped rather than removed.
    /// </summary>
    /// <summary>
    /// Opens the level up menu if one has been earned. Called after anything that could have
    /// killed something, so the choice arrives on the turn it was paid for rather than whenever
    /// the player next happens to look.
    /// </summary>
    private void OfferLevelUpIfEarned()
    {
        if (IsPlayerDead || Player.Level is null || !Player.Level.CanAdvance)
        {
            return;
        }

        Log.Add($"You have earned level {Player.Level.CurrentLevel + 1}.");

        Mode = GameMode.ChoosingLevelUp;
    }

    /// <summary>
    /// Spends an earned level on one of the three improvements and returns to play. A slot that
    /// is not one of them is a miss rather than an error - the player pressed a letter that is
    /// not on the menu. Returns true when a level was actually spent.
    /// </summary>
    public bool ChooseLevelUp(int slot)
    {
        if (Player.Level is null || !Player.Level.CanAdvance)
        {
            return false;
        }

        if (slot < 0 || slot >= LevelUpChoices.All.Count)
        {
            return false;
        }

        Log.Add(LevelUpChoices.Apply(LevelUpChoices.All[slot], Player.Fighter!));

        Player.Level.Advance();

        // A second level may have been earned by the same kill, so the menu reopens rather than
        // dropping to the map with an unspent level in hand.
        Mode = GameMode.Playing;

        OfferLevelUpIfEarned();

        return true;
    }

    private void RunMonsterTurns()
    {
        // A dead player takes no more turns, and neither should anything else - the game is over
        // in every sense that matters until Part 10 says what happens next.
        if (IsPlayerDead)
        {
            return;
        }

        // Snapshotting is what makes it safe for a monster to die mid-round: Die converts an
        // entity in place, and this loop must not care.
        foreach (Entity entity in _entities.ToList())
        {
            // The player is not a monster, and a corpse does not act.
            if (entity == Player || entity.Fighter is null)
            {
                continue;
            }

            string? message = MonsterTurn.Act(entity, this);

            if (message is not null)
            {
                Log.Add(message);
            }

            // The player dying ends the round immediately rather than letting the rest pile on.
            if (IsPlayerDead)
            {
                Log.Add("You die.");

                // Nothing beyond this point in the round matters, and the run is over: Part 10
                // deletes the save here so a death cannot be undone by reloading.
                return;
            }
        }
    }

    /// <summary>
    /// Fills in what the player remembers, for a world rebuilt from a save. What is visible is
    /// recomputed immediately afterwards, so memory and sight cannot disagree with the map.
    /// Throws ArgumentException when the list is not one entry per cell.
    /// </summary>
    public void RestoreMemory(IReadOnlyList<bool> remembered)
    {
        Visibility.RestoreMemory(remembered);

        RecomputeFieldOfView();
    }

    /// <summary>
    /// Opens or closes the pack. Costs no turn: looking at what you are carrying is not an
    /// action, and monsters do not get a move while a menu is open.
    /// </summary>
    public void SetMode(GameMode mode)
    {
        // Targeting carries state, so it is entered by reading a scroll rather than by asking.
        if (mode == GameMode.Targeting)
        {
            throw new ArgumentException("Targeting is entered by using a scroll, not by SetMode.", nameof(mode));
        }

        // A level up is earned rather than requested, and leaving it by asking would let the
        // player walk away from a decision they have already paid for.
        if (mode == GameMode.ChoosingLevelUp)
        {
            throw new ArgumentException("A level up is offered when it is earned, not by SetMode.", nameof(mode));
        }

        Aiming = null;
        Mode = mode;

        Debug.Assert(
            (Mode == GameMode.Targeting) == (Aiming is not null),
            "Something is being aimed exactly when the mode is Targeting.");
    }

    /// <summary>
    /// Picks up whatever item is lying on the player's cell. Reports what happened through the
    /// log: there may be nothing there, or the pack may be full, and both are ordinary outcomes
    /// rather than errors. Picking something up spends a turn; finding nothing does not.
    /// </summary>
    public bool PickUpHere()
    {
        if (IsPlayerDead || Player.Inventory is null)
        {
            return false;
        }

        // The first item on this cell, ignoring creatures and the player themselves.
        Entity? item = _entities.FirstOrDefault(
            entity => entity != Player && entity.IsCarryable && entity.Position == Player.Position);

        if (item is null)
        {
            Log.Add("There is nothing here to pick up.");
            return false;
        }

        if (!Player.Inventory.TryAdd(item))
        {
            Log.Add("Your pack is full.");
            return false;
        }

        // Carried items leave the map, so they stop being drawn and stop being picked up twice.
        _entities.Remove(item);

        Log.Add($"You pick up the {item.Name}.");

        RunMonsterTurns();

        return true;
    }

    /// <summary>
    /// Uses whatever is in the given slot. An empty slot is a miss rather than an error - the
    /// player pressed a letter for something they are not carrying. An item that would do nothing
    /// is not consumed and no turn is spent.
    /// </summary>
    public bool UseItem(int slot)
    {
        if (IsPlayerDead || Player.Inventory is null)
        {
            return false;
        }

        Entity? item = Player.Inventory.At(slot);

        if (item is null)
        {
            return false;
        }

        // Equipment has no "use": choosing it from the pack puts it on, or takes it off if it is
        // already on. One key does both, because a separate wear key would need a separate list.
        if (item.Equippable is not null)
        {
            return ToggleEquipped(item);
        }

        if (item.Consumable is null)
        {
            return false;
        }

        // A scroll needs somewhere to point. Rather than using it here, the game changes mode and
        // waits; the item stays in the pack until the shot is confirmed, so cancelling loses
        // nothing.
        if (item.Consumable.NeedsTarget)
        {
            BeginTargeting(item, slot);
            return false;
        }

        UseResult result = item.Consumable.UseOn(Player);

        Log.Add(result.Message);

        // An item that changed nothing stays in the pack, and the turn is not spent either.
        if (!result.Consumed)
        {
            return false;
        }

        Player.Inventory.Remove(item);

        RunMonsterTurns();

        return true;
    }

    /// <summary>
    /// Starts aiming a scroll from the given slot. The cursor begins on the nearest visible
    /// creature if there is one, and on the player otherwise - aiming almost always means aiming
    /// at something, and starting on empty floor makes the common case slower.
    /// </summary>
    private void BeginTargeting(Entity scroll, int slot)
    {
        Aiming = new Targeting(scroll, slot, NearestVisibleTarget(), scroll.Consumable!.Radius);

        Mode = GameMode.Targeting;

        Log.Add($"Aiming the {scroll.Name}. Move to aim, Enter to fire, Esc to cancel.");
    }

    // The closest creature the player can see, or the player's own cell when there is none.
    private Point NearestVisibleTarget()
    {
        Entity? nearest = null;
        int nearestDistance = int.MaxValue;

        foreach (Entity entity in _entities)
        {
            if (entity == Player || entity.Fighter is null)
            {
                continue;
            }

            if (Visibility.StateAt(entity.Position) != CellVisibility.Visible)
            {
                continue;
            }

            int distance = Math.Max(
                Math.Abs(entity.Position.X - Player.Position.X),
                Math.Abs(entity.Position.Y - Player.Position.Y));

            if (distance < nearestDistance)
            {
                nearest = entity;
                nearestDistance = distance;
            }
        }

        return nearest?.Position ?? Player.Position;
    }

    /// <summary>
    /// Moves the aiming cursor. Does nothing when not aiming, which is what makes a stray key
    /// press harmless rather than an exception.
    /// </summary>
    public void MoveCursor(Point offset)
    {
        Aiming?.MoveCursor(offset, Map);
    }

    /// <summary>
    /// Fires the scroll being aimed at wherever the cursor is. A shot that finds nothing leaves
    /// the scroll in the pack and returns the player to it, so a miss costs the turn rather than
    /// the item. Returns true when the scroll was spent.
    /// </summary>
    public bool ConfirmTarget()
    {
        if (Aiming is null)
        {
            return false;
        }

        Targeting aiming = Aiming;

        UseResult result = aiming.Scroll.Consumable!.UseAt(Player, aiming.Cursor, this);

        Log.Add(result.Message);

        if (!result.Consumed)
        {
            // Back to the pack, not to the map: the player has not put the scroll away.
            CancelTarget();
            return false;
        }

        Player.Inventory!.Remove(aiming.Scroll);

        Aiming = null;
        Mode = GameMode.Playing;

        // A fireball can kill the reader, and a dead player takes no more turns.
        if (!IsPlayerDead)
        {
            RunMonsterTurns();
        }

        OfferLevelUpIfEarned();

        return true;
    }

    /// <summary>
    /// Gives up aiming and returns to the pack, where the scroll still is. Costs no turn: the
    /// player has done nothing but look.
    /// </summary>
    public void CancelTarget()
    {
        Aiming = null;
        Mode = GameMode.ShowingInventory;
    }

    /// <summary>
    /// Drops whatever is in the given slot onto the player's cell. An empty slot is a miss.
    /// Dropping spends a turn, which is what makes a full pack a real decision in a fight.
    /// </summary>
    public bool DropItem(int slot)
    {
        if (IsPlayerDead || Player.Inventory is null)
        {
            return false;
        }

        Entity? item = Player.Inventory.At(slot);

        if (item is null)
        {
            return false;
        }

        // Dropping something you are wearing takes it off first, or it would go on lying on
        // the floor still adding its bonus.
        if (Player.Equipment is not null && Player.Equipment.IsEquipped(item))
        {
            Player.Equipment.Unequip(item.Equippable!.Slot);

            Log.Add($"You take off the {item.Name}.");
        }

        Player.Inventory.Remove(item);

        // Back onto the map, where the player stands, so it can be picked up again.
        item.MoveTo(Player.Position);

        // RenderLayer decides what covers what, so where this lands in the list does not.
        _entities.Add(item);

        Log.Add($"You drop the {item.Name}.");

        RunMonsterTurns();

        return true;
    }

    /// <summary>
    /// Builds the picture the player currently perceives: lit where they can see, dim where they
    /// only remember, blank where they have never been.
    /// </summary>
    public RenderedFrame ComposeFrame()
    {
        return FrameComposer.Compose(Map, _entities, Visibility);
    }

    // Works out what the player can see from where they now stand, and folds it into memory.
    private void RecomputeFieldOfView()
    {
        Visibility.Update(FieldOfView.From(Player.Position, PlayerSightRadius, Map));

        // The player standing somewhere they cannot see would mean sight itself is broken.
        Debug.Assert(
            Visibility.StateAt(Player.Position) == CellVisibility.Visible,
            "The player must always be able to see their own cell.");
    }
}

/*
 * What a key press means, worked out before anything acts on it.
 *
 * Until Part 8 there was one kind of input: a movement key that spent a turn. Now the same key
 * means different things depending on what the player is doing - 'd' walks nowhere on the map
 * and picks slot four in the inventory - so the meaning has to be decided somewhere, and it must
 * not be RootScreen, which no test can construct.
 *
 * Part 9's targeting cursor and Part 10's prompts need exactly this machinery, which is why it
 * is a type rather than a couple of branches inside the keyboard handler.
 *
 * Usage:
 *
 *     GameCommand command = CommandReader.Read(keys, world.Mode);
 *
 *     if (command.Kind == GameCommandKind.Move)  { world.MovePlayer(command.Offset); }
 *     if (command.Kind == GameCommandKind.UseItem) { world.UseItem(command.Slot); }
 *
 * Offset is meaningful only for Move, and Slot only for UseItem and DropItem. Nothing else
 * carries either.
 */

using SadRogue.Primitives;

namespace RogueTutorial;

/// <summary>What the player is doing, which decides what their keys mean.</summary>
internal enum GameMode
{
    /// <summary>Walking the dungeon. Movement keys move, and everything costs a turn.</summary>
    Playing,

    /// <summary>The pack is open. Letters choose an item and Escape closes it.</summary>
    ShowingInventory,

    /// <summary>
    /// A level has been earned and is waiting to be spent. The game does not continue until it
    /// is: an unspent level is a decision the player has already paid for.
    /// </summary>
    ChoosingLevelUp,

    /// <summary>
    /// The player has asked to abandon this run. One key confirms and anything else does not,
    /// because a stray press should never be able to destroy a game somebody is winning.
    /// </summary>
    ConfirmingNewGame,

    /// <summary>
    /// A scroll is being aimed. Movement keys move the cursor, Enter fires, Escape goes back to
    /// the pack rather than to the map - the scroll has not been used yet, so the player is
    /// still standing in their inventory as far as they are concerned.
    /// </summary>
    Targeting,
}

/// <summary>The kinds of thing a key press can mean.</summary>
internal enum GameCommandKind
{
    /// <summary>The key means nothing in this mode. Nothing happens and no turn is spent.</summary>
    None,

    /// <summary>Walk or attack in a direction.</summary>
    Move,

    /// <summary>Pick up whatever is underfoot.</summary>
    PickUp,

    /// <summary>Open the pack.</summary>
    OpenInventory,

    /// <summary>Close the pack without doing anything.</summary>
    CloseInventory,

    /// <summary>Use the item in a slot.</summary>
    UseItem,

    /// <summary>Drop the item in a slot.</summary>
    DropItem,

    /// <summary>Move the aiming cursor.</summary>
    MoveCursor,

    /// <summary>Fire the scroll at wherever the cursor is.</summary>
    ConfirmTarget,

    /// <summary>Give up aiming and go back to the pack.</summary>
    CancelTarget,

    /// <summary>Ask to abandon this run and start another.</summary>
    AskNewGame,

    /// <summary>Confirm it: the save is deleted and a fresh dungeon generated.</summary>
    ConfirmNewGame,

    /// <summary>Think better of it.</summary>
    CancelNewGame,

    /// <summary>Spend an earned level on one of the three improvements.</summary>
    ChooseLevelUp,
}

internal readonly struct GameCommand
{
    /// <summary>What the key meant.</summary>
    public GameCommandKind Kind { get; }

    /// <summary>Which way to move. Point.Zero for every kind but Move.</summary>
    public Point Offset { get; }

    /// <summary>Which pack slot. Minus one for every kind but UseItem and DropItem.</summary>
    public int Slot { get; }

    private GameCommand(GameCommandKind kind, Point offset, int slot)
    {
        Kind = kind;
        Offset = offset;
        Slot = slot;
    }

    /// <summary>The key meant nothing in this mode.</summary>
    public static GameCommand None => new GameCommand(GameCommandKind.None, Point.Zero, -1);

    /// <summary>Walk or attack in a direction.</summary>
    public static GameCommand Move(Point offset) => new GameCommand(GameCommandKind.Move, offset, -1);

    /// <summary>Pick up whatever is underfoot.</summary>
    public static GameCommand PickUp => new GameCommand(GameCommandKind.PickUp, Point.Zero, -1);

    /// <summary>Open the pack.</summary>
    public static GameCommand OpenInventory => new GameCommand(GameCommandKind.OpenInventory, Point.Zero, -1);

    /// <summary>Close the pack.</summary>
    public static GameCommand CloseInventory => new GameCommand(GameCommandKind.CloseInventory, Point.Zero, -1);

    /// <summary>Use what is in a slot.</summary>
    public static GameCommand UseItem(int slot) => new GameCommand(GameCommandKind.UseItem, Point.Zero, slot);

    /// <summary>Drop what is in a slot.</summary>
    public static GameCommand DropItem(int slot) => new GameCommand(GameCommandKind.DropItem, Point.Zero, slot);

    /// <summary>Move the aiming cursor by one step.</summary>
    public static GameCommand MoveCursor(Point offset) => new GameCommand(GameCommandKind.MoveCursor, offset, -1);

    /// <summary>Fire at wherever the cursor is.</summary>
    public static GameCommand ConfirmTarget => new GameCommand(GameCommandKind.ConfirmTarget, Point.Zero, -1);

    /// <summary>Give up aiming.</summary>
    public static GameCommand CancelTarget => new GameCommand(GameCommandKind.CancelTarget, Point.Zero, -1);

    /// <summary>Ask to abandon this run.</summary>
    public static GameCommand AskNewGame => new GameCommand(GameCommandKind.AskNewGame, Point.Zero, -1);

    /// <summary>Confirm abandoning it.</summary>
    public static GameCommand ConfirmNewGame => new GameCommand(GameCommandKind.ConfirmNewGame, Point.Zero, -1);

    /// <summary>Think better of it.</summary>
    public static GameCommand CancelNewGame => new GameCommand(GameCommandKind.CancelNewGame, Point.Zero, -1);

    /// <summary>Spend an earned level. The slot is which of the three was chosen.</summary>
    public static GameCommand ChooseLevelUp(int slot) => new GameCommand(GameCommandKind.ChooseLevelUp, Point.Zero, slot);
}

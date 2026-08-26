/*
 * Turns the keys held this frame into one command, given what the player is doing.
 *
 * This replaces MovementKeys as the entry point for input. Movement is still a table lookup and
 * still lives there; what is new is that the same key means different things in different modes,
 * so something has to decide which meaning applies.
 *
 * Usage:
 *
 *     GameCommand walk = CommandReader.Read(new[] { Keys.Left }, GameMode.Playing);
 *     // -> Move, offset (-1, 0)
 *
 *     GameCommand pick = CommandReader.Read(new[] { Keys.A }, GameMode.ShowingInventory);
 *     // -> UseItem, slot 0: 'a' is the first slot, 'b' the second, and so on
 *
 *     GameCommand nothing = CommandReader.Read(new[] { Keys.Left }, GameMode.ShowingInventory);
 *     // -> None: the map does not move while the pack is open
 *
 *     GameCommand aim = CommandReader.Read(new[] { Keys.Left }, GameMode.Targeting);
 *     // -> MoveCursor: the same key moves the crosshair instead of the player
 *
 * Refuses a null key collection. Holding shift with a letter drops rather than uses, which is
 * why the shift state is a separate argument rather than being read from the letter.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using SadConsole.Input;
using SadRogue.Primitives;

namespace RogueTutorial;

internal static class CommandReader
{
    /// <summary>
    /// Works out what the keys mean in the given mode. Throws ArgumentNullException on a null
    /// collection. A key with no meaning in this mode yields None, which costs no turn.
    /// </summary>
    public static GameCommand Read(IReadOnlyCollection<Keys> pressedKeys, GameMode mode, bool shiftHeld)
    {
        ArgumentNullException.ThrowIfNull(pressedKeys);

        return mode switch
        {
            GameMode.Playing => ReadPlaying(pressedKeys, shiftHeld),
            GameMode.ShowingInventory => ReadInventory(pressedKeys, shiftHeld),
            GameMode.Targeting => ReadTargeting(pressedKeys),
            GameMode.ConfirmingNewGame => ReadConfirmation(pressedKeys),
            GameMode.ChoosingLevelUp => ReadLevelUp(pressedKeys),
            _ => GameCommand.None,
        };
    }

    /// <summary>
    /// The convenience form for callers that never hold shift, which is every test about
    /// movement. Equivalent to Read with shiftHeld false.
    /// </summary>
    public static GameCommand Read(IReadOnlyCollection<Keys> pressedKeys, GameMode mode)
    {
        return Read(pressedKeys, mode, shiftHeld: false);
    }

    // Walking the dungeon: movement, picking up, and opening the pack.
    private static GameCommand ReadPlaying(IReadOnlyCollection<Keys> pressedKeys, bool shiftHeld)
    {
        // Movement first, because it is what almost every key press is.
        Point offset = MovementKeys.OffsetFor(pressedKeys);

        if (offset != Point.Zero)
        {
            return GameCommand.Move(offset);
        }

        // 'g' for get, which is the roguelike convention and leaves 'p' free.
        if (pressedKeys.Contains(Keys.G))
        {
            return GameCommand.PickUp;
        }

        if (pressedKeys.Contains(Keys.I))
        {
            return GameCommand.OpenInventory;
        }

        // '>' is shift and the period key, which is where the glyph is printed on the keycap
        // and what every roguelike uses for going down.
        if (shiftHeld && pressedKeys.Contains(Keys.OemPeriod))
        {
            return GameCommand.Descend;
        }

        // Abandoning a run is still here, for a player who wants to stop rather than go deeper.
        if (pressedKeys.Contains(Keys.N))
        {
            return GameCommand.AskNewGame;
        }

        return GameCommand.None;
    }

    // Choosing a level up: a letter picks one of the three, and nothing else applies. There is
    // deliberately no way out - the level is earned, and leaving it unspent would mean carrying
    // a menu around while monsters take turns.
    private static GameCommand ReadLevelUp(IReadOnlyCollection<Keys> pressedKeys)
    {
        foreach (Keys key in pressedKeys)
        {
            if (key < Keys.A || key > Keys.Z)
            {
                continue;
            }

            return GameCommand.ChooseLevelUp(key - Keys.A);
        }

        return GameCommand.None;
    }

    // Confirming: one key means yes and everything else means no, which is the safe way round
    // for a question whose yes destroys a run.
    private static GameCommand ReadConfirmation(IReadOnlyCollection<Keys> pressedKeys)
    {
        if (pressedKeys.Contains(Keys.Y))
        {
            return GameCommand.ConfirmNewGame;
        }

        // Any other key backs out. A player who has second thoughts should not have to find the
        // one correct way to say no.
        if (pressedKeys.Count > 0)
        {
            return GameCommand.CancelNewGame;
        }

        return GameCommand.None;
    }

    // Aiming: the movement keys move the cursor instead of the player, and two keys resolve it.
    private static GameCommand ReadTargeting(IReadOnlyCollection<Keys> pressedKeys)
    {
        // Escape first, so a player who panics gets out rather than firing.
        if (pressedKeys.Contains(Keys.Escape))
        {
            return GameCommand.CancelTarget;
        }

        if (pressedKeys.Contains(Keys.Enter))
        {
            return GameCommand.ConfirmTarget;
        }

        // The same table the player walks with, so aiming needs no new keys to learn.
        Point offset = MovementKeys.OffsetFor(pressedKeys);

        if (offset != Point.Zero)
        {
            return GameCommand.MoveCursor(offset);
        }

        return GameCommand.None;
    }

    // The pack is open: letters choose a slot, Escape closes, and nothing else applies.
    private static GameCommand ReadInventory(IReadOnlyCollection<Keys> pressedKeys, bool shiftHeld)
    {
        if (pressedKeys.Contains(Keys.Escape) || pressedKeys.Contains(Keys.I))
        {
            return GameCommand.CloseInventory;
        }

        foreach (Keys key in pressedKeys)
        {
            // A to Z are contiguous in the key enum, so the letter's distance from A is the slot.
            if (key < Keys.A || key > Keys.Z)
            {
                continue;
            }

            int slot = key - Keys.A;

            // Shift turns choosing into dropping, so one set of letters covers both.
            return shiftHeld ? GameCommand.DropItem(slot) : GameCommand.UseItem(slot);
        }

        return GameCommand.None;
    }
}

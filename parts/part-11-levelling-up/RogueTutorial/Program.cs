/*
 * Entry point for the tutorial game.
 *
 * Usage - this is the executable, so there is nothing to construct. Run it with:
 *
 *     dotnet run --project RogueTutorial
 *
 * A window opens showing an '@' on a black grid. The arrow keys or the numeric
 * keypad move it. Escape closes the window.
 *
 * The three constants below are the whole of the game's screen contract: the
 * grid is measured in character cells, not pixels, and every other file assumes
 * these dimensions.
 */

using SadConsole;
using SadConsole.Configuration;
using RogueTutorial;

// Width of the game grid, in character cells.
const int ScreenWidthInCells = 80;

// Height of the game grid, in character cells.
const int ScreenHeightInCells = 25;

// Text shown in the operating system's window title bar.
const string WindowTitle = "Roguelike Tutorial - Part 11: Levelling up";

// Describe the game to SadConsole: window size, title, and what to show first.
Builder gameStartup = new Builder()
    .SetWindowSizeInCells(ScreenWidthInCells, ScreenHeightInCells)
    .SetStartingScreen<RootScreen>()
    .IsStartingScreenFocused(true)
    .ConfigureFonts(true);

// Set the title separately; it is a global setting rather than part of the builder.
Settings.WindowTitle = WindowTitle;

// Open the window and run the game loop. This blocks until the window closes.
Game.Create(gameStartup);
Game.Instance.Run();

// Release the graphics device. Skipping this leaves the process alive on some drivers.
Game.Instance.Dispose();

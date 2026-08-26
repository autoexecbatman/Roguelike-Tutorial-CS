# Roguelike Tutorial in C#

A 13-part roguelike built on [SadConsole](https://github.com/Thraka/SadConsole) 10, following
the structure of the r/roguelikedev Complete Roguelike Tutorial.

## Running it

```
cd parts/part-01-drawing-and-moving
dotnet run --project RogueTutorial
```

An 80x25 grid opens with an `@` at the centre. Arrow keys or the numeric keypad move it;
keypad corners give diagonals; the edges of the grid stop movement rather than wrapping.

## Running the tests

```
dotnet test                                  # everything, including the window-opening test
dotnet test --filter "Category!=EndToEnd"    # unit and integration only, no window
```

## How the code is arranged

The movement rules are deliberately kept out of the SadConsole screen class, because
anything touching `Game.Instance` cannot be constructed without a live graphics host and
therefore cannot be unit tested.

| Type | Responsibility | Testable without a window |
|---|---|---|
| `GridBounds` | The rectangle the player may stand on, and clamping to it | yes |
| `PlayerMover` | The player's position and the only way it changes | yes |
| `MovementKeys` | Key to offset table, including diagonals | yes |
| `RootScreen` | Wires the window and keyboard to the three above, and draws | no |

## API reference

See [the generated API documentation](api/index.md).

# Roguelike Tutorial in C#

Build a roguelike from an empty folder, in C#, using [SadConsole](https://github.com/Thraka/SadConsole).
Each part is small, ends with something you can run, and explains why the code is shaped the
way it is rather than only what to type.

This follows the structure of the r/roguelikedev *Complete Roguelike Tutorial*, which is the
version most roguelike tutorials now use, ported to C#.

## What you need

- **.NET SDK 9.0 or newer.** Check with `dotnet --version`. Get it from
  [dotnet.microsoft.com](https://dotnet.microsoft.com/download).
- **An editor.** Visual Studio, VS Code with the C# Dev Kit extension, or Rider. Any of them
  work; nothing here depends on a particular one.
- **Some programming experience.** You do not need to know C#. Where the language does
  something a newcomer would not expect, the text says so.

You do **not** need to know graphics programming, and you will not write any.

## Run it

```
git clone https://github.com/autoexecbatman/Roguelike-Tutorial-CS
cd Roguelike-Tutorial-CS/parts/part-01-drawing-and-moving
dotnet run --project RogueTutorial
```

A window opens: an 80x25 grid with a white `@` in the middle. Arrow keys move it. The numeric
keypad also moves it, and its corner keys (7, 9, 1, 3) move diagonally. The `@` stops at the
edges of the grid.

## Run the tests

From inside a part's folder:

```
dotnet test                                  # everything
dotnet test --filter "Category!=EndToEnd"    # skip the test that opens a real window
```

Or from the repository root, which builds and tests every part at once through
`RogueTutorial-AllParts.slnx`:

```
dotnet build
dotnet test --filter "Category!=EndToEnd"
```

Use the filter at the root. Each part has an end-to-end test that launches the real game, so an
unfiltered root run opens one window per part and waits several seconds on each.

## The parts

| Part | What you build | Code |
|---|---|---|
| [1. Drawing and moving the player](docs/part-01-drawing-and-moving.md) | A window, an `@`, and eight-way movement | [code](parts/part-01-drawing-and-moving/) |
| [2. The entity class and the map](docs/part-02-entities-and-the-map.md) | Walls that stop you, a second character, and a testable picture | [code](parts/part-02-entities-and-the-map/) |
| [3. Dungeon generation](docs/part-03-dungeon-generation.md) | Random rooms joined by corridors, and how to test randomness | [code](parts/part-03-dungeon-generation/) |
| [4. Field of view](docs/part-04-field-of-view.md) | Sight, memory, and why symmetry matters | [code](parts/part-04-field-of-view/) |
| [5. Placing monsters](docs/part-05-placing-monsters.md) | Monsters that block you, and state that leaves the screen class | [code](parts/part-05-placing-monsters/) |
| [6. Combat](docs/part-06-combat.md) | Fighting as a component, monster turns, and death | [code](parts/part-06-combat/) |
| [7. Message log and health bar](docs/part-07-log-and-health-bar.md) | Dividing the window, and an interface you can assert | [code](parts/part-07-log-and-health-bar/) |
| [8. Items and inventory](docs/part-08-items-and-inventory.md) | Modal input, components for items, and a pack with a limit | [code](parts/part-08-items-and-inventory/) |
| [9. Ranged scrolls and targeting](docs/part-09-ranged-scrolls-and-targeting.md) | A mode that remembers where it came from | [code](parts/part-09-ranged-scrolls-and-targeting/) |
| 10. Save and load | Persisting the game between runs | planned |
| 11. Levelling up | Experience and character progression | planned |
| 12. Deeper levels | Monsters and loot that scale with depth | planned |
| 13. Equipment | Weapons and armour that change your numbers | planned |

## Repository layout

Every part is a **complete, runnable snapshot** in its own folder:

```
parts/part-01-drawing-and-moving/     the whole project as it stands at the end of Part 1
parts/part-02-entities-and-the-map/   the whole project as it stands at the end of Part 2
docs/part-01-drawing-and-moving.md    the walkthrough for that part
docs/part-02-entities-and-the-map.md  and for that one
parts/part-03-dungeon-generation/     ... and so on
```

So you can open the part you are on and run it, without git commands and without seeing code
from parts you have not read yet. To see exactly what a part changed, diff its folder against
the one before it:

```
git diff --no-index parts/part-01-drawing-and-moving parts/part-02-entities-and-the-map
```

**Finished parts are frozen.** They are snapshots, not a codebase kept in sync. If a bug is
found in shared code, it is fixed in the newest part and in the walkthrough text; earlier
folders keep the code the walkthrough describes, because a snapshot that has quietly drifted
from its own documentation is worse than one with a known wart.

## How this tutorial is organised

[Writing tests](docs/writing-tests.md) covers the testing approach used throughout, and is
worth reading once during Part 1.

Each part has a document in [`docs/`](docs/) that walks through the code it adds. The code
itself carries the details: every file opens with a usage block showing a real call, and every
function states what it does and what it refuses.

Two conventions run through the whole thing, and they are worth knowing before Part 1.

**Game rules are kept out of the SadConsole classes.** Anything that touches `Game.Instance`
needs a running window, so it cannot be constructed in a test. Rules that live in ordinary
classes can be tested in milliseconds without a window ever opening. Part 1 shows this
concretely, including a test that proves the screen class *cannot* be constructed headless.

**Every rule gets a test, and every test gets checked.** A test that has never failed has never
been tested. Each part says which line to break and what should go red, so you can confirm your
tests actually work rather than trusting a green run.

## API reference

Generated from the source comments with [docfx](https://dotnet.github.io/docfx/):

```
dotnet tool restore
dotnet docfx docfx.json --serve
```

Then open <http://localhost:8080>.

## Credits

- [SadConsole](https://github.com/Thraka/SadConsole) by Thraka, the console rendering library.
- The [r/roguelikedev Complete Roguelike Tutorial](https://rogueliketutorials.com/) for the
  part structure.
- [RogueBasin](https://www.roguebasin.com/) for the wider roguelike development wiki.

## Licence

MIT. See [LICENSE](LICENSE).

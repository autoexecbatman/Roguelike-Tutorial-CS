# Part 1: Drawing and moving the player

- [What it is](#what-it-is)
- [How to use it](#how-to-use-it)
- [How to set it up](#how-to-set-it-up)

---

# What it is

A window with an `@` in it that you move with the keyboard, and a test suite that checks the
movement rules without ever opening that window.

That second half is the reason this part is longer than "draw a character and move it". The
structure introduced here is the structure every later part is built on, and it is easier to
adopt now, with three rules to move, than in Part 6 with combat in the way.

## The libraries

**SadConsole** is a .NET library that draws a grid of characters and gives you a keyboard. It
is not a game engine: it has no concept of a player, a map, or a turn. You supply all of that.
What it supplies is a window measured in character cells, a font, and a per-frame callback.

**MonoGame** is the layer underneath that owns the actual window, the graphics device and the
input. You will never call it directly, but you do have to install it yourself: SadConsole's
MonoGame host is an adapter compiled against MonoGame rather than a package that contains it.

**xUnit** runs the tests. Nothing about the design depends on it; NUnit or MSTest would work
the same way.

## The files

| File | What it is |
|---|---|
| [`Program.cs`](../parts/part-01-drawing-and-moving/RogueTutorial/Program.cs) | The entry point. Describes the window, then starts the game. |
| [`RootScreen.cs`](../parts/part-01-drawing-and-moving/RogueTutorial/RootScreen.cs) | The bridge to SadConsole: owns the drawing surface, translates key presses, paints the frame. |
| [`GridBounds.cs`](../parts/part-01-drawing-and-moving/RogueTutorial/GridBounds.cs) | The rectangle the player may stand on, and the rule for staying inside it. |
| [`PlayerMover.cs`](../parts/part-01-drawing-and-moving/RogueTutorial/PlayerMover.cs) | The player's position, and the only way it is allowed to change. |
| [`MovementKeys.cs`](../parts/part-01-drawing-and-moving/RogueTutorial/MovementKeys.cs) | Which key means which direction. |

Three of those five have no idea SadConsole exists. That is deliberate, and the next section
explains why.

## The idea that shapes everything: the testability boundary

`RootScreen` cannot be constructed in a test. Its constructor asks SadConsole how big the
window is:

```csharp
_mapSurface = new ScreenSurface(Game.Instance.ScreenCellsX, Game.Instance.ScreenCellsY);
```

`Game.Instance` exists only after `Game.Create` has opened a real window. In a test process
there is no window, so there is no `Game.Instance`, so the constructor throws.

This repository contains a test that proves it:

```csharp
[Fact]
public void ConstructingRootScreenWithoutAHostThrows()
{
    Exception thrown = Record.Exception(() => new RootScreen());
    Assert.NotNull(thrown);
}
```

**That test passes.** It is not testing `RootScreen`; it exists so the constraint is written
down and stays true when someone forgets it.

The consequence is a rule for the whole tutorial: **anything that is a game rule lives outside
the SadConsole classes.** Put the movement rules on `RootScreen` and they are permanently
beyond the reach of a test. Put them in `PlayerMover` and they run in milliseconds with no
window in sight.

## What each rule class is

**`GridBounds`** answers two questions about a rectangle: is this position inside it, and what
is the nearest position that is.

```csharp
public Point Clamp(Point position)
{
    int clampedX = Math.Clamp(position.X, 0, Width - 1);
    int clampedY = Math.Clamp(position.Y, 0, Height - 1);
    ...
}
```

Note `Width - 1`. In an 80-wide grid the largest legal X is 79. Off-by-one errors live exactly
here, which is why this class has its own tests.

**`PlayerMover`** holds the position and enforces one invariant: the player is always on the
grid.

```csharp
public void Move(Point offset)
{
    Position = _bounds.Clamp(Position + offset);

    Debug.Assert(_bounds.Contains(Position), "The player must never stand outside the grid.");
}
```

Its constructor **rejects** a starting position outside the grid, while `Move` **clamps**. That
asymmetry is the point: a bad starting position is a programming error and should fail loudly
where the mistake was made; walking into a wall is something a player does every few seconds
and must not throw.

**`MovementKeys`** is a lookup table rather than a chain of twelve `if` statements:

```csharp
private static readonly IReadOnlyDictionary<Keys, Point> OffsetByKey = new Dictionary<Keys, Point>
{
    [Keys.Left]    = new Point(-1,  0),
    [Keys.Right]   = new Point( 1,  0),
    [Keys.Up]      = new Point( 0, -1),
    [Keys.Down]    = new Point( 0,  1),
    [Keys.NumPad7] = new Point(-1, -1),
    // ... the rest of the keypad
};
```

**Up is `(0, -1)`.** Y grows *downward* on a console grid, like screen coordinates and unlike
school graph paper. Getting this backwards is the most common bug in this part, which is why a
test is named `PressingUpMovesTowardTheTopOfTheScreen`.

It takes a collection of keys and sums their offsets, which buys two behaviours without any
extra code: Left and Up in the same frame make a diagonal, and Left and Right cancel out.

Taking `IReadOnlyCollection<Keys>` instead of SadConsole's `Keyboard` type is the whole trick.
A test hands it an array.

## Three C# details a newcomer will trip on

**There is no `Main` you wrote.** `Program.cs` uses *top-level statements*: in exactly one file
per project you may write statements with no enclosing class or method, and the compiler builds
the entry point for you. Inspect the compiled assembly and you find `Program.<Main>$`. The angle
brackets make the name impossible to write in C#, so it never collides with your code. Three
consequences: only one file may do this, `using` and `const` must precede the first statement,
and the type name `Program` is now taken.

**`Builder`'s methods are extension methods.** Every call in that configuration chain is a
static method living in `SadConsole.Configuration.Extensions`, not a member of `Builder`. That
is why `using SadConsole.Configuration;` is required — extension methods are invisible without
their namespace imported. If a configuration method seems to be missing, that missing `using` is
almost always the reason.

The chain also does not set properties. Each call stores a small configurator object in a list,
and `Game.Create` applies them afterwards. So **the order of the chain does not matter**, and
calling the same method twice overwrites rather than stacks.

**`readonly` pins the reference, not the object.**

```csharp
private readonly ScreenSurface _mapSurface;
```

The code writes to that surface every single frame. `readonly` restricts only *when the field
may be assigned* — the declaration or the constructor. `_mapSurface.Surface.Clear()` is fine;
`_mapSurface = new ScreenSurface(...)` outside the constructor is `error CS0191`.

It earns its place here: the constructor hands the surface to SadConsole via
`Children.Add(_mapSurface)`. If a later edit pointed the field at a different surface, the scene
tree would keep drawing the old one while your draw calls went to the new one. Nothing would
error. The screen would just silently stop updating.

## What is deliberately wrong

`RootScreen.DrawFrame` clears and repaints the whole surface on every move. That is correct for
one entity and wasteful for a map full of them. Part 2 replaces it, once there is more than one
thing to draw — introducing an entity system before there are entities means explaining
machinery with nothing to point at.

---

# How to use it

## Play it

```
cd parts/part-01-drawing-and-moving
dotnet run --project RogueTutorial
```

An 80x25 grid opens with a white `@` in the middle.

| Key | Effect |
|---|---|
| Arrow keys | Move one cell |
| Keypad 4, 8, 6, 2 | Move one cell (left, up, right, down) |
| Keypad 7, 9, 1, 3 | Move one cell diagonally |
| Close the window | Quit |

The `@` stops at the edges of the grid rather than wrapping or disappearing.

## Run the tests

```
dotnet test                                  # everything
dotnet test --filter "Category!=EndToEnd"    # skip the test that opens a real window
```

There are three levels, and each catches something the others cannot.

**Unit tests** exercise one class each: clamping at the edges, each key's offset, opposing keys
cancelling. Milliseconds, no window.

**Integration tests** compose the key table with the position rules, which is the path
`RootScreen.ProcessKeyboard` actually walks. This level catches an axis swap or a sign error,
because both halves can be individually correct and still wired together wrongly.

**The end-to-end test** launches the real executable and fails if it dies within six seconds:

```csharp
using Process game = Process.Start(startInfo);

bool exitedEarly = game.WaitForExit(TimeSpan.FromSeconds(6));

if (exitedEarly)
{
    Assert.Fail($"The game exited with {game.ExitCode}. Stderr:\n{game.StandardError.ReadToEnd()}");
}
```

This is the only level that touches assembly loading, the MonoGame host and the native SDL
libraries. It is what catches a missing dependency, and no unit test can see that.

## Prove the tests actually work

A suite that has only ever been green tells you nothing — you have tested that the tests
compile. Make each change below, run the suite, confirm it goes red, then undo it.

| Change | Expected result |
|---|---|
| `GridBounds.Clamp`: `Width - 1` becomes `Width` | 2 tests fail |
| `MovementKeys`: `[Keys.NumPad7]` becomes `new Point(-1, 0)` | 1 test fails |
| Delete `MonoGame.Framework.dll` from `bin/Debug/net9.0/` | the end-to-end test fails with `FileNotFoundException` |

If any of those stays green, that rule has no test and you have found a real gap.

Check the assertions the same way. Make `Clamp` return something outside the grid and the run
should report:

```
DebugAssertException : Method Debug.Fail failed with
'Clamp must return a position inside the grid.'
```

If it does not, your assertions are compiled out and are giving you false comfort.

## Read the API reference

```
dotnet tool restore
dotnet docfx docfx.json --serve
```

Then open <http://localhost:8080>. Every type, generated from the comments in the source.

## Extend it

Some changes that stay inside Part 1's structure, easiest first:

- Change the player's glyph or colour — `RootScreen.PlayerGlyph`, one constant.
- Add `wasd` movement — add four entries to the `MovementKeys` table, and a test for each.
- Add vi keys (`hjkl` plus `yubn`) — the same, eight entries.
- Make the grid a different size — the two constants at the top of `Program.cs`.
- Make the player wrap around the edges instead of stopping — replace `Clamp` in `PlayerMover`
  with a modulo, and watch which existing tests fail. They should; that is the suite telling you
  the behaviour changed.

---

# How to set it up

This section takes you from an empty folder to a running game. Every step says what to type,
what it does, and **what you should see** — so you find out immediately when something has gone
wrong, rather than three steps later.

If you cloned this repository you already have all of it in
`parts/part-01-drawing-and-moving/`, and you only need `dotnet run --project RogueTutorial` from
inside that folder.

## Step 0: get a terminal open in the right place

Everything below is typed at a command prompt. If you have not used one before:

- **Windows** — press the Windows key, type `powershell`, press Enter.
- **macOS** — press Cmd+Space, type `terminal`, press Enter.
- **Linux** — Ctrl+Alt+T on most desktops.

Make a folder for the project and move into it. `cd` means "change directory", and it is how
you tell the terminal which folder your commands apply to:

```
mkdir my-roguelike
cd my-roguelike
```

**Everything from here on is run from inside `my-roguelike`**, unless a step says otherwise.
If a command fails with "file not found" or "project not found", the usual cause is being in
the wrong folder. Type `pwd` (macOS/Linux) or `cd` with no arguments (Windows) to see where you
are.

## Step 1: check the .NET SDK is installed

```
dotnet --version
```

Expected: a version number, `9.0.0` or higher.

```
10.0.302
```

If instead you get "command not found" or "'dotnet' is not recognized", the SDK is not
installed or not on your PATH. Install it from
[dotnet.microsoft.com/download](https://dotnet.microsoft.com/download) — you want the **SDK**,
not the Runtime. The Runtime only runs .NET programs; the SDK is what builds them. Close and
reopen your terminal afterwards, because PATH changes do not reach a terminal that is already
open.

You will also want an editor: Visual Studio, VS Code with the C# Dev Kit extension, or Rider.
Any of them works, and nothing in this tutorial depends on which you pick.

## Step 2: create the solution and the two projects

> **You are in:** `my-roguelike`

Three commands, and it is worth knowing what each one makes.

A **project** is one thing that gets built — here, one program and one test suite. A
**solution** is a file listing several projects so your editor and the build tools can treat
them as one unit. You do not strictly need a solution for a single project, but you have two.

```
dotnet new sln --name RogueTutorial
```

```
The template "Solution File" was created successfully.
```

That creates `RogueTutorial.slnx`. Recent SDKs produce the newer `.slnx` format rather than the
old `.sln`; both work, and your editor opens either.

```
dotnet new console -n RogueTutorial -o RogueTutorial
```

`-n` is the project's name, `-o` is the folder to put it in. Expected:

```
The template "Console App" was created successfully.

Processing post-creation actions...
Restoring .../RogueTutorial/RogueTutorial.csproj:
  Determining projects to restore...
  Restored .../RogueTutorial/RogueTutorial.csproj (in 51 ms).
Restore succeeded.
```

"Restore" means downloading whatever packages the project needs. A brand-new console project
needs none, so it finishes instantly. It will matter in Step 4.

```
dotnet new xunit -n RogueTutorial.Tests -o RogueTutorial.Tests
```

Same shape of output. **xUnit** is the test framework; NUnit or MSTest would work identically
for everything in this tutorial.

Now tell the solution about both:

```
dotnet sln add RogueTutorial/RogueTutorial.csproj RogueTutorial.Tests/RogueTutorial.Tests.csproj
```

```
Project `RogueTutorial\RogueTutorial.csproj` added to the solution.
Project `RogueTutorial.Tests\RogueTutorial.Tests.csproj` added to the solution.
```

### Checkpoint: what you should have

```
my-roguelike/
  RogueTutorial.slnx
  RogueTutorial/
    Program.cs
    RogueTutorial.csproj
  RogueTutorial.Tests/
    UnitTest1.cs
    RogueTutorial.Tests.csproj
```

You will also see `bin/` and `obj/` folders appear. Those hold build output, they are
regenerated on every build, and they never belong in version control.

**Prove it works before writing a line of your own code:**

```
dotnet run --project RogueTutorial
```

```
Hello, World!
```

```
dotnet test
```

```
Passed!  - Failed: 0, Passed: 1, Skipped: 0, Total: 1
```

That one passing test is the template's placeholder. If both commands behave as above, your
toolchain is working, and any failure from here on is something you did rather than something
in your installation. That is the whole point of checking now.

Delete the placeholder, because it tests nothing:

```
RogueTutorial.Tests/UnitTest1.cs      <- delete this file
```

## Step 3: set the target framework

> **You are in:** `my-roguelike`. This step edits two files and runs no commands.

Open `my-roguelike/RogueTutorial/RogueTutorial.csproj` in your editor. It will look roughly like this, and
the framework line will say whatever your SDK defaults to — `net10.0` on a current install:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

Change that line to `net9.0` in **both** project files. They are separate files and it is easy
to change one and forget the other:

```
my-roguelike/
  RogueTutorial/RogueTutorial.csproj              <- edit this one
  RogueTutorial.Tests/RogueTutorial.Tests.csproj  <- and this one
```

```xml
<TargetFramework>net9.0</TargetFramework>
```

**Why deliberately target an older version.** Some editors resolve an older SDK than your
command line does, and then report:

```
NETSDK1209: The current Visual Studio version does not support targeting .NET 10.0.
Either target .NET 9.0 or lower, or use Visual Studio version 18.0 or higher.
```

That message blames your editor's version, which is usually not the problem — **read the file
path in the error instead**, because it names the SDK actually being used. Targeting `net9.0`
avoids the whole situation, and SadConsole ships `net8.0`, `net9.0` and `net10.0` builds, so
you lose nothing.

If you see a wall of errors like `CS0246: The type or namespace name 'SadConsole' could not be
found`, do not chase them individually. When the target framework fails to resolve, no packages
resolve either, so every type looks undefined at once. Fix the framework and they all vanish
together.

## Step 4: add the packages

> **You are in:** `my-roguelike`

These go on the **game** project, so move into it first. The final `cd ..` puts you back in
`my-roguelike`, where the next step expects you:

```
cd RogueTutorial
dotnet add package SadConsole
dotnet add package SadConsole.Host.MonoGame
dotnet add package MonoGame.Framework.DesktopGL --version 3.8.4.1
cd ..
```

Each one prints a confirmation naming the version it settled on:

```
info : PackageReference for package 'SadConsole' version '10.10.1' added to file '.../RogueTutorial.csproj'.
log  : Restored .../RogueTutorial.csproj (in 108 ms).
```

### Checkpoint

`RogueTutorial/RogueTutorial.csproj` should now contain:

```xml
<ItemGroup>
  <PackageReference Include="SadConsole" Version="10.10.1" />
  <PackageReference Include="SadConsole.Host.MonoGame" Version="10.10.1" />
  <PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.4.1" />
</ItemGroup>
```

```
dotnet build
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

## Step 5: let the tests see the game code

> **You are in:** `my-roguelike`

Two separate jobs here: one new file, then one command.

### 5a. Create the file

The classes you are about to write are `internal`, meaning visible only inside their own
project. The test project is a separate project, so by default it cannot see them.

Create a new file in your editor at this exact path:

```
my-roguelike/
  RogueTutorial/
    InternalsVisibleTo.cs      <- create this
```

with this as its entire contents:

```csharp
// Grants the test project access to internal types such as RootScreen.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("RogueTutorial.Tests")]
```

The alternative is making everything `public`, which widens the API of your game to the world
just to satisfy a test. This is the narrower option.

### 5b. Point the test project at the game project

The test project also needs to know the game project exists. Back in the terminal, from
`my-roguelike`:

```
cd RogueTutorial.Tests
dotnet add reference ../RogueTutorial/RogueTutorial.csproj
cd ..
```

```
Reference `..\RogueTutorial\RogueTutorial.csproj` added to the project.
```

### Checkpoint

Two mistakes here are common and neither announces itself clearly, so check both now.

Open `RogueTutorial.Tests/RogueTutorial.Tests.csproj` and confirm it contains:

```xml
<ItemGroup>
  <ProjectReference Include="..\RogueTutorial\RogueTutorial.csproj" />
</ItemGroup>
```

And confirm the new file is in the **game** project, not the test project:

```
my-roguelike/
  RogueTutorial/
    InternalsVisibleTo.cs      <- here
  RogueTutorial.Tests/
                               <- not here
```

If the reference is missing, or the file is in the wrong project, the tests will not compile
once you write them, and the errors will name types rather than the real problem:

```
The type or namespace name 'RogueTutorial' could not be found
The type or namespace name 'GridBounds' could not be found
The type or namespace name 'SadRogue' could not be found
```

All three mean the same thing: the test project cannot see the game project. `SadRogue` appears
in that list because the test project receives it through the game project rather than
installing it itself.

## Step 6: write the code

> **You are in:** `my-roguelike`. This step creates files in your editor and runs no commands.

Five files, in this order. Each is in
[`parts/part-01-drawing-and-moving/`](../parts/part-01-drawing-and-moving/) with full comments,
and the reasoning behind each is in [What it is](#what-it-is).

1. [`RogueTutorial/GridBounds.cs`](../parts/part-01-drawing-and-moving/RogueTutorial/GridBounds.cs) — the rectangle and the clamp rule.
2. [`RogueTutorial/PlayerMover.cs`](../parts/part-01-drawing-and-moving/RogueTutorial/PlayerMover.cs) — the position and the invariant.
3. [`RogueTutorial/MovementKeys.cs`](../parts/part-01-drawing-and-moving/RogueTutorial/MovementKeys.cs) — the key table.
4. [`RogueTutorial/RootScreen.cs`](../parts/part-01-drawing-and-moving/RogueTutorial/RootScreen.cs) — the surface, the translation, the drawing.
5. [`RogueTutorial/Program.cs`](../parts/part-01-drawing-and-moving/RogueTutorial/Program.cs) — replace the template's "Hello, World!" **entirely**:

```csharp
using SadConsole;
using SadConsole.Configuration;
using RogueTutorial;

const int ScreenWidthInCells = 80;
const int ScreenHeightInCells = 25;
const string WindowTitle = "Roguelike Tutorial - Part 1";

Builder gameStartup = new Builder()
    .SetWindowSizeInCells(ScreenWidthInCells, ScreenHeightInCells)
    .SetStartingScreen<RootScreen>()
    .IsStartingScreenFocused(true)
    .ConfigureFonts(true);

Settings.WindowTitle = WindowTitle;

Game.Create(gameStartup);
Game.Instance.Run();
Game.Instance.Dispose();
```

What those four configuration calls mean:

- `SetWindowSizeInCells(80, 25)` — the grid is measured in character cells, not pixels. The
  host multiplies by the font's cell size to get the window size.
- `SetStartingScreen<RootScreen>()` — records the *type*; SadConsole constructs it once the
  graphics device is alive, which is why `RootScreen` needs a parameterless constructor.
  Another overload takes a factory function for when your screen needs arguments.
- `IsStartingScreenFocused(true)` — gives it keyboard focus. **Omit this and the game runs,
  draws correctly, and ignores every key you press.**
- `ConfigureFonts(true)` — the argument is `useExtendedDefault`. `false` gives the standard
  256-glyph IBM font; `true` gives 512, adding box-drawing and shape characters you will want
  by Part 3.

Then write the tests, in `RogueTutorial.Tests/`. **Write each one before the code it covers and
watch it fail** — that is the only moment a test is itself tested.
[Writing tests](writing-tests.md) covers how, including the xUnit syntax, how to read a failure,
and how to prove your tests can fail at all. It applies to every part, so read it once here.

For this part that means six test classes:

| Test class | Level | Covers |
|---|---|---|
| [`GridBoundsTests`](../parts/part-01-drawing-and-moving/RogueTutorial.Tests/GridBoundsTests.cs) | unit | the edges of the grid, and the first cell past each one |
| [`PlayerMoverTests`](../parts/part-01-drawing-and-moving/RogueTutorial.Tests/PlayerMoverTests.cs) | unit | ordinary moves, diagonals, and stopping at an edge |
| [`MovementKeysTests`](../parts/part-01-drawing-and-moving/RogueTutorial.Tests/MovementKeysTests.cs) | unit | every key's offset, diagonals, and keys that mean nothing |
| [`MovementIntegrationTests`](../parts/part-01-drawing-and-moving/RogueTutorial.Tests/MovementIntegrationTests.cs) | integration | the key table and the position rules composed |
| [`GameStartsEndToEndTests`](../parts/part-01-drawing-and-moving/RogueTutorial.Tests/GameStartsEndToEndTests.cs) | end-to-end | the real executable surviving startup |
| [`UntestabilityProof`](../parts/part-01-drawing-and-moving/RogueTutorial.Tests/UntestabilityProof.cs) | unit | that `RootScreen` throws without a graphics host |

## Step 7: build and run

> **You are in:** `my-roguelike`

```
dotnet build
dotnet test
dotnet run --project RogueTutorial
```

Expected: a clean build, a green suite, and a window with an `@` you can move with the arrow
keys.

### If something is wrong

| Symptom | Cause |
|---|---|
| `FileNotFoundException: MonoGame.Framework` | Step 4's third package is missing, or its version was changed |
| `NETSDK1209` | Step 3 not applied, or applied to only one of the two `.csproj` files |
| `CS0246: 'SadConsole' could not be found` | Follow-on from a framework problem; fix Step 3 first |
| The window opens but keys do nothing | `IsStartingScreenFocused(true)` is missing |
| The `@` leaves a trail behind it | `DrawFrame` is not clearing the surface first |
| `CS0122: 'GridBounds' is inaccessible` | Step 5's `InternalsVisibleTo.cs` is missing, or is in the test project instead of the game project |
| A black console window opens behind the game | Expected; set `<OutputType>WinExe</OutputType>` to hide it |
| `The type or namespace name 'RogueTutorial'/'GridBounds'/'SadRogue' could not be found` | The test project has no `ProjectReference`; see [the finished project files](#the-finished-project-files) |
| `No test is available in ...` | Nothing was discovered: a test class that is not `public`, or a method with no `[Fact]` |
| docfx pages have no descriptions | `GenerateDocumentationFile` is missing from the game `.csproj` |
| `Cannot find config file ... docfx.json` | The file was never created; see the documentation build below |

## The finished project files

Compare yours against these. Most setup problems in this part are a difference between one of
your two `.csproj` files and one of these, and a whole-file comparison finds them faster than
reading error messages does.

Package versions may be newer than shown; that is fine. What matters is that the same packages
are present, that both files say `net9.0`, and that the test project has the `ProjectReference`.

**`my-roguelike/RogueTutorial/RogueTutorial.csproj`** — the game:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.4.1" />
    <PackageReference Include="SadConsole" Version="10.10.1" />
    <PackageReference Include="SadConsole.Host.MonoGame" Version="10.10.1" />
  </ItemGroup>

</Project>
```

The last two `PropertyGroup` lines are only needed if you want the docfx build below. Everything
else is required.

**`my-roguelike/RogueTutorial.Tests/RogueTutorial.Tests.csproj`** — the tests:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\RogueTutorial\RogueTutorial.csproj" />
  </ItemGroup>

</Project>
```

The four packages come from `dotnet new xunit` and you do not add them by hand. **You add
exactly one thing to this file: the `ProjectReference`**, and Step 5b's `dotnet add reference`
writes it for you. Note there is no SadConsole package here — the test project reaches
SadConsole and SadRogue through the game project.

`<Using Include="Xunit" />` is why the test files in this repository have no `using Xunit;` line;
the project supplies it to every file.

## Optional: the documentation build

> **You are in:** `my-roguelike`

This generates a browsable HTML reference from the `///` comments in your source. Skip it if you
do not want one; nothing else in the tutorial depends on it.

### 1. Let the compiler emit your comments

Without this, docfx builds a site with a page per type and **no descriptions on any of them**,
because the comments never leave the source file. Add two lines to
`RogueTutorial/RogueTutorial.csproj`, inside the existing `<PropertyGroup>`:

```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
<NoWarn>$(NoWarn);CS1591</NoWarn>
```

`GenerateDocumentationFile` writes your `///` comments into an XML file next to the assembly.
`NoWarn` suppresses "missing XML comment on publicly visible type", which would otherwise warn
once for every member you have not documented yet.

### 2. Install docfx

```
dotnet new tool-manifest
dotnet tool install docfx
```

This installs it into this folder rather than machine-wide, and records it in
`dotnet-tools.json`. Anyone who clones your project runs `dotnet tool restore` to get the same
version.

**If `dotnet-tools.json` already exists**, the first command refuses:

```
Creating this template will make changes to existing files:
  Overwrite   ./dotnet-tools.json
```

That means the step is already done, not that something is wrong. Skip it. Do not pass
`--force`, which would discard whatever else the manifest lists. Confirm docfx is available and
move on:

```
dotnet docfx --version
```

```
2.78.5+fafdcd5ddacdb756bd5c4b84f2f07c18292e4821
```

### 3. Create four files

**You write all four by hand, in your editor.** docfx does not generate them - they are the
input it reads. Copy each block below into a new file at the path given.

```
my-roguelike/
  docfx.json        <- create   (what to document, and where the output goes)
  index.md          <- create   (the site's front page)
  toc.yml           <- create   (the top navigation bar)
  api/
    index.md        <- create   (header for the generated reference)
```

Create the `api` folder first; the other three sit beside your two project folders. Everything
docfx generates lands in `api/*.yml` and `_site/`, and you never edit those.

**Do not copy this repository's `docfx.json`.** It points into `parts/part-01-drawing-and-moving/`
because of how the tutorial repository is arranged, and that path does not exist in yours. Use
this one, which matches the layout you have been building:

`my-roguelike/docfx.json`:

```json
{
  "metadata": [
    {
      "src": [ { "files": [ "RogueTutorial/RogueTutorial.csproj" ] } ],
      "dest": "api",
      "includePrivateMembers": true,
      "properties": { "TargetFramework": "net9.0" }
    }
  ],
  "build": {
    "content": [
      { "files": [ "api/**.yml", "api/index.md" ] },
      { "files": [ "index.md", "toc.yml" ] }
    ],
    "output": "_site",
    "template": [ "default", "modern" ],
    "globalMetadata": {
      "_appName": "RogueTutorial",
      "_appTitle": "My Roguelike",
      "_enableSearch": true
    }
  }
}
```

`includePrivateMembers` is on because most of the interesting classes in this tutorial are
`internal`, and without it the generated site would be nearly empty.

`my-roguelike/index.md` — the landing page, anything you like:

```markdown
# My Roguelike

Built by following the Roguelike Tutorial in C#.

Run it with `dotnet run --project RogueTutorial`.
```

`my-roguelike/toc.yml` — the top navigation bar:

```yaml
- name: Home
  href: index.md
- name: API
  href: api/
```

`my-roguelike/api/index.md` — a header for the generated reference. Create the `api` folder
first:

```markdown
# API Reference

Every type in the game, generated from the documentation comments in the source.
```

### 4. Build and serve it

```
dotnet docfx docfx.json --serve
```

Expected:

```
Build succeeded.
    0 warning(s)
    0 error(s)
```

Then open <http://localhost:8080>.

**If port 8080 is already taken**, pick another:

```
dotnet docfx docfx.json --serve --port 8081
```

Note that docfx checks the port *before* it reads the config file, so
`TCP port 8080 is already being in use` can hide a completely different problem. If you get it,
free the port or change it, then run again and read whatever error comes next.

To build the site without serving it, drop `--serve`. The output lands in `_site/`.

### Rebuilding after you delete a class

docfx writes one `.yml` per type into `api/` and **never removes the ones whose type has gone**,
so a class you delete keeps its page in the generated site indefinitely. Later parts of this
tutorial delete classes, so clear the generated files before rebuilding:

```
del api\*.yml
del api\.manifest
dotnet docfx docfx.json
```

`api/index.md` is yours and hand-written; leave it. Everything else in that folder is output.

### Where the output goes

```
my-roguelike/
  api/          generated .yml, one per type - regenerated every build
  _site/        the HTML site - this is what you open
```

Both are build output. If you put the project in git, ignore them:

```
_site/
api/*.yml
api/toc.yml
api/.manifest
```

Keep `api/index.md`, which is yours rather than generated.

---

Next: **Part 2, the entity class and the map.**

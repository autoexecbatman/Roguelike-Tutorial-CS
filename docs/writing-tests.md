# Writing tests

This applies to every part of the tutorial, so it lives here rather than being repeated
thirteen times. Read it once, during Part 1, and refer back when a part adds a rule.

- [What it is](#what-it-is)
- [How to use it](#how-to-use-it)
- [How to set it up](#how-to-set-it-up)

---

# What it is

## The three levels, and what each one can see

| Level | Covers | Cannot see | Cost |
|---|---|---|---|
| **Unit** | one class, on its own | whether two classes are wired together correctly | milliseconds |
| **Integration** | several classes composed | whether the program starts at all | milliseconds |
| **End-to-end** | the real executable | which line is wrong when it fails | seconds, opens a window |

They are not redundant. Part 1's `MovementKeys` and `PlayerMover` were each individually
correct while a sign error between them would have gone unnoticed — that is the integration
level. And a missing MonoGame package left every unit test green while the game crashed on
startup — that is the end-to-end level, and nothing cheaper can see it.

Write mostly unit tests, a few integration tests per part, and exactly one end-to-end test for
the whole tutorial.

## The vocabulary

xUnit is the framework this tutorial uses. One requirement and five pieces of syntax cover
almost everything you will write.

**The class must be `public`.** xUnit does not discover tests in an `internal` class, and it
reports nothing when it skips one — you get "No test is available in ..." rather than an error
naming the class. Visual Studio's *Add > Class* creates `internal class` by default, so this
catches people who add test files through the IDE rather than typing them:

```csharp
internal class GridBoundsTests   // discovered: nothing
public sealed class GridBoundsTests   // discovered
```

**`[Fact]`** marks a method as a test taking no arguments:

```csharp
[Fact]
public void ClampLeavesAnInsidePositionUnchanged()
{
    GridBounds bounds = new GridBounds(80, 25);

    Assert.Equal(new Point(5, 5), bounds.Clamp(new Point(5, 5)));
}
```

**`[Theory]` with `[InlineData]`** runs the same test body once per row. Each row is a separate
test in the results, so one failing row names itself:

```csharp
[Theory]
[InlineData(0, 0, true)]
[InlineData(79, 24, true)]
[InlineData(80, 24, false)]
public void ContainsAcceptsExactlyTheCellsOfTheGrid(int x, int y, bool expected)
{
    GridBounds bounds = new GridBounds(80, 25);

    Assert.Equal(expected, bounds.Contains(new Point(x, y)));
}
```

**`Assert.Equal(expected, actual)`** — expected first. Getting the order backwards does not
change whether the test passes, and it does make every failure message read inside out.

**`Assert.Throws<T>(...)`** asserts that a specific exception type is thrown:

```csharp
Assert.Throws<ArgumentOutOfRangeException>(() => new GridBounds(0, 5));
```

**`Record.Exception(...)`** captures whatever was thrown, or `null` if nothing was, for when you
care that something failed but not what:

```csharp
Exception thrown = Record.Exception(() => new RootScreen());
Assert.NotNull(thrown);
```

## What a test is named

Name the test as the claim it makes about the code, in a sentence:

```
ClampPullsAnOutsidePositionToTheNearestEdge
PressingUpMovesTowardTheTopOfTheScreen
AStartingPositionOutsideTheGridIsRejected
```

Not `TestClamp`, `Clamp1`, `Clamp_Works`. When a test fails, its name is the first and often
only thing you read, and it should tell you what stopped being true. A name you cannot write as
a sentence usually means the test is checking more than one thing.

---

# How to use it

## Write the test first, and watch it fail

This is the part people skip, and skipping it produces tests that cannot fail.

1. **Write the test before the code exists.** Give the class and method a stub that throws
   `NotImplementedException`, or nothing at all.
2. **Run it. Confirm it fails, and read why.** It must fail on your assertion, with your
   expected value in the message. A test that fails with a compile error or a
   `NullReferenceException` has not been tested — it has crashed.
3. **Write the code until it passes.**

Step 2 is the only moment the test itself is ever checked. A test written after the code is
written against an implementation you have already decided is correct, so it encodes your
assumption instead of checking it.

A real failure looks like this — this is the output from breaking one of Part 1's tests on
purpose:

```
  Error Message:
   Assert.Equal() Failure: Values differ
Expected: (6,5)
Actual:   (5,5)
  Stack Trace:
     at GridBoundsTests.ClampLeavesAnInsidePositionUnchanged() in ...\GridBoundsTests.cs:line 34
```

Expected, actual, and the line. That is a red step. Compare it with what you get when a
`Debug.Assert` inside the code fires first:

```
  Error Message:
   Microsoft.VisualStudio.TestPlatform.TestHost.DebugAssertException :
   Method Debug.Fail failed with 'Clamp must return a position inside the grid.'
```

Both are failures, and they mean different things. The first says the code computed the wrong
answer. The second says the code broke its own stated invariant before it ever got to answer,
which is usually the more serious of the two.

## Derive the expected value from the rule, not from the output

The temptation is to run the code, see what it returns, and paste that in as `expected`. A test
built that way passes forever and checks nothing — it records the bug along with everything
else.

Work it out from the specification instead. Part 1's grid is 80 cells wide, so the largest legal
X is 79, so `Clamp(new Point(200, -7))` must be `(79, 0)`. That arithmetic is done on paper
before the code runs.

## Test the boundary, not the middle

`Clamp(new Point(40, 12))` on an 80x25 grid tells you almost nothing — it is in the middle and
almost any implementation returns it unchanged. The values worth writing are the edges and the
first value past them:

```csharp
[InlineData(79, 24, true)]    // the last legal cell
[InlineData(80, 24, false)]   // one past, on X
[InlineData(79, 25, false)]   // one past, on Y
[InlineData(-1, 0, false)]    // one before
```

Off-by-one errors live exactly there and nowhere else.

## Prove your tests can fail

A suite that has only ever been green tells you nothing. Once a part's tests pass, break the
code on purpose and confirm the right test goes red:

| Break this | Expect |
|---|---|
| An upper bound: `Width - 1` becomes `Width` | the boundary tests fail |
| A direction: a diagonal becomes horizontal | the diagonal test fails |
| Delete a dependency from `bin/` | the end-to-end test fails |

If something stays green, that rule has no test. Undo each change afterwards and confirm the
suite is green again — check the file is genuinely back, not merely that the tests pass.

Do this once per part. It costs a few minutes and it is the difference between having tests and
believing you have tests.

## Test the rules, never the framework

You cannot construct a SadConsole screen class in a test — it needs a live graphics host, and
Part 1 covers why in detail. That constraint decides what you test:

- **Do** test the rule classes: what a position clamps to, which key means which direction,
  whether a tile blocks movement.
- **Do not** test that SadConsole draws a character, that MonoGame opens a window, or that
  a dictionary lookup works. Those are somebody else's tests.

If a rule is hard to test, that is information about the design rather than about testing. The
usual cause is that the rule is sitting inside a class that needs a window. Move it out.

## Assertions in the code are not tests

`Debug.Assert` inside the game and `Assert.Equal` inside a test look similar and do different
jobs.

- **`Debug.Assert` states an invariant**: something that must be true whenever the program is
  wired correctly. It fires at the site of the fault, in every test that happens to run through
  that line. It is compiled out of Release builds.
- **A test assertion states an expectation** about one specific input.

Use `Debug.Assert` where a condition is impossible rather than merely unwanted. A guard against
something that can legitimately happen at run time — a missing file, an empty lookup — is an
`if`, not an assert.

---

# How to set it up

The wiring is done once, in Part 1, and every later part inherits it. Steps 2 and 5 of
[Part 1's setup](part-01-drawing-and-moving.md#how-to-set-it-up) cover it in full; in short:

```
dotnet new xunit -n RogueTutorial.Tests -o RogueTutorial.Tests
dotnet sln add RogueTutorial.Tests/RogueTutorial.Tests.csproj
cd RogueTutorial.Tests
dotnet add reference ../RogueTutorial/RogueTutorial.csproj
cd ..
```

Plus one file in the game project, so the test project can see `internal` classes:

```csharp
// RogueTutorial/InternalsVisibleTo.cs
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("RogueTutorial.Tests")]
```

Delete the template's `UnitTest1.cs`. It is a placeholder that asserts nothing.

## Running them

```
dotnet test                                  # everything
dotnet test --filter "Category!=EndToEnd"    # skip the window-opening test
dotnet test --filter "FullyQualifiedName~GridBoundsTests"   # one class
```

If a run reports **"No test is available in ..."**, nothing was discovered. The usual causes are
a test class that is not `public`, or a method with no `[Fact]` or `[Theory]` on it.

The last form is what you want during the red step, when you care about one test and not the
other forty-five.

## Marking the end-to-end test

It is categorised so it can be filtered out, because it launches the real game and waits:

```csharp
[Trait("Category", "EndToEnd")]
public sealed class GameStartsEndToEndTests
```

---

Back to [Part 1](part-01-drawing-and-moving.md).

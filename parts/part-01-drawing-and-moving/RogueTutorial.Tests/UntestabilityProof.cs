/*
 * A single test recording WHY the movement logic had to be extracted from RootScreen.
 * It constructs the screen object in a plain test process, with no graphics host, and
 * expects the failure that proves the class cannot be exercised without a window.
 *
 * Usage - it is an ordinary xUnit fact, so:
 *
 *     dotnet test --filter FullyQualifiedName~UntestabilityProof
 */

using System;
using RogueTutorial;
using Xunit;

public sealed class UntestabilityProof
{
    /// <summary>
    /// Constructing RootScreen without a running SadConsole host throws, because the
    /// constructor reads Game.Instance for the grid dimensions. This is the red step
    /// that justifies moving the movement rules into a host-free class.
    /// </summary>
    [Fact]
    public void ConstructingRootScreenWithoutAHostThrows()
    {
        // No Game.Create call has run, so Game.Instance has never been assigned.
        Exception thrown = Record.Exception(() => new RootScreen());

        // Record the fact that it fails at all; the exact exception type is SadConsole's business.
        Assert.NotNull(thrown);
    }
}

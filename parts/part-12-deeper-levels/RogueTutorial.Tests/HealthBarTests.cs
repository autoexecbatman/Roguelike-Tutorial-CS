/*
 * Unit tests for the health bar. The fill fraction and the caption are tested separately: a bar
 * that draws the right length with the wrong numbers, or the reverse, would otherwise pass on
 * the strength of whichever half was correct.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~HealthBarTests
 */

using System;
using RogueTutorial;
using Xunit;

public sealed class HealthBarTests
{
    [Theory]
    [InlineData(30, 30, 10, 10)]    // full health fills the bar
    [InlineData(15, 30, 10, 5)]     // half
    [InlineData(24, 30, 10, 8)]     // eight tenths
    [InlineData(3, 30, 10, 1)]      // a tenth
    [InlineData(0, 30, 10, 0)]      // dead is empty, whatever the rounding
    public void TheFillIsTheFractionOfHealthRemaining(int current, int maximum, int cells, int expected)
    {
        Assert.Equal(expected, HealthBar.FilledCells(current, maximum, cells));
    }

    [Fact]
    public void RoundingDownMeansOnlyFullHealthReadsFull()
    {
        // 29 of 30 across ten cells is 9.67, which must not round up to a full bar: a player
        // who has been hit should be able to see it.
        Assert.Equal(9, HealthBar.FilledCells(29, 30, 10));
        Assert.Equal(10, HealthBar.FilledCells(30, 30, 10));
    }

    [Fact]
    public void ALivingFighterAlwaysShowsAtLeastOneCell()
    {
        // 1 of 30 across ten cells rounds down to zero, and an empty bar on a living player
        // reads as a bug rather than as low health.
        Assert.Equal(1, HealthBar.FilledCells(1, 30, 10));
    }

    [Fact]
    public void DeadIsTheOnlyEmptyBar()
    {
        Assert.Equal(0, HealthBar.FilledCells(0, 30, 10));
        Assert.True(HealthBar.FilledCells(1, 30, 10) > 0);
    }

    [Fact]
    public void TheCaptionCarriesTheRealNumbers()
    {
        string bar = HealthBar.Render(current: 24, maximum: 30, width: 30);

        Assert.StartsWith("HP: 24/30 ", bar);
    }

    [Fact]
    public void TheLineIsExactlyTheWidthAsked()
    {
        // The caller writes into a fixed row, so a short line leaves stale characters behind and
        // a long one overflows into the log.
        foreach (int width in new[] { 20, 40, 80 })
        {
            Assert.Equal(width, HealthBar.Render(24, 30, width).Length);
        }
    }

    [Fact]
    public void TheBarShowsFilledThenEmpty()
    {
        // 15 of 30 with a caption of "HP: 15/30 " (10 characters) leaves 10 bar cells, half full.
        string bar = HealthBar.Render(current: 15, maximum: 30, width: 20);

        Assert.Equal("HP: 15/30 =====-----", bar);
    }

    [Fact]
    public void AFullBarHasNoEmptyCells()
    {
        string bar = HealthBar.Render(current: 30, maximum: 30, width: 20);

        Assert.Equal("HP: 30/30 ==========", bar);
    }

    [Fact]
    public void ADeadPlayersBarHasNoFilledCells()
    {
        string bar = HealthBar.Render(current: 0, maximum: 30, width: 19);

        Assert.DoesNotContain("=", bar);
        Assert.StartsWith("HP: 0/30 ", bar);
    }

    [Fact]
    public void AWidthThatWouldHideTheNumbersIsRejected()
    {
        // The numbers matter more than the bar. Truncating the caption would hide the thing the
        // bar exists to show, so it fails instead.
        Assert.Throws<ArgumentOutOfRangeException>(() => HealthBar.Render(24, 30, width: 5));
    }

    [Fact]
    public void AWidthWithNoRoomForBarCellsIsStillValid()
    {
        // Exactly the caption and nothing else: legal, if useless.
        string bar = HealthBar.Render(24, 30, width: 10);

        Assert.Equal("HP: 24/30 ", bar);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AMaximumBelowOneIsRejected(int maximum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HealthBar.FilledCells(0, maximum, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => HealthBar.Render(0, maximum, 20));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(31)]
    public void HealthOutsideItsRangeIsRejected(int current)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HealthBar.FilledCells(current, 30, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => HealthBar.Render(current, 30, 20));
    }

    [Fact]
    public void ANegativeBarWidthIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HealthBar.FilledCells(15, 30, -1));
    }
}

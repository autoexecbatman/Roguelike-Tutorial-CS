/*
 * Unit tests for how the window is divided. Expected values are worked out from the description
 * - map, then one status row, then the log - rather than from what the code returned.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~ScreenLayoutTests
 */

using System;
using RogueTutorial;
using Xunit;

public sealed class ScreenLayoutTests
{
    [Fact]
    public void TheRegionsTileTheWindowExactly()
    {
        // Nothing lost between them and nothing overlapping: a row belongs to one region.
        ScreenLayout layout = new ScreenLayout(80, 25, logRows: 5);

        Assert.Equal(25, layout.MapHeight + 1 + layout.LogRows);
    }

    [Fact]
    public void TheMapGetsWhatThePanelDoesNotTake()
    {
        ScreenLayout layout = new ScreenLayout(80, 25, logRows: 5);

        // 25 rows less five of log less one of status.
        Assert.Equal(19, layout.MapHeight);
    }

    [Fact]
    public void TheStatusRowIsTheFirstRowBelowTheMap()
    {
        ScreenLayout layout = new ScreenLayout(80, 25, logRows: 5);

        Assert.Equal(19, layout.StatusRow);
        Assert.Equal(20, layout.LogTopRow);
    }

    [Fact]
    public void TheLastLogRowIsTheLastRowOfTheWindow()
    {
        // An off-by-one here writes a log line past the bottom of the screen.
        ScreenLayout layout = new ScreenLayout(80, 25, logRows: 5);

        Assert.Equal(24, layout.LogTopRow + layout.LogRows - 1);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(18, true)]
    [InlineData(19, false)]
    [InlineData(24, false)]
    [InlineData(-1, false)]
    public void IsMapRowAcceptsExactlyTheMapRows(int row, bool expected)
    {
        ScreenLayout layout = new ScreenLayout(80, 25, logRows: 5);

        Assert.Equal(expected, layout.IsMapRow(row));
    }

    [Fact]
    public void ABiggerLogLeavesASmallerMap()
    {
        ScreenLayout small = new ScreenLayout(80, 25, logRows: 3);
        ScreenLayout large = new ScreenLayout(80, 25, logRows: 10);

        Assert.Equal(21, small.MapHeight);
        Assert.Equal(14, large.MapHeight);
    }

    [Fact]
    public void TheSmallestWorkableWindowIsAccepted()
    {
        // One map row, one status row, one log row.
        ScreenLayout layout = new ScreenLayout(1, 3, logRows: 1);

        Assert.Equal(1, layout.MapHeight);
        Assert.Equal(1, layout.StatusRow);
        Assert.Equal(2, layout.LogTopRow);
    }

    [Fact]
    public void AWindowWithNoRoomForAMapIsRejected()
    {
        // Two rows: one status, one log, none left. A map of zero rows is unplayable rather
        // than merely small, so this fails loudly instead of producing it.
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenLayout(80, 2, logRows: 1));
    }

    [Fact]
    public void ALogTakingTheWholeWindowIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenLayout(80, 25, logRows: 24));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ALogOfNoRowsIsRejected(int logRows)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenLayout(80, 25, logRows));
    }

    [Fact]
    public void AWindowWithNoColumnsIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenLayout(0, 25, logRows: 5));
    }
}

/*
 * Unit tests for the message log. The property that matters is that it stays bounded: a long
 * game must not grow it without limit.
 *
 * Usage:  dotnet test --filter FullyQualifiedName~MessageLogTests
 */

using System;
using System.Linq;
using RogueTutorial;
using Xunit;

public sealed class MessageLogTests
{
    [Fact]
    public void ANewLogIsEmpty()
    {
        Assert.Empty(new MessageLog(10).Messages);
    }

    [Fact]
    public void MessagesComeBackInTheOrderTheyWereAdded()
    {
        MessageLog log = new MessageLog(10);

        log.Add("first");
        log.Add("second");
        log.Add("third");

        Assert.Equal(new[] { "first", "second", "third" }, log.Messages);
    }

    [Fact]
    public void TheOldestIsDroppedAtCapacity()
    {
        MessageLog log = new MessageLog(capacity: 3);

        log.Add("one");
        log.Add("two");
        log.Add("three");
        log.Add("four");

        Assert.Equal(new[] { "two", "three", "four" }, log.Messages);
    }

    [Fact]
    public void TheLogNeverGrowsPastItsCapacity()
    {
        MessageLog log = new MessageLog(capacity: 5);

        for (int turn = 0; turn < 500; turn++)
        {
            log.Add($"turn {turn}");
        }

        Assert.Equal(5, log.Messages.Count);
        Assert.Equal("turn 499", log.Messages.Last());
    }

    [Fact]
    public void LatestReturnsTheNewestStillOldestFirst()
    {
        // Oldest first, so the caller can print them top to bottom without reversing.
        MessageLog log = new MessageLog(10);

        log.Add("one");
        log.Add("two");
        log.Add("three");

        Assert.Equal(new[] { "two", "three" }, log.Latest(2));
    }

    [Fact]
    public void LatestReturnsEverythingWhenAskedForMoreThanExists()
    {
        MessageLog log = new MessageLog(10);

        log.Add("only");

        Assert.Equal(new[] { "only" }, log.Latest(5));
    }

    [Fact]
    public void LatestOfNoneIsEmpty()
    {
        MessageLog log = new MessageLog(10);

        log.Add("something");

        Assert.Empty(log.Latest(0));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankMessageIsRejected(string? message)
    {
        // A blank line means a formatting bug upstream, and keeping it would hide the cause.
        MessageLog log = new MessageLog(10);

        Assert.Throws<ArgumentException>(() => log.Add(message!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ACapacityThatHoldsNothingIsRejected(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MessageLog(capacity));
    }

    [Fact]
    public void ANegativeCountIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MessageLog(10).Latest(-1));
    }
}

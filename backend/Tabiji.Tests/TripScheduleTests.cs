using Tabiji.Domain.Trips;

namespace Tabiji.Tests;

/// <summary>
/// The chronological rule of a trip's route, on its own.
///
/// The rule is forgiving about missing dates and strict about contradictory
/// ones. A stop may have no dates, or only one of them. What it may not do
/// is contradict the stops that come before it on the route.
/// </summary>
public sealed class TripScheduleTests
{
    private static readonly DateOnly TripStartsOn = new(2027, 5, 16);
    private static readonly DateOnly TripEndsOn = new(2027, 6, 5);

    private static TripDestinationDraft Stop(
        string name,
        DateOnly? startsOn = null,
        DateOnly? endsOn = null) => new(null, name, startsOn, endsOn);

    private static DateOnly May(int day) => new(2027, 5, day);

    private static DateOnly June(int day) => new(2027, 6, day);

    private static void Accepts(params TripDestinationDraft[] destinations) =>
        TripSchedule.EnsureChronological(TripStartsOn, TripEndsOn, destinations);

    private static ArgumentException Refuses(params TripDestinationDraft[] destinations) =>
        Assert.Throws<ArgumentException>(
            () => TripSchedule.EnsureChronological(TripStartsOn, TripEndsOn, destinations));

    [Fact]
    public void ARouteWithNoDatesAtAllIsAccepted()
    {
        Accepts(Stop("Osaka"), Stop("Kyoto"), Stop("Ise"));
    }

    [Fact]
    public void FullyDatedStopsInOrderAreAccepted()
    {
        Accepts(
            Stop("Osaka", May(16), May(20)),
            Stop("Kyoto", May(20), May(24)),
            Stop("Tokyo", May(26), June(5)));
    }

    [Fact]
    public void AStopThatEndsBeforeItStartsIsRefused()
    {
        var exception = Refuses(Stop("Kyoto", May(20), May(18)));

        Assert.Contains("Kyoto", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADateBeforeTheTripStartsIsRefused()
    {
        var exception = Refuses(Stop("Osaka", new DateOnly(2027, 5, 1), null));

        Assert.Contains("before the trip starts", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADateAfterTheTripEndsIsRefused()
    {
        var exception = Refuses(Stop("Tokyo", null, new DateOnly(2027, 6, 10)));

        Assert.Contains("after the trip ends", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoFullyDatedStopsInOrderAreAccepted()
    {
        Accepts(Stop("Osaka", May(16), May(20)), Stop("Kyoto", May(21), May(24)));
    }

    [Fact]
    public void TwoOverlappingStaysAreRefused()
    {
        var exception = Refuses(
            Stop("Osaka", May(16), May(20)),
            Stop("Kyoto", May(18), May(22)));

        Assert.Contains("Osaka", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Kyoto", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AStopMayBeginOnTheDayTheLastOneEnded()
    {
        Accepts(Stop("Osaka", May(16), May(20)), Stop("Kyoto", May(20), May(24)));
    }

    [Fact]
    public void AStartOnlyStopAfterAFullStopIsAccepted()
    {
        Accepts(Stop("Osaka", May(16), May(20)), Stop("Kyoto", May(20), null));
    }

    [Fact]
    public void AStartOnlyStopThatBeginsInsideThePreviousStayIsRefused()
    {
        var exception = Refuses(
            Stop("Osaka", May(16), May(20)),
            Stop("Kyoto", May(18), null));

        Assert.Contains("Osaka", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Kyoto", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEndOnlyStopThatEndsBeforeTheFloorIsRefused()
    {
        var exception = Refuses(
            Stop("Osaka", May(16), May(20)),
            Stop("Kyoto", null, May(18)));

        Assert.Contains("Kyoto", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEndOnlyStopAfterAFullStopIsAccepted()
    {
        Accepts(Stop("Osaka", May(16), May(20)), Stop("Kyoto", null, May(24)));
    }

    [Fact]
    public void AStartOnlyStopBeforeAnEarlierStartOnlyStopIsRefused()
    {
        Refuses(Stop("Osaka", May(20), null), Stop("Kyoto", May(18), null));
    }

    [Fact]
    public void AnEndOnlyStopBeforeAnEarlierEndOnlyStopIsRefused()
    {
        Refuses(Stop("Osaka", null, May(20)), Stop("Kyoto", null, May(18)));
    }

    [Fact]
    public void AStartOnlyStopBeforeAnEndOnlyStopIsAccepted()
    {
        Accepts(Stop("Osaka", May(16), null), Stop("Kyoto", null, May(20)));
    }

    [Fact]
    public void AnEndOnlyStopBeforeAStartOnlyStopIsRefused()
    {
        Refuses(Stop("Osaka", null, May(20)), Stop("Kyoto", May(18), null));
    }

    [Fact]
    public void AnUndatedStopBetweenTwoDatedStopsIsAccepted()
    {
        Accepts(
            Stop("Osaka", May(16), May(20)),
            Stop("Ise"),
            Stop("Tokyo", May(26), June(5)));
    }

    [Fact]
    public void AnUndatedStopDoesNotResetTheFloor()
    {
        var exception = Refuses(
            Stop("Osaka", May(16), May(20)),
            Stop("Ise"),
            Stop("Tokyo", May(18), June(5)));

        Assert.Contains("Osaka", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Tokyo", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AGapBetweenStopsIsAccepted()
    {
        Accepts(Stop("Osaka", May(16), May(20)), Stop("Tokyo", May(26), June(5)));
    }

    [Fact]
    public void StopsThatAreOutOfOrderAreRefusedAndBothAreNamed()
    {
        var exception = Refuses(
            Stop("Tokyo", May(20), June(5)),
            Stop("Kyoto", May(16), May(24)));

        Assert.Contains("Tokyo", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Kyoto", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFirstDatedStopMayNotStartBeforeTheTrip()
    {
        Refuses(Stop("Osaka", new DateOnly(2027, 5, 1), May(20)));
    }

    [Fact]
    public void TheRouteFromTheBriefIsAccepted()
    {
        Accepts(
            Stop("Osaka", May(16), May(20)),
            Stop("Kyoto", May(20), May(24)),
            Stop("Ise"),
            Stop("Tokyo", May(26), June(5)));
    }
}

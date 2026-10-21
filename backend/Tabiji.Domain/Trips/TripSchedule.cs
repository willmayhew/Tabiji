using System.Globalization;

namespace Tabiji.Domain.Trips;

/// <summary>
/// The schedule of a trip: the rule that keeps a route in chronological
/// order.
///
/// The rule is forgiving about missing dates and strict about contradictory
/// ones. A stop may have no dates, or only one of them. What it may not do
/// is contradict the stops that come before it on the route.
///
/// The order is the traveller's. Nothing here reorders a route: a route whose
/// dates contradict its order is reported, so the traveller can put it right.
/// </summary>
public static class TripSchedule
{
    /// <summary>
    /// Checks a whole route against the trip it belongs to.
    ///
    /// Every date must fall inside the trip's own dates, a stop may not end
    /// before it starts, and the stops must be able to be visited in the
    /// order they are given. Gaps between stops are fine: the days that
    /// belong to no stop are the traveller's own.
    ///
    /// Raises <see cref="ArgumentException"/> naming the stop at fault and
    /// the stop it conflicts with.
    /// </summary>
    public static void EnsureChronological(
        DateOnly tripStartsOn,
        DateOnly tripEndsOn,
        IReadOnlyList<TripDestinationDraft> destinations)
    {
        var problem = FindProblem(tripStartsOn, tripEndsOn, destinations);

        if (problem is not null)
        {
            throw new ArgumentException(problem, nameof(destinations));
        }
    }

    /// <summary>
    /// The first thing that is wrong with the route, or null when the route
    /// is sound.
    /// </summary>
    private static string? FindProblem(
        DateOnly tripStartsOn,
        DateOnly tripEndsOn,
        IReadOnlyList<TripDestinationDraft> destinations)
    {
        foreach (var destination in destinations)
        {
            var outsideTrip = OutsideTrip(destination, tripStartsOn, tripEndsOn);

            if (outsideTrip is not null)
            {
                return outsideTrip;
            }

            var reversed = Reversed(destination);

            if (reversed is not null)
            {
                return reversed;
            }
        }

        return OutOfOrder(destinations, tripStartsOn);
    }

    /// <summary>
    /// Checks that a stop's dates fall inside the trip's own dates.
    /// </summary>
    private static string? OutsideTrip(
        TripDestinationDraft destination,
        DateOnly tripStartsOn,
        DateOnly tripEndsOn)
    {
        if (destination.StartsOn is DateOnly startsOn)
        {
            if (startsOn < tripStartsOn)
            {
                return $"\"{destination.Name}\" starts on {Format(startsOn)}, " +
                    $"which is before the trip starts on {Format(tripStartsOn)}.";
            }

            if (startsOn > tripEndsOn)
            {
                return $"\"{destination.Name}\" starts on {Format(startsOn)}, " +
                    $"which is after the trip ends on {Format(tripEndsOn)}.";
            }
        }

        if (destination.EndsOn is DateOnly endsOn)
        {
            if (endsOn < tripStartsOn)
            {
                return $"\"{destination.Name}\" ends on {Format(endsOn)}, " +
                    $"which is before the trip starts on {Format(tripStartsOn)}.";
            }

            if (endsOn > tripEndsOn)
            {
                return $"\"{destination.Name}\" ends on {Format(endsOn)}, " +
                    $"which is after the trip ends on {Format(tripEndsOn)}.";
            }
        }

        return null;
    }

    /// <summary>
    /// Checks that a stop does not end before it starts.
    /// </summary>
    private static string? Reversed(TripDestinationDraft destination)
    {
        if (destination.StartsOn is DateOnly startsOn &&
            destination.EndsOn is DateOnly endsOn &&
            endsOn < startsOn)
        {
            return $"\"{destination.Name}\" ends on {Format(endsOn)} and starts on {Format(startsOn)}.";
        }

        return null;
    }

    /// <summary>
    /// Walks the route in order and keeps a floor: the earliest day the next
    /// stop may begin.
    ///
    /// A stop that carries no dates establishes nothing and is stepped over.
    /// A stop that carries a start date establishes that day, because its stay
    /// may be as short as a single day. A stop that carries only an end date
    /// establishes its end, because it must have begun at or before it.
    ///
    /// The floor only ever moves forward, so a stop is measured against every
    /// dated stop before it and not only against its neighbour. A stop is
    /// refused only when its own date falls before the floor, which is the one
    /// case that no choice of the missing dates could repair.
    /// </summary>
    private static string? OutOfOrder(
        IReadOnlyList<TripDestinationDraft> destinations,
        DateOnly tripStartsOn)
    {
        var floor = tripStartsOn;
        TripDestinationDraft? anchor = null;

        foreach (var destination in destinations)
        {
            if (destination.StartsOn is DateOnly startsOn)
            {
                if (startsOn < floor)
                {
                    return Contradiction(destination, startsOn, anchor, floor, "starts");
                }

                floor = destination.EndsOn ?? startsOn;
            }
            else if (destination.EndsOn is DateOnly endsOn)
            {
                if (endsOn < floor)
                {
                    return Contradiction(destination, endsOn, anchor, floor, "ends");
                }

                floor = endsOn;
            }
            else
            {
                // A stop with no dates says nothing about when the journey
                // reaches it, so it changes nothing.
                continue;
            }

            anchor = destination;
        }

        return null;
    }

    /// <summary>
    /// The message for a stop that contradicts the route it is on. It names
    /// the stop at fault and the stop it conflicts with, so the traveller can
    /// put it right without guessing.
    /// </summary>
    private static string Contradiction(
        TripDestinationDraft destination,
        DateOnly date,
        TripDestinationDraft? anchor,
        DateOnly floor,
        string verb)
    {
        var conflict = anchor is null
            ? $"the trip starts on {Format(floor)}"
            : anchor.StartsOn is null
                ? $"\"{anchor.Name}\", which comes before it, does not end until {Format(floor)}"
                : anchor.EndsOn is null
                    ? $"\"{anchor.Name}\", which comes before it, does not begin until {Format(floor)}"
                    : $"\"{anchor.Name}\", which comes before it, is there until {Format(floor)}";

        return $"\"{destination.Name}\" {verb} on {Format(date)}, but {conflict}.";
    }

    /// <summary>
    /// A date as it is written out, so that the message reads the same
    /// wherever it is produced.
    /// </summary>
    private static string Format(DateOnly date) =>
        date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
}

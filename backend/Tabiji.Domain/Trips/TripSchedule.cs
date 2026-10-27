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
    /// Checks that a day is one the trip has: on or after the day it starts,
    /// and on or before the day it ends. Both ends are themselves days of the
    /// trip, exactly as they are for a stop's dates.
    ///
    /// The subject is the thing that was planned for the day, so that the
    /// refusal names it and reads like the rest of the schedule's refusals.
    /// </summary>
    public static void EnsureIsATripDay(
        DateOnly tripStartsOn,
        DateOnly tripEndsOn,
        DateOnly day,
        string subject)
    {
        if (day < tripStartsOn)
        {
            throw new ArgumentException(
                $"\"{subject}\" is planned for {Format(day)}, " +
                $"which is before the trip starts on {Format(tripStartsOn)}.",
                nameof(day));
        }

        if (day > tripEndsOn)
        {
            throw new ArgumentException(
                $"\"{subject}\" is planned for {Format(day)}, " +
                $"which is after the trip ends on {Format(tripEndsOn)}.",
                nameof(day));
        }
    }

    /// <summary>
    /// Checks every stop against the destination it belongs to.
    ///
    /// A stop may not end before it starts. Its dates must fall inside its
    /// destination's dates when the destination has dates; when the
    /// destination has only one of them, the stop is bounded by that one and
    /// by the trip's other end. A destination without dates leaves the trip's
    /// own dates as the only bound.
    ///
    /// The stops are not required to run in order within their destination.
    /// A stop is a part of the place, not a leg of the journey.
    /// </summary>
    public static void EnsureStopsWithinDestinations(
        DateOnly tripStartsOn,
        DateOnly tripEndsOn,
        IReadOnlyList<TripDestinationDraft> destinations)
    {
        foreach (var destination in destinations)
        {
            foreach (var stop in destination.Stops ?? [])
            {
                var problem = StopProblem(
                    tripStartsOn,
                    tripEndsOn,
                    destination.Name,
                    destination.StartsOn,
                    destination.EndsOn,
                    stop);

                if (problem is not null)
                {
                    throw new ArgumentException(problem, nameof(destinations));
                }
            }
        }
    }

    /// <summary>
    /// Checks one stop against the destination it belongs to. Used by the
    /// aggregate when a stop is added on its own rather than through a whole
    /// route.
    /// </summary>
    public static void EnsureStopWithinDestination(
        DateOnly tripStartsOn,
        DateOnly tripEndsOn,
        string parentName,
        DateOnly? parentStartsOn,
        DateOnly? parentEndsOn,
        TripStopDraft stop)
    {
        var problem = StopProblem(
            tripStartsOn,
            tripEndsOn,
            parentName,
            parentStartsOn,
            parentEndsOn,
            stop);

        if (problem is not null)
        {
            throw new ArgumentException(problem, nameof(stop));
        }
    }

    /// <summary>
    /// Checks every planned day against the trip it belongs to. A planned day
    /// is a calendar day of the trip, independent of the place's stay dates;
    /// a place may be planned for a day outside its stay but inside the trip.
    /// </summary>
    public static void EnsurePlannedOnIsATripDay(
        DateOnly tripStartsOn,
        DateOnly tripEndsOn,
        IReadOnlyList<TripDestinationDraft> destinations,
        IReadOnlyList<TripStopDraft>? unfiledPlaces = null)
    {
        foreach (var destination in destinations)
        {
            if (destination.PlannedOn is DateOnly destinationDay)
            {
                EnsureIsATripDay(tripStartsOn, tripEndsOn, destinationDay, destination.Name);
            }

            foreach (var stop in destination.Stops ?? [])
            {
                if (stop.PlannedOn is DateOnly stopDay)
                {
                    EnsureIsATripDay(tripStartsOn, tripEndsOn, stopDay, stop.Name);
                }
            }
        }

        foreach (var place in unfiledPlaces ?? [])
        {
            if (place.PlannedOn is DateOnly placeDay)
            {
                EnsureIsATripDay(tripStartsOn, tripEndsOn, placeDay, place.Name);
            }
        }
    }

    /// <summary>
    /// Checks a legacy unfiled place's optional stay dates against the trip.
    /// An unfiled place has no destination to bound it, so the trip is the
    /// only bound.
    /// </summary>
    public static void EnsurePlacesWithinTrip(
        DateOnly tripStartsOn,
        DateOnly tripEndsOn,
        IReadOnlyList<TripStopDraft> places)
    {
        foreach (var place in places)
        {
            var reversed = Reversed(place.Name, place.StartsOn, place.EndsOn);

            if (reversed is not null)
            {
                throw new ArgumentException(reversed, nameof(places));
            }

            if (place.StartsOn is DateOnly startsOn)
            {
                if (startsOn < tripStartsOn)
                {
                    throw new ArgumentException(
                        $"\"{place.Name}\" starts on {Format(startsOn)}, " +
                        $"which is before the trip starts on {Format(tripStartsOn)}.",
                        nameof(places));
                }

                if (startsOn > tripEndsOn)
                {
                    throw new ArgumentException(
                        $"\"{place.Name}\" starts on {Format(startsOn)}, " +
                        $"which is after the trip ends on {Format(tripEndsOn)}.",
                        nameof(places));
                }
            }

            if (place.EndsOn is DateOnly endsOn)
            {
                if (endsOn < tripStartsOn)
                {
                    throw new ArgumentException(
                        $"\"{place.Name}\" ends on {Format(endsOn)}, " +
                        $"which is before the trip starts on {Format(tripStartsOn)}.",
                        nameof(places));
                }

                if (endsOn > tripEndsOn)
                {
                    throw new ArgumentException(
                        $"\"{place.Name}\" ends on {Format(endsOn)}, " +
                        $"which is after the trip ends on {Format(tripEndsOn)}.",
                        nameof(places));
                }
            }
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
    /// Checks that a destination does not end before it starts.
    /// </summary>
    private static string? Reversed(TripDestinationDraft destination) =>
        Reversed(destination.Name, destination.StartsOn, destination.EndsOn);

    /// <summary>
    /// Checks that a place does not end before it starts.
    /// </summary>
    private static string? Reversed(string name, DateOnly? startsOn, DateOnly? endsOn)
    {
        if (startsOn is DateOnly start && endsOn is DateOnly end && end < start)
        {
            return $"\"{name}\" ends on {Format(end)} and starts on {Format(start)}.";
        }

        return null;
    }

    /// <summary>
    /// The first thing that is wrong with a stop, or null when the stop is
    /// sound.
    ///
    /// The bounds are the destination's own dates where it has them, and the
    /// trip's dates everywhere else. A stop is a part of the place, so it can
    /// never run outside the days the traveller is there.
    /// </summary>
    private static string? StopProblem(
        DateOnly tripStartsOn,
        DateOnly tripEndsOn,
        string parentName,
        DateOnly? parentStartsOn,
        DateOnly? parentEndsOn,
        TripStopDraft stop)
    {
        var reversed = Reversed(stop.Name, stop.StartsOn, stop.EndsOn);

        if (reversed is not null)
        {
            return reversed;
        }

        var lower = parentStartsOn ?? tripStartsOn;
        var upper = parentEndsOn ?? tripEndsOn;

        if (stop.StartsOn is DateOnly startsOn)
        {
            if (startsOn < lower)
            {
                return parentStartsOn is not null
                    ? $"\"{stop.Name}\", a stop at \"{parentName}\", starts on {Format(startsOn)}, " +
                      $"which is before \"{parentName}\" starts on {Format(lower)}."
                    : $"\"{stop.Name}\", a stop at \"{parentName}\", starts on {Format(startsOn)}, " +
                      $"which is before the trip starts on {Format(lower)}.";
            }

            if (startsOn > upper)
            {
                return parentEndsOn is not null
                    ? $"\"{stop.Name}\", a stop at \"{parentName}\", starts on {Format(startsOn)}, " +
                      $"which is after \"{parentName}\" ends on {Format(upper)}."
                    : $"\"{stop.Name}\", a stop at \"{parentName}\", starts on {Format(startsOn)}, " +
                      $"which is after the trip ends on {Format(upper)}.";
            }
        }

        if (stop.EndsOn is DateOnly endsOn)
        {
            if (endsOn < lower)
            {
                return parentStartsOn is not null
                    ? $"\"{stop.Name}\", a stop at \"{parentName}\", ends on {Format(endsOn)}, " +
                      $"which is before \"{parentName}\" starts on {Format(lower)}."
                    : $"\"{stop.Name}\", a stop at \"{parentName}\", ends on {Format(endsOn)}, " +
                      $"which is before the trip starts on {Format(lower)}.";
            }

            if (endsOn > upper)
            {
                return parentEndsOn is not null
                    ? $"\"{stop.Name}\", a stop at \"{parentName}\", ends on {Format(endsOn)}, " +
                      $"which is after \"{parentName}\" ends on {Format(upper)}."
                    : $"\"{stop.Name}\", a stop at \"{parentName}\", ends on {Format(endsOn)}, " +
                      $"which is after the trip ends on {Format(upper)}.";
            }
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

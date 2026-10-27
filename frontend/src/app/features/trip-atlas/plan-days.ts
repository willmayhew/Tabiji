import { stopsOf, TripDestination, TripStop } from '../../core/models/trip';

/**
 * The destination or stop the traveller is staying at on a day.
 *
 * A stop is the more specific answer, so it wins over its destination when its
 * own dates cover the day.
 */
export interface PlanNode {
  kind: 'destination' | 'stop';
  id: string;
  name: string;
  /** The destination holding the stop, or null when the node is a destination. */
  parentName: string | null;
}

/**
 * One day of the plan: the places planned for it, and the destination or stop
 * being stayed at then.
 *
 * The places are stops or legacy unfiled places: they are the same unified
 * entity, and each carries its own parent context through the route.
 */
export interface DayGroup {
  /** The day, as a plain `YYYY-MM-DD` date. */
  date: string;
  /** The destination or stop whose stay covers the day, or null when none does. */
  stop: PlanNode | null;
  places: TripStop[];
}

/** A trip's places, read day by day. */
export interface DayPlan {
  /** The days of the trip that hold something, in calendar order. */
  planned: DayGroup[];
  /** Days that fall outside the trip's dates, in calendar order. */
  outside: DayGroup[];
  /** The places that have no day yet, in the order they were given. */
  unplanned: TripStop[];
}

/**
 * Groups a trip's places by the day they are planned for.
 *
 * The days come in calendar order, because that is the order a trip is
 * travelled, and a day with nothing planned for it produces no group at all:
 * the plan shows what is in it, not what is missing from it.
 *
 * A day outside the trip's dates is kept apart rather than folded in or
 * dropped. A trip can be shortened after a place was planned, and the plan is
 * the reader's own work: it is shown where it is so they can move it, instead
 * of being quietly cleared or hidden.
 *
 * Within a day the journey comes first: places filed under a destination or
 * stop that is earlier on the route are read earlier, and the unfiled places
 * come last. Nothing here reorders the trip itself.
 */
export function groupStopsByDay(
  tripStartsOn: string,
  tripEndsOn: string,
  destinations: readonly TripDestination[],
  unfiledPlaces: readonly TripStop[],
): DayPlan {
  // The route, including its destinations, gives every place its rank. The
  // destinations themselves are not places in the plan: only the stops and
  // the legacy unfiled places are planned for a day.
  const rankRoute = [
    ...destinations.flatMap((destination) => [destination, ...stopsOf(destination)]),
    ...unfiledPlaces,
  ];
  const rank = new Map(rankRoute.map((place, index) => [place.id, index]));
  const unfiledRank = rankRoute.length;

  const places = [
    ...destinations.flatMap((destination) => stopsOf(destination)),
    ...unfiledPlaces,
  ];

  const rankOf = (place: TripStop): number => rank.get(place.id) ?? unfiledRank;

  const byDay = new Map<string, TripStop[]>();
  const unplanned: TripStop[] = [];

  for (const place of places) {
    const day = place.plannedOn ?? null;

    if (day === null) {
      unplanned.push(place);
      continue;
    }

    const onTheDay = byDay.get(day);

    if (onTheDay === undefined) {
      byDay.set(day, [place]);
    } else {
      onTheDay.push(place);
    }
  }

  const planned: DayGroup[] = [];
  const outside: DayGroup[] = [];

  for (const date of [...byDay.keys()].sort()) {
    const onTheDay = byDay.get(date) ?? [];

    // The sort is stable, so places that rank the same keep the order they
    // were given in.
    onTheDay.sort((left, right) => rankOf(left) - rankOf(right));

    const group: DayGroup = { date, stop: stopStaying(date, destinations), places: onTheDay };

    // `YYYY-MM-DD` sorts as text exactly as it sorts in time.
    if (date < tripStartsOn || date > tripEndsOn) {
      outside.push(group);
    } else {
      planned.push(group);
    }
  }

  return { planned, outside, unplanned };
}

/**
 * The destination or stop whose stay covers a day, or null when none does.
 *
 * A stop is the more specific answer, so it is looked for first. A place
 * covers a day only when both of its dates are known and the day falls between
 * them, both ends included. A place carrying one date or none says nothing
 * about which days it is there for — the same reason it establishes nothing in
 * the schedule's own rule — so it covers no day.
 *
 * Two stops can meet on the day one ends and the next begins. The one earlier
 * on the route is the answer, so a day always names exactly one place.
 */
function stopStaying(
  date: string,
  destinations: readonly TripDestination[],
): PlanNode | null {
  for (const destination of destinations) {
    for (const stop of stopsOf(destination)) {
      if (covers(stop.startsOn, stop.endsOn, date)) {
        return { kind: 'stop', id: stop.id, name: stop.name, parentName: destination.name };
      }
    }
  }

  for (const destination of destinations) {
    if (covers(destination.startsOn, destination.endsOn, date)) {
      return { kind: 'destination', id: destination.id, name: destination.name, parentName: null };
    }
  }

  return null;
}

function covers(startsOn: string | null, endsOn: string | null, date: string): boolean {
  return startsOn !== null && endsOn !== null && startsOn <= date && date <= endsOn;
}

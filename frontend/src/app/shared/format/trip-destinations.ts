import { TripDestination } from '../../core/models/trip';

/**
 * The separator between the stops of a route. It is a typographic arrow,
 * the same mark the destination plates draw.
 */
export const ROUTE_SEPARATOR = '→';

/**
 * The route as one line: "Rome → Amalfi → Naples".
 *
 * Past `max` stops the rest are counted rather than listed, so a long route
 * never breaks the line it is set on.
 */
export function routeLabel(destinations: readonly TripDestination[], max = 3): string {
  if (destinations.length === 0) {
    return '';
  }

  const shown = destinations.slice(0, max).map((destination) => destination.name);
  const rest = destinations.length - shown.length;

  const label = shown.join(` ${ROUTE_SEPARATOR} `);

  return rest > 0 ? `${label} ${ROUTE_SEPARATOR} +${rest} more` : label;
}

/**
 * How many stops a route has, in words: "3 stops".
 */
export function routeSummary(destinations: readonly TripDestination[]): string {
  return destinations.length === 1 ? '1 stop' : `${destinations.length} stops`;
}

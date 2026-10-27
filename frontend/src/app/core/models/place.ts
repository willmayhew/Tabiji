/**
 * A place as the geocoder found it.
 *
 * A destination field is filled with the `destinationName`, which carries the
 * name and the locality that tells it apart, e.g. `Asda, Poole`. The `name`
 * is the short name on its own and the `label` is the fuller line shown in
 * the search results, so two places with the same name can be told apart.
 */
export interface PlaceSuggestion {
  /** The most specific human name for the place, e.g. `Asda`. */
  name: string;
  /** The name with its locality, e.g. `Asda, Poole`. What a destination is filled with. */
  destinationName: string;
  /** The fuller line for the list, e.g. `Asda, Poole, England, United Kingdom`. */
  label: string;
  latitude: number;
  longitude: number;
}

/**
 * A place chosen from the map or the search, ready to fill a destination.
 *
 * A selection carries a name and its coordinates together: the one is never
 * stored without the other.
 */
export interface PlaceSelection {
  name: string;
  latitude: number;
  longitude: number;
}

/**
 * What kind of place a saved place is. Mirrors the API's `PlaceCategory`, so
 * the two can never drift without this file being changed.
 */
export type PlaceCategory = 'Sight' | 'Food' | 'Stay' | 'Shop' | 'Transport' | 'Other' | 'Entertainment';

/** Every category, in the order the API declares them. */
export const PLACE_CATEGORIES: readonly PlaceCategory[] = [
  'Sight',
  'Food',
  'Stay',
  'Shop',
  'Transport',
  'Other',
  'Entertainment',
];

/**
 * The note limit the API enforces on a place. The stop editor mirrors it so
 * the reader is told before the request is made.
 */
export const PLACE_NOTES_MAX_LENGTH = 1000;

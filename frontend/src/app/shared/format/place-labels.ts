import { PLACE_CATEGORIES, type PlaceCategory } from '../../core/models/place';

/**
 * The words shown for a place's category.
 *
 * The API's category is a stable key (`Sight`, `Stay`), so the label can be
 * changed here without touching the database or the wire contract.
 */
export const PLACE_CATEGORY_LABELS: Readonly<Record<PlaceCategory, string>> = {
  Sight: 'Sightseeing',
  Food: 'Food',
  Stay: 'Accommodation',
  Shop: 'Shop',
  Transport: 'Transport',
  Other: 'Other',
  Entertainment: 'Entertainment',
};

/** The word shown for a category key. */
export function placeCategoryLabel(category: PlaceCategory): string {
  return PLACE_CATEGORY_LABELS[category];
}

/**
 * Every category in the order the editor offers it: alphabetical by the word
 * shown, with Other kept last because it is a catch-all rather than a kind.
 */
export const PLACE_CATEGORIES_ALPHABETICALLY: readonly PlaceCategory[] = [
  ...PLACE_CATEGORIES.filter((category) => category !== 'Other').sort((left, right) =>
    PLACE_CATEGORY_LABELS[left].localeCompare(PLACE_CATEGORY_LABELS[right]),
  ),
  'Other',
];

/**
 * The labels the atlas gives to the main route and to the stops within it.
 *
 * A destination keeps the main-route number (1, 2, 3). A stop gets that
 * number and a letter for its place within the destination: 1a, 1b, 1c. After
 * 1z the suffix continues like a spreadsheet column: 1aa, 1ab, ... 1az, 1ba.
 *
 * The label is presentation only. Nothing is persisted and no entity identity
 * changes when a place is reordered.
 */

/** The main-route number of a destination, counting from one. */
export function parentLabel(index: number): string {
  return String(index + 1);
}

/**
 * The letter suffix of the stop at `index`, counting from zero, in the
 * spreadsheet-column style. It never collides, however many stops a
 * destination holds.
 */
export function stopSuffix(index: number): string {
  let value = index + 1;
  let suffix = '';

  while (value > 0) {
    const remainder = (value - 1) % 26;
    suffix = String.fromCharCode(97 + remainder) + suffix;
    value = Math.floor((value - 1) / 26);
  }

  return suffix;
}

/** The label of a stop: its destination's main-route number and its suffix. */
export function stopLabel(parentIndex: number, stopIndex: number): string {
  return `${parentLabel(parentIndex)}${stopSuffix(stopIndex)}`;
}

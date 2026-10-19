/**
 * Destination photography.
 *
 * The `Trip` model has no image, so a photograph is chosen from the trip's free
 * text `destination`. The lookup is deliberately a pure function over a fixed
 * table: it either resolves to a photograph we actually hold, or it resolves to
 * nothing and the caller falls back to the 旅 monogram. It never guesses.
 *
 * The rule is: a named place wins; among named places the first one mentioned
 * wins; otherwise the country photograph; otherwise no photograph at all.
 *
 * This module has no Angular imports and no knowledge of any feature.
 */

/** A photograph that is committed to the repository. */
export interface DestinationPhoto {
  /** Stable identifier, also the file name stem. */
  readonly slug: string;
  /** Describes the photograph for someone who cannot see it. */
  readonly alt: string;
  /** The large rendition. */
  readonly src: string;
  /** Responsive candidates, for `srcset`. */
  readonly srcSet: string;
  /** Intrinsic width of `src`, so the browser can reserve the space. */
  readonly width: number;
  /** Intrinsic height of `src`. */
  readonly height: number;
}

interface PhotoEntry {
  readonly slug: string;
  readonly alt: string;
  readonly keywords: readonly string[];
}

/** Named places. A match here always beats the country fallback. */
const PLACES: readonly PhotoEntry[] = [
  {
    slug: 'kyoto',
    alt: 'A traditional Kyoto alley lined with paper lanterns and wooden machiya houses',
    keywords: ['kyoto', '京都'],
  },
  {
    slug: 'tokyo',
    alt: 'The Tokyo skyline at night with the illuminated Tokyo Tower',
    keywords: ['tokyo', '東京'],
  },
  {
    slug: 'osaka',
    alt: 'Osaka Castle and its traditional Japanese rooflines',
    keywords: ['osaka', '大阪'],
  },
  {
    slug: 'nara',
    alt: 'A sika deer resting on a stone path in Nara Park',
    keywords: ['nara', '奈良'],
  },
  {
    slug: 'hakone',
    alt: 'A red torii gate on the shore of Lake Ashi with misty mountains beyond',
    keywords: ['hakone', '箱根'],
  },
  {
    slug: 'fuji',
    alt: 'Mount Fuji rising above foreground grasses in clear light',
    keywords: ['mount fuji', 'fuji', 'fujisan', '富士山'],
  },
  {
    slug: 'hokkaido',
    alt: 'Colourful flower fields across a rural landscape',
    keywords: ['hokkaido', '北海道'],
  },
  {
    slug: 'hiroshima',
    alt: 'The torii gate of Itsukushima Shrine standing in the water',
    keywords: ['hiroshima', 'miyajima', '広島', '宮島'],
  },
  {
    slug: 'okinawa',
    alt: 'A calm Okinawa beach with clear turquoise water and scattered rocks',
    keywords: ['okinawa', '沖縄'],
  },
];

/** The country, used only when no named place is mentioned. */
const COUNTRIES: readonly PhotoEntry[] = [
  {
    slug: 'japan',
    alt: 'A tranquil Japanese garden with greenery reflected in still water',
    keywords: ['japan', 'japanese', '日本'],
  },
];

const SMALL_WIDTH = 640;
const LARGE_WIDTH = 1280;
const LARGE_HEIGHT = 853;

/** Escapes a keyword so it can be used inside a regular expression. */
function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/**
 * The position where a keyword starts, or -1 when it is absent.
 *
 * The keyword has to sit on word boundaries, so "nara" matches "Nara" and
 * "Nara, Japan" but not "Nararino".
 */
function matchPosition(haystack: string, keyword: string): number {
  const match = new RegExp(`(^|[^a-z])${escapeRegExp(keyword)}([^a-z]|$)`).exec(haystack);

  if (!match) {
    return -1;
  }

  // The first group is the boundary character, so the keyword starts after it.
  return match.index + match[1].length;
}

/** The earliest position at which any of the entry's keywords appears. */
function earliestPosition(haystack: string, entry: PhotoEntry): number {
  let earliest = -1;

  for (const keyword of entry.keywords) {
    const position = matchPosition(haystack, keyword);

    if (position >= 0 && (earliest < 0 || position < earliest)) {
      earliest = position;
    }
  }

  return earliest;
}

function toPhoto(entry: PhotoEntry): DestinationPhoto {
  return {
    slug: entry.slug,
    alt: entry.alt,
    src: `/photos/${entry.slug}-${LARGE_WIDTH}.webp`,
    srcSet: `/photos/${entry.slug}-${SMALL_WIDTH}.webp ${SMALL_WIDTH}w, /photos/${entry.slug}-${LARGE_WIDTH}.webp ${LARGE_WIDTH}w`,
    width: LARGE_WIDTH,
    height: LARGE_HEIGHT,
  };
}

/**
 * Resolves a destination to a photograph, or to `null` when no photograph
 * matches. A region such as "Kanto" or an unfamiliar place resolves to `null`
 * so the caller can show the monogram fallback rather than a misleading image.
 */
export function resolveDestinationPhoto(destination: string | null | undefined): DestinationPhoto | null {
  if (!destination) {
    return null;
  }

  const haystack = destination.trim().toLowerCase();

  if (haystack.length === 0) {
    return null;
  }

  let best: { entry: PhotoEntry; position: number } | null = null;

  for (const entry of PLACES) {
    const position = earliestPosition(haystack, entry);

    if (position < 0) {
      continue;
    }

    if (best === null || position < best.position) {
      best = { entry, position };
    }
  }

  if (best !== null) {
    return toPhoto(best.entry);
  }

  for (const entry of COUNTRIES) {
    if (earliestPosition(haystack, entry) >= 0) {
      return toPhoto(entry);
    }
  }

  return null;
}

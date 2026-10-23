/**
 * The Angular dev server proxies `/api` and `/health` to the backend (see
 * `proxy.conf.json`), so the browser only ever talks to its own origin.
 * Point this at an absolute URL to call a hosted API directly.
 *
 * The map settings carry no secrets: OpenFreeMap serves tiles and Photon
 * serves search without an API key, so nothing here needs protecting and
 * nothing is a credential. Both are free and fair-use; the style and the
 * geocoder can be swapped by editing this block alone.
 */
export const environment = {
  production: false,
  apiBaseUrl: '',
  map: {
    /** The MapLibre style OpenFreeMap serves (Positron: a light, quiet map). */
    styleUrl: 'https://tiles.openfreemap.org/styles/positron',
    /**
     * MapLibre parses tiles and styles on a worker thread, and ships that
     * worker as a separate file. It is copied to the app root as an asset, so
     * it is named here rather than left to the bundler, which cannot place it.
     */
    workerUrl: 'maplibre-gl-worker.mjs',
    /** The geocoder, which supports search-as-you-type and reverse geocoding. */
    geocodingBaseUrl: 'https://photon.komoot.io',
    /** Shown on the picker; required by the tile and geocoding providers. */
    attribution:
      'OpenFreeMap © OpenMapTiles · Data © OpenStreetMap contributors · Search by Photon',
  },
};

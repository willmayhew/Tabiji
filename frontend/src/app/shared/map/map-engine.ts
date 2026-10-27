import { Injectable } from '@angular/core';
import type { Feature, FeatureCollection, LineString } from 'geojson';
import type { GeoJSONSource, Map as MapLibreMap, Marker as MapLibreMarker } from 'maplibre-gl';
import { environment } from '../../../environments/environment';
import { LocatedPoint, markerOffsets } from './marker-offsets';

// A saved place is drawn from its position alone, so the map's callers speak
// in the same point type the offsets are worked out from.
export type { LocatedPoint } from './marker-offsets';

/** How to draw a map. */
export interface MapOptions {
  styleUrl: string;
  latitude: number;
  longitude: number;
  zoom?: number;
}

/** A drawn map, and the few things the picker asks of it. */
export interface MapHandle {
  /** Drops or moves the marker that shows the chosen place. */
  setMarker(latitude: number, longitude: number): void;
  /** Recentres the map, leaving the zoom alone unless one is given. */
  setCentre(latitude: number, longitude: number, zoom?: number): void;
  /** Reports every click on the map as a point. */
  onClick(handler: (latitude: number, longitude: number) => void): void;
  destroy(): void;
}

/**
 * A point the atlas draws.
 *
 * Coordinates travel as separate latitude and longitude values everywhere
 * except here, where they are turned into the `[longitude, latitude]` order
 * MapLibre expects and nowhere else.
 */
export interface AtlasPoint extends LocatedPoint {
  /** The parent's main-route number, such as `1`. */
  label: string;
}

/** A stop marker: a place within a destination, labelled `1a`, `1b`, ... */
export interface AtlasStopPoint extends LocatedPoint {
  /** The hierarchical label, such as `1a`. */
  label: string;
}

/**
 * What the atlas has selected. A destination, a stop within one, and a saved
 * place are three different things with their own identities, so which one is
 * meant has to be said.
 */
export type AtlasSelectionKind = 'destination' | 'stop' | 'place';

export interface AtlasSelection {
  kind: AtlasSelectionKind;
  id: string;
}

/** How to draw the atlas. */
export interface AtlasOptions {
  styleUrl: string;
}

/**
 * A drawn atlas: the trip's located destinations, its saved places, the line
 * that joins the destinations in planned order, and the selection the map and
 * the list share.
 */
export interface AtlasHandle {
  /** Draws the located destinations, labelled, replacing what was there. */
  setDestinations(points: readonly AtlasPoint[]): void;
  /** Draws the visible stops of the selected destination, replacing what was there. */
  setStops(points: readonly AtlasStopPoint[]): void;
  /** Draws the planned connection, in the order the points are given. */
  setRoute(points: readonly AtlasPoint[]): void;
  /** Draws the connection between the visible stops, in their own order. */
  setStopRoute(points: readonly LocatedPoint[]): void;
  /** Marks one stop or place as selected, or clears the selection. */
  setSelection(selection: AtlasSelection | null): void;
  /**
   * Brings the selected stop or place into view, and does nothing when it is
   * already visible. The zoom is kept: the reader asked to see a place, not to
   * lose their bearings.
   */
  reveal(selection: AtlasSelection | null): void;
  /** Frames the trip, falling back to its places when no stop is located. */
  fitToTrip(): void;
  /** Re-measures the container, for a map that was hidden and has returned. */
  resize(): void;
  /** Reports a click on a marker. */
  onSelect(handler: (selection: AtlasSelection) => void): void;
  /** Reports that the map could not draw something, such as its tiles. */
  onError(handler: () => void): void;
  destroy(): void;
}

/** The colour of the picker's marker, matching the restrained terracotta. */
const MARKER_COLOUR = '#b4552f';

/** The colour of the planned connection: a warm grey, and a dashed line, so
    it reads as a plan rather than as a road. */
const ROUTE_COLOUR = '#6f6350';

const DEFAULT_ZOOM = 9;

/** How close the atlas looks when there is nothing to frame. */
const WORLD_VIEW = { latitude: 20, longitude: 0, zoom: 1.5 };

/** The room left around the trip when it is framed. */
const FIT_PADDING = 48;

/** How close a trip of a single stop is framed. */
const SINGLE_STOP_ZOOM = 11;

const ROUTE_SOURCE_ID = 'atlas-route';
const ROUTE_LAYER_ID = 'atlas-route-line';

/** A stop's connection is a dotted forest line, distinct from the main route. */
const STOP_ROUTE_COLOUR = '#ff0000';
const STOP_ROUTE_SOURCE_ID = 'atlas-stop-route';
const STOP_ROUTE_LAYER_ID = 'atlas-stop-route-line';

type MapLibreModule = typeof import('maplibre-gl');

/**
 * The one place that touches MapLibre.
 *
 * The library is imported **only** when a map is actually drawn, so it lives
 * in its own lazily-loaded chunk and never weighs down the first paint. When
 * the environment cannot draw a map — a test runner, or a browser without
 * WebGL — `create` and `createAtlas` return null and the caller carries on
 * without a map.
 */
@Injectable({ providedIn: 'root' })
export class MapEngine {
  /** Whether a map can be drawn here at all. */
  get supported(): boolean {
    return typeof document !== 'undefined' && typeof WebGLRenderingContext !== 'undefined';
  }

  async create(container: HTMLElement, options: MapOptions): Promise<MapHandle | null> {
    const maplibre = await this.loadMaplibre();

    if (maplibre === null) {
      return null;
    }

    const { Map: MapLibre, Marker } = maplibre;

    let map: MapLibreMap;

    try {
      map = new MapLibre({
        container,
        style: options.styleUrl,
        center: [options.longitude, options.latitude],
        zoom: options.zoom ?? DEFAULT_ZOOM,
        attributionControl: { compact: true },
      });
    } catch {
      return null;
    }

    let marker: MapLibreMarker | null = null;

    return {
      setMarker: (latitude, longitude) => {
        marker ??= new Marker({ color: MARKER_COLOUR });
        marker.setLngLat([longitude, latitude]).addTo(map);
      },
      setCentre: (latitude, longitude, zoom) => {
        map.easeTo({ center: [longitude, latitude], zoom: zoom ?? map.getZoom() });
      },
      onClick: (handler) => {
        map.on('click', (event) => handler(event.lngLat.lat, event.lngLat.lng));
      },
      destroy: () => {
        marker?.remove();
        marker = null;
        map.remove();
      },
    };
  }

  /**
   * Draws the trip's atlas: a numbered marker for every located destination,
   * a dot for every saved place, and one dashed line that joins the
   * destinations in the order they are given. Places are never joined into
   * that line: they are points of interest, not stops on the route.
   *
   * The markers are DOM elements the engine makes itself, so their classes
   * are global: Angular's component style encapsulation never reaches them.
   */
  async createAtlas(container: HTMLElement, options: AtlasOptions): Promise<AtlasHandle | null> {
    const maplibre = await this.loadMaplibre();

    if (maplibre === null) {
      return null;
    }

    const { Map: MapLibre, Marker } = maplibre;

    let map: MapLibreMap;

    try {
      map = new MapLibre({
        container,
        style: options.styleUrl,
        center: [WORLD_VIEW.longitude, WORLD_VIEW.latitude],
        zoom: WORLD_VIEW.zoom,
        attributionControl: { compact: true },
      });
    } catch {
      return null;
    }

    // The stops and the saved places are drawn as two sets of markers, because
    // they mean two different things and are told apart on the map. Their
    // identities come from different tables, so they are kept apart here too.
    const destinationMarkers = new Map<string, MapLibreMarker>();
    const destinationElements = new Map<string, HTMLElement>();
    const stopMarkers = new Map<string, MapLibreMarker>();
    const stopElements = new Map<string, HTMLElement>();

    let destinations: readonly AtlasPoint[] = [];
    let stops: readonly AtlasStopPoint[] = [];
    let route: readonly AtlasPoint[] = [];
    let stopRoute: readonly LocatedPoint[] = [];
    let selection: AtlasSelection | null = null;
    let loaded = false;
    let routeDirty = false;
    let stopRouteDirty = false;
    let failed = false;
    let selectHandler: ((selection: AtlasSelection) => void) | null = null;
    let errorHandler: (() => void) | null = null;

    // A source or a layer can only be added once the style has loaded, so the
    // current route is remembered and applied then.
    map.on('load', () => {
      loaded = true;

      if (routeDirty) {
        applyRoute();
      }

      if (stopRouteDirty) {
        applyStopRoute();
      }
    });

    // Something the map needed could not be fetched, such as a tile. The map
    // stays on screen; the caller is told so it can say so quietly.
    map.on('error', () => {
      if (errorHandler === null) {
        failed = true;
        return;
      }

      errorHandler();
    });

    function routeData(points: readonly AtlasPoint[]): Feature<LineString> | FeatureCollection {
      if (points.length < 2) {
        return { type: 'FeatureCollection', features: [] };
      }

      return {
        type: 'Feature',
        properties: {},
        geometry: {
          type: 'LineString',
          coordinates: points.map((point) => [point.longitude, point.latitude]),
        },
      };
    }

    function applyRoute(): void {
      routeDirty = false;

      const data = routeData(route);
      const source = map.getSource(ROUTE_SOURCE_ID) as GeoJSONSource | undefined;

      if (source !== undefined) {
        source.setData(data);
        return;
      }

      map.addSource(ROUTE_SOURCE_ID, { type: 'geojson', data });
      map.addLayer({
        id: ROUTE_LAYER_ID,
        type: 'line',
        source: ROUTE_SOURCE_ID,
        layout: { 'line-cap': 'round', 'line-join': 'round' },
        paint: {
          'line-color': ROUTE_COLOUR,
          'line-width': 1.6,
          'line-opacity': 0.85,
          'line-dasharray': [2, 1.5],
        },
      });
    }

    function stopRouteData(
      points: readonly LocatedPoint[],
    ): Feature<LineString> | FeatureCollection {
      if (points.length < 2) {
        return { type: 'FeatureCollection', features: [] };
      }

      return {
        type: 'Feature',
        properties: {},
        geometry: {
          type: 'LineString',
          coordinates: points.map((point) => [point.longitude, point.latitude]),
        },
      };
    }

    function applyStopRoute(): void {
      stopRouteDirty = false;

      const data = stopRouteData(stopRoute);
      const source = map.getSource(STOP_ROUTE_SOURCE_ID) as GeoJSONSource | undefined;

      if (source !== undefined) {
        source.setData(data);
        return;
      }

      map.addSource(STOP_ROUTE_SOURCE_ID, { type: 'geojson', data });

      // The stop line is drawn beneath the main route, so the route stays the
      // more prominent line when the two cross.
      map.addLayer(
        {
          id: STOP_ROUTE_LAYER_ID,
          type: 'line',
          source: STOP_ROUTE_SOURCE_ID,
          layout: { 'line-cap': 'round', 'line-join': 'round' },
          paint: {
            'line-color': STOP_ROUTE_COLOUR,
            'line-width': 1.2,
            'line-opacity': 0.7,
          },
        },
        map.getLayer(ROUTE_LAYER_ID) ? ROUTE_LAYER_ID : undefined,
      );
    }

    function paintSelection(): void {
      for (const [id, element] of destinationElements) {
        element.classList.toggle(
          'atlas-marker--selected',
          selection?.kind === 'destination' && selection.id === id,
        );
      }

      for (const [id, element] of stopElements) {
        element.classList.toggle(
          'atlas-marker--selected',
          selection?.kind === 'stop' && selection.id === id,
        );
      }
    }

    function setOffset(element: HTMLElement, offset: { x: number; y: number } | undefined): void {
      element.style.setProperty('--atlas-offset-x', `${offset?.x ?? 0}px`);
      element.style.setProperty('--atlas-offset-y', `${offset?.y ?? 0}px`);
    }

    /**
     * Two markers at the same place would cover each other, and a saved place
     * can share a place with a stop just as two stops can. So every marker is
     * offset against the whole set, not only its own kind.
     */
    function applyOffsets(): void {
      const offsets = markerOffsets([...destinations, ...stops]);

      for (const [id, element] of destinationElements) {
        setOffset(element, offsets.get(id));
      }

      for (const [id, element] of stopElements) {
        setOffset(element, offsets.get(id));
      }
    }

    /**
     * Brings one set of markers into line with the points it is given: markers
     * that have gone are removed, the rest are moved rather than replaced, and
     * a marker that is new to the set is made once.
     */
    function syncMarkers(
      points: readonly LocatedPoint[],
      markers: Map<string, MapLibreMarker>,
      elements: Map<string, HTMLElement>,
      kind: AtlasSelection['kind'],
      faceClass: string,
      textFor: (id: string) => string,
    ): void {
      const wanted = new Set(points.map((point) => point.id));

      for (const [id, marker] of markers) {
        if (!wanted.has(id)) {
          marker.remove();
          markers.delete(id);
          elements.delete(id);
        }
      }

      for (const point of points) {
        let anchor = elements.get(point.id);

        if (anchor === undefined) {
          // Two elements, because the map writes its own transform onto the one
          // it positions. Everything drawn, and the spread, therefore lives on
          // the inner face, where the stylesheet still has a say.
          anchor = document.createElement('span');
          anchor.className = 'atlas-marker';
          anchor.setAttribute('aria-hidden', 'true');

          const face = document.createElement('span');
          face.className = faceClass;
          face.textContent = textFor(point.id);

          anchor.append(face);
          anchor.addEventListener('click', () => selectHandler?.({ kind, id: point.id }));

          const marker = new Marker({ element: anchor })
            .setLngLat([point.longitude, point.latitude])
            .addTo(map);

          markers.set(point.id, marker);
          elements.set(point.id, anchor);
        } else {
          // A marker that is already on the map is simply moved, so the element
          // the reader clicked on is the one that stays.
          markers.get(point.id)?.setLngLat([point.longitude, point.latitude]);
        }
      }
    }

    function setDestinations(points: readonly AtlasPoint[]): void {
      destinations = points;

      // The number is the destination's place on the whole route, not its
      // place among the destinations that happen to have a location.
      const labels = new Map(points.map((point) => [point.id, point.label]));

      syncMarkers(
        points,
        destinationMarkers,
        destinationElements,
        'destination',
        'atlas-marker__face',
        (id) => labels.get(id) ?? '',
      );

      applyOffsets();
      paintSelection();
    }

    function setStops(points: readonly AtlasStopPoint[]): void {
      stops = points;

      // A stop carries a hierarchical label — 1a, 1b — never a main-route
      // number of its own.
      const labels = new Map(points.map((point) => [point.id, point.label]));

      syncMarkers(
        points,
        stopMarkers,
        stopElements,
        'stop',
        'atlas-marker__stop',
        (id) => labels.get(id) ?? '',
      );

      applyOffsets();
      paintSelection();
    }

    function setStopRoute(points: readonly LocatedPoint[]): void {
      stopRoute = points;

      if (loaded) {
        applyStopRoute();
        return;
      }

      stopRouteDirty = true;
    }

    return {
      setDestinations,
      setStops,
      setRoute: (points) => {
        route = points;

        if (loaded) {
          applyRoute();
          return;
        }

        routeDirty = true;
      },
      setStopRoute,
      setSelection: (next) => {
        selection = next;
        paintSelection();
      },
      reveal: (next) => {
        if (next === null || next.kind === 'place') {
          return;
        }

        const point =
          next.kind === 'destination'
            ? destinations.find((candidate) => candidate.id === next.id)
            : stops.find((candidate) => candidate.id === next.id);

        if (point === undefined) {
          return;
        }

        const position: [number, number] = [point.longitude, point.latitude];

        // Only when it cannot already be seen. A map that moves on every
        // selection takes the reader's bearings away from them.
        if (map.getBounds().contains(position)) {
          return;
        }

        map.easeTo({ center: position });
      },
      fitToTrip: () => {
        // The destinations are the trip; the stops are what is worth seeing
        // along the way. The map frames the destinations, and falls back to
        // the stops only when no destination has a location.
        const framing: readonly LocatedPoint[] =
          destinations.length > 0 ? destinations : stops;

        if (framing.length === 0) {
          map.easeTo({ center: [WORLD_VIEW.longitude, WORLD_VIEW.latitude], zoom: WORLD_VIEW.zoom });
          return;
        }

        let west = Number.POSITIVE_INFINITY;
        let south = Number.POSITIVE_INFINITY;
        let east = Number.NEGATIVE_INFINITY;
        let north = Number.NEGATIVE_INFINITY;

        for (const point of framing) {
          west = Math.min(west, point.longitude);
          south = Math.min(south, point.latitude);
          east = Math.max(east, point.longitude);
          north = Math.max(north, point.latitude);
        }

        // A single point has no bounds to fit, so it is simply looked at.
        if (west === east && south === north) {
          map.easeTo({ center: [west, south], zoom: SINGLE_STOP_ZOOM });
          return;
        }

        map.fitBounds(
          [
            [west, south],
            [east, north],
          ],
          { padding: FIT_PADDING, maxZoom: DEFAULT_ZOOM },
        );
      },
      resize: () => map.resize(),
      onSelect: (handler) => {
        selectHandler = handler;
      },
      onError: (handler) => {
        errorHandler = handler;

        if (failed) {
          failed = false;
          handler();
        }
      },
      destroy: () => {
        for (const marker of destinationMarkers.values()) {
          marker.remove();
        }

        for (const marker of stopMarkers.values()) {
          marker.remove();
        }

        destinationMarkers.clear();
        destinationElements.clear();
        stopMarkers.clear();
        stopElements.clear();

        if (map.getLayer(STOP_ROUTE_LAYER_ID)) {
          map.removeLayer(STOP_ROUTE_LAYER_ID);
        }

        if (map.getSource(STOP_ROUTE_SOURCE_ID)) {
          map.removeSource(STOP_ROUTE_SOURCE_ID);
        }

        if (map.getLayer(ROUTE_LAYER_ID)) {
          map.removeLayer(ROUTE_LAYER_ID);
        }

        if (map.getSource(ROUTE_SOURCE_ID)) {
          map.removeSource(ROUTE_SOURCE_ID);
        }

        map.remove();
      },
    };
  }

  /**
   * Loads MapLibre, once, and points it at the worker that ships beside the
   * app. The worker is a file of its own, which the bundler cannot place, so
   * its URL is given explicitly before any map is drawn.
   */
  private async loadMaplibre(): Promise<MapLibreModule | null> {
    if (!this.supported) {
      return null;
    }

    let maplibre: MapLibreModule;

    try {
      maplibre = await import('maplibre-gl');
    } catch {
      return null;
    }

    maplibre.setWorkerUrl(new URL(environment.map.workerUrl, document.baseURI).toString());

    return maplibre;
  }
}

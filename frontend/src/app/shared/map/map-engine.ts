import { Injectable } from '@angular/core';
import type { Feature, FeatureCollection, LineString } from 'geojson';
import type { GeoJSONSource, Map as MapLibreMap, Marker as MapLibreMarker } from 'maplibre-gl';
import { environment } from '../../../environments/environment';

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
export interface AtlasPoint {
  id: string;
  latitude: number;
  longitude: number;
  /** The stop's place on the route, counting from one. */
  index: number;
}

/** How to draw the atlas. */
export interface AtlasOptions {
  styleUrl: string;
}

/**
 * A drawn atlas: the trip's located destinations, the line that joins them
 * in planned order, and the selection the map and the list share.
 */
export interface AtlasHandle {
  /** Draws the located destinations, numbered, replacing what was there. */
  setDestinations(points: readonly AtlasPoint[]): void;
  /** Draws the planned connection, in the order the points are given. */
  setRoute(points: readonly AtlasPoint[]): void;
  /** Marks one destination as selected, or clears the selection. */
  setSelection(id: string | null): void;
  /** Frames every destination the map is drawing. */
  fitToTrip(): void;
  /** Re-measures the container, for a map that was hidden and has returned. */
  resize(): void;
  /** Reports a click on a destination marker. */
  onSelect(handler: (id: string) => void): void;
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
   * Draws the trip's atlas: a numbered marker for every located destination
   * and one dashed line that joins them in the order they are given.
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

    const markers = new Map<string, MapLibreMarker>();
    const elements = new Map<string, HTMLElement>();

    let route: readonly AtlasPoint[] = [];
    let selectedId: string | null = null;
    let loaded = false;
    let routeDirty = false;
    let failed = false;
    let selectHandler: ((id: string) => void) | null = null;
    let errorHandler: (() => void) | null = null;

    // A source or a layer can only be added once the style has loaded, so the
    // current route is remembered and applied then.
    map.on('load', () => {
      loaded = true;

      if (routeDirty) {
        applyRoute();
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

    function paintSelection(): void {
      for (const [id, element] of elements) {
        element.classList.toggle('atlas-marker--selected', id === selectedId);
      }
    }

    function setDestinations(points: readonly AtlasPoint[]): void {
      const wanted = new Set(points.map((point) => point.id));

      for (const [id, marker] of markers) {
        if (!wanted.has(id)) {
          marker.remove();
          markers.delete(id);
          elements.delete(id);
        }
      }

      for (const point of points) {
        const existing = markers.get(point.id);

        // A marker that is already on the map is simply moved, so the
        // element the reader clicked on is the one that stays.
        if (existing !== undefined) {
          existing.setLngLat([point.longitude, point.latitude]);
          continue;
        }

        const element = document.createElement('span');
        element.className = 'atlas-marker';
        element.setAttribute('aria-hidden', 'true');
        element.textContent = String(point.index);
        element.addEventListener('click', () => selectHandler?.(point.id));

        const marker = new Marker({ element })
          .setLngLat([point.longitude, point.latitude])
          .addTo(map);

        markers.set(point.id, marker);
        elements.set(point.id, element);
      }

      paintSelection();
    }

    return {
      setDestinations,
      setRoute: (points) => {
        route = points;

        if (loaded) {
          applyRoute();
          return;
        }

        routeDirty = true;
      },
      setSelection: (id) => {
        selectedId = id;
        paintSelection();
      },
      fitToTrip: () => {
        if (elements.size === 0) {
          map.easeTo({ center: [WORLD_VIEW.longitude, WORLD_VIEW.latitude], zoom: WORLD_VIEW.zoom });
          return;
        }

        let west = Number.POSITIVE_INFINITY;
        let south = Number.POSITIVE_INFINITY;
        let east = Number.NEGATIVE_INFINITY;
        let north = Number.NEGATIVE_INFINITY;

        for (const marker of markers.values()) {
          const { lng, lat } = marker.getLngLat();

          west = Math.min(west, lng);
          south = Math.min(south, lat);
          east = Math.max(east, lng);
          north = Math.max(north, lat);
        }

        // A single stop has no bounds to fit, so it is simply looked at.
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
        for (const marker of markers.values()) {
          marker.remove();
        }

        markers.clear();
        elements.clear();
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

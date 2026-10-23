import { Injectable } from '@angular/core';
import type { Map as MapLibreMap, Marker as MapLibreMarker } from 'maplibre-gl';
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

/** The colour of the marker, matching the app's restrained terracotta. */
const MARKER_COLOUR = '#b4552f';

const DEFAULT_ZOOM = 9;

/**
 * The one place that touches MapLibre.
 *
 * The library is imported **only** when a map is actually drawn, so it lives
 * in its own lazily-loaded chunk and never weighs down the first paint. When
 * the environment cannot draw a map — a test runner, or a browser without
 * WebGL — `create` returns null and the picker carries on with search alone.
 */
@Injectable({ providedIn: 'root' })
export class MapEngine {
  /** Whether a map can be drawn here at all. */
  get supported(): boolean {
    return typeof document !== 'undefined' && typeof WebGLRenderingContext !== 'undefined';
  }

  async create(container: HTMLElement, options: MapOptions): Promise<MapHandle | null> {
    if (!this.supported) {
      return null;
    }

    let maplibre: typeof import('maplibre-gl');

    try {
      maplibre = await import('maplibre-gl');
    } catch {
      return null;
    }

    const { Map: MapLibre, Marker, setWorkerUrl } = maplibre;

    // MapLibre does its tile and style work on a worker, which ships as a
    // separate file. The bundler cannot place it, so the URL is given
    // explicitly: the worker is served as a static asset beside the app.
    setWorkerUrl(new URL(environment.map.workerUrl, document.baseURI).toString());

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
}

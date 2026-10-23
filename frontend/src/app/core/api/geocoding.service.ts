import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PlaceSuggestion } from '../models/place';

/**
 * One feature of a Photon GeoJSON response. Photon is the geocoder the app
 * uses: it is built on OpenStreetMap data and, unlike Nominatim, it is meant
 * for search-as-you-type.
 */
interface PhotonFeature {
  geometry?: { coordinates?: [number, number] };
  properties?: {
    name?: string;
    street?: string;
    housenumber?: string;
    city?: string;
    district?: string;
    state?: string;
    country?: string;
  };
}

interface PhotonResponse {
  features?: PhotonFeature[];
}

/** How many suggestions a search asks for. */
const DEFAULT_LIMIT = 6;

/**
 * The one place that talks to the geocoder.
 *
 * The provider base URL lives in `environment.map.geocodingBaseUrl`, and the
 * mapping from a provider feature to a `PlaceSuggestion` lives in
 * `placeFromFeature` below, so moving to another provider is a change to
 * those two places and nothing else.
 */
@Injectable({ providedIn: 'root' })
export class GeocodingService {
  private readonly http = inject(HttpClient);

  /**
   * Finds places matching a query. Because the provider supports
   * search-as-you-type, this is safe to call as the reader types; the caller
   * debounces and cancels stale requests.
   */
  search(query: string, limit: number = DEFAULT_LIMIT): Observable<PlaceSuggestion[]> {
    const trimmed = query.trim();

    if (trimmed.length === 0) {
      return of([]);
    }

    const params = new HttpParams().set('q', trimmed).set('limit', limit);

    return this.http
      .get<PhotonResponse>(`${environment.map.geocodingBaseUrl}/api`, { params })
      .pipe(
        map((response) =>
          dedupe(
            (response.features ?? [])
              .map(placeFromFeature)
              .filter((place): place is PlaceSuggestion => place !== null),
          ),
        ),
      );
  }

  /**
   * Finds the place at a point, so a click on the map can name what it hit.
   * Returns null when the point names no place.
   */
  reverse(latitude: number, longitude: number): Observable<PlaceSuggestion | null> {
    const params = new HttpParams()
      .set('lat', latitude)
      .set('lon', longitude)
      .set('limit', 1);

    return this.http
      .get<PhotonResponse>(`${environment.map.geocodingBaseUrl}/reverse`, { params })
      .pipe(
        map((response) => {
          const [first] = response.features ?? [];

          return first ? placeFromFeature(first) : null;
        }),
      );
  }
}

/**
 * The geocoder can offer the same place more than once, under the same name
 * and the same context. The list says it once.
 */
function dedupe(places: PlaceSuggestion[]): PlaceSuggestion[] {
  const seen = new Set<string>();

  return places.filter((place) => {
    if (seen.has(place.label)) {
      return false;
    }

    seen.add(place.label);

    return true;
  });
}

/**
 * Turns a provider feature into a place, or null when it names nothing.
 * Photon returns coordinates as `[longitude, latitude]`.
 */
function placeFromFeature(feature: PhotonFeature): PlaceSuggestion | null {
  const [longitude, latitude] = feature.geometry?.coordinates ?? [];

  if (typeof latitude !== 'number' || typeof longitude !== 'number') {
    return null;
  }

  const name = pickName(feature.properties);

  if (name === null) {
    return null;
  }

  return {
    name,
    destinationName: buildDestinationName(name, feature.properties),
    label: buildLabel(name, feature.properties),
    latitude,
    longitude,
  };
}

/**
 * The name with the locality that tells it apart, and nothing more: `Asda,
 * Poole`. It is what a destination field is filled with, so it stays short
 * enough to read as a place while still saying which Asda is meant. A place
 * whose name already is its locality, such as a city, keeps the name alone.
 */
function buildDestinationName(name: string, properties: PhotonFeature['properties']): string {
  const locality = properties?.city?.trim() || properties?.district?.trim();

  return locality && locality !== name ? `${name}, ${locality}` : name;
}

/** The most specific name a feature carries. */
function pickName(properties: PhotonFeature['properties']): string | null {
  if (!properties) {
    return null;
  }

  const street = [properties.street, properties.housenumber].filter(Boolean).join(' ');

  for (const candidate of [
    properties.name,
    street,
    properties.city,
    properties.district,
    properties.state,
    properties.country,
  ]) {
    const trimmed = candidate?.trim();

    if (trimmed) {
      return trimmed;
    }
  }

  return null;
}

/** The name with the context that tells it apart, without repeating itself. */
function buildLabel(name: string, properties: PhotonFeature['properties']): string {
  const parts: string[] = [];

  for (const candidate of [name, properties?.city, properties?.state, properties?.country]) {
    const trimmed = candidate?.trim();

    if (trimmed && !parts.includes(trimmed)) {
      parts.push(trimmed);
    }
  }

  return parts.join(', ');
}

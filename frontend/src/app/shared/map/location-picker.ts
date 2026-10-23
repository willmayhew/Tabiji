import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
  OnDestroy,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  map,
  of,
  Subject,
  switchMap,
  tap,
} from 'rxjs';
import { environment } from '../../../environments/environment';
import { describeHttpError } from '../../core/api/http-error';
import { GeocodingService } from '../../core/api/geocoding.service';
import { PlaceSelection, PlaceSuggestion } from '../../core/models/place';
import { MapEngine, MapHandle } from './map-engine';

/** How long the picker waits after a keystroke before asking the geocoder. */
const SEARCH_DEBOUNCE_MS = 300;

/**
 * Where the map looks when no place has been chosen yet. It is the whole
 * world, so the picker always opens on something that reads as a map rather
 * than on an empty stretch of it.
 */
const WORLD_VIEW = { latitude: 20, longitude: 0, zoom: 1.5 };

/** How close the map looks once a place has been chosen. */
const SELECTED_ZOOM = 11;

/**
 * Chooses a place from a map, or from a search, and hands it back as a name
 * with its coordinates.
 *
 * The picker is presentational: it owns no destination field. The screen that
 * opens it decides which field the chosen place belongs to, so one picker
 * serves every stop in the form. Cancelling changes nothing, and a place is
 * only ever emitted when the reader confirms it.
 *
 * The whole block is one native `<dialog>`, so the browser gives it the top
 * layer, a focus trap, Escape to close and an inert page behind it. Where
 * `showModal` is missing the dialog falls back to the `open` attribute, so it
 * is still rendered and still testable.
 */
@Component({
  selector: 'app-location-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './location-picker.html',
  styleUrl: './location-picker.css',
})
export class LocationPicker implements OnDestroy {
  private readonly geocoding = inject(GeocodingService);
  private readonly mapEngine = inject(MapEngine);
  private readonly destroyRef = inject(DestroyRef);

  /** Whether the picker is on screen. The parent owns this. */
  readonly open = input(false);

  /** The destination's current name, used to seed the search box. */
  readonly initialQuery = input('');

  /** The destination's current coordinates, if it already has some. */
  readonly initialCoordinates = input<PlaceSelection | null>(null);

  /** Emits the chosen place; the parent fills its field and closes the picker. */
  readonly chosen = output<PlaceSelection>();

  /** Emits when the reader backs out. Nothing is changed. */
  readonly dismissed = output<void>();

  protected readonly attribution = environment.map.attribution;

  protected readonly query = signal('');
  protected readonly suggestions = signal<PlaceSuggestion[]>([]);
  protected readonly searching = signal(false);
  protected readonly searched = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly selection = signal<PlaceSelection | null>(null);
  protected readonly mapUnavailable = signal(false);

  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('dialog');
  private readonly searchInput = viewChild<ElementRef<HTMLInputElement>>('search');
  private readonly mapContainer = viewChild<ElementRef<HTMLElement>>('map');

  /** The element the focus returns to when the picker closes. */
  private previouslyFocused: HTMLElement | null = null;
  private mapHandle: MapHandle | null = null;
  private mapPending = false;

  /** Typing, once it has settled, so the geocoder is not called per keystroke. */
  private readonly queries = new Subject<string>();

  constructor() {
    this.queries
      .pipe(
        map((value) => value.trim()),
        debounceTime(SEARCH_DEBOUNCE_MS),
        distinctUntilChanged(),
        tap(() => {
          this.searching.set(true);
          this.error.set(null);
        }),
        switchMap((term) =>
          this.geocoding.search(term).pipe(
            catchError((failure: HttpErrorResponse) => {
              this.error.set(describeHttpError(failure));
              return of<PlaceSuggestion[]>([]);
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((places) => {
        this.suggestions.set(places);
        this.searching.set(false);
        this.searched.set(true);
      });

    // Only `open` decides whether the dialog is shown. Everything the
    // opening and closing do is untracked, so choosing a place (which writes
    // `selection`) never runs the effect again and never resets the choice.
    effect(() => {
      const open = this.open();

      untracked(() => {
        if (open) {
          this.openDialog();
        } else {
          this.closeDialog();
        }
      });
    });
  }

  ngOnDestroy(): void {
    this.teardownMap();
  }

  protected onQuery(event: Event): void {
    const value = (event.target as HTMLInputElement).value;

    this.query.set(value);

    if (value.trim().length === 0) {
      this.suggestions.set([]);
      this.searched.set(false);
      this.searching.set(false);
      this.error.set(null);
      return;
    }

    this.queries.next(value);
  }

  protected chooseSuggestion(place: PlaceSuggestion): void {
    this.select({
      name: place.destinationName,
      latitude: place.latitude,
      longitude: place.longitude,
    });
  }

  /** A click on the map: name the point, then select it. */
  protected selectFromCoordinates(latitude: number, longitude: number): void {
    this.geocoding
      .reverse(latitude, longitude)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (place) =>
          this.select({
            name: place?.destinationName ?? this.coordsText(latitude, longitude),
            latitude,
            longitude,
          }),
        error: () =>
          this.select({ name: this.coordsText(latitude, longitude), latitude, longitude }),
      });
  }

  protected confirm(): void {
    const selection = this.selection();

    if (selection === null) {
      return;
    }

    this.chosen.emit(selection);
  }

  protected dismiss(event?: Event): void {
    event?.preventDefault();
    this.dismissed.emit();
  }

  /** A click on the panel's backdrop, but not on the panel itself. */
  protected onBackdropClick(event: MouseEvent): void {
    if (event.target === this.dialog()?.nativeElement) {
      this.dismiss();
    }
  }

  protected coordsText(latitude: number, longitude: number): string {
    return `${latitude.toFixed(4)}, ${longitude.toFixed(4)}`;
  }

  private select(place: PlaceSelection): void {
    this.selection.set(place);
    this.mapHandle?.setMarker(place.latitude, place.longitude);
    this.mapHandle?.setCentre(place.latitude, place.longitude);
  }

  private openDialog(): void {
    this.previouslyFocused = (document.activeElement as HTMLElement | null) ?? null;

    const dialog = this.dialog()?.nativeElement;

    if (dialog) {
      if (typeof dialog.showModal === 'function') {
        if (!dialog.open) {
          dialog.showModal();
        }
      } else {
        dialog.setAttribute('open', '');
      }
    }

    this.query.set(this.initialQuery());
    this.suggestions.set([]);
    this.searched.set(false);
    this.searching.set(false);
    this.error.set(null);
    this.selection.set(this.initialCoordinates());

    setTimeout(() => this.searchInput()?.nativeElement.focus());
    this.startMap();
  }

  private closeDialog(): void {
    const dialog = this.dialog()?.nativeElement;

    if (dialog) {
      if (typeof dialog.close === 'function') {
        if (dialog.open) {
          dialog.close();
        }
      } else {
        dialog.removeAttribute('open');
      }
    }

    this.teardownMap();

    const target = this.previouslyFocused;
    this.previouslyFocused = null;
    setTimeout(() => target?.focus());
  }

  private startMap(): void {
    const container = this.mapContainer()?.nativeElement;

    if (!container || this.mapPending) {
      return;
    }

    this.mapPending = true;

    setTimeout(async () => {
      this.mapPending = false;

      const selection = this.selection();
      const latitude = selection?.latitude ?? WORLD_VIEW.latitude;
      const longitude = selection?.longitude ?? WORLD_VIEW.longitude;

      let handle: MapHandle | null = null;

      try {
        handle = await this.mapEngine.create(container, {
          styleUrl: environment.map.styleUrl,
          latitude,
          longitude,
          zoom: selection ? SELECTED_ZOOM : WORLD_VIEW.zoom,
        });
      } catch {
        handle = null;
      }

      if (handle === null) {
        this.mapUnavailable.set(true);
        return;
      }

      this.mapUnavailable.set(false);
      this.mapHandle = handle;
      handle.onClick((clickedLatitude, clickedLongitude) =>
        this.selectFromCoordinates(clickedLatitude, clickedLongitude),
      );

      if (selection) {
        handle.setMarker(selection.latitude, selection.longitude);
      }
    });
  }

  private teardownMap(): void {
    this.mapHandle?.destroy();
    this.mapHandle = null;
  }
}

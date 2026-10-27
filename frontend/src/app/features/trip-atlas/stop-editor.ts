import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
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
import { GeocodingService } from '../../core/api/geocoding.service';
import { describeHttpError } from '../../core/api/http-error';
import {
  PLACE_NOTES_MAX_LENGTH,
  PlaceCategory,
  PlaceSuggestion,
} from '../../core/models/place';
import {
  PLACE_ADDRESS_MAX_LENGTH,
  TripDestination,
  TripStop,
  TRIP_DESTINATION_MAX_LENGTH,
} from '../../core/models/trip';
import {
  PLACE_CATEGORIES_ALPHABETICALLY,
  placeCategoryLabel,
} from '../../shared/format/place-labels';
import { validatePlaceDay } from '../../shared/validate/place-day';

/** How long the editor waits after a keystroke before asking the geocoder. */
const SEARCH_DEBOUNCE_MS = 300;

/** What the stop editor hands back, before the screen writes the trip. */
export interface StopDraft {
  name: string;
  startsOn: string | null;
  endsOn: string | null;
  latitude: number | null;
  longitude: number | null;
  category: PlaceCategory | null;
  address: string | null;
  notes: string | null;
  plannedOn: string | null;
}

/**
 * Adds or edits a stop within a parent destination.
 *
 * A stop is a place: its name, position, category, address, notes and planned
 * day are all part of the same row. The stay dates are optional and separate
 * from the planned day: the stay is bounded by the destination when it has
 * dates, the planned day by the trip.
 *
 * The editor holds a draft. Nothing is written until the reader confirms; the
 * screen above owns the request and its failures.
 */
@Component({
  selector: 'app-stop-editor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './stop-editor.html',
  styleUrl: './stop-editor.css',
})
export class StopEditor {
  private readonly geocoding = inject(GeocodingService);
  private readonly destroyRef = inject(DestroyRef);

  readonly open = input(false);

  /** The destination the stop belongs to. */
  readonly parent = input<TripDestination | null>(null);

  /** The stop being changed, or null when one is being added. */
  readonly stop = input<TripStop | null>(null);

  /** The first day of the trip. */
  readonly tripStartsOn = input('');

  /** The last day of the trip. */
  readonly tripEndsOn = input('');

  readonly saving = input(false);

  readonly error = input<string | null>(null);

  readonly save = output<StopDraft>();

  readonly dismissed = output<void>();

  protected readonly categories = PLACE_CATEGORIES_ALPHABETICALLY;
  protected readonly categoryLabel = placeCategoryLabel;
  protected readonly nameMaxLength = TRIP_DESTINATION_MAX_LENGTH;
  protected readonly notesMaxLength = PLACE_NOTES_MAX_LENGTH;
  protected readonly attribution = environment.map.attribution;

  protected readonly editing = computed(() => this.stop() !== null);

  protected readonly title = computed(() => {
    if (this.editing()) {
      return `Edit ${this.stop()?.name ?? 'stop'}`;
    }

    const parent = this.parent();

    return parent === null ? 'Add a stop' : `Add a stop in ${parent.name}`;
  });

  // Search state, kept apart from the draft.
  protected readonly query = signal('');
  protected readonly results = signal<PlaceSuggestion[]>([]);
  protected readonly searching = signal(false);
  protected readonly searched = signal(false);
  protected readonly searchError = signal<string | null>(null);

  // The draft. Nothing here is persisted until it is confirmed.
  protected readonly name = signal('');
  protected readonly latitude = signal<number | null>(null);
  protected readonly longitude = signal<number | null>(null);
  protected readonly address = signal<string | null>(null);
  protected readonly category = signal<PlaceCategory | null>(null);
  protected readonly plannedOn = signal<string | null>(null);
  protected readonly startsOn = signal('');
  protected readonly endsOn = signal('');
  protected readonly notes = signal('');
  protected readonly submitted = signal(false);

  /** The position the draft carries, in words, so it can be checked by eye. */
  protected readonly coordinates = computed(() => {
    const latitude = this.latitude();
    const longitude = this.longitude();

    return latitude === null || longitude === null
      ? null
      : `${latitude.toFixed(4)}, ${longitude.toFixed(4)}`;
  });

  protected readonly nameError = computed(() => {
    if (!this.submitted()) {
      return null;
    }

    const value = this.name().trim();

    if (value.length === 0) {
      return 'This field is required.';
    }

    return value.length > TRIP_DESTINATION_MAX_LENGTH
      ? `Use at most ${TRIP_DESTINATION_MAX_LENGTH} characters.`
      : null;
  });

  /** The lower bound of the stay: the destination's start, or the trip's. */
  protected readonly lowerBound = computed(() => {
    const parent = this.parent();

    return parent?.startsOn ?? this.tripStartsOn();
  });

  /** The upper bound of the stay: the destination's end, or the trip's. */
  protected readonly upperBound = computed(() => {
    const parent = this.parent();

    return parent?.endsOn ?? this.tripEndsOn();
  });

  protected readonly lowerLabel = computed(() =>
    this.parent()?.startsOn != null ? 'the destination starts' : 'the trip starts',
  );

  protected readonly upperLabel = computed(() =>
    this.parent()?.endsOn != null ? 'the destination ends' : 'the trip ends',
  );

  /**
   * The stay's own dates. They are optional, but when they are given they must
   * fall inside the destination when it has dates, and inside the trip
   * everywhere else. Editing a stop never changes its destination's dates.
   */
  protected readonly dateError = computed(() => {
    const startsOn = this.startsOn();
    const endsOn = this.endsOn();

    if (startsOn.length > 0 && endsOn.length > 0 && endsOn < startsOn) {
      return 'The end date must be on or after the start date.';
    }

    const lower = this.lowerBound();
    const upper = this.upperBound();

    if (startsOn.length > 0 && lower.length > 0 && startsOn < lower) {
      return `The first day cannot be before ${this.lowerLabel()}.`;
    }

    if (startsOn.length > 0 && upper.length > 0 && startsOn > upper) {
      return `The first day cannot be after ${this.upperLabel()}.`;
    }

    if (endsOn.length > 0 && lower.length > 0 && endsOn < lower) {
      return `The last day cannot be before ${this.lowerLabel()}.`;
    }

    if (endsOn.length > 0 && upper.length > 0 && endsOn > upper) {
      return `The last day cannot be after ${this.upperLabel()}.`;
    }

    return null;
  });

  /**
   * The planned day is a calendar day of the trip, independent of the stay.
   * It is shown as soon as it is wrong, because the reader picked it from a
   * calendar; the API remains the authority.
   */
  protected readonly dayError = computed(() =>
    validatePlaceDay(this.tripStartsOn(), this.tripEndsOn(), this.plannedOn(), this.name()),
  );

  protected readonly canSave = computed(
    () =>
      this.name().trim().length > 0 &&
      this.name().trim().length <= TRIP_DESTINATION_MAX_LENGTH &&
      this.dayError() === null &&
      this.dateError() === null &&
      this.notes().trim().length <= PLACE_NOTES_MAX_LENGTH &&
      (this.address()?.length ?? 0) <= PLACE_ADDRESS_MAX_LENGTH,
  );

  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('dialog');
  private readonly searchInput = viewChild<ElementRef<HTMLInputElement>>('search');
  private readonly nameInput = viewChild<ElementRef<HTMLInputElement>>('nameInput');

  /** The element the focus returns to when the editor closes. */
  private previouslyFocused: HTMLElement | null = null;

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
          this.searchError.set(null);
        }),
        switchMap((term) =>
          this.geocoding.search(term).pipe(
            catchError((failure: HttpErrorResponse) => {
              // A search that fails is only a search that fails. The draft,
              // and anything already chosen, is left exactly as it was.
              this.searchError.set(describeHttpError(failure));
              return of<PlaceSuggestion[]>([]);
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((places) => {
        this.results.set(places);
        this.searching.set(false);
        this.searched.set(true);
      });

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

  protected onQuery(event: Event): void {
    const value = (event.target as HTMLInputElement).value;

    this.query.set(value);

    if (value.trim().length === 0) {
      this.results.set([]);
      this.searched.set(false);
      this.searching.set(false);
      this.searchError.set(null);
      return;
    }

    this.queries.next(value);
  }

  /**
   * Enter in the search box picks the first result rather than submitting the
   * draft. Without this it would submit the form the search sits inside,
   * which is never what the reader meant.
   */
  protected onSearchEnter(event: Event): void {
    event.preventDefault();

    const [first] = this.results();

    if (first !== undefined) {
      this.choose(first);
    }
  }

  /**
   * Fills the draft from a search result. The name is the place's own short
   * name and the address is the fuller line that tells it apart, so two
   * places with the same name remain distinct.
   */
  protected choose(place: PlaceSuggestion): void {
    this.name.set(place.name);
    this.latitude.set(place.latitude);
    this.longitude.set(place.longitude);
    this.address.set(place.label.slice(0, PLACE_ADDRESS_MAX_LENGTH));

    this.query.set('');
    this.results.set([]);
    this.searched.set(false);

    setTimeout(() => this.nameInput()?.nativeElement.focus());
  }

  protected onName(event: Event): void {
    this.name.set((event.target as HTMLInputElement).value);
  }

  protected onCategory(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;

    this.category.set(value === '' ? null : (value as PlaceCategory));
  }

  /** An emptied date field leaves the plan or the stay unset. */
  protected onPlannedOn(event: Event): void {
    const value = (event.target as HTMLInputElement).value;

    this.plannedOn.set(value === '' ? null : value);
  }

  protected onStartsOn(event: Event): void {
    this.startsOn.set((event.target as HTMLInputElement).value);
  }

  protected onEndsOn(event: Event): void {
    this.endsOn.set((event.target as HTMLInputElement).value);
  }

  protected onNotes(event: Event): void {
    this.notes.set((event.target as HTMLTextAreaElement).value);
  }

  protected submit(event: Event): void {
    event.preventDefault();
    this.submitted.set(true);

    if (!this.canSave()) {
      return;
    }

    const notes = this.notes().trim();

    this.save.emit({
      name: this.name().trim(),
      startsOn: this.startsOn() || null,
      endsOn: this.endsOn() || null,
      latitude: this.latitude(),
      longitude: this.longitude(),
      category: this.category(),
      address: this.address(),
      notes: notes.length > 0 ? notes : null,
      plannedOn: this.plannedOn(),
    });
  }

  protected dismiss(event?: Event): void {
    event?.preventDefault();
    this.dismissed.emit();
  }

  protected onBackdropClick(event: MouseEvent): void {
    if (event.target === this.dialog()?.nativeElement) {
      this.dismiss();
    }
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

    this.resetDraft();
    this.seedDraft();

    setTimeout(() => this.searchInput()?.nativeElement.focus());
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

    const target = this.previouslyFocused;
    this.previouslyFocused = null;
    setTimeout(() => target?.focus());
  }

  private resetDraft(): void {
    this.query.set('');
    this.results.set([]);
    this.searching.set(false);
    this.searched.set(false);
    this.searchError.set(null);
    this.submitted.set(false);

    this.name.set('');
    this.latitude.set(null);
    this.longitude.set(null);
    this.address.set(null);
    this.category.set(null);
    this.plannedOn.set(null);
    this.startsOn.set('');
    this.endsOn.set('');
    this.notes.set('');
  }

  private seedDraft(): void {
    const stop = this.stop();

    if (stop === null) {
      return;
    }

    this.name.set(stop.name);
    this.latitude.set(stop.latitude);
    this.longitude.set(stop.longitude);
    this.address.set(stop.address ?? null);
    this.category.set(stop.category ?? null);
    this.plannedOn.set(stop.plannedOn ?? null);
    this.startsOn.set(stop.startsOn ?? '');
    this.endsOn.set(stop.endsOn ?? '');
    this.notes.set(stop.notes ?? '');
  }
}

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
  map,
  of,
  Subject,
  switchMap,
  tap,
} from 'rxjs';
import { environment } from '../../../environments/environment';
import { GeocodingService } from '../../core/api/geocoding.service';
import { describeHttpError } from '../../core/api/http-error';
import { PlaceSuggestion } from '../../core/models/place';
import {
  TRIP_DESTINATION_MAX_LENGTH,
  TripDestination,
} from '../../core/models/trip';

/** How long the editor waits after a keystroke before asking the geocoder. */
const SEARCH_DEBOUNCE_MS = 300;

/** What the dialog hands back: a new destination or a new stop, not yet saved. */
export interface NewPlaceDraft {
  name: string;
  startsOn: string | null;
  endsOn: string | null;
  latitude: number | null;
  longitude: number | null;
}

/**
 * Adds a destination to the main route, or a stop within a chosen destination,
 * from the Atlas.
 *
 * It carries the name, the optional dates and, when the reader searches for a
 * place, its location. The screen above owns the request and its failures;
 * this only says what the reader wants added.
 */
@Component({
  selector: 'app-destination-editor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './destination-editor.html',
  styleUrl: './destination-editor.css',
})
export class DestinationEditor {
  private readonly geocoding = inject(GeocodingService);
  private readonly destroyRef = inject(DestroyRef);

  readonly open = input(false);

  /** Whether a destination on the main route or a stop within one is added. */
  readonly mode = input<'destination' | 'stop'>('destination');

  /** The destination a stop is added to. Null when adding a destination. */
  readonly parent = input<TripDestination | null>(null);

  /** The first day of the trip, which bounds a destination with no dates of its own. */
  readonly tripStartsOn = input('');

  /** The last day of the trip. */
  readonly tripEndsOn = input('');

  readonly saving = input(false);

  readonly error = input<string | null>(null);

  readonly save = output<NewPlaceDraft>();

  readonly dismissed = output<void>();

  protected readonly nameMaxLength = TRIP_DESTINATION_MAX_LENGTH;
  protected readonly attribution = environment.map.attribution;

  protected readonly name = signal('');
  protected readonly startsOn = signal('');
  protected readonly endsOn = signal('');
  protected readonly latitude = signal<number | null>(null);
  protected readonly longitude = signal<number | null>(null);
  protected readonly submitted = signal(false);

  // Search state, kept apart from the draft.
  protected readonly query = signal('');
  protected readonly results = signal<PlaceSuggestion[]>([]);
  protected readonly searching = signal(false);
  protected readonly searched = signal(false);
  protected readonly searchError = signal<string | null>(null);

  /** Typing, once it has settled, so the geocoder is not called per keystroke. */
  private readonly queries = new Subject<string>();

  protected readonly title = computed(() => {
    if (this.mode() === 'destination') {
      return 'Add a destination';
    }

    const parent = this.parent();

    return parent === null ? 'Add a stop' : `Add a stop at ${parent.name}`;
  });

  protected readonly lowerBound = computed(() => {
    const parent = this.parent();

    if (this.mode() === 'stop' && parent !== null && parent.startsOn !== null) {
      return parent.startsOn;
    }

    return this.tripStartsOn();
  });

  protected readonly upperBound = computed(() => {
    const parent = this.parent();

    if (this.mode() === 'stop' && parent !== null && parent.endsOn !== null) {
      return parent.endsOn;
    }

    return this.tripEndsOn();
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

  protected readonly dateError = computed(() => {
    const startsOn = this.startsOn();
    const endsOn = this.endsOn();

    if (startsOn.length > 0 && endsOn.length > 0 && endsOn < startsOn) {
      return 'The end date must be on or after the start date.';
    }

    const lower = this.lowerBound();
    const upper = this.upperBound();

    if (startsOn.length > 0 && lower.length > 0 && startsOn < lower) {
      return `The first day cannot be before ${this.mode() === 'stop' ? 'the destination starts' : 'the trip starts'}.`;
    }

    if (startsOn.length > 0 && upper.length > 0 && startsOn > upper) {
      return `The first day cannot be after ${this.mode() === 'stop' ? 'the destination ends' : 'the trip ends'}.`;
    }

    if (endsOn.length > 0 && lower.length > 0 && endsOn < lower) {
      return `The last day cannot be before ${this.mode() === 'stop' ? 'the destination starts' : 'the trip starts'}.`;
    }

    if (endsOn.length > 0 && upper.length > 0 && endsOn > upper) {
      return `The last day cannot be after ${this.mode() === 'stop' ? 'the destination ends' : 'the trip ends'}.`;
    }

    return null;
  });

  protected readonly canSave = computed(
    () =>
      this.name().trim().length > 0 &&
      this.name().trim().length <= TRIP_DESTINATION_MAX_LENGTH &&
      this.dateError() === null,
  );

  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('dialog');
  private readonly nameInput = viewChild<ElementRef<HTMLInputElement>>('nameInput');

  private previouslyFocused: HTMLElement | null = null;

  constructor() {
    this.queries
      .pipe(
        map((value) => value.trim()),
        debounceTime(SEARCH_DEBOUNCE_MS),
        tap(() => {
          this.searching.set(true);
          this.searchError.set(null);
        }),
        switchMap((term) =>
          this.geocoding.search(term).pipe(
            catchError((failure: HttpErrorResponse) => {
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
   * dialog the search sits inside.
   */
  protected onSearchEnter(event: Event): void {
    event.preventDefault();

    const [first] = this.results();

    if (first !== undefined) {
      this.choose(first);
    }
  }

  /**
   * Closes the overlay once the reader moves on to another field. The short
   * delay lets a click on a result land before the list is taken away.
   */
  protected onSearchFocusOut(event: FocusEvent): void {
    const container = event.currentTarget as HTMLElement;
    const next = event.relatedTarget as Node | null;

    if (next !== null && container.contains(next)) {
      return;
    }

    setTimeout(() => this.results.set([]));
  }

  /** Fills the name and location from a search result. */
  protected choose(place: PlaceSuggestion): void {
    this.name.set(place.destinationName);
    this.latitude.set(place.latitude);
    this.longitude.set(place.longitude);

    this.query.set('');
    this.results.set([]);
    this.searched.set(false);
    this.searching.set(false);

    setTimeout(() => this.nameInput()?.nativeElement.focus());
  }

  protected onName(event: Event): void {
    this.name.set((event.target as HTMLInputElement).value);

    // A name typed by hand no longer matches the place found by the search.
    this.latitude.set(null);
    this.longitude.set(null);
  }

  protected onStartsOn(event: Event): void {
    this.startsOn.set((event.target as HTMLInputElement).value);
  }

  protected onEndsOn(event: Event): void {
    this.endsOn.set((event.target as HTMLInputElement).value);
  }

  protected submit(event: Event): void {
    event.preventDefault();
    this.submitted.set(true);

    if (!this.canSave()) {
      return;
    }

    this.save.emit({
      name: this.name().trim(),
      startsOn: this.startsOn() || null,
      endsOn: this.endsOn() || null,
      latitude: this.latitude(),
      longitude: this.longitude(),
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

    this.name.set('');
    this.startsOn.set('');
    this.endsOn.set('');
    this.latitude.set(null);
    this.longitude.set(null);
    this.query.set('');
    this.results.set([]);
    this.searching.set(false);
    this.searched.set(false);
    this.searchError.set(null);
    this.submitted.set(false);

    setTimeout(() => this.nameInput()?.nativeElement.focus());
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
}

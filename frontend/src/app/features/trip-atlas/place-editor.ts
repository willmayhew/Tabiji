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
  PLACE_CATEGORIES,
  PLACE_NAME_MAX_LENGTH,
  PLACE_NOTES_MAX_LENGTH,
  PlaceCategory,
  PlaceSuggestion,
  SavedPlace,
  SavedPlaceDraft,
} from '../../core/models/place';
import { TripDestination } from '../../core/models/trip';

/** How long the editor waits after a keystroke before asking the geocoder. */
const SEARCH_DEBOUNCE_MS = 300;

/**
 * Adds a place to a trip, or changes one that is already saved.
 *
 * The editor holds a **draft**. Searching and choosing a result fills the
 * draft and nothing else: the place is written only when the reader confirms,
 * so backing out leaves the trip exactly as it was.
 *
 * The search talks to the geocoder through `GeocodingService`, the same
 * abstraction the map picker uses, so the provider can be replaced without
 * touching this dialog. The place itself is written by the screen above, which
 * owns the request and its failures — this only says what the reader wants
 * saved.
 */
@Component({
  selector: 'app-place-editor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './place-editor.html',
  styleUrl: './place-editor.css',
})
export class PlaceEditor {
  private readonly geocoding = inject(GeocodingService);
  private readonly destroyRef = inject(DestroyRef);

  /** Whether the editor is on screen. The parent owns this. */
  readonly open = input(false);

  /** The place being changed, or null when one is being added. */
  readonly place = input<SavedPlace | null>(null);

  /** The trip's stops, so a place can be filed under one of them. */
  readonly destinations = input<readonly TripDestination[]>([]);

  /** Whether the screen above is writing the place. */
  readonly saving = input(false);

  /** What the screen above was told when the write failed. */
  readonly error = input<string | null>(null);

  /** Emits the draft the reader confirmed. */
  readonly save = output<SavedPlaceDraft>();

  /** Emits when the reader backs out. Nothing is changed. */
  readonly dismissed = output<void>();

  protected readonly categories = PLACE_CATEGORIES;
  protected readonly nameMaxLength = PLACE_NAME_MAX_LENGTH;
  protected readonly notesMaxLength = PLACE_NOTES_MAX_LENGTH;
  protected readonly attribution = environment.map.attribution;

  protected readonly editing = computed(() => this.place() !== null);

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
  protected readonly destinationId = signal<string | null>(null);
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

    return value.length > PLACE_NAME_MAX_LENGTH
      ? `Use at most ${PLACE_NAME_MAX_LENGTH} characters.`
      : null;
  });

  protected readonly positionError = computed(() => {
    if (!this.submitted()) {
      return null;
    }

    return this.latitude() === null || this.longitude() === null
      ? 'Choose a place from the search first.'
      : null;
  });

  protected readonly canSave = computed(
    () => this.name().trim().length > 0 && this.latitude() !== null && this.longitude() !== null,
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

    // Only `open` decides whether the dialog is shown. Everything the opening
    // and closing do is untracked, so editing the draft never runs the effect
    // again and never resets what the reader has written.
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
   * places with the same name remain distinct in a list.
   */
  protected choose(place: PlaceSuggestion): void {
    this.name.set(place.name);
    this.latitude.set(place.latitude);
    this.longitude.set(place.longitude);
    this.address.set(place.label);

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

  protected onDestination(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;

    this.destinationId.set(value === '' ? null : value);
  }

  protected onNotes(event: Event): void {
    this.notes.set((event.target as HTMLTextAreaElement).value);
  }

  protected submit(event: Event): void {
    event.preventDefault();
    this.submitted.set(true);

    const latitude = this.latitude();
    const longitude = this.longitude();

    if (!this.canSave() || latitude === null || longitude === null) {
      return;
    }

    const notes = this.notes().trim();

    this.save.emit({
      name: this.name().trim(),
      latitude,
      longitude,
      category: this.category(),
      address: this.address(),
      notes: notes.length > 0 ? notes : null,
      destinationId: this.destinationId(),
    });
  }

  protected dismiss(event?: Event): void {
    event?.preventDefault();
    this.dismissed.emit();
  }

  /** A click on the dialog's backdrop, but not on the panel itself. */
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
    this.destinationId.set(null);
    this.notes.set('');
  }

  private seedDraft(): void {
    const place = this.place();

    if (place === null) {
      return;
    }

    this.name.set(place.name);
    this.latitude.set(place.latitude);
    this.longitude.set(place.longitude);
    this.address.set(place.address);
    this.category.set(place.category);
    this.destinationId.set(this.knownDestinationId(place.destinationId));
    this.notes.set(place.notes ?? '');
  }

  /**
   * A place filed under a stop that has since gone is offered as unassigned,
   * so the selector cannot show a stop the trip no longer has.
   */
  private knownDestinationId(destinationId: string | null): string | null {
    if (destinationId === null) {
      return null;
    }

    return this.destinations().some((stop) => stop.id === destinationId) ? destinationId : null;
  }
}

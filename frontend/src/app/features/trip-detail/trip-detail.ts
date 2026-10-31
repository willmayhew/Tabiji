import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, map, of, startWith, Subject, switchMap, tap } from 'rxjs';
import { describeHttpError } from '../../core/api/http-error';
import { TripsService } from '../../core/api/trips.service';
import { SessionService } from '../../core/auth/session.service';
import { MemberRole } from '../../core/models/member';
import { Trip, TripDestination, TripStatus } from '../../core/models/trip';
import {
  formatDestinationDates,
  formatTripDate,
  formatTripDates,
} from '../../shared/format/trip-dates';
import { routeSummary } from '../../shared/format/trip-destinations';
import { TripForm } from '../../shared/form/trip-form';
import { SignInPrompt } from '../../shared/ui/sign-in-prompt';
import { BadgeTone, StatusBadge } from '../../shared/ui/status-badge';
import { TripMembers } from './trip-members';

interface TripDetailResult {
  trip: Trip | null;
  error: string | null;
  notFound: boolean;
  signedOut: boolean;
}

@Component({
  selector: 'app-trip-detail',
  imports: [RouterLink, StatusBadge, TripForm, SignInPrompt, TripMembers],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-detail.html',
  styleUrl: './trip-detail.css',
})
export class TripDetail {
  private readonly tripsService = inject(TripsService);
  private readonly session = inject(SessionService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  /** Emits when the reader asks to try the request again. */
  private readonly retry = new Subject<void>();

  readonly trip = signal<Trip | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notFound = signal(false);

  /** Set when the API refused the request for want of a session. */
  readonly signedOut = signal(false);

  /** The trip's id, so the way back works even before the trip has loaded. */
  private readonly routeId = signal('');

  /** Where signing in comes back to. */
  protected readonly returnUrl = computed(() => `/trips/${this.routeId()}`);

  /** The caller's part in the trip, when the API has told us. */
  protected readonly viewerRole = computed(() => this.trip()?.viewerRole ?? null);

  /**
   * Only an owner may destroy a trip. The API refuses everybody else; this
   * only keeps the control away until the role is known, so an editor is
   * never offered something the trip will refuse.
   */
  protected readonly canDelete = computed(() => this.viewerRole() === 'Owner');

  /** Whether the trip is being changed. */
  readonly editing = signal(false);

  /** Set once the trip is deleted, so the unsaved-changes guard stays out of the way. */
  private readonly deleted = signal(false);

  /** The editor, when the page is in edit mode. */
  private readonly tripForm = viewChild(TripForm);

  constructor() {
    this.route.paramMap
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.trip.set(null);
          this.error.set(null);
          this.notFound.set(false);
          this.signedOut.set(false);
          this.editing.set(false);
          this.deleted.set(false);
        }),
        map((params) => {
          const id = params.get('id') ?? '';
          this.routeId.set(id);

          return id;
        }),
        switchMap((id) =>
          this.retry.pipe(
            startWith(undefined),
            switchMap(() =>
              this.tripsService.getById(id).pipe(
                map((trip): TripDetailResult => ({
                  trip,
                  error: null,
                  notFound: false,
                  signedOut: false,
                })),
                catchError((failure: HttpErrorResponse) =>
                  of<TripDetailResult>({
                    trip: null,
                    error: describeHttpError(failure),
                    notFound: failure.status === 404,
                    signedOut: failure.status === 401,
                  }),
                ),
              ),
            ),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => {
        this.trip.set(result.trip);
        this.error.set(result.error);
        this.notFound.set(result.notFound);
        this.signedOut.set(result.signedOut);
        this.loading.set(false);

        if (result.signedOut) {
          this.session.markSignedOut();
        }
      });
  }

  protected retryLoad(): void {
    this.loading.set(true);
    this.error.set(null);
    this.retry.next();
  }

  /** Starts changing the trip. */
  protected startEditing(): void {
    this.editing.set(true);
  }

  /** Backs out of a change without saving it. */
  protected cancelEditing(): void {
    this.editing.set(false);
  }

  /** Takes the trip the editor saved. */
  protected onSaved(trip: Trip): void {
    this.trip.set(trip);
    this.editing.set(false);
  }

  protected onDeleted(): void {
    this.deleted.set(true);
    this.editing.set(false);
    void this.router.navigate(['/trips']);
  }

  /** Takes a role the members panel changed, so the page's controls follow. */
  protected onViewerRoleChanged(role: MemberRole): void {
    const trip = this.trip();

    if (trip !== null) {
      this.trip.set({ ...trip, viewerRole: role });
    }
  }

  /** The caller removed themselves from the trip. */
  protected onLeftTrip(): void {
    this.deleted.set(true);
    void this.router.navigate(['/trips']);
  }

  /**
   * Whether the reader has a change they have not saved. The guard on the
   * route asks about it before the reader goes anywhere else.
   */
  hasUnsavedChanges(): boolean {
    return !this.deleted() && this.editing() && (this.tripForm()?.isDirty() ?? false);
  }

  protected readonly dates = computed(() => {
    const found = this.trip();

    return found === null ? null : formatTripDates(found.startsOn, found.endsOn);
  });

  protected readonly startDate = computed(() => {
    const found = this.trip();

    return found === null ? null : formatTripDate(found.startsOn);
  });

  protected readonly endDate = computed(() => {
    const found = this.trip();

    return found === null ? null : formatTripDate(found.endsOn);
  });

  /** How many stops the route has, in words. */
  protected readonly routeSummary = computed(() => {
    const found = this.trip();

    return found === null ? '' : routeSummary(found.destinations);
  });

  /**
   * How many stops have dates, so that a partly planned trip does not look
   * as though it were fully scheduled.
   */
  protected readonly datedStops = computed(() => {
    const found = this.trip();

    if (found === null || found.destinations.length === 0) {
      return null;
    }

    const dated = found.destinations.filter(
      (destination) => destination.startsOn !== null || destination.endsOn !== null,
    ).length;

    return dated === 0
      ? null
      : `Dates are set for ${dated} of ${found.destinations.length} destinations`;
  });

  /** The dates of one stop, or a note that they have not been decided. */
  protected destinationDates(destination: TripDestination): string {
    return formatDestinationDates(destination.startsOn, destination.endsOn) ?? 'Dates not decided';
  }

  /** Whether a stop has any dates at all. */
  protected hasDates(destination: TripDestination): boolean {
    return destination.startsOn !== null || destination.endsOn !== null;
  }

  protected toneFor(status: TripStatus): BadgeTone {
    switch (status) {
      case 'Confirmed':
        return 'healthy';
      case 'Cancelled':
        return 'danger';
      default:
        return 'neutral';
    }
  }
}

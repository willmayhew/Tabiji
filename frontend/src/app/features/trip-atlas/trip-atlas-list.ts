import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
} from '@angular/core';
import type { PlaceCategory } from '../../core/models/place';
import { stopsOf, TripDestination, TripStop } from '../../core/models/trip';
import {
  formatDestinationDates,
  formatPlanDay,
  formatTripDates,
} from '../../shared/format/trip-dates';
import { parentLabel, placeCategoryLabel, stopLabel } from '../../shared/format/place-labels';
import { routeSummary } from '../../shared/format/trip-destinations';
import { AtlasSelection } from '../../shared/map/map-engine';
import { groupStopsByDay } from './plan-days';

/**
 * The trip as a list, and the accessible half of the atlas.
 *
 * The Route is the main route: destinations, each with its own stops nested
 * beneath it. A stop is a place within its destination and is labelled
 * hierarchically (1a, 1b, ...), never with a main-route number. The plan is a
 * second reading of the same places, grouped by the day they are planned for.
 *
 * Every destination, stop and legacy unfiled place is a button, so selecting
 * one is reachable from the keyboard and everything on the map can be read
 * here instead. The list and the map share one selection and one expansion
 * state; neither owns them.
 */
@Component({
  selector: 'app-trip-atlas-list',
  imports: [NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-atlas-list.html',
  styleUrl: './trip-atlas-list.css',
})
export class TripAtlasList {
  readonly destinations = input<readonly TripDestination[]>([]);

  /** The legacy saved places that are not yet in a destination. */
  readonly unfiledPlaces = input<readonly TripStop[]>([]);

  /** The first day of the trip, so a planned day can be measured against it. */
  readonly startsOn = input('');

  /** The last day of the trip. Both ends of a trip are themselves days of it. */
  readonly endsOn = input('');

  /** The destination, stop or legacy place the map and the list agree is selected. */
  readonly selection = input<AtlasSelection | null>(null);

  /** Which destinations have their stops open. The screen owns this. */
  readonly expanded = input<ReadonlySet<string>>(new Set());

  readonly select = output<AtlasSelection>();

  readonly addDestination = output<void>();

  readonly addStop = output<TripDestination>();

  readonly toggleDestination = output<string>();

  /** A legacy unfiled place and the destination the reader wants to file it under. */
  readonly filePlace = output<{ place: TripStop; parentId: string }>();

  protected readonly ordered = computed(() =>
    [...this.destinations()].sort((left, right) => left.sortOrder - right.sortOrder),
  );

  protected readonly unfiled = computed(() =>
    [...this.unfiledPlaces()].sort((left, right) => left.sortOrder - right.sortOrder),
  );

  protected readonly plan = computed(() =>
    groupStopsByDay(
      this.startsOn(),
      this.endsOn(),
      this.ordered(),
      this.unfiled(),
    ),
  );

  protected readonly hasPlan = computed(
    () =>
      this.ordered().some((destination) =>
        stopsOf(destination).some((stop) => stop.plannedOn != null),
      ) || this.unfiled().some((place) => place.plannedOn != null),
  );

  protected readonly planNote = computed(() => {
    const planned = this.plan().planned.length;
    const range = formatTripDates(this.startsOn(), this.endsOn());

    if (planned === 0 || range === null) {
      return null;
    }

    return `${planned} of ${range.days} ${range.days === 1 ? 'day' : 'days'} planned`;
  });

  protected readonly outsideCount = computed(() =>
    this.plan().outside.reduce((total, group) => total + group.places.length, 0),
  );

  protected readonly unplannedCount = computed(() => this.plan().unplanned.length);

  protected readonly locatedCount = computed(
    () => this.ordered().filter((destination) => this.isLocated(destination)).length,
  );

  protected readonly summary = computed(() => routeSummary(this.ordered()));

  protected readonly mapNote = computed(() => {
    const total = this.ordered().length;
    const located = this.locatedCount();

    if (total === 0 || located === total) {
      return null;
    }

    return located === 0
      ? 'No destination is on the map yet'
      : `${located} of ${total} destinations are on the map`;
  });

  protected stopsOf(destination: TripDestination): TripStop[] {
    return stopsOf(destination);
  }

  protected parentLabel(index: number): string {
    return parentLabel(index);
  }

  protected stopLabel(destinationIndex: number, stopIndex: number): string {
    return stopLabel(destinationIndex, stopIndex);
  }

  protected categoryLabel(category: PlaceCategory): string {
    return placeCategoryLabel(category);
  }

  protected stopPosition(destination: TripDestination, stop: TripStop): number {
    return stopsOf(destination).findIndex((candidate) => candidate.id === stop.id);
  }

  protected isLocated(place: TripStop): boolean {
    return place.latitude !== null && place.longitude !== null;
  }

  protected datesFor(place: TripStop): string {
    return formatDestinationDates(place.startsOn, place.endsOn) ?? 'Dates not decided';
  }

  protected hasDates(place: TripStop): boolean {
    return place.startsOn !== null || place.endsOn !== null;
  }

  protected isSelected(kind: AtlasSelection['kind'], id: string): boolean {
    const selection = this.selection();

    return selection !== null && selection.kind === kind && selection.id === id;
  }

  protected stopCount(destination: TripDestination): string {
    const count = stopsOf(destination).length;

    return count === 1 ? '1 stop' : `${count} stops`;
  }

  protected isExpanded(destination: TripDestination): boolean {
    return this.expanded().has(destination.id);
  }

  protected stopsPanelId(destination: TripDestination): string {
    return `atlas-stops-${destination.id}`;
  }

  protected selectDestination(destination: TripDestination): void {
    this.select.emit({ kind: 'destination', id: destination.id });
  }

  protected selectStop(stop: TripStop): void {
    this.select.emit({ kind: 'stop', id: stop.id });
  }

  protected selectUnfiled(place: TripStop): void {
    this.select.emit({ kind: 'place', id: place.id });
  }

  protected toggle(destination: TripDestination): void {
    this.toggleDestination.emit(destination.id);
  }

  protected requestStop(destination: TripDestination): void {
    this.addStop.emit(destination);
  }

  protected fileUnfiled(place: TripStop, parentId: string): void {
    if (parentId.length > 0) {
      this.filePlace.emit({ place, parentId });
    }
  }

  /** A day in words, or the raw value when it is not a plain date. */
  protected dayLabel(date: string): string {
    return formatPlanDay(date) ?? date;
  }

  protected dayHeadingId(date: string): string {
    return `atlas-plan-day-${date}`;
  }

  protected readonly outsideHeadingId = 'atlas-plan-outside';

  protected placesLabel(count: number): string {
    return count === 1 ? '1 place' : `${count} places`;
  }

  /** The parent context of a place in the plan, when it has one. */
  protected parentOf(place: TripStop): TripDestination | null {
    return (
      this.ordered().find((destination) =>
        stopsOf(destination).some((stop) => stop.id === place.id),
      ) ?? null
    );
  }
}

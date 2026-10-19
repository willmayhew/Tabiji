import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Trip, TripStatus } from '../../core/models/trip';
import { formatTripDates } from '../format/trip-dates';
import { BadgeTone, StatusBadge } from './status-badge';
import { DestinationPhoto } from './destination-photo';

/**
 * The summary of a trip: its photograph, name, destination, dates and status.
 * Used by the trips list, and reusable wherever a trip is listed.
 */
@Component({
  selector: 'app-trip-card',
  imports: [DestinationPhoto, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-card.html',
  styleUrl: './trip-card.css',
})
export class TripCard {
  readonly trip = input.required<Trip>();

  protected readonly dates = computed(() => formatTripDates(this.trip().startsOn, this.trip().endsOn));

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

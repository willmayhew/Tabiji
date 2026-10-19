import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Trip, TripStatus } from '../../core/models/trip';
import { BadgeTone, StatusBadge } from './status-badge';

@Component({
  selector: 'app-trip-card',
  imports: [StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './trip-card.html',
  styleUrl: './trip-card.css',
})
export class TripCard {
  readonly trip = input.required<Trip>();

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

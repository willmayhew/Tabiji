import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import {
  PlateAspect,
  PlateTone,
  plateDimensions,
  plateTone,
  routeMotif,
} from '../media/route-motif';

/**
 * The longest name the plate will set. The plate's label is decoration: the
 * real name is always present as text beside it, so trimming here loses
 * nothing.
 */
const MAX_LABEL_LENGTH = 48;

/**
 * The plate that stands where a photograph used to.
 *
 * A plate carries a ground, a faint graticule, a route motif and a label set
 * in type. The motif is derived from the seed — the trip's id — so every trip
 * draws its own and no trip can be shown a picture of the wrong place.
 *
 * The label is whatever the page wants said: the first stop of the journey on
 * the trip page, the title of the trip on a card.
 *
 * The whole block is decorative. The label and the index are always repeated
 * as real text beside the plate, so the plate is marked `aria-hidden` and the
 * reader hears each fact exactly once.
 */
@Component({
  selector: 'app-destination-plate',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './destination-plate.html',
  styleUrl: './destination-plate.css',
})
export class DestinationPlate {
  /** What the motif is drawn from — the trip's id. */
  readonly seed = input.required<string>();

  /** The label, set in type on the plate. */
  readonly label = input<string | null>(null);

  /** The position of the trip in a list, shown as "No. 03". */
  readonly index = input<number | null>(null);

  /**
   * How many stops the trip has. The plate draws a waypoint for each, so the
   * graphic mirrors the shape of the route.
   */
  readonly stops = input(1);

  /** The shape of the plate. */
  readonly aspect = input<PlateAspect>('card');

  /** Overrides the ground the plate is drawn on. */
  readonly tone = input<PlateTone | null>(null);

  protected readonly resolvedTone = computed<PlateTone>(
    () => this.tone() ?? plateTone(this.seed()),
  );

  protected readonly motif = computed(() => routeMotif(this.seed(), this.stops(), this.aspect()));

  protected readonly viewBox = computed(() => {
    const { width, height } = plateDimensions(this.aspect());

    return `0 0 ${width} ${height}`;
  });

  protected readonly plateClass = computed(
    () => `plate plate--${this.resolvedTone()} plate--${this.aspect()}`,
  );

  protected readonly text = computed(() => {
    const label = this.label();

    if (label === null || label.length === 0) {
      return null;
    }

    return label.length <= MAX_LABEL_LENGTH
      ? label
      : `${label.slice(0, MAX_LABEL_LENGTH - 1).trimEnd()}…`;
  });

  protected readonly indexLabel = computed(() => {
    const index = this.index();

    return index === null ? null : `No. ${index < 10 ? `0${index}` : index}`;
  });
}

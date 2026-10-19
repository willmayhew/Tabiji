import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DestinationPhotoAsset, resolveDestinationPhoto } from '../media/destination-photos';

/**
 * The photograph that belongs to a destination, or the 旅 monogram when the
 * destination has no photograph. The component owns the fallback so no
 * feature has to know whether a photo exists.
 */
@Component({
  selector: 'app-destination-photo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './destination-photo.html',
  styleUrl: './destination-photo.css',
})
export class DestinationPhoto {
  /** The free text destination of the trip. */
  readonly destination = input.required<string>();

  /** The `sizes` attribute for the responsive image. */
  readonly sizes = input('100vw');

  /** Whether the browser should load the image lazily. */
  readonly lazy = input(true);

  protected readonly photo = computed<DestinationPhotoAsset | null>(() =>
    resolveDestinationPhoto(this.destination()),
  );
}

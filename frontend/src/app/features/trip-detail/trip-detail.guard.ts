import type { TripDetail } from './trip-detail';

/**
 * Asks the reader before they leave a trip they have changed.
 *
 * The prompt is the browser's own, so that no new dialog has to be built and
 * no new focus has to be trapped. The page only asks when it is actually
 * being edited and something has actually been changed, so reading a trip and
 * going elsewhere is never interrupted.
 */
export const unsavedChangesGuard = (detail: TripDetail): boolean =>
  detail.hasUnsavedChanges()
    ? window.confirm('You have unsaved changes. Leave without saving?')
    : true;

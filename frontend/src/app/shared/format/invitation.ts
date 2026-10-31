import { InvitationStatus } from '../../core/models/member';
import { BadgeTone } from '../ui/status-badge';

/**
 * The tone an invitation's status is drawn with. The trips page, the
 * invitation page and the travellers panel all show the same statuses, so
 * the mapping lives here rather than in each of them.
 */
export function invitationTone(status: InvitationStatus): BadgeTone {
  switch (status) {
    case 'Pending':
      return 'warning';
    case 'Accepted':
      return 'healthy';
    case 'Declined':
    case 'Revoked':
      return 'danger';
    default:
      return 'neutral';
  }
}

const DAY = 86_400_000;

/**
 * How long a pending invitation has left, in words, beside the date. The
 * absolute date stays the authority; this is the reading aid.
 */
export function expiryHint(expiresAt: string): string {
  const days = Math.floor((new Date(expiresAt).getTime() - Date.now()) / DAY);

  if (days <= 0) {
    return 'today';
  }

  if (days === 1) {
    return 'tomorrow';
  }

  return `in ${days} days`;
}

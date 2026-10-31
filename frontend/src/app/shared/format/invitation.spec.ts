import { InvitationStatus } from '../../core/models/member';
import { expiryHint, invitationTone } from './invitation';

describe('invitationTone', () => {
  it('should warn while an invitation can still be accepted', () => {
    expect(invitationTone('Pending')).toBe('warning');
  });

  it('should mark an accepted invitation healthy', () => {
    expect(invitationTone('Accepted')).toBe('healthy');
  });

  it('should mark a refused invitation dangerous', () => {
    expect(invitationTone('Declined')).toBe('danger');
    expect(invitationTone('Revoked')).toBe('danger');
  });

  it('should stay neutral for the rest', () => {
    expect(invitationTone('Expired')).toBe('neutral');
  });
});

describe('expiryHint', () => {
  const day = 86_400_000;

  it('should say today when the invitation ends within the day', () => {
    expect(expiryHint(new Date(Date.now() + 3_600_000).toISOString())).toBe('today');
  });

  it('should say tomorrow when it ends on the next day', () => {
    expect(expiryHint(new Date(Date.now() + day).toISOString())).toBe('tomorrow');
  });

  it('should count the days otherwise', () => {
    expect(expiryHint(new Date(Date.now() + 6 * day).toISOString())).toBe('in 6 days');
  });
});

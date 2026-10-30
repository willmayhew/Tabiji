/**
 * A person's part in a trip.
 *
 * An owner manages the trip and its membership; an editor changes the
 * journey. The API is the authority; the client only uses this to decide
 * which controls to offer.
 */
export type MemberRole = 'Owner' | 'Editor';

/** Where an invitation is in its life. */
export type InvitationStatus = 'Pending' | 'Accepted' | 'Declined' | 'Revoked' | 'Expired';

export interface TripMember {
  userId: string;
  email: string;
  role: MemberRole;
  grantedAt: string;
}

export interface TripMembers {
  viewerRole: MemberRole;
  members: TripMember[];
}

export interface TripInvitation {
  id: string;
  tripId: string;
  email: string;
  role: MemberRole;
  status: InvitationStatus;
  createdAt: string;
  expiresAt: string;
  invitedByEmail: string;
}

/**
 * The invitation the API just created. `invitationUrl` is only present in
 * Development, where there is no email provider; it carries the token in the
 * URL fragment.
 */
export interface CreatedInvitation extends TripInvitation {
  invitationUrl: string | null;
}

/** An invitation waiting for the signed-in account, for the trips page. */
export interface RecipientInvitation {
  id: string;
  tripId: string;
  tripName: string;
  invitedByEmail: string;
  role: MemberRole;
  status: InvitationStatus;
  expiresAt: string;
}

/** What the invitation link shows before the recipient decides. */
export interface InvitationPreview {
  tripId: string;
  tripName: string;
  invitedByEmail: string;
  role: MemberRole;
  status: InvitationStatus;
  expiresAt: string;
  canAccept: boolean;
}

/** The trip an accepted invitation gave the reader access to. */
export interface AcceptedInvitation {
  tripId: string;
  role: MemberRole;
}

export function isActionable(status: InvitationStatus): boolean {
  return status === 'Pending';
}

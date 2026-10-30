namespace Tabiji.Application.Membership;

/// <summary>What the invitation link may show, if it belongs to this account.</summary>
public sealed record InvitationPreviewResult(
    InvitationPreviewOutcome Outcome,
    InvitationPreviewResponse? Preview = null);

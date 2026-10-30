namespace Tabiji.Application.Membership;

/// <summary>The outcome of declining or revoking an invitation.</summary>
public sealed record InvitationActionResult(InvitationActionOutcome Outcome);

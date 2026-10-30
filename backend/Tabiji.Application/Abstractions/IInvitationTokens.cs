namespace Tabiji.Application.Abstractions;

/// <summary>
/// A freshly issued invitation token and the hash that is safe to store.
/// </summary>
public sealed record IssuedInvitationToken(string Token, byte[] Hash);

/// <summary>
/// Makes and hashes invitation tokens.
///
/// The raw token is shown once, to the inviter in development; only the hash
/// is ever stored, and the same hash is what an acceptance looks up.
/// </summary>
public interface IInvitationTokens
{
    IssuedInvitationToken Issue();

    byte[] Hash(string token);
}

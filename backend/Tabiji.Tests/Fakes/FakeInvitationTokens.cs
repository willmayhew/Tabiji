using System.Security.Cryptography;
using System.Text;
using Tabiji.Application.Abstractions;

namespace Tabiji.Tests.Fakes;

/// <summary>
/// A deterministic token source, so a test can assert the exact token the
/// service hands out and the exact hash it sends to the repository.
/// </summary>
internal sealed class FakeInvitationTokens : IInvitationTokens
{
    public IssuedInvitationToken Next { get; set; } = new(
        "test-invitation-token",
        [.. Enumerable.Range(0, 32).Select(value => (byte)value)]);

    public IssuedInvitationToken Issue() => Next;

    public byte[] Hash(string token) =>
        SHA256.HashData(Encoding.ASCII.GetBytes(token));
}

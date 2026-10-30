using System.Security.Cryptography;
using System.Text;
using Tabiji.Application.Abstractions;

namespace Tabiji.Infrastructure.Security;

/// <summary>
/// Invitation tokens from the operating system's cryptographic random source.
///
/// Thirty-two bytes are 256 bits of entropy, encoded as forty-three URL-safe
/// characters. Only the SHA-256 of the token is stored: the token is a bearer
/// credential, and a database read must not be enough to use one.
/// </summary>
public sealed class InvitationTokens : IInvitationTokens
{
    private const int TokenBytes = 32;

    public IssuedInvitationToken Issue()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenBytes);
        var token = Encode(bytes);

        return new IssuedInvitationToken(token, Hash(token));
    }

    public byte[] Hash(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("A token is required.", nameof(token));
        }

        return SHA256.HashData(Encoding.ASCII.GetBytes(token));
    }

    private static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}

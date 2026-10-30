namespace Tabiji.Domain.Invitations;

/// <summary>
/// The address an invitation is sent to.
///
/// It keeps the trimmed address for display and an upper-invariant form for
/// matching. That is the same normalisation ASP.NET Identity applies to
/// <c>AspNetUsers.NormalizedEmail</c>, so an invitation can be matched to an
/// account by the database rather than by application string handling.
///
/// It checks shape, not deliverability: an address may be invited before the
/// account exists.
/// </summary>
public readonly record struct EmailAddress
{
    public const int MaxLength = 256;

    private EmailAddress(string value, string normalized)
    {
        Value = value;
        Normalized = normalized;
    }

    /// <summary>The trimmed address as it was entered.</summary>
    public string Value { get; }

    /// <summary>The address in the form Identity stores for matching.</summary>
    public string Normalized { get; }

    public static EmailAddress Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("An address is required.", nameof(value));
        }

        var trimmed = value.Trim();

        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException(
                $"An address may be at most {MaxLength} characters.",
                nameof(value));
        }

        if (trimmed.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("An address cannot contain whitespace.", nameof(value));
        }

        var at = trimmed.IndexOf('@');

        if (at <= 0 || at != trimmed.LastIndexOf('@') || at == trimmed.Length - 1)
        {
            throw new ArgumentException("An address must contain one '@'.", nameof(value));
        }

        return new EmailAddress(trimmed, trimmed.ToUpperInvariant());
    }

    public override string ToString() => Value;
}

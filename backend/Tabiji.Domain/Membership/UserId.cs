namespace Tabiji.Domain.Membership;

/// <summary>
/// The identity of an account, as the travel side of the application sees it.
///
/// It is deliberately a value of its own rather than a bare <see cref="Guid"/>:
/// a trip identifier and an account identifier are not interchangeable, and a
/// signature that asks for one must not accept the other. No identity type
/// crosses into the domain — the accounts themselves live in Infrastructure.
/// </summary>
public readonly record struct UserId(Guid Value)
{
    /// <summary>
    /// Wraps an account identifier, refusing the empty one.
    /// </summary>
    public static UserId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An account id cannot be empty.", nameof(value));
        }

        return new UserId(value);
    }

    public override string ToString() => Value.ToString("D");
}

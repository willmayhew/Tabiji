using Tabiji.Domain.Membership;

namespace Tabiji.Tests;

/// <summary>
/// The accounts the tests act as.
///
/// A trip is private to its owner, so a test that signs in has to name one.
/// Three is enough for collaboration: the owner, an invitee, and somebody who
/// must never see the trip at all.
/// </summary>
internal static class TestUsers
{
    internal static readonly Guid OwnerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    internal static readonly Guid OtherId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    internal static readonly Guid StrangerId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    internal static readonly UserId Owner = UserId.Create(OwnerId);

    internal static readonly UserId Other = UserId.Create(OtherId);

    internal static readonly UserId Stranger = UserId.Create(StrangerId);
}

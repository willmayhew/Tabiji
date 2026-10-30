using Tabiji.Domain.Membership;
using Tabiji.Domain.Trips;

namespace Tabiji.Application.Trips;

/// <summary>
/// A trip and the caller's part in it, loaded together so a caller never
/// receives a trip without knowing what they may do with it.
/// </summary>
public sealed record TripAccess(Trip Trip, TripMemberRole Role);

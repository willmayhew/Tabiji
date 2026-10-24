using Tabiji.Domain.Trips;

namespace Tabiji.Application.Trips;

/// <summary>
/// A request to save a place to a trip.
///
/// The coordinates are nullable so that a request missing one is refused by
/// name rather than quietly read as zero, which is a real place in the Gulf
/// of Guinea. A saved place always has a position; a destination does not.
///
/// <paramref name="DestinationId"/> files the place under one of the trip's
/// stops, and may be left out.
/// </summary>
public sealed record CreateSavedPlaceRequest(
    string Name,
    double? Latitude,
    double? Longitude,
    PlaceCategory? Category = null,
    string? Address = null,
    string? Notes = null,
    Guid? DestinationId = null);

namespace Tabiji.Application.Trips;

/// <summary>
/// What happened when a trip was deleted. Only an owner may delete the trip
/// itself, so a member with another role gets <see cref="Forbidden"/> rather
/// than a misleading 404 for a trip they can see.
/// </summary>
public enum TripDeleteOutcome
{
    Deleted = 0,
    NotFound = 1,
    Forbidden = 2
}

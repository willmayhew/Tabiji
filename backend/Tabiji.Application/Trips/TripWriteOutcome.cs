namespace Tabiji.Application.Trips;

/// <summary>
/// What happened when a trip was written back.
///
/// The three are kept apart because the endpoint answers them differently:
/// an unknown trip and a caller who cannot reach it are both 404, while a
/// member whose role may not edit is 403.
/// </summary>
public enum TripWriteOutcome
{
    Written = 0,
    NotFound = 1,
    Forbidden = 2
}

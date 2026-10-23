namespace Tabiji.Domain.Trips;

/// <summary>
/// A stop on a trip's route.
///
/// A destination belongs to exactly one trip and carries its place in that
/// trip's order. The name is free text so that any place in the world can be
/// named; it is never parsed and never compared against a list of countries.
///
/// The dates describe the planned stay at the stop. Both are optional: a
/// stop can be added long before its dates are known, and either one can be
/// left out on its own.
///
/// The coordinates describe where the place is, and are optional too: a
/// place chosen from a map carries them, and a place typed by hand carries
/// none. They are never used to derive the name, and the name is never
/// derived from them.
/// </summary>
public sealed class TripDestination
{
    public const int NameMaxLength = 200;

    private TripDestination()
    {
    }

    private TripDestination(
        Guid id,
        Guid tripId,
        string name,
        DateOnly? startsOn,
        DateOnly? endsOn,
        double? latitude,
        double? longitude,
        int sortOrder)
    {
        Id = id;
        TripId = tripId;
        Name = name;
        StartsOn = startsOn;
        EndsOn = endsOn;
        Latitude = latitude;
        Longitude = longitude;
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }

    public Guid TripId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// The planned first day at this stop. Null when the day has not been
    /// decided yet.
    /// </summary>
    public DateOnly? StartsOn { get; private set; }

    /// <summary>
    /// The planned last day at this stop. Null when the day has not been
    /// decided yet.
    /// </summary>
    public DateOnly? EndsOn { get; private set; }

    /// <summary>
    /// The latitude of the place, when it was chosen from a map. Null when
    /// the place was typed by hand, which is always allowed.
    /// </summary>
    public double? Latitude { get; private set; }

    /// <summary>
    /// The longitude of the place, when it was chosen from a map. Null when
    /// the place was typed by hand, which is always allowed.
    /// </summary>
    public double? Longitude { get; private set; }

    /// <summary>
    /// The position of this stop on its trip's route, counting from zero.
    /// </summary>
    public int SortOrder { get; private set; }

    /// <summary>
    /// Creates a stop. Internal because the owning trip is the only thing
    /// allowed to make one, and it is the only thing that can give it a place
    /// on a route.
    /// </summary>
    internal static TripDestination Create(
        Guid tripId,
        string name,
        DateOnly? startsOn,
        DateOnly? endsOn,
        double? latitude,
        double? longitude,
        int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentException(
                "The destination sort order cannot be negative.",
                nameof(sortOrder));
        }

        ValidateCoordinates(latitude, longitude);

        return new TripDestination(
            Guid.NewGuid(),
            tripId,
            ValidateName(name),
            startsOn,
            endsOn,
            latitude,
            longitude,
            sortOrder);
    }

    /// <summary>
    /// Gives this stop a new name. Internal because only the owning trip
    /// decides what its stops are called.
    /// </summary>
    internal void Rename(string name)
    {
        Name = ValidateName(name);
    }

    /// <summary>
    /// Sets the planned stay at this stop. Either date may be left out.
    /// Internal because only the owning trip decides the schedule.
    /// </summary>
    internal void Schedule(DateOnly? startsOn, DateOnly? endsOn)
    {
        StartsOn = startsOn;
        EndsOn = endsOn;
    }

    /// <summary>
    /// Sets where the place is, or clears it. A place carries both
    /// coordinates or neither. Internal because only the owning trip decides
    /// the location.
    /// </summary>
    internal void Locate(double? latitude, double? longitude)
    {
        ValidateCoordinates(latitude, longitude);

        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>
    /// Moves this stop to a new position on its route. Internal because only
    /// the owning trip decides the order.
    /// </summary>
    internal void MoveTo(int sortOrder)
    {
        SortOrder = sortOrder;
    }

    /// <summary>
    /// Checks the optional coordinates of a place: both are given or neither
    /// is, and each falls inside the range the globe allows.
    ///
    /// A place typed by hand carries neither, which is valid: a destination
    /// never has to come from a map.
    /// </summary>
    internal static void ValidateCoordinates(double? latitude, double? longitude)
    {
        if (latitude is null && longitude is null)
        {
            return;
        }

        if (latitude is null || longitude is null)
        {
            throw new ArgumentException(
                "A destination needs both a latitude and a longitude, or neither.",
                latitude is null ? nameof(latitude) : nameof(longitude));
        }

        if (latitude.Value < -90 || latitude.Value > 90)
        {
            throw new ArgumentException(
                "The destination latitude must be between -90 and 90.",
                nameof(latitude));
        }

        if (longitude.Value < -180 || longitude.Value > 180)
        {
            throw new ArgumentException(
                "The destination longitude must be between -180 and 180.",
                nameof(longitude));
        }
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The trip destination name is required.", nameof(name));
        }

        var trimmed = name.Trim();

        if (trimmed.Length > NameMaxLength)
        {
            throw new ArgumentException(
                $"The trip destination name cannot exceed {NameMaxLength} characters.",
                nameof(name));
        }

        return trimmed;
    }
}

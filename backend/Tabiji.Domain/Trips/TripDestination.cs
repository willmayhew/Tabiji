namespace Tabiji.Domain.Trips;

/// <summary>
/// A place on a trip: a destination on the main route, a stop within one of
/// those destinations, or a legacy saved place not yet filed under a
/// destination.
///
/// A destination belongs to exactly one trip and carries its place in that
/// trip's order. A stop carries the identity of the destination it belongs to
/// in <see cref="ParentDestinationId"/>; a destination and an unfiled place
/// carry null there. The hierarchy is deliberately one level deep: a stop may
/// never be a parent.
///
/// The name is free text so that any place in the world can be named; it is
/// never parsed and never compared against a list of countries.
///
/// The dates describe the planned stay at the place. Both are optional: a
/// place can be added long before its dates are known, and either one can be
/// left out on its own. <see cref="PlannedOn"/> is a separate, optional
/// calendar day of the trip the place is planned for.
///
/// The coordinates describe where the place is, and are optional too: a
/// place chosen from a map carries them, and a place typed by hand carries
/// none. They are never used to derive the name, and the name is never
/// derived from them.
/// </summary>
public sealed class TripDestination
{
    public const int NameMaxLength = 200;

    public const int AddressMaxLength = 300;

    public const int NotesMaxLength = 1000;

    private TripDestination()
    {
    }

    private TripDestination(
        Guid id,
        Guid tripId,
        Guid? parentDestinationId,
        TripPlaceKind kind,
        string name,
        DateOnly? startsOn,
        DateOnly? endsOn,
        double? latitude,
        double? longitude,
        int sortOrder,
        PlaceCategory? category,
        string? address,
        string? notes,
        DateOnly? plannedOn,
        DateTimeOffset? createdAt)
    {
        Id = id;
        TripId = tripId;
        ParentDestinationId = parentDestinationId;
        Kind = kind;
        Name = name;
        StartsOn = startsOn;
        EndsOn = endsOn;
        Latitude = latitude;
        Longitude = longitude;
        SortOrder = sortOrder;
        Category = category;
        Address = address;
        Notes = notes;
        PlannedOn = plannedOn;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TripId { get; private set; }

    /// <summary>
    /// The destination this place belongs to when it is a stop, or null for a
    /// destination on the main route and for an unfiled place.
    /// </summary>
    public Guid? ParentDestinationId { get; private set; }

    /// <summary>
    /// What this place is: a destination on the main route, a stop within one,
    /// or a legacy place not yet filed under a destination.
    /// </summary>
    public TripPlaceKind Kind { get; private set; }

    /// <summary>
    /// Whether this place is a stop within a destination. It is derived from
    /// <see cref="Kind"/> and is not stored.
    /// </summary>
    public bool IsStop => Kind == TripPlaceKind.Stop;

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// The planned first day at this place. Null when the day has not been
    /// decided yet.
    /// </summary>
    public DateOnly? StartsOn { get; private set; }

    /// <summary>
    /// The planned last day at this place. Null when the day has not been
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
    /// The position of this place within its scope: the main route, its
    /// destination's stops, or the trip's unfiled places.
    /// </summary>
    public int SortOrder { get; private set; }

    /// <summary>
    /// What kind of place this is, or null when the reader has not said.
    /// </summary>
    public PlaceCategory? Category { get; private set; }

    /// <summary>
    /// A short description of where the place is, as the map or the search
    /// gave it, or null when there is none.
    /// </summary>
    public string? Address { get; private set; }

    /// <summary>
    /// The reader's own note about the place, or null when they have not
    /// written one.
    /// </summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// The day of the trip this place is planned for, or null when it has not
    /// been planned for one. It is a calendar day, not a count of days from the
    /// start, so it names the same day however the trip is described.
    /// </summary>
    public DateOnly? PlannedOn { get; private set; }

    /// <summary>
    /// When the place was saved, for a place that came from a saved place, or
    /// when it was added. Null when the row predates the unified model and has
    /// no recorded time.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; private set; }

    /// <summary>
    /// Creates a place on a trip. Internal because the owning trip is the only
    /// thing allowed to make one, and it is the only thing that can give it a
    /// place on a route or within a destination.
    /// </summary>
    internal static TripDestination Create(
        Guid tripId,
        Guid? parentDestinationId,
        TripPlaceKind kind,
        string name,
        DateOnly? startsOn,
        DateOnly? endsOn,
        double? latitude,
        double? longitude,
        int sortOrder,
        PlaceCategory? category = null,
        string? address = null,
        string? notes = null,
        DateOnly? plannedOn = null,
        DateTimeOffset? createdAt = null)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentException(
                "The destination sort order cannot be negative.",
                nameof(sortOrder));
        }

        ValidateCoordinates(latitude, longitude);
        ValidateKind(kind, parentDestinationId);
        ValidateCategory(category);

        var addressValue = ValidateAddress(address);
        var notesValue = ValidateNotes(notes);

        var id = Guid.NewGuid();

        if (parentDestinationId == id)
        {
            throw new ArgumentException(
                "A destination cannot be its own parent.",
                nameof(parentDestinationId));
        }

        return new TripDestination(
            id,
            tripId,
            parentDestinationId,
            kind,
            ValidateName(name),
            startsOn,
            endsOn,
            latitude,
            longitude,
            sortOrder,
            category,
            addressValue,
            notesValue,
            plannedOn,
            createdAt);
    }

    /// <summary>
    /// Gives this place a new name. Internal because only the owning trip
    /// decides what its places are called.
    /// </summary>
    internal void Rename(string name)
    {
        Name = ValidateName(name);
    }

    /// <summary>
    /// Sets the planned stay at this place. Either date may be left out.
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
    /// Changes the details the reader can enter about a place: its category,
    /// its address, its note and the day it is planned for.
    /// </summary>
    internal void EditDetails(
        PlaceCategory? category,
        string? address,
        string? notes,
        DateOnly? plannedOn)
    {
        ValidateCategory(category);

        Category = category;
        Address = ValidateAddress(address);
        Notes = ValidateNotes(notes);
        PlannedOn = plannedOn;
    }

    /// <summary>
    /// Moves this place to a new position within its scope. Internal because
    /// only the owning trip decides the order.
    /// </summary>
    internal void MoveTo(int sortOrder)
    {
        SortOrder = sortOrder;
    }

    /// <summary>
    /// Files this place under one of its trip's destinations. A stop can be
    /// moved between existing destinations; an unfiled place can be filed for
    /// the first time. Internal because only the owning trip decides the
    /// hierarchy.
    /// </summary>
    internal void FileUnder(Guid parentDestinationId)
    {
        if (parentDestinationId == Id)
        {
            throw new ArgumentException(
                "A destination cannot be its own parent.",
                nameof(parentDestinationId));
        }

        Kind = TripPlaceKind.Stop;
        ParentDestinationId = parentDestinationId;
    }

    /// <summary>
    /// Takes this place off its destination and leaves it as an unfiled place.
    /// </summary>
    internal void Unfile()
    {
        Kind = TripPlaceKind.Place;
        ParentDestinationId = null;
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

    /// <summary>
    /// A stop must have a destination; a destination or an unfiled place must
    /// not.
    /// </summary>
    internal static void ValidateKind(TripPlaceKind kind, Guid? parentDestinationId)
    {
        var expectedParent = kind == TripPlaceKind.Stop;

        if (expectedParent && parentDestinationId is null)
        {
            throw new ArgumentException(
                "A stop must belong to a destination.",
                nameof(parentDestinationId));
        }

        if (!expectedParent && parentDestinationId is not null)
        {
            throw new ArgumentException(
                "Only a stop may belong to a destination.",
                nameof(parentDestinationId));
        }
    }

    /// <summary>
    /// Checks that a category is one the reader could have chosen. The value
    /// is refused when it is not a member of the enum, which a number on the
    /// wire can otherwise smuggle in.
    /// </summary>
    internal static void ValidateCategory(PlaceCategory? category)
    {
        if (category is PlaceCategory value && !Enum.IsDefined(value))
        {
            throw new ArgumentException(
                "The place category is not one of the known categories.",
                nameof(category));
        }
    }

    internal static string? ValidateAddress(string? address) =>
        Trimmed(address, AddressMaxLength, "The place address", nameof(address));

    internal static string? ValidateNotes(string? notes) =>
        Trimmed(notes, NotesMaxLength, "The place notes", nameof(notes));

    private static string? Trimmed(string? value, int maxLength, string label, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException(
                $"{label} cannot exceed {maxLength} characters.",
                parameterName);
        }

        return trimmed;
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

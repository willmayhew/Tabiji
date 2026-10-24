namespace Tabiji.Domain.Trips;

/// <summary>
/// A place saved to a trip: somewhere worth going, such as an attraction, a
/// restaurant or a landmark.
///
/// A saved place belongs to exactly one trip, and may also be filed under one
/// of that trip's stops. The filing is optional and is dropped rather than
/// carried away when the stop goes, so a place is never lost with the stop it
/// happened to be filed under.
///
/// Unlike a destination, a place always carries its coordinates. A
/// destination may be typed by hand and have no position at all; a place is
/// the opposite — it is chosen from a map or a search and its position is the
/// whole point of it. The name stays free text, and is never derived from the
/// coordinates.
/// </summary>
public sealed class SavedPlace
{
    public const int NameMaxLength = 200;

    public const int AddressMaxLength = 300;

    public const int NotesMaxLength = 1000;

    private SavedPlace()
    {
    }

    private SavedPlace(
        Guid id,
        Guid tripId,
        Guid? destinationId,
        string name,
        double latitude,
        double longitude,
        PlaceCategory? category,
        string? address,
        string? notes,
        DateTimeOffset createdAt)
    {
        Id = id;
        TripId = tripId;
        DestinationId = destinationId;
        Name = name;
        Latitude = latitude;
        Longitude = longitude;
        Category = category;
        Address = address;
        Notes = notes;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// The trip this place was saved to. A place belongs to one trip and is
    /// only ever reached through it.
    /// </summary>
    public Guid TripId { get; private set; }

    /// <summary>
    /// The stop of the same trip this place is filed under, or null when it
    /// has not been filed under one. The filing is a convenience for reading
    /// a trip; the place stands on its own without it.
    /// </summary>
    public Guid? DestinationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public double Latitude { get; private set; }

    public double Longitude { get; private set; }

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

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Saves a place to a trip. The place starts unfiled: it is put on the
    /// route, if it belongs there, by <see cref="MoveToDestination"/>.
    ///
    /// Everything is checked before the place is made, so a refused place is
    /// never half made.
    /// </summary>
    public static SavedPlace Create(
        Guid tripId,
        string name,
        double latitude,
        double longitude,
        PlaceCategory? category,
        string? address,
        string? notes,
        DateTimeOffset createdAt)
    {
        var validatedName = ValidateName(name);
        var validatedAddress = ValidateAddress(address);
        var validatedNotes = ValidateNotes(notes);

        ValidateCoordinates(latitude, longitude);
        ValidateCategory(category);

        return new SavedPlace(
            Guid.NewGuid(),
            tripId,
            null,
            validatedName,
            latitude,
            longitude,
            category,
            validatedAddress,
            validatedNotes,
            createdAt);
    }

    /// <summary>
    /// Changes everything a reader can change about a saved place.
    ///
    /// Everything is checked before anything is changed, so a refused edit
    /// leaves the place exactly as it was.
    ///
    /// Where the place is filed is not part of this: see
    /// <see cref="MoveToDestination"/>.
    /// </summary>
    public void Edit(
        string name,
        double latitude,
        double longitude,
        PlaceCategory? category,
        string? address,
        string? notes)
    {
        var validatedName = ValidateName(name);
        var validatedAddress = ValidateAddress(address);
        var validatedNotes = ValidateNotes(notes);

        ValidateCoordinates(latitude, longitude);
        ValidateCategory(category);

        Name = validatedName;
        Latitude = latitude;
        Longitude = longitude;
        Category = category;
        Address = validatedAddress;
        Notes = validatedNotes;
    }

    /// <summary>
    /// Files this place under one of its trip's stops, or takes it off the
    /// route when null is given.
    ///
    /// Whether the stop belongs to the same trip cannot be answered here: a
    /// place does not hold its trip's stops, and a saved place must not load
    /// a whole trip to check one identifier. The caller answers it, and is
    /// the authority on it.
    /// </summary>
    public void MoveToDestination(Guid? destinationId)
    {
        DestinationId = destinationId;
    }

    /// <summary>
    /// Checks the coordinates of a place. Both are always given, each inside
    /// the range the globe allows.
    ///
    /// This is the one rule a place has that a destination does not: a
    /// destination may carry no coordinates at all, and a place may not.
    /// </summary>
    private static void ValidateCoordinates(double latitude, double longitude)
    {
        if (latitude < -90 || latitude > 90)
        {
            throw new ArgumentException(
                "The saved place latitude must be between -90 and 90.",
                nameof(latitude));
        }

        if (longitude < -180 || longitude > 180)
        {
            throw new ArgumentException(
                "The saved place longitude must be between -180 and 180.",
                nameof(longitude));
        }
    }

    /// <summary>
    /// Checks that a category is one the reader could have chosen. The value
    /// is refused when it is not a member of the enum, which a number on the
    /// wire can otherwise smuggle in.
    /// </summary>
    private static void ValidateCategory(PlaceCategory? category)
    {
        if (category is PlaceCategory value && !Enum.IsDefined(value))
        {
            throw new ArgumentException(
                "The saved place category is not one of the known categories.",
                nameof(category));
        }
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The saved place name is required.", nameof(name));
        }

        var trimmed = name.Trim();

        if (trimmed.Length > NameMaxLength)
        {
            throw new ArgumentException(
                $"The saved place name cannot exceed {NameMaxLength} characters.",
                nameof(name));
        }

        return trimmed;
    }

    /// <summary>
    /// Trims a description or a note, and treats one of nothing but spaces as
    /// absent rather than as an empty string.
    /// </summary>
    private static string? ValidateAddress(string? address) =>
        Trimmed(address, AddressMaxLength, "The saved place address", nameof(address));

    private static string? ValidateNotes(string? notes) =>
        Trimmed(notes, NotesMaxLength, "The saved place notes", nameof(notes));

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
}

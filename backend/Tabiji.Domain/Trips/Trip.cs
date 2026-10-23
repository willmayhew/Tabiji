namespace Tabiji.Domain.Trips;

public sealed class Trip
{
    public const int NameMaxLength = 100;

    /// <summary>
    /// The most stops a trip may hold. Generous for a real itinerary, and it
    /// bounds the request that creates one.
    /// </summary>
    public const int MaxDestinationCount = 20;

    /// <summary>
    /// The stops on this trip's route. The field backs the read-only
    /// navigation: only this class can add to it or change its order.
    /// </summary>
    private List<TripDestination> _destinations = [];

    private Trip()
    {
    }

    private Trip(
        Guid id,
        string name,
        DateOnly startsOn,
        DateOnly endsOn,
        DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        StartsOn = startsOn;
        EndsOn = endsOn;
        Status = TripStatus.Planning;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateOnly StartsOn { get; private set; }

    public DateOnly EndsOn { get; private set; }

    public TripStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// The stops on this trip's route, in the order they are visited.
    /// </summary>
    public IReadOnlyList<TripDestination> Destinations => _destinations;

    public static Trip Create(
        string name,
        IReadOnlyList<TripDestinationDraft> destinations,
        DateOnly startsOn,
        DateOnly endsOn,
        DateTimeOffset createdAt)
    {
        var validatedName = ValidateName(name);
        var validatedDestinations = ValidateDestinations(destinations);

        EnsureEndIsNotBeforeStart(startsOn, endsOn);

        TripSchedule.EnsureChronological(startsOn, endsOn, validatedDestinations);

        var trip = new Trip(Guid.NewGuid(), validatedName, startsOn, endsOn, createdAt);

        for (var index = 0; index < validatedDestinations.Count; index += 1)
        {
            var destination = validatedDestinations[index];

            trip._destinations.Add(
                TripDestination.Create(
                    trip.Id,
                    destination.Name,
                    destination.StartsOn,
                    destination.EndsOn,
                    destination.Latitude,
                    destination.Longitude,
                    index));
        }

        return trip;
    }

    public void Rename(string name)
    {
        Name = ValidateName(name);
    }

    public void Reschedule(DateOnly startsOn, DateOnly endsOn)
    {
        EnsureEndIsNotBeforeStart(startsOn, endsOn);

        StartsOn = startsOn;
        EndsOn = endsOn;
    }

    /// <summary>
    /// Applies a whole new route to the trip: its name, its dates and its
    /// ordered stops, with their names and their optional dates.
    ///
    /// Everything is checked first. Nothing is changed until the whole route
    /// has been accepted, so a rejected update leaves the trip exactly as it
    /// was.
    ///
    /// A draft that names a stop keeps that stop and its identity. A draft
    /// with no identity is a new stop. A stop the drafts leave out is
    /// removed.
    /// </summary>
    public void ApplyRoute(
        string name,
        DateOnly startsOn,
        DateOnly endsOn,
        IReadOnlyList<TripDestinationDraft> destinations)
    {
        var validatedName = ValidateName(name);
        var validatedDestinations = ValidateDestinations(destinations);

        EnsureEndIsNotBeforeStart(startsOn, endsOn);

        TripSchedule.EnsureChronological(startsOn, endsOn, validatedDestinations);

        var existing = _destinations.ToDictionary(destination => destination.Id);

        // A draft that names a stop this trip does not have is a mistake, not
        // a new stop: the caller asked to change a stop that has gone.
        foreach (var destination in validatedDestinations)
        {
            if (destination.Id is Guid id && !existing.ContainsKey(id))
            {
                throw new ArgumentException(
                    "The trip does not have that destination.",
                    nameof(destinations));
            }
        }

        // Only now is anything changed.
        Name = validatedName;
        StartsOn = startsOn;
        EndsOn = endsOn;

        var route = new List<TripDestination>(validatedDestinations.Count);

        foreach (var destination in validatedDestinations)
        {
            if (destination.Id is Guid id && existing.TryGetValue(id, out var kept))
            {
                kept.Rename(destination.Name);
                kept.Schedule(destination.StartsOn, destination.EndsOn);
                kept.Locate(destination.Latitude, destination.Longitude);
                route.Add(kept);
                continue;
            }

            route.Add(
                TripDestination.Create(
                    Id,
                    destination.Name,
                    destination.StartsOn,
                    destination.EndsOn,
                    destination.Latitude,
                    destination.Longitude,
                    route.Count));
        }

        _destinations.Clear();
        _destinations.AddRange(route);
        Renumber();
    }

    /// <summary>
    /// Appends a stop to the end of the route.
    /// </summary>
    public void AddDestination(string name)
    {
        EnsureThereIsRoomForAnotherDestination();

        _destinations.Add(TripDestination.Create(Id, name, null, null, null, null, _destinations.Count));
    }

    /// <summary>
    /// Removes a stop and closes the gap it leaves behind.
    /// </summary>
    public void RemoveDestination(Guid destinationId)
    {
        var destination = _destinations.FirstOrDefault(
            candidate => candidate.Id == destinationId);

        if (destination is null)
        {
            throw new ArgumentException(
                "The trip does not have that destination.",
                nameof(destinationId));
        }

        if (_destinations.Count == 1)
        {
            throw new ArgumentException(
                "A trip must have at least one destination.",
                nameof(destinationId));
        }

        _destinations.Remove(destination);
        Renumber();
    }

    /// <summary>
    /// Puts the stops in a new order. The ids must name every stop exactly
    /// once, so a route can never lose or gain a stop by being reordered.
    /// </summary>
    public void ReorderDestinations(IReadOnlyList<Guid> orderedIds)
    {
        if (orderedIds.Count != _destinations.Count)
        {
            throw new ArgumentException(
                "The new order must name every destination of the trip exactly once.",
                nameof(orderedIds));
        }

        var byId = _destinations.ToDictionary(destination => destination.Id);

        if (orderedIds.Any(id => !byId.ContainsKey(id)))
        {
            throw new ArgumentException(
                "The new order must name every destination of the trip exactly once.",
                nameof(orderedIds));
        }

        var reordered = orderedIds.Select(id => byId[id]).ToList();

        _destinations.Clear();
        _destinations.AddRange(reordered);
        Renumber();
    }

    private void Renumber()
    {
        for (var index = 0; index < _destinations.Count; index += 1)
        {
            _destinations[index].MoveTo(index);
        }
    }

    private void EnsureThereIsRoomForAnotherDestination()
    {
        if (_destinations.Count >= MaxDestinationCount)
        {
            throw new ArgumentException(
                $"A trip cannot have more than {MaxDestinationCount} destinations.",
                "destinations");
        }
    }

    private static void EnsureEndIsNotBeforeStart(DateOnly startsOn, DateOnly endsOn)
    {
        if (endsOn < startsOn)
        {
            throw new ArgumentException("The trip cannot end before it starts.", nameof(endsOn));
        }
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The trip name is required.", nameof(name));
        }

        var trimmed = name.Trim();

        if (trimmed.Length > NameMaxLength)
        {
            throw new ArgumentException($"The trip name cannot exceed {NameMaxLength} characters.", nameof(name));
        }

        return trimmed;
    }

    /// <summary>
    /// Checks the route as a whole: there must be at least one stop, there
    /// must not be too many, no stop may be blank, and no stop may be named
    /// twice.
    ///
    /// The same place may appear twice. A route that visits Rome on the way
    /// out and again on the way home is a round trip, not a mistake.
    /// </summary>
    private static List<TripDestinationDraft> ValidateDestinations(
        IReadOnlyList<TripDestinationDraft> destinations)
    {
        if (destinations is null || destinations.Count == 0)
        {
            throw new ArgumentException("A trip must have at least one destination.", nameof(destinations));
        }

        if (destinations.Count > MaxDestinationCount)
        {
            throw new ArgumentException(
                $"A trip cannot have more than {MaxDestinationCount} destinations.",
                nameof(destinations));
        }

        var seen = new HashSet<Guid>();
        var validated = new List<TripDestinationDraft>(destinations.Count);

        foreach (var destination in destinations)
        {
            if (destination.Id is Guid id && !seen.Add(id))
            {
                throw new ArgumentException(
                    "A destination may appear only once.",
                    nameof(destinations));
            }

            if (string.IsNullOrWhiteSpace(destination.Name))
            {
                throw new ArgumentException("The trip destination name is required.", nameof(destinations));
            }

            var trimmed = destination.Name.Trim();

            if (trimmed.Length > TripDestination.NameMaxLength)
            {
                throw new ArgumentException(
                    $"The trip destination name cannot exceed {TripDestination.NameMaxLength} characters.",
                    nameof(destinations));
            }

            // The coordinates are optional, but when they are given they must
            // be a whole place: both of them, and each inside the globe.
            TripDestination.ValidateCoordinates(destination.Latitude, destination.Longitude);

            validated.Add(destination with { Name = trimmed });
        }

        return validated;
    }
}

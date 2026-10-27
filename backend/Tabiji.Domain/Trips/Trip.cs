namespace Tabiji.Domain.Trips;

public sealed class Trip
{
    public const int NameMaxLength = 100;

    /// <summary>
    /// The most destinations a trip may hold. Generous for a real itinerary,
    /// and it bounds the request that creates one.
    /// </summary>
    public const int MaxDestinationCount = 20;

    /// <summary>
    /// The most stops one destination may hold.
    /// </summary>
    public const int MaxStopsPerDestination = 20;

    /// <summary>
    /// The most places a trip may hold altogether: its destinations, all of
    /// their stops, and its unfiled legacy places. It bounds the request that
    /// creates or changes a trip.
    /// </summary>
    public const int MaxDestinationRows = 200;

    /// <summary>
    /// Every place on this trip: its destinations, the stops within them and
    /// any legacy unfiled places. The field backs the read-only navigation:
    /// only this class can add to it or change its order.
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
    /// Every place on this trip, destinations, stops and unfiled places alike,
    /// in the order the flat list happens to hold them. The route is
    /// <see cref="ParentDestinations"/>; a destination's stops are
    /// <see cref="StopsOf"/>; the legacy unfiled places are
    /// <see cref="UnfiledPlaces"/>.
    /// </summary>
    public IReadOnlyList<TripDestination> Destinations => _destinations;

    /// <summary>
    /// The destinations on the main route, in the order they are visited.
    /// Stops and unfiled places are never part of this order.
    /// </summary>
    public IReadOnlyList<TripDestination> ParentDestinations =>
        _destinations
            .Where(destination => destination.Kind == TripPlaceKind.Destination)
            .OrderBy(destination => destination.SortOrder)
            .ToArray();

    /// <summary>
    /// The stops within one destination, in the order they are listed. A stop
    /// is part of the place, not a leg of the journey.
    /// </summary>
    public IReadOnlyList<TripDestination> StopsOf(Guid parentId) =>
        _destinations
            .Where(destination =>
                destination.Kind == TripPlaceKind.Stop &&
                destination.ParentDestinationId == parentId)
            .OrderBy(destination => destination.SortOrder)
            .ToArray();

    /// <summary>
    /// The legacy saved places that have not been filed under a destination.
    /// They are not on the route and they are never created by the current UI;
    /// they exist so that old saved-place records stay accessible.
    /// </summary>
    public IReadOnlyList<TripDestination> UnfiledPlaces =>
        _destinations
            .Where(destination => destination.Kind == TripPlaceKind.Place)
            .OrderBy(destination => destination.SortOrder)
            .ToArray();

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
        TripSchedule.EnsureStopsWithinDestinations(startsOn, endsOn, validatedDestinations);
        TripSchedule.EnsurePlannedOnIsATripDay(startsOn, endsOn, validatedDestinations);

        var trip = new Trip(Guid.NewGuid(), validatedName, startsOn, endsOn, createdAt);

        for (var index = 0; index < validatedDestinations.Count; index += 1)
        {
            var destination = validatedDestinations[index];

            var parent = TripDestination.Create(
                trip.Id,
                null,
                TripPlaceKind.Destination,
                destination.Name,
                destination.StartsOn,
                destination.EndsOn,
                destination.Latitude,
                destination.Longitude,
                index,
                destination.Category,
                destination.Address,
                destination.Notes,
                destination.PlannedOn,
                createdAt);

            trip._destinations.Add(parent);

            var stops = destination.Stops ?? [];

            for (var stopIndex = 0; stopIndex < stops.Count; stopIndex += 1)
            {
                var stop = stops[stopIndex];

                trip._destinations.Add(
                    TripDestination.Create(
                        trip.Id,
                        parent.Id,
                        TripPlaceKind.Stop,
                        stop.Name,
                        stop.StartsOn,
                        stop.EndsOn,
                        stop.Latitude,
                        stop.Longitude,
                        stopIndex,
                        stop.Category,
                        stop.Address,
                        stop.Notes,
                        stop.PlannedOn,
                        stop.CreatedAt ?? createdAt));
            }
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
    /// Applies a whole new route to the trip: its name, its dates, its
    /// destinations, the stops within them, and the unfiled legacy places it
    /// still holds.
    ///
    /// Everything is checked first. Nothing is changed until the whole route
    /// has been accepted, so a rejected update leaves the trip exactly as it
    /// was.
    ///
    /// A draft that names a place keeps that place and its identity. A draft
    /// with no identity is a new place. A place the drafts leave out is
    /// removed. A stop named under a different destination is moved there,
    /// and keeps its identity. A legacy unfiled place named under a
    /// destination is filed there.
    ///
    /// <paramref name="unfiledPlaces"/> may be null, which means "leave the
    /// trip's unfiled places alone". The current client sends the complete
    /// list; an older client that does not know about unfiled places cannot
    /// delete them by omission.
    /// </summary>
    public void ApplyRoute(
        string name,
        DateOnly startsOn,
        DateOnly endsOn,
        IReadOnlyList<TripDestinationDraft> destinations,
        IReadOnlyList<TripStopDraft>? unfiledPlaces = null)
    {
        var validatedName = ValidateName(name);
        var validatedDestinations = ValidateDestinations(destinations);
        var validatedPlaces = unfiledPlaces is null ? null : ValidatePlaces(unfiledPlaces);

        EnsureEndIsNotBeforeStart(startsOn, endsOn);

        TripSchedule.EnsureChronological(startsOn, endsOn, validatedDestinations);
        TripSchedule.EnsureStopsWithinDestinations(startsOn, endsOn, validatedDestinations);
        TripSchedule.EnsurePlannedOnIsATripDay(
            startsOn,
            endsOn,
            validatedDestinations,
            validatedPlaces);

        if (validatedPlaces is not null)
        {
            TripSchedule.EnsurePlacesWithinTrip(startsOn, endsOn, validatedPlaces);
        }

        EnsureTotalRows(validatedDestinations, validatedPlaces);

        var existingDestinations = _destinations
            .Where(destination => destination.Kind == TripPlaceKind.Destination)
            .ToDictionary(destination => destination.Id);

        var existingStops = _destinations
            .Where(destination => destination.Kind == TripPlaceKind.Stop)
            .ToDictionary(destination => destination.Id);

        var existingPlaces = _destinations
            .Where(destination => destination.Kind == TripPlaceKind.Place)
            .ToDictionary(destination => destination.Id);

        // A draft that names a place this trip does not have is a mistake, not
        // a new place: the caller asked to change something that has gone. A
        // place may be named only once, and a destination may not be named as
        // a stop or the other way round.
        var seen = new HashSet<Guid>();

        foreach (var destination in validatedDestinations)
        {
            if (destination.Id is Guid parentId)
            {
                if (!existingDestinations.ContainsKey(parentId))
                {
                    throw new ArgumentException(
                        "The trip does not have that destination.",
                        nameof(destinations));
                }

                if (!seen.Add(parentId))
                {
                    throw new ArgumentException(
                        "A destination may appear only once.",
                        nameof(destinations));
                }
            }

            foreach (var stop in destination.Stops ?? [])
            {
                if (stop.Id is not Guid stopId)
                {
                    continue;
                }

                if (!existingStops.ContainsKey(stopId) && !existingPlaces.ContainsKey(stopId))
                {
                    throw new ArgumentException(
                        "The trip does not have that stop.",
                        nameof(destinations));
                }

                // A stop can be moved between destinations that already
                // exist, but not into one this request is creating: the new
                // destination has no row for the stop to point at yet.
                if (destination.Id is null)
                {
                    throw new ArgumentException(
                        "A stop cannot be moved to a new destination.",
                        nameof(destinations));
                }

                if (!seen.Add(stopId))
                {
                    throw new ArgumentException(
                        "A stop may appear only once.",
                        nameof(destinations));
                }
            }
        }

        foreach (var place in validatedPlaces ?? [])
        {
            if (place.Id is not Guid placeId)
            {
                continue;
            }

            if (!existingPlaces.ContainsKey(placeId))
            {
                throw new ArgumentException(
                    "The trip does not have that place.",
                    nameof(unfiledPlaces));
            }

            if (!seen.Add(placeId))
            {
                throw new ArgumentException(
                    "A place may appear only once.",
                    nameof(unfiledPlaces));
            }
        }

        // Only now is anything changed.
        Name = validatedName;
        StartsOn = startsOn;
        EndsOn = endsOn;

        var route = new List<TripDestination>();

        for (var parentIndex = 0; parentIndex < validatedDestinations.Count; parentIndex += 1)
        {
            var destination = validatedDestinations[parentIndex];

            TripDestination parent;

            if (destination.Id is Guid parentId && existingDestinations.TryGetValue(parentId, out var keptParent))
            {
                keptParent.Rename(destination.Name);
                keptParent.Schedule(destination.StartsOn, destination.EndsOn);
                keptParent.Locate(destination.Latitude, destination.Longitude);
                keptParent.EditDetails(
                    destination.Category,
                    destination.Address,
                    destination.Notes,
                    destination.PlannedOn);
                keptParent.MoveTo(parentIndex);
                parent = keptParent;
            }
            else
            {
                parent = TripDestination.Create(
                    Id,
                    null,
                    TripPlaceKind.Destination,
                    destination.Name,
                    destination.StartsOn,
                    destination.EndsOn,
                    destination.Latitude,
                    destination.Longitude,
                    parentIndex,
                    destination.Category,
                    destination.Address,
                    destination.Notes,
                    destination.PlannedOn,
                    destination.CreatedAt ?? CreatedAt);
            }

            route.Add(parent);

            var stops = destination.Stops ?? [];

            for (var stopIndex = 0; stopIndex < stops.Count; stopIndex += 1)
            {
                var stopDraft = stops[stopIndex];

                TripDestination stop;

                if (stopDraft.Id is Guid stopId && existingStops.TryGetValue(stopId, out var keptStop))
                {
                    keptStop.Rename(stopDraft.Name);
                    keptStop.Schedule(stopDraft.StartsOn, stopDraft.EndsOn);
                    keptStop.Locate(stopDraft.Latitude, stopDraft.Longitude);
                    keptStop.EditDetails(
                        stopDraft.Category,
                        stopDraft.Address,
                        stopDraft.Notes,
                        stopDraft.PlannedOn);
                    keptStop.FileUnder(parent.Id);
                    keptStop.MoveTo(stopIndex);
                    stop = keptStop;
                }
                else if (stopDraft.Id is Guid placeId && existingPlaces.TryGetValue(placeId, out var filedPlace))
                {
                    filedPlace.Rename(stopDraft.Name);
                    filedPlace.Schedule(stopDraft.StartsOn, stopDraft.EndsOn);
                    filedPlace.Locate(stopDraft.Latitude, stopDraft.Longitude);
                    filedPlace.EditDetails(
                        stopDraft.Category,
                        stopDraft.Address,
                        stopDraft.Notes,
                        stopDraft.PlannedOn);
                    filedPlace.FileUnder(parent.Id);
                    filedPlace.MoveTo(stopIndex);
                    stop = filedPlace;
                }
                else
                {
                    stop = TripDestination.Create(
                        Id,
                        parent.Id,
                        TripPlaceKind.Stop,
                        stopDraft.Name,
                        stopDraft.StartsOn,
                        stopDraft.EndsOn,
                        stopDraft.Latitude,
                        stopDraft.Longitude,
                        stopIndex,
                        stopDraft.Category,
                        stopDraft.Address,
                        stopDraft.Notes,
                        stopDraft.PlannedOn,
                        stopDraft.CreatedAt);
                }

                route.Add(stop);
            }
        }

        if (validatedPlaces is null)
        {
            // The caller did not send the unfiled places; keep them exactly as
            // they were rather than treating their absence as a removal.
            route.AddRange(UnfiledPlaces);
        }
        else
        {
            for (var placeIndex = 0; placeIndex < validatedPlaces.Count; placeIndex += 1)
            {
                var placeDraft = validatedPlaces[placeIndex];
                var keptPlace = existingPlaces[placeDraft.Id!.Value];

                keptPlace.Rename(placeDraft.Name);
                keptPlace.Schedule(placeDraft.StartsOn, placeDraft.EndsOn);
                keptPlace.Locate(placeDraft.Latitude, placeDraft.Longitude);
                keptPlace.EditDetails(
                    placeDraft.Category,
                    placeDraft.Address,
                    placeDraft.Notes,
                    placeDraft.PlannedOn);
                keptPlace.Unfile();
                keptPlace.MoveTo(placeIndex);
                route.Add(keptPlace);
            }
        }

        _destinations.Clear();
        _destinations.AddRange(route);

        RenumberParents();
        RenumberStops();
        RenumberPlaces();
    }

    /// <summary>
    /// Appends a destination to the end of the main route.
    /// </summary>
    public void AddDestination(string name)
    {
        EnsureThereIsRoomForAnotherDestination();

        var parent = TripDestination.Create(
            Id,
            null,
            TripPlaceKind.Destination,
            name,
            null,
            null,
            null,
            null,
            ParentDestinations.Count);

        _destinations.Add(parent);
        RenumberParents();
    }

    /// <summary>
    /// Removes a destination and every stop within it, and closes the gap it
    /// leaves behind on the main route. Unfiled places are not children of a
    /// destination, so they are left alone.
    /// </summary>
    public void RemoveDestination(Guid destinationId)
    {
        var destination = ParentDestinations.FirstOrDefault(
            candidate => candidate.Id == destinationId);

        if (destination is null)
        {
            throw new ArgumentException(
                "The trip does not have that destination.",
                nameof(destinationId));
        }

        if (ParentDestinations.Count == 1)
        {
            throw new ArgumentException(
                "A trip must have at least one destination.",
                nameof(destinationId));
        }

        _destinations.RemoveAll(
            candidate =>
                candidate.Id == destinationId ||
                (candidate.Kind == TripPlaceKind.Stop &&
                 candidate.ParentDestinationId == destinationId));

        RenumberParents();
        RenumberStops();
    }

    /// <summary>
    /// Puts the destinations in a new order. The ids must name every
    /// destination exactly once, so a route can never lose or gain one by
    /// being reordered. A destination's stops move with it, and keep their own
    /// order.
    /// </summary>
    public void ReorderDestinations(IReadOnlyList<Guid> orderedIds)
    {
        var parents = ParentDestinations;

        if (orderedIds.Count != parents.Count)
        {
            throw new ArgumentException(
                "The new order must name every destination of the trip exactly once.",
                nameof(orderedIds));
        }

        var byId = parents.ToDictionary(destination => destination.Id);

        if (orderedIds.Any(id => !byId.ContainsKey(id)))
        {
            throw new ArgumentException(
                "The new order must name every destination of the trip exactly once.",
                nameof(orderedIds));
        }

        var rebuilt = new List<TripDestination>(_destinations.Count);

        foreach (var id in orderedIds)
        {
            rebuilt.Add(byId[id]);
            rebuilt.AddRange(StopsOf(id));
        }

        rebuilt.AddRange(UnfiledPlaces);

        _destinations.Clear();
        _destinations.AddRange(rebuilt);

        RenumberParents();
        RenumberStops();
        RenumberPlaces();
    }

    /// <summary>
    /// Appends a stop to the end of a destination's stops.
    /// </summary>
    public void AddStop(
        Guid parentId,
        string name,
        DateOnly? startsOn = null,
        DateOnly? endsOn = null,
        double? latitude = null,
        double? longitude = null,
        PlaceCategory? category = null,
        string? address = null,
        string? notes = null,
        DateOnly? plannedOn = null)
    {
        var parent = ParentDestinations.FirstOrDefault(
            candidate => candidate.Id == parentId);

        if (parent is null)
        {
            throw new ArgumentException(
                "The trip does not have that destination.",
                nameof(parentId));
        }

        EnsureThereIsRoomForAnotherStop(parentId);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The trip stop name is required.", nameof(name));
        }

        var validatedName = name.Trim();

        if (validatedName.Length > TripDestination.NameMaxLength)
        {
            throw new ArgumentException(
                $"The trip stop name cannot exceed {TripDestination.NameMaxLength} characters.",
                nameof(name));
        }

        TripDestination.ValidateCoordinates(latitude, longitude);
        TripDestination.ValidateCategory(category);

        var draft = new TripStopDraft(
            null,
            validatedName,
            startsOn,
            endsOn,
            latitude,
            longitude,
            category,
            address,
            notes,
            plannedOn);

        TripSchedule.EnsureStopWithinDestination(
            StartsOn,
            EndsOn,
            parent.Name,
            parent.StartsOn,
            parent.EndsOn,
            draft);

        if (plannedOn is DateOnly day)
        {
            TripSchedule.EnsureIsATripDay(StartsOn, EndsOn, day, validatedName);
        }

        _destinations.Add(
            TripDestination.Create(
                Id,
                parent.Id,
                TripPlaceKind.Stop,
                validatedName,
                startsOn,
                endsOn,
                latitude,
                longitude,
                StopsOf(parentId).Count,
                category,
                address,
                notes,
                plannedOn));

        RenumberStops();
    }

    /// <summary>
    /// Removes one stop from a destination and closes the gap it leaves
    /// behind. The stop is the place: its category, notes and planned day go
    /// with it. Unfiled places are not children of a stop and are unaffected.
    /// </summary>
    public void RemoveStop(Guid parentId, Guid stopId)
    {
        var stop = _destinations.FirstOrDefault(
            candidate =>
                candidate.Id == stopId &&
                candidate.Kind == TripPlaceKind.Stop &&
                candidate.ParentDestinationId == parentId);

        if (stop is null)
        {
            throw new ArgumentException(
                "The trip does not have that stop.",
                nameof(stopId));
        }

        _destinations.Remove(stop);
        RenumberStops();
    }

    /// <summary>
    /// Puts the stops of one destination in a new order. The ids must name
    /// every stop of that destination exactly once.
    /// </summary>
    public void ReorderStops(Guid parentId, IReadOnlyList<Guid> orderedIds)
    {
        var stops = StopsOf(parentId);

        if (orderedIds.Count != stops.Count || orderedIds.Any(id => stops.All(stop => stop.Id != id)))
        {
            throw new ArgumentException(
                "The new order must name every stop of the destination exactly once.",
                nameof(orderedIds));
        }

        var byId = stops.ToDictionary(stop => stop.Id);
        var reordered = orderedIds.Select(id => byId[id]).ToList();

        // The flat list is rebuilt in the new order, with the parent followed
        // by its stops, so that a later renumbering reads the order the
        // reader chose rather than the order the rows happened to hold.
        var rebuilt = new List<TripDestination>(_destinations.Count);

        foreach (var destination in _destinations)
        {
            if (destination.Id == parentId)
            {
                rebuilt.Add(destination);
                rebuilt.AddRange(reordered);
                continue;
            }

            if (destination.Kind == TripPlaceKind.Stop &&
                destination.ParentDestinationId == parentId)
            {
                continue;
            }

            rebuilt.Add(destination);
        }

        _destinations.Clear();
        _destinations.AddRange(rebuilt);

        RenumberStops();
    }

    private void RenumberParents()
    {
        var index = 0;

        foreach (var destination in _destinations)
        {
            if (destination.Kind != TripPlaceKind.Destination)
            {
                continue;
            }

            destination.MoveTo(index);
            index += 1;
        }
    }

    private void RenumberStops()
    {
        foreach (var parent in _destinations)
        {
            if (parent.Kind != TripPlaceKind.Destination)
            {
                continue;
            }

            var index = 0;

            foreach (var stop in _destinations)
            {
                if (stop.Kind != TripPlaceKind.Stop ||
                    stop.ParentDestinationId != parent.Id)
                {
                    continue;
                }

                stop.MoveTo(index);
                index += 1;
            }
        }
    }

    private void RenumberPlaces()
    {
        var index = 0;

        foreach (var place in _destinations)
        {
            if (place.Kind != TripPlaceKind.Place)
            {
                continue;
            }

            place.MoveTo(index);
            index += 1;
        }
    }

    private void EnsureThereIsRoomForAnotherDestination()
    {
        if (ParentDestinations.Count >= MaxDestinationCount)
        {
            throw new ArgumentException(
                $"A trip cannot have more than {MaxDestinationCount} destinations.",
                "destinations");
        }
    }

    private void EnsureThereIsRoomForAnotherStop(Guid parentId)
    {
        if (StopsOf(parentId).Count >= MaxStopsPerDestination)
        {
            throw new ArgumentException(
                $"A destination cannot have more than {MaxStopsPerDestination} stops.",
                nameof(parentId));
        }

        if (_destinations.Count >= MaxDestinationRows)
        {
            throw new ArgumentException(
                $"A trip cannot have more than {MaxDestinationRows} destinations and stops.",
                nameof(parentId));
        }
    }

    private static void EnsureEndIsNotBeforeStart(DateOnly startsOn, DateOnly endsOn)
    {
        if (endsOn < startsOn)
        {
            throw new ArgumentException("The trip cannot end before it starts.", nameof(endsOn));
        }
    }

    private static void EnsureTotalRows(
        List<TripDestinationDraft> destinations,
        List<TripStopDraft>? places)
    {
        var total = destinations.Sum(destination => 1 + (destination.Stops?.Count ?? 0))
            + (places?.Count ?? 0);

        if (total > MaxDestinationRows)
        {
            throw new ArgumentException(
                $"A trip cannot have more than {MaxDestinationRows} destinations and stops.",
                nameof(destinations));
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
    /// Checks the route as a whole: there must be at least one destination,
    /// there must not be too many, no name may be blank, every identity must be
    /// unique, and the stops of each destination must be within it.
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

            var name = destination.Name.Trim();

            if (name.Length > TripDestination.NameMaxLength)
            {
                throw new ArgumentException(
                    $"The trip destination name cannot exceed {TripDestination.NameMaxLength} characters.",
                    nameof(destinations));
            }

            // The coordinates are optional, but when they are given they must
            // be a whole place: both of them, and each inside the globe.
            TripDestination.ValidateCoordinates(destination.Latitude, destination.Longitude);
            TripDestination.ValidateCategory(destination.Category);
            TripDestination.ValidateAddress(destination.Address);
            TripDestination.ValidateNotes(destination.Notes);

            var stops = destination.Stops ?? [];

            if (stops.Count > MaxStopsPerDestination)
            {
                throw new ArgumentException(
                    $"A destination cannot have more than {MaxStopsPerDestination} stops.",
                    nameof(destinations));
            }

            var validatedStops = new List<TripStopDraft>(stops.Count);

            foreach (var stop in stops)
            {
                if (stop.Id is Guid stopId && !seen.Add(stopId))
                {
                    throw new ArgumentException(
                        "A stop may appear only once.",
                        nameof(destinations));
                }

                if (string.IsNullOrWhiteSpace(stop.Name))
                {
                    throw new ArgumentException("The trip stop name is required.", nameof(destinations));
                }

                var stopName = stop.Name.Trim();

                if (stopName.Length > TripDestination.NameMaxLength)
                {
                    throw new ArgumentException(
                        $"The trip stop name cannot exceed {TripDestination.NameMaxLength} characters.",
                        nameof(destinations));
                }

                TripDestination.ValidateCoordinates(stop.Latitude, stop.Longitude);
                TripDestination.ValidateCategory(stop.Category);
                TripDestination.ValidateAddress(stop.Address);
                TripDestination.ValidateNotes(stop.Notes);

                validatedStops.Add(stop with { Name = stopName });
            }

            validated.Add(destination with { Name = name, Stops = validatedStops });
        }

        return validated;
    }

    /// <summary>
    /// Checks the legacy unfiled places. They are existing rows being kept or
    /// edited, never new rows: a new location must be added to a destination.
    /// </summary>
    private static List<TripStopDraft> ValidatePlaces(IReadOnlyList<TripStopDraft> places)
    {
        var seen = new HashSet<Guid>();
        var validated = new List<TripStopDraft>(places.Count);

        foreach (var place in places)
        {
            if (place.Id is not Guid id)
            {
                throw new ArgumentException(
                    "A new place must be added to a destination.",
                    nameof(places));
            }

            if (!seen.Add(id))
            {
                throw new ArgumentException(
                    "A place may appear only once.",
                    nameof(places));
            }

            if (string.IsNullOrWhiteSpace(place.Name))
            {
                throw new ArgumentException("The trip place name is required.", nameof(places));
            }

            var name = place.Name.Trim();

            if (name.Length > TripDestination.NameMaxLength)
            {
                throw new ArgumentException(
                    $"The trip place name cannot exceed {TripDestination.NameMaxLength} characters.",
                    nameof(places));
            }

            TripDestination.ValidateCoordinates(place.Latitude, place.Longitude);
            TripDestination.ValidateCategory(place.Category);
            TripDestination.ValidateAddress(place.Address);
            TripDestination.ValidateNotes(place.Notes);

            validated.Add(place with { Name = name });
        }

        return validated;
    }
}

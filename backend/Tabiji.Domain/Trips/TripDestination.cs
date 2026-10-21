namespace Tabiji.Domain.Trips;

/// <summary>
/// A stop on a trip's route.
///
/// A destination belongs to exactly one trip and carries its place in that
/// trip's order. The name is free text so that any place in the world can be
/// named; it is never parsed and never compared against a list of countries.
/// </summary>
public sealed class TripDestination
{
    public const int NameMaxLength = 200;

    private TripDestination()
    {
    }

    private TripDestination(Guid id, Guid tripId, string name, int sortOrder)
    {
        Id = id;
        TripId = tripId;
        Name = name;
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }

    public Guid TripId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// The position of this stop on its trip's route, counting from zero.
    /// </summary>
    public int SortOrder { get; private set; }

    /// <summary>
    /// Creates a stop. Internal because the owning trip is the only thing
    /// allowed to make one, and it is the only thing that can give it a place
    /// on a route.
    /// </summary>
    internal static TripDestination Create(Guid tripId, string name, int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentException(
                "The destination sort order cannot be negative.",
                nameof(sortOrder));
        }

        return new TripDestination(Guid.NewGuid(), tripId, ValidateName(name), sortOrder);
    }

    /// <summary>
    /// Moves this stop to a new position on its route. Internal because only
    /// the owning trip decides the order.
    /// </summary>
    internal void MoveTo(int sortOrder)
    {
        SortOrder = sortOrder;
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

namespace Tabiji.Domain.Trips;

public sealed class Trip
{
    public const int NameMaxLength = 100;
    public const int DestinationMaxLength = 200;

    private Trip()
    {
    }

    private Trip(
        Guid id,
        string name,
        string destination,
        DateOnly startsOn,
        DateOnly endsOn,
        DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Destination = destination;
        StartsOn = startsOn;
        EndsOn = endsOn;
        Status = TripStatus.Planning;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Destination { get; private set; } = string.Empty;

    public DateOnly StartsOn { get; private set; }

    public DateOnly EndsOn { get; private set; }

    public TripStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Trip Create(
        string name,
        string destination,
        DateOnly startsOn,
        DateOnly endsOn,
        DateTimeOffset createdAt)
    {
        var validatedName = ValidateName(name);
        var validatedDestination = ValidateDestination(destination);

        EnsureEndIsNotBeforeStart(startsOn, endsOn);

        return new Trip(Guid.NewGuid(), validatedName, validatedDestination, startsOn, endsOn, createdAt);
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

    private static string ValidateDestination(string destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            throw new ArgumentException("The trip destination is required.", nameof(destination));
        }

        var trimmed = destination.Trim();

        if (trimmed.Length > DestinationMaxLength)
        {
            throw new ArgumentException(
                $"The trip destination cannot exceed {DestinationMaxLength} characters.",
                nameof(destination));
        }

        return trimmed;
    }
}

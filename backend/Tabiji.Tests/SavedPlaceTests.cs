using Tabiji.Domain.Trips;

namespace Tabiji.Tests;

/// <summary>
/// The rules of a saved place, checked on their own.
///
/// A place is its own aggregate, so unlike a destination it can be made and
/// changed without a trip. What it cannot do is be made without a position:
/// a place is chosen from a map or a search, so there is no such thing as a
/// place with no coordinates.
/// </summary>
public sealed class SavedPlaceTests
{
    private static readonly Guid TripId = Guid.NewGuid();
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private static SavedPlace Place(
        string name = "  Fushimi Inari  ",
        double latitude = 34.9671,
        double longitude = 135.7727,
        PlaceCategory? category = PlaceCategory.Sight,
        string? address = "  68 Fukakusa Yabunouchicho, Kyoto  ",
        string? notes = "  Go before the crowds.  ") =>
        SavedPlace.Create(TripId, name, latitude, longitude, category, address, notes, CreatedAt);

    [Fact]
    public void Create_KeepsEverythingItWasGivenAndStartsUnfiled()
    {
        var place = Place();

        Assert.NotEqual(Guid.Empty, place.Id);
        Assert.Equal(TripId, place.TripId);
        Assert.Null(place.DestinationId);
        Assert.Equal(34.9671, place.Latitude);
        Assert.Equal(135.7727, place.Longitude);
        Assert.Equal(PlaceCategory.Sight, place.Category);
        Assert.Equal(CreatedAt, place.CreatedAt);
    }

    [Fact]
    public void Create_TrimsTheNameTheAddressAndTheNotes()
    {
        var place = Place();

        Assert.Equal("Fushimi Inari", place.Name);
        Assert.Equal("68 Fukakusa Yabunouchicho, Kyoto", place.Address);
        Assert.Equal("Go before the crowds.", place.Notes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_TreatsABlankAddressAsAbsent(string blank)
    {
        var place = Place(address: blank);

        Assert.Null(place.Address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_TreatsABlankNoteAsAbsent(string blank)
    {
        var place = Place(notes: blank);

        Assert.Null(place.Notes);
    }

    [Fact]
    public void Create_AllowsNoCategory()
    {
        var place = Place(category: null);

        Assert.Null(place.Category);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsABlankName(string name)
    {
        var exception = Assert.Throws<ArgumentException>(() => Place(name: name));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void Create_RejectsANameLongerThanTheLimit()
    {
        var name = new string('x', SavedPlace.NameMaxLength + 1);

        var exception = Assert.Throws<ArgumentException>(() => Place(name: name));

        Assert.Equal("name", exception.ParamName);
        Assert.Contains($"{SavedPlace.NameMaxLength}", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_AcceptsANameAtTheLimit()
    {
        var name = new string('x', SavedPlace.NameMaxLength);

        Assert.Equal(name, Place(name: name).Name);
    }

    [Fact]
    public void Create_RejectsACategoryThatIsNotOneOfTheKnownOnes()
    {
        // A number on the wire can smuggle in a value the enum does not have.
        var exception = Assert.Throws<ArgumentException>(() => Place(category: (PlaceCategory)99));

        Assert.Equal("category", exception.ParamName);
    }

    [Fact]
    public void Create_RejectsAnAddressLongerThanTheLimit()
    {
        var address = new string('x', SavedPlace.AddressMaxLength + 1);

        var exception = Assert.Throws<ArgumentException>(() => Place(address: address));

        Assert.Equal("address", exception.ParamName);
    }

    [Fact]
    public void Create_RejectsNotesLongerThanTheLimit()
    {
        var notes = new string('x', SavedPlace.NotesMaxLength + 1);

        var exception = Assert.Throws<ArgumentException>(() => Place(notes: notes));

        Assert.Equal("notes", exception.ParamName);
    }

    [Theory]
    [InlineData(90.0, 180.0)]
    [InlineData(-90.0, -180.0)]
    [InlineData(0.0, 0.0)]
    public void Create_AcceptsAnyPositionOnTheGlobe(double latitude, double longitude)
    {
        var place = Place(latitude: latitude, longitude: longitude);

        Assert.Equal(latitude, place.Latitude);
        Assert.Equal(longitude, place.Longitude);
    }

    [Theory]
    [InlineData(90.1, 0.0, "latitude")]
    [InlineData(-90.1, 0.0, "latitude")]
    [InlineData(0.0, 180.1, "longitude")]
    [InlineData(0.0, -180.1, "longitude")]
    public void Create_RejectsAPositionOffTheGlobe(double latitude, double longitude, string parameter)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Place(latitude: latitude, longitude: longitude));

        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public void Edit_ChangesEverythingItIsGiven()
    {
        var place = Place();

        place.Edit(
            "Nishiki Market",
            35.0050,
            135.7649,
            PlaceCategory.Food,
            "Kyoto",
            "Try the tamagoyaki.");

        Assert.Equal("Nishiki Market", place.Name);
        Assert.Equal(35.0050, place.Latitude);
        Assert.Equal(135.7649, place.Longitude);
        Assert.Equal(PlaceCategory.Food, place.Category);
        Assert.Equal("Kyoto", place.Address);
        Assert.Equal("Try the tamagoyaki.", place.Notes);
    }

    [Fact]
    public void Edit_ClearsWhatIsLeftOut()
    {
        var place = Place();

        place.Edit("Fushimi Inari", 34.9671, 135.7727, null, null, null);

        Assert.Null(place.Category);
        Assert.Null(place.Address);
        Assert.Null(place.Notes);
    }

    [Fact]
    public void Edit_KeepsTheIdentityAndTheTripAndTheFiling()
    {
        var place = Place();
        var destinationId = Guid.NewGuid();

        place.MoveToDestination(destinationId);

        place.Edit("Nishiki Market", 35.0050, 135.7649, PlaceCategory.Food, null, null);

        Assert.Equal(TripId, place.TripId);
        Assert.Equal(destinationId, place.DestinationId);
        Assert.Equal(CreatedAt, place.CreatedAt);
    }

    [Fact]
    public void Edit_ChangesNothingWhenANameIsBlank()
    {
        var place = Place();
        var before = Snapshot(place);

        Assert.Throws<ArgumentException>(
            () => place.Edit("   ", 1.0, 2.0, PlaceCategory.Food, "Kyoto", "A note"));

        Assert.Equal(before, Snapshot(place));
    }

    [Fact]
    public void Edit_ChangesNothingWhenAPositionIsOffTheGlobe()
    {
        var place = Place();
        var before = Snapshot(place);

        Assert.Throws<ArgumentException>(
            () => place.Edit("Nishiki Market", 91.0, 135.7649, PlaceCategory.Food, null, null));

        Assert.Equal(before, Snapshot(place));
    }

    [Fact]
    public void Edit_ChangesNothingWhenTheCategoryIsNotOneOfTheKnownOnes()
    {
        var place = Place();
        var before = Snapshot(place);

        Assert.Throws<ArgumentException>(
            () => place.Edit("Nishiki Market", 35.0050, 135.7649, (PlaceCategory)99, null, null));

        Assert.Equal(before, Snapshot(place));
    }

    [Fact]
    public void MoveToDestination_FilesThePlaceAndThenTakesItOffTheRoute()
    {
        var place = Place();
        var destinationId = Guid.NewGuid();

        place.MoveToDestination(destinationId);
        Assert.Equal(destinationId, place.DestinationId);

        place.MoveToDestination(null);
        Assert.Null(place.DestinationId);
    }

    private static (string, double, double, PlaceCategory?, string?, string?) Snapshot(SavedPlace place) =>
        (place.Name, place.Latitude, place.Longitude, place.Category, place.Address, place.Notes);
}

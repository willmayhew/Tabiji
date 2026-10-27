namespace Tabiji.Domain.Trips;

/// <summary>
/// What kind of place a saved place is, so that a list of them can be read at
/// a glance.
///
/// The set is small and closed on purpose: a category is a label, not a
/// taxonomy, and a free-text one would soon be spelled three different ways.
/// A place the reader has not sorted yet carries no category at all, which is
/// always allowed.
/// </summary>
public enum PlaceCategory
{
    Sight = 0,
    Food = 1,
    Stay = 2,
    Shop = 3,
    Transport = 4,
    Other = 5,
    Entertainment = 6,
}

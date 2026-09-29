using System.Reactive;

namespace Automation.Models.Persons;

/// <summary>
/// The house sitter who looks after the house and Pixel while Vincent and Carleen are away.
/// To change who the house sitter is, change <see cref="GetPerson"/>.
/// </summary>
public class HouseSitterModel
{
    private readonly IDisposable _subscription;

    /// <summary>
    /// Gets a value indicating whether the house sitter is currently home.
    /// </summary>
    public bool IsHome { get; private set; }

    /// <summary>
    /// Emits when the house sitter leaves the house.
    /// </summary>
    public IObservable<Unit> Leaves { get; }

    public HouseSitterModel(IEntities entities)
    {
        var person = GetPerson(entities);
        IsHome = IsHomeState(person.State);
        _subscription = person.StateChanges().Subscribe(x => IsHome = IsHomeState(x.New?.State));

        Leaves = person.StateChanges()
            .Where(x => IsHomeState(x.Old?.State) && !IsHomeState(x.New?.State))
            .Select(_ => Unit.Default);
    }

    /// <summary>
    /// The person entity of the house sitter. This is the single place to change who the house sitter is.
    /// </summary>
    private static PersonEntity GetPerson(IEntities entities) => entities.Person.Timo;

    private static bool IsHomeState(string? state) => state?.ToLower() == "home";

    public void Dispose()
    {
        _subscription.Dispose();
    }
}

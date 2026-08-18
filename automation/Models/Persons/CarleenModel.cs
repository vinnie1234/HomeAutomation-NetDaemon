using System.Reactive.Disposables;

namespace Automation.Models.Persons;

public class CarleenModel: IPerson
{
    private readonly IDisposable _subscriptions;

    public bool IsSleeping { get; set; }
    public bool IsDriving { get; set; } = false;
    public bool IsHome { get; set; }
    public string? DirectionOfTravel { get; set; } = null;
    public string? State { get; set; }

    
    public CarleenModel(IEntities entities)
    {
        IsSleeping = entities.InputBoolean.Sleepingcarleen.IsOn();
        IsHome = entities.InputBoolean.Awaycarleen.IsOff();
        State = entities.Person.Carleen.State;
        
        _subscriptions = new CompositeDisposable(
            entities.InputBoolean.Sleepingcarleen.StateChanges().Subscribe(x => IsSleeping = x.New.IsOn()),
            entities.InputBoolean.Awaycarleen.StateChanges().Subscribe(x => IsHome = x.New.IsOff()),
            entities.Person.Carleen.StateChanges().Subscribe(x => State = x.New?.State)
        );
    }

    public void Dispose()
    {
        _subscriptions.Dispose();
    }
}
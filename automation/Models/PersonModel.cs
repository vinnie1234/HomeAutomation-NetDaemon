using System.Reactive.Disposables;

namespace Automation.Models;

public class PersonModel
{
    private readonly IDisposable _subscriptions;

    public bool IsSleeping { get; private set; }
    public bool IsDriving { get; private set; }
    public bool IsHome { get; private set; }
    public string? DirectionOfTravel { get; private set; }
    public string? State { get; private set; }

    public PersonModel(IEntities entities)
    {
        _subscriptions = new CompositeDisposable(
            entities.InputBoolean.Sleeping.StateChanges().Subscribe(x => IsSleeping = x.New.IsOn()),
            entities.BinarySensor.VincentPhoneAndroidAuto.StateChanges().Subscribe(x => IsDriving = x.New.IsOn()),
            entities.InputBoolean.Away.StateChanges().Subscribe(x => IsHome = x.New.IsOff()),
            entities.Sensor.ThuisSmS938bDirectionOfTravel.StateChanges().Subscribe(x =>
            {
                if (x.New?.State != null) DirectionOfTravel = x.New.State;
            }),
            entities.Person.VincentMaarschalkerweerd.StateChanges().Subscribe(x => State = x.New?.State)
        );
    }

    public void Dispose()
    {
        _subscriptions.Dispose();
    }
}
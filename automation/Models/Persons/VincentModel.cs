using System.Reactive.Disposables;

namespace Automation.Models.Persons;

public class VincentModel: IPerson
{
    private readonly IDisposable _subscriptions;

    public bool IsSleeping { get; set; }
    public bool IsDriving { get; set; }
    public bool IsHome { get; set; }
    public string? DirectionOfTravel { get; set; }
    public string? State { get; set; }

    public VincentModel(IEntities entities)
    {
        IsSleeping = entities.InputBoolean.Sleepingvincent.IsOn();
        IsDriving = entities.BinarySensor.VincentPhoneAndroidAuto.IsOn();
        IsHome = entities.InputBoolean.Awayvincent.IsOff();
        DirectionOfTravel = entities.Sensor.ThuisSmS938bDirectionOfTravel.State;
        State = entities.Person.VincentMaarschalkerweerd.State;

        _subscriptions = new CompositeDisposable(
            entities.InputBoolean.Sleepingvincent.StateChanges().Subscribe(x => IsSleeping = x.New.IsOn()),
            entities.BinarySensor.VincentPhoneAndroidAuto.StateChanges().Subscribe(x => IsDriving = x.New.IsOn()),
            entities.InputBoolean.Awayvincent.StateChanges().Subscribe(x => IsHome = x.New.IsOff()),
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
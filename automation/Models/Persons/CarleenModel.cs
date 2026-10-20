using System.Reactive.Disposables;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.Models.Persons;

public class CarleenModel: IPerson
{
    private readonly IDisposable _subscriptions;

    public bool IsSleeping { get; set; }
    public bool IsDriving { get; set; } = false;
    public bool IsHome { get; set; }
    public string? DirectionOfTravel { get; set; } = null;
    public string? State { get; set; }

    /// <summary>Carleen's WhatsApp "lid" identifier, from <c>config.json</c> (<see cref="Automation.Configuration.WhatsAppConfig"/>).</summary>
    public string Whatsapp { get; }

    public CarleenModel(IEntities entities, IOptions<AppConfig> config)
    {
        Whatsapp = config.Value.Whatsapp.Carleen;
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
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using NetDaemon.Client.HomeAssistant.Model;
using NSubstitute;

namespace TestAutomation.Helpers;

public class HaContextMock
{
    public HaContextMock()
    {
        HaContext = Substitute.For<IHaContext>();
        HaContext.StateAllChanges().Returns(
            StateChangeSubject
        );
        HaContext.StateChanges().Returns(
            StateChangeSubject.Where(n => n.New?.State != n.Old?.State)
        );
        HaContext.Events.Returns(_ => EventSubject.Select(_ => new Event()));
    }

    public IHaContext HaContext { get; init; }
    public Subject<StateChange> StateChangeSubject { get; } = new();
    public Subject<HassEvent> EventSubject { get; } = new();
}

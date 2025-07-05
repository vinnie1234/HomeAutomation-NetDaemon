using System.Reactive.Linq;
using System.Reactive.Subjects;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;
using NetDaemon.Client.HomeAssistant.Model;
using NSubstitute;
using Microsoft.Reactive.Testing;

namespace TestAutomation.Helpers;

public class HaContextMock
{
    private readonly TestScheduler? _scheduler;

    public HaContextMock(TestScheduler? scheduler = null)
    {
        _scheduler = scheduler;
        HaContext = Substitute.For<IHaContext>();
        StateChangeSubject = new Subject<StateChange>();
        EventSubject = new Subject<HassEvent>();

        if (_scheduler != null)
        {
            // When TestScheduler is provided, use it for time-based operations
            HaContext.StateAllChanges().Returns(
                StateChangeSubject.ObserveOn(_scheduler)
            );
            HaContext.StateChanges().Returns(
                StateChangeSubject.Where(n => n.New?.State != n.Old?.State).ObserveOn(_scheduler)
            );
            HaContext.Events.Returns(_ => EventSubject.Select(_ => new Event()).ObserveOn(_scheduler));
        }
        else
        {
            // Fallback to immediate execution for simple reactive tests
            HaContext.StateAllChanges().Returns(StateChangeSubject);
            HaContext.StateChanges().Returns(
                StateChangeSubject.Where(n => n.New?.State != n.Old?.State)
            );
            HaContext.Events.Returns(_ => EventSubject.Select(_ => new Event()));
        }
    }

    public IHaContext HaContext { get; init; }
    public Subject<StateChange> StateChangeSubject { get; }
    public Subject<HassEvent> EventSubject { get; }
    
    /// <summary>
    /// Trigger all pending reactive operations synchronously
    /// This is useful for testing immediate reactive behaviors
    /// </summary>
    public void ProcessPendingOperations()
    {
        if (_scheduler != null)
        {
            _scheduler.AdvanceBy(1); // Process immediate operations
        }
    }
}

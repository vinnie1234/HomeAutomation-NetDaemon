using Automation.apps;
using Automation.Interfaces;
using Microsoft.Reactive.Testing;
using NetDaemon.HassModel;
using NSubstitute;

namespace TestAutomation.Helpers;

public class AppTestContext
{
    public TestScheduler Scheduler { get; } = new();
    public HaContextMock HaContextMock { get; }
    public IHaContext HaContext => HaContextMock.HaContext;
    public INotify Notify { get; }
    private IDataRepository DataRepository { get; } = Substitute.For<IDataRepository>();

    private AppTestContext(bool useSchedulerForReactive = false)
    {
        Scheduler.AdvanceTo(DateTimeOffset.Now.ToUnixTimeMilliseconds());
        HaContextMock = new HaContextMock(useSchedulerForReactive ? Scheduler : null);
        Notify = new Notify(HaContext, DataRepository);
    }
    
    public static AppTestContext New()
    {
        return new AppTestContext();
    }
    
    public static AppTestContext NewWithScheduler()
    {
        return new AppTestContext(useSchedulerForReactive: true);
    }
    
    public void AdvanceTimeTo(long absoluteTime)
    {
        Scheduler.AdvanceTo(absoluteTime);
    }
    
    public void AdvanceTimeBy(long absoluteTime)
    {
        Scheduler.AdvanceBy(absoluteTime);
    }
    
    public void SetCurrentTime(DateTime time)
    {
        AdvanceTimeTo(time.Ticks);
    }
}
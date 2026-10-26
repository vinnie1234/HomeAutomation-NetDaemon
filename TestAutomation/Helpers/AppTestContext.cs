using Automation.apps;
using Automation.Configuration;
using Automation.Helpers;
using Automation.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Reactive.Testing;
using NetDaemon.HassModel;
using NSubstitute;

namespace TestAutomation.Helpers;

public class AppTestContext : IDisposable
{
    private ILivingRoomPresenceService? _livingRoomPresenceService;

    /// <summary>
    /// Gets the Spotcast mock so tests can verify <see cref="ISpotcast.PlaySpotify"/> was called.
    /// </summary>
    public ISpotcast Spotcast { get; } = Substitute.For<ISpotcast>();

    /// <summary>
    /// The moment the test clock starts at.
    /// </summary>
    public static readonly DateTimeOffset StartTime = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public TestScheduler Scheduler { get; } = new();
    public HaContextMock HaContextMock { get; }
    public IHaContext HaContext => HaContextMock.HaContext;
    public INotify Notify { get; }
    private IDataRepository DataRepository { get; } = Substitute.For<IDataRepository>();

    /// <summary>
    /// Gets the configuration used for services that are created by this context.
    /// Change it before the app under test is initialized.
    /// </summary>
    public AppConfiguration Config { get; } = new();

    /// <summary>
    /// Gets the real circadian lighting service, so apps under test use the actual curve.
    /// </summary>
    public ICircadianLightingService CircadianLightingService =>
        field ??= new CircadianLightingService(
            HaContext,
            Options.Create(Config),
            NullLogger<CircadianLightingService>.Instance);

    /// <summary>
    /// Gets the real living room presence service, wired to the test scheduler.
    /// </summary>
    public ILivingRoomPresenceService LivingRoomPresenceService =>
        _livingRoomPresenceService ??= new LivingRoomPresenceService(
            HaContext,
            Scheduler,
            Options.Create(Config),
            NullLogger<LivingRoomPresenceService>.Instance);

    private AppTestContext(bool useSchedulerForReactive = false)
    {
        // Start the test clock (UTC ticks) at a fixed, realistic moment that lies before any time a test
        // moves to: the TestScheduler can't go back in time. Year 1 doesn't work with the Dutch time zone,
        // whose historical offset (local mean time) isn't a whole number of minutes.
        Scheduler.AdvanceTo(StartTime.UtcTicks);
        HaContextMock = new HaContextMock(useSchedulerForReactive ? Scheduler : null);
        Notify = new Notify(HaContext, DataRepository, Substitute.For<ILogger<Notify>>(), Options.Create(new AppConfig()));
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
    
    public void Dispose()
    {
        (_livingRoomPresenceService as IDisposable)?.Dispose();
    }
}
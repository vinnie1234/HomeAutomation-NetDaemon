using System.Reactive.Concurrency;
using Automation.Helpers;

namespace Automation.apps.General;

/// <summary>
/// Represents an application that manages the NetDaemon and handles its restart logic.
/// </summary>
[NetDaemonApp(Id = nameof(NetDaemon))]
public class NetDaemon : BaseApp, IAsyncInitializable, IDisposable
{
    private readonly string _discordLogChannel = ConfigManager.GetValueFromConfigNested("Discord", "Logs") ?? "";

    /// <summary>
    /// Initializes a new instance of the <see cref="NetDaemon"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    /// <param name="storage">The data repository for storing and retrieving data.</param>
    private readonly IDataRepository _storage;

    public NetDaemon(IHaContext ha, ILogger<NetDaemon> logger,
        INotify notify, IScheduler scheduler, IDataRepository storage)
        : base(ha, logger, notify, scheduler)
    {
        _storage = storage;
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        Notify.NotifyDiscord("Het huis is opnieuw opgestart net 9 test", [_discordLogChannel]);

        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes the resources used by the <see cref="NetDaemon"/> class.
    /// </summary>
#pragma warning disable CA1816
    public void Dispose()
#pragma warning restore CA1816
    {
        Notify.NotifyDiscord("NetDaemon stopped", [_discordLogChannel]);
    }
}
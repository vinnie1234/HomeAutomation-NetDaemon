using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Automation.Helpers;
using Microsoft.Extensions.Options;
using Automation.Configuration;

namespace Automation.apps.General;

/// <summary>
/// Represents an application that manages the NetDaemon and handles its restart logic.
/// </summary>
[NetDaemonApp(Id = nameof(NetDaemon))]
public class NetDaemon : BaseApp, IAsyncInitializable, IDisposable
{
    private readonly AppConfig _config;

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
        INotify notify, IScheduler scheduler, IDataRepository storage,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler)
    {
        _config = config.Value;
        _storage = storage;
    }

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        var lightColor = _storage.Get<IReadOnlyList<double>>("NetDaemonRestart");

        if (lightColor is { Count: >= 3 })
        {
            // Translate the value from IReadOnlyList<double> to IReadOnlyCollection<int>
            IReadOnlyCollection<int> lightColorInInt = [(int)lightColor[0], (int)lightColor[1], (int)lightColor[2]];
            Entities.Light.Koelkast.TurnOn(rgbColor: lightColorInInt);
        }

        // Always clear the stored color: it has been consumed, and clearing it with a
        // typed null keeps the stored JSON readable as IReadOnlyList<double> next time.
        _storage.Save<IReadOnlyList<double>?>("NetDaemonRestart", null);

        if (!Entities.InputBoolean.Sleepingvincent.IsOn() && !Entities.InputBoolean.Sleepingcarleen.IsOn())
            Notify.NotifyHouse("Het huis is opnieuw opgestart", "Het huis is opnieuw opgestart", true);
        Notify.NotifyDiscord("Het huis is opnieuw opgestart", [_config.Discord.Logs]);

        Entities.InputButton.Restartnetdaemon.StateChanges().Subscribe(_ =>
        {
            _storage.Save("NetDaemonRestart", Entities.Light.Koelkast.Attributes?.RgbColor);
            Entities.Light.Koelkast.TurnOn(rgbColor: LightColors.Red);
            Notify.NotifyHouse("Het huis wordt opnieuw opgestart", "Het huis wordt opnieuw opgestart", true);

            Observable.Timer(TimeSpan.FromSeconds(5), Scheduler).Subscribe(_ =>
            {
                Services.Hassio.AddonRestart("c6a2317c_netdaemon6");
            });
        });
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes the resources used by the <see cref="NetDaemon"/> class.
    /// </summary>
#pragma warning disable CA1816
    public void Dispose()
#pragma warning restore CA1816
    {
        Notify.NotifyDiscord("NetDaemon stopped", [_config.Discord.Logs]);
    }
}
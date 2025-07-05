using System.Reactive.Concurrency;
using System.Threading;
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

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var lightColor = await _storage.GetAsync<IReadOnlyList<double>>("NetDaemonRestart");

        if (lightColor != null && lightColor.ToString() != "")
        {
            // Translate the value from IReadOnlyList<double> to IReadOnlyCollection<int>
            IReadOnlyCollection<int> lightColorInInt = [(int)lightColor[0], (int)lightColor[1], (int)lightColor[2]];
            Entities.Light.Koelkast.TurnOn(rgbColor: lightColorInInt);
            await _storage.SaveAsync("NetDaemonRestart", "");
        }

        if (!Entities.InputBoolean.Sleeping.IsOn())
            _ = Notify.NotifyHouse("Het huis is opnieuw opgestart", "Het huis is opnieuw opgestart", true);
        Notify.NotifyDiscord("Het huis is opnieuw opgestart", [_discordLogChannel]);

        Entities.InputButton.Restartnetdaemon.StateChanges().Subscribe(async _ =>
        {
            await _storage.SaveAsync("NetDaemonRestart", Entities.Light.Koelkast.Attributes?.RgbColor);
            Entities.Light.Koelkast.TurnOn(colorName: "red");
            await Notify.NotifyHouse("Het huis wordt opnieuw opgestart", "Het huis wordt opnieuw opgestart", true);

            Thread.Sleep(TimeSpan.FromSeconds(5));
            Services.Hassio.AddonRestart("c6a2317c_netdaemon5");
        });
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
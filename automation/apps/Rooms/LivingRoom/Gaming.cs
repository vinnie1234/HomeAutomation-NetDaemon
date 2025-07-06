using System.Reactive.Concurrency;

namespace Automation.apps.Rooms.LivingRoom;

[NetDaemonApp(Id = nameof(Gaming))]
public class Gaming : BaseApp
{
    /// <summary>
    /// Gets a value indicating whether light automations are disabled.
    /// </summary>
    private bool DisableLightAutomations => Entities.InputBoolean.Disablelightautomationlivingroom.IsOn();

    /// <summary>
    /// Initializes a new instance of the <see cref="Gaming"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    public Gaming(
        IHaContext ha,
        ILogger<Gaming> logger,
        INotify notify,
        IScheduler scheduler)
        : base(ha, logger, notify, scheduler)
    {
        Entities.DeviceTracker.Sony.WhenTurnsOn(_ => GameSetUp());
    }

    /// <summary>
    /// Sets up the gaming environment by turning on and configuring various devices and lights.
    /// </summary>
    private void GameSetUp()
    {
        Logger.LogDebug("Gaming lights");

        if (!DisableLightAutomations)
        {
            Entities.MediaPlayer.Tv.TurnOn();
            Entities.MediaPlayer.Tv.SelectSource("HDMI2");
            Entities.MediaPlayer.AvSoundbar.TurnOn();
            Entities.Light.PlafondWoonkamer.TurnOff();
            Entities.Light.Plafond.TurnOff();
            Entities.Light.Nachtkastje.TurnOff();
            Entities.Light.HueFilamentBulb1.TurnOff();
            Entities.Light.HueFilamentBulb2.TurnOff();
            Entities.MediaPlayer.Tv.VolumeSet(0.14);
        }
    }
}
using System.Reactive.Concurrency;
using Automation.Enum;
using Automation.Helpers;
using static Automation.Globals;

namespace Automation.apps.Rooms.LivingRoom;

[NetDaemonApp(Id = nameof(Tv))]
public class Tv : BaseApp
{
    /// <summary>
    /// Gets a value indicating whether the system is in working mode.
    /// </summary>
    private bool IsWorking => Entities.InputBoolean.Working.IsOn();

    /// <summary>
    /// Gets a value indicating whether light automations are disabled.
    /// </summary>
    private bool DisableLightAutomations => Entities.InputBoolean.Disablelightautomationlivingroom.IsOn();

    /// <summary>
    /// Whether a TV state means the TV is on.
    /// </summary>
    private static bool IsTvOnState(string? state) => state is "on" or "playing" or "paused" or "idle";

    /// <summary>
    /// Initializes a new instance of the <see cref="Tv"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    public Tv(
        IHaContext ha,
        ILogger<Tv> logger,
        INotify notify,
        IScheduler scheduler)
        : base(ha, logger, notify, scheduler)
    {
        // The TV also reports playing/paused/idle while it is on, not only "on"
        Entities.MediaPlayer.Tv.StateChanges()
            .Where(x => x.Old?.State == "off" && IsTvOnState(x.New?.State))
            .Subscribe(_ => MovieTime());
        Entities.MediaPlayer.Tv.StateChanges()
            .Where(x => IsTvOnState(x.Old?.State) && x.New?.State == "off")
            .Subscribe(_ => LetThereBeLight());

        // Green is broken, red is always good
        Entities.Light.Bank
            .StateChanges()
            .Where(x => x.New.IsOn())
            .Subscribe(x =>
            {
                x.Entity.TurnOn(rgbColor: LightColors.Red);
            });
    }

    /// <summary>
    /// Handles the actions to be performed when the TV is turned off.
    /// </summary>
    private void LetThereBeLight()
    {
        Logger.LogDebug("TV Turned off");

        if (DisableLightAutomations) return;

        Entities.MediaPlayer.AvSoundbar.TurnOff();
        Entities.Switch.Ps5VincentPower.TurnOff();

        // The TV is also turned off when going to sleep or leaving: don't turn the living room lights back on then
        if (IsSleepMode || Entities.InputBoolean.Away.IsOn()) return;

        switch (GetHouseState(Entities))
        {
            case HouseState.Morning:
                Entities.Scene.Woonkamermorning.TurnOn();
                break;
            case HouseState.Day:
                Entities.Scene.Woonkamerday.TurnOn();
                break;
            case HouseState.Evening:
                Entities.Scene.Woonkamerevening.TurnOn();
                break;
            case HouseState.Night:
                Entities.Scene.Woonkamernight.TurnOn();
                break;
            default:
                Entities.Scene.Woonkamerday.TurnOn();
                break;
        }

        if (IsWorking) Entities.Light.Plafondslaapkamer.TurnOn();
    }

    /// <summary>
    /// Handles the actions to be performed when the TV is turned on.
    /// </summary>
    private void MovieTime()
    {
        Logger.LogDebug("TV Turned on");

        if (!DisableLightAutomations)
        {
            Entities.Scene.WoonkamerMovie2.TurnOn();
            Entities.MediaPlayer.AvSoundbar.TurnOn();
            Entities.Light.PlafondWoonkamer.TurnOff();
            Entities.Light.HueFilamentBulb1.TurnOff();
            Entities.Light.HueFilamentBulb2.TurnOff();
            Entities.Light.LampenKeuken.TurnOff();
            Entities.Light.Plafondslaapkamer.TurnOff();
            Entities.Light.Nachtkastje.TurnOff();
            Entities.MediaPlayer.Tv.VolumeSet(0.14);
            Logger.LogDebug("Movie time started!");
        }
    }
}
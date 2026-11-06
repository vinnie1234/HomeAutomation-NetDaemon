using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Enum;
using Automation.Helpers;
using Microsoft.Extensions.Options;
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

    /// <summary>How long the TV must stay on before the movie scene starts.</summary>
    public static readonly TimeSpan TurnOnDelay = TimeSpan.FromSeconds(30);

    private bool _tvIsOn;
    private bool _turnOnPending;

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
    /// <param name="config">The application settings.</param>
    public Tv(
        IHaContext ha,
        ILogger<Tv> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        // The TV also reports playing/paused/idle while it is on, not only "on". It sometimes flashes "on" for a few seconds without
        // anyone switching it on (the lights then went out for no reason), so the movie scene only starts when it stays on.
        Entities.MediaPlayer.Tv.StateChanges().Subscribe(x => _tvIsOn = IsTvOnState(x.New?.State));
        Entities.MediaPlayer.Tv.StateChanges()
            .Where(x => x.Old?.State == "off" && IsTvOnState(x.New?.State))
            .Do(_ => _turnOnPending = true)
            .Select(_ => Observable.Timer(TurnOnDelay, Scheduler))
            .Switch()
            .Do(_ => _turnOnPending = false)
            .Where(_ => _tvIsOn)
            .Subscribe(_ => MovieTime());
        Entities.MediaPlayer.Tv.StateChanges()
            .Where(x => IsTvOnState(x.Old?.State) && x.New?.State == "off")
            .Subscribe(_ =>
            {
                // It went off again before the movie scene started: the lights were never touched, so leave them
                if (_turnOnPending)
                {
                    _turnOnPending = false;
                    return;
                }
                LetThereBeLight();
            });

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
                Entities.Scene.Woonkamermorning.Activate();
                break;
            case HouseState.Day:
                Entities.Scene.Woonkamerday.Activate();
                break;
            case HouseState.Evening:
                Entities.Scene.Woonkamerevening.Activate();
                break;
            case HouseState.Night:
                Entities.Scene.Woonkamernight.Activate();
                break;
            default:
                Entities.Scene.Woonkamerday.Activate();
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
            Entities.Scene.WoonkamerMovie2.Activate();
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
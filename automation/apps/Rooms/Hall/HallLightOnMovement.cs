using System.Reactive.Concurrency;
using Automation.Helpers;
using static Automation.Globals;

namespace Automation.apps.Rooms.Hall;

[NetDaemonApp(Id = nameof(HallLightOnMovement))]
public class HallLightOnMovement : BaseApp
{
    /// <summary>
    /// Gets a value indicating whether light automations are disabled.
    /// </summary>
    private bool DisableLightAutomations => Entities.InputBoolean.Disablelightautomationhall.IsOn();

    private readonly ICircadianLightingService _circadianLightingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="HallLightOnMovement"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    /// <param name="circadianLightingService">The service providing the brightness and color temperature that match the time of day.</param>
    public HallLightOnMovement(
        IHaContext ha,
        ILogger<HallLightOnMovement> logger,
        INotify notify,
        IScheduler scheduler,
        ICircadianLightingService circadianLightingService)
        : base(ha, logger, notify, scheduler)
    {
        _circadianLightingService = circadianLightingService;
        InitializeLights();

        HaContext.Events.Where(x => x.EventType == "hue_event").Subscribe(x =>
        {
            var eventModel = x.DataElement?.ToObject<EventModel>();
            if (eventModel != null) OverwriteSwitch(eventModel);
        });
    }

    /// <summary>
    /// Initializes the light automation based on motion sensor state changes.
    /// </summary>
    private void InitializeLights()
    {
        Entities.BinarySensor.GangMotion
            .StateChanges()
            .Where(x => x.New.IsOn() && !DisableLightAutomations)
            .Subscribe(_ => ChangeLight(true, GetBrightness()));

        Entities.BinarySensor.GangMotion
            .StateChanges()
            .WhenStateIsFor(x => x.IsOff(), TimeSpan.FromMinutes(GetStateTime()), Scheduler)
            .Where(_ => !DisableLightAutomations)
            .Subscribe(_ => ChangeLight(false));
    }

    /// <summary>
    /// Gets the brightness level based on the sleeping state.
    /// </summary>
    /// <returns>The brightness level.</returns>
    private int GetBrightness()
    {
        // On office days Vincent needs full brightness regardless of Carleen sleeping
        if (IsOfficeDay(Entities, DateTimeOffset.Now.DayOfWeek) && !Vincent.IsSleeping)
            return 100;

        return _circadianLightingService.GetBrightness(IsNightMode);
    }

    /// <summary>
    /// Gets the state time based on the sleeping state.
    /// </summary>
    /// <returns>The state time in minutes.</returns>
    private int GetStateTime()
    {
        return IsNightMode switch
        {
            true => Convert.ToInt32(Entities.InputNumber.Halllightnighttime.State),
            false => Convert.ToInt32(Entities.InputNumber.Halllightdaytime.State)
        };
    }

    /// <summary>
    /// Changes the state of the lights.
    /// </summary>
    /// <param name="on">A value indicating whether to turn the lights on or off.</param>
    /// <param name="brightnessPct">The brightness percentage.</param>
    private void ChangeLight(bool on, int brightnessPct = 0)
    {
        switch (on)
        {
            case true:
                var colorTemp = _circadianLightingService.GetColorTemperature(Entities.Light.Hal2);
                Entities.Light.Hal2.TurnOn(brightnessPct: brightnessPct, transition: 5, colorTempKelvin: colorTemp);
                if (!IsNightMode || (!Vincent.IsSleeping && !Carleen.IsSleeping && !Entities.InputBoolean.Away.IsOn()))
                {
                    Entities.Light.Hal.TurnOn();
                    if (Entities.Light.Hal.IsOff())
                        Entities.Switch.Bot29ff.TurnOn();
                    
                    if(Entities.MediaPlayer.Tv.IsOff())
                        Entities.Light.GangSigaret.TurnOn();
                }

                break;
            case false:
                Entities.Light.Hal.TurnOff();
                Entities.Light.Hal2.TurnOff();
                Entities.Light.GangSigaret.TurnOff();
                break;
        }
    }

    /// <summary>
    /// Handles the switch events for the hall lights.
    /// </summary>
    /// <param name="eventModel">The event model containing the switch event data.</param>
    private void OverwriteSwitch(EventModel eventModel)
    {
        const string hueSwitchBathroomId = "4339833970e35ff10c568a94b59e50dd";

        if (eventModel is { DeviceId: hueSwitchBathroomId, Type: "initial_press" })
            switch (eventModel.Subtype)
            {
                //button one: toggle "everybody away"
                case 1:
                    if (Vincent.IsHome || Carleen.IsHome)
                    {
                        Entities.InputBoolean.Awayvincent.TurnOn();
                        Entities.InputBoolean.Awaycarleen.TurnOn();
                    }
                    else
                    {
                        Entities.InputBoolean.Awayvincent.TurnOff();
                        Entities.InputBoolean.Awaycarleen.TurnOff();
                    }
                    break;
                //button two
                case 2:
                    Entities.Light.Hal2.TurnOn(brightnessStepPct: 10);
                    break;
                //button three
                case 3:
                    Entities.Light.Hal2.TurnOn(brightnessStepPct: -10);
                    break;
                //button four
                case 4:
                    Entities.MediaPlayer.FriendsSpeakers.VolumeSet(0.5);
                    Entities.Light.Hal.TurnOn();
                    Entities.Switch.Bot29ff.TurnOn();
                    Entities.Light.Hal2.TurnOff();
                    Notify.SendMusicToHome("http://192.168.50.189:8123/local/Friends.mp3");

                    break;
            }
    }
}
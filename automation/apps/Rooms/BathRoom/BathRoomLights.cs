using System.Reactive.Concurrency;
using Automation.Helpers;
using static Automation.Globals;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.Rooms.BathRoom;

[NetDaemonApp(Id = nameof(BathRoomLights))]
public class BathRoomLights : BaseApp
{
    /// <summary>
    /// Gets a value indicating whether it is nighttime.
    /// </summary>
    private bool IsNighttime => Entities.InputSelect.Housemodeselect.State == "Night";

    private ISpotcast _spotcast;
    private readonly AppConfig _config;
    private readonly ICircadianLightingService _circadianLightingService;
    
    /// <summary>
    /// Gets a value indicating whether light automations are disabled.
    /// </summary>
    private bool DisableLightAutomations => Entities.InputBoolean.Disablelightautomationbathroom.IsOn();

    /// <summary>
    /// Gets a value indicating whether the shower is in use.
    /// </summary>
    private bool IsDouching => Entities.InputBoolean.Douchen.IsOn();

    /// <summary>
    /// Initializes a new instance of the <see cref="BathRoomLights"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    /// <param name="spotcast">The spotcast service</param>
    /// <param name="config">The application configuration.</param>
    /// <param name="circadianLightingService">The service providing the brightness and color temperature that match the time of day.</param>
    public BathRoomLights(
        IHaContext ha,
        ILogger<BathRoomLights> logger,
        INotify notify,
        IScheduler scheduler,
        ISpotcast spotcast,
        IOptions<AppConfig> config,
        ICircadianLightingService circadianLightingService)
        : base(ha, logger, notify, scheduler)
    {
        _config = config.Value;
        _spotcast = spotcast;
        _circadianLightingService = circadianLightingService;
        
        HaContext.Events.Where(x => x.EventType == "hue_event").Subscribe(x =>
        {
            var eventModel = x.DataElement?.ToObject<EventModel>();
            if (eventModel != null) OverwriteSwitch(eventModel);
        });

        InitializeLights();
        ToothbrushHandler();
    }

    /// <summary>
    /// Initializes the light automation based on motion sensor state changes.
    /// </summary>
    private void InitializeLights()
    {
        Entities.BinarySensor.BadkamerMotion
            .StateChanges()
            .Where(x => x.Old.IsOff() && x.New.IsOn() && !DisableLightAutomations)
            .Subscribe(_ => ChangeLight(true, GetBrightness()));

        Entities.BinarySensor.BadkamerMotion
            .StateChanges()
            .WhenStateIsFor(x => x.IsOff(),
                TimeSpan.FromMinutes((int)(Entities.InputNumber.Bathroomlightnighttime.State ?? 0)), Scheduler)
            .Where(x => x.Old.IsOn() && !DisableLightAutomations && !IsDouching && IsNighttime && IsNightMode)
            .Subscribe(_ => ChangeLight(false));

        Entities.BinarySensor.BadkamerMotion
            .StateChanges()
            .WhenStateIsFor(x => x.IsOff(), TimeSpan.FromMinutes((int)(Entities.InputNumber.Bathroomlightdaytime.State ?? 0)),
                Scheduler)
            .Where(x => x.Old.IsOn() && !DisableLightAutomations && !IsDouching && !IsNightMode)
            .Subscribe(_ => ChangeLight(false));

        Entities.InputBoolean.Douchen
            .StateChanges()
            .Subscribe(x => DouchingAutomation(x.New.IsOn()));
    }

    /// <summary>
    /// Handles the automation when the shower is in use.
    /// </summary>
    /// <param name="isOn">A value indicating whether the shower is in use.</param>
    private void DouchingAutomation(bool isOn)
    {
        if (isOn)
        {
            if (!IsNightMode)
            {
                Entities.MediaPlayer.Googlehome0351.VolumeSet(0.40);
                _spotcast.PlaySpotify(Entities.MediaPlayer.Googlehome0351,
                    Carleen.IsHome ? _config.SpotifyDoucheUrl : _config.SpotifyRadioNlUrl);
            }
            Entities.Light.BadkamerSpiegel.TurnOn(brightnessPct: 100);
            Entities.Light.PlafondBadkamer.TurnOn(brightnessPct: 100);
            Entities.Cover.Rollerblind0003.OpenCover();
            Notify.NotifyHouse("timeToDouche", "Tijd om te douchen", true);
            Scheduler.Schedule(TimeSpan.FromHours(1), () =>
            {
                if (IsDouching)
                {
                    Entities.MediaPlayer.Googlehome0351.MediaStop();
                    Entities.InputBoolean.Douchen.TurnOff();
                    Notify.NotifyHouse("toLongDouchen",
                        "Je bent of een uur aan het douchen, of weer is vergeten alles uit te zetten!", false, 60);
                }
            });
        }
        else
        {
            Scheduler.Schedule(TimeSpan.FromMinutes(3), () =>
            {
                if (!IsDouching)
                {
                    Entities.Light.BadkamerSpiegel.TurnOff();
                    Entities.Light.PlafondBadkamer.TurnOff();
                }
            });

            Entities.Cover.Rollerblind0003.CloseCover();
            Entities.MediaPlayer.Googlehome0351.MediaPause();
            Notify.NotifyHouse("readyDouche", "Klaar met douchen", true);
        }
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
    /// Changes the state of the lights.
    /// </summary>
    /// <param name="on">A value indicating whether to turn the lights on or off.</param>
    /// <param name="brightnessPct">The brightness percentage.</param>
    private void ChangeLight(bool on, int brightnessPct = 0)
    {
        switch (on)
        {
            case true:
                Entities.Light.PlafondBadkamer.TurnOn(
                    brightnessPct: brightnessPct,
                    transition: 2,
                    colorTempKelvin: _circadianLightingService.GetColorTemperature(Entities.Light.PlafondBadkamer));
                Entities.Light.BadkamerSpiegel.TurnOn(
                    brightnessPct: brightnessPct,
                    transition: 2,
                    colorTempKelvin: _circadianLightingService.GetColorTemperature(Entities.Light.BadkamerSpiegel));
                break;
            case false:
                Entities.Light.PlafondBadkamer.TurnOff();
                Entities.Light.BadkamerSpiegel.TurnOff();
                break;
        }
    }

    /// <summary>
    /// Handles the switch events for the bathroom lights.
    /// </summary>
    /// <param name="eventModel">The event model containing the switch event data.</param>
    private void OverwriteSwitch(EventModel eventModel)
    {
        const string hueSwitchBathroomId = "3dcab87acc97379282b359fdf3557a52";

        if (eventModel is { DeviceId: hueSwitchBathroomId, Type: "initial_press" })
            switch (eventModel.Subtype)
            {
                //button one
                case 1:
                    Entities.Light.BadkamerSpiegel.TurnOff();
                    Entities.Light.PlafondBadkamer.TurnOff();
                    break;
                //button two
                case 2:
                    Entities.Light.BadkamerSpiegel.TurnOn(brightnessStepPct: 10);
                    Entities.Light.PlafondBadkamer.TurnOn(brightnessStepPct: 10);
                    break;
                //button three
                case 3:
                    Entities.Light.BadkamerSpiegel.TurnOn(brightnessStepPct: -10);
                    Entities.Light.PlafondBadkamer.TurnOn(brightnessStepPct: -10);
                    break;
                //button four
                case 4:
                    Entities.InputBoolean.Douchen.Toggle();
                    break;
            }
    }

    /// <summary>
    /// Handles the automation for the toothbrush state changes.
    /// </summary>
    private void ToothbrushHandler()
    {
        Entities.Sensor.SmartSeries400097aeToothbrushState
            .StateChanges()
            .Where(x => x.New?.State != "idle" && x.Old?.State == "idle")
            .Subscribe(_ =>
            {
                if (!IsDouching && !IsNightMode)
                {
                    Entities.MediaPlayer.Googlehome0351.VolumeSet(0.25);
                    _spotcast.PlaySpotify(Entities.MediaPlayer.Googlehome0351,
                        Carleen.IsHome ? _config.SpotifyDoucheUrl : _config.SpotifyRadioNlUrl);

                    Entities.MediaPlayer.Googlehome0351.MediaPlay();
                }
            });

        Entities.Sensor.SmartSeries400097aeToothbrushState
            .StateChanges()
            .WhenStateIsFor(x => x?.State == "idle" && (Vincent.IsHome || Carleen.IsHome),
                TimeSpan.FromSeconds(30), Scheduler)
            .Subscribe(_ =>
            {
                if (!IsDouching)
                {
                    Entities.MediaPlayer.Googlehome0351.MediaStop();
                    Entities.Light.Slaapkamer.TurnOn();
                }
            });
    }
}
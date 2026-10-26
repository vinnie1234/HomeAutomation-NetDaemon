using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using Automation.Helpers;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.Rooms.BathRoom;

[NetDaemonApp(Id = nameof(BathRoomLights))]
public class BathRoomLights : BaseApp
{
    /// <summary>The lights-off delay used while the bathroom light input_numbers have no value.</summary>
    private static readonly TimeSpan DefaultLightOffDelay = TimeSpan.FromMinutes(5);

    /// <summary>The "showering for an hour" check of the current shower, cancelled when showering stops.</summary>
    private readonly SerialDisposable _douchingTooLongCheck = new();

    private readonly ISpotcast _spotcast;
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
        : base(ha, logger, notify, scheduler, config)
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
        // While showering the lights stay at full strength: moving under the shower must not dim them to the
        // circadian (or, while someone else still sleeps, night) brightness
        Entities.BinarySensor.BadkamerMotion
            .StateChanges()
            .Where(x => x.Old.IsOff() && x.New.IsOn() && !DisableLightAutomations && !IsHouseUnattended && !IsDouching)
            .Subscribe(_ => ChangeLight(true, GetBrightness()));

        // Lights off once there was no motion for a while: shorter while someone sleeps. The waiting time is
        // read each time the motion stops, so changing the input_numbers in Home Assistant applies right away.
        Entities.BinarySensor.BadkamerMotion
            .StateChanges()
            .WhenOffFor(GetLightOffDelay, Scheduler)
            .Where(x => x.Old.IsOn() && !DisableLightAutomations && !IsDouching)
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
            if (!IsSleepMode)
            {
                Entities.MediaPlayer.Googlehome0351.VolumeSet(0.40);
                _spotcast.PlaySpotify(Entities.MediaPlayer.Googlehome0351,
                    Carleen.IsHome ? _config.SpotifyDoucheUrl : _config.SpotifyRadioNlUrl);
            }
            Entities.Light.BadkamerSpiegel.TurnOn(brightnessPct: 100);
            Entities.Light.PlafondBadkamer.TurnOn(brightnessPct: 100);
            Entities.Cover.Rollerblind0003.OpenCover();
            Notify.NotifyHouse("timeToDouche", "Tijd om te douchen", true);
            // Replaces the check of an earlier shower, so that one can't end this shower early
            _douchingTooLongCheck.Disposable = Scheduler.Schedule(TimeSpan.FromHours(1), () =>
            {
                if (IsDouching)
                {
                    Entities.MediaPlayer.Googlehome0351.StopIfPlaying();
                    Entities.InputBoolean.Douchen.TurnOff();
                    Notify.NotifyHouse("toLongDouchen",
                        "Je bent of een uur aan het douchen, of weer is vergeten alles uit te zetten!", false, 60);
                }
            });
        }
        else
        {
            _douchingTooLongCheck.Disposable = Disposable.Empty;

            Scheduler.Schedule(TimeSpan.FromMinutes(3), () =>
            {
                if (!IsDouching)
                {
                    Entities.Light.BadkamerSpiegel.TurnOff();
                    Entities.Light.PlafondBadkamer.TurnOff();
                }
            });

            Entities.Cover.Rollerblind0003.CloseCover();
            Entities.MediaPlayer.Googlehome0351.PauseIfPlaying();
            Notify.NotifyHouse("readyDouche", "Klaar met douchen", true);
        }
    }

    /// <summary>
    /// How long the lights stay on after the last motion: the night time while someone sleeps (not on an office morning), otherwise
    /// the day time. Falls back to <see cref="DefaultLightOffDelay"/> when the input_number has no value.
    /// </summary>
    private TimeSpan GetLightOffDelay()
    {
        var minutes = IsLightSleepMode
            ? Entities.InputNumber.Bathroomlightnighttime.State
            : Entities.InputNumber.Bathroomlightdaytime.State;

        return minutes is > 0 ? TimeSpan.FromMinutes(minutes.Value) : DefaultLightOffDelay;
    }

    /// <summary>
    /// The brightness for the time of day: dimmed while someone sleeps, except on the morning of an office day.
    /// </summary>
    private int GetBrightness() => _circadianLightingService.GetBrightness(IsLightSleepMode);

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
    /// It's Vincent's toothbrush, so these automations never run while Vincent isn't home.
    /// </summary>
    private void ToothbrushHandler()
    {
        Entities.Sensor.SmartSeries400097aeToothbrushState
            .StateChanges()
            .Where(x => x.New?.State != "idle" && x.Old?.State == "idle" && Vincent.IsHome)
            .Subscribe(_ =>
            {
                if (!IsDouching && !IsSleepMode)
                {
                    Entities.MediaPlayer.Googlehome0351.VolumeSet(0.25);
                    _spotcast.PlaySpotify(Entities.MediaPlayer.Googlehome0351,
                        Carleen.IsHome ? _config.SpotifyDoucheUrl : _config.SpotifyRadioNlUrl);

                    Entities.MediaPlayer.Googlehome0351.MediaPlay();
                }
            });

        Entities.Sensor.SmartSeries400097aeToothbrushState
            .StateChanges()
            .WhenStateIsFor(x => x?.State == "idle" && Vincent.IsHome,
                TimeSpan.FromSeconds(30), Scheduler)
            .Subscribe(_ =>
            {
                if (!IsDouching && Vincent.IsHome)
                {
                    Entities.MediaPlayer.Googlehome0351.StopIfPlaying();
                    Entities.Light.Slaapkamer.TurnOn();
                }
            });
    }
}
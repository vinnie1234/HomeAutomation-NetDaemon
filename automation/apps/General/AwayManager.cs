using System.Reactive.Concurrency;
using Automation.Enum;
using static Automation.Globals;

namespace Automation.apps.General;

/// <summary>
/// Represents an application that manages the "away" state and related notifications.
/// </summary>
[NetDaemonApp(Id = nameof(AwayManager))]
public class AwayManager : BaseApp
{
    private bool _backHome;

    /// <summary>
    /// Initializes a new instance of the <see cref="AwayManager"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    public AwayManager(
        IHaContext ha,
        ILogger<AwayManager> logger,
        INotify notify,
        IScheduler scheduler)
        : base(ha, logger, notify, scheduler)
    {
        TriggersHandler();
        VincentHomeHandler();
        AutoAway();
    }


    /// <summary>
    /// Handles the event when Vincent comes home.
    /// </summary>
    private void VincentHomeHandler()
    {
        Entities.Person.VincentMaarschalkerweerd
            .StateChanges()
            .Where(x => x.Old?.State != "home" &&
                        x.New?.State == "home" &&
                        !Vincent.IsHome)
            .Subscribe(_ => Entities.InputBoolean.Away.TurnOff());
    }

    /// <summary>
    /// Sets up the triggers for handling away and home states.
    /// </summary>
    private void TriggersHandler()
    {
        Entities.InputBoolean.Away.WhenTurnsOn(_ => AwayHandler());
        Entities.InputBoolean.Away.WhenTurnsOff(_ => { _backHome = true; });
        Entities.BinarySensor.GangMotion.WhenTurnsOn(_ => WelcomeHome());
    }

    /// <summary>
    /// Handles the actions to be taken when the away state is activated.
    /// </summary>
    private void AwayHandler()
    {
        _backHome = false;

        if (IsOfficeDay(Entities, DateTimeOffset.Now.DayOfWeek)
            && DateTimeOffset.Now.Hour < 9
            && Entities.InputBoolean.Holliday.IsOff())
            Notify.NotifyPhoneVincent("Werkse Vincent", "Succes op kantoor :)", false, 5);
        else
            Notify.NotifyPhoneVincent("Tot ziens", "Je laat je huis weer alleen :(", false, 5);

        Entities.Light.TurnAllOff();
        Entities.MediaPlayer.Tv.TurnOff();
        Entities.MediaPlayer.AvSoundbar.TurnOff();
    }

    /// <summary>
    /// Handles the actions to be taken when Vincent comes home.
    /// </summary>
    private void WelcomeHome()
    {
        var houseState = GetHouseState(Entities);

        if (_backHome)
        {
            NotifyVincentPhone(houseState);

            SetLightScene(houseState);

            _backHome = false;

            Scheduler.Schedule(TimeSpan.FromSeconds(15), () =>
            {
                var message = "Welkom thuis Vincent!";
                if (Entities.Sensor.ZedarFoodStorageStatus.State != "full")
                    message += " Het eten van Pixel is bijna op!";

                Notify.NotifyHouse("welcomeHome", message, true);
            });
        }
    }

    /// <summary>
    /// Sets the light scene based on the current house state.
    /// </summary>
    /// <param name="houseState">The current state of the house.</param>
    private void SetLightScene(HouseState houseState)
    {
        switch (houseState)
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
                throw new ArgumentOutOfRangeException(nameof(houseState), $"{houseState} is not a valid house state!");
        }
    }

    /// <summary>
    /// Sends a notification to Vincent when he comes home.
    /// </summary>
    /// <param name="houseState">The current state of the house.</param>
    private void NotifyVincentPhone(HouseState houseState)
    {
        Notify.NotifyPhoneVincent("Welkom thuis Vincent",
            $"De huis status is nu: {houseState}. Je lampen worden voor je ingesteld.",
            true,
            action: [new ActionModel(action: "TURNONTV", title: "TV Aanzetten", func: () => { Entities.MediaPlayer.Tv.TurnOn(); })]);
    }
    
    /// <summary>
    /// Automatically sets the "away" state based on Vincent's phone distance and direction of travel.
    /// </summary>
    private void AutoAway()
    {
        Entities.Sensor.ThuisSmS938bDistance.StateChanges()
            .WhenStateIsFor(x => x?.State > 300, TimeSpan.FromMinutes(5), Scheduler)
            .Subscribe(_ =>
            {
                if (Vincent.DirectionOfTravel is "away_from" or "stationary" &&
                    Entities.InputBoolean.Away.IsOff() && Entities.Zone.Boodschappen.IsOff()) 
                    Entities.InputBoolean.Away.TurnOn();
            });
    }
}
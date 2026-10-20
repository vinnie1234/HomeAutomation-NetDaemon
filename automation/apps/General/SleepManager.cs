using System.Reactive.Concurrency;
using Automation.Configuration;
using Microsoft.Extensions.Options;

namespace Automation.apps.General;

public static class TestExceptionCatcher { public static Exception? CaughtException; }

/// <summary>
/// Represents an application that manages sleep routines and related automations.
/// </summary>
[NetDaemonApp(Id = nameof(SleepManager))]
public class SleepManager : BaseApp
{
    /// <summary>
    /// Gets a value indicating whether light automations are disabled.
    /// </summary>
    private bool DisableLightAutomations => Entities.InputBoolean.Disablelightautomationgeneral.IsOn();
    
    private readonly IDataRepository _storage;
    private const string PetsnowyLastCleanedDateKey = "PetsnowyLastCleanedDate";

    /// <summary>
    /// Initializes a new instance of the <see cref="SleepManager"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    /// <param name="storage">The data repository for storing and retrieving data.</param>
    public SleepManager(
        IHaContext ha,
        ILogger<SleepManager> logger,
        INotify notify,
        IScheduler scheduler,
        IDataRepository storage,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler, config)
    {
        _storage = storage;
        
        AwakeExtraChecks();
        ScheduleCarleenWakeUp();
        
        Entities.InputBoolean.Sleepingvincent.WhenTurnsOff(_ => WakeUp());
        Entities.InputBoolean.Sleepingvincent.WhenTurnsOn(_ => Sleeping());
        Entities.InputBoolean.Sleepingcarleen.WhenTurnsOff(_ => CarleenWokeUp());

        Scheduler.ScheduleCronNl("00 10 * * *", () =>
        {
            if (!Globals.IsDayOff(Entities, Scheduler.NowNl().DayOfWeek) && Entities.InputBoolean.Sleepingvincent.IsOn() && Entities.InputBoolean.Datenight.IsOff())
                Entities.InputBoolean.Sleepingvincent.TurnOff();
        });
    }

    /// <summary>
    /// Executes the wake-up routine for Vincent.
    /// Rollerblind is skipped if Carleen is still home and sleeping.
    /// </summary>
    private void WakeUp()
    {
        try {
            var carleenStillSleeping = Carleen is { IsHome: true, IsSleeping: true };

            if (!carleenStillSleeping)
                OpenRollerblind();

            SendBatteryWarning();
        } catch (Exception ex) {
            Logger.LogError(ex, "Error in the wake-up routine");
            TestExceptionCatcher.CaughtException = ex;
        }
    }

    /// <summary>
    /// Opens the rollerblind at the appropriate position: fully on days off, partially on work days.
    /// </summary>
    private void OpenRollerblind()
    {
        if (Globals.IsDayOff(Entities, Scheduler.NowNl().DayOfWeek))
        {
            Entities.Cover.Rollerblind0003.SetCoverPosition(100);
            Entities.Light.Slaapkamer.TurnOn(brightnessPct: 30);
        }
        else if ((Entities.Cover.Rollerblind0003.Attributes?.CurrentPosition ?? 0) < 100)
            Entities.Cover.Rollerblind0003.SetCoverPosition(45);
    }

    /// <summary>
    /// Called when Carleen wakes up. Opens the rollerblind if Vincent is already awake.
    /// </summary>
    private void CarleenWokeUp()
    {
        if (!Carleen.IsHome) return;
        if (!Vincent.IsSleeping)
            OpenRollerblind();
    }

    /// <summary>
    /// Executes the sleeping routine.
    /// </summary>
    private void Sleeping()
    {
        try {
            Logger.LogDebug("Sleep Routine started");

            if (Carleen.IsHome)
                Entities.InputBoolean.Sleepingcarleen.TurnOn();

            ChangeRelevantHouseState();
            TurnAllLightsOut();
            SendBatteryWarning();
            Entities.MediaPlayer.Tv.TurnOff();
            Entities.Cover.Rollerblind0003.SetCoverPosition(0);
            var now = Scheduler.NowNl();
            var message = Entities.Sensor.AfvalMorgen.State;
            if (now.Hour is >= 05 and < 07)
                message = Entities.Sensor.AfvalVandaag.State;

            // Like Alarm's garbage check: no reminder when there's no garbage or the sensor has no value
            if (message is not (null or "" or "Geen" or "unknown" or "unavailable"))
                Notify.NotifyPeopleHome("Vergeet het afval niet",
                    $"Vergeet je niet op {message} buiten te zetten?", true);

            var cleanedToday = _storage.Get<string>(PetsnowyLastCleanedDateKey) == now.Date.ToString("O");
            var hasErrors = int.TryParse(Entities.Sensor.PetsnowyLitterboxErrors.State, out var errors) && errors > 0;
            if (hasErrors || !cleanedToday)
                Notify.NotifyPeopleHome("PetSnowy heeft errors",
                    "Er staat nog een error open voor de PetSnowy", true);
        } catch (Exception ex) {
            Logger.LogError(ex, "Error in the sleep routine");
            TestExceptionCatcher.CaughtException = ex;
        }
    }

    /// <summary>
    /// Changes relevant house states when sleeping.
    /// </summary>
    private void ChangeRelevantHouseState()
    {
        // Going to sleep means being home: clear Vincent's away so the derived "away" follows.
        // (Carleen's away is left alone: it only says whether she is home.)
        Entities.InputBoolean.Awayvincent.TurnOff();
        Entities.InputBoolean.Douchen.TurnOff();
    }

    /// <summary>
    /// Sends a battery warning if certain devices have low battery.
    /// </summary>
    private void SendBatteryWarning()
    {
        var message = "";

        // An unknown/unavailable battery level (null) is not "almost empty"
        if (Entities.Sensor.VincentPhoneBatteryLevel.State is < 30 &&
            Entities.BinarySensor.VincentPhoneIsCharging.IsOff())
            message = "Telefoon van Vincent is bijna leeg.";

        if (Entities.Sensor.CarleenMobielBatteryLevel.State is < 30 &&
            Entities.Sensor.CarleenMobielBatteryState.State is not ("charging" or "full"))
        {
            if(!string.IsNullOrEmpty(message))
                message += " En ";
            message += "Telefoon van Carleen is bijna leeg.";
        }

        // To the phones, not the speakers: this runs when going to sleep or waking up, and nothing is
        // announced through the speakers while someone is sleeping
        if (!string.IsNullOrEmpty(message))
            Notify.NotifyPeopleHome("Telefoon bijna leeg", message, true);

        if (Entities.Sensor.SmT860BatteryLevel.State is < 30 && Entities.BinarySensor.SmT860IsCharging.IsOff())
            Notify.NotifyPhoneVincent("Tabled bijna leeg", "Je moet je tabled opladen", true);
    }

    /// <summary>
    /// Turns off all lights if light automations are not disabled.
    /// </summary>
    private void TurnAllLightsOut()
    {
        if (!DisableLightAutomations) 
            Entities.Light.TurnAllOff();
    }
    
    /// <summary>
    /// Performs extra checks when the system is awake.
    /// </summary>
    private void AwakeExtraChecks()
    {
        Entities.MediaPlayer.Tv.WhenTurnsOn(_ =>
        {
            if (Entities.InputBoolean.Sleepingvincent.IsOn()) 
                Entities.InputBoolean.Sleepingvincent.TurnOff();
            
            if (Entities.InputBoolean.Sleepingcarleen.IsOn()) 
                Entities.InputBoolean.Sleepingcarleen.TurnOff();
        });

        Entities.Light.Bureau.WhenTurnsOn(_ =>
        {
            if (Entities.InputBoolean.Sleepingvincent.IsOn()) 
                Entities.InputBoolean.Sleepingvincent.TurnOff();            
        });

        // The bedroom ceiling light in the morning means getting up; in the evening it must not end the night
        Entities.Light.Plafondslaapkamer.WhenTurnsOn(_ =>
        {
            switch (Scheduler.NowNl().Hour)
            {
                case 9:
                {
                    if (!Vincent.IsHome)
                    {
                        Entities.InputBoolean.Sleepingcarleen.TurnOff();
                        Entities.InputBoolean.Sleepingvincent.TurnOff();
                    }

                    break;
                }
                case >= 10 and < 14:
                    Entities.InputBoolean.Sleepingcarleen.TurnOff();
                    Entities.InputBoolean.Sleepingvincent.TurnOff();
                    break;
            }
        });
    }
    
    /// <summary>
    /// Schedules Carleen's sleeping boolean to turn off at 09:00. Cancels any previous schedule.
    /// </summary>
    private void ScheduleCarleenWakeUp()
    {
        Scheduler.ScheduleCronNl("00 10 * * *", () =>
        {
            if (Carleen.IsHome && Carleen.IsSleeping)
            {
                Logger.LogInformation("Scheduled 10:00 wake-up: setting Carleen sleeping off");
                Entities.InputBoolean.Sleepingcarleen.TurnOff();
            }
        });
    }
}
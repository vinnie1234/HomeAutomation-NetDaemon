using System.Collections;
using System.Reactive.Concurrency;

namespace Automation.apps.General;

/// <summary>
/// Represents an application that manages sleep routines and related automations.
/// </summary>
[NetDaemonApp(Id = nameof(SleepManager))]
public static class TestExceptionCatcher { public static Exception? CaughtException; }

public class SleepManager : BaseApp
{
    /// <summary>
    /// Gets a value indicating whether light automations are disabled.
    /// </summary>
    private bool DisableLightAutomations => Entities.InputBoolean.Disablelightautomationgeneral.IsOn();

    /// <summary>
    /// Initializes a new instance of the <see cref="SleepManager"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    public SleepManager(
        IHaContext ha,
        ILogger<SleepManager> logger,
        INotify notify,
        IScheduler scheduler)
        : base(ha, logger, notify, scheduler)
    {
        AwakeExtraChecks();

        Entities.InputBoolean.Sleepingvincent.WhenTurnsOff(_ => WakeUp());
        Entities.InputBoolean.Sleepingvincent.WhenTurnsOn(_ => Sleeping());
        Entities.InputBoolean.Sleepingcarleen.WhenTurnsOff(_ => CarleenWokeUp());

        Scheduler.ScheduleCron("00 10 * * *", () =>
        {
            if (!((IList)Globals.WeekendDays).Contains(Scheduler.Now.DayOfWeek) && Entities.InputBoolean.Sleepingvincent.IsOn() && Entities.InputBoolean.Holliday.IsOff() && Entities.InputBoolean.Datenight.IsOff())
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
            Logger.LogDebug("Wake up Routine");
            if (Scheduler.Now.Hour < 7 && Entities.InputBoolean.Onvacation.IsOff())
            {
                Entities.InputBoolean.Sleepingvincent.TurnOn();
                return;
            }

            var carleenStillSleeping = Carleen.IsHome && Carleen.IsSleeping;

            if (!carleenStillSleeping)
                OpenRollerblind();

            SendBatteryWarning();
        } catch (Exception ex) {
            TestExceptionCatcher.CaughtException = ex;
        }
    }

    /// <summary>
    /// Opens the rollerblind at the appropriate position based on the day of week.
    /// </summary>
    private void OpenRollerblind()
    {
        if (((IList)Globals.WeekendDays).Contains(Scheduler.Now.DayOfWeek))
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
            var checkDate = Scheduler.Now;
            var message = Entities.Sensor.AfvalMorgen.State;
            if (checkDate.Hour is >= 00 and < 07) 
                message = Entities.Sensor.AfvalVandaag.State;

            if (message != "Geen")
                Notify.NotifyPhoneVincent("Vergeet het afval niet",
                    $"Vergeet je niet op {message} buiten te zetten?", true);

            if (int.Parse(Entities.Sensor.PetsnowyLitterboxErrors.State ?? "0") > 0)
                Notify.NotifyPhoneVincent("PetSnowy heeft errors",
                    "Er staat nog een error open voor de PetSnowy", true);
        } catch (Exception ex) {
            TestExceptionCatcher.CaughtException = ex;
        }
    }

    /// <summary>
    /// Changes relevant house states when sleeping.
    /// </summary>
    private void ChangeRelevantHouseState()
    {
        // Going to sleep means being home: clear per-person away so the derived "away" follows.
        Entities.InputBoolean.Awayvincent.TurnOff();
        if (Carleen.IsHome)
            Entities.InputBoolean.Awaycarleen.TurnOff();
        Entities.InputBoolean.Douchen.TurnOff();
    }

    /// <summary>
    /// Sends a battery warning if certain devices have low battery.
    /// </summary>
    private void SendBatteryWarning()
    {
        if ((Entities.Sensor.VincentPhoneBatteryLevel.State ?? 0) < 30 && Entities.BinarySensor.VincentPhoneIsCharging.IsOff())
            Notify.NotifyPhoneVincent("Telefoon bijna leeg", "Je moet je telefoon opladen", true);

        if ((Entities.Sensor.SmT860BatteryLevel.State ?? 0) < 30 && Entities.BinarySensor.SmT860IsCharging.IsOff())
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
            
            if (Entities.InputBoolean.Sleepingcarleen.IsOn()) 
                Entities.InputBoolean.Sleepingcarleen.TurnOff();
        });
    }
}
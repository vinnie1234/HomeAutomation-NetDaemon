using System.Collections;
using System.Reactive.Concurrency;

namespace Automation.apps.General;

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

        Entities.InputBoolean.Sleeping.WhenTurnsOff(_ => WakeUp());
        Entities.InputBoolean.Sleeping.WhenTurnsOn(_ => Sleeping());

        Scheduler.ScheduleCron("00 10 * * *", () =>
        {
            if (!((IList)Globals.WeekendDays).Contains(DateTimeOffset.Now.DayOfWeek) && Entities.InputBoolean.Sleeping.IsOn() && Entities.InputBoolean.Holliday.IsOff())
                Entities.InputBoolean.Sleeping.TurnOff();
        });
    }

    /// <summary>
    /// Executes the wake-up routine.
    /// </summary>
    private void WakeUp()
    {
        Logger.LogDebug("Wake up Routine");
        if (DateTime.Now.Hour < 7 && Entities.InputBoolean.Onvacation.IsOff())
        {
            Entities.InputBoolean.Sleeping.TurnOn();
            return;
        }

        if (((IList)Globals.WeekendDays).Contains(DateTimeOffset.Now.DayOfWeek))
        {
            Entities.Cover.Rollerblind0003.SetCoverPosition(100);
            Entities.Light.Slaapkamer.TurnOn(brightnessPct: 30);
        }
        else if ((Entities.Cover.Rollerblind0003.Attributes?.CurrentPosition ?? 0) < 100) 
            Entities.Cover.Rollerblind0003.SetCoverPosition(45);

        SendBatteryWarning();
    }

    /// <summary>
    /// Executes the sleeping routine.
    /// </summary>
    private void Sleeping()
    {
        Logger.LogDebug("Sleep Routine started");

        ChangeRelevantHouseState();
        TurnAllLightsOut();
        SendBatteryWarning();
        Entities.MediaPlayer.Tv.TurnOff();
        Entities.Cover.Rollerblind0003.SetCoverPosition(0);
        var checkDate = DateTimeOffset.Now;
        var message = Entities.Sensor.AfvalMorgen.State;
        if (checkDate.Hour is >= 00 and < 07) 
            message = Entities.Sensor.AfvalVandaag.State;

        if (message != "Geen")
            Notify.NotifyPhoneVincent("Vergeet het afval niet",
                $"Vergeet je niet op {message} buiten te zetten?", true);

        if (int.Parse(Entities.Sensor.PetsnowyLitterboxErrors.State ?? "0") > 0)
            Notify.NotifyPhoneVincent("PetSnowy heeft errors",
                "Er staat nog een error open voor de PetSnowy", true);
    }

    /// <summary>
    /// Changes relevant house states when sleeping.
    /// </summary>
    private void ChangeRelevantHouseState()
    {
        Entities.InputBoolean.Away.TurnOff();
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
            if (Entities.InputBoolean.Sleeping.IsOn()) 
                Entities.InputBoolean.Sleeping.TurnOff();
        });

        Entities.Light.Bureau.WhenTurnsOn(_ =>
        {
            if (Entities.InputBoolean.Sleeping.IsOn()) 
                Entities.InputBoolean.Sleeping.TurnOff();
        });
    }
}
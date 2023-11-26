using System.Collections;
using System.Reactive.Concurrency;

namespace Automation.apps.General;

[NetDaemonApp(Id = nameof(SleepManager))]
public class SleepManager : BaseApp
{
    private bool DisableLightAutomations => Entities.InputBoolean.Disablelightautomationgeneral.IsOn();

    public SleepManager(
        IHaContext ha,
        ILogger<SleepManager> logger,
        INotify notify,
        IScheduler scheduler)
        : base(ha, logger, notify, scheduler)
    {
        EnergyPriceCheck();
        AwakeExtraChecks();

        Entities.InputBoolean.Sleeping.WhenTurnsOff(_ => WakeUp());
        Entities.InputBoolean.Sleeping.WhenTurnsOn(_ => Sleeping());

        Scheduler.ScheduleCron("00 10 * * *", () =>
        {
            if (!((IList)Globals.WeekendDays).Contains(DateTime.Now.DayOfWeek))
                if (Entities.InputBoolean.Sleeping.IsOn())
                    Entities.InputBoolean.Sleeping.TurnOff();
        });
    }

    private void WakeUp()
    {
        Logger.LogDebug("Wake up Routine");
        if (((IList)Globals.WeekendDays).Contains(DateTime.Now.DayOfWeek))
            Entities.Cover.Rollerblind0001.SetCoverPosition(100);
        else if (Entities.Cover.Rollerblind0001.Attributes?.CurrentPosition < 100) Entities.Cover.Rollerblind0001.SetCoverPosition(45);

        SendBatteryWarning();
    }

    private void Sleeping()
    {
        Logger.LogDebug("Sleep Routine started");

        ChangeRelevantHouseState();
        TurnAllLightsOut();
        SendBatteryWarning();
        Entities.MediaPlayer.Tv.TurnOff();
        Entities.Cover.Rollerblind0001.SetCoverPosition(0);
        var checkDate = DateTime.Now;
        var message = Entities.Sensor.AfvalMorgen.State;
        if (checkDate.Hour is >= 00 and < 07) message = Entities.Sensor.AfvalVandaag.State;

        if (message != @"Geen")
            Notify.NotifyPhoneVincent(@"Vergeet het afval niet",
                @$"Vergeet je niet op {message} buiten te zetten?", true);

        if (int.Parse(Entities.Sensor.PetsnowyError.State ?? "0") > 0)
            Notify.NotifyPhoneVincent(@"PetSnowy heeft errors",
                @"Er staat nog een error open voor de PetSnowy", true);
    }

    private void ChangeRelevantHouseState()
    {
        Entities.InputBoolean.Away.TurnOff();
        Entities.InputBoolean.Douchen.TurnOff();
    }

    private void SendBatteryWarning()
    {
        if (Entities.Sensor.PhoneVincentBatteryLevel.State < 30)
            if (Entities.BinarySensor.PhoneVincentIsCharging.IsOff())
                Notify.NotifyPhoneVincent(@"Telefoon bijna leeg", @"Je moet je telefoon opladen", true);

        if (Entities.Sensor.SmT860BatteryLevel.State < 30)
            if (Entities.BinarySensor.SmT860IsCharging.IsOff())
                Notify.NotifyPhoneVincent(@"Tabled bijna leeg", @"Je moet je tabled opladen", true);
    }

    private void TurnAllLightsOut()
    {
        if (!DisableLightAutomations) Entities.Light.TurnAllOff();
    }

    private void EnergyPriceCheck()
    {
        var priceList = Entities.Sensor.EpexSpotNlNetPrice
            .Attributes?.Data;

        if (priceList != null)
            foreach (JsonElement price in priceList)
            {
                var model = price.ToObject<EnergyPriceModel>();

                if (model is { PriceCtPerKwh: < -20 })
                {
                    Notify.NotifyPhoneVincent(@"Morgen is het stroom gratis", @$"Stroom kost morgen om {model.StartTime} {model.PriceCtPerKwh} cent!", false, 10);
                    break;
                }
            }
    }

    private void AwakeExtraChecks()
    {
        Entities.MediaPlayer.Tv.WhenTurnsOn(x =>
        {
            if (Entities.InputBoolean.Sleeping.IsOn()) Entities.InputBoolean.Sleeping.TurnOff();
        });

        Entities.Light.Bureau.WhenTurnsOn(_ =>
        {
            if (Entities.InputBoolean.Sleeping.IsOn()) Entities.InputBoolean.Sleeping.TurnOff();
        });
    }
}
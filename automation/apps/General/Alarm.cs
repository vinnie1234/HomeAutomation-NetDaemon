using System.Reactive.Concurrency;
using System.Threading;
using Automation.Helpers;
using Automation.Models.DiscordNotificationModels;
using NetDaemon.Client;
using NetDaemon.Client.HomeAssistant.Extensions;

namespace Automation.apps.General;

/// <summary>
/// Represents the Alarm application that monitors various sensors and triggers notifications based on specific conditions.
/// </summary>
[NetDaemonApp(Id = nameof(Alarm))]
public class Alarm : BaseApp
{
    private readonly string _discordLogChannel = ConfigManager.GetValueFromConfigNested("Discord", "Logs") ?? "";

    /// <summary>
    /// Initializes a new instance of the <see cref="Alarm"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for timed tasks.</param>
    /// <param name="homeAssistantConnection">The Home Assistant connection.</param>
    public Alarm(
        IHaContext ha,
        ILogger<Alarm> logger,
        INotify notify,
        IScheduler scheduler,
        IHomeAssistantConnection homeAssistantConnection)
        : base(ha, logger, notify, scheduler)
    {
        TemperatureCheck();
        EnergyCheck();
        GarbageCheck();
        PetSnowyCheck();
        HaChecks(homeAssistantConnection);
        EnergyNegativeCheck();
        BackUpCheck();
        TrafficToWorkCheck();

        Entities.BinarySensor.GangMotion.WhenTurnsOn(_ =>
        {
            if (Globals.AmIHomeCheck(Entities) && !Vincent.IsHome)
                Notify.NotifyPhoneVincent("ALARM", "Beweging gedetecteerd", false, 5, channel: "ALARM",
                    vibrationPattern: "100, 1000, 100, 1000, 100");
        });
    }

    /// <summary>
    /// Checks the traffic to work and sends a notification if the travel time exceeds the threshold.
    /// </summary>
    private void TrafficToWorkCheck()
    {
        Scheduler.ScheduleCron("50 7 * * 4,5", () =>
        {
            if (Entities.InputBoolean.Holliday.IsOff())
            {
                if (Entities.Sensor.HereTravelTimeReistijdInHetVerkeer.State > 40)
                {
                    Notify.NotifyPhoneVincent(
                        "HET IS DRUK OP DE WEG!!!",
                        $"Het kost je momenteeel {Entities.Sensor.HereTravelTimeReistijdInHetVerkeer.State} minuten tot kantoor!",
                        true,
                        action:
                        [
                            new ActionModel(action: "URI", title: "Ga naar maps",
                                uri:
                                "https://www.google.nl/maps/dir/Ida+Gerhardtlaan+28,+Veenendaal/Papendorpseweg+99,+3528+BJ+Utrecht,+Nederland/@52.0460841,4.9910623,10z/data=!3m1!4b1!4m14!4m13!1m5!1m1!1s0x47c6519986b166d3:0x69ceb74bf73a6521!2m2!1d5.5278732!2d52.0275379!1m5!1m1!1s0x47c6659909ea7b8d:0x70525f5d1a86e320!2m2!1d5.0879509!2d52.0640583!3e0?entry=ttu&g_ep=EgoyMDI0MTAxNi4wIKXMDSoASAFQAw%3D%3D"
                            )
                        ]
                    );
                }
            }
        });
    }

    /// <summary>
    /// Checks the temperature and sends a notification if it exceeds the threshold.
    /// </summary>
    private void TemperatureCheck()
    {
        foreach (var temperatureSensor in Collections.GetAllTemperatureSensors(Entities))
            temperatureSensor.Key
                .StateChanges()
                .Where(x => x.Entity.State > 25 && !Vincent.IsSleeping)
                .Subscribe(x => Notify.NotifyPhoneVincent("Hoge temperatuur gedetecteerd",
                    $"{temperatureSensor.Value} is {x.Entity.State} graden", true, channel: "ALARM",
                    vibrationPattern: "100, 1000, 100, 1000, 100"));
    }

    /// <summary>
    /// Checks the energy consumption and sends a notification if it exceeds the threshold for a specified duration.
    /// </summary>
    private void EnergyCheck()
    {
        Entities.Sensor.P1Meter3c39e72a64e8ActivePower
            .StateChanges()
            .WhenStateIsFor(x => x?.State > 2000, TimeSpan.FromMinutes(10), Scheduler)
            .Subscribe(x =>
                {
                    Notify.NotifyPhoneVincent("Hoog energie verbruik",
                        $"Energie verbruik is al voor 10 minuten {x.Entity.State}",
                        true,
                        10,
                        [
                            new ActionModel(action: "URI", title: "Ga naar dashboard",
                                uri: ConfigManager.GetValueFromConfig("BaseUrlHomeAssistant") + "/energy")
                        ],
                        channel: "ALARM", vibrationPattern: "100, 1000, 100, 1000, 100");
                }
            );
    }

    /// <summary>
    /// Schedules a daily check for garbage collection and sends a reminder notification.
    /// </summary>
    private void GarbageCheck()
    {
        Scheduler.ScheduleCron("00 22 * * *", () =>
        {
            var message = Entities.Sensor.AfvalMorgen.State;
            if (message != "Geen")
                Notify.NotifyPhoneVincent("Vergeet het afval niet",
                    $"Vergeet je niet op {message} buiten te zetten?", true);
        });
    }

    /// <summary>
    /// Schedules a daily check for PetSnowy errors and sends a notification if any errors are found.
    /// </summary>
    private void PetSnowyCheck()
    {
        Scheduler.ScheduleCron("00 22 * * *", () =>
        {
            if (int.Parse(Entities.Sensor.PetsnowyLitterboxErrors.State ?? "0") > 0)
            {
                var discordNotificationModel = new DiscordNotificationModel
                {
                    Embed = new Embed
                    {
                        Color = 15548997,
                        Fields =
                        [
                            new Field { Name = "Totaal erros", Value = Entities.Sensor.PetsnowyLitterboxErrors.State ?? "0" },
                            new Field
                            {
                                Name = "Laatste error",
                                Value = Entities.Sensor.PetsnowyLitterboxErrors.EntityState?.LastChanged.ToString() ??
                                        string.Empty
                            }
                        ]
                    }
                };

                Notify.NotifyDiscord("PetSnowy heeft errors", [_discordLogChannel], discordNotificationModel);
                Notify.NotifyPhoneVincent("PetSnowy heeft errors",
                    "Er staat nog een error open voor de PetSnowy", false, 10);
            }
        });
    }

    /// <summary>
    /// Checks the connection to Home Assistant and sends a notification if the connection is lost.
    /// </summary>
    /// <param name="homeAssistantConnection">The Home Assistant connection.</param>
    private void HaChecks(IHomeAssistantConnection homeAssistantConnection)
    {
        Scheduler.RunEvery(TimeSpan.FromSeconds(30), DateTimeOffset.Now, async () =>
        {
            var entities = await homeAssistantConnection.GetEntitiesAsync(new CancellationToken());

            if (!(entities?.Count > 0))
            {
                Notify.NotifyDiscord("NetDeamon heeft geen verbinding meer met HA", [_discordLogChannel]);
                Notify.NotifyPhoneVincent("NetDeamon heeft geen verbinding meer met HA",
                    "De ping naar HA is helaas niet gelukt!", false, 10);
            }
        });
    }

    /// <summary>
    /// Checks the energy price and sends a notification if it becomes negative.
    /// </summary>
    private void EnergyNegativeCheck()
    {
        Entities.Sensor.Energykwhnetpriceincents
            .StateChanges()
            .Subscribe(x =>
            {
                if (x.New?.State < -20.00)
                {
                    Notify.NotifyDiscord($"ENERGY IS NEGATIEF - {x.New.State}", [_discordLogChannel]);
                    Notify.NotifyPhoneVincent($"ENERGY IS NEGATIEF - {x.New.State}",
                        "Je energy is negatief, dit kost geld.", false, 10);
                }
            });
    }

    /// <summary>
    /// Schedules a daily check for backups and sends a notification if no recent backups are found.
    /// </summary>
    private void BackUpCheck()
    {
        Scheduler.ScheduleCron("00 22 * * *", () =>
        {
            var lastLocalBackString = Entities.Sensor.Onedrivebackup
                .Attributes?.LastLocalbackupdate;

            var lastOneDriveBackString = Entities.Sensor.Onedrivebackup
                .Attributes?.LastOneDrivebackupdate;

            if (!string.IsNullOrEmpty(lastLocalBackString))
            {
                var dateTime = DateTime.Parse(lastLocalBackString);
                if (dateTime < DateTime.Now.AddDays(-2))
                    Notify.NotifyDiscord(
                        $"Er is al 2 dagen geen locale backup, laatste backup is van {lastLocalBackString}",
                        [_discordLogChannel]);
            }
            else
            {
                Notify.NotifyDiscord("Er is geen laatste locale backup", [_discordLogChannel]);
            }

            if (!string.IsNullOrEmpty(lastOneDriveBackString))
            {
                var dateTime = DateTime.Parse(lastOneDriveBackString);
                if (dateTime < DateTime.Now.AddDays(-2))
                    Notify.NotifyDiscord(
                        $"Er is al 2 dagen geen OneDrive backup, laatste backup is van {lastLocalBackString}",
                        [_discordLogChannel]);
            }
            else
            {
                Notify.NotifyDiscord("Er is geen laatste OneDrive backup", [_discordLogChannel]);
            }
        });
    }
}
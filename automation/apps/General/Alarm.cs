using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Models.DiscordNotificationModels;
using Microsoft.Extensions.Options;
using Automation.Configuration;
using Automation.Helpers;

namespace Automation.apps.General;

/// <summary>
/// Represents the Alarm application that monitors various sensors and triggers notifications based on specific conditions.
/// </summary>
[NetDaemonApp(Id = nameof(Alarm))]
public class Alarm : BaseApp
{
    private readonly AppConfig _config;

    private readonly IEntityManager _entityManager;
    private readonly IDataRepository _storage;

    private const string PetSnowyEvent = "event.snow_self_cleaning_litter_box_device_notifications";

    /// <summary>Words that make a litter box notification worth waking the phones for (English, Dutch and what a full bin or jam might say).</summary>
    private static readonly string[] ProblemWords = ["error", "fault", "alarm", "fout", "storing", "stuck", "jam", "blocked", "full", "vol", "overload"];

    /// <summary>On while more than <see cref="HighUsageWatts"/> is used for 10 minutes, for the alert on the dashboard.</summary>
    public const string HighUsageSensor = "sensor.hoog_stroomverbruik";
    private const double HighUsageWatts = 2000;

    /// <summary>
    /// Initializes a new instance of the <see cref="Alarm"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for timed tasks.</param>
    /// <param name="entityManager">The entity manager for creating and managing entities.</param>
    /// <param name="config">The application configuration.</param>
    /// <param name="storage">The data repository for storing and retrieving data.</param>
    public Alarm(
        IHaContext ha,
        ILogger<Alarm> logger,
        INotify notify,
        IScheduler scheduler,
        IEntityManager entityManager,
        IOptions<AppConfig> config,
        IDataRepository storage)
        : base(ha, logger, notify, scheduler, config)
    {
        _config = config.Value;
        _entityManager = entityManager;
        _storage = storage;
        InitializeGarbageCounterEntities();
        
        TemperatureCheck();
        EnergyCheck();
        GarbageCheck();
        PetSnowyCheck();
        PetSnowyDeviceNotifications();
        EnergyNegativeCheck();
        EnergyHighPriceCheck();
        BackUpCheck();
        
        Entities.BinarySensor.GangMotion.WhenTurnsOn(_ =>
        {
            // Only alarm when nobody is home (neither Vincent nor Carleen) and the house sitter isn't there either.
            if (Entities.InputBoolean.Away.IsOn() && !HouseSitter.IsHome)
                Notify.NotifyPhoneVincent("ALARM", "Beweging gedetecteerd", false, 5, channel: "ALARM",
                    vibrationPattern: "100, 1000, 100, 1000, 100");
        });
    }

    
    /// <summary>
    /// Checks the temperature and sends a notification if it exceeds the threshold.
    /// </summary>
    private void TemperatureCheck()
    {
        foreach (var temperatureSensor in GetAllTemperatureSensors())
            temperatureSensor.Key
                .StateChanges()
                // Only when crossing 25 degrees, not again for every tenth of a degree above it
                .Where(x => x.Old?.State is <= 25 && x.New?.State is > 25 && !IsSleepMode)
                .Subscribe(x => Notify.NotifyPhoneVincentCarleen("Hoge temperatuur gedetecteerd",
                    $"{temperatureSensor.Value} is {x.Entity.State} graden", true, channel: "ALARM",
                    vibrationPattern: "100, 1000, 100, 1000, 100"));
    }

    /// <summary>
    /// Gets a dictionary of all temperature sensors with their corresponding descriptions.
    /// </summary>
    private Dictionary<NumericSensorEntity, string> GetAllTemperatureSensors()
    {
        return new Dictionary<NumericSensorEntity, string>
        {
            { Entities.Sensor.BadkamerTemperature, "Badkamer" },
            { Entities.Sensor.BergingTemperature, "Berging" },
            { Entities.Sensor.GangTemperature, "Gang" }
        };
    }

    /// <summary>
    /// Checks the energy consumption and sends a notification if it exceeds the threshold for a specified duration.
    /// </summary>
    private void EnergyCheck()
    {
        _ = CreateHighUsageSensorAsync();

        Entities.Sensor.P1Meter3c39e72a64e8ActivePower
            .StateChanges()
            .WhenStateIsFor(x => x?.State > HighUsageWatts, TimeSpan.FromMinutes(10), Scheduler)
            .Subscribe(x =>
                {
                    SetHighUsage(true, Scheduler.NowNl().AddMinutes(-10));
                    Notify.NotifyPhoneVincent("Hoog energie verbruik",
                        $"Energie verbruik is al voor 10 minuten {x.Entity.State}",
                        true,
                        10,
                        [
                            new ActionModel(action: "URI", title: "Ga naar dashboard",
                                uri: _config.BaseUrlHomeAssistant + "/energy")
                        ],
                        channel: "ALARM", vibrationPattern: "100, 1000, 100, 1000, 100");
                }
            );

        // Off again once the usage is below the limit for 2 minutes (a short dip is not the end of it)
        Entities.Sensor.P1Meter3c39e72a64e8ActivePower
            .StateChanges()
            .WhenStateIsFor(x => x?.State <= HighUsageWatts, TimeSpan.FromMinutes(2), Scheduler)
            .Subscribe(_ => SetHighUsage(false, null));
    }

    private async Task CreateHighUsageSensorAsync()
    {
        try
        {
            if (!_entityManager.EntityExists(HighUsageSensor))
                await _entityManager.Create(HighUsageSensor, new EntityCreationOptions { Name = "Hoog stroomverbruik", Icon = "mdi:flash-alert" });
            SetHighUsage(false, null);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not create {Sensor}", HighUsageSensor);
        }
    }

    /// <summary>Publishes whether the usage is high, and since when.</summary>
    private void SetHighUsage(bool high, DateTimeOffset? since) =>
        _entityManager.SetState(HighUsageSensor, high ? "on" : "off", new
        {
            sinds = since?.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            grens = HighUsageWatts
        });

    /// <summary>
    /// Schedules a daily check for garbage collection and sends a reminder notification with action button.
    /// </summary>
    private void GarbageCheck()
    {
        Scheduler.ScheduleCronNl("00 22 * * *", () =>
        {
            var message = Entities.Sensor.AfvalMorgen.State;
            if (message != "Geen" && !string.IsNullOrEmpty(message))
            {
                var garbageType = message.ToLower().Replace(" ", "_");
                Notify.NotifyPeopleHome("Vergeet het afval niet",
                    $"Vergeet je niet op {message} buiten te zetten?", true,
                    action: 
                    [
                        new ActionModel(
                            action: $"garbage_placed_{garbageType}",
                            title: "Buiten gezet",
                            func: () => HandleGarbagePlaced(garbageType, message)
                        )
                    ]);
            }
        });
    }

    /// <summary>
    /// Handles the action when garbage is placed outside.
    /// </summary>
    /// <param name="garbageType">The type of garbage (normalized).</param>
    /// <param name="originalMessage">The original message from the sensor.</param>
    private void HandleGarbagePlaced(string garbageType, string originalMessage)
    {
        var entityId = $"sensor.garbage_placed_{garbageType}";
        
        // Get current count and increment
        var currentCount = 0;
        if (_entityManager.EntityExists(entityId))
        {
            var currentState = HaContext.GetState(entityId);
            if (currentState?.State != null && int.TryParse(currentState.State, out var count)) currentCount = count;
        }
        
        currentCount++;
        
        // Update the entity with new count
        _entityManager.SetState(entityId, currentCount, new
        {
            last_placed = Scheduler.NowNl().ToString("yyyy-MM-dd HH:mm:ss"),
            garbage_type = originalMessage,
            friendly_name = $"{originalMessage} keer buiten gezet"
        });
        
        // Send confirmation notification
        Notify.NotifyPhoneVincent("Afval bevestiging",
            $"{originalMessage} is gemarkeerd als buiten gezet. Totaal: {currentCount} keer", true);
        
        Logger.LogInformation("Garbage placed: {GarbageType} - Total count: {Count}", originalMessage, currentCount);
    }

    /// <summary>
    /// Initializes the garbage counter entities for different garbage types.
    /// </summary>
    private void InitializeGarbageCounterEntities()
    {
        var garbageTypes = new[]
        {
            ("gft", "GFT"),
            ("restafval", "Restafval"),
            ("pmd", "PMD"),
            ("papier", "Papier"),
            ("karton", "Karton"),
            ("glas", "Glas"),
            ("textiel", "Textiel")
        };

        // Use fire-and-forget async for entity initialization
        _ = Task.Run(async () =>
        {
            foreach (var (key, displayName) in garbageTypes)
            {
                var entityId = $"sensor.garbage_placed_{key}";
                
                if (!_entityManager.EntityExists(entityId))
                    try
                    {
                        await _entityManager.Create(entityId, new EntityCreationOptions
                        {
                            Name = $"{displayName} keer buiten gezet",
                            Icon = "mdi:trash-can",
                            UnitOfMeasurement = "keer"
                        });

                        // Initialize with 0
                        _entityManager.SetState(entityId, 0, new
                        {
                            last_placed = "Nog nooit",
                            garbage_type = displayName,
                            friendly_name = $"{displayName} keer buiten gezet"
                        });
                        
                        Logger.LogInformation("Initialized garbage counter for {GarbageType}", displayName);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to initialize garbage counter for {GarbageType}", displayName);
                    }
            }
        });
    }

    /// <summary>
    /// The SNOW⁺ litter box reports problems through this event entity. It has not fired yet, so the format is not known:
    /// every notification goes to the Discord logs channel with all its attributes, to learn what they look like, and one that
    /// mentions a problem word also goes to the phones. Refine <see cref="ProblemWords"/> when real notifications are known.
    /// </summary>
    private void PetSnowyDeviceNotifications() =>
        new Entity(HaContext, PetSnowyEvent).StateChanges()
            .Where(x => x.New?.State is not (null or "unknown" or "unavailable") && x.Old?.State != x.New?.State)
            .Subscribe(x => ReportPetSnowyNotification(x.New!));

    private void ReportPetSnowyNotification(EntityState state)
    {
        try
        {
            var details = new List<string>();
            if (state.AttributesJson is { ValueKind: JsonValueKind.Object } attributes)
                details.AddRange(attributes.EnumerateObject()
                    .Where(a => a.Name is not ("friendly_name" or "event_types"))
                    .Select(a => $"{a.Name}: {a.Value}"));
            var text = details.Count > 0 ? string.Join("\n", details) : "(geen details)";
            Logger.LogInformation("PetSnowy notification: {Details}", text.Replace('\n', ' '));

            Notify.NotifyDiscord("PetSnowy meldt iets", [_config.Discord.Logs], new DiscordNotificationModel
            {
                Embed = new Embed
                {
                    Color = 15548997,
                    Fields = [new Field { Name = "Melding", Value = text.Length > 1000 ? text[..1000] : text }]
                }
            });

            if (ProblemWords.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase)))
                Notify.NotifyPeopleHome("PetSnowy meldt een probleem", text, false, 60);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not handle the PetSnowy notification");
        }
    }

    /// <summary>
    /// Schedules a daily check for PetSnowy errors and sends a notification if any errors are found.
    /// </summary>
    private void PetSnowyCheck()
    {
        Entities.Sensor.SnowSelfCleaningLitterBoxStatus.StateChanges().Subscribe(_ =>
        {
            PetSnowyCleaning.MarkCleaned(_storage, Scheduler.NowNl());
        });

        Scheduler.ScheduleCronNl("00 22 * * *", () =>
        {
            var cleanedToday = PetSnowyCleaning.IsCleanedToday(_storage, Scheduler.NowNl());
            var hasErrors = int.TryParse(Entities.Sensor.PetsnowyLitterboxErrors.State, out var litterboxErrors) && litterboxErrors > 0;

            if (hasErrors || !cleanedToday)
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

                Notify.NotifyDiscord("PetSnowy heeft errors of is vandaag nog niet geschoond", [_config.Discord.Logs], discordNotificationModel);
                // Say what is wrong: "not cleaned" is not an error
                if (hasErrors)
                    Notify.NotifyPeopleHome("PetSnowy heeft errors", "Er staat nog een error open voor de PetSnowy", false, 10);
                else
                    Notify.NotifyPeopleHome("PetSnowy is vandaag nog niet geschoond", "De PetSnowy heeft vandaag nog niet schoongemaakt", false, 10);

            }
        });
    }

    /// <summary>
    /// Checks the energy price and sends a notification if it becomes negative.
    /// </summary>
    private void EnergyNegativeCheck()
    {
        Entities.Sensor.AnwbElectricityAllInPriceCurrent
            .StateChanges()
            .Subscribe(x =>
            {
                if (x.New?.State < 0)
                {
                    Notify.NotifyDiscord($"ENERGY IS NEGATIEF - {x.New.State}", [_config.Discord.Logs]);
                    Notify.NotifyPeopleHome($"ENERGY IS NEGATIEF - {x.New.State}",
                        "Je energy is negatief, dit kan geld kosten.", false, 10);
                }
            });
    }
    
    /// <summary>
    /// Checks the energy price and sends a notification if it becomes high.
    /// </summary>
    private void EnergyHighPriceCheck()
    {
        Entities.Sensor.AnwbElectricityAllInPriceCurrent
            .StateChanges()
            .Subscribe(x =>
            {
                if (x.New?.State > 0.55)
                {
                    Notify.NotifyDiscord($"ENERGY IS ENORM DUUR - {x.New.State}", [_config.Discord.Logs]);
                    Notify.NotifyPeopleHome($"ENERGY IS ENORM DUUR - {x.New.State}",
                        "Je energy is hoog, dit kan geld kosten.", false, 10);
                }
            });
    }
    

    /// <summary>
    /// Schedules a daily check for backups and sends a notification if no recent backups are found.
    /// </summary>
    private void BackUpCheck()
    {
        Scheduler.ScheduleCronNl("00 22 * * *", () =>
        {
            var lastLocalBackString = Entities.Sensor.BackupLastAttemptedAutomaticBackup.State;
            

            if (string.IsNullOrEmpty(lastLocalBackString))
            {
                Notify.NotifyDiscord("Er is geen  backup", [_config.Discord.Logs]);
            }
            else if (!DateTimeOffset.TryParse(lastLocalBackString, CultureInfo.InvariantCulture, out var lastBackup))
            {
                // e.g. "unknown"/"unavailable": a broken backup sensor must not go unnoticed
                Notify.NotifyDiscord($"De backup status is onbekend ({lastLocalBackString})", [_config.Discord.Logs]);
            }
            else if (lastBackup < DateTimeOffset.Now.AddDays(-2))
            {
                Notify.NotifyDiscord(
                    $"Er is al 2 dagen geen backup, laatste backup is van {lastLocalBackString}",
                    [_config.Discord.Logs]);
            }
        });
    }
}
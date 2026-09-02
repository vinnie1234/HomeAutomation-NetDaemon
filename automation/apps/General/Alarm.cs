using System.Reactive.Concurrency;
using Automation.Models.DiscordNotificationModels;
using Microsoft.Extensions.Options;
using Automation.Configuration;

namespace Automation.apps.General;

/// <summary>
/// Represents the Alarm application that monitors various sensors and triggers notifications based on specific conditions.
/// </summary>
[NetDaemonApp(Id = nameof(Alarm))]
public class Alarm : BaseApp
{
    private readonly AppConfig _config;

    private readonly IEntityManager _entityManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="Alarm"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for timed tasks.</param>
    /// <param name="entityManager">The entity manager for creating and managing entities.</param>
    /// <param name="config">The application configuration.</param>
    public Alarm(
        IHaContext ha,
        ILogger<Alarm> logger,
        INotify notify,
        IScheduler scheduler,
        IEntityManager entityManager,
        IOptions<AppConfig> config)
        : base(ha, logger, notify, scheduler)
    {
        _config = config.Value;
        _entityManager = entityManager;
        InitializeGarbageCounterEntities();
        
        TemperatureCheck();
        EnergyCheck();
        GarbageCheck();
        PetSnowyCheck();
        EnergyNegativeCheck();
        BackUpCheck();
        
        Entities.BinarySensor.GangMotion.WhenTurnsOn(_ =>
        {
            // Only alarm when nobody is home (neither Vincent nor Carleen).
            if (Entities.InputBoolean.Away.IsOn())
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
                .Where(x => x.Entity.State > 25 && !IsNightMode)
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
                                uri: _config.BaseUrlHomeAssistant + "/energy")
                        ],
                        channel: "ALARM", vibrationPattern: "100, 1000, 100, 1000, 100");
                }
            );
    }

    /// <summary>
    /// Schedules a daily check for garbage collection and sends a reminder notification with action button.
    /// </summary>
    private void GarbageCheck()
    {
        Scheduler.ScheduleCron("00 22 * * *", () =>
        {
            var message = Entities.Sensor.AfvalMorgen.State;
            if (message != "Geen" && !string.IsNullOrEmpty(message))
            {
                var garbageType = message.ToLower().Replace(" ", "_");

                if (Carleen.IsHome)
                    Notify.NotifyPhoneVincentCarleen("Vergeet het afval niet",
                        $"Vergeet je niet op {message} buiten te zetten?", true,
                        action: 
                        [
                            new ActionModel(
                                action: $"garbage_placed_{garbageType}",
                                title: "Buiten gezet",
                                func: () => HandleGarbagePlaced(garbageType, message)
                            )
                        ]);
                else
                    Notify.NotifyPhoneVincent("Vergeet het afval niet",
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
            last_placed = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
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
    /// Schedules a daily check for PetSnowy errors and sends a notification if any errors are found.
    /// </summary>
    private void PetSnowyCheck()
    {
        Scheduler.ScheduleCron("00 22 * * *", () =>
        {
            if (int.TryParse(Entities.Sensor.PetsnowyLitterboxErrors.State, out var litterboxErrors) && litterboxErrors > 0)
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

                Notify.NotifyDiscord("PetSnowy heeft errors", [_config.Discord.Logs], discordNotificationModel);
                if (Carleen.IsHome)
                    Notify.NotifyPhoneVincentCarleen("PetSnowy heeft errors",
                        "Er staat nog een error open voor de PetSnowy", false, 10);
                else
                    Notify.NotifyPhoneVincent("PetSnowy heeft errors",
                        "Er staat nog een error open voor de PetSnowy", false, 10);
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
                    
                    if(Carleen.IsHome)
                        Notify.NotifyPhoneVincentCarleen($"ENERGY IS NEGATIEF - {x.New.State}",
                            "Je energy is negatief, dit kan geld kosten.", false, 10);
                    else
                        Notify.NotifyPhoneVincent($"ENERGY IS NEGATIEF - {x.New.State}",
                            "Je energy is negatief, dit kan geld kosten.", false, 10);
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
            var lastLocalBackString = Entities.Sensor.BackupLastAttemptedAutomaticBackup.State;
            

            if (!string.IsNullOrEmpty(lastLocalBackString))
            {
                if (!DateTime.TryParse(lastLocalBackString, out var dateTime))
                    return;
                if (dateTime < DateTime.Now.AddDays(-2))
                    Notify.NotifyDiscord(
                        $"Er is al 2 dagen geen backup, laatste backup is van {lastLocalBackString}",
                        [_config.Discord.Logs]);
            }
            else
            {
                Notify.NotifyDiscord("Er is geen  backup", [_config.Discord.Logs]);
            }
        });
    }
}
using System.Globalization;
using System.Reactive.Concurrency;
using Automation.Helpers;
using Automation.Models.DiscordNotificationModels;

namespace Automation.apps.General;

/// <summary>
/// Represents an application that manages various cat-related automations and notifications.
/// </summary>
[NetDaemonApp(Id = nameof(Cat))]
public class Cat : BaseApp
{
    private readonly string _discordPixelChannel = ConfigManager.GetValueFromConfigNested("Discord", "Pixel") ?? "";

    /// <summary>
    /// Initializes a new instance of the <see cref="Cat"/> class.
    /// </summary>
    /// <param name="haContext">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    public Cat(
        IHaContext haContext,
        ILogger<Cat> logger,
        INotify notify,
        IScheduler scheduler)
        : base(haContext, logger, notify, scheduler)
    {
        ButtonFeedCat();
        PetSnowyStatusMonitoring();
        AutoFeedCat();
        MonitorCat();
        SendAlarmWhenStuffIsOff();

        Entities.InputButton.Pixelgivenextfeedeary.StateChanges()
            .Subscribe(_ =>
            {
                try
                {
                    GiveNextFeedEarly();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error during early feed");
                }
            });

        Entities.InputButton.Cleanpetsnowy.StateChanges()
            .Subscribe( _ =>
            {
                try
                {
                    CleanPetSnowy();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error during PetSnowy cleaning");
                }
            });

        Entities.InputButton.Emptypetsnowy.StateChanges()
            .Subscribe(_ =>
            {
                try
                {
                    EmptyPetSnowy();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error during PetSnowy emptying");
                }
            });
    }
    

    /// <summary>
    /// Monitors the status of the Pet Snowy litter box and updates counters based on its state.
    /// </summary>
    private void PetSnowyStatusMonitoring()
    {
        Entities.Sensor.PetsnowyLitterboxStatus
            .StateChanges()
            .Subscribe(x =>
            {
                switch (x.New?.State)
                {
                    case "pet_into":
                        Entities.Counter.Petsnowylitterboxpixelinit.Increment();
                        break;
                    case "cleaning":
                        Entities.Counter.Petsnowylittleboxcleaning.Increment();
                        break;
                    case "emptying":
                        Entities.Counter.Petsnowylitterboxemptying.Increment();
                        break;
                }
            });
    }

    /// <summary>
    /// Sets up a subscription to feed the cat when the feed button is pressed.
    /// </summary>
    private void ButtonFeedCat()
    {
        Entities.InputButton.Feedcat.StateChanges()
            .Subscribe(_ =>
            {
                try
                {
                    FeedCat(Convert.ToInt32(Entities.InputNumber.Pixelnumberofmanualfood.State ?? 0));
                    
                    Entities.InputNumber.Pixellastamountmanualfeed.SetValue(Convert.ToInt32(
                        (Entities.InputNumber.Pixelnumberofmanualfood.State ?? 0) +
                        Convert.ToInt32(Entities.InputNumber.Pixellastamountmanualfeed.State ?? 0)));
                    Entities.InputDatetime.Pixellastmanualfeed.SetDatetime(new InputDatetimeSetDatetimeParameters
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error during manual cat feeding");
                }

            });
    }

    /// <summary>
    /// Feeds the cat with the specified amount of food.
    /// </summary>
    /// <param name="amount">The amount of food to give to the cat.</param>
    private void FeedCat(int amount)
    {
        // Update counters immediately (local state)
        var amountToday = Convert.ToInt32((Entities.InputNumber.Pixeltotalamountfeedday.State ?? 0) + amount);
        Entities.InputNumber.Pixeltotalamountfeedday.SetValue(amountToday);
        Entities.InputNumber.Pixeltotalamountfeedalltime.SetValue(
            Convert.ToInt32((Entities.InputNumber.Pixeltotalamountfeedalltime.State ?? 0) + amount));

        try
        {
            FeedCatViaTuya(amount);
        }catch(Exception ex)
        {
            NotifyManualFeedingRequired(amount);
        }
    }

    /// <summary>
    /// Feeds the cat via LocalTuya integration.
    /// </summary>
    /// <param name="amount">The amount of food to dispense.</param>
    /// <returns>A task representing the feeding operation.</returns>
    private void FeedCatViaTuya(int amount)
    {
        Services.Localtuya.SetDp(new LocaltuyaSetDpParameters
        {
            DeviceId = ConfigManager.GetValueFromConfig("ZedarDeviceId"),
            Dp = 3,
            Value = amount
        });
        
        Logger.LogInformation("Successfully fed cat {Amount}g via Zedar feeder", amount);
    }

    /// <summary>
    /// Fallback notification when automatic feeding fails.
    /// </summary>
    /// <param name="amount">The amount that failed to be dispensed.</param>
    /// <returns>A task representing the notification operation.</returns>
    private void NotifyManualFeedingRequired(int amount)
    {
        var message = $"Automatische voeding van {amount}g is mislukt. Voer Pixel handmatig of check de Zedar feeder.";
        
        // Try to notify via phone first, then Discord as secondary
        try
        {
            Notify.NotifyPhoneVincent(
                "Kat voeding gefaald", 
                message,
                true);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to send phone notification, trying Discord");
            
            Notify.NotifyDiscord(
                $"🐱❌ **Feeding Failed**\n{message}", 
                [_discordPixelChannel]);
        }
        
        Logger.LogWarning("Cat feeding failed - manual intervention required for {Amount}g", amount);
    }

    /// <summary>
    /// Monitors the cat's feeding times and sends notifications.
    /// </summary>
    private void MonitorCat()
    {
        Entities.InputDatetime.Pixellastmanualfeed.StateChanges()
            .Subscribe(_ =>
                {
                    var discordNotificationModel = new DiscordNotificationModel
                    {
                        Embed = new Embed
                        {
                            Title = "Pixel heeft handmatig eten gehad",
                            Url = ConfigManager.GetValueFromConfig("BaseUrlHomeAssistant") + "/lovelace/2",
                            Thumbnail = new Location("https://cdn.pixabay.com/photo/2016/10/11/18/17/black-cat-1732366_960_720.png"),
                            Fields =
                            [
                                new Field { Name = "Eten gegeven", Value = Entities.InputNumber.Pixellastamountmanualfeed.State.ToString() ?? "0" },
                                new Field { Name = "Totaal gehad vandaag", Value = Entities.InputNumber.Pixeltotalamountfeedday.State.ToString() ?? "0" }
                            ]
                        }
                    };

                    Notify.NotifyDiscord("", [_discordPixelChannel], discordNotificationModel);
                }
                );

        Entities.InputDatetime.Pixellastautomatedfeed.StateChanges()
            .Subscribe(_ =>
            {
                Logger.LogDebug("NOTIFICATIE: Pixel heeft automatisch eten gehad");
                var discordNotificationModel = new DiscordNotificationModel
                {
                    Embed = new Embed
                    {
                        Title = "Pixel heeft eten gehad",
                        Url = ConfigManager.GetValueFromConfig("BaseUrlHomeAssistant") + "/status-huis/1",
                        Thumbnail = new Location("https://cdn.pixabay.com/photo/2016/10/11/18/17/black-cat-1732366_960_720.png"),
                        Fields =
                        [
                            new Field { Name = "Eten gegeven", Value = Entities.InputNumber.Pixellastamountautomationfeed.State.ToString() ?? "0" },
                            new Field { Name = "Totaal gehad vandaag", Value = Entities.InputNumber.Pixeltotalamountfeedday.State.ToString() ?? "0" }
                        ]
                    }
                };

                Notify.NotifyDiscord("", [_discordPixelChannel], discordNotificationModel);
            });


        Scheduler.ScheduleCron("59 23 * * *", () => Entities.InputNumber.Pixeltotalamountfeedday.SetValue(0));
    }

    /// <summary>
    /// Sets up automatic feeding times for the cat.
    /// </summary>
    private void AutoFeedCat()
    {
        foreach (var autoFeed in
                 Collections.GetFeedTimes(Entities).Where(autoFeed => autoFeed.Key.State != null))
            Scheduler.RunDaily(TimeSpan.Parse(autoFeed.Key.State ?? "00:00:00", new CultureInfo("nl-Nl")), () =>
            {
                if (Entities.InputBoolean.Pixelskipnextautofeed.IsOff())
                {
                    FeedCat(Convert.ToInt32(autoFeed.Value.State));

                    Entities.InputNumber.Pixellastamountautomationfeed.SetValue(Convert.ToInt32(autoFeed.Value.State));
                    Entities.InputDatetime.Pixellastautomatedfeed.SetDatetime(new InputDatetimeSetDatetimeParameters
                    {
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    });
                }

                Entities.InputBoolean.Pixelskipnextautofeed.TurnOff();
            });
    }

    /// <summary>
    /// Feeds the cat the next scheduled feed early.
    /// </summary>
    private void GiveNextFeedEarly()
    {
        var closestFeed = GetClosestFeed();

        Entities.InputBoolean.Pixelskipnextautofeed.TurnOn();
        FeedCat(Convert.ToInt32(closestFeed.Value.State));

        Entities.InputNumber.Pixellastamountmanualfeed.SetValue(Convert.ToInt32(closestFeed.Value.State));

        Entities.InputDatetime.Pixellastmanualfeed.SetDatetime(new InputDatetimeSetDatetimeParameters
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
    }

    /// <summary>
    /// Sends an alarm notification when certain devices are turned off.
    /// </summary>
    private void SendAlarmWhenStuffIsOff()
    {
        Entities.Switch.PetsnowyFountainIson.WhenTurnsOff(_ =>
        {
            var discordNotificationModel = new DiscordNotificationModel
            {
                Embed = new Embed
                {
                    Title = "PIXEL ZIJN FOUNTAIN STAAT UIT!!!!"
                }
            };
            Notify.NotifyDiscord("", [_discordPixelChannel], discordNotificationModel);
        }, 600);

        Entities.Switch.PetsnowyLitterboxAutoClean.WhenTurnsOff(_ =>
        {
            var discordNotificationModel = new DiscordNotificationModel
            {
                Embed = new Embed
                {
                    Title = "DE KATTENBAK STAAT UIT!!!!"
                }
            };
            Notify.NotifyDiscord("", [_discordPixelChannel], discordNotificationModel);
        }, 600);
    }

    /// <summary>
    /// Gets the closest scheduled feed time.
    /// </summary>
    /// <returns>The closest feed time as a key-value pair.</returns>
    private KeyValuePair<InputDatetimeEntity, InputNumberEntity> GetClosestFeed()
    {
        var closestFeed =
            Collections
                .GetFeedTimes(Entities)
                .MinBy(pair =>
                    Math.Abs(
                        (DateTimeOffset.Parse(pair.Key.State ?? "00:00:00", new CultureInfo("nl-Nl")) - DateTimeOffset.Now)
                        .Ticks));
        return closestFeed;
    }


    /// <summary>
    /// Sends a command to clean the Pet Snowy litter box.
    /// </summary>
    private void CleanPetSnowy()
    {
        Services.Localtuya.SetDp(new LocaltuyaSetDpParameters
        {
            DeviceId = ConfigManager.GetValueFromConfig("PetSnowyDeviceId"),
            Dp = 9,
            Value = "true"
        });
    }

    /// <summary>
    /// Sends a command to empty the Pet Snowy litter box.
    /// </summary>
    private void EmptyPetSnowy()
    {
        Services.Localtuya.SetDp(new LocaltuyaSetDpParameters
        {
            DeviceId = ConfigManager.GetValueFromConfig("PetSnowyDeviceId"),
            Dp = 109,
            Value = "true"
        });
    }
}
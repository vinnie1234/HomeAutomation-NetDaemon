using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Models.DiscordNotificationModels;
using Microsoft.Extensions.Options;

namespace Automation.apps.General.Pets;

/// <summary>
/// Represents an application that manages various cat-related automations and notifications.
/// </summary>
[NetDaemonApp(Id = nameof(Cat))]
public partial class Cat : BaseApp
{
    private readonly AppConfig _config;
    private readonly IDataRepository _storage;
    private readonly IEntityManager _entityManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="Cat"/> class.
    /// </summary>
    /// <param name="haContext">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    /// <param name="config">The application settings.</param>
    public Cat(
        IHaContext haContext,
        ILogger<Cat> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config,
        IDataRepository storage,
        IEntityManager entityManager)
        : base(haContext, logger, notify, scheduler, config)
    {
        _config = config.Value;
        _storage = storage;
        _entityManager = entityManager;
        SetUpFeeding();
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
        Entities.Sensor.SnowSelfCleaningLitterBoxStatus
            .StateChanges()
            .Subscribe(x =>
            {
                switch (x.New?.State)
                {
                    // A visit: Pixel gets his zoomies for 10 minutes after (the dashboard header reads the time)
                    case "pet_into":
                        Entities.Counter.Petsnowylitterboxpixelinit.Increment();
                        HaContext.CallService("input_datetime", "set_datetime", ServiceTarget.FromEntity("input_datetime.pixellastlitterboxvisit"),
                            new { timestamp = Scheduler.Now.ToUnixTimeSeconds() });
                        break;
                    case "cleaning":
                        Entities.Counter.Petsnowylittleboxcleaning.Increment();
                        break;
                }
            });

        // Xtend Tuya reports emptying as a notification instead of a status
        Entities.BinarySensor.SnowSelfCleaningLitterBoxNotificationEmptyComplete
            .WhenTurnsOn(_ => Entities.Counter.Petsnowylitterboxemptying.Increment());
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
                    var amount = Convert.ToInt32(Entities.InputNumber.Pixelnumberofmanualfood.State ?? 0);
                    FeedByHand(amount, NowLabel(), "handmatig");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error during manual cat feeding");
                }

            });
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
                            Url = _config.BaseUrlHomeAssistant + "/lovelace/2",
                            Thumbnail = new Location("https://cdn.pixabay.com/photo/2016/10/11/18/17/black-cat-1732366_960_720.png"),
                            Fields =
                            [
                                new Field { Name = "Eten gegeven", Value = Entities.InputNumber.Pixellastamountmanualfeed.State.ToString() ?? "0" },
                                new Field { Name = "Totaal gehad vandaag", Value = Entities.InputNumber.Pixeltotalamountfeedday.State.ToString() ?? "0" }
                            ]
                        }
                    };

                    Notify.NotifyDiscord("", [_config.Discord.Pixel], discordNotificationModel);
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
                        Url = _config.BaseUrlHomeAssistant + "/status-huis/1",
                        Thumbnail = new Location("https://cdn.pixabay.com/photo/2016/10/11/18/17/black-cat-1732366_960_720.png"),
                        Fields =
                        [
                            new Field { Name = "Eten gegeven", Value = Entities.InputNumber.Pixellastamountautomationfeed.State.ToString() ?? "0" },
                            new Field { Name = "Totaal gehad vandaag", Value = Entities.InputNumber.Pixeltotalamountfeedday.State.ToString() ?? "0" }
                        ]
                    }
                };

                Notify.NotifyDiscord("", [_config.Discord.Pixel], discordNotificationModel);
            });


        Scheduler.ScheduleCronNl("59 23 * * *", () => Entities.InputNumber.Pixeltotalamountfeedday.SetValue(0));
    }

    /// <summary>
    /// Sets up automatic feeding times for the cat.
    /// </summary>
    private void AutoFeedCat()
    {
        // The feed times are rescheduled when changed in Home Assistant; a feed without a valid time is skipped
        foreach (var autoFeed in GetFeedTimes())
            Scheduler.RunDailyAt(autoFeed.Key, fallback: null, () =>
            {
                if (Entities.InputBoolean.Pixelskipnextautofeed.IsOff())
                    FeedScheduled(Convert.ToInt32(autoFeed.Value.State), TimeLabel(autoFeed.Key.State));

                Entities.InputBoolean.Pixelskipnextautofeed.TurnOff();
            });
    }

    /// <summary>
    /// Feeds the cat the next scheduled feed early.
    /// </summary>
    private void GiveNextFeedEarly()
    {
        var closestFeed = GetClosestFeed();
        if (closestFeed.Key is null)
        {
            Logger.LogWarning("Cannot give the next feed early: none of the feed times is set");
            return;
        }

        // The next scheduled feeding is skipped because this one comes early; when the early one fails the scheduled one has to stay
        Entities.InputBoolean.Pixelskipnextautofeed.TurnOn();
        FeedByHand(Convert.ToInt32(closestFeed.Value.State), TimeLabel(closestFeed.Key.State), "eerder geven",
            whenFailed: () => Entities.InputBoolean.Pixelskipnextautofeed.TurnOff());
    }

    /// <summary>
    /// Sends an alarm notification when certain devices are turned off.
    /// </summary>
    private void SendAlarmWhenStuffIsOff()
    {
        Entities.Switch.SnowAutomaticPetWaterFountainSchakelaar.WhenTurnsOff(_ =>
        {
            var discordNotificationModel = new DiscordNotificationModel
            {
                Embed = new Embed
                {
                    Title = "PIXEL ZIJN FOUNTAIN STAAT UIT!!!!"
                }
            };
            Notify.NotifyDiscord("", [_config.Discord.Pixel], discordNotificationModel);
        }, 600, Scheduler);

        Entities.Switch.SnowSelfCleaningLitterBoxAutoClean.WhenTurnsOff(_ =>
        {
            var discordNotificationModel = new DiscordNotificationModel
            {
                Embed = new Embed
                {
                    Title = "DE KATTENBAK STAAT UIT!!!!"
                }
            };
            Notify.NotifyDiscord("", [_config.Discord.Pixel], discordNotificationModel);
        }, 600, Scheduler);
    }

    /// <summary>
    /// Gets the closest scheduled feed time.
    /// </summary>
    /// <returns>The closest feed time as a key-value pair.</returns>
    private KeyValuePair<InputDatetimeEntity, InputNumberEntity> GetClosestFeed()
    {
        // The next upcoming feed (also past midnight), which is also the one "skip next auto feed" skips.
        // Feed times are Dutch clock times, so compare them with the Dutch time of day.
        var timeOfDay = Scheduler.NowNl().TimeOfDay;
        var closestFeed =
            GetFeedTimes()
                .Select(pair => (Feed: pair, Time: Extensions.SchedulerExtensions.ParseTimeOfDay(pair.Key.State)))
                .Where(feed => feed.Time != null)
                .MinBy(feed => TimeUntil(feed.Time!.Value, timeOfDay))
                .Feed;
        return closestFeed;
    }

    /// <summary>
    /// How long it takes until the given time of day, counting from <paramref name="now"/> and wrapping past midnight.
    /// </summary>
    private static TimeSpan TimeUntil(TimeSpan timeOfDay, TimeSpan now)
    {
        var until = timeOfDay - now;
        return until < TimeSpan.Zero ? until + TimeSpan.FromDays(1) : until;
    }
    /// <summary>
    /// Gets a dictionary of feed times with their corresponding feed amounts.
    /// </summary>
    private Dictionary<InputDatetimeEntity, InputNumberEntity> GetFeedTimes()
    {
        return new Dictionary<InputDatetimeEntity, InputNumberEntity>
        {
            { Entities.InputDatetime.Pixelfeedfirsttime, Entities.InputNumber.Pixelfeedfirstamount },
            { Entities.InputDatetime.Pixelfeedsecondtime, Entities.InputNumber.Pixelfeedsecondamount },
            { Entities.InputDatetime.Pixelfeedthirdtime, Entities.InputNumber.Pixelfeedthirdamount },
            { Entities.InputDatetime.Pixelfeedfourthtime, Entities.InputNumber.Pixelfeedfourthamount }
        };
    }


    /// <summary>
    /// Sends a command to clean the Pet Snowy litter box.
    /// </summary>
    private void CleanPetSnowy()
    {
        Entities.Button.SnowSelfCleaningLitterBoxManualClean.Press();
    }

    /// <summary>
    /// Sends a command to empty the Pet Snowy litter box.
    /// </summary>
    private void EmptyPetSnowy()
    {
        Entities.Button.SnowSelfCleaningLitterBoxEmptyLitterBox.Press();
    }
}

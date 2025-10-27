using System.Reactive.Concurrency;
using Automation.Helpers;
using Automation.Models.DiscordNotificationModels;

namespace Automation.apps.General;

/// <summary>
/// Represents an application that handles automatic updates and notifications.
/// </summary>
[NetDaemonApp(Id = nameof(AutoUpdateApp))]
public class AutoUpdateApp : BaseApp
{
    private readonly UpdateEntities _updates;
    private readonly string _discordUpdateChannel = ConfigManager.GetValueFromConfigNested("Discord", "Updates") ?? "";

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoUpdateApp"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="notify">The notification service.</param>
    /// <param name="scheduler">The scheduler for cron jobs.</param>
    public AutoUpdateApp(
        IHaContext ha,
        ILogger<AutoUpdateApp> logger,
        INotify notify,
        IScheduler scheduler)
        : base(ha, logger, notify, scheduler)
    {
        _updates = Entities.Update;
        scheduler.ScheduleCron("0 11 30 * *", () => _ = AutoUpdate());
    }

    /// <summary>
    /// Automatically updates the entities that need updates and sends notifications.
    /// </summary>
    private async Task AutoUpdate()
    {
        try
        {
            var needUpdate = _updates.EnumerateAll().Where(u => u.IsOn()).ToArray();
            if (needUpdate.Length == 0) return;

            var names = string.Join(",", needUpdate.Select(u => u.Attributes?.FriendlyName ?? u.EntityId));
            NotifyMeOnDiscord("Updates beschikbaar voor", names);

            foreach (var updateEntity in needUpdate)
            {
                var name = updateEntity.Attributes?.FriendlyName ?? updateEntity.EntityId;
                Logger.LogInformation("Start updating {name}", name);
                NotifyMeOnDiscord("Updates word geinstaleerd", $"Installeer update voor {name}");

                updateEntity.Install();

                Logger.LogInformation("Ready updating {name}", name);
                NotifyMeOnDiscord("Updates is geinstaleerd", $"Geinstalleerde update voor {name}");
                await Task.Delay(TimeSpan.FromMinutes(1));
            }

            NotifyVincentPhone(needUpdate.Length);

        }
        catch (Exception)
        {
            //ignore
        }
    }

    /// <summary>
    /// Sends a notification to Discord.
    /// </summary>
    /// <param name="title">The title of the notification.</param>
    /// <param name="message">The message of the notification.</param>
    private void NotifyMeOnDiscord(string title, string message)
    {
        var discordNotificationModel = new DiscordNotificationModel
        {
            Embed = new Embed
            {
                Title = title,
                Url = ConfigManager.GetValueFromConfig("BaseUrlHomeAssistant") + "/config/updates",
                Thumbnail = new Location("https://icon-library.com/images/update-icon-png/update-icon-png-22.jpg"),
                Description = message
            }
        };

        Notify.NotifyDiscord("", [_discordUpdateChannel], discordNotificationModel);
    }
    
    /// <summary>
    /// Sends a notification to Vincent's phone about the updates performed.
    /// </summary>
    /// <param name="totalUpdates">The total number of updates performed.</param>
    private void NotifyVincentPhone(int totalUpdates)
    {
        Notify.NotifyPhoneVincent("Updates uitgevoerd",
            $"Er zijn {totalUpdates} updates uitgevoerd op de Home Assistant", 
            true,
            action:
            [
                new ActionModel(action: "REBOOTHOUSE", title: "Huis opnieuw opstarten",
                    func: () => { Services.Homeassistant.Restart(); })
            ]);
    }
}
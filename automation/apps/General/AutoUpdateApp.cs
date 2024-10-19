﻿using System.Reactive.Concurrency;
using System.Threading.Tasks;
using Automation.Helpers;
using Automation.Models.DiscordNotificationModels;

namespace Automation.apps.General;

[NetDaemonApp(Id = nameof(AutoUpdateApp))]
public class AutoUpdateApp : BaseApp
{
    private readonly UpdateEntities _updates;
    private readonly string _discordUpdateChannel = ConfigManager.GetValueFromConfigNested("Discord", "Updates") ?? "";
    
    public AutoUpdateApp(
        IHaContext ha,
        ILogger<Alarm> logger,
        INotify notify,
        IScheduler scheduler)
        : base(ha, logger, notify, scheduler)
    {
       
        _updates = Entities.Update;
        AutoUpdate();
        scheduler.ScheduleCron("0 3 * * *", AutoUpdate);
    }
    
    private async void AutoUpdate()
    {
        var needUpdate = _updates.EnumerateAll().Where(u => u.IsOn()).ToArray();
        if (needUpdate.Length == 0) return;

        var names = string.Join(",", needUpdate.Select(u => u.Attributes?.FriendlyName ?? u.EntityId));
        NotifyMeOnDiscord("Updates beschikbaar voor", names);
        
        foreach (var updateEntity in needUpdate)
        {
            Logger.LogInformation($"Start updating {updateEntity.Attributes?.FriendlyName ?? updateEntity.EntityId}");
            NotifyMeOnDiscord("Updates word geinstaleerd", $"Installeer update voor {updateEntity.Attributes?.FriendlyName ?? updateEntity.EntityId}");
            
            updateEntity.Install();
            
            await updateEntity.StateChanges().Where(s => s.New.IsOff() && s.New?.Attributes?.InProgress == false).Take(1);
            Logger.LogInformation($"Ready updating {updateEntity.Attributes?.FriendlyName ?? updateEntity.EntityId}");
            NotifyMeOnDiscord("Updates is geinstaleeerd",
                $"Geinstalleerde update voor {updateEntity.Attributes?.FriendlyName ?? updateEntity.EntityId}");
            await Task.Delay(TimeSpan.FromMinutes(1));
        }
    }

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

        Notify.NotifyDiscord("", new[] { _discordUpdateChannel }, discordNotificationModel);
    }
}
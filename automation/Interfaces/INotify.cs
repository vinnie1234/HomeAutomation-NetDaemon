using Automation.Models.DiscordNotificationModels;

namespace Automation.Interfaces;

public interface INotify
{
    Task NotifyHouse(string title, string message, bool canAlwaysSendNotification, double? sendAfterMinutes = null);

    Task NotifyPhoneVincent(
        string title,
        string message,
        bool canAlwaysSendNotification,
        double? sendAfterMinutes = null,
        List<ActionModel>? action = null,
        string? image = null,
        string? channel = null,
        string? vibrationPattern = null);

    // ReSharper disable once UnusedMember.Global
    Task NotifyPhoneVincentTts(string title, string message, bool canAlwaysSendNotification, double? sendAfterMinutes = null);

    Task ResetNotificationHistoryForNotificationTitle(string title);

    void NotifyDiscord(string message, string[] target, DiscordNotificationModel? data = null);

    void SendMusicToHome(string mediaContentId, double volume = 0.5);
}
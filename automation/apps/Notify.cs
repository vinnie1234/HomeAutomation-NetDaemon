using System.Net.Http;
using System.Net.Sockets;
using Automation.Enum;
using Automation.Models.DiscordNotificationModels;
using Polly;
using Polly.CircuitBreaker;
using static System.Enum;

namespace Automation.apps;

/// <summary>
/// Provides notification services for the home automation system.
/// </summary>
public class Notify : INotify
{
    private readonly Entities _entities;
    private readonly IServices _services;
    private readonly IHaContext _ha;
    private readonly IDataRepository _storage;
    private readonly ILogger<Notify> _logger;
    private readonly ResiliencePipeline _discordResiliencePipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="Notify"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="storage">The data repository for storing notification history.</param>
    /// <param name="logger">The logger instance.</param>
    public Notify(IHaContext ha, IDataRepository storage, ILogger<Notify> logger)
    {
        _ha = ha;
        _storage = storage;
        _logger = logger;
        _entities = new Entities(ha);
        _services = new Services(ha);
        _discordResiliencePipeline = CreateDiscordResiliencePipeline();
    }

    /// <summary>
    /// Sends a notification to the house.
    /// </summary>
    /// <param name="title">The title of the notification.</param>
    /// <param name="message">The message of the notification.</param>
    /// <param name="canAlwaysSendNotification">Indicates whether the notification can always be sent.</param>
    /// <param name="sendAfterMinutes">The delay in minutes after which the notification can be sent again.</param>
    public void NotifyHouse(string title, string message, bool canAlwaysSendNotification,
        double? sendAfterMinutes = null)
    {
        var canSendNotification = CanSendNotification(_storage, canAlwaysSendNotification, title, sendAfterMinutes);
        if (!canSendNotification) return;

        SaveNotification(_storage, title, message);

        _entities.MediaPlayer.HeleHuis.VolumeSet(0.4);

        _services.Tts.CloudSay(_entities.MediaPlayer.HeleHuis.EntityId, message);
    }

    /// <summary>
    /// Sends a notification to Vincent's phone.
    /// </summary>
    /// <param name="title">The title of the notification.</param>
    /// <param name="message">The message of the notification.</param>
    /// <param name="canAlwaysSendNotification">Indicates whether the notification can always be sent.</param>
    /// <param name="sendAfterMinutes">The delay in minutes after which the notification can be sent again.</param>
    /// <param name="action">The list of actions associated with the notification.</param>
    /// <param name="image">The image URL for the notification.</param>
    /// <param name="channel">The notification channel.</param>
    /// <param name="vibrationPattern">The vibration pattern for the notification.</param>
    public void NotifyPhoneVincent(
        string title,
        string message,
        bool canAlwaysSendNotification,
        double? sendAfterMinutes = null,
        List<ActionModel>? action = null,
        string? image = null,
        string? channel = null,
        string? vibrationPattern = null)
    {
        var canSendNotification = CanSendNotification(_storage, canAlwaysSendNotification, title, sendAfterMinutes);
        if (!canSendNotification) return;

        SaveNotification(_storage, title, message);

        var data = ConstructData(action, image: image, channel: channel, vibrationPattern: vibrationPattern);
        _services.Notify.MobileAppVincentPhone(new NotifyMobileAppVincentPhoneParameters
            { Title = title, Message = message, Data = data });
    }

    /// <summary>
    /// Sends a TTS notification to Vincent's phone.
    /// </summary>
    /// <param name="title">The title of the notification.</param>
    /// <param name="message">The message of the notification.</param>
    /// <param name="canAlwaysSendNotification">Indicates whether the notification can always be sent.</param>
    /// <param name="sendAfterMinutes">The delay in minutes after which the notification can be sent again.</param>
    public void NotifyPhoneVincentTts(string title, string message, bool canAlwaysSendNotification,
        double? sendAfterMinutes = null)
    {
        var canSendNotification = CanSendNotification(_storage, canAlwaysSendNotification, title, sendAfterMinutes);
        if (!canSendNotification) return;

        SaveNotification(_storage, title, message);

        var data = ConstructData(null, true, phoneMessage: message);
        _services.Notify.MobileAppVincentPhone(new NotifyMobileAppVincentPhoneParameters
            { Message = "TTS", Data = data });
    }

    /// <summary>
    /// Resets the notification history for a specific notification title.
    /// </summary>
    /// <param name="title">The title of the notification to reset.</param>
    public void ResetNotificationHistoryForNotificationTitle(string title)
    {
        var oldData = _storage.Get<List<NotificationModel>>("notificationHistory") ?? [];
        var data = oldData.Find(x => x.Name == title);
        if (data != null) oldData.Remove(data);

        _storage.Save("notificationHistory", oldData);
    }

    /// <summary>
    /// Sends a notification to Discord with resilience patterns.
    /// </summary>
    /// <param name="message">The message of the notification.</param>
    /// <param name="target">The target channels for the notification.</param>
    /// <param name="data">Additional data for the notification.</param>
    public void NotifyDiscord(string message, string[] target, DiscordNotificationModel? data = null)
    {
        _ = Task.Run(async () => await NotifyDiscordAsync(message, target, data));
    }

    /// <summary>
    /// Sends a notification to Discord with resilience patterns (async implementation).
    /// </summary>
    /// <param name="message">The message of the notification.</param>
    /// <param name="target">The target channels for the notification.</param>
    /// <param name="data">Additional data for the notification.</param>
    private async Task NotifyDiscordAsync(string message, string[] target, DiscordNotificationModel? data = null)
    {
        try
        {
            await _discordResiliencePipeline.ExecuteAsync(async _ =>
            {
                await Task.Run(() => _services.Notify.DiscordHomeassistant(message, "", target, data));
            });
            
            _logger.LogDebug("Discord notification sent successfully to channels: {Channels}", string.Join(", ", target));
        }
        catch (BrokenCircuitException)
        {
            _logger.LogWarning("Discord notification blocked by circuit breaker - service may be unavailable");
            // Could implement alternative notification here (e.g., log to file, send to phone)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Discord notification after all retry attempts");
            // Could implement fallback notification mechanism here
        }
    }

    /// <summary>
    /// Sends music to the home media player.
    /// </summary>
    /// <param name="mediaContentId">The media content ID.</param>
    /// <param name="volume">The volume level.</param>
    public void SendMusicToHome(string mediaContentId, double volume = 0.5)
    {
        _entities.MediaPlayer.HeleHuis.PlayMedia(new MediaPlayerPlayMediaParameters
        {
            Media = mediaContentId,
            Announce = false
        });

        _entities.MediaPlayer.HeleHuis.VolumeSet(volume);
    }

    /// <summary>
    /// Subscribes to a notification action.
    /// </summary>
    /// <param name="func">The action to perform.</param>
    /// <param name="key">The key for the action.</param>
    private void SubscribeToNotificationAction(Action func, string key)
    {
        _ha.Events.Where(x => x.EventType == "mobile_app_notification_action")
            .Subscribe(x =>
            {
                var eventActionModel = x.DataElement?.ToObject<EventActionModel>();
                if (eventActionModel?.Action == key) func.Invoke();
            });
    }

    /// <summary>
    /// Constructs the data for a notification.
    /// </summary>
    /// <param name="actions">The list of actions associated with the notification.</param>
    /// <param name="tts">Indicates whether the notification is a TTS notification.</param>
    /// <param name="priority">The priority of the notification.</param>
    /// <param name="phoneMessage">The phone message for the notification.</param>
    /// <param name="image">The image URL for the notification.</param>
    /// <param name="channel">The notification channel.</param>
    /// <param name="vibrationPattern">The vibration pattern for the notification.</param>
    /// <returns>The constructed notification data.</returns>
    private RecordNotifyData ConstructData(List<ActionModel>? actions = null,
        bool tts = false,
        NotifyPriority priority = NotifyPriority.High,
        string? phoneMessage = null,
        string? image = null,
        string? channel = null,
        string? vibrationPattern = null)
    {
        //construct the data here
        RecordNotifyData data = new(
            priority: GetName(priority)?.ToLower(),
            ttl: 0,
            tag: null,
            color: "",
            sticky: "true");

        if (tts)
        {
            data.Channel = "process";
            data.TtsText = phoneMessage;
        }

        data.Channel = channel;
        data.VibrationPattern = vibrationPattern; //"100, 1000, 100, 1000, 100"
        data.LedColor = vibrationPattern;         //"red"
        data.Image = image;

        if (actions != null)
        {
            if (actions.Count > 3) throw new ArgumentException("To many actions");

            foreach (var action in actions.Where(action => action.Func != null))
            {
                action.Action = $"{action.Action}-{Guid.NewGuid().ToString()}";
                if (action.Func != null) SubscribeToNotificationAction(action.Func, action.Action);
                action.Func = null;
            }

            data.Actions = actions;
        }

        return data;
    }

    /// <summary>
    /// Creates a resilience pipeline specifically for Discord notifications.
    /// </summary>
    /// <returns>The configured resilience pipeline for Discord operations.</returns>
    private ResiliencePipeline CreateDiscordResiliencePipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .Handle<TimeoutException>()
                    .Handle<SocketException>(),
                MaxRetryAttempts = 2, // Fewer retries for notifications
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning("Discord notification retry {AttemptNumber} after {Delay}ms due to: {Exception}",
                        args.AttemptNumber, args.RetryDelay.TotalMilliseconds, args.Outcome.Exception?.Message ?? "Unknown error");
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .Handle<TimeoutException>(),
                FailureRatio = 0.6, // Lower threshold for notifications
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 3,
                BreakDuration = TimeSpan.FromMinutes(1), // Shorter break for notifications
                OnOpened = args =>
                {
                    _logger.LogWarning("Discord notification circuit breaker opened for {Duration} due to: {Exception}",
                        args.BreakDuration, args.Outcome.Exception?.Message ?? "Unknown error");
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("Discord notification circuit breaker closed");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    _logger.LogInformation("Discord notification circuit breaker half-open - testing service");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Determines whether a notification can be sent.
    /// </summary>
    /// <param name="storage">The data repository for storing notification history.</param>
    /// <param name="canAlwaysSend">Indicates whether the notification can always be sent.</param>
    /// <param name="title">The title of the notification.</param>
    /// <param name="sendAfterMinutes">The delay in minutes after which the notification can be sent again.</param>
    /// <returns>True if the notification can be sent; otherwise, false.</returns>
    private static bool CanSendNotification(IDataRepository storage, bool canAlwaysSend, string title,
        double? sendAfterMinutes)
    {
        if (canAlwaysSend) return true;

        var notification = GetLastNotification(storage, title);

        sendAfterMinutes ??= 60;
        return DateTimeOffset.Now.AddMinutes((double)sendAfterMinutes) >= (notification?.LastSendNotification ?? DateTime.Now.AddDays(-1000));
    }

    /// <summary>
    /// Saves a notification to the notification history.
    /// </summary>
    /// <param name="storage">The data repository for storing notification history.</param>
    /// <param name="title">The title of the notification.</param>
    /// <param name="message">The message of the notification.</param>
    private static void SaveNotification(IDataRepository storage, string title, string message)
    {
        var oldData = storage.Get<List<NotificationModel>>("notificationHistory") ?? new List<NotificationModel>();
        var data = oldData.Find(x => x.Name == title);
        if (data != null)
            data.Value = message;
        else
            oldData.Add(new NotificationModel(name: title, value: message, lastSendNotification: DateTimeOffset.Now));

        storage.Save("notificationHistory", oldData);
    }

    /// <summary>
    /// Gets the last notification for a specific title.
    /// </summary>
    /// <param name="storage">The data repository for storing notification history.</param>
    /// <param name="title">The title of the notification.</param>
    /// <returns>The last notification model for the specified title.</returns>
    private static NotificationModel? GetLastNotification(IDataRepository storage, string title)
    {
        var oldData = storage.Get<List<NotificationModel>>("notificationHistory") ?? new List<NotificationModel>();
        return oldData.Find(x => x.Name == title);
    }
}
using System.Net.Http;
using System.Net.Sockets;
using System.Reactive.Disposables;
using Automation.Configuration;
using Automation.Enum;
using Automation.Models.DiscordNotificationModels;
using Automation.Models.Persons;
using Microsoft.Extensions.Options;
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
    private readonly CarleenModel _carleen;
    private readonly VincentModel _vincentModel;

    /// <summary>Guards the notification history, which is shared by the Notify instances of all apps.</summary>
    private static readonly Lock HistoryLock = new();

    /// <summary>How long the buttons of an actionable notification keep working when nobody presses them.</summary>
    private static readonly TimeSpan NotificationActionLifetime = TimeSpan.FromDays(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="Notify"/> class.
    /// </summary>
    /// <param name="ha">The Home Assistant context.</param>
    /// <param name="storage">The data repository for storing notification history.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="config">The app configuration.</param>
    public Notify(IHaContext ha, IDataRepository storage, ILogger<Notify> logger, IOptions<AppConfig> config)
    {
        _ha = ha;
        _storage = storage;
        _logger = logger;
        _entities = new Entities(ha);
        _services = new Services(ha);
        _discordResiliencePipeline = CreateDiscordResiliencePipeline();
        _vincentModel = new VincentModel(_entities, config);
        _carleen = new CarleenModel(_entities, config);
    }

    /// <summary>
    /// Announces a notification through the house speakers. Nothing is announced while Vincent or Carleen
    /// is sleeping or when nobody is home.
    /// </summary>
    /// <param name="title">The title of the notification.</param>
    /// <param name="message">The message of the notification.</param>
    /// <param name="canAlwaysSendNotification">Indicates whether the notification can always be sent.</param>
    /// <param name="sendAfterMinutes">The delay in minutes after which the notification can be sent again.</param>
    public void NotifyHouse(string title, string message, bool canAlwaysSendNotification,
        double? sendAfterMinutes = null)
    {
        if (SpeakersNotAllowed(nameof(NotifyHouse))) return;

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
    /// Sends a notification to All people that are home.
    /// </summary>
    /// <param name="title">The title of the notification.</param>
    /// <param name="message">The message of the notification.</param>
    /// <param name="canAlwaysSendNotification">Indicates whether the notification can always be sent.</param>
    /// <param name="sendAfterMinutes">The delay in minutes after which the notification can be sent again.</param>
    /// <param name="action">The list of actions associated with the notification.</param>
    /// <param name="image">The image URL for the notification.</param>
    /// <param name="channel">The notification channel.</param>
    /// <param name="vibrationPattern">The vibration pattern for the notification.</param>
    public void NotifyPeopleHome(string title, string message, bool canAlwaysSendNotification, double? sendAfterMinutes = null,
        List<ActionModel>? action = null, string? image = null, string? channel = null, string? vibrationPattern = null)
    {
        switch (_carleen.IsHome)
        {
            case true when _vincentModel.IsHome:
                NotifyPhoneVincentCarleen(
                    title,
                    message,
                    canAlwaysSendNotification,
                    sendAfterMinutes,
                    action,
                    image,
                    channel,
                    vibrationPattern);
                break;
            case true:
                NotifyPhoneCarleen(
                    title,
                    message,
                    canAlwaysSendNotification,
                    sendAfterMinutes,
                    action,
                    image,
                    channel,
                    vibrationPattern);
                break;
            default:
                NotifyPhoneVincent(
                    title,
                    message,
                    canAlwaysSendNotification,
                    sendAfterMinutes,
                    action,
                    image,
                    channel,
                    vibrationPattern);
                break;
        }
    }
    
    
    
    /// <summary>
    /// Sends a notification to Carleen's phone.
    /// </summary>
    /// <param name="title">The title of the notification.</param>
    /// <param name="message">The message of the notification.</param>
    /// <param name="canAlwaysSendNotification">Indicates whether the notification can always be sent.</param>
    /// <param name="sendAfterMinutes">The delay in minutes after which the notification can be sent again.</param>
    /// <param name="action">The list of actions associated with the notification.</param>
    /// <param name="image">The image URL for the notification.</param>
    /// <param name="channel">The notification channel.</param>
    /// <param name="vibrationPattern">The vibration pattern for the notification.</param>
    public void NotifyPhoneCarleen(
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
        _services.Notify.MobileAppCarleenMobiel(new NotifyMobileAppCarleenMobielParameters
            { Title = title, Message = message, Data = data });
    }
    
    /// <summary>
    /// Sends a notification to Vincent's phone and Carleen's Phone.
    /// </summary>
    /// <param name="title">The title of the notification.</param>
    /// <param name="message">The message of the notification.</param>
    /// <param name="canAlwaysSendNotification">Indicates whether the notification can always be sent.</param>
    /// <param name="sendAfterMinutes">The delay in minutes after which the notification can be sent again.</param>
    /// <param name="action">The list of actions associated with the notification.</param>
    /// <param name="image">The image URL for the notification.</param>
    /// <param name="channel">The notification channel.</param>
    /// <param name="vibrationPattern">The vibration pattern for the notification.</param>
    public void NotifyPhoneVincentCarleen(
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
        
        _services.Notify.MobileAppCarleenMobiel(new NotifyMobileAppCarleenMobielParameters
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
        lock (HistoryLock)
        {
            var oldData = _storage.Get<List<NotificationModel>>("notificationHistory") ?? [];
            var data = oldData.Find(x => x.Name == title);
            if (data != null) oldData.Remove(data);

            _storage.Save("notificationHistory", oldData);
        }
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
            await _discordResiliencePipeline.ExecuteAsync(async cancellationToken =>
            {
                await Task.Run(() => _services.Notify.DiscordHomeassistant(message, "", target, data), cancellationToken);
            });
            
            _logger.LogDebug("Discord notification sent successfully to channels: {Channels}", string.Join(", ", target));
        }
        catch (ObjectDisposedException)
        {
            // NetDaemon is stopping and has already disposed the Home Assistant context; nothing can be sent anymore
            _logger.LogDebug("Discord notification skipped: NetDaemon is shutting down");
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
        if (SpeakersNotAllowed(nameof(SendMusicToHome))) return;

        _entities.MediaPlayer.HeleHuis.PlayMedia(new MediaPlayerPlayMediaParameters
        {
            Media = mediaContentId,
            Announce = false
        });

        _entities.MediaPlayer.HeleHuis.VolumeSet(volume);
    }

    /// <summary>
    /// Whether the house speakers may not be used. They are never used while Vincent or Carleen is
    /// sleeping, nor while both are away (so e.g. a house sitter isn't bothered by announcements or music).
    /// </summary>
    /// <param name="operation">The operation that is skipped, used for logging.</param>
    private bool SpeakersNotAllowed(string operation)
    {
        string? reason = null;
        if (_entities.InputBoolean.Sleepingvincent.IsOn() || _entities.InputBoolean.Sleepingcarleen.IsOn())
            reason = "someone is sleeping";
        else if (_entities.InputBoolean.Away.IsOn())
            reason = "nobody is home";

        if (reason is null) return false;

        _logger.LogDebug("Skipped {Operation}: {Reason}", operation, reason);
        return true;
    }

    /// <summary>
    /// Subscribes to a notification action (button press).
    /// </summary>
    /// <param name="key">The key for the action.</param>
    /// <param name="onPressed">What to do when the button is pressed.</param>
    /// <returns>The subscription, which stops listening when disposed.</returns>
    private IDisposable SubscribeToNotificationAction(string key, Action onPressed)
    {
        return _ha.Events.Where(x => x.EventType == "mobile_app_notification_action")
            .Subscribe(x =>
            {
                var eventActionModel = x.DataElement?.ToObject<EventActionModel>();
                if (eventActionModel?.Action == key) onPressed();
            });
    }

    /// <summary>
    /// Removes the notification with the given tag from Vincent's and Carleen's phone.
    /// </summary>
    /// <param name="tag">The tag of the notification to clear.</param>
    private void ClearNotification(string tag)
    {
        var data = new { tag };
        _services.Notify.MobileAppVincentPhone(new NotifyMobileAppVincentPhoneParameters
            { Message = "clear_notification", Data = data });
        _services.Notify.MobileAppCarleenMobiel(new NotifyMobileAppCarleenMobielParameters
            { Message = "clear_notification", Data = data });
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
            data.TtsText = phoneMessage;

        // TTS notifications must use the "process" channel, otherwise the phone doesn't speak them
        data.Channel = tts ? "process" : channel;
        data.VibrationPattern = vibrationPattern; //"100, 1000, 100, 1000, 100"
        // Notifications with a vibration pattern are alarms: show them with a red LED
        data.LedColor = vibrationPattern != null ? "red" : null;
        data.Image = image;

        if (actions != null)
        {
            if (actions.Count > 3) throw new ArgumentException("To many actions");

            // A tag is needed to be able to clear the notification once one of its actions is pressed
            var tag = $"actionable-{Guid.NewGuid()}";
            data.Tag = tag;

            // All button subscriptions of this notification: disposed once one button is handled, or after a day
            var subscriptions = new CompositeDisposable();
            var handled = 0;
            subscriptions.Add(Observable.Timer(NotificationActionLifetime).Subscribe(_ => subscriptions.Dispose()));

            foreach (var action in actions.Where(action => action.Func != null))
            {
                action.Action = $"{action.Action}-{Guid.NewGuid().ToString()}";
                var func = action.Func!;
                subscriptions.Add(SubscribeToNotificationAction(action.Action, () =>
                {
                    // Only the first press counts, e.g. when both phones press a button of the same notification
                    if (Interlocked.Exchange(ref handled, 1) == 1) return;

                    subscriptions.Dispose();
                    func.Invoke();
                    ClearNotification(tag);
                }));
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
        if (notification == null) return true;

        // Only send again once the waiting time since the last notification with this title has passed
        return DateTimeOffset.Now >= notification.LastSendNotification.AddMinutes(sendAfterMinutes ?? 60);
    }

    /// <summary>
    /// Saves a notification to the notification history.
    /// </summary>
    /// <param name="storage">The data repository for storing notification history.</param>
    /// <param name="title">The title of the notification.</param>
    /// <param name="message">The message of the notification.</param>
    private static void SaveNotification(IDataRepository storage, string title, string message)
    {
        // Every app has its own Notify, but they share the history: update it one at a time
        lock (HistoryLock)
        {
            var oldData = storage.Get<List<NotificationModel>>("notificationHistory") ?? new List<NotificationModel>();
            var data = oldData.Find(x => x.Name == title);
            if (data != null)
            {
                data.Value = message;
                data.LastSendNotification = DateTimeOffset.Now;
            }
            else
                oldData.Add(new NotificationModel(name: title, value: message, lastSendNotification: DateTimeOffset.Now));

            storage.Save("notificationHistory", oldData);
        }
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
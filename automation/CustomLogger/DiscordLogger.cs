using Automation.Helpers;
using Serilog.Core;
using Serilog.Events;
using Discord;
using Discord.Webhook;
using Color = Discord.Color;

namespace Automation.CustomLogger;

/// <summary>
/// A custom logger that sends log events to a Discord channel using webhooks.
/// </summary>
public class DiscordLogger : ILogEventSink
{
    private readonly IFormatProvider? _formatProvider;
    private readonly LogEventLevel _restrictedToMinimumLevel;

    private static readonly string WebhookUrlDebug = ConfigManager.GetValueFromConfigNested("NetDaemonLogging", "Debug") ?? throw new InvalidOperationException();
    private static readonly string WebhookUrlInformation = ConfigManager.GetValueFromConfigNested("NetDaemonLogging", "Information") ?? throw new InvalidOperationException();
    private static readonly string WebhookUrlWarning = ConfigManager.GetValueFromConfigNested("NetDaemonLogging", "Warning") ?? throw new InvalidOperationException();
    private static readonly string WebhookUrlError = ConfigManager.GetValueFromConfigNested("NetDaemonLogging", "Error") ?? throw new InvalidOperationException();
    private static readonly string WebhookUrlException = ConfigManager.GetValueFromConfigNested("NetDaemonLogging", "Exception") ?? throw new InvalidOperationException();

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscordLogger"/> class.
    /// </summary>
    /// <param name="formatProvider">The format provider for formatting log messages.</param>
    /// <param name="restrictedToMinimumLevel">The minimum log event level to log.</param>
    public DiscordLogger(
        IFormatProvider? formatProvider,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Information)
    {
        _formatProvider = formatProvider;
        _restrictedToMinimumLevel = restrictedToMinimumLevel;
    }

    /// <summary>
    /// Emits a log event to the Discord channel.
    /// </summary>
    /// <param name="logEvent">The log event to emit.</param>
    public void Emit(LogEvent logEvent)
    {
        _ = Task.Run(async () => await SendMessageAsync(logEvent));
    }

    /// <summary>
    /// Sends a log event message to the Discord channel.
    /// </summary>
    /// <param name="logEvent">The log event to send.</param>
    private async Task SendMessageAsync(LogEvent logEvent)
    {
        if (!ShouldLogMessage(_restrictedToMinimumLevel, logEvent.Level))
            return;

        var embedBuilder = new EmbedBuilder();

        try
        {
            if (logEvent.Exception != null)
            {
                var webHook = new DiscordWebhookClient(GetWebhookUrl());
                embedBuilder.Color = Color.DarkRed;
                embedBuilder.WithTitle(":o: Exception");
                embedBuilder.AddField("Type:", $"```{logEvent.Exception.GetType().FullName}```");

                var message = FormatMessage(logEvent.Exception.Message, 240);
                embedBuilder.AddField("Message:", message);

                var stackTrace = FormatMessage(logEvent.Exception.StackTrace ?? string.Empty, 1024);
                embedBuilder.AddField("StackTrace:", stackTrace);

                await webHook.SendMessageAsync(null, false, [embedBuilder.Build()]);
            }
            else
            {
                var webHook = new DiscordWebhookClient(GetWebhookUrl(logEvent.Level));
                var message = logEvent.RenderMessage(_formatProvider);
                message = FormatMessage(message, 240);

                SpecifyEmbedLevel(logEvent.Level, embedBuilder);
                embedBuilder.Description = message;

                await webHook.SendMessageAsync(
                        null, false, [embedBuilder.Build()]);
            }
        }
        catch (Exception ex)
        {
            var webHook = new DiscordWebhookClient(GetWebhookUrl());
            await webHook.SendMessageAsync(
                    $"ooo snap, {ex.Message}");
        }
    }

    /// <summary>
    /// Specifies the embed level for the log event.
    /// </summary>
    /// <param name="level">The log event level.</param>
    /// <param name="embed">The embed builder to configure.</param>
    private static void SpecifyEmbedLevel(LogEventLevel level, EmbedBuilder embed)
    {
        switch (level)
        {
            case LogEventLevel.Verbose:
                embed.Title = ":loud_sound: Verbose";
                embed.Color = Color.LightGrey;
                break;
            case LogEventLevel.Debug:
                embed.Title = ":mag: Debug";
                embed.Color = Color.LightGrey;
                break;
            case LogEventLevel.Information:
                embed.Title = ":information_source: Information";
                embed.Color = new Color(0, 186, 255);
                break;
            case LogEventLevel.Warning:
                embed.Title = ":warning: Warning";
                embed.Color = new Color(255, 204, 0);
                break;
            case LogEventLevel.Error:
                embed.Title = ":x: Error";
                embed.Color = new Color(255, 0, 0);
                break;
            case LogEventLevel.Fatal:
                embed.Title = ":skull_crossbones: Fatal";
                embed.Color = Color.DarkRed;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(level), level, null);
        }
    }

    /// <summary>
    /// Formats a message to a specified maximum length.
    /// </summary>
    /// <param name="message">The message to format.</param>
    /// <param name="maxLength">The maximum length of the message.</param>
    /// <returns>The formatted message.</returns>
    private static string FormatMessage(string message, int maxLength)
    {
        if (message.Length > maxLength)
            message = $"{message[..maxLength]} ...";

        if (!string.IsNullOrWhiteSpace(message))
            message = $"```{message}```";

        return message;
    }

    /// <summary>
    /// Determines whether a log message should be logged based on the minimum log event level.
    /// </summary>
    /// <param name="minimumLogEventLevel">The minimum log event level.</param>
    /// <param name="messageLogEventLevel">The log event level of the message.</param>
    /// <returns>True if the message should be logged; otherwise, false.</returns>
    private static bool ShouldLogMessage(
        LogEventLevel minimumLogEventLevel,
        LogEventLevel messageLogEventLevel) =>
        (int)messageLogEventLevel >= (int)minimumLogEventLevel;

    /// <summary>
    /// Gets the webhook URL for a specific log event level.
    /// </summary>
    /// <param name="level">The log event level.</param>
    /// <returns>The webhook URL.</returns>
    private static string GetWebhookUrl(LogEventLevel? level = null)
    {
        return level switch
        {
            LogEventLevel.Verbose => WebhookUrlDebug,
            LogEventLevel.Debug => WebhookUrlDebug,
            LogEventLevel.Information => WebhookUrlInformation,
            LogEventLevel.Warning => WebhookUrlWarning,
            LogEventLevel.Error => WebhookUrlError,
            LogEventLevel.Fatal => WebhookUrlException,
            null => WebhookUrlException,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
        };
    }
}
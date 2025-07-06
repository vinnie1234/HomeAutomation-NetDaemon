# Logging & Monitoring

## Overview

The Home Automation NetDaemon system implements comprehensive logging and monitoring to ensure system reliability, troubleshooting capabilities, and operational awareness.

## Logging Architecture

### Multi-Target Logging
The system uses Serilog with multiple output targets:
- **Console Logging**: Real-time development feedback
- **Discord Logging**: Real-time notifications and alerts
- **File Logging**: Local log file storage (optional)
- **Structured Logging**: JSON-formatted logs for analysis

### Custom Discord Logger

#### Implementation
```csharp
public class DiscordLogger : ILogEventSink
{
    private readonly IFormatProvider? _formatProvider;
    private readonly LogEventLevel _restrictedToMinimumLevel;

    public void Emit(LogEvent logEvent)
    {
        _ = Task.Run(async () => await SendMessageAsync(logEvent));
    }

    private async Task SendMessageAsync(LogEvent logEvent)
    {
        if (logEvent.Exception != null)
        {
            // Handle exceptions with detailed stack traces
            var embedBuilder = new EmbedBuilder();
            embedBuilder.Color = Color.DarkRed;
            embedBuilder.WithTitle(":o: Exception");
            embedBuilder.AddField("Type:", $"```{logEvent.Exception.GetType().FullName}```");
            embedBuilder.AddField("Message:", FormatMessage(logEvent.Exception.Message, 240));
            embedBuilder.AddField("StackTrace:", FormatMessage(logEvent.Exception.StackTrace ?? string.Empty, 1024));
            
            await webHook.SendMessageAsync(null, false, [embedBuilder.Build()]);
        }
        else
        {
            // Handle regular log messages with appropriate color coding
            var embedBuilder = new EmbedBuilder();
            SpecifyEmbedLevel(logEvent.Level, embedBuilder);
            embedBuilder.Description = FormatMessage(logEvent.RenderMessage(_formatProvider), 240);
            
            await webHook.SendMessageAsync(null, false, [embedBuilder.Build()]);
        }
    }
}
```

#### Discord Integration Features
- **Real-time Notifications**: Immediate alerts sent to Discord via webhooks
- **Rich Embeds**: Color-coded messages with emojis for different log levels
- **Exception Handling**: Detailed stack traces and exception information
- **Multiple Channels**: Different webhook URLs for different log levels (Debug, Information, Warning, Error, Exception)
- **Structured Formatting**: Automatic message formatting with code blocks

## Log Levels & Categories

### Standard Log Levels
- **Critical**: System failures, security issues
- **Error**: Exceptions, failed operations
- **Warning**: Recoverable issues, deprecated usage
- **Information**: Important events, state changes
- **Debug**: Detailed execution flow, variable values
- **Trace**: Extremely detailed debugging information

### Application-Specific Categories

#### System Events
```csharp
Logger.LogInformation("NetDaemon application started successfully");
Logger.LogWarning("High memory usage detected: {MemoryUsage}MB", memoryUsage);
Logger.LogError("Failed to connect to Home Assistant: {Error}", exception.Message);
```

#### Automation Events
```csharp
Logger.LogDebug("Motion detected in {Room}, turning on lights", roomName);
Logger.LogInformation("Away mode activated, securing house");
Logger.LogWarning("Battery low on {Device}: {Level}%", deviceName, batteryLevel);
```

#### Device Interactions
```csharp
Logger.LogDebug("Sending service call: {Service} to entity: {Entity}", service, entityId);
Logger.LogInformation("Light scene changed to {Scene} in {Room}", sceneName, roomName);
Logger.LogError("Failed to control device {Device}: {Error}", deviceId, error);
```

## Discord Notification System

### Channel Configuration
Different Discord channels receive different types of log messages based on severity:

#### Debug Channel
- **Detailed Execution**: Step-by-step operation logging
- **Variable Values**: State information and debugging data
- **Development Information**: Internal system operations

#### Information Channel
- **System Events**: Application startup, normal operations
- **State Changes**: Entity state updates and automation triggers
- **Routine Operations**: Daily automation activities

#### Warning Channel
- **Recoverable Issues**: Non-critical problems that were handled
- **Performance Warnings**: High resource usage alerts
- **Configuration Warnings**: Potential configuration issues

#### Error Channel
- **Failed Operations**: Service calls that failed
- **Connection Issues**: Network or integration problems
- **Unexpected Behavior**: Logic errors and unexpected states

#### Exception Channel
- **Critical Errors**: Unhandled exceptions with full stack traces
- **System Failures**: Critical system component failures
- **Security Issues**: Authentication or authorization failures

### Notification Examples

#### Rich Embed Notifications
```csharp
var notification = new DiscordNotificationModel
{
    Embed = new Embed
    {
        Title = "🔋 Low Battery Alert",
        Description = "Multiple devices require attention",
        Color = 0xFF6B35, // Orange color
        Thumbnail = new Location("https://example.com/battery-icon.png"),
        Fields = new[]
        {
            new Field { Name = "Device", Value = "Vincent's Phone", Inline = true },
            new Field { Name = "Battery Level", Value = "15%", Inline = true },
            new Field { Name = "Status", Value = "Not Charging", Inline = true }
        },
        Footer = new Footer 
        { 
            Text = $"Home Automation • {DateTime.Now:HH:mm:ss}" 
        }
    }
};
```

#### Simple Text Notifications
```csharp
Notify.NotifyPhoneVincent("Welcome Home", 
    "Motion detected in hallway. Lights activated.", priority: true);
```

## Monitoring Features

### Performance Monitoring

#### System Metrics
- **Memory Usage**: RAM consumption tracking
- **CPU Usage**: Processor utilization monitoring
- **Response Times**: Service call performance
- **Thread Pool Status**: Async operation efficiency

#### Application Metrics
- **Event Processing**: Rate of entity state changes
- **Service Calls**: Home Assistant API call frequency
- **Error Rates**: Failed operations percentage
- **Notification Volume**: Discord message frequency

### Health Checks

#### Connectivity Monitoring
```csharp
// Regular health checks for external services
Scheduler.ScheduleCron("*/5 * * * *", async () =>
{
    var isHealthy = await CheckHomeAssistantConnectivity();
    if (!isHealthy)
    {
        Logger.LogError("Home Assistant connectivity lost");
        await NotifySystemAdministrator("HA Connection Failed");
    }
});
```

#### Device Status Monitoring
- **Battery Levels**: Automatic monitoring of all battery-powered devices
- **Last Seen**: Track when devices last reported status
- **Communication Errors**: Failed device communications
- **Service Availability**: External API availability

### Error Tracking & Analysis

#### Exception Handling
```csharp
try
{
    await entity.TurnOnAsync();
}
catch (Exception ex)
{
    Logger.LogError(ex, "Failed to turn on {Entity}", entity.EntityId);
    
    // Send immediate notification for critical errors
    if (IsCriticalDevice(entity))
    {
        await Notify.NotifyDiscord("Critical Device Error", 
            $"Failed to control {entity.EntityId}: {ex.Message}");
    }
}
```

#### Error Categorization
- **Transient Errors**: Network timeouts, temporary unavailability
- **Configuration Errors**: Invalid settings, missing entities
- **Logic Errors**: Unexpected application behavior
- **External Errors**: Third-party service failures

## Debugging & Troubleshooting

### Debug Mode Features
When running in debug mode, additional logging includes:
- **Detailed State Changes**: Full entity state information
- **Service Call Parameters**: Complete parameter sets for service calls
- **Timing Information**: Execution duration for operations
- **Memory Snapshots**: Periodic memory usage reports

### Log Analysis Tools

#### Structured Logging Benefits
```json
{
    "@timestamp": "2025-07-06T10:30:00.000Z",
    "level": "Information",
    "message": "Motion detected in hallway",
    "properties": {
        "Room": "hallway",
        "Sensor": "binary_sensor.hall_motion",
        "Action": "lights_activated",
        "Duration": "PT15M"
    }
}
```

#### Query Examples
- **Find all battery warnings**: `level:Warning AND message:*battery*`
- **Track specific device**: `properties.Entity:light.living_room`
- **Error analysis**: `level:Error AND @timestamp:[now-1h TO now]`

## Operational Procedures

### Daily Monitoring Checklist
1. **Review Discord notifications** for any alerts
2. **Check system status** in monitoring dashboard
3. **Verify critical automations** are functioning
4. **Monitor device battery levels** and replace as needed

### Weekly Maintenance
1. **Review error logs** for patterns
2. **Check external service health** and API limits
3. **Monitor system performance** trends
4. **Update device firmware** as available

### Emergency Procedures

#### System Failure Response
1. **Immediate notification** via Discord critical channel
2. **Fallback to manual control** for essential devices
3. **System restart** with health check validation
4. **Root cause analysis** and documentation

#### Communication Failure Response
1. **Local logging** continues during outages
2. **Retry mechanisms** for Discord notifications
3. **Alternative notification methods** (email, SMS) as backup
4. **Service restoration** procedures

## Configuration Examples

### Logging Configuration
```json
{
    "Serilog": {
        "MinimumLevel": {
            "Default": "Information",
            "Override": {
                "Automation": "Debug",
                "Microsoft": "Warning",
                "System": "Warning"
            }
        },
        "WriteTo": [
            {
                "Name": "Console",
                "Args": {
                    "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                }
            },
            {
                "Name": "Discord",
                "Args": {
                    "webhookUrl": "https://discord.com/api/webhooks/...",
                    "restrictedToMinimumLevel": "Warning"
                }
            }
        ]
    }
}
```

### Discord Webhook Configuration
```json
{
    "NetDaemonLogging": {
        "Debug": "https://discord.com/api/webhooks/debug/...",
        "Information": "https://discord.com/api/webhooks/information/...",
        "Warning": "https://discord.com/api/webhooks/warning/...",
        "Error": "https://discord.com/api/webhooks/error/...",
        "Exception": "https://discord.com/api/webhooks/exception/..."
    }
}
```

### Log Level Visual Indicators
Each log level has a distinct visual representation in Discord:

- **Debug**: :mag: Debug (Light Grey)
- **Information**: :information_source: Information (Blue)
- **Warning**: :warning: Warning (Yellow)
- **Error**: :x: Error (Red)
- **Exception**: :skull_crossbones: Fatal (Dark Red)
# Configuration

## Overview

This document describes the configuration system used in the Home Automation NetDaemon project, including app settings, device configurations, and customization options.

## Configuration Architecture

### Hierarchical Configuration
The system uses a multi-layered configuration approach:
1. **Default Values**: Hard-coded defaults in configuration classes
2. **appsettings.json**: Base application configuration
3. **appsettings.Development.json**: Development environment overrides
4. **config.json**: Runtime app-specific settings
5. **Environment Variables**: Override any setting via environment variables

### Configuration Structure
```
automation/
├── appsettings.json              # Main application settings
├── appsettings.Development.json  # Development overrides
├── config.json                   # App-specific runtime settings
└── Configuration/
    └── AppConfiguration.cs       # Centralized configuration classes
```

## Core Configuration Files

### appsettings.json
Main application configuration file:
```json
{
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "Microsoft": "Warning",
            "Microsoft.Hosting.Lifetime": "Information"
        }
    },
    "HomeAssistant": {
        "Host": "localhost",
        "Port": 8123,
        "Ssl": false,
        "Token": "your-long-lived-access-token-here"
    },
    "Discord": {
        "System": "https://discord.com/api/webhooks/system-webhook-url",
        "General": "https://discord.com/api/webhooks/general-webhook-url",
        "Security": "https://discord.com/api/webhooks/security-webhook-url",
        "Pixel": "https://discord.com/api/webhooks/pixel-webhook-url"
    }
}
```

### config.json
Runtime application settings:
```json
{
    "BaseUrlHomeAssistant": "http://192.168.1.100:8123",
    "ZedarDeviceId": "your-zedar-device-id",
    "PetSnowyDeviceId": "your-petsnowy-device-id",
    "Discord": {
        "Pixel": "https://discord.com/api/webhooks/pixel-specific-url"
    }
}
```

## Centralized App Configuration

### AppConfiguration Class Structure
The system uses strongly-typed configuration classes for better maintainability:

```csharp
public class AppConfiguration
{
    public BatteryConfiguration Battery { get; set; } = new();
    public LightConfiguration Lights { get; set; } = new();
    public TimingConfiguration Timing { get; set; } = new();
}
```

### Battery Configuration
Controls battery monitoring behavior:
```csharp
public class BatteryConfiguration
{
    /// <summary>
    /// Battery level (%) below which warnings are sent
    /// </summary>
    public int WarningLevel { get; set; } = 20;
    
    /// <summary>
    /// How long to wait before sending repeated battery warnings
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(10);
}
```

**Usage in Apps:**
```csharp
public class BatteryMonitoring : BaseApp
{
    private readonly AppConfiguration _config = new();
    
    private void MonitorBatteries()
    {
        batterySensor.WhenStateIsFor(
            x => x?.State <= _config.Battery.WarningLevel, 
            _config.Battery.CheckInterval, 
            Scheduler);
    }
}
```

### Light Configuration
Controls lighting automation behavior:
```csharp
public class LightConfiguration
{
    /// <summary>
    /// Device IDs for various smart devices
    /// </summary>
    public Dictionary<string, string> DeviceIds { get; set; } = new()
    {
        ["HueWallLivingRoom"] = "b4784a8e43cc6f5aabfb6895f3a8dbac"
    };
    
    /// <summary>
    /// Default transition time for light changes (seconds)
    /// </summary>
    public int DefaultTransitionSeconds { get; set; } = 5;
    
    /// <summary>
    /// Throttle time for state change events
    /// </summary>
    public TimeSpan StateChangeThrottleMs { get; set; } = TimeSpan.FromMilliseconds(50);
    
    /// <summary>
    /// Delay between sequential light operations
    /// </summary>
    public TimeSpan DelayBetweenLights { get; set; } = TimeSpan.FromMilliseconds(200);
}
```

### Timing Configuration
Controls various timing delays throughout the application:
```csharp
public class TimingConfiguration
{
    /// <summary>
    /// Delay before showing welcome home message
    /// </summary>
    public TimeSpan WelcomeHomeDelay { get; set; } = TimeSpan.FromSeconds(15);
    
    /// <summary>
    /// Delay for New Year music timing
    /// </summary>
    public TimeSpan NewYearMusicDelay { get; set; } = TimeSpan.FromSeconds(49);
    
    /// <summary>
    /// Short delay for rapid operations
    /// </summary>
    public TimeSpan ShortDelay { get; set; } = TimeSpan.FromSeconds(0.5);
}
```

## Device-Specific Configuration

### Smart Pet Devices
Configuration for pet-related smart devices:
```json
{
    "ZedarDeviceId": "zedar-feeder-001",
    "PetSnowyDeviceId": "petsnowy-litterbox-001"
}
```

### Network Configuration
Home Assistant and network settings:
```json
{
    "BaseUrlHomeAssistant": "http://192.168.1.100:8123",
    "HomeAssistant": {
        "Host": "192.168.1.100",
        "Port": 8123,
        "Ssl": false,
        "Token": "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9..."
    }
}
```

## Environment-Specific Settings

### Development Configuration
`appsettings.Development.json` for local development:
```json
{
    "Logging": {
        "LogLevel": {
            "Default": "Debug",
            "Automation": "Trace"
        }
    },
    "HomeAssistant": {
        "Host": "localhost",
        "Port": 8123
    }
}
```

### Production Configuration
Production settings with enhanced security:
```json
{
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "Microsoft": "Warning"
        }
    },
    "HomeAssistant": {
        "Host": "homeassistant.local",
        "Port": 8123,
        "Ssl": true
    }
}
```

## Configuration Usage Patterns

### Accessing Configuration in Apps
```csharp
public class MyApp : BaseApp
{
    private readonly AppConfiguration _config = new();
    
    public MyApp(/* dependencies */) : base(/* ... */)
    {
        // Use configuration values
        var warningLevel = _config.Battery.WarningLevel;
        var deviceId = _config.Lights.DeviceIds["HueWallLivingRoom"];
        var delay = _config.Timing.WelcomeHomeDelay;
    }
}
```

### Runtime Configuration Access
```csharp
// Access config.json values
var baseUrl = ConfigManager.GetValueFromConfig("BaseUrlHomeAssistant");
var deviceId = ConfigManager.GetValueFromConfig("ZedarDeviceId");

// Access nested configuration values
var discordUrl = ConfigManager.GetValueFromConfigNested("Discord", "Pixel");
```

## Security Considerations

### Sensitive Information
**Never commit sensitive information to source control:**
- Home Assistant access tokens
- Discord webhook URLs
- Device IDs and credentials
- Network configuration details

### Configuration File Security
```bash
# Example .gitignore entries
appsettings.Production.json
config.json
secrets.json
*.local.json
```

### Environment Variables
Override sensitive settings using environment variables:
```bash
export HomeAssistant__Token="your-secure-token"
export Discord__System="https://discord.com/api/webhooks/secure-url"
```

## Configuration Validation

### Startup Validation
The system validates configuration at startup:
```csharp
public void ValidateConfiguration()
{
    if (string.IsNullOrEmpty(homeAssistantToken))
        throw new InvalidOperationException("Home Assistant token is required");
        
    if (_config.Battery.WarningLevel < 1 || _config.Battery.WarningLevel > 100)
        throw new ArgumentOutOfRangeException("Battery warning level must be 1-100");
}
```

### Configuration Health Checks
```csharp
// Regular configuration health checks
Scheduler.ScheduleCron("0 */6 * * *", () =>
{
    var isValid = ValidateConfiguration();
    if (!isValid)
    {
        Logger.LogWarning("Configuration validation failed");
        NotifySystemAdministrator("Configuration Issues Detected");
    }
});
```

## Configuration Management Best Practices

### 1. Default Values
Always provide sensible defaults in configuration classes:
```csharp
public class TimingConfiguration
{
    public TimeSpan DefaultDelay { get; set; } = TimeSpan.FromSeconds(30); // Good default
}
```

### 2. Documentation
Document configuration options clearly:
```csharp
/// <summary>
/// Controls how long to wait before sending duplicate notifications
/// Range: 1 minute to 24 hours
/// Default: 1 hour
/// </summary>
public TimeSpan NotificationThrottle { get; set; } = TimeSpan.FromHours(1);
```

### 3. Type Safety
Use strongly-typed configuration classes instead of magic strings:
```csharp
// Good
var level = _config.Battery.WarningLevel;

// Avoid
var level = int.Parse(ConfigurationManager.AppSettings["BatteryWarningLevel"]);
```

### 4. Configuration Reloading
Support configuration reloading for non-critical settings:
```csharp
// Monitor configuration file changes
IOptionsMonitor<AppConfiguration> configMonitor;
configMonitor.OnChange(newConfig => 
{
    Logger.LogInformation("Configuration updated");
    ApplyNewConfiguration(newConfig);
});
```

## Example Complete Configuration

### appsettings.json (Production Ready)
```json
{
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "Automation.apps": "Debug",
            "Microsoft": "Warning",
            "System": "Warning"
        }
    },
    "HomeAssistant": {
        "Host": "homeassistant.local",
        "Port": 8123,
        "Ssl": true,
        "Token": "${HOME_ASSISTANT_TOKEN}"
    },
    "Discord": {
        "System": "${DISCORD_SYSTEM_WEBHOOK}",
        "General": "${DISCORD_GENERAL_WEBHOOK}",
        "Security": "${DISCORD_SECURITY_WEBHOOK}",
        "Pixel": "${DISCORD_PIXEL_WEBHOOK}"
    },
    "AppConfiguration": {
        "Battery": {
            "WarningLevel": 20,
            "CheckInterval": "10:00:00"
        },
        "Lights": {
            "DefaultTransitionSeconds": 5,
            "StateChangeThrottleMs": "00:00:00.050",
            "DelayBetweenLights": "00:00:00.200"
        },
        "Timing": {
            "WelcomeHomeDelay": "00:00:15",
            "NewYearMusicDelay": "00:00:49",
            "ShortDelay": "00:00:00.500"
        }
    }
}
```
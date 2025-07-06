# General Automation Apps

## Overview

This document covers the smaller general-purpose automation apps that provide system-wide functionality and utilities.

## Alarm App

### Purpose
Manages alarm clock functionality and wake-up routines with integration to Home Assistant and external alarms.

### Key Features
- **Multiple Alarm Sources**: Supports phone alarms and Home Assistant alarms
- **Wake-up Automation**: Coordinates with sleep manager for wake-up routines
- **Alarm State Tracking**: Monitors and logs alarm states
- **Smart Snoozing**: Intelligent snooze handling

### Configuration
- **Alarm Entities**: `sensor.hub_vincent_alarms`
- **Sleep State**: Integration with sleep manager
- **Wake-up Actions**: Coordinated lighting and device control

---

## Auto Update App

### Purpose
Monitors and manages system updates for Home Assistant addons and the NetDaemon application itself.

### Key Features
- **Update Detection**: Monitors for available updates
- **Notification System**: Discord alerts for pending updates
- **Restart Management**: Handles restart notifications and confirmations
- **Update Scheduling**: Smart timing for update installations

### Technical Implementation
```csharp
private async Task AutoUpdate()
{
    var updates = await GetAvailableUpdates();
    if (updates.Any())
    {
        await NotifyUpdatesAvailable(updates);
        await ScheduleUpdateInstallation();
    }
}
```

---

## Battery Monitoring App

### Purpose
Comprehensive battery level monitoring for all battery-powered devices in the home automation system.

### Key Features
- **Universal Monitoring**: Tracks all devices with battery sensors
- **Smart Thresholds**: Configurable warning levels (default: 20%)
- **Throttled Notifications**: Prevents notification spam
- **Charging Detection**: Different behavior for charging vs non-charging devices

### Configuration
```csharp
public class BatteryConfiguration
{
    public int WarningLevel { get; set; } = 20;
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(10);
}
```

### Monitoring Logic
- **Low Battery Alerts**: Notifications when battery drops below threshold
- **Charging Status**: Monitors charging state to avoid false alerts
- **Device Categories**: Different handling for phones, tablets, sensors, etc.

---

## CoC Monitoring App

### Purpose
Monitors Clash of Clans clan activities via Twitter/X integration and provides Discord notifications.

### Key Features
- **Twitter Integration**: Monitors clan Twitter account via RestSharp
- **Clan Activity Tracking**: War results, member activities, announcements
- **Discord Notifications**: Real-time updates to Discord channels
- **Error Handling**: Robust handling of API failures

### Technical Implementation
```csharp
private async Task CheckClanActivity()
{
    var request = new RestRequest("clan-activity-endpoint");
    var response = await _client.ExecuteAsync(request);
    
    if (response.IsSuccessful)
    {
        await ProcessClanData(response.Content);
    }
}
```

---

## Download Monitoring App

### Purpose
Monitors download completion and provides notifications for completed downloads.

### Key Features
- **Download Detection**: Monitors download directories and status
- **Completion Notifications**: Alerts when downloads finish
- **File Management**: Optional file organization after download
- **Progress Tracking**: Real-time download progress updates

---

## Fun App

### Purpose
Provides entertainment and special occasion automations including New Year celebrations and parent visit notifications.

### Key Features

#### New Year Automation
- **Music Playback**: Automatic "Happy New Year" music
- **Light Show**: Synchronized lighting effects
- **Firework Simulation**: LED light effects simulating fireworks
- **Timing Control**: Precise timing for celebrations

#### Parent Visit Detection
- **Arrival Detection**: Detects when parents arrive
- **Welcome Messages**: Personalized greetings
- **House Preparation**: Adjusts house settings for guests

### Technical Implementation
```csharp
private async Task ChristmasFirework()
{
    var colors = new[] { "RED", "GREEN", "BLUE", "WHITE" };
    var stopwatch = Stopwatch.StartNew();
    
    do
    {
        var randomColor = colors[Random.Next(colors.Length)];
        Entities.Light.Tv.TurnOn(colorName: randomColor);
        await Task.Delay(_config.Timing.ShortDelay);
    } 
    while (stopwatch.Elapsed < TimeSpan.FromMinutes(4));
}
```

---

## Google Assistant Button Translate App

### Purpose
Translates Google Assistant voice commands and input boolean states to appropriate automation actions.

### Key Features
- **Voice Command Translation**: Converts voice commands to actions
- **Input Boolean Mapping**: Maps input booleans to input buttons
- **Command Categorization**: Different handling for different command types
- **Action Execution**: Executes appropriate automations based on commands

### Command Categories
- **Entertainment Commands**: TV, music, gaming controls
- **Vacuum Commands**: Room-specific cleaning commands
- **Cat Care Commands**: Pet feeding and care actions
- **Lighting Commands**: Room and scene lighting controls

---

## Holiday Manager App

### Purpose
Manages holiday states and related automations, including calendar integration for vacation detection.

### Key Features
- **Holiday Detection**: Automatic detection via calendar events
- **Alarm Management**: Reminds to disable/enable alarms during holidays
- **Automation Adjustments**: Modified behavior during holiday periods
- **Calendar Integration**: Reads calendar for "vrij" (free) or "vakantie" (vacation) keywords

### Holiday Behaviors
- **Alarm Reminders**: Notifications to turn off alarms when holiday starts
- **Return Reminders**: Notifications to set alarms when holiday ends
- **Automation Modes**: Different automation behavior during holidays

---

## House State Manager App

### Purpose
Central management of house states (Morning, Day, Evening, Night) with automatic transitions and manual overrides.

### Key Features
- **State Management**: Controls overall house state
- **Sun-Based Transitions**: Automatic state changes based on sunrise/sunset
- **Schedule Integration**: Work schedule awareness for state transitions
- **Scene Integration**: Coordinates with lighting scenes
- **Manual Overrides**: Scene activation can override automatic states

### State Transitions
- **Morning**: Sunrise-triggered, prepares house for day
- **Day**: Work/weekend schedule awareness
- **Evening**: Sunset-triggered, relaxing ambiance
- **Night**: Sleep preparation, minimal lighting

---

## NetDaemon App

### Purpose
System monitoring and lifecycle management for the NetDaemon application itself.

### Key Features
- **Startup Notifications**: Discord alerts when NetDaemon starts
- **Health Monitoring**: System health checks and reporting
- **Restart Management**: Handles application restarts
- **Error Reporting**: System-level error notifications

---

## PC Manager App

### Purpose
Manages PC-related automations including startup detection and device control.

### Key Features
- **PC State Detection**: Monitors when PC starts/stops
- **Device Control**: Controls PC-related devices (lights, peripherals)
- **Gaming Integration**: Coordinates with gaming automation
- **Power Management**: Smart power control for PC peripherals

---

## Reset App

### Purpose
Provides system reset and restoration capabilities for lights and device states.

### Key Features
- **Light State Restoration**: Restores saved lighting states
- **Device Reset**: Resets devices to default states
- **Alarm Management**: Manages alarm deletion and restoration
- **Color Temperature**: Restores proper color temperature for compatible lights

---

## Save In State App

### Purpose
Saves and manages device states for later restoration, particularly useful for lighting scenes and device configurations.

### Key Features
- **State Capture**: Saves current states of lights and devices
- **Selective Saving**: Choose which entities to save
- **State Restoration**: Restore previously saved states
- **Scene Management**: Integration with lighting scene system

### State Management
- **Light States**: Brightness, color, temperature
- **Device States**: Power states, settings, configurations
- **Alarm States**: Alarm configurations and schedules

---

## Sleep Manager App

### Purpose
Comprehensive sleep routine management including wake-up sequences, sleep preparation, and energy price monitoring.

### Key Features

#### Sleep Routines
- **Sleep Preparation**: Coordinated house shutdown for sleep
- **Wake-up Sequences**: Gradual lighting and device activation
- **Weekend Handling**: Different behavior on weekends
- **Automatic Override**: Safety overrides for early morning activity

#### Energy Price Monitoring
- **Real-time Prices**: Monitors EPEX SPOT electricity prices
- **Price Alerts**: Notifications for very high/low prices
- **Smart Scheduling**: Energy usage recommendations based on prices

#### Battery Warnings
- **Device Monitoring**: Checks phone and tablet battery levels
- **Charging Reminders**: Alerts to charge devices before sleep
- **Low Battery Alerts**: Wake-up notifications for low battery devices

### Sleep Sequence
1. **Sleep Mode Activated** → All lights turn off, TV shuts down, blinds close
2. **Security Check** → Verify doors locked, alarms set
3. **Pet Care** → Check litter box status, food levels
4. **Waste Reminder** → Garbage collection reminders if applicable
5. **Energy Prices** → Next-day electricity price notifications

---

## Vacuum App

### Purpose
Intelligent robot vacuum control with room-specific cleaning, scheduling, and trigger-based activation.

### Key Features
- **Room-Specific Cleaning**: Individual room cleaning via buttons
- **Litter Box Integration**: Automatic cleaning after litter box use
- **Schedule Management**: Smart scheduling based on occupancy
- **Status Monitoring**: Vacuum status and error reporting

### Cleaning Triggers
- **Manual Buttons**: Individual room cleaning buttons
- **Automatic Triggers**: Cleaning after specific events (litter box use)
- **Schedule-Based**: Time-based cleaning routines
- **Occupancy-Aware**: Avoids cleaning when people are sleeping

### Room Management
```csharp
var cleaningRooms = new Dictionary<InputButtonEntity, string>
{
    { Entities.InputButton.Vacuumcleankattenbak, "Kattenbak" },
    { Entities.InputButton.Vacuumcleanbank, "Bank" },
    { Entities.InputButton.Vacuumcleangang, "Gang" },
    { Entities.InputButton.Vacuumcleanslaapkamer, "Slaapkamer" },
    { Entities.InputButton.Vacuumcleanwoonkamer, "Woonkamer" }
};
```

## Common Configuration Patterns

### Notification Integration
Most apps integrate with the Discord notification system:
```csharp
protected void SendNotification(string title, string message, bool priority = false)
{
    Notify.NotifyPhoneVincent(title, message, priority);
    // Or Discord notifications
    Notify.NotifyDiscord(title, channels, richContent);
}
```

### Scheduler Usage
Apps frequently use scheduling for automation:
```csharp
// Daily schedules
Scheduler.RunDaily(TimeSpan.FromHours(8), () => DailyRoutine());

// Cron schedules
Scheduler.ScheduleCron("0 0 * * *", () => MidnightTasks());

// Delayed actions
Scheduler.Schedule(TimeSpan.FromMinutes(5), () => DelayedAction());
```

### State Monitoring
Common pattern for entity state monitoring:
```csharp
Entities.Sensor.SomeDevice
    .StateChanges()
    .Where(x => x.New.State == "target_state")
    .Subscribe(_ => HandleStateChange());
```
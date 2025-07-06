# Away Manager App

## Overview

The Away Manager app (`AwayManager.cs`) handles home/away state detection and orchestrates all related automations when the house is occupied or vacant.

## Key Features

### 🏠 Home/Away Detection
- **Vincent's Presence**: Primary person detection via device tracker
- **Guest Detection**: Secondary person (Timo) presence monitoring
- **Vacation Mode**: Override for extended away periods
- **Smart State Management**: Prevents rapid state changes

### 🔒 Away Mode Automations
- **Security Measures**: Activates security-related automations
- **Media Control**: Turns off all media players and TVs
- **Energy Saving**: Optimizes energy consumption
- **Notification System**: Sends departure confirmation

### 🎉 Welcome Home Automations
- **Lighting Scene**: Activates appropriate lighting based on time of day
- **Welcome Message**: Personalized greeting announcement
- **Pet Food Alert**: Checks if pet food needs refilling
- **System Restoration**: Restores normal operational modes

### ⏰ Delayed Actions
- **15-Second Delay**: Welcome message delayed to allow settling in
- **State Stabilization**: Prevents automation conflicts during transitions
- **Context-Aware Messages**: Different greetings based on house state

## Technical Implementation

### Presence Detection Logic
```csharp
private void SetupPresenceDetection()
{
    // Primary person (Vincent) presence detection
    Entities.Person.VincentMaarschalkerweerd
        .StateChanges()
        .Where(x => x.Entity.State == "home" && !_backHome)
        .Subscribe(_ => BackHome());

    // Away detection (both Vincent and Timo away, or vacation mode)
    var awayCondition = Entities.Person.VincentMaarschalkerweerd
        .StateChanges()
        .Where(x => Globals.AmIHomeCheck(Entities))
        .Subscribe(_ => SetAwayMode());
}
```

### Welcome Home Sequence
```csharp
private void BackHome()
{
    var houseState = Globals.GetHouseState(Entities);
    
    // Send phone notification
    NotifyVincentPhone(houseState);
    
    // Set appropriate lighting scene
    SetLightScene(houseState);
    
    _backHome = false;
    
    // Delayed welcome message (15 seconds)
    Scheduler.Schedule(_config.Timing.WelcomeHomeDelay, () =>
    {
        var message = "Welkom thuis Vincent!";
        if (Entities.Sensor.ZedarFoodStorageStatus.State != "full")
            message += " Het eten van Pixel is bijna op!";
            
        Notify.NotifyHouse("welcomeHome", message, true);
    });
}
```

### Away Mode Activation
```csharp
private void SetAwayMode()
{
    // Turn off all media players
    TurnOffMediaPlayers();
    
    // Send departure notification
    Notify.NotifyPhoneVincent("Tot ziens", "Je laat je huis weer alleen :(", true);
    
    // Set away state
    Entities.InputBoolean.Away.TurnOn();
    
    // Additional security and energy-saving measures
    ActivateSecurityMode();
}
```

## Configuration

### Required Entities
- **Person Trackers**: `person.vincent_maarschalkerweerd`, `person.timo`
- **Away State**: `input_boolean.away`
- **Vacation Mode**: `input_boolean.onvacation`
- **House Mode**: `input_select.housemodeselect`
- **Pet Food Status**: `sensor.zedar_food_storage_status`

### Device Trackers
The system relies on device presence detection:
- **Phone GPS**: Primary location tracking
- **WiFi Connection**: Secondary presence confirmation
- **Network Devices**: Router-based device detection

### Timing Configuration
```csharp
public class TimingConfiguration
{
    public TimeSpan WelcomeHomeDelay { get; set; } = TimeSpan.FromSeconds(15);
}
```

## Behavior Scenarios

### Scenario 1: Leaving Home
1. **Vincent Leaves** → Device tracker shows "not_home"
2. **Away Check** → System verifies both Vincent and Timo are away (or vacation mode)
3. **Away Mode Activated** → All away automations trigger
4. **Media Shutdown** → TVs and media players turn off
5. **Notification Sent** → "Tot ziens" message to Vincent's phone
6. **Security Active** → Away-related security measures activate

### Scenario 2: Returning Home
1. **Vincent Arrives** → Device tracker shows "home"
2. **Welcome Sequence** → Immediate phone notification sent
3. **Lighting Scene** → Appropriate lights turn on based on time of day
4. **Delayed Message** → 15-second delay before welcome announcement
5. **Pet Check** → Food level checked, alert if low
6. **State Reset** → Away mode deactivated, normal operations resume

### Scenario 3: Guest Presence
1. **Timo Present** → Secondary person detection
2. **Vincent Away** → Primary person not home
3. **No Away Mode** → House remains in "home" state
4. **Limited Automation** → Some automations may behave differently

### Scenario 4: Vacation Mode
1. **Vacation Activated** → Manual override for extended absence
2. **Extended Away** → Different automation behavior for long periods
3. **Security Enhanced** → Additional security measures during vacation
4. **Energy Optimization** → Extended energy-saving modes

## Smart Features

### Adaptive Lighting
Welcome home lighting adapts to current house state:
- **Morning**: Bright, energizing lighting
- **Day**: Standard daytime lighting levels
- **Evening**: Warm, relaxing ambiance
- **Night**: Minimal lighting to avoid disruption

### Context-Aware Notifications
```csharp
private void NotifyVincentPhone(HouseState houseState)
{
    var greeting = houseState switch
    {
        HouseState.Morning => "Goedemorgen Vincent!",
        HouseState.Day => "Welkom thuis!",
        HouseState.Evening => "Goedenavond Vincent!",
        HouseState.Night => "Welkom thuis (stil aan, het is laat!)",
        _ => "Welkom thuis Vincent!"
    };
    
    Notify.NotifyPhoneVincent("Thuis", greeting, priority: true);
}
```

### Pet Care Integration
- **Food Level Check**: Automatic check of pet food status
- **Low Food Alert**: Included in welcome message if food is low
- **Pet Device Status**: Monitors pet-related devices during away periods

### Race Condition Prevention
```csharp
private bool _backHome; // Prevents rapid state changes
```

## Integration Points

### House State Manager
- **State Coordination**: Works with central house state system
- **Time-Based Behavior**: Different actions based on time of day
- **Mode Awareness**: Respects current house operational mode

### Security System
- **Away Security**: Activates enhanced security monitoring
- **Motion Alerts**: Different motion detection behavior when away
- **Door/Window Monitoring**: Enhanced monitoring during away periods

### Media Control
- **Automatic Shutdown**: Turns off all entertainment devices
- **Power Saving**: Reduces standby power consumption
- **Welcome Restoration**: Can restore previous media states


## Advanced Features

### Smart Scheduling
- **Arrival Prediction**: Learn typical arrival patterns
- **Preparation Automation**: Pre-activate systems before arrival
- **Energy Optimization**: Smart pre-heating/cooling based on arrival times

### Extended Away Detection
- **Long-term Away**: Different behavior for multi-day absences
- **Maintenance Mode**: Reduced automation activity during extended away
- **Security Enhancement**: Escalated security measures for long absences

### Guest Mode
- **Guest Detection**: Different behavior when guests are present
- **Privacy Mode**: Reduced monitoring when guests are home
- **Guest Notifications**: Different welcome messages for different people


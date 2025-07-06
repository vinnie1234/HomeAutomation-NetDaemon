# Cat Automation App

## Overview

The Cat app (`Cat.cs`) provides comprehensive automation for pet care, including automatic feeding schedules, litter box monitoring, and pet device management for Pixel the cat.

## Key Features

### 🍽️ Automatic Feeding System
- **Scheduled Feeding**: Multiple daily feeding times with configurable portions
- **Manual Feeding**: On-demand feeding via button control
- **Portion Tracking**: Monitors daily and lifetime food consumption
- **Smart Scheduling**: Configurable feeding times and amounts

### 📊 Food Consumption Tracking
- **Daily Totals**: Tracks total food given per day
- **Lifetime Statistics**: Cumulative feeding data
- **Manual vs Automatic**: Separate tracking for different feeding types
- **Food Level Monitoring**: Alerts when food storage is low

### 🏠 Litter Box Automation (PetSnowy)
- **Usage Monitoring**: Tracks when Pixel uses the litter box
- **Automatic Cleaning**: Triggers cleaning cycles after use
- **Error Detection**: Monitors for litter box malfunctions
- **Manual Controls**: Override options for cleaning and emptying

### 💧 Water Fountain Monitoring
- **Status Monitoring**: Ensures water fountain stays operational
- **Alert System**: Notifications when fountain goes offline
- **Automatic Recovery**: Attempts to restore fountain operation

### 📱 Discord Notifications
- **Feeding Alerts**: Rich notifications for all feeding events
- **Device Status**: Updates on litter box and fountain status
- **Emergency Alerts**: Critical notifications for device failures

## Technical Implementation

### Automatic Feeding Schedule
```csharp
private void AutoFeedCat()
{
    foreach (var autoFeed in Collections.GetFeedTimes(Entities))
    {
        var feedTime = TimeSpan.Parse(autoFeed.Key.State ?? "00:00:00");
        var amount = Convert.ToInt32(autoFeed.Value.State);
        
        Scheduler.RunDaily(feedTime, () =>
        {
            if (Entities.InputBoolean.Pixelskipnextautofeed.IsOff())
            {
                FeedCat(amount);
                UpdateFeedingRecords(amount, isAutomatic: true);
            }
            Entities.InputBoolean.Pixelskipnextautofeed.TurnOff();
        });
    }
}
```

### Manual Feeding Control
```csharp
private void ButtonFeedCat()
{
    Entities.InputButton.Feedcat.StateChanges().Subscribe(_ =>
    {
        var amount = Convert.ToInt32(Entities.InputNumber.Pixelnumberofmanualfood.State ?? 0);
        FeedCat(amount);
        UpdateManualFeedingRecords(amount);
        UpdateLastManualFeedTime();
    });
}
```

### Food Consumption Tracking
```csharp
private void FeedCat(int amount)
{
    // Update daily total
    var dailyTotal = (Entities.InputNumber.Pixeltotalamountfeedday.State ?? 0) + amount;
    Entities.InputNumber.Pixeltotalamountfeedday.SetValue(dailyTotal);
    
    // Update lifetime total
    var lifetimeTotal = (Entities.InputNumber.Pixeltotalamountfeedalltime.State ?? 0) + amount;
    Entities.InputNumber.Pixeltotalamountfeedalltime.SetValue(lifetimeTotal);
    
    // Control physical feeder
    Services.Localtuya.SetDp(new LocaltuyaSetDpParameters
    {
        DeviceId = ConfigManager.GetValueFromConfig("ZedarDeviceId"),
        Dp = 3,
        Value = amount
    });
}
```

## Device Integration

### Zedar Smart Feeder
- **Protocol**: LocalTuya integration
- **Control**: Portion dispensing via data point 3
- **Configuration**: Device ID stored in config.json
- **Portions**: Configurable portion sizes (typically 5-15g per portion)

### PetSnowy Litter Box
- **Protocol**: LocalTuya integration
- **Monitoring**: Usage detection, cleaning cycles, error states
- **Controls**: Manual cleaning (DP 9), manual emptying (DP 109)
- **Status Tracking**: Pet usage, cleaning events, error conditions

### Water Fountain
- **Monitoring**: Online/offline status detection
- **Alerts**: Notifications when fountain becomes unavailable
- **Recovery**: Automatic status checking and alerts

## Configuration

### Required Entities

#### Feeding System
- **Manual Feed Button**: `input_button.feedcat`
- **Manual Amount**: `input_number.pixelnumberofmanualfood`
- **Daily Total**: `input_number.pixeltotalamountfeedday`
- **Lifetime Total**: `input_number.pixeltotalamountfeedalltime`
- **Last Manual Feed**: `input_datetime.pixellastmanualfeed`
- **Last Auto Feed**: `input_datetime.pixellastautomatedfeed`

#### Feeding Schedule
- **Feed Time 1**: `input_datetime.pixelfeedtime1`
- **Feed Amount 1**: `input_number.pixelfeedamount1`
- **Feed Time 2**: `input_datetime.pixelfeedtime2`
- **Feed Amount 2**: `input_number.pixelfeedamount2`
- (Additional feeding times as configured)

#### Control Buttons
- **Skip Next Feed**: `input_boolean.pixelskipnextautofeed`
- **Give Next Early**: `input_button.pixelgivenextfeedeary`
- **Clean Litter Box**: `input_button.cleanpetsnowy`
- **Empty Litter Box**: `input_button.emptypetsnowy`

#### Device Status
- **Litter Box Status**: `sensor.petsnowy_litterbox_status`
- **Litter Box Errors**: `sensor.petsnowy_litterbox_errors`
- **Water Fountain**: `switch.petsnowy_fountain_ison`

### Device Configuration (config.json)
```json
{
    "ZedarDeviceId": "your-zedar-feeder-device-id",
    "PetSnowyDeviceId": "your-petsnowy-device-id",
    "Discord": {
        "Pixel": "https://discord.com/api/webhooks/pixel-channel-url"
    }
}
```

## Usage Scenarios

### Daily Feeding Routine
1. **Morning Feed** (e.g., 7:00 AM): 8 portions automatic feeding
2. **Evening Feed** (e.g., 6:00 PM): 7 portions automatic feeding
3. **Manual Treats**: On-demand feeding via button
4. **Daily Reset**: Total consumption resets at 23:59

### Emergency Feeding
1. **Next Feed Early**: Button to give next scheduled meal immediately
2. **Skip Feed**: Option to skip next automatic feeding
3. **Manual Override**: Direct control of portion amounts

### Litter Box Maintenance
1. **Usage Detection**: Automatic monitoring of litter box usage
2. **Cleaning Triggers**: Automatic cleaning after each use
3. **Manual Controls**: Override cleaning and emptying functions
4. **Error Handling**: Alerts for maintenance issues

## Smart Features

### Intelligent Feeding
- **Food Level Monitoring**: Alerts when Zedar feeder food storage is low
- **Consumption Analysis**: Tracks eating patterns and amounts
- **Schedule Flexibility**: Easy adjustment of feeding times and amounts

### Litter Box Intelligence
- **Usage Patterns**: Monitors frequency and timing of litter box use
- **Proactive Cleaning**: Cleaning cycles based on usage
- **Health Monitoring**: Unusual usage patterns can indicate health issues

### Discord Integration
Rich notifications with feeding details:
```csharp
var discordNotification = new DiscordNotificationModel
{
    Embed = new Embed
    {
        Title = "Pixel heeft eten gehad",
        Thumbnail = new Location("https://cdn.pixabay.com/photo/cat-icon.png"),
        Fields = new[]
        {
            new Field { Name = "Eten gegeven", Value = $"{amount} porties" },
            new Field { Name = "Totaal vandaag", Value = $"{dailyTotal} porties" },
            new Field { Name = "Type", Value = isAutomatic ? "Automatisch" : "Handmatig" }
        }
    }
};
```

## Monitoring & Alerts

### Health Monitoring
- **Daily Consumption**: Tracks if Pixel is eating normal amounts
- **Feeding Schedule**: Ensures regular feeding times are maintained
- **Device Status**: Monitors all pet devices for proper operation

### Critical Alerts
- **Feeder Malfunction**: Immediate notification if feeding fails
- **Litter Box Offline**: Alert if litter box becomes unresponsive
- **Water Fountain Down**: Critical alert for water system failures
- **Food Level Low**: Warning when food storage needs refilling

### Status Reports
- **Daily Summary**: End-of-day consumption and activity report
- **Weekly Trends**: Feeding pattern analysis
- **Device Health**: Regular status updates for all pet devices



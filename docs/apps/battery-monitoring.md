# Battery Monitoring App

## Overview

The Battery Monitoring app continuously monitors the battery levels of all battery-powered devices in the home automation system. It provides proactive alerts when devices have low battery levels, ensuring that critical sensors and devices remain operational.

## Key Features

### Comprehensive Device Monitoring
- **Coverage**: Monitors all battery-powered devices registered in Home Assistant
- **Automatic Discovery**: Dynamically discovers and monitors new battery-powered devices
- **Device Types**: Sensors, buttons, smart locks, remotes, and other battery-powered devices

### Configurable Alert System
- **Threshold Configuration**: Customizable low battery warning level
- **Time-based Alerts**: Configurable check intervals to prevent notification spam
- **Smart Reset**: Automatically resets notifications when battery reaches 100%

### Intelligent Notification Management
- **Duplicate Prevention**: Avoids sending repeated notifications for the same device
- **Battery Level Tracking**: Monitors battery level changes over time
- **Notification Reset**: Clears alert history when battery is recharged

## Configuration

### App Configuration Settings
```json
{
    "Battery": {
        "WarningLevel": 20,
        "CheckInterval": 30
    }
}
```

### Parameters
- **WarningLevel**: Battery percentage threshold for low battery alerts (default: 20%)
- **CheckInterval**: Minutes between battery level checks (default: 30 minutes)

## Functionality

### Battery Level Monitoring
The app continuously monitors all devices with battery attributes:
- **Real-time Tracking**: Monitors battery level changes as they occur
- **Threshold Detection**: Triggers alerts when battery drops below configured level
- **Duration Tracking**: Ensures battery has been low for specified duration before alerting

### Notification System
- **Phone Notifications**: Sends alerts directly to Vincent's phone
- **Battery Dashboard**: Provides quick access to battery status dashboard
- **Alert Content**: Includes device name, current battery level, and dashboard link

### Automatic Reset Logic
- **Full Charge Detection**: Monitors for batteries reaching 100%
- **Notification History Reset**: Clears alert history when device is recharged
- **Fresh Start**: Allows new alerts once battery is recharged and depletes again

## Technical Implementation

### Triggers
- Battery level changes on all monitored devices
- Scheduled checks based on configured interval
- Battery level reaching 100% for reset logic

### Actions
- Phone notifications with vibration patterns
- Dashboard link generation
- Notification history management
- Alert reset processing

### Data Persistence
- **Notification History**: Stores when alerts were sent for each device
- **Battery State Tracking**: Maintains battery level history
- **Reset State Management**: Tracks when devices have been recharged

## Usage Examples

### Low Battery Alert
```
🔋 Low Battery Alert
Device: Front Door Sensor
Battery Level: 15%
Last Check: 14:30
Action: Replace battery soon
[View Battery Dashboard]
```

### Multiple Device Alert
```
🔋 Multiple Low Batteries
- Motion Sensor Hall: 18%
- Window Sensor Bedroom: 12%
- Smart Button Kitchen: 8%
[View Battery Dashboard]
```

## Monitored Device Types

### Sensors
- **Motion Sensors**: PIR sensors throughout the house
- **Door/Window Sensors**: Security sensors on entry points
- **Environmental Sensors**: Temperature, humidity, air quality sensors
- **Smart Buttons**: Wireless control buttons

### Smart Devices
- **Smart Locks**: Electronic door locks
- **Remote Controls**: TV and media device remotes
- **Wireless Switches**: Battery-powered light switches
- **Security Cameras**: Battery-powered security cameras

### Wearables & Mobile
- **Smart Watches**: Health and fitness trackers
- **Mobile Devices**: Phones and tablets
- **Fitness Devices**: Heart rate monitors and activity trackers

## Integration

### Home Assistant Integration
- **Entity Discovery**: Automatically discovers battery-powered entities
- **Attribute Reading**: Reads battery level from device attributes
- **State Monitoring**: Monitors battery state changes in real-time

### Notification Services
- **Phone Notifications**: Direct mobile notifications
- **Dashboard Integration**: Battery status dashboard access
- **Discord Logging**: System logs for battery monitoring activities

## Maintenance

### Regular Tasks
- **Battery Replacement**: Physical battery replacement when notified
- **Sensor Cleaning**: Clean sensors during battery replacement
- **Device Testing**: Verify device functionality after battery replacement

### Configuration Tuning
- **Threshold Adjustment**: Modify warning level based on device behavior
- **Interval Tuning**: Adjust check frequency based on notification preferences
- **Device Filtering**: Exclude specific devices from monitoring if needed


## Best Practices

### Battery Management
- **Proactive Replacement**: Replace batteries before they reach critical levels
- **Bulk Purchases**: Keep spare batteries for common device types
- **Seasonal Checks**: Perform battery audits during seasonal maintenance

### Device Optimization
- **Device Placement**: Ensure devices are within optimal signal range
- **Regular Cleaning**: Keep sensors clean for accurate readings
- **Firmware Updates**: Keep device firmware up to date for better battery life

The Battery Monitoring app ensures that all critical home automation devices remain powered and functional, providing peace of mind through proactive battery management and timely replacement notifications.
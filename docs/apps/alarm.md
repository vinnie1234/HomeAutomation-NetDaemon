# Alarm App

## Overview

The Alarm app is a comprehensive home monitoring and alerting system that provides real-time notifications for various conditions throughout the house. It serves as the central monitoring hub for security, environmental conditions, device status, and daily reminders.

## Key Features

### Temperature Monitoring
- **Threshold**: Alerts when any temperature sensor exceeds 25°C
- **Purpose**: Prevent overheating and detect potential fire hazards
- **Action**: Immediate phone notification with vibration

### Energy Consumption Alerts
- **Threshold**: Alerts when energy consumption exceeds 2000W for 10+ minutes
- **Purpose**: Detect high energy usage and potential electrical issues
- **Action**: Phone notification with energy dashboard link

### Motion Detection Security
- **Trigger**: Motion detected when away from home
- **Purpose**: Security monitoring and intrusion detection
- **Action**: Immediate notification for unexpected activity

### Daily Reminders
- **Garbage Collection**: Daily reminder at 22:00 for garbage collection
- **Purpose**: Ensure household maintenance tasks are completed
- **Action**: Simple reminder notification

### PetSnowy Litter Box Monitoring
- **Trigger**: Error states and maintenance alerts from smart litter box
- **Purpose**: Ensure pet hygiene equipment is functioning properly
- **Action**: Maintenance notifications and error alerts

### Home Assistant Connection Monitoring
- **Purpose**: Monitor connectivity to Home Assistant system
- **Action**: Immediate alerts if connection is lost

### Energy Price Alerts
- **Threshold**: Alerts when energy prices drop below -20 cents
- **Purpose**: Notify of negative energy prices for potential savings
- **Action**: Phone notification with energy price information

### Backup Verification
- **Monitor**: Local and OneDrive backup status
- **Purpose**: Ensure data backup systems are functioning
- **Action**: Alerts if backup systems fail

### Traffic Monitoring
- **Schedule**: Thursday and Friday mornings at 7:50 AM
- **Purpose**: Monitor work commute traffic conditions
- **Action**: Traffic updates for work planning

## Configuration

### Thresholds
- **Temperature**: 25°C maximum
- **Energy**: 2000W for 10+ minutes
- **Energy Price**: -20 cents or below
- **Motion**: Away state required

### Schedules
- **Daily Garbage Reminder**: 22:00 every day
- **Traffic Check**: 7:50 AM on Thursday and Friday

## Notifications

### Phone Notifications
- **Priority**: High with vibration patterns
- **Content**: Specific alert type and relevant data
- **Actions**: Quick access to relevant dashboards

### Discord Notifications
- **Channels**: Various channels based on alert type
- **Content**: Detailed alert information with context
- **Rich Embeds**: Formatted alerts with relevant data

## Technical Implementation

### Triggers
- Temperature sensor state changes
- Energy usage patterns and thresholds
- Motion detection events
- Scheduled cron jobs for daily reminders
- Device status changes

### Actions
- Phone notifications with custom vibration patterns
- Discord webhook notifications
- Dashboard link generation
- Emergency contact procedures

### Error Handling
- Graceful degradation when services unavailable
- Retry mechanisms for failed notifications
- Fallback notification methods

## Usage Examples

### Temperature Alert
```
🌡️ Temperature Alert
Kitchen sensor: 27°C
Threshold exceeded: 25°C
Time: 14:30
Action: Check ventilation
```

### Energy Consumption Alert
```
⚡ High Energy Usage
Current: 2,150W
Duration: 12 minutes
Threshold: 2,000W for 10+ minutes
Action: Check major appliances
```

### Motion Security Alert
```
🚨 Motion Detected
Location: Living room
Status: Away from home
Time: 15:45
Action: Review security cameras
```

## Integration

### Home Assistant Entities
- Temperature sensors throughout house
- Energy monitoring devices
- Motion sensors
- Away state boolean
- PetSnowy litter box sensors
- Backup status sensors

### External Services
- Discord webhook integration
- Phone notification service
- Energy price API
- Traffic monitoring service
- Backup verification services

## Maintenance

### Regular Checks
- Verify sensor connectivity
- Test notification delivery
- Review threshold settings
- Check backup systems


The Alarm app provides comprehensive monitoring and ensures that all critical home systems are functioning properly while keeping residents informed of any important conditions or maintenance needs.
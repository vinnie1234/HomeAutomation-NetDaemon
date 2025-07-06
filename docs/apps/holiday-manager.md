# Holiday Manager App

## Overview

The Holiday Manager app provides intelligent holiday detection and alarm coordination for the home automation system. It automatically detects holidays from the calendar system and adjusts alarm management routines accordingly, ensuring that wake-up schedules adapt appropriately for holidays and special occasions.

## Key Features

### Automatic Holiday Detection
- **Calendar Integration**: Connects with Home Assistant calendar system
- **Real-time Monitoring**: Continuously monitors holiday status changes
- **State Management**: Maintains current holiday state throughout the system
- **Daily Validation**: Performs midnight checks to ensure accurate holiday detection

### Alarm Coordination
- **Holiday-Aware Alarms**: Adjusts alarm behavior based on holiday status
- **Work Day Logic**: Distinguishes between work days and holidays
- **Reminder System**: Provides appropriate alarm management reminders
- **Schedule Adaptation**: Modifies automation schedules for holiday periods

### State Change Notifications
- **Holiday Transitions**: Alerts when holiday state changes
- **Alarm Reminders**: Context-aware alarm management notifications
- **System Coordination**: Integrates with other automation systems
- **Status Updates**: Provides clear holiday status information

## Technical Implementation

### Holiday Detection Logic
- **Calendar Monitoring**: Monitors Home Assistant calendar entities for holiday events
- **Boolean State Management**: Maintains holiday boolean state in Home Assistant
- **Midnight Validation**: Scheduled daily check at 00:00 to verify holiday status
- **State Persistence**: Maintains holiday state across system restarts

### Triggers
- **Holiday State Changes**: Boolean state transitions for holiday status
- **Calendar Events**: Detection of holiday events in connected calendars
- **Scheduled Checks**: Daily midnight validation of holiday status
- **Manual Overrides**: Support for manual holiday state changes

### Actions
- **State Updates**: Updates holiday boolean state in Home Assistant
- **Alarm Reminders**: Sends appropriate alarm management notifications
- **System Notifications**: Logs holiday state changes for other systems
- **Integration Coordination**: Notifies connected automation systems

## Holiday Management

### Holiday Detection Sources
- **Personal Calendar**: Individual calendar events marked as holidays
- **National Holidays**: Official national holiday calendar integration
- **Work Calendar**: Company-specific holiday and vacation schedules
- **Custom Events**: User-defined special occasions and events

### Holiday Types
- **National Holidays**: Official government holidays
- **Personal Holidays**: Individual vacation days and personal time off
- **Religious Holidays**: Faith-based observances and celebrations
- **Special Events**: Birthdays, anniversaries, and family occasions

### State Management
- **Holiday Boolean**: Central holiday state indicator
- **Duration Tracking**: Monitors holiday period start and end
- **Multi-day Events**: Handles extended holiday periods
- **Transition Management**: Smooth transitions between holiday and work states

## Alarm Integration

### Holiday Alarm Behavior
- **No Alarms**: Disables work-related alarms during holidays
- **Flexible Wake-up**: Allows natural wake-up patterns
- **Optional Reminders**: Provides gentle reminders if requested
- **Custom Schedules**: Supports holiday-specific alarm schedules

### Work Day vs. Holiday Logic
```csharp
if (IsHoliday)
{
    // Holiday mode: relaxed scheduling
    DisableWorkAlarms();
    EnableOptionalReminders();
    SetFlexibleSchedule();
}
else
{
    // Work mode: standard alarm routines
    EnableWorkAlarms();
    SetStandardSchedule();
    ActivateWeekdayRoutines();
}
```

### Reminder System
- **Context-Aware Notifications**: Different messages for holidays vs. work days
- **Gentle Reminders**: Softer notification approach during holidays
- **Optional Alerts**: User-configurable holiday notifications
- **Schedule Previews**: Upcoming holiday and work day previews

## Integration

### Home Assistant Entities
- **Calendar Entities**: Holiday and vacation calendar monitoring
- **Holiday Boolean**: Central holiday state indicator
- **Alarm Systems**: Integration with alarm and wake-up routines
- **Notification Services**: Alert and reminder delivery systems

### Connected Systems
- **Alarm Manager**: Coordinates with wake-up and alarm systems
- **House State Manager**: Influences daily routine state management
- **Sleep Manager**: Coordinates with sleep and wake-up routines
- **Work Schedule**: Integrates with work and personal scheduling

## Usage Examples

### Holiday Detection
```
🎉 Holiday Detected
Event: Christmas Day
Date: December 25
Duration: All day
Actions: Alarms disabled, relaxed schedule
Source: National holiday calendar
```

### Holiday Reminder
```
🔔 Holiday Alarm Reminder
Status: Holiday mode active
Alarms: Disabled for today
Next work day: December 26
Action: Enjoy your holiday!
```

### Holiday Transition
```
📅 Holiday Status Change
Previous: Work day
Current: Holiday
Event: New Year's Day
Duration: Today only
Adjustments: Schedule modified
```

### End of Holiday
```
📅 Back to Work Tomorrow
Holiday: Martin Luther King Day (ending)
Next: Regular work schedule
Alarms: Will resume tomorrow
Reminder: Check alarm settings
```

## Configuration

### Calendar Settings
```json
{
    "HolidayManager": {
        "CalendarEntities": [
            "calendar.holidays",
            "calendar.personal_vacation",
            "calendar.work_holidays"
        ],
        "DefaultReminders": true,
        "AlarmOverride": "disable",
        "NotificationLevel": "normal"
    }
}
```

### Holiday Types Configuration
- **National Holidays**: Automatic detection from national calendar
- **Personal Vacation**: User-configured vacation periods
- **Work Holidays**: Company-specific holiday schedules
- **Family Events**: Personal celebrations and special occasions

## Maintenance

### Regular Tasks
- **Calendar Sync**: Verify calendar integration is functioning
- **Holiday Accuracy**: Check holiday detection accuracy
- **Alarm Testing**: Test alarm coordination during holidays
- **Schedule Review**: Review and update holiday schedules


### Updates and Adjustments
- **Calendar Updates**: Add new holiday calendars as needed
- **Schedule Tuning**: Adjust reminder timing and content
- **Integration Updates**: Update connections to other systems
- **User Preferences**: Modify holiday behavior based on feedback

## Best Practices

### Calendar Management
- **Consistent Formatting**: Use standard holiday event formats
- **Advance Planning**: Enter holidays well in advance
- **Multiple Sources**: Use multiple calendar sources for completeness
- **Regular Updates**: Keep holiday calendars current and accurate

### System Integration
- **Graceful Degradation**: Maintain functionality if calendar fails
- **User Override**: Always allow manual holiday state changes
- **Clear Communication**: Provide clear holiday status information
- **Flexible Configuration**: Allow customization of holiday behavior

### User Experience
- **Predictable Behavior**: Maintain consistent holiday handling
- **Clear Notifications**: Provide informative holiday status updates
- **Easy Control**: Simple manual override capabilities
- **Helpful Reminders**: Timely and relevant holiday reminders

The Holiday Manager app ensures that the home automation system intelligently adapts to holidays and special occasions, providing appropriate schedule adjustments while maintaining user control and system reliability.
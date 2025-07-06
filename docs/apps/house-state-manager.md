# House State Manager App

## Overview

The House State Manager app serves as the central orchestrator for the home's operational states throughout the day. It manages four distinct house states (Morning, Day, Evening, Night) and coordinates automated transitions based on sun position, time schedules, and manual scene activations.

## Key Features

### Four House States
- **Morning**: Early morning routines and gentle lighting
- **Day**: Full brightness and active home operations
- **Evening**: Relaxed atmosphere with warm lighting
- **Night**: Minimal lighting and sleep-friendly environment

### Automatic State Transitions
- **Sun-based Transitions**: Uses sun position for natural lighting transitions
- **Time-based Schedules**: Scheduled transitions for consistent daily routines
- **Scene-based Changes**: Manual state changes through scene activation
- **Sleep Integration**: Coordinates with sleep and alarm states

### Working State Management
- **Work Day Detection**: Manages different schedules for work vs. home days
- **Schedule Adaptation**: Adjusts transition times based on work status
- **Weekend Flexibility**: Different patterns for weekends and holidays

## House States Description

### Morning State
**Active Period**: Sunrise to mid-morning
- **Lighting**: Gradual brightness increase, warm colors
- **Purpose**: Gentle wake-up and morning routine support
- **Triggers**: Sun elevation, alarm states, manual activation

### Day State
**Active Period**: Mid-morning to late afternoon
- **Lighting**: Full brightness, natural white light
- **Purpose**: Active daytime activities and maximum visibility
- **Triggers**: Sun elevation, scheduled time, scene activation

### Evening State
**Active Period**: Late afternoon to bedtime
- **Lighting**: Warm, comfortable lighting for relaxation
- **Purpose**: Evening activities and wind-down routines
- **Triggers**: Sun elevation, scheduled time, scene activation

### Night State
**Active Period**: Bedtime to sunrise
- **Lighting**: Minimal lighting, red/warm colors
- **Purpose**: Sleep-friendly environment and nighttime navigation
- **Triggers**: Sleep state, scheduled time, scene activation

## Configuration

### Schedule Settings
```json
{
    "HouseStates": {
        "Weekday": {
            "Morning": "06:00",
            "Day": "08:00",
            "Evening": "18:00",
            "Night": "22:00"
        },
        "Weekend": {
            "Morning": "08:00",
            "Day": "09:00",
            "Evening": "19:00",
            "Night": "23:00"
        }
    }
}
```

### Working State Configuration
- **Work Days**: Monday through Friday
- **Home Days**: Weekends and holidays
- **Special Schedules**: Holiday and vacation modes

## Technical Implementation

### State Management
- **Input Select Control**: Uses Home Assistant input_select entity
- **State Persistence**: Maintains current state across system restarts
- **Transition Logging**: Logs all state changes for debugging

### Triggers
- **Sun Position**: Solar elevation and azimuth calculations
- **Scheduled Times**: Cron-based time triggers
- **Scene Activation**: Manual scene selection triggers
- **Sleep State**: Integration with sleep management system
- **Alarm States**: Coordination with alarm and wake-up routines

### Actions
- **State Updates**: Updates house state input_select
- **Working State Changes**: Manages work/home status
- **Lighting Coordination**: Coordinates with lighting systems
- **System Notifications**: Logs state changes

## State Transition Logic

### Automatic Transitions
1. **Sun-based**: Primary method using solar calculations
2. **Time-based**: Backup method for cloudy days or sensor failures
3. **Manual Override**: Scene activation can force state changes
4. **Sleep Integration**: Sleep state forces Night mode

### Transition Priorities
1. **Sleep State**: Highest priority - always forces Night mode
2. **Manual Scenes**: High priority - user intent
3. **Sun Position**: Medium priority - natural transitions
4. **Scheduled Times**: Low priority - fallback method

### Working State Logic
- **Work Days**: Modified schedule for work routines
- **Home Days**: Relaxed schedule for leisure days
- **Holiday Mode**: Special schedules for holidays and vacations

## Integration

### Home Assistant Entities
- **Sun Entity**: Solar elevation and azimuth data
- **Input Select**: House state selector
- **Scene Entities**: Manual scene activation
- **Sleep Boolean**: Sleep state integration
- **Alarm States**: Wake-up and alarm coordination

### Connected Systems
- **Lighting System**: Coordinated lighting scenes
- **Media Controls**: Audio/video system integration
- **Climate Control**: Temperature and HVAC coordination
- **Security System**: Armed/disarmed state coordination

## Usage Examples

### Morning Transition
```
🌅 House State: Morning
Time: 06:30
Trigger: Sun elevation
Previous: Night
Actions: Gradual lighting increase
```

### Manual Scene Override
```
🎬 House State: Evening
Time: 15:00
Trigger: Movie scene activated
Previous: Day
Actions: Dim lights, close blinds
```

### Sleep Integration
```
🌙 House State: Night
Time: 21:45
Trigger: Sleep mode activated
Previous: Evening
Actions: Minimal lighting, quiet mode
```

## Lighting Coordination

### State-specific Lighting
- **Morning**: Warm white, 30-60% brightness
- **Day**: Cool white, 80-100% brightness
- **Evening**: Warm white, 40-70% brightness
- **Night**: Red/amber, 5-20% brightness

### Transition Behavior
- **Gradual Changes**: Smooth transitions over 5-10 minutes
- **Room-specific**: Different behavior per room
- **Activity-aware**: Considers current activities

## Maintenance

### Regular Checks
- **State Accuracy**: Verify states match actual conditions
- **Transition Timing**: Adjust schedules seasonally
- **Sun Data**: Verify solar calculations are accurate
- **Integration Health**: Check connected system responses


### Seasonal Adjustments
- **Daylight Saving**: Automatic time adjustment
- **Sun Position**: Seasonal variation accommodation
- **Schedule Tuning**: Adjust for changing daylight hours
- **Holiday Schedules**: Special event accommodations

## Best Practices

### Schedule Management
- **Consistent Routines**: Maintain regular daily patterns
- **Flexibility**: Allow for weekend and holiday variations
- **User Preferences**: Adapt to household preferences
- **Seasonal Updates**: Adjust for changing seasons

### System Integration
- **Coordinated Changes**: Ensure all systems respond to state changes
- **Fallback Options**: Maintain manual control capabilities
- **Error Handling**: Graceful degradation when systems fail
- **User Override**: Always allow manual state changes

The House State Manager app provides the foundation for intelligent home automation by coordinating all systems according to natural daily rhythms and user preferences, creating a seamless and comfortable living environment.
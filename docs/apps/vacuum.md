# Vacuum App

## Overview

The Vacuum app provides intelligent control and automation for the robot vacuum cleaner system. It enables zone-based cleaning, automatic cleaning triggers based on household activities, and smart scheduling that respects sleep and activity patterns.

## Key Features

### Zone-Based Cleaning
- **Targeted Cleaning**: Clean specific areas on demand
- **Room Mapping**: Predefined zones for different rooms and areas
- **Button Integration**: Manual control via physical and virtual buttons
- **Custom Regions**: Configurable cleaning zones with specific region IDs

### Automatic Cleaning Triggers
- **Litter Box Integration**: Automatic cleaning after cat litter box use
- **Activity-Based**: Triggered by specific household activities
- **Smart Timing**: Avoids cleaning during sleep or inappropriate times
- **Skip Options**: Configurable override for automatic cleaning

### Sleep State Awareness
- **Sleep Detection**: Integrates with sleep management system
- **Quiet Hours**: Prevents noisy cleaning during sleep
- **Schedule Adaptation**: Adjusts cleaning times based on sleep patterns
- **Override Protection**: Prevents accidental activation during night

## Configuration

### Room Mappings
```json
{
    "VacuumZones": {
        "Kitchen": {
            "ButtonEntity": "input_button.vacuum_kitchen",
            "RegionIds": ["18"]
        },
        "LivingRoom": {
            "ButtonEntity": "input_button.vacuum_living_room", 
            "RegionIds": ["16", "17"]
        },
        "Bedroom": {
            "ButtonEntity": "input_button.vacuum_bedroom",
            "RegionIds": ["19"]
        },
        "Hall": {
            "ButtonEntity": "input_button.vacuum_hall",
            "RegionIds": ["20"]
        },
        "LitterBox": {
            "ButtonEntity": "input_button.vacuum_litter_box",
            "RegionIds": ["21"]
        }
    }
}
```

### Skip Configuration
- **Global Skip**: Disable all automatic cleaning
- **Zone-specific Skip**: Disable cleaning for specific areas
- **Time-based Skip**: Temporary disable during specific periods

## Technical Implementation

### Zone Control System
- **Button Triggers**: Physical and virtual button press detection
- **Region Mapping**: Translates rooms to vacuum map regions
- **Command Generation**: Creates zone-specific cleaning commands
- **Status Monitoring**: Tracks cleaning progress and completion

### Automatic Triggers
- **Litter Box Monitoring**: Detects when litter box cleaning cycle completes
- **Delay Logic**: Waits appropriate time before starting vacuum
- **Conflict Resolution**: Prevents multiple simultaneous cleaning requests
- **Error Handling**: Manages failed cleaning attempts

### Sleep Integration
- **Sleep State Detection**: Monitors current sleep state
- **Time Restrictions**: Prevents cleaning during quiet hours
- **Schedule Coordination**: Coordinates with daily routines
- **Override Prevention**: Blocks inappropriate automatic activation

## Cleaning Zones

### Kitchen Zone
- **Region ID**: 18
- **Coverage**: Kitchen floor area, under appliances
- **Frequency**: High-traffic area, frequent cleaning
- **Triggers**: Manual button, spill detection

### Living Room Zone
- **Region IDs**: 16, 17
- **Coverage**: Main seating area, entertainment center
- **Frequency**: Daily cleaning recommended
- **Triggers**: Manual button, scheduled cleaning

### Bedroom Zone
- **Region ID**: 19
- **Coverage**: Bedroom floor, under bed
- **Frequency**: Weekly or as needed
- **Triggers**: Manual button only (respects sleep)

### Hall Zone
- **Region ID**: 20
- **Coverage**: Hallway, transition areas
- **Frequency**: High-traffic, frequent cleaning
- **Triggers**: Manual button, motion-based

### Litter Box Zone
- **Region ID**: 21
- **Coverage**: Cat litter area, surrounding floor
- **Frequency**: After each litter box cleaning
- **Triggers**: Automatic after litter box cycle

## Automation Logic

### Litter Box Cleaning Sequence
1. **Detection**: PetSnowy litter box completes cleaning cycle
2. **Delay**: Wait for dust and debris to settle
3. **Sleep Check**: Verify not in sleep mode
4. **Skip Check**: Confirm automatic cleaning is enabled
5. **Execution**: Start vacuum cleaning for litter box area
6. **Monitoring**: Track cleaning completion

### Manual Zone Cleaning
1. **Button Press**: User activates zone cleaning button
2. **Zone Lookup**: Map button to specific region IDs
3. **Status Check**: Verify vacuum is available
4. **Command Send**: Execute zone-specific cleaning command
5. **Feedback**: Provide cleaning status updates

### Conflict Resolution
- **Multiple Requests**: Queue cleaning requests appropriately
- **Busy Vacuum**: Handle vacuum already in use
- **Low Battery**: Defer cleaning until charged
- **Error States**: Manage vacuum errors and recovery

## Integration

### Home Assistant Entities
- **Vacuum Entity**: Robot vacuum device control
- **Button Entities**: Zone cleaning trigger buttons
- **Sleep Boolean**: Sleep state monitoring
- **Skip Booleans**: Override controls for automatic cleaning
- **PetSnowy Sensors**: Litter box status monitoring

### Smart Home Devices
- **Robot Vacuum**: Primary cleaning device
- **PetSnowy Litter Box**: Trigger for automatic cleaning
- **Motion Sensors**: Activity detection for timing
- **Sleep System**: Quiet hours coordination

## Usage Examples

### Manual Kitchen Cleaning
```
🧹 Vacuum: Kitchen Cleaning
Zone: Kitchen (Region 18)
Trigger: Manual button press
Duration: ~15 minutes
Status: Cleaning in progress
```

### Automatic Litter Box Cleaning
```
🧹 Vacuum: Auto Litter Box Clean
Zone: Litter Box (Region 21)
Trigger: PetSnowy cleaning completed
Delay: 5 minutes (dust settling)
Status: Starting cleaning cycle
```

### Sleep Mode Prevention
```
🧹 Vacuum: Cleaning Skipped
Zone: Living Room
Reason: Sleep mode active
Time: 23:45
Action: Deferred until morning
```

## Maintenance

### Regular Tasks
- **Vacuum Maintenance**: Empty dustbin, clean filters
- **Map Updates**: Update zone mappings if room layout changes
- **Button Testing**: Verify all zone buttons function correctly
- **Integration Check**: Test automatic triggers work properly


### Optimization
- **Schedule Tuning**: Adjust automatic cleaning timing
- **Zone Refinement**: Optimize zone boundaries for efficiency
- **Frequency Adjustment**: Modify cleaning frequency per zone
- **Skip Logic**: Fine-tune skip conditions for user preferences

## Best Practices

### Zone Management
- **Regular Mapping**: Update zone maps when furniture moves
- **Efficient Zones**: Design zones for optimal cleaning paths
- **Overlap Prevention**: Avoid overlapping zone boundaries
- **Access Planning**: Ensure vacuum can access all zone areas

### Automation Setup
- **Smart Timing**: Schedule cleaning during optimal times
- **Activity Coordination**: Coordinate with household activities
- **Maintenance Windows**: Plan cleaning around maintenance schedules
- **Override Options**: Always provide manual override capabilities

### User Experience
- **Predictable Behavior**: Maintain consistent cleaning patterns
- **Feedback Systems**: Provide clear status and completion notifications
- **Error Recovery**: Implement graceful error handling and recovery
- **Customization Options**: Allow user preferences and adjustments

The Vacuum app provides comprehensive automated cleaning management that integrates seamlessly with household routines while maintaining user control and system reliability.
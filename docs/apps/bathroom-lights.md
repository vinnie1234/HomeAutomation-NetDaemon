# Bathroom Lights App

## Overview

The Bathroom Lights app (`BathRoomLights.cs`) provides comprehensive bathroom automation including motion-activated lighting, shower mode with music integration, smart toothbrush automation, and Hue switch control. It creates an intelligent bathroom environment that adapts to different activities and times of day.

## Key Features

### 🚶‍♂️ Motion-Based Lighting
- **Dual Light Control**: Controls both ceiling light (`plafond_badkamer`) and mirror light (`badkamer_spiegel`)
- **Adaptive Brightness**: 5% brightness when sleeping, 100% when awake
- **Smooth Transitions**: 2-second transition times for comfortable lighting changes
- **Sleep State Awareness**: Automatically detects sleep state for appropriate brightness
- **Configurable Timeouts**: Separate day and night timeout periods via input numbers

### 🚿 Comprehensive Shower Mode
- **Music Integration**: Automatically starts Spotify via Google Home
- **Volume Control**: Sets audio volume to 40% for shower ambiance
- **Environmental Control**: Closes roller blinds for privacy
- **Extended Lighting**: 1-hour timeout with warning notifications
- **Discord Notifications**: Alerts when shower mode times out
- **Full Brightness**: Ensures optimal lighting during shower activities

### 🎵 Smart Toothbrush Automation
- **Automatic Music Start**: Detects toothbrush activation and starts music at 15% volume
- **Timer Integration**: Monitors brushing duration and patterns
- **Auto-Stop**: Stops music after 30 seconds of toothbrush idle time
- **Health Routine Support**: Encourages proper oral hygiene timing

### 🔘 Advanced Hue Switch Control
- **4-Button Control**: Complete Hue switch integration with multiple functions
- **Button 1**: Toggle shower mode on/off
- **Button 2**: Direct light control toggle
- **Button 3**: [Custom function]
- **Button 4**: [Custom function]
- **Physical Override**: Always provides manual control regardless of automation state

### 🌙 Sleep & Time Awareness
- **Night Mode Detection**: Uses house mode and sleep state for intelligent behavior
- **Configurable Timers**: Separate timeout settings for day (`input_number.bathroom_light_daytime`) and night (`input_number.bathroom_light_nighttime`)
- **Sleep State Integration**: Coordinates with Sleep Manager for optimal brightness
- **House Mode Coordination**: Integrates with central house state management

## Technical Implementation

### Motion Detection Logic
```csharp
private void InitializeLights()
{
    Entities.BinarySensor.BadkamerMotion
        .StateChanges()
        .Where(x => x.New.IsOn())
        .Subscribe(_ => TurnOnLights());

    // Auto turn off after 2 minutes (normal) or 60 minutes (shower mode)
    Entities.BinarySensor.BadkamerMotion
        .StateChanges()
        .Where(x => x.New.IsOff())
        .Subscribe(_ => ScheduleTurnOff());
}
```

### Shower Mode Detection
```csharp
private bool IsDouching => Entities.InputBoolean.Douchen.IsOn();

private void ScheduleTurnOff()
{
    var delay = IsDouching ? TimeSpan.FromHours(1) : TimeSpan.FromMinutes(2);
    
    Scheduler.Schedule(delay, () =>
    {
        if (!DisableLightAutomations && Entities.BinarySensor.BadkamerMotion.IsOff())
        {
            Entities.Light.Badkamer.TurnOff();
        }
    });
}
```

### Night Mode Lighting
```csharp
private bool IsNighttime => Entities.InputSelect.Housemodeselect.State == "Night";

private void TurnOnLights()
{
    if (DisableLightAutomations) return;
    
    if (IsNighttime)
    {
        // Dim lighting for nighttime
        Entities.Light.Badkamer.TurnOn(brightnessPct: 30);
    }
    else
    {
        // Full brightness for daytime
        Entities.Light.Badkamer.TurnOn(brightnessPct: 100);
    }
}
```

## Configuration

### Required Entities
- **Motion Sensor**: `binary_sensor.badkamer_motion`
- **Bathroom Light**: `light.badkamer`
- **Shower Mode**: `input_boolean.douchen`
- **Disable Automation**: `input_boolean.disablelightautomationbathroom`
- **House Mode**: `input_select.housemodeselect`
- **Toothbrush Sensor**: `sensor.toothbrush_active` (or similar)

### Timing Configuration
- **Normal Timeout**: 2 minutes after motion stops
- **Shower Timeout**: 60 minutes when shower mode is active
- **Night Brightness**: 30% of maximum brightness
- **Day Brightness**: 100% of maximum brightness

## Behavior Scenarios

### Scenario 1: Normal Bathroom Use
1. **Motion Detected** → Lights turn on at full brightness (or 30% if nighttime)
2. **Motion Stops** → 2-minute countdown begins
3. **No Motion for 2 Minutes** → Lights turn off automatically
4. **Motion During Countdown** → Countdown resets

### Scenario 2: Shower Mode
1. **Shower Mode Activated** → `input_boolean.douchen` turns on
2. **Motion Detected** → Lights turn on at appropriate brightness
3. **Motion Stops** → 60-minute countdown begins (instead of 2 minutes)
4. **No Motion for 60 Minutes** → Lights turn off automatically

### Scenario 3: Manual Override
1. **Hue Button Pressed** → Toggles shower mode on/off
2. **Disable Automation** → All automatic behaviors disabled
3. **Manual Light Control** → Direct light control bypasses automation

### Scenario 4: Toothbrush Routine
1. **Toothbrush Activated** → Music starts playing automatically
2. **Oral Hygiene Routine** → Music continues during brushing
3. **Toothbrush Deactivated** → Music stops when routine complete

## Smart Features

### Adaptive Behavior
- **Context Awareness**: Different behavior based on house mode (day/night)
- **Usage Patterns**: Extended lighting during shower activities
- **Energy Efficiency**: Automatic shutoff prevents lights staying on indefinitely

### Integration Points
- **House State Manager**: Coordinates with overall house state system
- **Away Manager**: Respects away mode settings
- **Sleep Manager**: Adjusts behavior during sleep hours

### Safety Features
- **Motion Safety**: Lights won't turn off if motion is still detected
- **Manual Override**: Physical controls always work regardless of automation state
- **Disable Switch**: Easy way to disable automation when needed



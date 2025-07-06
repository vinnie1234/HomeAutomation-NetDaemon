# Hall Light Automation App

## Overview

The Hall Light Automation app (`HallLightOnMovement.cs`) manages intelligent lighting control for the hallway based on motion detection, sleep state, and manual controls.

## Key Features

### 🚶‍♂️ Motion-Based Lighting
- **Automatic Activation**: Lights turn on when motion is detected in the hallway
- **Sleep-Aware Brightness**: Different brightness levels based on sleep state
  - **Awake**: 100% brightness for full visibility
  - **Sleeping**: 5% brightness to avoid disturbing sleep
- **Configurable Timing**: Adjustable timeout periods for different states

### 😴 Sleep State Integration
- **Sleep Detection**: Monitors Vincent's sleep state via PersonModel
- **Adaptive Behavior**: Automatically adjusts brightness and timeout based on sleep status
- **Night Mode**: Minimal lighting during sleep hours to prevent sleep disruption

### ⏰ Smart Timing Controls
- **Day Time**: Configurable timeout via `input_number.halllightdaytime`
- **Night Time**: Separate timeout for sleep hours via `input_number.halllightnighttime`
- **Automatic Shutoff**: Lights turn off after configured period with no motion

### 🔘 Manual Override Controls
- **Hue Button Integration**: Physical Hue button for manual control
- **Away State Toggle**: Button can toggle away state when Vincent is away
- **Brightness Control**: Manual brightness adjustment via button presses
- **Disable Override**: Complete automation disable via `input_boolean.disablelightautomationhall`

## Technical Implementation

### Motion Detection Logic
```csharp
private void InitializeLights()
{
    // Turn on lights when motion detected
    Entities.BinarySensor.GangMotion
        .StateChanges()
        .Where(x => x.New.IsOn() && !DisableLightAutomations)
        .Subscribe(_ => ChangeLight(true, GetBrightness()));

    // Turn off lights after timeout period
    Entities.BinarySensor.GangMotion
        .StateChanges()
        .WhenStateIsFor(x => x.IsOff(), TimeSpan.FromMinutes(GetStateTime()), Scheduler)
        .Where(_ => !DisableLightAutomations)
        .Subscribe(_ => ChangeLight(false));
}
```

### Sleep-Aware Brightness Control
```csharp
private int GetBrightness()
{
    return Vincent.IsSleeping switch
    {
        true => 5,      // Very dim during sleep
        false => 100    // Full brightness when awake
    };
}
```

### Adaptive Timing System
```csharp
private int GetStateTime()
{
    return Vincent.IsSleeping switch
    {
        true => Convert.ToInt32(Entities.InputNumber.Halllightnighttime.State),
        false => Convert.ToInt32(Entities.InputNumber.Halllightdaytime.State)
    };
}
```

### Hue Button Controls
```csharp
private void OverwriteSwitch(EventModel eventModel)
{
    if (eventModel.DeviceId == hueButtonDeviceId)
    {
        switch (eventModel.Type)
        {
            case "initial_press":
                // Toggle away state if Vincent is away
                if (!Vincent.IsHome)
                    Entities.InputBoolean.Away.Toggle();
                break;
                
            case "repeat":
                // Increase brightness
                IncreaseBrightness();
                break;
        }
    }
}
```

## Configuration

### Required Entities
- **Motion Sensor**: `binary_sensor.gang_motion`
- **Hall Light**: `light.gang` (or appropriate hallway light entity)
- **Day Timeout**: `input_number.halllightdaytime` (minutes)
- **Night Timeout**: `input_number.halllightnighttime` (minutes)
- **Disable Automation**: `input_boolean.disablelightautomationhall`
- **Away State**: `input_boolean.away`

### Person Model Integration
- **Vincent's Sleep State**: Accessed via `Vincent.IsSleeping` property
- **Vincent's Home State**: Accessed via `Vincent.IsHome` property
- **Automatic Updates**: PersonModel automatically tracks state changes

### Timing Configuration Examples
```yaml
# Example Home Assistant configuration
input_number:
  halllightdaytime:
    name: "Hall Light Day Timeout"
    min: 1
    max: 60
    step: 1
    unit_of_measurement: "minutes"
    initial: 5
    
  halllightnighttime:
    name: "Hall Light Night Timeout"
    min: 1
    max: 30
    step: 1
    unit_of_measurement: "minutes"
    initial: 2
```

## Behavior Scenarios

### Scenario 1: Daytime Motion
1. **Motion Detected** → Lights turn on at 100% brightness
2. **Motion Continues** → Lights remain on
3. **Motion Stops** → Timeout countdown begins (e.g., 5 minutes)
4. **No Motion for Timeout** → Lights turn off automatically
5. **Motion During Countdown** → Countdown resets

### Scenario 2: Nighttime Motion (Sleep Mode)
1. **Motion Detected** → Lights turn on at 5% brightness (dim)
2. **Motion Stops** → Shorter timeout countdown begins (e.g., 2 minutes)
3. **No Motion for Timeout** → Lights turn off automatically
4. **Minimal Disruption** → Very low brightness prevents sleep disturbance

### Scenario 3: Manual Override
1. **Hue Button Press** → If Vincent is away, toggles away state
2. **Button Repeat** → Increases light brightness manually
3. **Disable Switch** → Automation completely disabled, manual control only

### Scenario 4: Away State Management
1. **Vincent Away** → Different button behavior activates
2. **Button Press** → Toggles away state for testing or override
3. **Security Integration** → Coordinates with away manager automation

## Smart Features

### Adaptive Behavior
- **Context Awareness**: Behavior changes based on sleep state
- **Energy Efficiency**: Shorter timeouts and dimmer lighting during sleep
- **User Experience**: Bright lighting when needed, minimal disruption when sleeping

### Integration Points
- **Sleep Manager**: Coordinates with sleep state detection
- **Away Manager**: Integrates with home/away state management
- **House State Manager**: Respects overall house operational modes

### Safety Features
- **Motion Safety**: Lights won't turn off if motion is still detected
- **Manual Override**: Physical controls always work regardless of automation
- **Disable Switch**: Easy way to completely disable automation

## Advanced Controls

### Brightness Management
The system supports dynamic brightness control:
```csharp
private void IncreaseBrightness()
{
    var currentBrightness = Entities.Light.Gang.Attributes?.Brightness ?? 0;
    var newBrightness = Math.Min(255, currentBrightness + 51); // Increase by ~20%
    
    Entities.Light.Gang.TurnOn(brightness: newBrightness);
}
```

### Multiple Button Functions
Different button press types trigger different actions:
- **Initial Press**: Primary function (away toggle when away)
- **Repeat/Hold**: Secondary function (brightness control)
- **Double Press**: Additional functions can be added

## Troubleshooting

### Common Issues

#### Lights Not Turning On
1. **Check Motion Sensor**: Verify `binary_sensor.gang_motion` is working
2. **Check Disable Switch**: Ensure `input_boolean.disablelightautomationhall` is off
3. **Entity Names**: Confirm all entity IDs match your Home Assistant setup
4. **PersonModel**: Verify Vincent's sleep state is correctly detected

#### Wrong Brightness Level
1. **Sleep State**: Check if Vincent.IsSleeping is correctly detected
2. **Manual Adjustment**: Use Hue button or manual control to adjust
3. **Entity State**: Verify PersonModel is receiving correct sleep state updates

#### Lights Not Turning Off
1. **Motion Status**: Verify motion sensor shows "off" state
2. **Timeout Settings**: Check day/night timeout configuration values
3. **Disable State**: Ensure automation isn't disabled
4. **Manual Override**: Use physical switch if automation fails

#### Button Not Working
1. **Device ID**: Verify Hue button device ID matches configuration
2. **Event Detection**: Check that "hue_event" events are being received
3. **Network**: Ensure Hue button is connected to Hue bridge
4. **Battery**: Check Hue button battery level

### Debug Information
Enable debug logging for detailed automation tracking:
```json
{
    "Logging": {
        "LogLevel": {
            "Automation.apps.Rooms.Hall.HallLightOnMovement": "Debug"
        }
    }
}
```

Debug logs will show:
- Motion detection events
- Sleep state changes
- Brightness calculations
- Button press events
- Timeout scheduling

## Configuration Examples

### Typical Timeout Settings
- **Day Timeout**: 5-10 minutes (longer for convenience)
- **Night Timeout**: 1-3 minutes (shorter to minimize sleep disruption)
- **Brightness Levels**: 100% day, 5% night (dramatic difference for sleep protection)

### Home Assistant Integration
```yaml
automation:
  - alias: "Hall Light Manual Override"
    trigger:
      - platform: state
        entity_id: input_boolean.disablelightautomationhall
        to: 'on'
    action:
      - service: light.turn_off
        entity_id: light.gang
```
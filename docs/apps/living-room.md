# Living Room Automation Apps

## Overview

The Living Room contains multiple automation apps that work together to create an intelligent entertainment and lighting environment: `LivingRoomLights.cs`, `Tv.cs`, and `Gaming.cs`.

## Living Room Lights App

### Key Features

#### 🔘 Hue Wall Switch Control
- **Wall Switch Integration**: Physical Hue button controls all living room lighting
- **Scene Coordination**: Activates coordinated lighting scenes across multiple lights
- **Smart Sequencing**: Lights turn on/off in a specific sequence for optimal effect

#### 💡 Multi-Light Coordination
- **HueFilamentBulb1**: Primary accent lighting
- **HueFilamentBulb2**: Secondary accent lighting  
- **PlafondWoonkamer**: Main ceiling lighting
- **Sequential Activation**: Lights activate in timed sequence for smooth transitions

#### 🎨 House State Integration
- **Adaptive Color Temperature**: Adjusts based on time of day and house mode
- **Brightness Control**: Coordinated brightness across all lights
- **Scene Synchronization**: All lights change together when house state changes

### Technical Implementation
```csharp
private void TurnOnPlafond(EventModel eventModel)
{
    var hueWallLivingRoomId = _config.Lights.DeviceIds["HueWallLivingRoom"];
    
    if (eventModel is { DeviceId: { } deviceId, Type: "initial_press" } 
        && deviceId == hueWallLivingRoomId)
    {
        if (Entities.Light.HueFilamentBulb2.IsOff())
        {
            LightExtension.TurnOnLightsWoonkamer(Entities, Scheduler);
        }
        else
        {
            LightExtension.TurnOffLightsWoonkamer(Entities, Scheduler);
        }
    }
}
```

## TV Automation App

### Key Features

#### 📺 Smart TV Control
- **Automatic Movie Mode**: Activates optimal lighting when TV turns on
- **Scene Restoration**: Restores previous lighting when TV turns off
- **Power Management**: Coordinates TV power state with room automation

#### 🎬 Movie Mode Lighting
- **Dimmed Ambiance**: Automatically dims lights for optimal viewing
- **Behind-TV Lighting**: Activates accent lighting behind TV
- **Eye Comfort**: Reduces eye strain during viewing

#### 🔄 State Restoration
- **Memory Function**: Remembers lighting state before TV activation
- **Smart Recovery**: Restores appropriate lighting based on time of day
- **Seamless Transitions**: Smooth lighting changes during TV state changes

### Usage Scenarios
1. **TV Turns On** → Movie mode lighting activates (dimmed, accent lights)
2. **TV Turns Off** → Previous lighting state restored or time-appropriate lighting
3. **Gaming Mode** → Special gaming lighting scenario (if gaming detected)

## Gaming Automation App

### Key Features

#### 🎮 Gaming Detection
- **PlayStation Integration**: Detects when PlayStation or gaming devices are active
- **Automatic Mode Switching**: Switches to gaming-optimized environment
- **Multi-Device Support**: Supports various gaming platforms

#### 🎯 Gaming Environment Setup
- **Optimal Lighting**: Gaming-specific lighting configuration
- **Audio Control**: Adjusts audio settings for gaming
- **TV Source**: Automatically switches to correct input source
- **Volume Optimization**: Sets appropriate volume levels

#### 🔊 Audio Management
- **Volume Control**: Automatic volume adjustment for gaming
- **Audio Source**: Switches to gaming audio profile
- **Surround Sound**: Activates surround sound if available

### Technical Implementation
```csharp
private void SetupGamingEnvironment()
{
    // Set TV to gaming input
    Entities.MediaPlayer.Tv.SelectSource("PlayStation");
    
    // Adjust volume for gaming
    Entities.MediaPlayer.Tv.VolumeSet(0.6);
    
    // Set gaming lighting
    TurnOffLivingRoomLights();
    Entities.Light.TvBacklight.TurnOn(colorName: "BLUE", brightnessPct: 30);
}
```

## Configuration

### Required Entities

#### Lighting Entities
- **Accent Lights**: `light.hue_filament_bulb_1`, `light.hue_filament_bulb_2`
- **Main Light**: `light.plafond_woonkamer`
- **TV Backlight**: `light.tv_backlight`
- **Wall Switch**: Hue button with device ID in configuration

#### Media Entities
- **TV**: `media_player.tv`
- **Audio System**: `media_player.hele_huis`
- **Gaming Devices**: `media_player.playstation`, etc.

#### Control Entities
- **House Mode**: `input_select.housemodeselect`
- **Gaming Mode**: `input_boolean.gaming_mode`

### Device Configuration
```json
{
    "Lights": {
        "DeviceIds": {
            "HueWallLivingRoom": "b4784a8e43cc6f5aabfb6895f3a8dbac"
        },
        "StateChangeThrottleMs": "00:00:00.050",
        "DelayBetweenLights": "00:00:00.200"
    }
}
```

## Integrated Behaviors

### Scenario 1: Evening TV Watching
1. **Wall Switch Pressed** → All living room lights turn on
2. **TV Turns On** → Lights automatically dim for viewing
3. **Movie Mode** → Accent lighting provides comfortable ambiance
4. **TV Turns Off** → Lights restore to evening brightness

### Scenario 2: Gaming Session
1. **PlayStation Starts** → Gaming mode automatically activates
2. **TV Input** → Automatically switches to PlayStation input
3. **Gaming Lighting** → Optimized lighting for gaming (minimal glare)
4. **Audio Setup** → Volume and audio settings optimized
5. **Gaming Ends** → Normal living room lighting restored

### Scenario 3: House State Changes
1. **Day Mode** → All lights use daylight color temperature
2. **Evening Mode** → Warm color temperature activated
3. **Night Mode** → Dimmed lighting if any lights are on
4. **Automatic Sync** → All lights change together for consistency

## Smart Coordination Features

### Cross-App Communication
- **State Sharing**: Apps share state information for coordinated behavior
- **Priority System**: Gaming mode overrides TV mode, etc.
- **Conflict Resolution**: Smart handling when multiple modes are active

### Energy Efficiency
- **Automatic Shutoff**: Lights turn off when no activity detected
- **Smart Dimming**: Reduces brightness instead of full off/on cycling
- **Standby Reduction**: Turns off unnecessary devices during away mode

### User Experience
- **Seamless Transitions**: Smooth lighting changes between modes
- **Predictive Behavior**: Anticipates user needs based on patterns
- **Manual Override**: Physical controls always take precedence

## Advanced Features

### Adaptive Color Temperature
```csharp
private static int GetColorTemp(IEntities entities)
{
    var houseState = Globals.GetHouseState(entities);
    return houseState switch
    {
        HouseState.Day or HouseState.Morning => 4504,  // Cool white
        HouseState.Evening or HouseState.Night => 2300, // Warm white
        _ => 2700  // Default warm
    };
}
```

### Sequential Light Control
- **Staged Activation**: Lights turn on in sequence for dramatic effect
- **Timing Control**: Configurable delays between light activations
- **Smooth Transitions**: Gradual brightness changes rather than instant on/off

### State Memory
- **Previous State**: Remembers lighting configuration before mode changes
- **Time-Aware Restoration**: Restores appropriate lighting for current time
- **Context Switching**: Different restoration based on activity type


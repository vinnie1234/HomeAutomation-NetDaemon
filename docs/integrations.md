# Integrations & Connections

## Overview

This document describes all external integrations and connections used by the Home Automation NetDaemon system.

## Primary Integration: Home Assistant

### Connection Details
- **Protocol**: WebSocket API connection via NetDaemon
- **Authentication**: Long-lived access token
- **Communication**: Bidirectional real-time communication
- **Data Format**: JSON over WebSocket

### Entity Management
The system interacts with numerous Home Assistant entities:

#### Sensors
- **Battery Sensors**: All devices with battery monitoring
- **Motion Sensors**: PIR sensors throughout the house
- **Environmental**: Temperature, humidity, air quality
- **Energy**: Electricity pricing, consumption monitoring
- **Device Status**: Various smart device states

#### Lights
- **Philips Hue**: Smart bulbs and light strips
- **IKEA TRADFRI**: Additional smart lighting
- **LED Strips**: Custom LED installations
- **Traditional**: Smart switches for regular bulbs

#### Media Players
- **TV**: Samsung smart TV integration
- **Audio**: Speakers throughout house
- **Gaming**: PlayStation and gaming device status

#### Switches & Buttons
- **Smart Switches**: Wall-mounted smart switches
- **Input Buttons**: Virtual buttons for automation triggers
- **Scene Controllers**: Multi-button scene controllers

## Discord Integration

### Notification System
- **Webhook URLs**: Secure Discord webhook integration using Discord.Net.Webhook
- **Multiple Channels**: Different channels for different notification types
- **Rich Embeds**: Formatted notifications with images, fields, and color coding
- **Priority Levels**: High, medium, low priority notifications
- **Logging Integration**: All application logs sent to Discord channels

### Discord Channels Configuration
The system uses multiple Discord channels for different types of notifications:
```csharp
// Configuration structure for Discord logging
{
    "NetDaemonLogging": {
        "Debug": "https://discord.com/api/webhooks/...",       // Debug messages
        "Information": "https://discord.com/api/webhooks/...", // Information logs
        "Warning": "https://discord.com/api/webhooks/...",     // Warning alerts
        "Error": "https://discord.com/api/webhooks/...",       // Error notifications
        "Exception": "https://discord.com/api/webhooks/..."    // Exception details
    }
}
```

### Discord Logging Features
- **Structured Logging**: All application logs automatically sent to Discord
- **Rich Embeds**: Color-coded messages with emojis for different log levels
- **Exception Handling**: Detailed exception information with stack traces
- **Real-time Monitoring**: Instant notifications for system events

### Notification Types
- **Security Alerts**: Motion detection, door/window sensors
- **System Status**: Updates, errors, maintenance notifications
- **Pet Care**: Cat feeding, litter box status
- **Energy**: Electricity price alerts, high/low usage
- **Device Status**: Battery warnings, device offline alerts

## External APIs

### Energy Price Monitoring
- **Service**: EPEX SPOT Netherlands electricity prices
- **Integration**: Via Home Assistant sensor
- **Purpose**: Dynamic energy price notifications
- **Frequency**: Real-time price updates

### Twitter/X Integration (CocMonitoring)
- **Purpose**: Monitor Clash of Clans clan activities
- **Method**: RSS feed monitoring
- **Notifications**: Discord alerts for clan events
- **Data**: Clan war results, member activities

### Weather Services
- **Provider**: Various weather APIs via Home Assistant
- **Purpose**: Weather-based automation triggers
- **Data**: Temperature, precipitation, forecasts

## Device-Specific Integrations

### Smart Pet Devices
#### PetSnowy Litter Box
- **Protocol**: LocalTuya integration
- **Monitoring**: Usage patterns, cleaning cycles, errors
- **Automation**: Automatic cleaning schedules
- **Notifications**: Error alerts, maintenance reminders

#### Zedar Pet Feeder
- **Protocol**: LocalTuya integration
- **Control**: Automated feeding schedules
- **Monitoring**: Food level, feeding history
- **Safety**: Portion control, schedule management

### Vacuum Cleaner
- **Brand**: Various robot vacuum models
- **Integration**: Home Assistant vacuum platform
- **Automation**: Room-specific cleaning schedules
- **Triggers**: Motion-based cleaning (e.g., after litter box use)

### Smart Speakers & Audio
- **Google Assistant**: Voice command integration
- **Multi-room Audio**: Synchronized audio playback
- **Music Services**: Integrated streaming services
- **Notifications**: Text-to-speech announcements

## Network Infrastructure

### Local Network Setup
- **Router**: Advanced router with VLAN support
- **IoT Network**: Separate network segment for IoT devices
- **Monitoring**: Network device status monitoring
- **Security**: Isolated IoT network with firewall rules

### Communication Protocols
- **WiFi**: Primary communication for most devices
- **Zigbee**: Low-power devices (sensors, buttons)
- **Z-Wave**: Some smart switches and sensors
- **HTTP/HTTPS**: API communications
- **MQTT**: Lightweight messaging for some sensors

## Authentication & Security

### API Security
- **Home Assistant**: Long-lived access tokens
- **Discord**: Webhook URLs (secure but not authenticated)
- **External APIs**: API keys stored in configuration
- **LocalTuya**: Local network encryption

### Network Security
- **Firewall**: Network-level security
- **VPN**: Remote access capabilities
- **Certificates**: HTTPS for all external communications
- **Access Control**: Restricted device access

## Data Flow Diagram

```
┌─────────────────┐    WebSocket    ┌──────────────────┐
│ Home Assistant  │◄────────────────┤   NetDaemon      │
│                 │                 │   Application    │
└─────────────────┘                 └──────────────────┘
        │                                     │
        │ Entity States                       │ Notifications
        │ Service Calls                       │ Logs
        ▼                                     ▼
┌─────────────────┐                 ┌──────────────────┐
│ Smart Devices   │                 │ Discord Webhooks │
│ - Lights        │                 │ - Alerts         │
│ - Sensors       │                 │ - Status         │
│ - Media Players │                 │ - Logs           │
└─────────────────┘                 └──────────────────┘
```

## Integration Monitoring

### Health Checks
- **Connection Status**: Monitor all integration connections
- **API Response Times**: Track external API performance
- **Error Rates**: Monitor failed API calls
- **Data Freshness**: Ensure sensor data is recent

### Error Handling
- **Retry Logic**: Automatic retry for failed API calls
- **Fallback Behavior**: Graceful degradation when services unavailable
- **Alert System**: Notifications when integrations fail
- **Recovery**: Automatic reconnection capabilities

## Configuration Examples

### Discord Webhook Configuration
```json
{
    "Discord": {
        "Pixel": "https://discord.com/api/webhooks/123456789/abcdef...",
        "General": "https://discord.com/api/webhooks/987654321/ghijkl..."
    }
}
```

### Home Assistant Connection
```json
{
    "HomeAssistant": {
        "Host": "192.168.1.100",
        "Port": 8123,
        "Ssl": false,
        "Token": "your-long-lived-access-token"
    }
}
```

### Device Integration Settings
```json
{
    "ZedarDeviceId": "zedar-feeder-001",
    "PetSnowyDeviceId": "petsnowy-litterbox-001",
    "BaseUrlHomeAssistant": "http://192.168.1.100:8123"
}
```

## External Documentation Links

### Integration Documentation
- **Home Assistant**: https://www.home-assistant.io/integrations/
- **NetDaemon Integration**: https://netdaemon.xyz/docs/started/hass_integration
- **Discord Webhooks**: https://discord.com/developers/docs/resources/webhook
- **Discord.Net Documentation**: https://discordnet.dev/guides/concepts/events.html
- **LocalTuya Integration**: https://github.com/rospogrigio/localtuya
- **EPEX SPOT**: https://www.home-assistant.io/integrations/epex_spot/
- **Home Assistant Vacuum**: https://www.home-assistant.io/integrations/vacuum/
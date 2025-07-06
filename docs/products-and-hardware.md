# Products and Hardware

## Overview

This document provides a comprehensive list of all smart home devices, sensors, and hardware products integrated into the NetDaemon home automation system. Each product category includes specific models, integration methods, and key features.

## Smart Lighting

### Philips Hue Ecosystem
- **Philips Hue Filament Bulbs**: Vintage-style smart bulbs
  - Models: 3x Hue Filament Bulb - Filament Bulb 1 (`light.hue_filament_bulb_1`), Filament Bulb 2 (`light.hue_filament_bulb_2`), Living Room Ceiling (`light.plafond_woonkamer`)
  - Integration: Hue integration via Home Assistant
  - Features: Warm white, dimming, vintage aesthetics
  - Locations: Living room area (coordinated lighting sequence)
  - Automation: Sequential turn-on with color temperature coordination

- **Philips Hue Play Lightbars**: Gaming and entertainment lighting
  - Models: 3x Philips Hue Play Lightbar - Left (`light.hue_play_links`), Center (`light.hue_play_midden`), Right (`light.hue_play_rechts`)
  - Integration: Hue integration via Home Assistant
  - Features: RGB color sync, HDMI sync box integration, gaming ambiance
  - Usage: Gaming lighting, TV sync lighting, entertainment scenes

- **Philips Hue Iris Table Lamps**: Accent and mood lighting
  - Models: 3x Philips Hue Iris - Bedside (`light.nachtkastje`), Kitchen (`light.koelkast`), Outdoor (`light.buiten_deur`)
  - Integration: Hue integration via Home Assistant
  - Features: RGB color changing, mood lighting, accent illumination, weather resistant (outdoor model)
  - Usage: Bedside lighting, kitchen accent lighting, outdoor entrance lighting

- **Philips Hue Adore Bathroom Light**: Bathroom vanity lighting
  - Model: 1x Philips Hue Adore - Bathroom Mirror (`light.badkamer_spiegel`)
  - Integration: Hue integration via Home Assistant
  - Features: RGB color changing, bathroom-specific design, moisture resistant
  - Usage: Bathroom mirror illumination, vanity lighting

- **Philips Hue Struana Ceiling Light**: Bathroom ceiling lighting
  - Model: 1x Philips Hue Struana - Bathroom Ceiling (`light.plafond_badkamer`)
  - Integration: Hue integration via Home Assistant
  - Features: RGB color changing, bathroom ceiling design, moisture resistant
  - Usage: Main bathroom lighting, shower lighting

- **Philips Hue Smart Plugs**: Smart power control
  - Models: 3x Philips Hue Smart Plug - Storage (`light.berging`), Office (`light.bureau`), Hallway (`light.gang`)
  - Integration: Hue integration via Home Assistant
  - Features: Remote power control, scheduling, energy monitoring
  - Usage: Control non-smart devices, power management

- **Philips Hue Motion Sensors**: Motion and temperature detection
  - Entities: Bathroom Motion (`binary_sensor.badkamer_motion`), Hall Motion (`binary_sensor.gang_motion`), Front Door Motion (`binary_sensor.voordeur_motion`), Storage Motion (`binary_sensor.berging_motion`)
  - Integration: Hue integration via Home Assistant
  - Features: Motion detection, temperature sensing, battery monitoring
  - Usage: Automatic lighting control, presence detection

- **Philips Hue Smart Bulbs**: Standard color-changing smart bulbs
  - Entities: Throughout house including main bathroom (`light.badkamer`), bedroom (`light.slaapkamer`), hall lights (`light.hal`, `light.hal_2`), kitchen (`light.lampen_keuken`), storage room 2 (`light.berging_2`), office 2 (`light.bureau_2`), main ceiling (`light.plafond`), TV area (`light.tv`), temp light (`light.temp`)
  - Integration: Hue integration via Home Assistant
  - Features: RGB color, brightness control, scenes, coordinated room lighting
  - Locations: Every room in the house for comprehensive lighting coverage

### IKEA TRADFRI
- **IKEA TRADFRI Smart Bulbs**: Budget-friendly smart lighting
  - Integration: IKEA Hub (Zigbee) via Home Assistant
  - Features: Warm/cool white, dimming
  - Compatibility: Works with existing automation system
  - Usage: Additional lighting zones

### Traditional Smart Switches
- **Smart Light Switches**: Various manufacturers
  - Integration: WiFi/Zigbee via Home Assistant
  - Features: Remote control, scheduling
  - Usage: Control of non-smart traditional bulbs

## Motion and Environmental Sensors

### Philips Hue Motion Sensors (Combined Motion & Temperature)
- **Hue Motion Sensors**: Multi-functional sensors providing both motion and temperature detection
  - Specific Entities:
    - Bathroom Motion: `binary_sensor.badkamer_motion` with temperature `sensor.badkamer_temperature`
    - Hall Motion: `binary_sensor.gang_motion` with temperature `sensor.gang_temperature`
    - Front Door Motion: `binary_sensor.voordeur_motion`
    - Storage Motion: `binary_sensor.berging_motion`
  - Integration: Hue integration via Home Assistant
  - Features: PIR motion detection, temperature monitoring, battery powered, wireless
  - Battery Monitoring: Low battery alerts (`binary_sensor.badkamermotion_battery_low`)
  - Temperature Threshold: 25°C alert level for overheating protection
  - Usage: Automatic lighting control, climate monitoring, security monitoring

## Entertainment and Media

### Television and Audio
- **Samsung Smart TV**: Main living room television
  - Model: Samsung Smart TV series
  - Integration: Home Assistant TV platform
  - Features: HDMI switching, volume control, power management
  - Usage: Gaming mode, movie scenes, automated control

- **AV Soundbar**: Audio system enhancement
  - Integration: IR/HDMI-CEC via Home Assistant
  - Features: Volume control, power management
  - Usage: Automatic activation with TV and gaming

### Gaming Equipment
- **PlayStation 5 (PS5)**: Gaming console
  - Detection: Sony device tracker (`device_tracker.sony`)
  - Integration: Network-based detection via Home Assistant
  - Features: Automatic gaming environment setup
  - Usage: Triggers gaming mode lighting and audio

### Smart Speakers and Audio
- **Google Home/Google Assistant**: Voice control and music playback
  - Integration: Google Assistant integration
  - Features: Voice commands, music streaming, TTS announcements
  - Services: Spotify (Spotcast), Google Home control
  - Usage: Bathroom music, shower mode, toothbrush automation

## Pet Care Automation

### PetSnowy Smart Litter Box
- **PetSnowy Automatic Litter Box**: Automated cat litter management
  - Model: PetSnowy smart litter box
  - Integration: LocalTuya integration via Home Assistant
  - Entities: Status (`sensor.petsnowy_litterbox_status`), Errors (`sensor.petsnowy_litterbox_errors`), Notifications (`sensor.petsnowy_litterbox_notifications`)
  - Features: Automatic cleaning cycles, auto deodorization, error monitoring, usage tracking
  - Controls: Auto clean, child safety lock, DND mode, scheduled cleaning
  - Filter Monitoring: Filter remaining life tracking
  - Automation: Triggers iRobot vacuum cleaning after use
  - Safety: Child safety lock, error detection and alerts

### PetSnowy Smart Pet Fountain
- **PetSnowy Water Fountain**: Automated cat hydration system
  - Model: PetSnowy smart fountain
  - Integration: LocalTuya integration via Home Assistant
  - Entities: Fountain switch (`switch.petsnowy_fountain_ison`), Light (`switch.petsnowy_fountain_light`), Mode (`select.petsnowy_fountain_mode`)
  - Features: Automatic water circulation, interval controls, cleaning reminders
  - Settings: Fountain interval (`number.petsnowy_fountain_fountain_interval`), Pause interval (`number.petsnowy_fountain_pause_interval`)
  - Monitoring: Cleaning days remaining (`sensor.petsnowy_fountain_cleaning_days_remaining`), Filter days remaining (`sensor.petsnowy_fountain_filter_days_remaining`)
  - Automation: Scheduled operation, maintenance alerts

### Zedar Smart Pet Feeder
- **Zedar Intelligent Pet Feeder**: Automated cat feeding system
  - Model: Zedar smart feeder
  - Device ID: `zedar-feeder-001`
  - Integration: LocalTuya integration via Home Assistant
  - Features: Scheduled feeding, portion control, food level monitoring
  - Monitoring: Food level warnings (`sensor.petsnowy_feeder_warnings`), Food enough status (`sensor.petsnowy_feeder_food_enough`)
  - Automation: Manual and scheduled feeding routines via Cat app
  - Safety: Portion control, schedule management, feeding history tracking

## Cleaning Automation

### iRobot Vacuum
- **iRobot Vacuum Cleaner "Jaap"**: Automated floor cleaning
  - Model: iRobot series robot vacuum
  - Entity: `vacuum.jaap`
  - Integration: Home Assistant vacuum platform
  - Features: Zone-based cleaning, scheduling, remote control
  - Zone Configuration: Kitchen (18), Living Room (16,17), Bedroom (19), Hall (20), Litter Box (21)
  - Automation: Motion-triggered cleaning, automatic litter box area cleaning after PetSnowy cycle
  - Smart Features: Sleep state awareness, configurable skip options
  - Control: Via vacuum buttons (`input_button.vacuumclean*` entities)

## Home Security and Access

### Security Sensors
- **Door/Window Sensors**: Entry point monitoring
  - Integration: Zigbee/WiFi sensors via Home Assistant
  - Features: Open/close detection, battery monitoring
  - Usage: Security alerts, automation triggers

## Environmental Control

### Window Coverings
- **Roller Blinds**: Automated window coverings
  - Integration: Motor control via Home Assistant
  - Features: Position control, privacy automation
  - Usage: Shower mode privacy, sleep routines, light control

### Climate Control
- **Smart Thermostat**: Temperature regulation
  - Integration: Home Assistant climate platform
  - Features: Schedule control, remote adjustment
  - Coordination: House state integration, energy optimization

## Energy and Utility Monitoring

### Energy Monitoring
- **Smart Energy Meter**: Electricity consumption tracking
  - Integration: Home Assistant energy platform
  - Features: Real-time usage, cost tracking
  - Thresholds: >2000W alert for 10+ minutes
  - Automation: High usage alerts, price notifications

### Battery Monitoring
- **Device Battery Sensors**: Battery level tracking for all devices
  - Monitoring: All battery-powered devices in system
  - Thresholds: Configurable warning levels (default 20%)
  - Features: Automatic notifications, charging reminders

## Personal Health and Monitoring

### Oral-B Smart Toothbrush
- **Oral-B Smart Series 4000**: Intelligent electric toothbrush
  - Model: Smart Series 4000 (97AE)
  - Entities: 
    - Toothbrush State: `sensor.smart_series_4000_97ae_toothbrush_state`
    - Battery Level: `sensor.smart_series_4000_97ae_battery`
    - Brushing Time: `sensor.smart_series_4000_97ae_time`
    - Pressure: `sensor.smart_series_4000_97ae_pressure`
    - Mode: `sensor.smart_series_4000_97ae_mode`
    - Number of Sectors: `sensor.smart_series_4000_97ae_number_of_sectors`
    - Current Sector: `sensor.smart_series_4000_97ae_sector`
  - Integration: Bluetooth integration via Home Assistant
  - Features: Usage detection, timer integration, pressure monitoring, brushing mode tracking
  - Automation: Music playbook during brushing (15% volume), 30-second idle timeout
  - Health Tracking: Brushing duration, pressure feedback, sector completion

### Mobile Devices
- **Smartphones and Tablets**: Personal device integration
  - Devices: Vincent's phone, tablets
  - Integration: Home Assistant mobile app
  - Features: Location tracking, battery monitoring, notifications
  - Thresholds: <30% battery warning
  - Automation: Away detection, presence sensing

## Network and Infrastructure

### ASUS RT-AX88U Router
- **ASUS RT-AX88U**: Advanced WiFi 6 router
  - Model: RT-AX88U high-performance router
  - Integration: AsusRouter integration via Home Assistant
  - Monitoring Entities:
    - Router uptime: `sensor.routeruptime`
    - CPU temperature: Multiple temperature sensors for 2.4GHz, 5GHz, and CPU
    - Network statistics: Download/upload speeds for various bands
    - Connected devices: Device tracking and monitoring
    - LED control: `light.rt_ax88u_led`
  - Features: WiFi 6 support, VLAN support, device tracking, guest networks
  - Security: Isolated IoT network, firewall rules, VPN support (OpenVPN, WireGuard)
  - Management: Firmware updates, service controls, parental controls

### Communication Protocols
- **Zigbee Hubs**: Low-power device communication
  - IKEA TRADFRI Hub: IKEA devices integration
  - Philips Hue Hub: Hue ecosystem integration
  - Usage: Motion sensors, smart buttons, some lighting
  - Integration: Direct Home Assistant integration

- **WiFi Devices**: High-bandwidth device communication
  - Usage: Smart TVs, streaming devices, cameras, smart speakers
  - Integration: Network-based discovery and control via ASUS router

- **LocalTuya Protocol**: Local device control
  - Usage: PetSnowy devices (litter box, fountain, feeder)
  - Integration: LocalTuya integration for local network control

## External Service Integrations

### Energy Services
- **EPEX SPOT Netherlands**: Dynamic electricity pricing
  - Integration: Home Assistant sensor
  - Features: Real-time price tracking, negative price alerts
  - Automation: Energy optimization notifications

### Weather Services
- **Weather API Providers**: Weather data integration
  - Integration: Various weather APIs via Home Assistant
  - Features: Temperature, precipitation, forecasts
  - Usage: Weather-based automation triggers

## Brand and Manufacturer Directory

### Major Brands
- **Philips**: Hue lighting ecosystem, smart bulbs, motion sensors, Play light bars
- **IKEA**: TRADFRI budget smart lighting and Zigbee hub
- **Samsung**: Smart TV, entertainment devices
- **Sony**: PlayStation gaming console
- **Google**: Home/Assistant smart speakers, voice control
- **PetSnowy**: Smart litter box, water fountain, pet feeder automation
- **Zedar**: Intelligent pet feeding systems
- **iRobot**: Vacuum cleaning automation ("Jaap")
- **ASUS**: RT-AX88U high-performance router
- **Oral-B**: Smart Series 4000 electric toothbrush

### Integration Platforms
- **Home Assistant**: Central automation platform
- **NetDaemon**: Automation application framework
- **Discord**: Notification and logging platform
- **Spotify**: Music streaming integration
- **LocalTuya**: Local device control protocol
- **Hue Integration**: Philips Hue ecosystem integration
- **AsusRouter Integration**: ASUS router monitoring and control

## Purchase and Compatibility Information

### Recommended Retailers (Netherlands)
- **Philips Hue**: Official Philips store, bol.com, Coolblue, MediaMarkt
- **IKEA TRADFRI**: IKEA Nederland stores and online
- **Pet Products**: Specialized pet care retailers, online marketplaces
- **General Electronics**: bol.com, Coolblue, MediaMarkt, Alternate, Azerty
- **ASUS Router**: Computer stores, bol.com, Coolblue, Alternate
- **iRobot Vacuum**: bol.com, Coolblue, Robot specific retailers

### Compatibility Requirements
- **Home Assistant**: All devices must be compatible with Home Assistant
- **Network**: Stable WiFi/Ethernet connection required
- **Power**: USB power or battery backup for critical sensors
- **Protocols**: Support for Zigbee, Z-Wave, or WiFi integration

### Setup and Configuration
- **Device Registration**: All devices registered in Home Assistant
- **Network Configuration**: Proper VLAN and security setup
- **Battery Management**: Regular battery replacement schedule
- **Firmware Updates**: Keeping device firmware current

This comprehensive hardware list provides the foundation for the intelligent home automation system, with each device contributing to the seamless, automated living experience managed by the NetDaemon platform.
# Home Automation NetDaemon

A comprehensive smart home automation system built on NetDaemon 5 for seamless control over all aspects of the home environment.

## 📋 Overview

This project is a fully personalized home automation solution that leverages NetDaemon 5 to implement complex automation logic on top of Home Assistant. The system manages lighting, devices, notifications, security, and much more through intelligent apps that respond to sensors, time schedules, and user interactions.

**Project Type:** Personal NetDaemon-based Home Automation System

This is a complete home automation ecosystem that includes:
- **Smart Lighting Control** - Automatic light management based on motion, time, and room occupancy
- **Notification System** - Multi-channel notifications via Discord, mobile app, and text-to-speech
- **Device Management** - Automated control of vacuum cleaners, alarms, and other smart devices
- **Room-based Automation** - Specialized automation for different areas of the home
- **Intelligent State Management** - Context-aware automation based on home/away status, sleep schedules, and daily routines

## 🚀 Quick Start

```bash
# Build the project
dotnet build

# Run tests
dotnet test

# Deploy (local network)
./automation/Scripts/Build\ and\ deploy\ over\ LAN.ps1
```

## 📚 Documentation

The complete documentation is split into different sections:

> **Note**: This comprehensive documentation was generated and structured with assistance from **Claude (Anthropic)** to ensure accuracy, completeness, and professional presentation of the NetDaemon home automation system.

### Core Documentation
- **[Technical Specifications](docs/technical-specifications.md)** - C# version, NetDaemon version, and architecture details
- **[Integrations & Connections](docs/integrations.md)** - Discord, Home Assistant, external services  
- **[Logging & Monitoring](docs/logging-and-monitoring.md)** - Discord logging, error handling, debugging
- **[Configuration](docs/configuration.md)** - App configuration, settings and customizations

### App Documentation
Each automation app has its own detailed documentation:

#### 🏠 General Apps
- **[Alarm System](docs/apps/alarm.md)** - Home security monitoring and alerting system
- **[Alarm Clock](docs/apps/alarm-clock.md)** - Wake-up routines and alarm management
- **[Auto Update](docs/apps/auto-update.md)** - System update monitoring and management
- **[Away Manager](docs/apps/away-manager.md)** - Home/away detection and related automations
- **[Battery Monitoring](docs/apps/battery-monitoring.md)** - Battery status monitoring for all devices
- **[Cat Care](docs/apps/cat.md)** - Automatic cat feeding and litter box monitoring
- **[CoC Monitoring](docs/apps/coc-monitoring.md)** - Clash of Clans activity monitoring via social media
- **[Download Monitoring](docs/apps/download-monitoring.md)** - Download completion notifications and file management
- **[Fun App](docs/apps/fun.md)** - Entertainment and special occasion automations
- **[Google Assistant Translate](docs/apps/google-assistant-translate.md)** - Voice command translation and automation
- **[Holiday Manager](docs/apps/holiday-manager.md)** - Holiday detection and special automation modes
- **[House State Manager](docs/apps/house-state-manager.md)** - Central management of house states (Morning/Day/Evening/Night)
- **[NetDaemon System](docs/apps/netdaemon-system.md)** - System monitoring and lifecycle management
- **[PC Manager](docs/apps/pc-manager.md)** - PC-related automations and device control
- **[Reset App](docs/apps/reset.md)** - System reset and restoration capabilities
- **[Save In State](docs/apps/save-in-state.md)** - Device state management and restoration
- **[Sleep Manager](docs/apps/sleep-manager.md)** - Sleep routines and energy price notifications
- **[Vacuum](docs/apps/vacuum.md)** - Robot vacuum automation and room-specific cleaning

#### 🚪 Room-specific Apps  
- **[Bathroom](docs/apps/bathroom-lights.md)** - Motion detection, shower mode, music automation
- **[Bedroom](docs/apps/bedroom-lights.md)** - Light automation for bedroom
- **[Hall](docs/apps/hall-lights.md)** - Motion detection and light brightness in hallway
- **[Living Room](docs/apps/living-room.md)** - TV automation, gaming mode, lighting scenes

### Hardware & Products
- **[Product List](docs/products-and-hardware.md)** - Complete list of smart home devices and integrations used

## 🛠️ Development

```bash
# Code generation for NetDaemon
dotnet tool run nd-codegen

# Local build and deployment
dotnet publish -c Release -o ./Release
```

## 🏗️ Project Structure

```
automation/
├── apps/                   # All automation apps
│   ├── General/           # General automations
│   └── Rooms/             # Room-specific automations
├── Configuration/         # Configuration classes
├── Extensions/           # Extension methods
├── Helpers/              # Utility classes
├── Interfaces/           # Service interfaces
├── Models/               # Data models
└── Repository/           # Data persistence

TestAutomation/            # Unit tests
docs/                      # Comprehensive documentation
```

## 🤝 Contributing

1. Fork the project
2. Create a feature branch (`git checkout -b feature/new-feature`)
3. Commit your changes (`git commit -m 'Add new feature'`)
4. Push to the branch (`git push origin feature/new-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🆘 Support

For questions or support:
- Open a GitHub Issue
- Join the [NetDaemon Discord server](https://discord.gg/K3xwfcX)

---

**Note**: This is a personal home automation project. Configuration and device IDs are specific to this setup and must be adapted for use in other environments.
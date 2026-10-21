# Home Automation NetDaemon

A fully personal smart home, built with **NetDaemon 26** on **.NET 10** on top of Home Assistant, together with a
hand-made Home Assistant dashboard that brings it all together.

## 📋 Overview

This project implements the automation logic of the house as C# apps on NetDaemon, and ships the Lovelace dashboard
that shows it. The apps react to sensors, schedules and people, and manage lighting, presence, the cat, laundry,
energy, parcels, notifications and more.

**Project Type:** Personal NetDaemon-based Home Automation System

Highlights:
- **Smart lighting** — motion, time of day, circadian colour temperature and per-room scenes
- **Presence and routines** — per-person presence, work schedules (office, home or off per day), sleep and wake-up
- **The cat, Pixel** — automatic feeding, litter box and fountain monitoring, and a log of when he threw up
- **Energy** — dynamic prices, warnings for extreme prices, the cost of working from home, and power estimates for
  devices that only report kWh
- **Household** — laundry advice and monitoring, garbage pickup, parcels from PostNL, DHL, DPD and GLS
- **Notifications** — Discord, the mobile app, text-to-speech and a WhatsApp assistant (text and voice notes)
- **Self-maintaining** — creates its own Home Assistant helpers, reloads failing integrations and auto-updates

## 🖥️ Dashboard

The dashboard lives in [`dashboard/`](dashboard) as a YAML-mode Lovelace dashboard, built mainly with
[button-card](https://github.com/custom-cards/button-card), [Bubble Card](https://github.com/Clooos/Bubble-Card) and
[Navbar Card](https://github.com/joseluis9595/lovelace-navbar-card) and the "Glass Home" theme. Installation and the
required HACS cards are described in [`dashboard/README.md`](dashboard/README.md).

> The screenshots below are rendered from the real dashboard templates with made-up data.

### Weather card

The heart of the Home page: a live sky (sun and moon at their real position, generated clouds, rain, snow, fog and
storms) over a landscape that changes with the season and the holidays. It also tells what is going on at home:
smoke from the chimney and a lit window while someone is home, the laundry on the line (swaying in the wind),
garbage bags or the paper bin at the curb when they go out, and Pixel wandering around — ten minutes before his
food he waits at the front door. The pills show who is home, asleep or working, and kitchen timers count down live.

![Weather card on an autumn afternoon](docs/images/dashboard/weather-card-day.png)

![Weather card on Christmas Eve](docs/images/dashboard/weather-card-christmas.png)

### Alerts on Home

Everything that needs attention right now gets an eye-catching card on Home, and disappears again when it no longer
matters: extreme electricity prices, the running washing machine with its phase and time left, laundry outside when
rain is coming, problems with Pixel's things and batteries below 10%.

![Alerts on the Home page](docs/images/dashboard/home-alerts.png)

### Pixel and the personal pages

A two-week timeline of when Pixel threw up, where days with more moments get more room so the times stay readable.
Vincent and Carleen each have a private page with their own work schedule (tap a day for off, home or office) and,
for Vincent, the stock of cigarette packs.

![Pixel's timeline, cigarette stock and work schedule](docs/images/dashboard/pixel-and-personal.png)

### Energy, parcels and working from home

Hourly prices for today, parcels per carrier, and per day how much electricity working from home used and cost —
imports at the all-in price, feed-in at the market price, without the washing machine.

![Energy prices, parcels and working from home](docs/images/dashboard/energy-parcels-homework.png)

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

> **Note**: This documentation was generated and structured with assistance from **Claude (Anthropic)**.

### Core Documentation
- **[Technical Specifications](docs/technical-specifications.md)** - C# version, NetDaemon version, and architecture details
- **[Integrations & Connections](docs/integrations.md)** - Discord, Home Assistant, external services
- **[Logging & Monitoring](docs/logging-and-monitoring.md)** - Discord logging, error handling, debugging
- **[Configuration](docs/configuration.md)** - App configuration, settings and customizations
- **[Testing Framework](docs/testing.md)** - Details on Rx testing, AppTestContext, and time manipulation
- **[Product List](docs/products-and-hardware.md)** - Smart home devices and integrations used

### Apps

#### 🏠 General Apps
- **[Alarm System](docs/apps/alarm.md)** - Security monitoring, high temperature and energy price warnings
- **[Auto Update](docs/apps/auto-update.md)** - Monthly Home Assistant updates with notifications
- **[Away Manager](docs/apps/away-manager.md)** - Away and welcome-home state machine
- **[Battery Monitoring](docs/apps/battery-monitoring.md)** - Low battery notifications for all devices
- **[Cat Care](docs/apps/cat.md)** - Automatic feeding, litter box and fountain monitoring
- **Cat Vomit Tracker** - Remembers when Pixel threw up and publishes the last two weeks for the dashboard
- **[CoC Monitoring](docs/apps/coc-monitoring.md)** - Clash of Clans activity monitoring via social media
- **[Fun App](docs/apps/fun.md)** - Entertainment and special occasion automations
- **[Google Assistant Translate](docs/apps/google-assistant-translate.md)** - Voice command translation and automation
- **Helper Manager** - Creates the missing Home Assistant helpers (input_boolean, input_select, counter, ...) from code
- **[Holiday Manager](docs/apps/holiday-manager.md)** - Days off from the calendar and the matching alarm reminders
- **Home Work Energy Tracker** - Electricity use and costs per day while working from home
- **[House State Manager](docs/apps/house-state-manager.md)** - House states (morning/day/evening/night) and per-person working hours
- **Integration Monitor** - Detects failing integrations and reloads them
- **Laundry** - Washing machine monitoring and advice on drying the laundry outside
- **[NetDaemon System](docs/apps/netdaemon-system.md)** - System monitoring and lifecycle management
- **Parcel Notifications** - Notifications for new parcels, status changes and PostNL letters
- **[PC Manager](docs/apps/pc-manager.md)** - PC-related automations and device control
- **Power Estimator** - Estimated power for devices that only report an energy meter (such as the washing machine)
- **Presence Manager** - Per-person presence and the shared away state
- **Presence Simulation** - Makes the house look lived-in while on vacation
- **[Reset App](docs/apps/reset.md)** - System reset and restoration capabilities
- **[Save In State](docs/apps/save-in-state.md)** - Device state management and restoration
- **[Sleep Manager](docs/apps/sleep-manager.md)** - Sleep routines, roller blind and wake-up
- **[Vacuum](docs/apps/vacuum.md)** - Robot vacuum automation and room-specific cleaning
- **WhatsApp Assistant** - Answers text and voice messages from Vincent and Carleen through the Home Assistant assistant

#### 🚪 Room-specific Apps
- **[Bathroom](docs/apps/bathroom-lights.md)** - Motion detection, shower mode, music automation
- **[Bedroom](docs/apps/bedroom-lights.md)** - Light automation for the bedroom
- **[Hall](docs/apps/hall-lights.md)** - Motion detection and light brightness in the hallway
- **Kitchen Music** - Music in the kitchen on motion when nobody is working
- **[Living Room](docs/apps/living-room.md)** - Lighting, TV automation and gaming mode

## 🛠️ Development

```bash
# Code generation for NetDaemon (after adding entities or helpers in Home Assistant)
dotnet tool run nd-codegen

# Local build and deployment
dotnet publish -c Release -o ./Release
```

New helpers are added in `automation/Configuration/HelperDefinitions.cs`: the Helper Manager creates them in Home
Assistant on the next start, after which code generation makes them available as typed entities.

## 🏗️ Project Structure

```
automation/
├── apps/                  # All automation apps
│   ├── General/           # General automations
│   └── Rooms/             # Room-specific automations
├── Configuration/         # Configuration classes and the helper definitions
├── Core/                  # Core services (entity manager, assistant clients)
├── CustomLogger/          # Logging, including Discord
├── Enum/                  # Enumerations
├── Extensions/            # Extension methods (Dutch-time scheduling, ...)
├── Helpers/               # Utility classes
├── Interfaces/            # Service interfaces
├── Models/                # Data models
└── Repository/            # Data persistence

dashboard/                 # Home Assistant dashboard (YAML), theme and templates
TestAutomation/            # Unit tests
TestIntegration/           # Integration tests
docs/                      # Documentation and dashboard screenshots
```

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🆘 Support

For questions or support:
- Open a GitHub Issue
- Join the [NetDaemon Discord server](https://discord.gg/K3xwfcX)

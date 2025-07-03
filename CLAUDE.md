# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a NetDaemon-based home automation system that manages various aspects of a smart home including lighting, alarms, notifications, and device control. The project uses NetDaemon 4 framework to interface with Home Assistant.

## Key Commands

### Build and Development
- `dotnet build` - Build the solution
- `dotnet build automation/Automation.csproj` - Build main automation project
- `dotnet publish -c Release -o ./Release` - Publish for deployment
- `dotnet tool run nd-codegen` - Generate NetDaemon code

### Testing
- `dotnet test` - Run all tests
- `dotnet test TestAutomation/TestAutomation.csproj` - Run specific test project

### Deployment
- `automation/Scripts/Build and deploy over LAN.ps1` - PowerShell script for local deployment
- `automation/build.sh` - Simple build script

## Architecture

### Project Structure
- `automation/` - Main NetDaemon application
  - `apps/` - All automation apps organized by category
    - `BaseApp.cs` - Base class for all apps providing common services
    - `General/` - General purpose automation apps
    - `Rooms/` - Room-specific automation apps
    - `Notify.cs` - Notification service implementation
  - `Models/` - Data models and DTOs
  - `Interfaces/` - Service interfaces
  - `Extensions/` - Extension methods
  - `Helpers/` - Utility classes
  - `Repository/` - Data persistence layer
  - `CustomLogger/` - Custom logging implementation including Discord logging
- `TestAutomation/` - Test project with unit tests for all apps

### Key Components

#### BaseApp Class
All automation apps inherit from `BaseApp` which provides:
- `Entities` - Home Assistant entities
- `Services` - Home Assistant services
- `Logger` - Logging interface
- `Notify` - Notification service
- `Scheduler` - Task scheduling
- `HaContext` - Home Assistant context
- `Vincent` - PersonModel for the main user

#### App Categories
- **General Apps**: System-wide automation (alarms, away management, battery monitoring, etc.)
- **Room Apps**: Room-specific automation (lights, TV control, etc.)
- **Notification System**: Discord-based notifications with different priority levels

#### Data Persistence
- Uses `IDataRepository` for state persistence
- JSON-based storage in `.storage/` directory
- Models include `AlarmStateModel`, `LightStateModel`, etc.

### Testing Framework
- Uses xUnit with FluentAssertions
- `AppTestContext` helper for mocking NetDaemon dependencies
- `Init.cs` provides helper methods for app initialization in tests
- Tests are organized to mirror the app structure

### Key Technologies
- .NET 9.0 with C# 12
- NetDaemon 4 (version 25.18.1)
- Discord.Net for notifications
- Serilog for logging
- xUnit, Moq, NSubstitute for testing

## Development Notes

### Creating New Apps
1. Inherit from `BaseApp`
2. Place in appropriate folder (`General/` or `Rooms/`)
3. Follow naming convention matching the class name
4. Create corresponding test class in `TestAutomation/`

### Notification System
- Uses Discord webhooks for notifications
- Priority levels: High, Medium, Low
- Supports rich embeds with author, fields, and footers

### State Management
- Apps requiring persistence should inject `IDataRepository`
- State is automatically saved/restored using JSON serialization
- Storage location: `.storage/` directory

### Home Assistant Integration
- Uses generated types from `HomeAssistantGenerated.cs`
- Entity access through `Entities` property
- Service calls through `Services` property
- State changes handled via reactive extensions

## Configuration
- `appsettings.json` and `appsettings.Development.json` for configuration
- `config.json` for app-specific settings
- YAML files automatically copied to output directory
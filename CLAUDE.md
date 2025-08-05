# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a NetDaemon-based home automation system that manages various aspects of a smart home including lighting, alarms, notifications, device control, and advanced battery trend analysis. The project uses NetDaemon 4 framework to interface with Home Assistant and emphasizes synchronous operations throughout the codebase.

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
    - `General/` - General purpose automation apps (including BatteryTrendAnalyzer)
    - `Rooms/` - Room-specific automation apps
    - `Notify.cs` - Notification service implementation with resilience patterns
  - `Models/` - Data models and DTOs
    - `Battery/` - Battery-related models (BatteryDeviceInfo, BatteryTrendResult)
  - `Interfaces/` - Service interfaces (IDataRepository, INotify, IEntityManager)
  - `Extensions/` - Extension methods
  - `Helpers/` - Utility classes
  - `Repository/` - Data persistence layer (synchronous operations)
  - `Core/` - Core services (EntityManager for Home Assistant entity management)
  - `CustomLogger/` - Custom logging implementation including Discord logging
- `TestAutomation/` - Test project with comprehensive unit tests for all apps

### Key Components

#### BaseApp Class
All automation apps inherit from `BaseApp` which provides:
- `Entities` - Home Assistant entities
- `Services` - Home Assistant services  
- `Logger` - Logging interface
- `Notify` - Notification service with resilience patterns
- `Scheduler` - Task scheduling
- `HaContext` - Home Assistant context
- `Vincent` - PersonModel for the main user
- `ResiliencePipeline` - Polly-based resilience patterns (retry + circuit breaker)

#### App Categories
- **General Apps**: System-wide automation (alarms, away management with state machine, battery trend analysis, etc.)
- **Room Apps**: Room-specific automation (lights, TV control, etc.)
- **Notification System**: Discord-based notifications with resilience patterns and priority levels

#### Data Persistence  
- Uses `IDataRepository` for state persistence (synchronous operations)
- JSON-based storage in `.storage/` directory
- Models include `AlarmStateModel`, `LightStateModel`, `BatteryDeviceInfo`, `BatteryTrendResult`, etc.

#### Entity Management
- Uses `IEntityManager` for creating and managing Home Assistant entities
- Synchronous entity creation and state updates
- Automatic duplicate entity prevention

### Testing Framework
- Uses xUnit with FluentAssertions
- `AppTestContext` helper for mocking NetDaemon dependencies
- `Init.cs` provides helper methods for app initialization in tests
- Tests are organized to mirror the app structure

### Key Technologies
- .NET 9.0 with C# 12
- NetDaemon 4 (version 25.18.1)
- Discord.Net for notifications
- Polly v8 for resilience patterns (retry, circuit breaker)
- Serilog for logging
- xUnit, Moq, NSubstitute for testing
- System.Text.Json for serialization

## Development Notes

### Creating New Apps
1. Inherit from `BaseApp`
2. Place in appropriate folder (`General/` or `Rooms/`)
3. Follow naming convention matching the class name
4. Use synchronous operations (no async/await unless absolutely necessary)
5. Inject required services via constructor (IDataRepository, IEntityManager, etc.)
6. Create corresponding test class in `TestAutomation/`

### Synchronous-First Architecture
- **IMPORTANT**: Prefer synchronous operations throughout the codebase
- Use `IDataRepository.Get<T>()` and `Save<T>()` for data operations
- Only use async when required by external APIs or NetDaemon framework
- Avoid `Task.Run()` and unnecessary async/await patterns

### Notification System
- Uses Discord webhooks for notifications with resilience patterns
- Polly-based retry and circuit breaker for reliability
- Priority levels: High, Medium, Low
- Supports rich embeds with author, fields, and footers
- Fallback mechanisms for failed notifications

### State Management
- Apps requiring persistence should inject `IDataRepository`
- State is automatically saved/restored using synchronous JSON serialization
- Storage location: `.storage/` directory
- Models organized in appropriate subfolders (e.g., `Models/Battery/`)

### Entity Management
- Use `IEntityManager` for creating and managing Home Assistant entities
- Automatic duplicate prevention and state management
- Synchronous entity operations with proper error handling

### Home Assistant Integration
- Uses generated types from `HomeAssistantGenerated.cs`
- Entity access through `Entities` property
- Service calls through `Services` property  
- State changes handled via reactive extensions
- Use `HaContext.CallService()` for direct service calls with proper data structures

## Configuration
- `appsettings.json` and `appsettings.Development.json` for configuration
- `config.json` for app-specific settings
- YAML files automatically copied to output directory

## Advanced Features

### Battery Trend Analysis System
**Location**: `automation/apps/General/BatteryTrendAnalyzer.cs`

#### Core Functionality
- **Daily Analysis**: Runs at 23:00 every day via cron scheduler
- **Predictive Analytics**: Calculates discharge rates and remaining battery life
- **Health Assessment**: Categorizes battery health (Excellent, Good, Warning, Critical, Degrading)
- **Automatic Entity Creation**: Creates Home Assistant sensors for all battery devices
- **Smart Replacement Detection**: Monitors battery replacement buttons and validates actual replacements

#### Entity Management
Creates the following entities per device:
- `sensor.{device}_battery_discharge_rate` - Daily discharge rate (%/day)
- `sensor.{device}_battery_days_remaining` - Predicted days until replacement
- `sensor.{device}_battery_age_days` - Current battery age in days
- `sensor.{device}_battery_health_status` - Health status assessment
- `sensor.{device}_battery_replacement_date` - Predicted replacement date

System-wide entities:
- `sensor.battery_devices_critical` - Count of devices with critical battery status
- `sensor.battery_devices_warning` - Count of devices with warning status
- `sensor.next_battery_replacement` - Next device requiring battery replacement
- `sensor.batteries_to_buy` - Shopping list of batteries needed

#### Battery Replacement Detection
- **Button Integration**: Listens to `button.{device}_battery_replaced` events
- **Smart Verification**: Validates actual battery replacement using level analysis:
  - Current level > 80% OR
  - Level increased by >30% from recent average OR
  - Recovery from low level (<25%) to high level (>70%)
- **Automatic Updates**: Updates `input_datetime.{device}_battery_last_replaced` entities
- **Trend Reset**: Resets all trend analysis data for the replaced battery
- **Notifications**: Sends confirmation notifications to user

#### Expected Battery Life Database
```csharp
"aa" => 365 days      // Motion sensors
"aaa" => 180 days     // Remotes  
"cr2032" => 730 days  // Coin cells
"cr2450" => 1095 days // Large coin cells
"9v" => 270 days      // 9V batteries
```

#### Health Status Categories
- **Critical**: ≤10% charge OR ≤7 days remaining
- **Warning**: ≤25% charge OR ≤30 days remaining
- **Good**: ≤90 days remaining with normal performance
- **Degrading**: Performance 30% worse than expected lifespan
- **Excellent**: >90 days remaining with normal/above-average performance

#### Proactive Notifications
- **2-Week Warning**: Notification 14 days before expected replacement
- **Performance Alerts**: Notifications for batteries degrading faster than expected
- **Replacement Confirmations**: Automatic confirmation when battery replacement is detected

### Away Manager State Machine
**Location**: `automation/apps/General/AwayManager.cs`

#### State Machine Implementation
- **Thread-Safe**: Uses locks to prevent race conditions
- **State Validation**: Validates all state transitions before execution
- **Comprehensive Logging**: Tracks all state changes and transitions

#### States
- `Home` - Normal presence at home
- `Away` - Away from home (triggers away actions)
- `Returning` - Detected as returning home (waiting for motion)
- `WelcomingHome` - Executing welcome home sequence

#### State Transitions
```
Home → Away (manual/automatic)
Away → Returning (presence detected)
Returning → WelcomingHome (motion detected)
WelcomingHome → Home (sequence completed)
```

#### Features
- **Context-Aware Notifications**: Different greetings based on time of day
- **Delayed Welcome Sequence**: Configurable delay for welcome home actions
- **Office Day Detection**: Special notifications for work days
- **Error Recovery**: Automatic reset to safe state on errors

### Cat Management System
**Location**: `automation/apps/General/Cat.cs`

#### Resilience Patterns
- **Feeding Operations**: Uses `ExecuteWithFallbackAsync` for reliable cat feeding
- **Fallback Notifications**: Phone notifications with Discord backup
- **Error Recovery**: Comprehensive error handling for all cat-related operations

#### Features
- **Automatic Feeding**: Scheduled feeding times with skip functionality
- **Manual Feeding**: On-demand feeding with amount tracking
- **PetSnowy Integration**: Automated litter box cleaning and emptying
- **Food Monitoring**: Tracks daily and lifetime food consumption
- **Early Feed**: Option to give next scheduled feed early

#### Monitoring & Notifications
- **Food Storage**: Monitors food levels and sends low-food alerts
- **PetSnowy Status**: Tracks cleaning cycles and error states
- **Discord Integration**: Rich notifications with feeding statistics

### Enhanced Testing Framework
**Location**: `TestAutomation/`

#### Comprehensive Test Coverage
- **Unit Tests**: Full coverage for all automation apps
- **Mocking Support**: Sophisticated mocking of NetDaemon dependencies
- **Test Helpers**: `AppTestContext` and `TestDebugHelper` for easier testing
- **Behavioral Testing**: Tests focus on behavior rather than implementation

#### Test Organization
```
TestAutomation/
├── Apps/
│   ├── General/         # Tests for general automation apps
│   └── Rooms/          # Tests for room-specific apps
└── Helpers/            # Test utilities and helpers
```

## Code Conventions

### Synchronous Operations
```csharp
// ✅ Preferred: Synchronous operations
var data = _storage.Get<MyModel>("key");
_storage.Save("key", data);

// ❌ Avoid: Unnecessary async
var data = await _storage.GetAsync<MyModel>("key");
await _storage.SaveAsync("key", data);
```

### Entity Management
```csharp
// ✅ Create entities with proper options
_entityManager.Create("sensor.my_entity", new EntityCreationOptions
{
    Name = "My Sensor",
    DeviceClass = "battery", 
    UnitOfMeasurement = "%",
    Icon = "mdi:battery"
});

// ✅ Update entity state with attributes
_entityManager.SetState("sensor.my_entity", 75, new 
{
    last_updated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
    source = "automated_calculation"
});
```

### Service Calls
```csharp
// ✅ Proper service call structure
HaContext.CallService("input_datetime", "set_datetime", data: new
{
    entity_id = "input_datetime.my_datetime",
    date = DateTime.Now.ToString("yyyy-MM-dd"),
    time = DateTime.Now.ToString("HH:mm:ss")
});
```

### Error Handling
```csharp
// ✅ Use resilience patterns from BaseApp
await ExecuteWithFallbackAsync(
    primaryOperation,
    fallbackOperation,
    "OperationName"
);
```

### Dependency Injection
```csharp
// ✅ Proper constructor injection
public MyApp(
    IHaContext ha,
    ILogger<MyApp> logger,
    INotify notify,
    IScheduler scheduler,
    IDataRepository storage,
    IEntityManager entityManager)
    : base(ha, logger, notify, scheduler)
{
    _storage = storage;
    _entityManager = entityManager;
}
```
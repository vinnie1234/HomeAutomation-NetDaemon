# Testing Framework

This project features a comprehensive testing framework tailored for **NetDaemon 5** using **xUnit**, **FluentAssertions**, and **NSubstitute**. It is specifically designed to handle the reactive nature (Rx) of NetDaemon applications, including time-based scheduling and state changes.

## 🏃 Running Tests

```bash
# Run all tests
dotnet test

# Run a specific test project
dotnet test TestAutomation/TestAutomation.csproj
```

Currently, the project contains **158** passing unit tests covering all General and Room-specific apps.

## 🏗️ Test Structure

The test project `TestAutomation` is structured to mirror the `automation` project:
- `Apps/General/` - Tests for general applications (e.g., `BatteryMonitoringTests.cs`, `AlarmTests.cs`)
- `Apps/Rooms/` - Tests for room-specific applications (e.g., `TvTests.cs`)
- `Helpers/` - Crucial helper classes for mocking Home Assistant behavior, notably `AppTestContext`.

## 🧪 AppTestContext

The heart of the testing framework is the `AppTestContext`. It provides mocked instances of `IHaContext`, `INotify`, and Rx `TestScheduler`.

### Initialization

```csharp
// 1. Create a test context with a mocked TestScheduler
var ctx = AppTestContext.NewWithScheduler();

// 2. Setup mock entity states BEFORE initializing the app
var sensor1 = new EntityState { State = "20" };
ctx.HaContext.GetState("sensor.battery_1").Returns(sensor1);

// 3. Initialize the app
// Note: InitApp injects all dependencies into your app constructor automatically
var config = Options.Create(new AppConfig());
var appConfig = Options.Create(new AppConfiguration());
var app = ctx.InitApp<BatteryMonitoring>(config, appConfig);
```

### Mocking State Changes

NetDaemon relies heavily on `StateChanges()` or `StateAllChanges()`. In tests, you can trigger these synchronously using:

```csharp
ctx.ChangeStateFor("sensor.battery_1")
   .FromState("20")
   .ToState("5");

// It's always a good idea to process any pending RX operations after changing a state
ctx.HaContextMock.ProcessPendingOperations();
```

### Time Manipulation (Rx Scheduling)

For apps that use time-based operators like `Throttle`, `Delay`, `WhenStateIsFor`, or cron jobs, the `TestScheduler` is crucial. It allows you to simulate the passage of time instantly without actually waiting.

```csharp
// Simulate advancing time by 1 hour and 1 second
ctx.AdvanceTimeBy(TimeSpan.FromHours(1).Ticks + TimeSpan.FromSeconds(1).Ticks);

// Process any Rx buffers or timers that were triggered by the time jump
ctx.HaContextMock.ProcessPendingOperations();
```

> **Note**: If your app uses `Task.Run` or `Task.Delay` alongside Rx timers (for instance in custom `INotify` or other services), you may still need a small physical await (e.g., `await Task.Delay(150);`) in your test before assertions, to allow the background threads to complete.

### Verifying Service Calls

We have extension methods to easily verify if Home Assistant services were called.

```csharp
// Verify a specific service call with exact data
ctx.VerifyCallServiceWithData("light", "turn_on", new { entity_id = "light.woonkamer", brightness = 255 }, times: 1);

// Verify any call to a service
ctx.VerifyCallService("scene.turn_on", times: 1);

// Verify NO calls were made
ctx.VerifyNotCallService("notify.mobile_app_vincent_phone");

// Specific helper for notifications
ctx.VerifyCallNotify("notify", "mobile_app_vincent_phone", times: 1);
```

## 📝 Best Practices for Writing Tests

1. **Test Behavior, Not Implementation**: Focus on what the app should do in response to a state change, rather than how it internally processes it.
2. **Setup State First**: Mock the initial `EntityState` returned by `GetState` *before* calling `InitApp`, as the constructor often reads initial states.
3. **Use TestScheduler**: Pass the injected `IScheduler` into your Rx operators (e.g. `.WhenStateIsFor(..., Scheduler)` instead of relying on default system time).
4. **Isolate Assertions**: Use multiple smaller tests over one giant test that tests the whole app lifecycle.

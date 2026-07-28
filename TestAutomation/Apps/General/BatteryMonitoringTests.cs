using System.Text.Json;
using System.Collections.Generic;
using Automation.apps.General;
using Automation.Configuration;
using Automation.Interfaces;
using Automation.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class BatteryMonitoringTests
{
    [Fact]
    public async Task BatteryMonitoring_LowBattery_SendsNotificationAfterInterval()
    {
        var ctx = AppTestContext.NewWithScheduler();
        var config = Options.Create(new AppConfig { BaseUrlHomeAssistant = "http://ha" });
        var appConfig = Options.Create(new AppConfiguration 
        { 
            Battery = new BatteryConfiguration { WarningLevel = 10, CheckInterval = TimeSpan.FromHours(1) } 
        });

        var sensor1 = new EntityState
        {
            EntityId = "sensor.battery_1",
            State = "20",
            AttributesJson = JsonSerializer.SerializeToDocument(new { device_class = "battery", friendly_name = "Sensor 1", unit_of_measurement = "%" }).RootElement
        };

        ctx.HaContext.GetAllEntities().Returns(new List<Entity> { new Entity(ctx.HaContext, "sensor.battery_1") });
        ctx.HaContext.GetState("sensor.battery_1").Returns(sensor1);
        
        var app = ctx.InitApp<BatteryMonitoring>(config, appConfig);

        ctx.ChangeStateFor("sensor.battery_1").FromState("20").ToState("5");
        ctx.HaContextMock.ProcessPendingOperations();
        
        ctx.VerifyNotCallService("notify.mobile_app_vincent_phone");

        ctx.AdvanceTimeBy(TimeSpan.FromHours(1).Ticks + TimeSpan.FromSeconds(1).Ticks);
        ctx.HaContextMock.ProcessPendingOperations();

        await Task.Delay(150);

        ctx.VerifyCallNotify("notify", "mobile_app_vincent_phone", times: 1);
    }
    
    [Fact]
    public async Task BatteryMonitoring_LowBattery_SendsNotificationAfterInterval_OnlyWhenBatteryLevelDecreases()
    {
        var ctx = AppTestContext.NewWithScheduler();
        var config = Options.Create(new AppConfig());
        var appConfig = Options.Create(new AppConfiguration 
        { 
            Battery = new BatteryConfiguration { WarningLevel = 10, CheckInterval = TimeSpan.FromHours(1) } 
        });

        var sensor1 = new EntityState
        {
            EntityId = "sensor.battery_1",
            State = "20",
            AttributesJson = JsonSerializer.SerializeToDocument(new { device_class = "battery", friendly_name = "Sensor 1", unit_of_measurement = "%" }).RootElement
        };

        ctx.HaContext.GetAllEntities().Returns(new List<Entity> { new Entity(ctx.HaContext, "sensor.battery_1") });
        ctx.HaContext.GetState("sensor.battery_1").Returns(sensor1);
        
        var app = ctx.InitApp<BatteryMonitoring>(config, appConfig);

        ctx.ChangeStateFor("sensor.battery_1").FromState("20").ToState("5");
        ctx.HaContextMock.ProcessPendingOperations();
        
        ctx.AdvanceTimeBy(TimeSpan.FromHours(1).Ticks + TimeSpan.FromSeconds(1).Ticks);
        ctx.HaContextMock.ProcessPendingOperations();

        // Simulate an increase in battery, e.g., charging started
        ctx.ChangeStateFor("sensor.battery_1").FromState("5").ToState("15");
        ctx.HaContextMock.ProcessPendingOperations();

        ctx.AdvanceTimeBy(TimeSpan.FromHours(1).Ticks + TimeSpan.FromSeconds(1).Ticks);
        ctx.HaContextMock.ProcessPendingOperations();

        await Task.Delay(150);

        // It should have sent exactly 1 notification (from the first drop)
        ctx.VerifyCallNotify("notify", "mobile_app_vincent_phone", times: 1);
    }
    
    [Fact]
    public async Task BatteryMonitoring_HighBattery_DoesNotSendNotification()
    {
        var ctx = AppTestContext.NewWithScheduler();
        var config = Options.Create(new AppConfig());
        var appConfig = Options.Create(new AppConfiguration 
        { 
            Battery = new BatteryConfiguration { WarningLevel = 10, CheckInterval = TimeSpan.FromHours(1) } 
        });

        var sensor1 = new EntityState
        {
            EntityId = "sensor.battery_1",
            State = "90",
            AttributesJson = JsonSerializer.SerializeToDocument(new { device_class = "battery", friendly_name = "Sensor 1", unit_of_measurement = "%" }).RootElement
        };

        ctx.HaContext.GetAllEntities().Returns(new List<Entity> { new Entity(ctx.HaContext, "sensor.battery_1") });
        ctx.HaContext.GetState("sensor.battery_1").Returns(sensor1);
        
        var app = ctx.InitApp<BatteryMonitoring>(config, appConfig);

        ctx.ChangeStateFor("sensor.battery_1").FromState("90").ToState("100");
        ctx.HaContextMock.ProcessPendingOperations();
        
        ctx.AdvanceTimeBy(TimeSpan.FromHours(1).Ticks + TimeSpan.FromSeconds(1).Ticks);
        ctx.HaContextMock.ProcessPendingOperations();

        await Task.Delay(150);

        ctx.VerifyNotCallService("notify.mobile_app_vincent_phone");
    }

    [Fact]
    public void BatteryMonitoring_BatteryRecovers_DoesNotSendNotification()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        var config = Options.Create(new AppConfig { BaseUrlHomeAssistant = "http://ha" });
        var appConfig = Options.Create(new AppConfiguration 
        { 
            Battery = new BatteryConfiguration { WarningLevel = 10, CheckInterval = TimeSpan.FromHours(1) } 
        });

        var sensor1 = new EntityState
        {
            EntityId = "sensor.battery_1",
            State = "20",
            AttributesJson = JsonSerializer.SerializeToDocument(new { device_class = "battery", friendly_name = "Sensor 1", unit_of_measurement = "%" }).RootElement
        };

        ctx.HaContext.GetAllEntities().Returns(new List<Entity> { new Entity(ctx.HaContext, "sensor.battery_1") });
        ctx.HaContext.GetState("sensor.battery_1").Returns(sensor1);
        
        var app = ctx.InitApp<BatteryMonitoring>(config, appConfig);

        // Act
        ctx.ChangeStateFor("sensor.battery_1").FromState("20").ToState("5");
        ctx.HaContextMock.ProcessPendingOperations();
        
        ctx.AdvanceTimeBy(TimeSpan.FromMinutes(30).Ticks);
        ctx.ChangeStateFor("sensor.battery_1").FromState("5").ToState("15");
        ctx.HaContextMock.ProcessPendingOperations();
        
        ctx.AdvanceTimeBy(TimeSpan.FromMinutes(30).Ticks);
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyNotCallService("notify.mobile_app_vincent_phone");
    }

    [Fact]
    public void BatteryMonitoring_BatteryFull_ResetsNotificationHistory()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        var config = Options.Create(new AppConfig { BaseUrlHomeAssistant = "http://ha" });
        var appConfig = Options.Create(new AppConfiguration 
        { 
            Battery = new BatteryConfiguration { WarningLevel = 10, CheckInterval = TimeSpan.FromHours(1) } 
        });

        var sensor1 = new EntityState
        {
            EntityId = "sensor.battery_1",
            State = "90",
            AttributesJson = JsonSerializer.SerializeToDocument(new { device_class = "battery", friendly_name = "Sensor 1", unit_of_measurement = "%" }).RootElement
        };

        ctx.HaContext.GetAllEntities().Returns(new List<Entity> { new Entity(ctx.HaContext, "sensor.battery_1") });
        ctx.HaContext.GetState("sensor.battery_1").Returns(sensor1);
        
        var app = ctx.InitApp<BatteryMonitoring>(config, appConfig);

        // Act
        ctx.ChangeStateFor("sensor.battery_1").FromState("90").ToState("100");
        ctx.HaContextMock.ProcessPendingOperations();
        
        // Assert
    }
}




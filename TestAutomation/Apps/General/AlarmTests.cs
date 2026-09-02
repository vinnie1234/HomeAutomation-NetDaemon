using Automation.apps.General;
using Automation.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Automation.Interfaces;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class AlarmTests
{
    private readonly AppTestContext _ctx;
    private readonly INotify _notify;
    private readonly ILogger<Alarm> _logger;
    private readonly IEntityManager _entityManager;
    private readonly IOptions<AppConfig> _config;
    
    public AlarmTests()
    {
        _ctx = AppTestContext.NewWithScheduler();
        _notify = Substitute.For<INotify>();
        _logger = Substitute.For<ILogger<Alarm>>();
        _entityManager = Substitute.For<IEntityManager>();
        _config = Options.Create(new AppConfig { BaseUrlHomeAssistant = "http://test", Discord = new DiscordConfig { Logs = "logs" } });

        // Set default states for person models
        _ctx.HaContext.GetState("input_boolean.awayvincent").Returns(new EntityState { EntityId = "input_boolean.awayvincent", State = "off" });
        _ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { EntityId = "input_boolean.awaycarleen", State = "off" });
        _ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "off" });
        _ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { EntityId = "input_boolean.sleepingcarleen", State = "off" });
        
        // Temperature check setup
        _ctx.HaContext.GetState("sensor.badkamer_temperature").Returns(new EntityState { EntityId = "sensor.badkamer_temperature", State = "20" });
        _ctx.HaContext.GetState("sensor.berging_temperature").Returns(new EntityState { EntityId = "sensor.berging_temperature", State = "20" });
        _ctx.HaContext.GetState("sensor.gang_temperature").Returns(new EntityState { EntityId = "sensor.gang_temperature", State = "20" });

        // Energy setup
        _ctx.HaContext.GetState("sensor.p1_meter_3c39e72a64e8_active_power").Returns(new EntityState { EntityId = "sensor.p1_meter_3c39e72a64e8_active_power", State = "1000" });
        
        // Garbage setup
        _ctx.HaContext.GetState("sensor.afval_morgen").Returns(new EntityState { EntityId = "sensor.afval_morgen", State = "Geen" });
        
        // Petsnowy setup
        _ctx.HaContext.GetState("sensor.petsnowy_litterbox_errors").Returns(new EntityState { EntityId = "sensor.petsnowy_litterbox_errors", State = "0" });
        
        // Energy negative check setup
        _ctx.HaContext.GetState("sensor.anwb_electricity_all_in_price_current").Returns(new EntityState { EntityId = "sensor.anwb_electricity_all_in_price_current", State = "10.0" });
        
        // Backup check setup
        _ctx.HaContext.GetState("sensor.backup_last_attempted_automatic_backup").Returns(new EntityState { EntityId = "sensor.backup_last_attempted_automatic_backup", State = DateTime.Now.ToString("O") });
    }

    private Alarm CreateApp()
    {
        return new Alarm(_ctx.HaContext, _logger, _notify, _ctx.Scheduler, _entityManager, _config);
    }

    [Fact]
    public void GangMotion_WhenTurnsOn_AndNobodyHome_SendsAlarm()
    {
        // Arrange
        _ctx.HaContext.GetState("input_boolean.awayvincent").Returns(new EntityState { EntityId = "input_boolean.awayvincent", State = "on" });
        _ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { EntityId = "input_boolean.awaycarleen", State = "on" });
        _ctx.HaContext.GetState("input_boolean.away").Returns(new EntityState { EntityId = "input_boolean.away", State = "on" });
        var app = CreateApp();

        // Act
        _ctx.ChangeStateFor("binary_sensor.gang_motion").FromState("off").ToState("on");
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        _notify.Received(1).NotifyPhoneVincent(
            "ALARM",
            "Beweging gedetecteerd",
            false,
            5,
            channel: "ALARM",
            vibrationPattern: "100, 1000, 100, 1000, 100",
            action: null,
            image: null);
    }

    [Fact]
    public void GangMotion_WhenTurnsOn_AndSomeoneHome_DoesNotSendAlarm()
    {
        // Arrange
        // default state has vincent and carleen at home
        var app = CreateApp();

        // Act
        _ctx.ChangeStateFor("binary_sensor.gang_motion").FromState("off").ToState("on");
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        _notify.DidNotReceiveWithAnyArgs().NotifyPhoneVincent(default!, default!, default!);
    }

    [Fact]
    public void TemperatureCheck_HighTemperature_WhenNotNightMode_SendsNotification()
    {
        // Arrange
        var app = CreateApp();

        // Act
        _ctx.ChangeStateFor("sensor.badkamer_temperature").FromState("20").ToState("26");
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert - temperature always notifies both Vincent and Carleen
        _notify.Received(1).NotifyPhoneVincentCarleen(
            "Hoge temperatuur gedetecteerd",
            "Badkamer is 26 graden",
            true,
            Arg.Any<double?>(),
            channel: "ALARM",
            vibrationPattern: "100, 1000, 100, 1000, 100",
            action: null,
            image: null);
    }

    [Fact]
    public void TemperatureCheck_HighTemperature_WhenNightMode_DoesNotSendNotification()
    {
        // Arrange
        _ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "on" });
        var app = CreateApp();

        // Act
        _ctx.ChangeStateFor("sensor.badkamer_temperature").FromState("20").ToState("26");
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        _notify.DidNotReceiveWithAnyArgs().NotifyPhoneVincent(default!, default!, default!);
    }

    [Fact]
    public void EnergyCheck_HighEnergyFor10Minutes_SendsNotification()
    {
        // Arrange
        var app = CreateApp();

        // Act
        _ctx.ChangeStateFor("sensor.p1_meter_3c39e72a64e8_active_power").FromState("1000").ToState("2500");
        _ctx.HaContextMock.ProcessPendingOperations();
        
        // Fast forward 10 minutes
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(10).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        _notify.Received(1).NotifyPhoneVincent(
            "Hoog energie verbruik",
            "Energie verbruik is al voor 10 minuten 2500",
            true,
            10,
            Arg.Any<List<Automation.Models.ActionModel>>(),
            channel: "ALARM",
            vibrationPattern: "100, 1000, 100, 1000, 100",
            image: null);
    }

    [Fact]
    public void EnergyCheck_HighEnergyForLess10Minutes_DoesNotSendNotification()
    {
        // Arrange
        var app = CreateApp();

        // Act
        _ctx.ChangeStateFor("sensor.p1_meter_3c39e72a64e8_active_power").FromState("1000").ToState("2500");
        _ctx.HaContextMock.ProcessPendingOperations();
        
        // Fast forward 5 minutes, then back to low
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(5).Ticks);
        _ctx.ChangeStateFor("sensor.p1_meter_3c39e72a64e8_active_power").FromState("2500").ToState("1000");
        _ctx.HaContextMock.ProcessPendingOperations();
        
        _ctx.AdvanceTimeBy(TimeSpan.FromMinutes(6).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        _notify.DidNotReceiveWithAnyArgs().NotifyPhoneVincent(default!, default!, default!);
    }

    [Fact]
    public void GarbageCheck_SendsNotification_WhenGarbageNextDay_AndCarleenHome()
    {
        // Arrange
        var app = CreateApp();
        _ctx.HaContext.GetState("sensor.afval_morgen").Returns(new EntityState { EntityId = "sensor.afval_morgen", State = "Restafval" });

        // Act
        _ctx.AdvanceTimeBy(TimeSpan.FromDays(1).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert - Carleen is home (default), so NotifyPhoneVincentCarleen is used
        _notify.Received(1).NotifyPhoneVincentCarleen(
            "Vergeet het afval niet",
            "Vergeet je niet op Restafval buiten te zetten?",
            true,
            sendAfterMinutes: Arg.Any<double?>(),
            action: Arg.Any<List<Automation.Models.ActionModel>>());
    }

    [Fact]
    public void GarbageCheck_SendsNotification_WhenGarbageNextDay_AndCarleenAway()
    {
        // Arrange
        _ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { EntityId = "input_boolean.awaycarleen", State = "on" });
        var app = CreateApp();
        _ctx.HaContext.GetState("sensor.afval_morgen").Returns(new EntityState { EntityId = "sensor.afval_morgen", State = "Restafval" });

        // Act
        _ctx.AdvanceTimeBy(TimeSpan.FromDays(1).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert - Carleen is away, so only NotifyPhoneVincent is used
        _notify.Received(1).NotifyPhoneVincent(
            "Vergeet het afval niet",
            "Vergeet je niet op Restafval buiten te zetten?",
            true,
            sendAfterMinutes: Arg.Any<double?>(),
            action: Arg.Any<List<Automation.Models.ActionModel>>());
    }

    [Fact]
    public void GarbageCheck_DoesNotSendNotification_WhenNoGarbage()
    {
        // Arrange
        var app = CreateApp();
        _ctx.HaContext.GetState("sensor.afval_morgen").Returns(new EntityState { EntityId = "sensor.afval_morgen", State = "Geen" });

        // Act
        _ctx.AdvanceTimeBy(TimeSpan.FromDays(1).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        _notify.DidNotReceiveWithAnyArgs().NotifyPhoneVincent(default!, default!, default!);
    }

    [Fact]
    public void PetSnowyCheck_ErrorsFound_SendsNotification_WhenCarleenHome()
    {
        // Arrange
        var app = CreateApp();
        _ctx.HaContext.GetState("sensor.petsnowy_litterbox_errors").Returns(new EntityState { EntityId = "sensor.petsnowy_litterbox_errors", State = "1" });

        // Act
        _ctx.AdvanceTimeBy(TimeSpan.FromDays(1).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert - Carleen is home (default), so NotifyPhoneVincentCarleen is used
        _notify.Received(1).NotifyPhoneVincentCarleen(
            "PetSnowy heeft errors",
            "Er staat nog een error open voor de PetSnowy",
            false,
            10,
            channel: null,
            vibrationPattern: null,
            image: null,
            action: null);
        _notify.Received(1).NotifyDiscord(
            "PetSnowy heeft errors", 
            Arg.Is<string[]>(t => t.Contains("logs")), 
            Arg.Any<Automation.Models.DiscordNotificationModels.DiscordNotificationModel>());
    }

    [Fact]
    public void PetSnowyCheck_ErrorsFound_SendsNotification_WhenCarleenAway()
    {
        // Arrange
        _ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { EntityId = "input_boolean.awaycarleen", State = "on" });
        var app = CreateApp();
        _ctx.HaContext.GetState("sensor.petsnowy_litterbox_errors").Returns(new EntityState { EntityId = "sensor.petsnowy_litterbox_errors", State = "1" });

        // Act
        _ctx.AdvanceTimeBy(TimeSpan.FromDays(1).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert - Carleen is away, so only NotifyPhoneVincent is used
        _notify.Received(1).NotifyPhoneVincent(
            "PetSnowy heeft errors",
            "Er staat nog een error open voor de PetSnowy",
            false,
            10,
            channel: null,
            vibrationPattern: null,
            image: null,
            action: null);
        _notify.Received(1).NotifyDiscord(
            "PetSnowy heeft errors", 
            Arg.Is<string[]>(t => t.Contains("logs")), 
            Arg.Any<Automation.Models.DiscordNotificationModels.DiscordNotificationModel>());
    }

    [Fact]
    public void EnergyNegativeCheck_WhenPriceIsNegative_SendsNotification_WhenCarleenHome()
    {
        // Arrange
        var app = CreateApp();

        // Act
        _ctx.ChangeStateFor("sensor.anwb_electricity_all_in_price_current").FromState("10.0").ToState("-3.0");
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert - Carleen is home (default), so NotifyPhoneVincentCarleen is used
        _notify.Received(1).NotifyDiscord("ENERGY IS NEGATIEF - -3", Arg.Is<string[]>(t => t.Contains("logs")), null);
        _notify.Received(1).NotifyPhoneVincentCarleen("ENERGY IS NEGATIEF - -3", "Je energy is negatief, dit kan geld kosten.", false, 10, null, null, null, null);
    }

    [Fact]
    public void EnergyNegativeCheck_WhenPriceIsNegative_SendsNotification_WhenCarleenAway()
    {
        // Arrange
        _ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { EntityId = "input_boolean.awaycarleen", State = "on" });
        var app = CreateApp();

        // Act
        _ctx.ChangeStateFor("sensor.anwb_electricity_all_in_price_current").FromState("10.0").ToState("-3.0");
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert - Carleen is away, so only NotifyPhoneVincent is used
        _notify.Received(1).NotifyDiscord("ENERGY IS NEGATIEF - -3", Arg.Is<string[]>(t => t.Contains("logs")), null);
        _notify.Received(1).NotifyPhoneVincent("ENERGY IS NEGATIEF - -3", "Je energy is negatief, dit kan geld kosten.", false, 10, null, null, null, null);
    }

    [Fact]
    public void EnergyNegativeCheck_WhenPriceIsPositive_DoesNotSendNotification()
    {
        // Arrange
        var app = CreateApp();

        // Act
        _ctx.ChangeStateFor("sensor.anwb_electricity_all_in_price_current").FromState("10.0").ToState("5.0");
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        _notify.DidNotReceiveWithAnyArgs().NotifyDiscord(default!, default!, default!);
        _notify.DidNotReceiveWithAnyArgs().NotifyPhoneVincent(default!, default!, default!);
    }

    [Fact]
    public void BackUpCheck_WhenNoRecentBackup_SendsNotification()
    {
        // Arrange
        var app = CreateApp();
        var oldBackup = DateTime.Now.AddDays(-3).ToString("O");
        _ctx.HaContext.GetState("sensor.backup_last_attempted_automatic_backup").Returns(new EntityState { EntityId = "sensor.backup_last_attempted_automatic_backup", State = oldBackup });

        // Act
        _ctx.AdvanceTimeBy(TimeSpan.FromDays(1).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        _notify.Received(1).NotifyDiscord($"Er is al 2 dagen geen backup, laatste backup is van {oldBackup}", Arg.Is<string[]>(t => t.Contains("logs")), null);
    }

    [Fact]
    public void BackUpCheck_WhenNoBackupAtAll_SendsNotification()
    {
        // Arrange
        var app = CreateApp();
        _ctx.HaContext.GetState("sensor.backup_last_attempted_automatic_backup").Returns(new EntityState { EntityId = "sensor.backup_last_attempted_automatic_backup", State = "" });

        // Act
        _ctx.AdvanceTimeBy(TimeSpan.FromDays(1).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        _notify.Received(1).NotifyDiscord("Er is geen  backup", Arg.Is<string[]>(t => t.Contains("logs")), null);
    }

    [Fact]
    public void BackUpCheck_WhenRecentBackup_DoesNotSendNotification()
    {
        // Arrange
        var app = CreateApp();
        var recentBackup = DateTime.Now.AddDays(-1).ToString("O");
        _ctx.HaContext.GetState("sensor.backup_last_attempted_automatic_backup").Returns(new EntityState { EntityId = "sensor.backup_last_attempted_automatic_backup", State = recentBackup });

        // Act
        _ctx.AdvanceTimeBy(TimeSpan.FromDays(1).Ticks);
        _ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        _notify.DidNotReceiveWithAnyArgs().NotifyDiscord(default!, default!, default!);
    }
}


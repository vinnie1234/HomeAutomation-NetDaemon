using System;
using Automation.apps.General;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class HouseStateManagerTests
{
    private void SetupDefaultStates(AppTestContext ctx)
    {
        ctx.HaContext.GetState("sun.sun").Returns(new EntityState { EntityId = "sun.sun", State = "above_horizon" });
        ctx.HaContext.GetState("input_datetime.nighttimeweekdays").Returns(new EntityState { EntityId = "input_datetime.nighttimeweekdays", State = "00:00:00" });
        ctx.HaContext.GetState("input_datetime.nighttimeweekends").Returns(new EntityState { EntityId = "input_datetime.nighttimeweekends", State = "00:30:00" });
        ctx.HaContext.GetState("input_datetime.daytimeweekend").Returns(new EntityState { EntityId = "input_datetime.daytimeweekend", State = "10:00:00" });
        ctx.HaContext.GetState("input_datetime.daytimehomework").Returns(new EntityState { EntityId = "input_datetime.daytimehomework", State = "08:15:00" });
        ctx.HaContext.GetState("input_datetime.daytimeoffice").Returns(new EntityState { EntityId = "input_datetime.daytimeoffice", State = "07:15:00" });
        ctx.HaContext.GetState("input_boolean.holliday").Returns(new EntityState { EntityId = "input_boolean.holliday", State = "off" });
        ctx.HaContext.GetState("input_boolean.working").Returns(new EntityState { EntityId = "input_boolean.working", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { EntityId = "input_boolean.sleepingcarleen", State = "off" });
        ctx.HaContext.GetState("input_select.housemodeselect").Returns(new EntityState { EntityId = "input_select.housemodeselect", State = "Day" });
        ctx.HaContext.GetState("input_boolean.awayvincent").Returns(new EntityState { EntityId = "input_boolean.awayvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { EntityId = "input_boolean.awaycarleen", State = "off" });
    }

    [Fact]
    public void Constructor_SetsEveningState_WhenSunBelowHorizon()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("sun.sun").Returns(new EntityState { EntityId = "sun.sun", State = "below_horizon" });

        // Act
        ctx.InitApp<HouseStateManager>();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Evening");
    }

    [Fact]
    public void Constructor_SetsDayState_WhenSunAboveHorizon()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("sun.sun").Returns(new EntityState { EntityId = "sun.sun", State = "above_horizon" });

        // Act
        ctx.InitApp<HouseStateManager>();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Day");
    }

    [Fact]
    public void Constructor_DoesNotSetState_WhenSunStateUnknown()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("sun.sun").Returns(new EntityState { EntityId = "sun.sun", State = "unknown" });

        // Act
        ctx.InitApp<HouseStateManager>();

        // Assert
        ctx.VerifyNotCallService("input_select.select_option");
    }

    [Fact]
    public void Constructor_DoesNotSetState_WhenSunStateUnavailable()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("sun.sun").Returns(new EntityState { EntityId = "sun.sun", State = "unavailable" });

        // Act
        ctx.InitApp<HouseStateManager>();

        // Assert
        ctx.VerifyNotCallService("input_select.select_option");
    }

    [Fact]
    public void SunGoesDown_SetsEveningState()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.InitApp<HouseStateManager>();

        // Act
        ctx.ChangeStateFor("sun.sun").FromState("above_horizon").ToState("below_horizon");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Evening");
    }

    [Fact]
    public void SunComesUp_SetsMorningState()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("sun.sun").Returns(new EntityState { EntityId = "sun.sun", State = "below_horizon" });
        ctx.InitApp<HouseStateManager>();

        // Act
        ctx.ChangeStateFor("sun.sun").FromState("below_horizon").ToState("above_horizon");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Morning");
    }

    [Fact]
    public void SunStateChange_IgnoredFromUnknown()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("sun.sun").Returns(new EntityState { EntityId = "sun.sun", State = "unknown" });
        ctx.InitApp<HouseStateManager>();

        // Act
        ctx.ChangeStateFor("sun.sun").FromState("unknown").ToState("above_horizon");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyNotCallService("input_select.select_option");
    }

    [Fact]
    public void SunStateChange_IgnoredFromUnavailable()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("sun.sun").Returns(new EntityState { EntityId = "sun.sun", State = "unavailable" });
        ctx.InitApp<HouseStateManager>();

        // Act
        ctx.ChangeStateFor("sun.sun").FromState("unavailable").ToState("below_horizon");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyNotCallService("input_select.select_option");
    }

    [Fact]
    public void SceneActivation_SetsDay_WhenWoonkamerdayActivated()
    {
        // Arrange — use below_horizon so constructor sets Evening first, not Day
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.HaContext.GetState("sun.sun").Returns(new EntityState { EntityId = "sun.sun", State = "below_horizon" });
        ctx.InitApp<HouseStateManager>();
        ctx.HaContext.ClearReceivedCalls();

        // Act
        ctx.ChangeStateFor("scene.woonkamerday").FromState("10:00:00").ToState("10:00:01");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Day");
    }

    [Fact]
    public void SceneActivation_SetsEvening_WhenWoonkamereveningActivated()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.InitApp<HouseStateManager>();

        // Act
        ctx.ChangeStateFor("scene.woonkamerevening").FromState("10:00:00").ToState("10:00:01");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Evening");
    }

    [Fact]
    public void SceneActivation_SetsNight_WhenWoonkamernightActivated()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.InitApp<HouseStateManager>();

        // Act
        ctx.ChangeStateFor("scene.woonkamernight").FromState("10:00:00").ToState("10:00:01");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Night");
    }

    [Fact]
    public void SceneActivation_SetsMorning_WhenWoonkamermorningActivated()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.InitApp<HouseStateManager>();

        // Act
        ctx.ChangeStateFor("scene.woonkamermorning").FromState("10:00:00").ToState("10:00:01");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Morning");
    }

    [Fact]
    public void SleepingVincentTurnsOn_SetsNightState()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.InitApp<HouseStateManager>();

        // Act
        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Night");
    }

    [Fact]
    public void SleepingCarleenTurnsOn_SetsNightState()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        SetupDefaultStates(ctx);
        ctx.InitApp<HouseStateManager>();

        // Act
        ctx.ChangeStateFor("input_boolean.sleepingcarleen").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyInputSelect_SelectOption("input_select.housemodeselect", "Night");
    }
}


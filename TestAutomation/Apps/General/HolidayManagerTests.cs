using Automation.apps.General;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class HolidayManagerTests
{
    [Fact]
    public void HolidayManager_ShouldInitializeWithoutErrors()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        ctx.HaContext.GetState("input_boolean.awayvincent").Returns(new EntityState { EntityId = "input_boolean.awayvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { EntityId = "input_boolean.awaycarleen", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { EntityId = "input_boolean.sleepingcarleen", State = "off" });

        // Act
        var app = ctx.InitApp<HolidayManager>();

        // Assert
        Assert.NotNull(app);
    }

    // Since the methods SetHoliday, SetEndHoliday, and CheckCalenderForHoliday are currently commented out
    // in the source code, we only need to verify that changing the state doesn't crash the app and 
    // nothing unexpected happens.

    [Fact]
    public void HolidayManager_ShouldHandleHollidayTurnedOn()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        ctx.HaContext.GetState("input_boolean.awayvincent").Returns(new EntityState { EntityId = "input_boolean.awayvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { EntityId = "input_boolean.awaycarleen", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { EntityId = "input_boolean.sleepingcarleen", State = "off" });
        ctx.HaContext.GetState("input_boolean.holliday").Returns(new EntityState { EntityId = "input_boolean.holliday", State = "off" });
        ctx.InitApp<HolidayManager>();

        // Act
        ctx.ChangeStateFor("input_boolean.holliday").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        // Currently SetHoliday is commented out, so no actions to verify
        ctx.VerifyNotCallService("notify.notify_phone_vincent");
    }

    [Fact]
    public void HolidayManager_ShouldHandleHollidayTurnedOff()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        ctx.HaContext.GetState("input_boolean.awayvincent").Returns(new EntityState { EntityId = "input_boolean.awayvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { EntityId = "input_boolean.awaycarleen", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { EntityId = "input_boolean.sleepingcarleen", State = "off" });
        ctx.HaContext.GetState("input_boolean.holliday").Returns(new EntityState { EntityId = "input_boolean.holliday", State = "on" });
        ctx.InitApp<HolidayManager>();

        // Act
        ctx.ChangeStateFor("input_boolean.holliday").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        // Currently SetEndHoliday is commented out, so no actions to verify
        ctx.VerifyNotCallService("notify.notify_phone_vincent");
    }
}



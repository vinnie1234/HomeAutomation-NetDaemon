using System.Text.Json;
using Automation.apps.General;
using HomeAssistantGenerated;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class HolidayManagerTests
{
    private static void SetPersonModelDefaults(AppTestContext ctx)
    {
        ctx.HaContext.GetState("input_boolean.awayvincent").Returns(new EntityState { EntityId = "input_boolean.awayvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { EntityId = "input_boolean.awaycarleen", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { EntityId = "input_boolean.sleepingcarleen", State = "off" });
    }

    [Fact]
    public void HolidayManager_ShouldInitializeWithoutErrors()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        SetPersonModelDefaults(ctx);

        // Act
        var app = ctx.InitApp<HolidayManager>();

        // Assert
        Assert.NotNull(app);
    }

    [Fact]
    public void HolidayManager_HollidayTurnedOn_NoAlarmSet_DoesNotSendNotification()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        SetPersonModelDefaults(ctx);
        ctx.HaContext.GetState("input_boolean.holliday").Returns(new EntityState { EntityId = "input_boolean.holliday", State = "off" });
        ctx.InitApp<HolidayManager>();

        // Act - sensor.hub_vincent_alarms has no attributes configured, so next_alarm_status is unknown
        ctx.ChangeStateFor("input_boolean.holliday").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyNotCallService("notify.mobile_app_vincent_phone");
    }

    [Fact]
    public void HolidayManager_HollidayTurnedOn_AlarmSet_SendsReminderToDisableAlarm()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        SetPersonModelDefaults(ctx);
        ctx.HaContext.GetState("input_boolean.holliday").Returns(new EntityState { EntityId = "input_boolean.holliday", State = "off" });
        ctx.HaContext.GetState("sensor.hub_vincent_alarms").Returns(new EntityState
        {
            EntityId = "sensor.hub_vincent_alarms",
            State = "2",
            AttributesJson = JsonSerializer.SerializeToElement(new
            {
                next_alarm_status = "set",
                alarms = new[]
                {
                    new { alarm_id = "2", status = "set", local_time = "08:30" },
                    new { alarm_id = "1", status = "set", local_time = "07:00" }
                }
            })
        });
        ctx.InitApp<HolidayManager>();

        // Act
        ctx.ChangeStateFor("input_boolean.holliday").FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert - reminded about the earliest alarm that's still set
        ctx.HaContext.Received(1).CallService("notify", "mobile_app_vincent_phone", null,
            Arg.Is<NotifyMobileAppVincentPhoneParameters>(p =>
                p.Title == "WEKKER UITZETTEN" &&
                p.Message == "Je moet je wekker nog uit zetten voor 07:00"));
    }

    [Fact]
    public void HolidayManager_HollidayTurnedOff_AlarmNotInactive_DoesNotSendNotification()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        SetPersonModelDefaults(ctx);
        ctx.HaContext.GetState("input_boolean.holliday").Returns(new EntityState { EntityId = "input_boolean.holliday", State = "on" });
        ctx.InitApp<HolidayManager>();

        // Act - sensor.hub_vincent_alarms has no attributes configured, so next_alarm_status is unknown
        ctx.ChangeStateFor("input_boolean.holliday").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyNotCallService("notify.mobile_app_vincent_phone");
    }

    [Fact]
    public void HolidayManager_HollidayTurnedOff_AlarmInactive_SendsReminderToEnableAlarm()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        SetPersonModelDefaults(ctx);
        ctx.HaContext.GetState("input_boolean.holliday").Returns(new EntityState { EntityId = "input_boolean.holliday", State = "on" });
        ctx.HaContext.GetState("sensor.hub_vincent_alarms").Returns(new EntityState
        {
            EntityId = "sensor.hub_vincent_alarms",
            State = "0",
            AttributesJson = JsonSerializer.SerializeToElement(new { next_alarm_status = "inactive" })
        });
        ctx.InitApp<HolidayManager>();

        // Act
        ctx.ChangeStateFor("input_boolean.holliday").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.HaContext.Received(1).CallService("notify", "mobile_app_vincent_phone", null,
            Arg.Is<NotifyMobileAppVincentPhoneParameters>(p =>
                p.Title == "WEKKER AANZETTEN" &&
                p.Message == "Helaas moet je je wekker nog aanzetten :("));
    }

    [Fact]
    public void HolidayManager_DailyCheck_CalendarMentionsVrij_TurnsOnHoliday()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        SetPersonModelDefaults(ctx);
        ctx.HaContext.GetState("input_boolean.holliday").Returns(new EntityState { EntityId = "input_boolean.holliday", State = "off" });
        ctx.HaContext.GetState("calendar.vincentmaarschalkerweerd_gmail_com").Returns(new EntityState
        {
            EntityId = "calendar.vincentmaarschalkerweerd_gmail_com",
            State = "on",
            AttributesJson = JsonSerializer.SerializeToElement(new { description = "Lekker een dagje Vrij!" })
        });
        ctx.InitApp<HolidayManager>();

        // Act
        ctx.AdvanceTimeBy(TimeSpan.FromDays(1).Ticks);
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("input_boolean", "turn_on", "holliday");
    }

    [Fact]
    public void HolidayManager_DailyCheck_CalendarHasNoActiveEvent_DoesNotTurnOnHoliday()
    {
        // Arrange - no active calendar event means the "description" attribute isn't present at all
        using var ctx = AppTestContext.NewWithScheduler();
        SetPersonModelDefaults(ctx);
        ctx.HaContext.GetState("input_boolean.holliday").Returns(new EntityState { EntityId = "input_boolean.holliday", State = "off" });
        ctx.HaContext.GetState("calendar.vincentmaarschalkerweerd_gmail_com").Returns(new EntityState
        {
            EntityId = "calendar.vincentmaarschalkerweerd_gmail_com",
            State = "off"
        });
        ctx.InitApp<HolidayManager>();

        // Act
        ctx.AdvanceTimeBy(TimeSpan.FromDays(1).Ticks);
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyNotCallService("input_boolean.turn_on");
    }

    [Fact]
    public void HolidayManager_DailyCheck_CalendarDescriptionUnrelated_DoesNotTurnOnHoliday()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        SetPersonModelDefaults(ctx);
        ctx.HaContext.GetState("input_boolean.holliday").Returns(new EntityState { EntityId = "input_boolean.holliday", State = "off" });
        ctx.HaContext.GetState("calendar.vincentmaarschalkerweerd_gmail_com").Returns(new EntityState
        {
            EntityId = "calendar.vincentmaarschalkerweerd_gmail_com",
            State = "on",
            AttributesJson = JsonSerializer.SerializeToElement(new { description = "Teammeeting" })
        });
        ctx.InitApp<HolidayManager>();

        // Act
        ctx.AdvanceTimeBy(TimeSpan.FromDays(1).Ticks);
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyNotCallService("input_boolean.turn_on");
    }
}

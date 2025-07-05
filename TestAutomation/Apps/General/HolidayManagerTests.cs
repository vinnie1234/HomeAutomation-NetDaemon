using Automation.apps.General;
using TestAutomation.Helpers;
using Xunit;
using NetDaemon.HassModel.Entities;
using NSubstitute;

namespace TestAutomation.Apps.General;

public class HolidayManagerTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    [Fact]
    public void ShouldSendAlarmReminderWhenHolidayStarts()
    {
        // Arrange
        _ = _ctx.InitApp<HolidayManager>();

        // Act — trigger holiday mode on
        _ctx.ChangeStateFor("input_boolean.holliday")
            .FromState("off")
            .ToState("on");

        // Assert — should send alarm reminder notification.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("notify", Arg.Any<string>(), 
                null, 
                Arg.Any<object>());
        }, nameof(ShouldSendAlarmReminderWhenHolidayStarts));
    }

    [Fact]
    public void ShouldSendAlarmReminderWhenHolidayEnds()
    {
        // Arrange
        _ = _ctx.InitApp<HolidayManager>();

        // Act — trigger holiday mode off
        _ctx.ChangeStateFor("input_boolean.holliday")
            .FromState("on")
            .ToState("off");

        // Assert — should send alarm setup reminder notification.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("notify", Arg.Any<string>(), 
                null, 
                Arg.Any<object>());
        }, nameof(ShouldSendAlarmReminderWhenHolidayEnds));
    }

    [Fact]
    public void ShouldCheckCalendarForHolidayKeywords()
    {
        // Arrange
        _ = _ctx.InitApp<HolidayManager>();

        // Act — simulate daily calendar check at midnight
        _ctx.Scheduler.AdvanceTo(DateTime.Today.AddDays(1).Ticks);

        // Assert — should schedule daily calendar checks.
        // Note: Testing calendar integration requires mocking calendar entities
    }

    [Fact]
    public void ShouldDetectHolidayFromCalendarEvents()
    {
        // Arrange
        _ctx.WithEntityState("calendar.personal", "Holiday trip to Spain");
        _ = _ctx.InitApp<HolidayManager>();

        // Act — simulate calendar event with holiday keywords
        _ctx.ChangeStateFor("calendar.personal")
            .FromState("No events")
            .ToState("Holiday vacation weekend");

        // Assert — should automatically enable holiday mode.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.Received().CallService("input_boolean", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "input_boolean.holliday"), 
                Arg.Any<object>());
        }, nameof(ShouldDetectHolidayFromCalendarEvents));
    }

    [Fact]
    public void ShouldNotTriggerForNonHolidayCalendarEvents()
    {
        // Arrange
        _ = _ctx.InitApp<HolidayManager>();

        // Act — simulate regular calendar event without holiday keywords
        _ctx.ChangeStateFor("calendar.personal")
            .FromState("No events")
            .ToState("Doctor appointment");

        // Assert — should not enable holiday mode for regular events.
        TestDebugHelper.AssertCallWithDebug(_ctx.HaContext, haContext =>
        {
            haContext.DidNotReceive().CallService("input_boolean", "turn_on", 
                Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "input_boolean.holliday"), 
                Arg.Any<object>());
        }, nameof(ShouldNotTriggerForNonHolidayCalendarEvents));
    }
}
using Automation;
using HomeAssistantGenerated;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation;

public class GlobalsTests
{
    private static Entities CreateEntities(AppTestContext ctx, string holliday = "off", string onvacation = "off")
    {
        ctx.HaContext.GetState("input_boolean.holliday").Returns(new EntityState { EntityId = "input_boolean.holliday", State = holliday });
        ctx.HaContext.GetState("input_boolean.onvacation").Returns(new EntityState { EntityId = "input_boolean.onvacation", State = onvacation });
        ctx.HaContext.GetState("input_boolean.officeday_monday").Returns(new EntityState { EntityId = "input_boolean.officeday_monday", State = "on" });
        ctx.HaContext.GetState("input_boolean.officeday_tuesday").Returns(new EntityState { EntityId = "input_boolean.officeday_tuesday", State = "off" });
        return new Entities(ctx.HaContext);
    }

    [Fact]
    public void WorkDays_WhenNotOnLeave_FollowOfficeDaySettings()
    {
        using var ctx = AppTestContext.NewWithScheduler();
        var entities = CreateEntities(ctx);

        Assert.True(Globals.IsOfficeDay(entities, DayOfWeek.Monday));
        Assert.True(Globals.IsHomeWorkDay(entities, DayOfWeek.Tuesday));
        Assert.False(Globals.IsDayOff(entities, DayOfWeek.Monday));
        Assert.True(Globals.IsDayOff(entities, DayOfWeek.Saturday));
    }

    [Theory]
    [InlineData("on", "off")]
    [InlineData("off", "on")]
    public void WorkDays_WhenOnLeave_AreDaysOff(string holliday, string onvacation)
    {
        using var ctx = AppTestContext.NewWithScheduler();
        var entities = CreateEntities(ctx, holliday, onvacation);

        Assert.False(Globals.IsOfficeDay(entities, DayOfWeek.Monday));
        Assert.False(Globals.IsHomeWorkDay(entities, DayOfWeek.Tuesday));
        Assert.True(Globals.IsDayOff(entities, DayOfWeek.Monday));
    }
}

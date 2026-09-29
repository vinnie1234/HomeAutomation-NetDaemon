using Automation.apps.General;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class PresenceSimulationTests
{
    private static AppTestContext CreateContext(string onvacation, string houseSitter)
    {
        var ctx = AppTestContext.NewWithScheduler();
        ctx.HaContext.GetState("input_boolean.onvacation").Returns(new EntityState { EntityId = "input_boolean.onvacation", State = onvacation });
        ctx.HaContext.GetState("input_boolean.away").Returns(new EntityState { EntityId = "input_boolean.away", State = "on" });
        ctx.HaContext.GetState("input_boolean.awayvincent").Returns(new EntityState { EntityId = "input_boolean.awayvincent", State = "on" });
        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { EntityId = "input_boolean.awaycarleen", State = "on" });
        ctx.HaContext.GetState("person.timo").Returns(new EntityState { EntityId = "person.timo", State = houseSitter });
        return ctx;
    }

    private static void SunsetAndWholeEveningPasses(AppTestContext ctx)
    {
        ctx.ChangeStateFor("sun.sun").FromState("above_horizon").ToState("below_horizon");
        ctx.AdvanceTimeBy(TimeSpan.FromDays(2).Ticks);
        ctx.HaContextMock.ProcessPendingOperations();
    }

    [Fact]
    public void Sunset_WhenHouseUnattended_TurnsLivingRoomAndBedroomLightsOnAndOff()
    {
        // Arrange
        using var ctx = CreateContext(onvacation: "on", houseSitter: "not_home");
        ctx.InitApp<PresenceSimulation>();

        // Act
        SunsetAndWholeEveningPasses(ctx);

        // Assert
        ctx.VerifyCallService("light", "turn_on", "slaapkamer");
        ctx.VerifyCallService("light", "turn_off", "slaapkamer");
        ctx.VerifyCallService("light", "turn_off", "woonkamer");
    }

    [Theory]
    [InlineData("off", "not_home")] // not on vacation
    [InlineData("on", "home")]      // house sitter is home
    public void Sunset_WhenHouseNotUnattended_DoesNothing(string onvacation, string houseSitter)
    {
        // Arrange
        using var ctx = CreateContext(onvacation, houseSitter);
        ctx.InitApp<PresenceSimulation>();

        // Act
        SunsetAndWholeEveningPasses(ctx);

        // Assert
        ctx.VerifyNotCallService("light.turn_on");
        ctx.VerifyNotCallService("light.turn_off");
    }
}

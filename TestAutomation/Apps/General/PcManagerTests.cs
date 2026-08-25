using NSubstitute;
using Automation.apps.General;
using NetDaemon.HassModel.Entities;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class PcManagerTests 
{
    [Fact]
    public void PcManager_ShouldInitializeWithoutErrors()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();

        // Act
        var app = ctx.InitApp<PcManager>();

        // Assert
        Assert.NotNull(app);
    }

    [Fact]
    public void StartPcButton_ShouldTurnOnAndOffLightsAndTV()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        ctx.HaContext.GetState("input_button.start_pc").Returns(new EntityState { EntityId = "input_button.start_pc", State = "unknown" });
        ctx.InitApp<PcManager>();

        // Act
        ctx.ChangeStateFor("input_button.start_pc").FromState("unknown").ToState("123456");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("light", "turn_on", "bureau", times: 1);
        ctx.VerifyCallService("light", "turn_off", "nachtkastje", times: 1);
        ctx.VerifyCallService("light", "turn_on", "plafondslaapkamer", times: 1);
        ctx.VerifyCallService("media_player", "turn_off", "tv", times: 1);
    }

    [Fact]
    public void VincentPcLaatstopgestartSensor_ShouldTurnOnAndOffLightsAndTV()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        ctx.HaContext.GetState("sensor.vincent_pc_laatstopgestart").Returns(new EntityState { EntityId = "sensor.vincent_pc_laatstopgestart", State = "2023-10-10" });
        ctx.InitApp<PcManager>();

        // Act
        ctx.ChangeStateFor("sensor.vincent_pc_laatstopgestart").FromState("2023-10-10").ToState("2023-10-11");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("light", "turn_on", "bureau", times: 1);
        ctx.VerifyCallService("light", "turn_off", "nachtkastje", times: 1);
        ctx.VerifyCallService("light", "turn_on", "plafondslaapkamer", times: 1);
        ctx.VerifyCallService("media_player", "turn_off", "tv", times: 1);
    }
}


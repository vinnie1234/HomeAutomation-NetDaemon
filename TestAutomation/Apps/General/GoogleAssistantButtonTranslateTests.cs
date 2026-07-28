using Automation.apps.General;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class GoogleAssistantButtonTranslateTests
{
    [Fact]
    public void GoogleAssistantButtonTranslate_ShouldInitializeWithoutErrors()
    {
        // Arrange
        using var ctx = AppTestContext.New();

        // Act
        var app = ctx.InitApp<GoogleAssistantButtonTranslate>();

        // Assert
        Assert.NotNull(app);
    }

    [Theory]
    [InlineData("input_boolean.start_friends", "input_button.start_friends")]
    [InlineData("input_boolean.restartnetdaemon", "input_button.restartnetdaemon")]
    [InlineData("input_boolean.pixelgivenextfeedeary", "input_button.pixelgivenextfeedeary")]
    [InlineData("input_boolean.emptypetsnowy", "input_button.emptypetsnowy")]
    [InlineData("input_boolean.cleanpetsnowy", "input_button.cleanpetsnowy")]
    [InlineData("input_boolean.feedcat", "input_button.feedcat")]
    [InlineData("input_boolean.start_pc", "input_button.start_pc")]
    public void GoogleAssistantButtonTranslate_ShouldTurnOffBooleanAndPressInputButton_WhenBooleanTurnsOn(string booleanEntityId, string buttonEntityId)
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        
        ctx.HaContext.GetState(booleanEntityId).Returns(new EntityState { EntityId = booleanEntityId, State = "off" });
        var app = ctx.InitApp<GoogleAssistantButtonTranslate>();

        // Act
        ctx.ChangeStateFor(booleanEntityId).FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("input_boolean", "turn_off", booleanEntityId.Replace("input_boolean.", ""), times: 1);
        ctx.VerifyCallService("input_button", "press", buttonEntityId.Replace("input_button.", ""), times: 1);
    }

    [Fact]
    public void GoogleAssistantButtonTranslate_ShouldTurnOffBooleanAndPressButton_WhenBooleanTurnsOn()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        var booleanEntityId = "input_boolean.vincent_pc_afsluiten";
        var buttonEntityId = "button.vincent_pc_afsluiten";

        ctx.HaContext.GetState(booleanEntityId).Returns(new EntityState { EntityId = booleanEntityId, State = "off" });
        var app = ctx.InitApp<GoogleAssistantButtonTranslate>();

        // Act
        ctx.ChangeStateFor(booleanEntityId).FromState("off").ToState("on");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("input_boolean", "turn_off", booleanEntityId.Replace("input_boolean.", ""), times: 1);
        ctx.VerifyCallService("button", "press", buttonEntityId.Replace("button.", ""), times: 1);
    }
}


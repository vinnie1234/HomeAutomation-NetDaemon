using Automation.apps.General;
using TestAutomation.Helpers;
using Xunit;
using NetDaemon.HassModel.Entities;
using NSubstitute;

namespace TestAutomation.Apps.General;

public class GoogleAssistantButtonTranslateTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    [Fact]
    public void ShouldTranslateInputBooleanToInputButton()
    {
        // Arrange
        _ = _ctx.InitApp<GoogleAssistantButtonTranslate>();

        // Act — trigger input_boolean state change from Google Assistant
        _ctx.ChangeStateFor("input_boolean.feedcat")
            .FromState("off")
            .ToState("on");

        // Assert — should trigger corresponding input_button.
        _ctx.HaContext.Received().CallService("input_button", "press", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "input_button.feedcat"), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldTranslateVacuumCleaningCommands()
    {
        // Arrange
        _ = _ctx.InitApp<GoogleAssistantButtonTranslate>();

        // Act — trigger vacuum cleaning via Google Assistant
        _ctx.ChangeStateFor("input_boolean.vacuumcleankitchen")
            .FromState("off")
            .ToState("on");

        // Assert — should trigger vacuum cleaning button.
        _ctx.HaContext.Received().CallService("input_button", "press", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "input_button.vacuumcleankitchen"), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldTranslateCatCareCommands()
    {
        // Arrange
        _ = _ctx.InitApp<GoogleAssistantButtonTranslate>();

        // Act — trigger cat litter box cleaning via Google Assistant
        _ctx.ChangeStateFor("input_boolean.cleanpetsnowy")
            .FromState("off")
            .ToState("on");

        // Assert — should trigger litter box cleaning button.
        _ctx.HaContext.Received().CallService("input_button", "press", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "input_button.cleanpetsnowy"), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldTranslateEntertainmentCommands()
    {
        // Arrange
        _ = _ctx.InitApp<GoogleAssistantButtonTranslate>();

        // Act — trigger Friends theme via Google Assistant
        _ctx.ChangeStateFor("input_boolean.start_friends")
            .FromState("off")
            .ToState("on");

        // Assert — should trigger Friends theme button.
        _ctx.HaContext.Received().CallService("input_button", "press", 
            Arg.Is<ServiceTarget>(t => t.EntityIds!.First() == "input_button.start_friends"), 
            Arg.Any<object>());
    }

    [Fact]
    public void ShouldIgnoreInputBooleanWithoutCorrespondingButton()
    {
        // Arrange
        _ = _ctx.InitApp<GoogleAssistantButtonTranslate>();

        // Act — trigger unmapped input_boolean
        _ctx.ChangeStateFor("input_boolean.some_random_toggle")
            .FromState("off")
            .ToState("on");

        // Assert — should not trigger any button press.
        _ctx.HaContext.DidNotReceive().CallService("input_button", "press", 
            Arg.Any<ServiceTarget>(), 
            Arg.Any<object>());
    }
}
using System.Text.Json;
using Automation.apps.General;
using Automation.Interfaces;
using Automation.Models;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class SaveInStateTests
{
    [Fact]
    public async Task InitializeAsync_DoesNotOverwriteTheLightSnapshot()
    {
        // Arrange - Reset still needs the snapshot from before the restart
        using var ctx = AppTestContext.NewWithScheduler();
        var storage = Substitute.For<IDataRepository>();

        // Act
        await ctx.InitAppAsync<SaveInState>(storage);

        // Assert
        storage.DidNotReceive().Save("LightState", Arg.Any<List<LightStateModel>>());
    }

    [Fact]
    public async Task LightChange_SavesTheLightStates_AfterTheLightsSettle()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        var storage = Substitute.For<IDataRepository>();
        ctx.HaContext.GetState("light.bureau").Returns(new EntityState { EntityId = "light.bureau", State = "on" });
        await ctx.InitAppAsync<SaveInState>(storage);

        // Act - the change is processed within the advance, so advance a bit past the 10 seconds
        ctx.ChangeStateFor("light.bureau").FromState("off").ToState("on");
        ctx.AdvanceTimeBy(TimeSpan.FromSeconds(11).Ticks);

        // Assert
        storage.Received(1).Save("LightState", Arg.Is<List<LightStateModel>>(x => x.Any(l => l.EntityId == "light.bureau" && l.IsOn)));
    }

    [Fact]
    public async Task LightChange_KeepsThePreviousState_OfAnUnavailableLight()
    {
        // Arrange - the bureau light was saved as on, and is now unavailable (e.g. Home Assistant restarting)
        using var ctx = AppTestContext.NewWithScheduler();
        var storage = Substitute.For<IDataRepository>();
        storage.Get<List<LightStateModel>>("LightState").Returns(_ => [new("light.bureau", null, 100, 3000, true, null)]);
        ctx.HaContext.GetState("light.bureau").Returns(new EntityState { EntityId = "light.bureau", State = "unavailable" });
        await ctx.InitAppAsync<SaveInState>(storage);

        // Act
        ctx.ChangeStateFor("light.bureau").FromState("on").ToState("unavailable");
        ctx.AdvanceTimeBy(TimeSpan.FromSeconds(11).Ticks);

        // Assert - still saved as on, so Reset doesn't turn it off
        storage.Received(1).Save("LightState", Arg.Is<List<LightStateModel>>(x => x.Single(l => l.EntityId == "light.bureau").IsOn));
    }

    [Fact]
    public async Task InitializeAsync_ShouldSaveAlarmStates()
    {
        // Arrange
        using var ctx = AppTestContext.New();
        var storage = Substitute.For<IDataRepository>();
        
        var attributes = new Dictionary<string, object>
        {
            { "supported_color_modes", new[] { "xy" } },
            { "rgb_color", new[] { 255.0, 128.0, 64.0 } },
            { "brightness", 100.0 }
        };
        
        // Mock a light state to be saved
        ctx.HaContext.GetState("light.bureau").Returns(new EntityState
        {
            EntityId = "light.bureau",
            State = "on",
            AttributesJson = JsonSerializer.SerializeToElement(attributes)
        });

        // Act
        var app = await ctx.InitAppAsync<SaveInState>(storage);

        // Assert
        storage.Received(1).Save("AlarmState", Arg.Any<List<AlarmStateModel?>>());
    }
}


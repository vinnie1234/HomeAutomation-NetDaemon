using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
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
    public async Task InitializeAsync_ShouldSaveLightAndAlarmStates()
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
        storage.Received(1).Save("LightState", Arg.Is<List<LightStateModel>>(x => x.Count > 0));
        storage.Received(1).Save("AlarmState", Arg.Any<List<AlarmStateModel?>>());
    }
}


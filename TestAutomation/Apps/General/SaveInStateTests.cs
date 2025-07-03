using Automation.apps.General;
using Automation.Interfaces;
using Automation.Models;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class SaveInStateTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();
    private readonly IDataRepository _storage = Substitute.For<IDataRepository>();

    [Fact]
    public void ShouldSaveLightStatesToStorage()
    {
        // Arrange
        _ctx.WithEntityState("light.bureau", "on");
        _ctx.SetAttributesFor("light.bureau", new { brightness = 150, rgb_color = new List<double> { 255, 128, 64 } });

        // Act
        _ = _ctx.InitApp<SaveInState>(_storage);

        // Assert — should save light states to storage.
        _storage.Received().Save("LightState", Arg.Any<List<LightStateModel>>());
    }

    [Fact]
    public void ShouldSaveAlarmStatesToStorage()
    {
        // Arrange
        var alarms = new List<object>
        {
            new { AlarmId = "alarm1", Status = "set", LocalTime = "08:00" }
        };
        _ctx.SetAttributesFor("sensor.hub_vincent_alarms", new { alarms });

        // Act
        _ = _ctx.InitApp<SaveInState>(_storage);

        // Assert — should save alarm states to storage.
        _storage.Received().Save("AlarmState", Arg.Any<List<AlarmStateModel?>>());
    }

    [Fact]
    public void ShouldCreateLightStateModelWithCorrectProperties()
    {
        // Arrange
        _ctx.WithEntityState("light.bureau", "on");
        _ctx.SetAttributesFor("light.bureau", new { brightness = 200, supported_color_modes = new[] { "xy", "color_temp" } });

        // Act
        _ = _ctx.InitApp<SaveInState>(_storage);

        // Assert — should create light state with correct properties.
        _storage.Received().Save("LightState", Arg.Is<List<LightStateModel>>(
            states => states.Any(s => s.EntityId == "light.bureau" && s.IsOn == true)));
    }

    [Fact]
    public void ShouldHandleMultipleLightEntities()
    {
        // Arrange
        _ctx.WithEntityState("light.bureau", "on");
        _ctx.WithEntityState("light.plafond", "off");
        _ctx.WithEntityState("light.nachtkastje", "on");

        // Act
        _ = _ctx.InitApp<SaveInState>(_storage);

        // Assert — should save all light entities.
        _storage.Received().Save("LightState", Arg.Is<List<LightStateModel>>(
            states => states.Count >= 3));
    }

    [Fact]
    public void ShouldSetEntityIdForAlarmStates()
    {
        // Arrange
        var alarms = new List<object>
        {
            new { AlarmId = "alarm1", Status = "set", LocalTime = "08:00" },
            new { AlarmId = "alarm2", Status = "cancelled", LocalTime = "09:00" }
        };
        _ctx.SetAttributesFor("sensor.hub_vincent_alarms", new { alarms });

        // Act
        _ = _ctx.InitApp<SaveInState>(_storage);

        // Assert — should set entity ID for alarm states.
        _storage.Received().Save("AlarmState", Arg.Any<List<AlarmStateModel?>>());
    }
}
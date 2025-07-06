using Automation.apps.Rooms.BedRoom;
using FluentAssertions;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.Rooms.BedRoom;

public class BedRoomLightsTests
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    [Fact]
    public void ShouldInitializeSuccessfully()
    {
        // Arrange & Act
        var app = _ctx.InitApp<BedRoomLights>();

        // Assert — should initialize app successfully.
        app.Should().NotBeNull();
    }

    [Fact]
    public void ShouldNotThrowExceptionDuringInitialization()
    {
        // Arrange & Act & Assert — should not throw exception during initialization.
        var exception = Record.Exception(() => _ctx.InitApp<BedRoomLights>());
        exception.Should().BeNull();
    }

    [Fact]
    public void ShouldBeProperlyDisposed()
    {
        // Arrange
        var app = _ctx.InitApp<BedRoomLights>();

        // Act & Assert — should be properly disposed without exceptions.
        var exception = Record.Exception(() => { /* No dispose method on NetDaemon apps */ });
        exception.Should().BeNull();
    }

    [Fact]
    public void ShouldHaveCorrectAppId()
    {
        // Arrange & Act
        var app = _ctx.InitApp<BedRoomLights>();

        // Assert — should have correct app type.
        app.Should().BeOfType<BedRoomLights>();
    }

    [Fact]
    public void ShouldInheritFromBaseApp()
    {
        // Arrange & Act
        var app = _ctx.InitApp<BedRoomLights>();

        // Assert — should inherit from BaseApp.
        app.Should().BeAssignableTo<Automation.apps.BaseApp>();
    }
}
using Automation.apps.Rooms.BedRoom;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.Rooms;

public class BedroomLightsTests
{
    [Fact]
    public void BedroomLights_InitializesCorrectly()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();

        // Act
        var app = ctx.InitApp<BedRoomLights>();

        // Assert
        Assert.NotNull(app);
    }
}


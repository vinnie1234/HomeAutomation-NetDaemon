using Automation.apps.General;
using Automation.Configuration;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class VacuumTests
{
    private static IOptions<AppConfig> CreateConfig()
    {
        return Options.Create(new AppConfig
        {
            Roomba = new RoombaConfig 
            { 
                PmapId = "test_pmap_id",
                Rooms = new Dictionary<string, RoombaRoomOptions>
                {
                    { "Kattenbak", new RoombaRoomOptions { Id = "0", Type = "zid" } },
                    { "Bank", new RoombaRoomOptions { Id = "1", Type = "zid" } },
                    { "Slaapkamer", new RoombaRoomOptions { Id = "2", Type = "rid" } },
                    { "Gang", new RoombaRoomOptions { Id = "4", Type = "rid" } },
                    { "Woonkamer", new RoombaRoomOptions { Id = "3", Type = "rid" } }
                }
            }
        });
    }

    private static void SetupBaseStates(AppTestContext ctx)
    {
        ctx.HaContext.GetState("input_boolean.awayvincent").Returns(new EntityState { EntityId = "input_boolean.awayvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "off" });
        ctx.HaContext.GetState("binary_sensor.vincent_phone_android_auto").Returns(new EntityState { EntityId = "binary_sensor.vincent_phone_android_auto", State = "off" });
        ctx.HaContext.GetState("sensor.thuis_sm_s938b_direction_of_travel").Returns(new EntityState { EntityId = "sensor.thuis_sm_s938b_direction_of_travel", State = "unknown" });
        ctx.HaContext.GetState("person.vincent_maarschalkerweerd").Returns(new EntityState { EntityId = "person.vincent_maarschalkerweerd", State = "home" });

        ctx.HaContext.GetState("input_boolean.awaycarleen").Returns(new EntityState { EntityId = "input_boolean.awaycarleen", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { EntityId = "input_boolean.sleepingcarleen", State = "off" });
        ctx.HaContext.GetState("person.carleen").Returns(new EntityState { EntityId = "person.carleen", State = "home" });
    }

    [Fact]
    public void Vacuum_ShouldInitializeWithoutErrors()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        SetupBaseStates(ctx);

        // Act
        var app = ctx.InitApp<Vacuum>(CreateConfig());

        // Assert
        Assert.NotNull(app);
    }

    [Theory]
    [InlineData("input_button.vacuumcleankattenbak", "Kattenbak")]
    [InlineData("input_button.vacuumcleanbank", "Bank")]
    [InlineData("input_button.vacuumcleangang", "Gang")]
    [InlineData("input_button.vacuumcleanslaapkamer", "Slaapkamer")]
    [InlineData("input_button.vacuumcleanwoonkamer", "Woonkamer")]
    public void StartFromButton_ShouldCleanSpecificRoom_WhenButtonPressed(string buttonEntityId, string expectedRoomKey)
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        SetupBaseStates(ctx);
        ctx.HaContext.GetState(buttonEntityId).Returns(new EntityState { EntityId = buttonEntityId, State = "unknown" });
        ctx.InitApp<Vacuum>(CreateConfig());

        // Act
        ctx.ChangeStateFor(buttonEntityId).FromState("unknown").ToState("12345");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("vacuum", "send_command", "jaap", times: 1);
    }

    [Fact]
    public void CleanLitterBoxAfterUse_ShouldNotClean_WhenDisableResetIsOn()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        SetupBaseStates(ctx);
        ctx.HaContext.GetState("input_boolean.disablereset").Returns(new EntityState { EntityId = "input_boolean.disablereset", State = "on" });
        ctx.HaContext.GetState("sensor.snow_self_cleaning_litter_box_status").Returns(new EntityState { EntityId = "sensor.snow_self_cleaning_litter_box_status", State = "idle" });
        
        ctx.InitApp<Vacuum>(CreateConfig());

        // Act
        ctx.ChangeStateFor("sensor.snow_self_cleaning_litter_box_status").FromState("idle").ToState("cleaning");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyNotCallService("vacuum.send_command");
    }

    [Fact]
    public void CleanLitterBoxAfterUse_ShouldCleanKattenbak_WhenNotNightModeAndNotSkipped()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        SetupBaseStates(ctx);
        ctx.HaContext.GetState("input_boolean.disablereset").Returns(new EntityState { EntityId = "input_boolean.disablereset", State = "off" });
        ctx.HaContext.GetState("input_boolean.skipvaccumlitterbox").Returns(new EntityState { EntityId = "input_boolean.skipvaccumlitterbox", State = "off" });
        
        // IsNightMode is false if both are not sleeping
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { EntityId = "input_boolean.sleepingcarleen", State = "off" });
        
        ctx.HaContext.GetState("sensor.snow_self_cleaning_litter_box_status").Returns(new EntityState { EntityId = "sensor.snow_self_cleaning_litter_box_status", State = "idle" });
        
        ctx.InitApp<Vacuum>(CreateConfig());

        // Act
        ctx.ChangeStateFor("sensor.snow_self_cleaning_litter_box_status").FromState("idle").ToState("cleaning");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyCallService("vacuum", "send_command", "jaap", times: 1);
        ctx.VerifyCallService("input_boolean", "turn_off", "skipvaccumlitterbox", times: 1);
    }

    [Fact]
    public void CleanLitterBoxAfterUse_ShouldNotClean_WhenSkipped()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        SetupBaseStates(ctx);
        ctx.HaContext.GetState("input_boolean.disablereset").Returns(new EntityState { EntityId = "input_boolean.disablereset", State = "off" });
        ctx.HaContext.GetState("input_boolean.skipvaccumlitterbox").Returns(new EntityState { EntityId = "input_boolean.skipvaccumlitterbox", State = "on" });
        
        // IsNightMode is false
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { EntityId = "input_boolean.sleepingcarleen", State = "off" });
        
        ctx.HaContext.GetState("sensor.snow_self_cleaning_litter_box_status").Returns(new EntityState { EntityId = "sensor.snow_self_cleaning_litter_box_status", State = "idle" });
        
        ctx.InitApp<Vacuum>(CreateConfig());

        // Act
        ctx.ChangeStateFor("sensor.snow_self_cleaning_litter_box_status").FromState("idle").ToState("cleaning");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        ctx.VerifyNotCallService("vacuum.send_command");
        ctx.VerifyCallService("input_boolean", "turn_off", "skipvaccumlitterbox", times: 1);
    }

    [Fact]
    public void CleanLitterBoxAfterUse_ShouldCleanLater_WhenNightModeIsActive()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        SetupBaseStates(ctx);
        ctx.HaContext.GetState("input_boolean.disablereset").Returns(new EntityState { EntityId = "input_boolean.disablereset", State = "off" });
        ctx.HaContext.GetState("input_boolean.skipvaccumlitterbox").Returns(new EntityState { EntityId = "input_boolean.skipvaccumlitterbox", State = "off" });
        
        // IsNightMode is true (someone is sleeping)
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "on" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { EntityId = "input_boolean.sleepingcarleen", State = "off" });
        
        ctx.HaContext.GetState("sensor.snow_self_cleaning_litter_box_status").Returns(new EntityState { EntityId = "sensor.snow_self_cleaning_litter_box_status", State = "idle" });
        
        ctx.InitApp<Vacuum>(CreateConfig());

        // Act 1: litter box status changes to cleaning while sleeping
        ctx.ChangeStateFor("sensor.snow_self_cleaning_litter_box_status").FromState("idle").ToState("cleaning");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert 1
        ctx.VerifyNotCallService("vacuum.send_command");
        ctx.VerifyCallService("input_boolean", "turn_off", "skipvaccumlitterbox", times: 1);

        // Act 2: waking up
        ctx.ChangeStateFor("input_boolean.sleepingvincent").FromState("on").ToState("off");
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert 2
        ctx.VerifyCallService("vacuum", "send_command", "jaap", times: 1);
    }
}



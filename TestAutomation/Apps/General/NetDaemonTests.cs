using Automation.Configuration;
using Automation.Interfaces;
using Microsoft.Extensions.Options;
using NetDaemon.HassModel.Entities;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class NetDaemonTests
{
    private static IOptions<AppConfig> CreateConfig()
    {
        return Options.Create(new AppConfig
        {
            Discord = new DiscordConfig { Logs = "logs_channel" }
        });
    }

    [Fact]
    public async Task InitializeAsync_ShouldRestoreKoelkastColorAndNotify_WhenStorageHasColor()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        var storage = Substitute.For<IDataRepository>();
        var color = new List<double> { 255, 0, 0 };
        storage.Get<IReadOnlyList<double>>("NetDaemonRestart").Returns(color);
        
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { EntityId = "input_boolean.sleepingcarleen", State = "off" });

        // Act
        var app = await ctx.InitAppAsync<Automation.apps.General.NetDaemon>(storage, CreateConfig());
        await Task.Delay(100);

        // Assert
        ctx.VerifyCallService("light", "turn_on", "koelkast", times: 1);
        storage.Received().Save("NetDaemonRestart", (IReadOnlyList<double>?)null);
        
        ctx.VerifyCallServiceWithData("tts", "cloud_say", null, new HomeAssistantGenerated.TtsCloudSayParameters { EntityId = "media_player.hele_huis", Message = "Het huis is opnieuw opgestart" });
        ctx.VerifyCallNotify("notify", "discord_homeassistant", times: 1);
    }

    [Fact]
    public async Task InitializeAsync_ShouldNotNotifyHouse_WhenSomeoneIsSleeping()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        var storage = Substitute.For<IDataRepository>();
        
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "on" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { EntityId = "input_boolean.sleepingcarleen", State = "off" });
        storage.Get<IReadOnlyList<double>>("NetDaemonRestart").Returns((IReadOnlyList<double>)null);

        // Act
        var app = await ctx.InitAppAsync<Automation.apps.General.NetDaemon>(storage, CreateConfig());
        await Task.Delay(100);

        // Assert
        ctx.VerifyCallNotify("notify", "discord_homeassistant", times: 1); 
    }

    [Fact]
    public async Task RestartNetdaemonButton_ShouldTriggerRestartSequence()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        var storage = Substitute.For<IDataRepository>();
        
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { EntityId = "input_boolean.sleepingcarleen", State = "off" });

        var app = await ctx.InitAppAsync<Automation.apps.General.NetDaemon>(storage, CreateConfig());

        ctx.HaContext.GetState("input_button.restartnetdaemon").Returns(new EntityState { EntityId = "input_button.restartnetdaemon", State = "unknown" });

        // Reset the mock to clear initialization calls
        ctx.HaContext.ClearReceivedCalls();

        // Act
        // Create state change for button press
        ctx.ChangeStateFor("input_button.restartnetdaemon").FromState("unknown").ToState("12345");
        ctx.HaContextMock.ProcessPendingOperations();
        await Task.Delay(100);

        ctx.AdvanceTimeBy(TimeSpan.FromSeconds(5).Ticks);

        // Assert
        storage.Received().Save("NetDaemonRestart", Arg.Any<object>());
        ctx.VerifyCallService("light", "turn_on", "koelkast", times: 1);
        ctx.VerifyCallServiceWithData("tts", "cloud_say", null, new HomeAssistantGenerated.TtsCloudSayParameters { EntityId = "media_player.hele_huis", Message = "Het huis wordt opnieuw opgestart" });
        ctx.VerifyCallNotify("hassio", "addon_restart", times: 1);
    }
    
    [Fact]
    public async Task Dispose_ShouldNotifyDiscord()
    {
        // Arrange
        using var ctx = AppTestContext.NewWithScheduler();
        var storage = Substitute.For<IDataRepository>();
        
        ctx.HaContext.GetState("input_boolean.sleepingvincent").Returns(new EntityState { EntityId = "input_boolean.sleepingvincent", State = "off" });
        ctx.HaContext.GetState("input_boolean.sleepingcarleen").Returns(new EntityState { EntityId = "input_boolean.sleepingcarleen", State = "off" });

        var app = await ctx.InitAppAsync<Automation.apps.General.NetDaemon>(storage, CreateConfig());
        ctx.HaContext.ClearReceivedCalls();

        // Act
        app.Dispose();
        await Task.Delay(100);

        // Assert
        ctx.VerifyCallNotify("notify", "discord_homeassistant", times: 2); // 1 from init, 1 from dispose
    }
}




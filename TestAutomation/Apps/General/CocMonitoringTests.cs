using Automation.apps.General;
using Automation.Configuration;
using Automation.Interfaces;
using Automation.Models.COC;
using Microsoft.Extensions.Options;
using NSubstitute;
using TestAutomation.Helpers;
using Xunit;

namespace TestAutomation.Apps.General;

public class CocMonitoringTests
{
    [Fact]
    public void Scheduler_TriggersAt7AM_SavesRunTime_WhenNoTweets()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        var storage = Substitute.For<IDataRepository>();
        var config = Options.Create(new AppConfig 
        { 
            Discord = new DiscordConfig { COC = "coc_channel" },
            Twitter = new TwitterConfig { BearerToken = "fake_token" }
        });


        // We assume last run time is empty
        storage.Get<string>("COC_LAST_RUN_TIME").Returns((string)null);
        storage.Get<List<COCModel>>("COC_TWEET_ID_LIST").Returns(new List<COCModel>());
        
        var app = ctx.InitApp<CocMonitoring>(storage, config);

        // Act
        // Advance to 07:00:00 to trigger the morning schedule
        ctx.AdvanceTimeBy(TimeSpan.FromHours(7).Ticks);
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        // Since we have a fake token, twitter will return 401, so it shouldn't save new tweets
        // But it MUST save the last run time
        storage.Received().Save("COC_LAST_RUN_TIME", Arg.Is<string>(s => true));
    }

    [Fact]
    public void Scheduler_TriggersAt7AM_SkipsIfAlreadyRanAt7AM()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        var storage = Substitute.For<IDataRepository>();
        var config = Options.Create(new AppConfig 
        { 
            Discord = new DiscordConfig { COC = "coc_channel" },
            Twitter = new TwitterConfig { BearerToken = "fake_token" }
        });

        var initialTime = new DateTime(2023, 1, 1, 6, 59, 0);
        ctx.SetCurrentTime(initialTime);

        // Set that it already ran at 7 AM today
        storage.Get<string>("COC_LAST_RUN_TIME").Returns("2023-01-01 07:15:00");
        
        var app = ctx.InitApp<CocMonitoring>(storage, config);

        // Act
        ctx.AdvanceTimeBy(TimeSpan.FromMinutes(1).Ticks);

        // Assert
        // It shouldn't save the run time again because it's skipped
        storage.DidNotReceive().Save("COC_LAST_RUN_TIME", Arg.Any<string>());
    }
    
    [Fact]
    public void Scheduler_TriggersAt7PM_RunsIfLastRunWas7AM()
    {
        // Arrange
        var ctx = AppTestContext.NewWithScheduler();
        var storage = Substitute.For<IDataRepository>();
        var config = Options.Create(new AppConfig 
        { 
            Discord = new DiscordConfig { COC = "coc_channel" },
            Twitter = new TwitterConfig { BearerToken = "fake_token" }
        });


        // Last run was 7 AM today
        storage.Get<string>("COC_LAST_RUN_TIME").Returns("2023-01-01 07:00:00");
        
        var app = ctx.InitApp<CocMonitoring>(storage, config);

        // Act
        ctx.AdvanceTimeBy(TimeSpan.FromHours(19).Ticks);
        ctx.HaContextMock.ProcessPendingOperations();

        // Assert
        // Should run again at 19:00
        storage.Received().Save("COC_LAST_RUN_TIME", Arg.Is<string>(s => true));
    }
}





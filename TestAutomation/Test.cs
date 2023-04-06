using Automation.apps;
using Automation.apps.General;
using HomeAssistantGenerated;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TestAutomation.Helpers;

namespace TestAutomation;

public class Test
{
    private readonly AppTestContext _ctx = AppTestContext.New();

    [Fact]
    public void test()
    {
        _ctx.InitHouseManagerApp();
    }
}

public static class HouseStateManagerInstanceExtensions
{
    public static HouseStateManager InitHouseManagerApp(this AppTestContext ctx)
    {
        var loggerMock = Substitute.For<ILogger<HouseStateManager>>();
        return new HouseStateManager(ctx.HaContext, ctx.Scheduler, loggerMock,
            new Notify(ctx.HaContext));
    }
}
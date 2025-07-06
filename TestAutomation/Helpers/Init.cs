using Automation.apps;
using Automation.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace TestAutomation.Helpers;

public static class Init
{
    public static T InitApp<T>(this AppTestContext ctx, params object[] additionalParams) where T : BaseApp
    {
        var logger = Substitute.For<ILogger<T>>();
        var parameters = new object[] { ctx.HaContext, logger, ctx.Notify, ctx.Scheduler }.Concat(additionalParams).ToArray();
        return (T)Activator.CreateInstance(typeof(T), parameters)!;
    }
    
    public static T InitAppWithStorage<T>(this AppTestContext ctx, params object[] additionalParams) where T : BaseApp
    {
        var logger = Substitute.For<ILogger<T>>();
        var dataRepository = Substitute.For<IDataRepository>();
        var parameters = new object[] { ctx.HaContext, logger, ctx.Notify, ctx.Scheduler, dataRepository }.Concat(additionalParams).ToArray();
        return (T)Activator.CreateInstance(typeof(T), parameters)!;
    }
}
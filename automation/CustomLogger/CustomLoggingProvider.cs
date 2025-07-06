using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Automation.CustomLogger;

public static class CustomLoggingProvider
{
    public static IHostBuilder UseCustomLogging(this IHostBuilder builder)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.Discord()
            .CreateLogger();
        
        return builder.UseSerilog(logger);
    }
}
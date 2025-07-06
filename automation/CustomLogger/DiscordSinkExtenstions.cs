using Serilog;
using Serilog.Configuration;
using Serilog.Events;

namespace Automation.CustomLogger;

public static class DiscordSinkExtensions
{
    public static LoggerConfiguration Discord(
        this LoggerSinkConfiguration loggerConfiguration,
        IFormatProvider? formatProvider = null,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose)
    {
        return loggerConfiguration.Sink(
            new DiscordLogger(formatProvider,restrictedToMinimumLevel));
    }
}
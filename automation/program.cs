using System.Reflection;
using Automation;
using Microsoft.Extensions.Hosting;
using NetDaemon.Extensions.Scheduler;
using NetDaemon.Extensions.Tts;
using NetDaemon.Runtime;

#pragma warning disable CA1812

//dotnet tool run nd-codegen
//dotnet publish -c Release -o ./Release
//[Focus]

try
{
    await Host.CreateDefaultBuilder(args)
        .UseNetDaemonAppSettings()
        .UseCustomLogging()
        .UseNetDaemonRuntime()
        .UseNetDaemonTextToSpeech()
        .AddAppServices()
        .ConfigureServices((_, services) =>
            services
                .AddAppsFromAssembly(Assembly.GetExecutingAssembly())
                .AddNetDaemonStateManager()
                .AddNetDaemonScheduler()
        )
        .Build()
        .RunAsync()
        .ConfigureAwait(false);
}
catch (Exception e)
{
    Console.WriteLine($"Failed to start host... {e}");
    throw;
}
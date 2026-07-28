using System.Reflection;
using Automation;
using Automation.Configuration;
using Automation.CustomLogger;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetDaemon.Extensions.Tts;
using NetDaemon.Extensions.MqttEntityManager;
using NetDaemon.Runtime;

#pragma warning disable CA1812

//dotnet tool run nd-codegen
//dotnet publish -c Release -o ./Release
//[Focus]

try
{
    Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
    
    await Host.CreateDefaultBuilder(args)
        .UseCustomLogging()
        .UseNetDaemonAppSettings()
        .UseNetDaemonRuntime()
        .UseNetDaemonTextToSpeech()
        .UseNetDaemonMqttEntityManagement()
        .AddAppServices()
        .ConfigureAppConfiguration(config =>
            config.AddJsonFile("config.json", optional: false, reloadOnChange: false))
        .ConfigureServices((context, services) =>
        {
            services.Configure<AppConfig>(context.Configuration);
            services.Configure<AppConfiguration>(context.Configuration.GetSection("AppConfiguration"));
            services
                .AddAppsFromAssembly(Assembly.GetExecutingAssembly())
                .AddNetDaemonStateManager()
                .AddNetDaemonScheduler()
                .AddHomeAssistantGenerated();
        })
        .Build()
        .RunAsync()
        .ConfigureAwait(false);
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to start host... {ex}");
    throw;
}
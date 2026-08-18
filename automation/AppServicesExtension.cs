using System.IO;
using System.Reactive.Concurrency;
using Automation.apps;
using Automation.Configuration;
using Automation.Repository;
using Automation.Core;
using Automation.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NetDaemon.Extensions.MqttEntityManager;

namespace Automation;

internal static class AppServicesExtension
{
    public static IHostBuilder AddAppServices(this IHostBuilder hostBuilder)
    {
        return hostBuilder.ConfigureServices((_, services) =>
        {
            services.AddSingleton<IDataRepository>(provider => new DataRepository(
                    Path.Combine(
                        Directory.GetCurrentDirectory(),
                        ".storage"),
                    provider.GetRequiredService<ILogger<DataRepository>>()))
                .AddScoped<INotify>(provider =>
                    new Notify(provider.GetRequiredService<IHaContext>(), provider.GetRequiredService<IDataRepository>(), provider.GetRequiredService<ILogger<Notify>>()))
                .AddScoped<ICircadianLightingService>(provider =>
                    new CircadianLightingService(
                        provider.GetRequiredService<IHaContext>(),
                        provider.GetRequiredService<IOptions<AppConfiguration>>(),
                        provider.GetRequiredService<ILogger<CircadianLightingService>>()))
                .AddScoped<ILivingRoomPresenceService>(provider =>
                    new LivingRoomPresenceService(
                        provider.GetRequiredService<IHaContext>(),
                        provider.GetRequiredService<IScheduler>(),
                        provider.GetRequiredService<IOptions<AppConfiguration>>(),
                        provider.GetRequiredService<ILogger<LivingRoomPresenceService>>()))
                .AddScoped<ISpotcast>(provider => new Spotcast(provider.GetRequiredService<IHaContext>()))
                .AddScoped<IEntityManager>(provider =>
                    new EntityManager(
                        provider.GetRequiredService<IMqttEntityManager>(),
                        provider.GetRequiredService<IHaContext>(), 
                        provider.GetRequiredService<ILogger<EntityManager>>()));
        });
    }
}
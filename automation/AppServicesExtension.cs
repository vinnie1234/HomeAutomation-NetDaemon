using System.IO;
using Automation.apps;
using Automation.Helpers;
using Automation.Repository;
using Automation.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
                .AddSingleton<INotify>(provider =>
                    new Notify(GenericHelpers.GetHaContext(provider), provider.GetRequiredService<IDataRepository>(), provider.GetRequiredService<ILogger<Notify>>()))
                .AddSingleton<ISpotcast>(provider => new Spotcast(GenericHelpers.GetHaContext(provider)))
                .AddSingleton<IEntityManager>(provider =>
                    new EntityManager(
                        provider.GetRequiredService<IMqttEntityManager>(),
                        GenericHelpers.GetHaContext(provider), 
                        provider.GetRequiredService<ILogger<EntityManager>>()));
        });
    }
}
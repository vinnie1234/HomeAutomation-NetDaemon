using System.IO;
using Automation.apps;
using Automation.Helpers;
using Automation.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
                    new Notify(GenericHelpers.GetHaContext(provider), provider.GetRequiredService<IDataRepository>()));
        });
    }
}
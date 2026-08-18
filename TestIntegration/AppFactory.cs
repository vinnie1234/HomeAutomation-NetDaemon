using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace TestIntegration;

public class AppFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// WebApplicationFactory points the content root at the automation project folder, where the
    /// real appsettings.json and config.json live. Those files are gitignored because they hold
    /// live tokens, so they are missing on a fresh clone and on CI, which makes the host fail to
    /// start on the non-optional config.json. Using the test output folder as content root picks
    /// up the committed, secret-free fixtures of this project instead.
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseContentRoot(AppContext.BaseDirectory);
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override configuration to prevent real connections if needed
            config.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("AppConfiguration:Discord:WebhookUrl", "http://localhost/dummy"),
                new KeyValuePair<string, string?>("BaseUrlHomeAssistant", "http://localhost:8123")
            });
        });

        builder.ConfigureServices(services =>
        {
            // Find the NetDaemon Runtime hosted service and remove it so it doesn't attempt to connect to HA during our tests
            var hostedServices = services
                .Where(d => d.ServiceType == typeof(IHostedService))
                .ToList();

            foreach (var descriptor in hostedServices)
                if (descriptor.ImplementationType != null && descriptor.ImplementationType.Name.Contains("NetDaemonRuntime"))
                    services.Remove(descriptor);
        });
    }
}

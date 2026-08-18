using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace TestIntegration;

public class AppFactory : WebApplicationFactory<Program>
{
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

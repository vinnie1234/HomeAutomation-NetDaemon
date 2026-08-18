using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NetDaemon.AppModel;
using System.Reflection;

namespace TestIntegration;

public class StartupTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public StartupTests(AppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthy()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 200-299
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Healthy");
    }

    [Fact]
    public async Task DIContainer_ShouldResolveAllNetDaemonApps()
    {
        // Arrange
        var assembly = Assembly.Load("Automation");
        var appTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.GetCustomAttribute<NetDaemonAppAttribute>() != null)
            .ToList();
            
        await using var scope = _factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        // Act & Assert
        foreach (var appType in appTypes)
        {
            var constructors = appType.GetConstructors();
            foreach (var constructor in constructors)
            foreach (var parameter in constructor.GetParameters())
                try
                {
                    var service = provider.GetRequiredService(parameter.ParameterType);
                    service.Should().NotBeNull();
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to resolve parameter '{parameter.Name}' of type '{parameter.ParameterType.Name}' for app '{appType.Name}'.", ex);
                }
        }
    }
}

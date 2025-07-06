using Microsoft.Extensions.DependencyInjection;

namespace Automation.Helpers;

/// <summary>
/// Provides generic helper methods for the automation project.
/// </summary>
public static class GenericHelpers
{
    /// <summary>
    /// Retrieves an instance of <see cref="IHaContext"/> from the specified service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider to retrieve the <see cref="IHaContext"/> from.</param>
    /// <returns>An instance of <see cref="IHaContext"/>.</returns>
    public static IHaContext GetHaContext(IServiceProvider serviceProvider)
    {
        var scope = serviceProvider.CreateScope();
        var haContext = scope.ServiceProvider.GetRequiredService<IHaContext>();

        return haContext;
    }
}
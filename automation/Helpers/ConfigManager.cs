using System.IO;

namespace Automation.Helpers;

/// <summary>
/// Provides methods to manage and retrieve configuration values from a JSON file.
/// Only used by DiscordLogger which operates outside the DI container.
/// For all other config access, use IOptions&lt;AppConfig&gt; via dependency injection.
/// </summary>
public static class ConfigManager
{
    /// <summary>
    /// Retrieves a nested value from the configuration file based on the specified keys.
    /// </summary>
    /// <param name="firstKey">The first key of the nested configuration value to retrieve.</param>
    /// <param name="secondKey">The second key of the nested configuration value to retrieve.</param>
    /// <returns>The nested configuration value as a string, or null if the keys are not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the keys are not found in the configuration file.</exception>
    public static string? GetValueFromConfigNested(string firstKey, string secondKey)
    {
        using var doc = GetJson();
        return doc.RootElement.TryGetProperty(firstKey, out var parent)
            && parent.TryGetProperty(secondKey, out var value)
            ? value.GetString()
            : throw new InvalidOperationException($"Can't find config {firstKey} - {secondKey}");
    }

    /// <summary>
    /// Reads and parses the JSON configuration file.
    /// </summary>
    /// <returns>A <see cref="JsonDocument"/> representing the parsed JSON configuration.</returns>
    private static JsonDocument GetJson()
    {
        var json = File.ReadAllText("config.json");
        return JsonDocument.Parse(json);
    }
}
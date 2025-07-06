using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Automation.Helpers;

/// <summary>
/// Provides methods to manage and retrieve configuration values from a JSON file.
/// </summary>
public static class ConfigManager
{
    /// <summary>
    /// Retrieves a value from the configuration file based on the specified key.
    /// </summary>
    /// <param name="key">The key of the configuration value to retrieve.</param>
    /// <returns>The configuration value as a string, or null if the key is not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the key is not found in the configuration file.</exception>
    public static string? GetValueFromConfig(string key)
    {
        var json = GetJson();
        return (json?[key] ?? throw new InvalidOperationException($"Can't find config for {key}")).Value<string>();
    }

    /// <summary>
    /// Retrieves a nested value from the configuration file based on the specified keys.
    /// </summary>
    /// <param name="firstKey">The first key of the nested configuration value to retrieve.</param>
    /// <param name="secondKey">The second key of the nested configuration value to retrieve.</param>
    /// <returns>The nested configuration value as a string, or null if the keys are not found.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the keys are not found in the configuration file.</exception>
    public static string? GetValueFromConfigNested(string firstKey, string secondKey)
    {
        var json = GetJson();
        return (json?[firstKey]?[secondKey] ?? throw new InvalidOperationException($"Can't find config {firstKey} - {secondKey}")).Value<string>();
    }

    /// <summary>
    /// Reads and parses the JSON configuration file.
    /// </summary>
    /// <returns>A <see cref="JObject"/> representing the parsed JSON configuration, or null if the file cannot be read.</returns>
    private static JObject? GetJson()
    {
        using var stream = File.OpenRead("config.json");
        var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return (JObject?)JsonConvert.DeserializeObject(json);
    }
}
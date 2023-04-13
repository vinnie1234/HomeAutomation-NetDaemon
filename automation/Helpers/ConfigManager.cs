using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Automation.Helpers;

public static class ConfigManager
{
    public static string? GetValueFromConfig(string key)
    {
        var json = GetJson();
        return (json?[key] ?? throw new InvalidOperationException("Can't find config")).Value<string>();
    }
    
    private static JObject? GetJson()
    {
        using var stream = File.OpenRead($"config.json");
        var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return (JObject?)JsonConvert.DeserializeObject(json);
    }
}
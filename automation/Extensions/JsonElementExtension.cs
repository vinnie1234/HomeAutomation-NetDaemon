namespace Automation.Extensions;

/// <summary>
/// Provides extension methods for <see cref="JsonElement"/>.
/// </summary>
public static class JsonElementExtension
{
    /// <summary>
    /// Converts a <see cref="JsonElement"/> to an object of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the object to convert to.</typeparam>
    /// <param name="element">The <see cref="JsonElement"/> to convert.</param>
    /// <returns>The converted object of type <typeparamref name="T"/>.</returns>
    public static T? ToObject<T>(this JsonElement element)
    {
        var json = element.GetRawText();
        return JsonSerializer.Deserialize<T>(json);
    }
}
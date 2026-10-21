namespace Automation.Models.Helpers;

/// <summary>
/// A helper that should exist in Home Assistant, such as an input_boolean or a counter.
/// </summary>
/// <param name="Domain">The helper domain, e.g. <c>input_boolean</c> or <c>counter</c>.</param>
/// <param name="Id">The id of the helper, which is also the object id of its entity (<c>input_boolean.{Id}</c>).</param>
/// <param name="Name">The name; Home Assistant derives the id from it when it creates the helper.</param>
/// <param name="Settings">The other settings, with the keys Home Assistant uses (<c>icon</c>, <c>min</c>, <c>options</c>, ...).</param>
public record HelperDefinition(string Domain, string Id, string Name, IReadOnlyDictionary<string, object> Settings);

namespace Automation.Models.Helpers;

/// <summary>
/// A helper that should exist in Home Assistant, such as an input_boolean or a counter.
/// </summary>
/// <param name="Domain">The helper domain, e.g. <c>input_boolean</c> or <c>counter</c>.</param>
/// <param name="Id">The id Home Assistant stores the helper under. Usually the object id of its entity too, but not always:
/// an entity renamed in Home Assistant keeps its old id (e.g. id <c>sleeping</c> for <c>input_boolean.sleepingvincent</c>).</param>
/// <param name="Name">The name; Home Assistant derives the id from it when it creates the helper.</param>
/// <param name="Settings">The other settings, with the keys Home Assistant uses (<c>icon</c>, <c>min</c>, <c>options</c>, ...).</param>
/// <param name="CopyFrom">An older entity whose value the helper takes over once it is created, when it replaces that one.</param>
/// <param name="CopyMap">Translates the value of <paramref name="CopyFrom"/>, e.g. <c>on</c> to <c>Kantoor</c>; unknown values are copied as they are.</param>
public record HelperDefinition(
    string Domain,
    string Id,
    string Name,
    IReadOnlyDictionary<string, object> Settings,
    string? CopyFrom = null,
    IReadOnlyDictionary<string, string>? CopyMap = null);

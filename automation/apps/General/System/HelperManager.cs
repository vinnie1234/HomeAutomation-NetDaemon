using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Models.Helpers;
using Microsoft.Extensions.Options;
using NetDaemon.Client;

namespace Automation.apps.General;

/// <summary>
/// Makes sure the helpers in <see cref="HelperDefinitions"/> exist in Home Assistant. When NetDaemon starts it lists the
/// helpers per domain over the websocket (<c>{domain}/list</c>) and creates the missing ones (<c>{domain}/create</c>), the
/// same way the Home Assistant UI does. Existing helpers are left alone, so changes made in the UI stay, with two
/// exceptions: an input_select gets the options it misses added at the end (e.g. a new theme), its other options and
/// order stay; and an <c>initial</c> that the definition doesn't have is removed, because a helper with <c>initial</c>
/// goes back to that value at every restart of Home Assistant (that is how the work schedule kept getting lost).
/// </summary>
[NetDaemonApp(Id = nameof(HelperManager))]
public class HelperManager : BaseApp
{
    private readonly IHomeAssistantRunner _runner;

    public HelperManager(
        IHaContext ha,
        ILogger<HelperManager> logger,
        INotify notify,
        IScheduler scheduler,
        IOptions<AppConfig> config,
        IHomeAssistantRunner runner)
        : base(ha, logger, notify, scheduler, config)
    {
        _runner = runner;
        _ = EnsureHelpersAsync(HelperDefinitions.All);
    }

    /// <summary>
    /// Creates the helpers that don't exist yet. A domain that can't be listed is skipped, so nothing is created twice.
    /// </summary>
    /// <param name="helpers">The helpers that should exist.</param>
    /// <returns>The helpers that were created.</returns>
    public async Task<IReadOnlyList<HelperDefinition>> EnsureHelpersAsync(IReadOnlyList<HelperDefinition> helpers)
    {
        var created = new List<HelperDefinition>();
        if (_runner.CurrentConnection is not { } connection)
        {
            Logger.LogWarning("Not connected to Home Assistant, the helpers are not checked");
            return created;
        }

        foreach (var domain in helpers.GroupBy(x => x.Domain))
        {
            IReadOnlyList<HelperItem> existing;
            try
            {
                existing = await connection.SendCommandAndReturnResponseAsync<ListHelpersCommand, IReadOnlyList<HelperItem>>(
                    new ListHelpersCommand(domain.Key), CancellationToken.None) ?? [];
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not list the {Domain} helpers, skipping them", domain.Key);
                continue;
            }

            var ids = existing.Select(x => x.Id).ToHashSet();
            foreach (var item in existing) await UpdateExistingAsync(connection, domain.FirstOrDefault(x => x.Id == item.Id), item);

            foreach (var helper in domain.Where(x => !ids.Contains(x.Id)))
            {
                try
                {
                    var item = await connection.SendCommandAndReturnResponseAsync<CreateHelperCommand, HelperItem>(
                        new CreateHelperCommand(helper), CancellationToken.None);
                    created.Add(helper);

                    // Home Assistant derives the id from the name; a helper renamed in the UI gets a different id when it is created again
                    if (item != null && item.Id != helper.Id)
                        Logger.LogWarning("Created {Domain} helper {Name} as {CreatedId} instead of {Id}; rename it or update HelperDefinitions",
                            helper.Domain, helper.Name, item.Id, helper.Id);
                    else
                        Logger.LogInformation("Created {Domain}.{Id}", helper.Domain, helper.Id);

                    if (helper.CopyFrom != null || helper.StartValue != null) SetStartValue(helper, $"{helper.Domain}.{item?.Id ?? helper.Id}");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Could not create {Domain}.{Id}", helper.Domain, helper.Id);
                }
            }
        }

        return created;
    }

    /// <summary>
    /// Brings the existing helper <paramref name="item"/> in line with <paramref name="helper"/>: adds the options an
    /// input_select misses and removes an <c>initial</c> the definition doesn't have. Home Assistant replaces all settings
    /// with an update, so the current settings are sent along.
    /// </summary>
    private async Task UpdateExistingAsync(IHomeAssistantConnection connection, HelperDefinition? helper, HelperItem item)
    {
        if (helper == null) return;
        var settings = (item.OtherSettings ?? []).ToDictionary(x => x.Key, x => (object)x.Value);
        settings["name"] = item.Name;
        var changes = new List<string>();

        if (helper.Domain == "input_select" && item.Options != null && helper.Settings.TryGetValue("options", out var value))
        {
            var missing = ((IEnumerable<string>)value).Except(item.Options).ToList();
            settings["options"] = item.Options.Concat(missing).ToList();
            if (missing.Count > 0) changes.Add($"added {string.Join(", ", missing)}");
        }

        if (settings.ContainsKey("initial") && !helper.Settings.ContainsKey("initial"))
        {
            settings.Remove("initial");
            changes.Add("removed initial (it reset the value at every restart)");
        }

        if (changes.Count == 0) return;
        try
        {
            await connection.SendCommandAndReturnResponseAsync<UpdateHelperCommand, HelperItem>(
                new UpdateHelperCommand(helper.Domain, item.Id, settings), CancellationToken.None);
            Logger.LogInformation("{Domain}.{Id}: {Changes}", helper.Domain, item.Id, string.Join("; ", changes));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not update {Domain}.{Id} ({Changes})", helper.Domain, item.Id, string.Join("; ", changes));
        }
    }

    /// <summary>
    /// Gives a newly created helper the value of the entity it replaces, translated with <see cref="HelperDefinition.CopyMap"/>,
    /// or else its <see cref="HelperDefinition.StartValue"/>.
    /// </summary>
    private void SetStartValue(HelperDefinition helper, string entityId)
    {
        var copied = helper.CopyFrom == null ? null : HaContext.GetState(helper.CopyFrom)?.State;
        if (copied is "unknown" or "unavailable") copied = null;
        if (copied != null && helper.CopyMap?.TryGetValue(copied, out var mapped) == true) copied = mapped;
        var value = copied ?? helper.StartValue;
        if (value == null)
        {
            Logger.LogInformation("{CopyFrom} has no value to copy to {EntityId}", helper.CopyFrom, entityId);
            return;
        }

        var target = ServiceTarget.FromEntity(entityId);
        switch (helper.Domain)
        {
            case "input_boolean":
                HaContext.CallService("input_boolean", value == "on" ? "turn_on" : "turn_off", target);
                break;
            case "input_select":
                HaContext.CallService("input_select", "select_option", target, new { option = value });
                break;
            case "input_datetime":
                HaContext.CallService("input_datetime", "set_datetime", target, new { time = value });
                break;
            case "input_number":
                HaContext.CallService("input_number", "set_value", target, new { value });
                break;
            default:
                Logger.LogWarning("Setting a value on a {Domain} helper is not supported", helper.Domain);
                return;
        }

        Logger.LogInformation("Set {EntityId} to {Value} ({Source})", entityId, value, copied != null ? $"from {helper.CopyFrom}" : "start value");
    }
}

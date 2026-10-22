using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Models.Helpers;
using Microsoft.Extensions.Options;
using NetDaemon.Client;

namespace Automation.apps.General;

/// <summary>
/// Makes sure the helpers in <see cref="HelperDefinitions"/> exist in Home Assistant. When NetDaemon starts it lists the
/// helpers per domain over the websocket (<c>{domain}/list</c>) and creates the missing ones (<c>{domain}/create</c>), the
/// same way the Home Assistant UI does. Existing helpers are left alone, so changes made in the UI stay; only an
/// input_select gets the options it misses added at the end (e.g. a new theme), its other options and order stay.
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
            foreach (var item in existing) await AddMissingOptionsAsync(connection, domain.FirstOrDefault(x => x.Id == item.Id), item);

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

                    if (helper.CopyFrom != null) CopyValue(helper, $"{helper.Domain}.{item?.Id ?? helper.Id}");
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
    /// Adds the options of <paramref name="helper"/> that the existing input_select <paramref name="item"/> doesn't have yet.
    /// </summary>
    private async Task AddMissingOptionsAsync(IHomeAssistantConnection connection, HelperDefinition? helper, HelperItem item)
    {
        if (helper?.Domain != "input_select" || item.Options == null || !helper.Settings.TryGetValue("options", out var value)) return;
        var missing = ((IEnumerable<string>)value).Except(item.Options).ToList();
        if (missing.Count == 0) return;

        try
        {
            await connection.SendCommandAndReturnResponseAsync<UpdateHelperCommand, HelperItem>(
                new UpdateHelperCommand(helper.Domain, item.Id, new Dictionary<string, object>
                {
                    // Home Assistant wants the name with every update; the other settings it keeps as they are
                    ["name"] = item.Name,
                    ["options"] = item.Options.Concat(missing).ToList()
                }),
                CancellationToken.None);
            Logger.LogInformation("Added {Options} to {Domain}.{Id}", string.Join(", ", missing), helper.Domain, item.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not add {Options} to {Domain}.{Id}", string.Join(", ", missing), helper.Domain, item.Id);
        }
    }

    /// <summary>
    /// Gives a newly created helper the value of the entity it replaces, translated with <see cref="HelperDefinition.CopyMap"/>.
    /// </summary>
    private void CopyValue(HelperDefinition helper, string entityId)
    {
        var value = HaContext.GetState(helper.CopyFrom!)?.State;
        if (value is null or "unknown" or "unavailable")
        {
            Logger.LogInformation("{CopyFrom} has no value to copy to {EntityId}", helper.CopyFrom, entityId);
            return;
        }

        if (helper.CopyMap?.TryGetValue(value, out var mapped) == true) value = mapped;
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
                Logger.LogWarning("Copying a value to a {Domain} helper is not supported", helper.Domain);
                return;
        }

        Logger.LogInformation("Copied {Value} from {CopyFrom} to {EntityId}", value, helper.CopyFrom, entityId);
    }
}

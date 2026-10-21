using System.Reactive.Concurrency;
using Automation.Configuration;
using Automation.Models.Helpers;
using Microsoft.Extensions.Options;
using NetDaemon.Client;

namespace Automation.apps.General;

/// <summary>
/// Makes sure the helpers in <see cref="HelperDefinitions"/> exist in Home Assistant. When NetDaemon starts it lists the
/// helpers per domain over the websocket (<c>{domain}/list</c>) and creates the missing ones (<c>{domain}/create</c>), the
/// same way the Home Assistant UI does. Existing helpers are left alone, so changes made in the UI stay.
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
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Could not create {Domain}.{Id}", helper.Domain, helper.Id);
                }
            }
        }

        return created;
    }
}

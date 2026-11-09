using System.Text.Json;

namespace Automation.Core;

/// <summary>
/// The actions written to the activity log while nobody was listening yet (NetDaemon is starting). It keeps the first few hundred and is
/// handed over once, to the recorder; after that it stays empty and takes nothing, so it does not grow while NetDaemon runs.
/// </summary>
public sealed class ActivityBacklog
{
    private const int Max = 500;
    private readonly object _lock = new();
    private List<JsonElement>? _items = [];

    /// <summary>Keeps an action until the recorder takes them over (the oldest go when it is full).</summary>
    public void Add(JsonElement entry)
    {
        lock (_lock)
        {
            if (_items == null) return;
            if (_items.Count >= Max) _items.RemoveAt(0);
            _items.Add(entry);
        }
    }

    /// <summary>Hands over everything kept so far, once; from then on nothing is kept.</summary>
    public List<JsonElement> Drain()
    {
        lock (_lock)
        {
            var items = _items ?? [];
            _items = null;
            return items;
        }
    }
}

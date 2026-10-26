using System.Collections.Concurrent;
using System.IO;

namespace Automation.Repository;

/// <summary>
/// Provides methods for data storage and retrieval with in-memory caching.
/// The repository is shared by all apps (singleton), so it caches the JSON instead of the object:
/// every <see cref="Get{T}"/> returns its own copy, and apps can't change each other's data in place.
/// </summary>
public class DataRepository : IDataRepository
{
    private readonly string _dataStoragePath;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, string> _cache;
    private readonly object _fileLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRepository"/> class.
    /// </summary>
    /// <param name="dataStoragePath">The path where data will be stored.</param>
    /// <param name="logger">The logger to use for logging errors.</param>
    public DataRepository(string dataStoragePath, ILogger logger)
    {
        _dataStoragePath = dataStoragePath;
        _logger = logger;
        _cache = new ConcurrentDictionary<string, string>();
    }

    /// <summary>
    /// Retrieves data of type <typeparamref name="T"/> from storage.
    /// Uses an in-memory cache to avoid disk reads.
    /// </summary>
    /// <typeparam name="T">The type of data to retrieve.</typeparam>
    /// <param name="id">The identifier of the data to retrieve.</param>
    /// <returns>A copy of the retrieved data, or null if the data does not exist.</returns>
    public T? Get<T>(string id) where T : class
    {
        try
        {
            if (!_cache.TryGetValue(id, out var json))
            {
                var storageJsonFile = Path.Combine(_dataStoragePath, $"{id}_store.json");

                lock (_fileLock)
                {
                    if (!File.Exists(storageJsonFile))
                        return null;

                    json = File.ReadAllText(storageJsonFile);
                }

                _cache[id] = json;
            }

            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error getting storage file {Id}, error message: {Error}", id, ex.Message);
        }

        return null;
    }

    /// <summary>
    /// Saves data of type <typeparamref name="T"/> to storage and updates the cache.
    /// </summary>
    /// <typeparam name="T">The type of data to save.</typeparam>
    /// <param name="id">The identifier of the data to save.</param>
    /// <param name="data">The data to save.</param>
    public void Save<T>(string id, T data)
    {
        try
        {
            var jsonContent = JsonSerializer.Serialize(data);
            var storageJsonFile = Path.Combine(_dataStoragePath, $"{id}_store.json");

            lock (_fileLock)
            {
                Directory.CreateDirectory(_dataStoragePath);
                File.WriteAllText(storageJsonFile, jsonContent);
                _cache[id] = jsonContent;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error saving storage file {Id}, error message: {Error}", id, ex.Message);
        }
    }
}

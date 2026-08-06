using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using Automation.Interfaces;
using Microsoft.Extensions.Logging;

namespace Automation.Repository;

/// <summary>
/// Provides methods for data storage and retrieval with in-memory caching.
/// </summary>
public class DataRepository : IDataRepository
{
    private readonly string _dataStoragePath;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, object> _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRepository"/> class.
    /// </summary>
    /// <param name="dataStoragePath">The path where data will be stored.</param>
    /// <param name="logger">The logger to use for logging errors.</param>
    public DataRepository(string dataStoragePath, ILogger logger)
    {
        _dataStoragePath = dataStoragePath;
        _logger = logger;
        _cache = new ConcurrentDictionary<string, object>();
    }

    /// <summary>
    /// Retrieves data of type <typeparamref name="T"/> from storage.
    /// Uses an in-memory cache to avoid disk reads.
    /// </summary>
    /// <typeparam name="T">The type of data to retrieve.</typeparam>
    /// <param name="id">The identifier of the data to retrieve.</param>
    /// <returns>The retrieved data, or null if the data does not exist.</returns>
    public T? Get<T>(string id) where T : class
    {
        try
        {
            if (_cache.TryGetValue(id, out var cachedObj))
            {
                return cachedObj as T;
            }

            var storageJsonFile = Path.Combine(_dataStoragePath, $"{id}_store.json");

            if (!File.Exists(storageJsonFile))
                return null;

            var jsonContent = File.ReadAllText(storageJsonFile);
            var parsedObj = JsonSerializer.Deserialize<T>(jsonContent);
            
            if (parsedObj != null)
            {
                _cache[id] = parsedObj;
            }
            
            return parsedObj;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error getting storage file {Id}, error message: {Error}", id, ex.Message);
        }

        return default;
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
            if (data != null)
            {
                _cache[id] = data;
            }
            else
            {
                // Never leave a stale cached value behind when the stored value is cleared.
                _cache.TryRemove(id, out _);
            }

            var storageJsonFile = Path.Combine(_dataStoragePath, $"{id}_store.json");
            Directory.CreateDirectory(_dataStoragePath);

            var jsonContent = JsonSerializer.Serialize(data);
            File.WriteAllText(storageJsonFile, jsonContent);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error saving storage file {Id}, error message: {Error}", id, ex.Message);
        }
    }
}
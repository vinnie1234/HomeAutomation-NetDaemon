using System.IO;

namespace Automation.Repository;

/// <summary>
/// Provides methods for data storage and retrieval.
/// </summary>
public class DataRepository : IDataRepository
{
    private readonly string _dataStoragePath;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRepository"/> class.
    /// </summary>
    /// <param name="dataStoragePath">The path where data will be stored.</param>
    /// <param name="logger">The logger to use for logging errors.</param>
    public DataRepository(string dataStoragePath, ILogger logger)
    {
        _dataStoragePath = dataStoragePath;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves data of type <typeparamref name="T"/> from storage.
    /// </summary>
    /// <typeparam name="T">The type of data to retrieve.</typeparam>
    /// <param name="id">The identifier of the data to retrieve.</param>
    /// <returns>The retrieved data, or null if the data does not exist.</returns>
    public T? Get<T>(string id) where T : class
    {
        try
        {
            var storageJsonFile = Path.Combine(_dataStoragePath, $"{id}_store.json");

            if (!File.Exists(storageJsonFile))
                return null;

            var jsonContent = File.ReadAllText(storageJsonFile);
            return JsonSerializer.Deserialize<T>(jsonContent);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error getting storage file {Id}, error message: {Error}", id, ex.Message);
        }

        return default;
    }

    /// <summary>
    /// Saves data of type <typeparamref name="T"/> to storage.
    /// </summary>
    /// <typeparam name="T">The type of data to save.</typeparam>
    /// <param name="id">The identifier of the data to save.</param>
    /// <param name="data">The data to save.</param>
    public void Save<T>(string id, T data)
    {
        try
        {
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
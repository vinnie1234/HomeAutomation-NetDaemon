namespace Automation.Interfaces;

public interface IDataRepository
{
    void Save<T>(string id, T data);
    T? Get<T>(string id) where T : class;
}
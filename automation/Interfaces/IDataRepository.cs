using System.Threading.Tasks;

namespace Automation.Interfaces;

public interface IDataRepository
{
    Task SaveAsync<T>(string id, T data);
    Task<T?> GetAsync<T>(string id) where T : class;
}
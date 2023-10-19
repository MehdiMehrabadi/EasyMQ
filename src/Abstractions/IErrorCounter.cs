using System.Threading.Tasks;

namespace EasyMQ.Abstractions
{
    public interface IErrorCounter
    {
        Task<int> GetTryCountAsync(string messageId);
        Task UpdateTryCountAsync(string messageId, int tryCount);
        Task KillCounterAsync(string messageId);
    }
}
    
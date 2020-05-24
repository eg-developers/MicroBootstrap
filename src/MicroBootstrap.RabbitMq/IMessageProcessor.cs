using System.Threading.Tasks;

namespace MicroBootstrap.RabbitMq
{
    public interface IMessageProcessor
    {
        Task<bool> TryProcessAsync(string id);
        Task RemoveAsync(string id);
    }
}
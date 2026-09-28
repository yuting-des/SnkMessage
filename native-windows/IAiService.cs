using System.Threading;
using System.Threading.Tasks;

namespace SnkMessage
{
    internal interface IAiService
    {
        Task<AiResult> GenerateAsync(AiRequest request, CancellationToken cancellationToken);
    }
}

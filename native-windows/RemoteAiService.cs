using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SnkMessage
{
    internal sealed class RemoteAiService : IAiService
    {
        private static readonly HttpClient Client = new HttpClient();
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        private readonly Uri endpoint;

        public RemoteAiService(Uri endpoint)
        {
            this.endpoint = endpoint;
        }

        public async Task<AiResult> GenerateAsync(AiRequest request, CancellationToken cancellationToken)
        {
            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = JsonContent.Create(request, options: JsonOptions)
                };
                message.Headers.Add("X-Request-Id", request.RequestId);

                using HttpResponseMessage response = await Client.SendAsync(message, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    throw new AiServiceException("算法服务暂时不可用（" + (int)response.StatusCode + "）。");

                AiResult result = await response.Content.ReadFromJsonAsync<AiResult>(JsonOptions, cancellationToken);
                if (result == null) throw new AiServiceException("算法服务返回了空结果。");
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (AiServiceException)
            {
                throw;
            }
            catch (Exception error)
            {
                throw new AiServiceException("无法连接算法服务。", error);
            }
        }
    }
}

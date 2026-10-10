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
                if ((int)response.StatusCode == 504)
                    throw new AiServiceException("模型响应超时，请重新尝试。");
                if (!response.IsSuccessStatusCode)
                {
                    string payload=await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new AiServiceException(FriendlyError((int)response.StatusCode,ReadErrorCode(payload)));
                }

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

        private static string ReadErrorCode(string payload)
        {
            try
            {
                using JsonDocument document=JsonDocument.Parse(payload);
                return document.RootElement.TryGetProperty("error",out JsonElement error)?error.GetString()??String.Empty:String.Empty;
            }
            catch{return String.Empty;}
        }

        private static string FriendlyError(int status,string code)
        {
            if(code.StartsWith("OPENROUTER_429",StringComparison.Ordinal))return "模型请求较多，请稍后重试。";
            if(code.StartsWith("OPENROUTER_5",StringComparison.Ordinal))return "模型供应商暂时不可用，已自动重试一次。";
            if(code=="OPENROUTER_INVALID_JSON"||code=="INVALID_SUGGESTION_COUNT"||code=="EMPTY_INTERPRETATION")return "模型返回格式异常，请重新生成。";
            if(code=="REQUEST_TOO_LARGE"||code=="INVALID_OCR_IMAGE")return "本次 OCR 扫描区域过大，请缩小微信窗口后重试。";
            if(code.StartsWith("OPENROUTER_400",StringComparison.Ordinal))return "当前模型不接受本次请求格式，请检查模型配置。";
            return "算法服务暂时不可用（"+status+(String.IsNullOrWhiteSpace(code)?String.Empty:"，"+code.Split(':')[0])+"）。";
        }
    }
}

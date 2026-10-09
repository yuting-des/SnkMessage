using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SnkMessage
{
    internal sealed class AiCoordinator : IAiService, IDisposable
    {
        private readonly IAiService inner;
        private readonly TimeSpan timeout;

        public AiCoordinator(IAiService inner, TimeSpan timeout)
        {
            this.inner = inner;
            this.timeout = timeout;
        }

        public async Task<AiResult> GenerateAsync(AiRequest request, CancellationToken cancellationToken)
        {
            ValidateRequest(request);
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            try
            {
                AiResult result = await inner.GenerateAsync(request, timeoutSource.Token);
                return ValidateResult(request.Mode, result);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AiServiceException("生成超时，请重新尝试。");
            }
        }

        private static void ValidateRequest(AiRequest request)
        {
            if (request == null || String.IsNullOrWhiteSpace(request.SelectedText))
                throw new AiServiceException("没有可分析的选中文本。");
            if (request.SelectedText.Length > 6000)
                throw new AiServiceException("选中的内容过长，请缩短后重试。");
        }

        private static AiResult ValidateResult(AiMode mode, AiResult result)
        {
            if (result == null) throw new AiServiceException("算法服务没有返回结果。");

            if (mode == AiMode.Interpret)
            {
                string text = (result.Text ?? String.Empty).Trim();
                if (text.Length == 0) throw new AiServiceException("算法服务没有返回解读内容。");
                if (text.Length > 500) text = text.Substring(0, 500);
                return AiResult.Interpretation(text);
            }

            IEnumerable<string> source = result.Suggestions ?? Array.Empty<string>();
            string[] suggestions = source
                .Where(value => !String.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .Take(3)
                .Select(value => value.Length > 280 ? value.Substring(0, 280) : value)
                .ToArray();
            if (suggestions.Length == 0) throw new AiServiceException("算法服务没有返回可用建议。");
            return AiResult.SuggestionList(suggestions);
        }

        internal void ReloadLocalConfiguration()
        {
            if(inner is LocalAlgorithmAiService local)local.ReloadConfiguration();
        }

        public void Dispose()
        {
            if(inner is IDisposable disposable)disposable.Dispose();
        }
    }

    internal static class AiServiceFactory
    {
        public static IAiService Create()
        {
            string configuredEndpoint = Environment.GetEnvironmentVariable("SNKMESSAGE_AI_ENDPOINT");
            IAiService provider = String.IsNullOrWhiteSpace(configuredEndpoint)
                ? new LocalAlgorithmAiService()
                : new RemoteAiService(ParseEndpoint(configuredEndpoint));
            return new AiCoordinator(provider, TimeSpan.FromSeconds(25));
        }

        private static Uri ParseEndpoint(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri endpoint))
                throw new InvalidOperationException("SNKMESSAGE_AI_ENDPOINT 不是有效地址。");
            bool local = endpoint.IsLoopback;
            if (!local && endpoint.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("远程算法服务必须使用 HTTPS。");
            return endpoint;
        }
    }
}

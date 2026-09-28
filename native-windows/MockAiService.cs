using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SnkMessage
{
    internal sealed class MockAiService : IAiService
    {
        public async Task<AiResult> GenerateAsync(AiRequest request, CancellationToken cancellationToken)
        {
            await Task.Delay(650, cancellationToken);

            if (request.Mode == AiMode.Interpret)
                return AiResult.Interpretation(Interpret(request.SelectedText));

            return AiResult.SuggestionList(Suggestions(request.Mode));
        }

        private static string Interpret(string text)
        {
            if (text.Contains("问题", StringComparison.Ordinal) || text.Contains("考虑", StringComparison.Ordinal))
                return "对方可能希望你重新评估，但没有明确指出具体问题。";
            if (text.Contains("谢谢", StringComparison.Ordinal) || text.Contains("收到", StringComparison.Ordinal))
                return "看起来是在确认信息并表达礼貌回应。";
            return "这段话的具体含义可能需要结合前后文进一步确认。";
        }

        private static IReadOnlyList<string> Suggestions(AiMode mode)
        {
            if (mode == AiMode.Polish)
            {
                return new[]
                {
                    "我想先进一步了解具体情况，再给出明确回复。",
                    "方便再说明一下具体问题吗？我会据此调整。",
                    "我的理解是还需要进一步确认，您看是否准确？"
                };
            }

            return new[]
            {
                "您觉得具体是哪部分需要调整？",
                "好的，我再梳理一下，稍后和您确认。",
                "我们方便一起过一下具体问题吗？"
            };
        }
    }
}

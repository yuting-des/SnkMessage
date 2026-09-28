using System;
using System.Collections.Generic;

namespace SnkMessage
{
    internal enum AiMode { Interpret, Reply, Polish }

    internal sealed class ConversationTurn
    {
        public string Role { get; set; }
        public string Text { get; set; }
    }

    internal sealed class AiRequest
    {
        public string RequestId { get; set; }
        public AiMode Mode { get; set; }
        public string SelectedText { get; set; }
        public string Language { get; set; }
        public IReadOnlyList<ConversationTurn> Context { get; set; }

        public static AiRequest FromSelection(AiMode mode, string selectedText)
        {
            return new AiRequest
            {
                RequestId = Guid.NewGuid().ToString("N"),
                Mode = mode,
                SelectedText = selectedText,
                Language = "zh-CN",
                Context = Array.Empty<ConversationTurn>()
            };
        }
    }

    internal sealed class AiResult
    {
        public string Type { get; set; }
        public string Text { get; set; }
        public IReadOnlyList<string> Suggestions { get; set; }

        public static AiResult Interpretation(string text)
        {
            return new AiResult { Type = "interpretation", Text = text, Suggestions = Array.Empty<string>() };
        }

        public static AiResult SuggestionList(IReadOnlyList<string> suggestions)
        {
            return new AiResult { Type = "suggestions", Text = String.Empty, Suggestions = suggestions };
        }
    }

    internal sealed class AiServiceException : Exception
    {
        public AiServiceException(string message) : base(message) { }
        public AiServiceException(string message, Exception innerException) : base(message, innerException) { }
    }
}

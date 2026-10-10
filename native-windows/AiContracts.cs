using System;
using System.Collections.Generic;

namespace SnkMessage
{
    internal enum AiMode { Interpret, Reply, Polish }

    internal sealed class ConversationTurn
    {
        public string Role { get; set; }
        public string Speaker { get; set; }
        public string Text { get; set; }
    }

    internal sealed class AiRequest
    {
        public string RequestId { get; set; }
        public AiMode Mode { get; set; }
        public string SelectedText { get; set; }
        public string Language { get; set; }
        public IReadOnlyList<ConversationTurn> Context { get; set; }
        public string ConversationLabel { get; set; }
        public string ContextSource { get; set; }
        public string Adjustment { get; set; }
        public IReadOnlyList<string> ReferenceSuggestions { get; set; }
        public string OcrImageBase64 { get; set; }
        public int OcrImageWidth { get; set; }
        public int OcrImageHeight { get; set; }
        public int OcrContentTop { get; set; }

        public static AiRequest FromSelection(AiMode mode, SelectionContext selection, string adjustment=null, IReadOnlyList<string> referenceSuggestions=null)
        {
            return new AiRequest
            {
                RequestId = Guid.NewGuid().ToString("N"),
                Mode = mode,
                SelectedText = selection.Text,
                Language = "zh-CN",
                Context = selection.Context ?? Array.Empty<ConversationTurn>(),
                ConversationLabel = selection.ConversationLabel,
                ContextSource = selection.ContextSource,
                Adjustment = adjustment,
                ReferenceSuggestions = referenceSuggestions ?? Array.Empty<string>(),
                OcrImageBase64 = selection.OcrImageBase64,
                OcrImageWidth = selection.OcrImageWidth,
                OcrImageHeight = selection.OcrImageHeight,
                OcrContentTop = selection.OcrContentTop
            };
        }
    }

    internal sealed class AiResult
    {
        public string Type { get; set; }
        public string Text { get; set; }
        public IReadOnlyList<string> Suggestions { get; set; }
        public IReadOnlyList<ConversationTurn> Context { get; set; }
        public string ContextSource { get; set; }
        public string ConversationLabel { get; set; }

        public static AiResult Interpretation(string text)
        {
            return new AiResult { Type = "interpretation", Text = text, Suggestions = Array.Empty<string>() };
        }

        public static AiResult Interpretation(string text,IReadOnlyList<string> suggestions)
        {
            return new AiResult { Type = "interpretation", Text = text, Suggestions = suggestions ?? Array.Empty<string>() };
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

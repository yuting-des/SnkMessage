const modes = new Set(["interpret", "reply", "polish"]);
const adjustments = new Set(["shorter", "softer", "direct"]);

export function validateRequest(body) {
  if (!body || !modes.has(body.mode)) throw new Error("INVALID_MODE");
  if (typeof body.selectedText !== "string" || !body.selectedText.trim()) throw new Error("EMPTY_SELECTION");
  if (body.selectedText.length > 6000) throw new Error("SELECTION_TOO_LONG");
  if (body.context !== undefined && !Array.isArray(body.context)) throw new Error("INVALID_CONTEXT");
  if (Array.isArray(body.context)) {
    if (body.context.length > 8) throw new Error("CONTEXT_TOO_LONG");
    for (const turn of body.context) {
      if (!turn || !["user", "other"].includes(turn.role) || typeof turn.text !== "string" || !turn.text.trim() || turn.text.length > 1000
        || (turn.speaker !== undefined && (typeof turn.speaker !== "string" || turn.speaker.length > 80)))
        throw new Error("INVALID_CONTEXT");
    }
  }
  if (body.ocrImageBase64 !== undefined && (typeof body.ocrImageBase64 !== "string" || body.ocrImageBase64.length > 7_500_000))
    throw new Error("INVALID_OCR_IMAGE");
  if (body.conversationLabel !== undefined && (typeof body.conversationLabel !== "string" || body.conversationLabel.length > 100))
    throw new Error("INVALID_CONVERSATION_LABEL");
  if (body.adjustment !== undefined && body.adjustment !== null && !adjustments.has(body.adjustment)) throw new Error("INVALID_ADJUSTMENT");
  if (body.referenceSuggestions !== undefined) {
    if (!Array.isArray(body.referenceSuggestions) || body.referenceSuggestions.length > 3
      || body.referenceSuggestions.some((value) => typeof value !== "string" || !value.trim() || value.length > 280))
      throw new Error("INVALID_REFERENCE_SUGGESTIONS");
  }
}

export function validateResult(mode, result, selectedText = "") {
  if (!result || typeof result !== "object") throw new Error("INVALID_PROVIDER_RESULT");
  if (mode === "interpret") {
    const text = typeof result.text === "string" ? result.text.trim() : "";
    if (!text) throw new Error("EMPTY_INTERPRETATION");
    if (normalize(text) === normalize(selectedText)) throw new Error("INTERPRETATION_REPEATS_INPUT");
    const suggestions = cleanSuggestions(result.suggestions);
    if (suggestions.length !== 3) throw new Error("INVALID_SUGGESTION_COUNT");
    return { type: "interpretation", text: text.slice(0, 500), suggestions };
  }

  const suggestions = cleanSuggestions(result.suggestions);
  if (suggestions.length !== 3) throw new Error("INVALID_SUGGESTION_COUNT");
  return { type: "suggestions", text: "", suggestions };
}

function cleanSuggestions(source) {
  return Array.isArray(source)
    ? [...new Set(source.filter((value) => typeof value === "string").map((value) => value.trim()).filter(Boolean))]
        .slice(0, 3).map((value) => value.slice(0, 280))
    : [];
}

function normalize(value) {
  return String(value || "").replace(/\s+/gu, "").replace(/[，。！？、,.!?]/gu, "").toLowerCase();
}

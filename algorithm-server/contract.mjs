const modes = new Set(["interpret", "reply", "polish"]);

export function validateRequest(body) {
  if (!body || !modes.has(body.mode)) throw new Error("INVALID_MODE");
  if (typeof body.selectedText !== "string" || !body.selectedText.trim()) throw new Error("EMPTY_SELECTION");
  if (body.selectedText.length > 6000) throw new Error("SELECTION_TOO_LONG");
  if (body.context !== undefined && !Array.isArray(body.context)) throw new Error("INVALID_CONTEXT");
  if (Array.isArray(body.context)) {
    if (body.context.length > 8) throw new Error("CONTEXT_TOO_LONG");
    for (const turn of body.context) {
      if (!turn || !["user", "other"].includes(turn.role) || typeof turn.text !== "string" || !turn.text.trim() || turn.text.length > 1000)
        throw new Error("INVALID_CONTEXT");
    }
  }
  if (body.ocrImageBase64 !== undefined && (typeof body.ocrImageBase64 !== "string" || body.ocrImageBase64.length > 7_500_000))
    throw new Error("INVALID_OCR_IMAGE");
}

export function validateResult(mode, result, selectedText = "") {
  if (!result || typeof result !== "object") throw new Error("INVALID_PROVIDER_RESULT");
  if (mode === "interpret") {
    const text = typeof result.text === "string" ? result.text.trim() : "";
    if (!text) throw new Error("EMPTY_INTERPRETATION");
    if (normalize(text) === normalize(selectedText)) throw new Error("INTERPRETATION_REPEATS_INPUT");
    return { type: "interpretation", text: text.slice(0, 500), suggestions: [] };
  }

  const suggestions = Array.isArray(result.suggestions)
    ? [...new Set(result.suggestions.filter((value) => typeof value === "string").map((value) => value.trim()).filter(Boolean))]
        .slice(0, 3)
        .map((value) => value.slice(0, 280))
    : [];
  if (suggestions.length !== 3) throw new Error("INVALID_SUGGESTION_COUNT");
  return { type: "suggestions", text: "", suggestions };
}

function normalize(value) {
  return String(value || "").replace(/\s+/gu, "").replace(/[，。！？、,.!?]/gu, "").toLowerCase();
}

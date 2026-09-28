const modes = new Set(["interpret", "reply", "polish"]);

export function validateRequest(body) {
  if (!body || !modes.has(body.mode)) throw new Error("INVALID_MODE");
  if (typeof body.selectedText !== "string" || !body.selectedText.trim()) throw new Error("EMPTY_SELECTION");
  if (body.selectedText.length > 6000) throw new Error("SELECTION_TOO_LONG");
}

export function validateResult(mode, result) {
  if (!result || typeof result !== "object") throw new Error("INVALID_PROVIDER_RESULT");
  if (mode === "interpret") {
    const text = typeof result.text === "string" ? result.text.trim() : "";
    if (!text) throw new Error("EMPTY_INTERPRETATION");
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

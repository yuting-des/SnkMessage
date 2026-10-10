import { buildMessages, responseFormat } from "../prompts.mjs";

export function createOpenRouterProvider(config, { fetchImpl = globalThis.fetch } = {}) {
  if (!config.apiKey) throw new Error("OPENROUTER_API_KEY_MISSING");
  if (typeof fetchImpl !== "function") throw new Error("FETCH_NOT_AVAILABLE");

  return {
    name: "openrouter",
    model: config.model,
    async generate(request, { signal } = {}) {
      for (let attempt = 0; attempt < 2; attempt += 1) {
        const timeoutSignal = AbortSignal.timeout(config.timeoutMs);
        const combinedSignal = signal ? AbortSignal.any([signal, timeoutSignal]) : timeoutSignal;
        const response = await fetchImpl(config.baseUrl, {
          method: "POST",
          signal: combinedSignal,
          headers: buildHeaders(config),
          body: JSON.stringify(buildRequest(config, request)),
        });

        if (!response.ok) {
          const details = await safeErrorCode(response);
          if (attempt === 0 && [408, 429, 502, 503, 504].includes(response.status)) {
            await delay(400, signal);
            continue;
          }
          throw new Error(`OPENROUTER_${response.status}${details ? `:${details}` : ""}`);
        }

        const payload = await response.json();
        const content = payload?.choices?.[0]?.message?.content;
        if (typeof content !== "string" || !content.trim()) throw new Error("OPENROUTER_EMPTY_RESPONSE");
        return parseModelJson(content);
      }
      throw new Error("OPENROUTER_RETRY_EXHAUSTED");
    },
  };
}

function delay(milliseconds, signal) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(resolve, milliseconds);
    signal?.addEventListener("abort", () => { clearTimeout(timer); reject(signal.reason); }, { once: true });
  });
}

function buildHeaders(config) {
  const headers = {
    Authorization: `Bearer ${config.apiKey}`,
    "Content-Type": "application/json",
    "X-Title": config.appTitle,
  };
  if (config.httpReferer) headers["HTTP-Referer"] = config.httpReferer;
  return headers;
}

function buildRequest(config, request) {
  return {
    model: config.model,
    messages: buildMessages(request),
    temperature: request.mode === "interpret" ? 0.2 : 0.6,
    max_tokens: 500,
    reasoning: { enabled: false },
    response_format: responseFormat(request.mode),
    provider: {
      require_parameters: config.requireParameters,
      zdr: config.zdr,
      data_collection: config.dataCollection,
    },
  };
}

function parseModelJson(content) {
  const normalized = content.trim().replace(/^```(?:json)?\s*/iu, "").replace(/\s*```$/u, "");
  try {
    return JSON.parse(normalized);
  } catch {
    throw new Error("OPENROUTER_INVALID_JSON");
  }
}

async function safeErrorCode(response) {
  try {
    const payload = await response.json();
    const code = payload?.error?.code;
    return typeof code === "string" || typeof code === "number" ? String(code).slice(0, 80) : "";
  } catch {
    return "";
  }
}

import assert from "node:assert/strict";
import test from "node:test";
import { readConfig } from "../algorithm-server/config.mjs";
import { validateResult } from "../algorithm-server/contract.mjs";
import { createProvider } from "../algorithm-server/providers/index.mjs";
import { createOpenRouterProvider } from "../algorithm-server/providers/openrouter.mjs";

function config(overrides = {}) {
  return {
    apiKey: "test-key",
    baseUrl: "https://openrouter.ai/api/v1/chat/completions",
    model: "deepseek/deepseek-v3.2",
    zdr: true,
    dataCollection: "deny",
    requireParameters: true,
    timeoutMs: 1000,
    appTitle: "SnkMessage",
    httpReferer: "",
    ...overrides,
  };
}

test("configuration keeps model and provider replaceable", () => {
  const settings = readConfig({
    SNKMESSAGE_AI_PROVIDER: "openrouter",
    OPENROUTER_API_KEY: "secret",
    OPENROUTER_MODEL: "moonshotai/kimi-k2.5",
  });
  assert.equal(settings.provider, "openrouter");
  assert.equal(settings.openRouter.model, "moonshotai/kimi-k2.5");
  assert.equal(settings.openRouter.zdr, true);
});

test("OpenRouter adapter sends privacy routing and strict response schema", async () => {
  let captured;
  const provider = createOpenRouterProvider(config(), {
    fetchImpl: async (url, options) => {
      captured = { url, options, body: JSON.parse(options.body) };
      return new Response(JSON.stringify({
        choices: [{ message: { content: JSON.stringify({
          type: "suggestions",
          text: "",
          suggestions: ["回复一", "回复二", "回复三"],
        }) } }],
      }), { status: 200, headers: { "content-type": "application/json" } });
    },
  });

  const raw = await provider.generate({ mode: "reply", selectedText: "我们再考虑一下" });
  const result = validateResult("reply", raw);

  assert.equal(captured.url, "https://openrouter.ai/api/v1/chat/completions");
  assert.equal(captured.options.headers.Authorization, "Bearer test-key");
  assert.equal(captured.body.model, "deepseek/deepseek-v3.2");
  assert.equal(captured.body.reasoning.enabled, false);
  assert.equal(captured.body.provider.zdr, true);
  assert.equal(captured.body.provider.data_collection, "deny");
  assert.equal(captured.body.provider.require_parameters, true);
  assert.equal(captured.body.response_format.type, "json_schema");
  assert.equal(result.suggestions.length, 3);
});

test("provider factory can switch to mock without changing callers", async () => {
  const provider = createProvider({ provider: "mock", openRouter: config() });
  const result = validateResult("interpret", await provider.generate({ mode: "interpret", selectedText: "收到，谢谢" }));
  assert.equal(provider.name, "mock");
  assert.equal(result.type, "interpretation");
});

test("OpenRouter adapter refuses to start without a server-side key", () => {
  assert.throws(() => createOpenRouterProvider(config({ apiKey: "" })), /OPENROUTER_API_KEY_MISSING/u);
});

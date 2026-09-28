import { createMockProvider } from "./mock.mjs";
import { createOpenRouterProvider } from "./openrouter.mjs";

export function createProvider(config, dependencies = {}) {
  if (config.provider === "mock") return createMockProvider();
  if (config.provider === "openrouter") return createOpenRouterProvider(config.openRouter, dependencies);
  throw new Error(`UNSUPPORTED_AI_PROVIDER:${config.provider}`);
}

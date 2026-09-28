import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const serverDirectory = dirname(fileURLToPath(import.meta.url));

export function loadLocalEnvironment(file = join(serverDirectory, ".env.local"), environment = process.env) {
  if (!existsSync(file)) return;
  for (const rawLine of readFileSync(file, "utf8").split(/\r?\n/u)) {
    const line = rawLine.trim();
    if (!line || line.startsWith("#")) continue;
    const separator = line.indexOf("=");
    if (separator < 1) continue;
    const key = line.slice(0, separator).trim();
    if (environment[key] !== undefined) continue;
    environment[key] = unquote(line.slice(separator + 1).trim());
  }
}

export function readConfig(environment = process.env) {
  const provider = environment.SNKMESSAGE_AI_PROVIDER || (environment.OPENROUTER_API_KEY ? "openrouter" : "mock");
  return {
    host: environment.SNKMESSAGE_AI_HOST || "127.0.0.1",
    port: readInteger(environment.SNKMESSAGE_AI_PORT, 8787),
    provider,
    openRouter: {
      apiKey: environment.OPENROUTER_API_KEY || "",
      baseUrl: environment.OPENROUTER_BASE_URL || "https://openrouter.ai/api/v1/chat/completions",
      model: environment.OPENROUTER_MODEL || "deepseek/deepseek-v4-pro-0813:nitro",
      zdr: readBoolean(environment.OPENROUTER_ZDR, true),
      dataCollection: environment.OPENROUTER_DATA_COLLECTION || "deny",
      requireParameters: readBoolean(environment.OPENROUTER_REQUIRE_PARAMETERS, true),
      timeoutMs: readInteger(environment.OPENROUTER_TIMEOUT_MS, 12000),
      appTitle: environment.OPENROUTER_APP_TITLE || "SnkMessage",
      httpReferer: environment.OPENROUTER_HTTP_REFERER || "",
    },
  };
}

function readBoolean(value, fallback) {
  if (value === undefined || value === "") return fallback;
  return value.toLowerCase() === "true";
}

function readInteger(value, fallback) {
  const number = Number(value);
  return Number.isInteger(number) && number > 0 ? number : fallback;
}

function unquote(value) {
  if (value.length >= 2 && ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'")))) {
    return value.slice(1, -1);
  }
  return value;
}

loadLocalEnvironment();

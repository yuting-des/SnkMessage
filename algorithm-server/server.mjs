import { createServer } from "node:http";
import { readConfig } from "./config.mjs";
import { validateRequest, validateResult } from "./contract.mjs";
import { createProvider } from "./providers/index.mjs";

const config = readConfig();
const provider = createProvider(config);
const host = config.host;
const port = config.port;
const maxBodyBytes = 32 * 1024;

function sendJson(response, status, payload) {
  response.writeHead(status, {
    "content-type": "application/json; charset=utf-8",
    "cache-control": "no-store",
  });
  response.end(JSON.stringify(payload));
}

async function readJson(request) {
  const chunks = [];
  let size = 0;
  for await (const chunk of request) {
    size += chunk.length;
    if (size > maxBodyBytes) throw new Error("REQUEST_TOO_LARGE");
    chunks.push(chunk);
  }
  return JSON.parse(Buffer.concat(chunks).toString("utf8"));
}

const server = createServer(async (request, response) => {
  if (request.method === "GET" && request.url === "/health") {
    return sendJson(response, 200, { ok: true, provider: provider.name, model: provider.model });
  }
  if (request.method !== "POST" || request.url !== "/v1/generate") {
    return sendJson(response, 404, { error: "NOT_FOUND" });
  }

  const startedAt = Date.now();
  try {
    const body = await readJson(request);
    validateRequest(body);
    const result = validateResult(body.mode, await provider.generate(body), body.selectedText);
    console.log(`[ai] request=${body.requestId || "unknown"} mode=${body.mode} status=ok elapsedMs=${Date.now()-startedAt}`);
    return sendJson(response, 200, result);
  } catch (error) {
    const timeout = error instanceof Error && (error.name === "TimeoutError" || error.name === "AbortError");
    const code = timeout ? "PROVIDER_TIMEOUT" : error instanceof Error ? error.message : "UNKNOWN_ERROR";
    const clientError = ["REQUEST_TOO_LARGE", "INVALID_MODE", "EMPTY_SELECTION", "SELECTION_TOO_LONG"].includes(code);
    console.warn(`[ai] status=${code} elapsedMs=${Date.now()-startedAt}`);
    return sendJson(response, clientError ? 400 : timeout ? 504 : 500, { error: code });
  }
});

server.listen(port, host, () => {
  console.log(`SnkMessage algorithm server: http://${host}:${port}/v1/generate (${provider.name}/${provider.model})`);
});

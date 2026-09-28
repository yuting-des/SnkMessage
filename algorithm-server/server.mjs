import { createServer } from "node:http";
import { generate } from "./providers/mock.mjs";

const host = process.env.SNKMESSAGE_AI_HOST || "127.0.0.1";
const port = Number(process.env.SNKMESSAGE_AI_PORT || 8787);
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

function validateRequest(body) {
  if (!body || !["interpret", "reply", "polish"].includes(body.mode)) {
    throw new Error("INVALID_MODE");
  }
  if (typeof body.selectedText !== "string" || !body.selectedText.trim()) {
    throw new Error("EMPTY_SELECTION");
  }
  if (body.selectedText.length > 6000) throw new Error("SELECTION_TOO_LONG");
}

const server = createServer(async (request, response) => {
  if (request.method === "GET" && request.url === "/health") {
    return sendJson(response, 200, { ok: true, provider: "mock" });
  }
  if (request.method !== "POST" || request.url !== "/v1/generate") {
    return sendJson(response, 404, { error: "NOT_FOUND" });
  }

  try {
    const body = await readJson(request);
    validateRequest(body);
    const result = await generate(body);
    return sendJson(response, 200, result);
  } catch (error) {
    const code = error instanceof Error ? error.message : "UNKNOWN_ERROR";
    const clientError = ["REQUEST_TOO_LARGE", "INVALID_MODE", "EMPTY_SELECTION", "SELECTION_TOO_LONG"].includes(code);
    return sendJson(response, clientError ? 400 : 500, { error: code });
  }
});

server.listen(port, host, () => {
  console.log(`SnkMessage algorithm server: http://${host}:${port}/v1/generate`);
});

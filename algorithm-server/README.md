# SnkMessage algorithm service

The Windows client talks to one provider-neutral endpoint:

```http
POST /v1/generate
Content-Type: application/json

{
  "requestId": "unique-id",
  "mode": "interpret | reply | polish",
  "selectedText": "text selected in WeChat",
  "language": "zh-CN",
  "context": []
}
```

Interpretation responses use `{ "type": "interpretation", "text": "...", "suggestions": [] }`.
Reply and polish responses use `{ "type": "suggestions", "text": "", "suggestions": ["...", "...", "..."] }`.

## Providers

The server uses one internal provider contract. `providers/openrouter.mjs` and `providers/mock.mjs` are adapters; the Windows application never depends on a model vendor. Changing an OpenRouter model only changes `OPENROUTER_MODEL`. Adding another API means adding one provider adapter and registering it in `providers/index.mjs`.

Copy `.env.example` to `.env.local` and add your OpenRouter key locally:

```env
SNKMESSAGE_AI_PROVIDER=openrouter
OPENROUTER_API_KEY=sk-or-v1-your-key
OPENROUTER_MODEL=deepseek/deepseek-v3.2
OPENROUTER_ZDR=true
OPENROUTER_DATA_COLLECTION=deny
OPENROUTER_REQUIRE_PARAMETERS=true
```

`.env.local` is ignored by Git. The server never prints the key or selected message text. `zdr`, `data_collection`, and `require_parameters` ensure routing only uses endpoints that satisfy the requested privacy and structured-output capabilities. If no matching endpoint is available, the request fails instead of weakening these requirements.

Run the algorithm service with `node algorithm-server/server.mjs` or `npm run ai:server`, then start the Windows app with:

```powershell
$env:SNKMESSAGE_AI_ENDPOINT='http://127.0.0.1:8787/v1/generate'
./dist/SnkMessage-native-v0.3.exe
```

For offline UI work, set `SNKMESSAGE_AI_PROVIDER=mock`. Keep all model credentials on the server.

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

Run the current mock transport with `npm run ai:server`, then start the Windows app with:

```powershell
$env:SNKMESSAGE_AI_ENDPOINT='http://127.0.0.1:8787/v1/generate'
./dist/SnkMessage-native-v0.3.exe
```

Replace `providers/mock.mjs` with a server-side model adapter when a model provider is selected. Keep model credentials on the server. The service must not log selected message text.

import assert from "node:assert/strict";
import test from "node:test";
import { generate } from "../algorithm-server/providers/mock.mjs";
import { validateRequest } from "../algorithm-server/contract.mjs";
import { buildMessages } from "../algorithm-server/prompts.mjs";

test("algorithm provider returns one interpretation", async () => {
  const result = await generate({ mode: "interpret", selectedText: "这个方案还要再考虑一下" });
  assert.equal(result.type, "interpretation");
  assert.equal(typeof result.text, "string");
  assert.ok(result.text.length > 0);
  assert.equal(result.suggestions.length, 3);
});

for (const mode of ["reply", "polish"]) {
  test(`${mode} provider returns three usable suggestions`, async () => {
    const result = await generate({ mode, selectedText: "这个方案还要再考虑一下" });
    assert.equal(result.type, "suggestions");
    assert.equal(result.suggestions.length, 3);
    assert.equal(new Set(result.suggestions).size, 3);
    assert.ok(result.suggestions.every((value) => typeof value === "string" && value.trim().length > 0));
  });
}

test("limited nearby context is validated and included in the prompt", () => {
  const request = {
    mode: "reply",
    selectedText: "那就这样吧",
    context: [
      { role: "other", text: "周五下午可以吗？" },
      { role: "user", text: "我确认一下时间" },
    ],
  };
  assert.doesNotThrow(() => validateRequest(request));
  const messages = buildMessages(request);
  assert.match(messages[1].content, /对方：周五下午可以吗/);
  assert.match(messages[1].content, /我：我确认一下时间/);
  assert.match(messages[1].content, /<selected_text>\n那就这样吧/);
});

test("conversation identity, speakers, and the user's own style samples reach the prompt", () => {
  const request = {
    mode: "reply",
    selectedText: "今天可以先发第一版",
    conversationLabel: "产品讨论群",
    context: [
      { role: "other", speaker: "小林", text: "今天可以先发第一版" },
      { role: "user", text: "行，我晚点发" },
    ],
  };
  assert.doesNotThrow(() => validateRequest(request));
  const messages = buildMessages(request);
  assert.match(messages[0].content, /模仿附近聊天中“我”的句长/);
  assert.match(messages[1].content, /聊天对象或会话：产品讨论群/);
  assert.match(messages[1].content, /对方（小林）：今天可以先发第一版/);
  assert.match(messages[1].content, /我：行，我晚点发/);
});

test("oversized context is rejected", () => {
  const context = Array.from({ length: 9 }, () => ({ role: "other", text: "消息" }));
  assert.throws(() => validateRequest({ mode: "interpret", selectedText: "好", context }), /CONTEXT_TOO_LONG/);
});

test("suggestion adjustment carries the previous options without a free-form prompt", () => {
  const request = {
    mode: "reply",
    selectedText: "那就这样吧",
    adjustment: "softer",
    referenceSuggestions: ["行，就这样", "可以", "没问题"],
  };
  assert.doesNotThrow(() => validateRequest(request));
  const messages = buildMessages(request);
  assert.match(messages[1].content, /上一轮建议/);
  assert.match(messages[1].content, /- 行，就这样/);
  assert.match(messages[1].content, /三条更委婉的版本/);
});

test("Windows client optional fields may be serialized as null", () => {
  assert.doesNotThrow(() => validateRequest({
    mode: "reply",
    selectedText: "收到",
    context: [{ role: "other", speaker: null, text: "好的" }],
    conversationLabel: null,
    contextSource: null,
    adjustment: null,
    referenceSuggestions: [],
    ocrImageBase64: null,
    ocrImageWidth: 0,
    ocrImageHeight: 0,
    ocrContentTop: 0,
  }));
});

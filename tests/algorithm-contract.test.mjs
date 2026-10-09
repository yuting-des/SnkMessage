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
  assert.deepEqual(result.suggestions, []);
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

test("oversized context is rejected", () => {
  const context = Array.from({ length: 9 }, () => ({ role: "other", text: "消息" }));
  assert.throws(() => validateRequest({ mode: "interpret", selectedText: "好", context }), /CONTEXT_TOO_LONG/);
});

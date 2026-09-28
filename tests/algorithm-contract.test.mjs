import assert from "node:assert/strict";
import test from "node:test";
import { generate } from "../algorithm-server/providers/mock.mjs";

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

import assert from "node:assert/strict";
import test from "node:test";
import { extractTurns } from "../algorithm-server/ocr.mjs";

test("OCR paragraphs become a bounded conversation with inferred sides", () => {
  const blocks = [{ paragraphs: [
    { text: "周五下午可以吗？\n", bbox: { x0: 40, x1: 300, y0: 40, y1: 80 } },
    { text: "我确认一下时间", bbox: { x0: 650, x1: 940, y0: 100, y1: 140 } },
    { text: "那就这样吧", bbox: { x0: 50, x1: 260, y0: 160, y1: 200 } },
  ] }];
  assert.deepEqual(extractTurns(blocks, 1000, "那就这样吧"), [
    { role: "other", text: "周五下午可以吗？" },
    { role: "user", text: "我确认一下时间" },
  ]);
});

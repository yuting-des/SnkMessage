import assert from "node:assert/strict";
import test from "node:test";
import { extractConversation, extractTurns } from "../algorithm-server/ocr.mjs";

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

test("OCR removes sender labels, image placeholders, timestamps, and low-confidence text", () => {
  const blocks = [{ paragraphs: [
    { text: "产品讨论群", confidence: 96, bbox: { x0: 42, x1: 160, y0: 20, y1: 38 } },
    { text: "先确认一下排期。", confidence: 94, bbox: { x0: 45, x1: 330, y0: 45, y1: 80 } },
    { text: "[图片]", confidence: 99, bbox: { x0: 600, x1: 700, y0: 100, y1: 130 } },
    { text: "下午 3:20", confidence: 90, bbox: { x0: 430, x1: 510, y0: 150, y1: 170 } },
    { text: "图片里的随机小字", confidence: 31, bbox: { x0: 80, x1: 250, y0: 190, y1: 215 } },
  ] }];
  assert.deepEqual(extractTurns(blocks, 1000), [{ role: "other", speaker: "产品讨论群", text: "先确认一下排期。" }]);
});

test("OCR keeps the conversation label and attaches group sender names to messages", () => {
  const blocks = [{ paragraphs: [
    { text: "产品讨论群 (8)", confidence: 98, bbox: { x0: 40, x1: 250, y0: 10, y1: 42 } },
    { text: "小林", confidence: 97, bbox: { x0: 45, x1: 95, y0: 100, y1: 118 } },
    { text: "今天可以先发第一版", confidence: 96, bbox: { x0: 46, x1: 330, y0: 124, y1: 160 } },
    { text: "行，我晚点发", confidence: 96, bbox: { x0: 690, x1: 940, y0: 190, y1: 225 } },
  ] }];
  assert.deepEqual(extractConversation(blocks, 1000, "", 70), {
    conversationLabel: "产品讨论群 (8)",
    turns: [
      { role: "other", speaker: "小林", text: "今天可以先发第一版" },
      { role: "user", text: "行，我晚点发" },
    ],
  });
});

test("consecutive short chat messages are not mistaken for sender names", () => {
  const blocks = [{ paragraphs: [
    { text: "【图片】", confidence: 93, bbox: { x0: 50, x1: 130, y0: 10, y1: 32 } },
    { text: "还是抽了", confidence: 50, bbox: { x0: 48, x1: 155, y0: 45, y1: 69 } },
    { text: "哈哈哈哈", confidence: 51, bbox: { x0: 48, x1: 160, y0: 78, y1: 102 } },
    { text: "好喜欢她的人设！", confidence: 54, bbox: { x0: 48, x1: 250, y0: 111, y1: 135 } },
    { text: "而且好漂亮啊", confidence: 52, bbox: { x0: 48, x1: 220, y0: 144, y1: 168 } },
  ] }];
  assert.deepEqual(extractTurns(blocks, 1000, "而且好漂亮啊"), [
    { role: "other", text: "还是抽了" },
    { role: "other", text: "哈哈哈哈" },
    { role: "other", text: "好喜欢她的人设！" },
  ]);
});

test("long right-aligned bubbles are recognized as my messages", () => {
  const blocks = [{ paragraphs: [
    { text: "你先看看这个版本", confidence: 95, bbox: { x0: 48, x1: 270, y0: 30, y1: 64 } },
    { text: "我已经看完了，整体方向没问题，只需要再调整一下间距", confidence: 95, bbox: { x0: 300, x1: 940, y0: 85, y1: 128 } },
  ] }];
  assert.deepEqual(extractTurns(blocks, 1000), [
    { role: "other", text: "你先看看这个版本" },
    { role: "user", text: "我已经看完了，整体方向没问题，只需要再调整一下间距" },
  ]);
});

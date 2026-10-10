import { createRequire } from "node:module";
import path from "node:path";
import { createWorker, PSM } from "tesseract.js";

const require = createRequire(import.meta.url);
const languageRoot = path.join(path.dirname(require.resolve("@tesseract.js-data/chi_sim/package.json")), "4.0.0_best_int");
let workerPromise;

function getWorker() {
  workerPromise ??= createWorker("chi_sim", 1, {
    langPath: languageRoot,
    cacheMethod: "none",
    logger: () => {},
  }).then(async (worker) => {
    await worker.setParameters({ tessedit_pageseg_mode: PSM.SPARSE_TEXT });
    return worker;
  });
  return workerPromise;
}

export async function enrichContextWithOcr(request) {
  if (request.contextSource && Array.isArray(request.context)) return { request, source: request.contextSource };
  if (Array.isArray(request.context) && request.context.length > 0)
    return { request, source: "uia" };
  if (typeof request.ocrImageBase64 !== "string" || !request.ocrImageBase64)
    return { request, source: "none" };

  const worker = await getWorker();
  const { data } = await worker.recognize(Buffer.from(request.ocrImageBase64, "base64"), {}, { blocks: true });
  const extracted = extractConversation(data?.blocks, Number(request.ocrImageWidth), request.selectedText, Number(request.ocrContentTop));
  return {
    request: { ...request, context: extracted.turns, conversationLabel: extracted.conversationLabel, ocrImageBase64: undefined },
    source: extracted.turns.length ? "ocr" : "none",
  };
}

export function extractTurns(blocks, imageWidth, selectedText = "") {
  return extractConversation(blocks, imageWidth, selectedText, 0).turns;
}

export function extractConversation(blocks, imageWidth, selectedText = "", contentTop = 0) {
  const selected = normalize(selectedText);
  const entries = [];
  for (const block of Array.isArray(blocks) ? blocks : []) {
    for (const paragraph of Array.isArray(block?.paragraphs) ? block.paragraphs : []) {
      const text = clean(paragraph?.text);
      const box = paragraph?.bbox;
      if (!text || text.length > 500 || normalize(text) === selected || !box) continue;
      if (Number.isFinite(paragraph?.confidence) && paragraph.confidence < 42) continue;
      if (isUiNoise(text)) continue;
      entries.push({
        text,
        top: box.y0 || 0,
        bottom: box.y1 || 0,
        left: box.x0 || 0,
        right: box.x1 || 0,
        center: ((box.x0 || 0) + (box.x1 || 0)) / 2,
        confidence: Number.isFinite(paragraph?.confidence) ? paragraph.confidence : 100,
      });
    }
  }
  entries.sort((a, b) => a.top - b.top);
  const conversationLabel = findConversationLabel(entries.filter((entry) => contentTop > 0 && entry.bottom <= contentTop));
  const messages = entries.filter((entry) => !contentTop || entry.top >= contentTop);
  const senderNames = new Map();
  const nameEntries = new Set();
  messages.forEach((entry, index) => {
    if (isLikelySenderName(entry, messages[index + 1], imageWidth)) {
      nameEntries.add(entry);
      senderNames.set(messages[index + 1], entry.text);
    }
  });
  const withoutNames = messages.filter((entry) => !nameEntries.has(entry));
  const unique = withoutNames.filter((entry, index, all) => all.findIndex((other) => normalize(other.text) === normalize(entry.text)) === index);
  const chosen = chooseBalancedTail(unique, imageWidth, 5);
  const turns = chosen.map((entry) => {
    const role = roleFor(entry, imageWidth);
    const turn = { role, text: entry.text };
    if (role === "other" && senderNames.has(entry)) turn.speaker = senderNames.get(entry);
    return turn;
  });
  return { conversationLabel, turns };
}

function chooseBalancedTail(entries, imageWidth, limit) {
  const chosen = entries.slice(-limit);
  for (const role of ["other", "user"]) {
    if (chosen.some((entry) => roleFor(entry, imageWidth) === role)) continue;
    const candidate = [...entries].reverse().find((entry) => roleFor(entry, imageWidth) === role);
    if (candidate && !chosen.includes(candidate)) {
      if (chosen.length >= limit) chosen.shift();
      chosen.push(candidate);
      chosen.sort((a, b) => a.top - b.top);
    }
  }
  return chosen;
}

function roleFor(entry, imageWidth) {
  if (!(imageWidth > 0)) return "other";
  const leftGap = Math.max(0, entry.left);
  const rightGap = Math.max(0, imageWidth - entry.right);
  return rightGap + imageWidth * 0.04 < leftGap ? "user" : "other";
}

function clean(value) {
  return String(value || "")
    .replace(/\s*\n\s*/gu, " ")
    .replace(/(?<=\p{Script=Han})\s+(?=\p{Script=Han})/gu, "")
    .replace(/\s{2,}/gu, " ")
    .trim();
}

function normalize(value) {
  return String(value || "").replace(/[\s，。！？、,.!?：:]/gu, "").toLowerCase();
}

function isUiNoise(value) {
  const compact = value.replace(/[\s【】\[\]()（）]/gu, "");
  return /^(微信|通讯录|发现|我|朋友圈|视频号|搜一搜|看一看|小程序|文件传输助手|发送|表情|截图|图片|照片|视频|动画表情|文件|语音)$/u.test(compact)
    || /^(星期[一二三四五六日天]|昨天|今天|刚刚|上午|下午|晚上)?\s*\d{1,2}:\d{2}$/u.test(value);
}

function isLikelySenderName(entry, next, imageWidth) {
  if (!next || !imageWidth || entry.text.length > 24 || /[，。！？!?：:；;]/u.test(entry.text)) return false;
  const gap = next.top - entry.bottom;
  if (gap < -2 || gap > 22) return false;
  const entryRight = entry.center > imageWidth * 0.57;
  const nextRight = next.center > imageWidth * 0.57;
  if (entryRight !== nextRight) return false;
  const aligned = entryRight ? Math.abs(entry.right - next.right) < 42 : Math.abs(entry.left - next.left) < 42;
  const entryHeight = Math.max(1, entry.bottom - entry.top);
  const nextHeight = Math.max(1, next.bottom - next.top);
  return aligned && entryHeight <= nextHeight * .82;
}

function findConversationLabel(entries) {
  return entries
    .filter((entry) => entry.text.length <= 60 && !isUiNoise(entry.text))
    .sort((a, b) => ((b.bottom - b.top) * 2 + b.confidence) - ((a.bottom - a.top) * 2 + a.confidence))[0]?.text || "";
}

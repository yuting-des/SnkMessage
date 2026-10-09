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
    await worker.setParameters({ tessedit_pageseg_mode: PSM.AUTO });
    return worker;
  });
  return workerPromise;
}

export async function enrichContextWithOcr(request) {
  if (Array.isArray(request.context) && request.context.length > 0)
    return { request, source: "uia" };
  if (typeof request.ocrImageBase64 !== "string" || !request.ocrImageBase64)
    return { request, source: "none" };

  const worker = await getWorker();
  const { data } = await worker.recognize(Buffer.from(request.ocrImageBase64, "base64"), {}, { blocks: true });
  const turns = extractTurns(data?.blocks, Number(request.ocrImageWidth), request.selectedText);
  return {
    request: { ...request, context: turns, ocrImageBase64: undefined },
    source: turns.length ? "ocr" : "none",
  };
}

export function extractTurns(blocks, imageWidth, selectedText = "") {
  const selected = normalize(selectedText);
  const entries = [];
  for (const block of Array.isArray(blocks) ? blocks : []) {
    for (const paragraph of Array.isArray(block?.paragraphs) ? block.paragraphs : []) {
      const text = clean(paragraph?.text);
      const box = paragraph?.bbox;
      if (!text || text.length > 500 || normalize(text) === selected || !box) continue;
      if (isUiNoise(text)) continue;
      entries.push({ text, top: box.y0 || 0, center: ((box.x0 || 0) + (box.x1 || 0)) / 2 });
    }
  }
  entries.sort((a, b) => a.top - b.top);
  const unique = entries.filter((entry, index, all) => all.findIndex((other) => normalize(other.text) === normalize(entry.text)) === index);
  return unique.slice(-5).map((entry) => ({
    role: imageWidth > 0 && entry.center > imageWidth * 0.57 ? "user" : "other",
    text: entry.text,
  }));
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
  return /^(微信|通讯录|发现|我|朋友圈|视频号|搜一搜|看一看|小程序|文件传输助手|发送|表情|截图)$/u.test(value)
    || /^\d{1,2}:\d{2}$/u.test(value);
}

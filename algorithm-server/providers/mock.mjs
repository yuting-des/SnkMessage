const replySuggestions = [
  "您觉得具体是哪部分需要调整？",
  "好的，我再梳理一下，稍后和您确认。",
  "我们方便一起过一下具体问题吗？",
];

const polishSuggestions = [
  "我想先进一步了解具体情况，再给出明确回复。",
  "方便再说明一下具体问题吗？我会据此调整。",
  "我的理解是还需要进一步确认，您看是否准确？",
];

export async function generate(request) {
  if (request.mode === "interpret") {
    return {
      type: "interpretation",
      text: interpret(request.selectedText),
      suggestions: [],
    };
  }

  return {
    type: "suggestions",
    text: "",
    suggestions: request.mode === "polish" ? polishSuggestions : replySuggestions,
  };
}

export function createMockProvider() {
  return { name: "mock", model: "local-rules", generate };
}

function interpret(text) {
  if (text.includes("问题") || text.includes("考虑")) {
    return "对方可能希望你重新评估，但没有明确指出具体问题。";
  }
  if (text.includes("谢谢") || text.includes("收到")) {
    return "看起来是在确认信息并表达礼貌回应。";
  }
  return "这段话的具体含义可能需要结合前后文进一步确认。";
}

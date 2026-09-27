# Message Replier — Project Brief

## 1. Project Definition

Message Replier is a lightweight Windows desktop AI communication assistant.

It is **not** a chatbot and **not** a large AI side panel. Its purpose is to help users understand conversation context and express their own intent with minimal interruption.

Core principle:

> AI provides information and expression possibilities; humans make the judgment.

AI does not decide:
- whether the user should reply
- whether the user agrees or disagrees
- what the user truly wants to say
- what should ultimately be sent

AI mainly helps with:
- understanding the current communication context
- offering possible interpretations
- turning the user's own intent into suitable wording

---

## 2. Product Form

Target platform: **Windows desktop**

Product form: **Desktop Companion AI**

The AI UI exists as an independent overlay rather than modifying or embedding into WeChat or another chat app.

Primary UI:
- AI Bar
- loading/status toast
- interpretation card
- expression suggestion card

Do **not** build a large persistent sidebar.

Design keywords:

> Lightweight / Contextual / Low-interruption / Fast

---

## 3. Core Interaction Principle

The AI should stay hidden by default.

To minimize interruption:

> The user must first select text before the AI Bar appears.

Flow:

```text
User selects text
↓
AI Bar appears near the selection
↓
User explicitly invokes AI
↓
AI starts processing
```

This also creates a clear privacy boundary:

> AI only processes content the user actively selects or explicitly invokes.

---

## 4. Demo Core Flow

The first demo only needs one complete end-to-end experience:

```text
Select incoming message
↓
AI Bar appears
↓
Click “Interpret”
↓
Analyzing
↓
Show one-line AI interpretation
↓
User types their real intent in the chat input
↓
Invoke “Express”
↓
Generating
↓
Show 2–3 expression suggestions
↓
Click a suggestion
↓
Replace current input content directly
↓
User edits if needed
↓
User sends manually
```

The AI must **never auto-send** a message.

---

## 5. Figma

Design file:

https://www.figma.com/design/94eWQhY6bIxNKLZDDoHebE/Message-Replier?node-id=27-1132

Main frames:

| Frame | Node |
|---|---|
| 选择回复 | `35:3929` |
| 分析聊天 | `35:5699` |
| 解读信息 | `35:6598` |
| 输入内容 | `35:7515` |
| 优化表达 | `35:9489` |
| 选择表达 | `38:10400` |
| 发送 | `38:11586` |

Use Figma as the visual source of truth.

---

## 6. AI UI Components

### AIQuickBar

Approximate size:

```text
84 × 30 px
```

Only appears after text selection.

Primary actions:

```text
✦ 解读
```

or

```text
✦ 表达
```

Position it close to the active selection or current user focus.

### AIStatusToast

Approximate size:

```text
146 × 34 px
```

Examples:

```text
正在分析当前聊天
```

```text
正在优化表达
```

Only visible during processing.

### AIInterpretCard

Figma size:

```text
267 × 84 px
```

Example structure:

```text
✦ 解读                    重新生成

可能是在希望你重新评估方案，
但没有明确具体问题。
```

Rules:
- one short interpretation
- no long analysis
- do not state uncertain inference as fact
- use wording such as “可能”, “看起来”, etc.

### AIExpressionCard

Figma size:

```text
280 × 170 px
```

Maximum:

```text
3 suggestions
```

Rules:
- short
- natural
- directly sendable
- preferably different expression strategies
- no explanation under each suggestion

For the demo, do **not** implement copy behavior.

Interaction:

```text
Click suggestion
→ Replace current ChatInput content
→ Close AI card
```

---

## 7. Example Scenario

Manager:

> 这个方案整体方向可以，不过我觉得还有一些问题，你再重新考虑一下。

User selects the message.

Clicks:

```text
✦ 解读
```

AI:

> 可能是在希望你重新评估方案，但没有明确具体问题。

User types:

> 我不认同，想先问清楚具体问题。

Then invokes:

```text
✦ 表达
```

Possible suggestions:

```text
具体是哪部分需要调整？

我觉得方向还可以，具体哪里有问题？

您觉得主要是哪部分需要重新考虑？
```

The user clicks one suggestion.

The selected suggestion **replaces the current input content**.

The user decides whether to edit or send.

---

## 8. Demo Scope

### Must Implement

- mock desktop chat interface
- selectable message text
- AI Bar appears after selection
- loading state
- interpretation card
- editable chat input
- expression action
- 2–3 suggestion options
- click suggestion to replace input
- manual send action
- hover / selected / loading / visible states
- simple transitions

### Do Not Implement Yet

- real WeChat integration
- system-wide OCR
- Windows UI Automation
- automatic chat-app detection
- long-term user memory
- vector database
- auto-send
- complex settings
- large AI sidebar

Those belong to later MVP stages.

---

## 9. Future Technical Architecture

```text
Windows Desktop Client
        │
        ├── Selection Detection
        ├── Context Acquisition
        ├── AI Overlay UI
        └── Input Replacement
                │
                ▼
          Backend Service
                │
        ┌───────┴────────┐
        │                │
 Context Processor   AI Pipeline
                         │
                 ┌───────┴───────┐
                 │               │
             Jev Layer          LLM
             optional        Generation
```

---

## 10. Context Acquisition Strategy

For a real Windows version, use this priority order:

### Priority 1 — Windows UI Automation

Try to obtain:
- selected text
- current active window
- accessible chat text
- input field

### Priority 2 — Clipboard Fallback

When structured selection APIs are unavailable, use clipboard-based retrieval when appropriate.

### Priority 3 — OCR Fallback

Only use screenshot + OCR when text cannot be obtained structurally.

Do not default to sending the full screen to a vision model.

---

## 11. Context Strategy

Do not send the entire chat history to the model.

Default context:

```text
Selected text
+
recent relevant context
```

Possible initial rule:

```text
Selected text
+
last 5–8 nearby messages
```

Goals:
- reduce token usage
- improve response speed
- reduce privacy exposure
- avoid stale context

Long-term retrieval belongs to V2.

---

## 12. Backend API

The initial backend can stay very small.

### POST /interpret

Input:

```json
{
  "selectedText": "...",
  "context": []
}
```

Output:

```json
{
  "interpretation": "可能是在希望你重新评估方案，但没有明确具体问题。"
}
```

### POST /express

Input:

```json
{
  "context": [],
  "userIntent": "我不认同，想先问清楚具体问题",
  "language": "zh-CN"
}
```

Output:

```json
{
  "suggestions": [
    "具体是哪部分需要调整？",
    "我觉得方向还可以，具体哪里有问题？",
    "您觉得主要是哪部分需要重新考虑？"
  ]
}
```

---

## 13. AI Architecture

### LLM — Language Layer

The LLM handles:

#### Interpretation
Generate one short, natural-language interpretation from the conversation context.

#### Expression
Convert:

```text
conversation context
+
user intent
+
communication constraints
```

into 2–3 natural expression options.

The LLM is responsible for:

> language understanding + natural-language generation

---

## 14. Jev — Decision Layer (Experimental / V2)

Jev can be explored as a structured decision/classification layer.

Potential tasks:
- communication intent
- stance
- directness
- clarity
- whether clarification is needed
- conversation goal

Example internal output:

```json
{
  "intent": "request_reconsideration",
  "directness": "indirect",
  "clarity": "low",
  "needs_clarification": true
}
```

Then pass these structured signals to the LLM for final wording.

Conceptual pipeline:

```text
Conversation
      │
      ▼
Jev Decision Layer
      │
      ▼
Structured communication signals
      │
      ▼
LLM Expression Layer
      │
      ▼
Natural-language suggestions
```

The first demo must **not depend on Jev**.

Keep the architecture open for it later.

---

## 15. Multilingual Strategy

Never discard the original language.

If Jev performs better with English as an intermediate representation:

```text
Original language
        │
        ├───────────────┐
        │               │
        ▼               ▼
 Original Context   English Translation
        │               │
        │              Jev
        │               │
        └───────┬───────┘
                ▼
        Structured Signals
                │
                ▼
               LLM
                │
                ▼
        Original-language reply
```

Important principle:

> Translation is an auxiliary representation, not a replacement for the original context.

The original text must remain available to the final LLM because translation may lose:
- politeness level
- power dynamics
- social tone
- implication
- language-specific nuance

---

## 16. Model Output Rules

### Interpretation

```text
1 sentence
short
probabilistic wording
no overclaim
no unsolicited advice
```

### Expression

```text
2–3 suggestions
short
natural
conversation-ready
different strategies
no explanations
```

Avoid ChatGPT-style long responses such as:

```text
根据上下文分析……
从沟通心理学来看……
建议您考虑……
```

Target experience:

> glance and use

---

## 17. Human-in-the-loop Boundary

```text
AI understands
        ↓
AI provides possibilities
        ↓
Human decides intent
        ↓
AI helps express
        ↓
Human edits / confirms
        ↓
Human sends
```

AI must not:
- automatically decide the user's stance
- automatically send messages
- present uncertain interpretation as fact

---

## 18. Implementation Priority

### P0
Match Figma UI and state transitions.

### P1

```text
text selection
→ AI Bar
→ loading
→ interpretation card
```

### P2

```text
user intent
→ expression action
→ suggestions
→ click suggestion
→ replace input
```

### P3

```text
manual send
```

### P4
Connect a real model API.

---

## 19. Suggested Frontend State Machine

```text
idle
↓
text-selected
↓
ai-bar-visible
↓
analyzing
↓
interpreted
↓
user-composing
↓
generating-expression
↓
suggestions-visible
↓
suggestion-selected
↓
ready-to-send
↓
sent
```

Keep this logic centralized rather than scattering it across components.

---

## 20. Product Experience Test

Before adding any new feature, ask:

1. Does this require more user actions?
2. Does this make AI occupy too much visual space?
3. Does this let AI make a judgment that should belong to the user?

If yes, simplify.

Target experience:

> **Select → Understand → Express → Send**

Avoid:

> **Open AI → Chat with AI → Read analysis → Configure → Generate → Edit → Return to conversation**

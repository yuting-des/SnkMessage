// Replace this adapter with POST /interpret and POST /express when connecting a model.
// Demo output is deterministic, local, and never sends messages.
const delay = () => new Promise(resolve=>setTimeout(resolve,850));
export const ai = {
 async interpret({selectedText}) {
  await delay();
  return {interpretation:/方案|问题|考虑/.test(selectedText) ? '对方可能希望你重新评估方案，但没有明确指出具体问题。' : '仅凭这段文字，可能还无法确定对方的具体意图。'};
 },
 async reply({selectedText}) {
  await delay();
  if (/方案|问题|考虑/.test(selectedText)) return {suggestions:['您觉得具体是哪部分需要调整？','我再梳理一下方案，稍后和您确认。','我们方便一起过一下具体的问题吗？']};
  if (/邮箱|发给|发送/.test(selectedText)) return {suggestions:['收到，我看完后回复你。','好的，我稍后查看。','谢谢，收到后我们再一起确认。']};
  return {suggestions:['你可以再具体说说吗？','我想先确认一下你的意思。','我们方便再详细聊一下吗？']};
 },
 async express({selectedText}) {
  await delay();
  if (/不认同|不赞同/.test(selectedText)) return {suggestions:['我有不同的看法，想先了解具体问题。','对此我有些保留，能否先明确问题所在？','我暂时不太认同，希望先把具体问题讨论清楚。']};
  // For other input preserve the user's wording; never invent a stance in this mock.
  const text=selectedText.trim().replace(/[。！？?！]+$/u,'');
  return {suggestions:[`${text}。`,`我的想法是：${text}。`]};
 }
};

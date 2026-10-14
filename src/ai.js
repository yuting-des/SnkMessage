// Replace this adapter with POST /interpret and POST /express when connecting a model.
// Demo output is deterministic, local, and never sends messages.
const delay = () => new Promise(resolve=>setTimeout(resolve,850));
export const ai = {
 async interpret({selectedText}) {
  await delay();
  if (/聚|鸽子|周六|静安寺/.test(selectedText)) return {interpretation:'对方希望你参加这次聚会，语气比较熟悉、轻松，但也在提醒你之前有过临时取消的情况。可能有一点期待或担心，不一定是在责怪你。',...await ai.reply({selectedText,skipDelay:true})};
  return {interpretation:/方案|问题|考虑/.test(selectedText) ? '对方可能希望你重新评估方案，但没有明确指出具体问题。' : '仅凭这段文字，可能还无法确定对方的具体意图。'};
 },
 async reply({selectedText,adjustment,referenceSuggestions=[],skipDelay=false}) {
  if(!skipDelay)await delay();
  // Adjust each existing option independently to preserve its accept / decline stance.
  if(adjustment && referenceSuggestions.length) return {suggestions:referenceSuggestions.map(text=>{
   const uncertain=/没确定|准信|不确定/.test(text);
   const accepting=/一定到|这次我会参加/.test(text);
   if(adjustment==='shorter')return uncertain?'周六还没确定，我明天给你准信！':accepting?'这次一定到，时间地点发我！':'这周六去不了了，你们玩得开心，下次再约！';
   if(adjustment==='softer')return uncertain?'我也很想见大家，不过周六还有点事没确定，可以让我明天再给你准信吗？':accepting?'我也很期待见到大家！这次我会参加，定好时间地点告诉我就好。':'不好意思呀，这周六我想在家休息一下，最近确实有点累。你们玩得开心，下次有机会我们再一起聚！';
   return uncertain?'周六是否能参加还没确定，我明天告诉你。':accepting?'这次我会参加，定好时间地点发我。':'这周六我不参加了，想在家休息。我们下次再约。';
  })};
  if (/聚|鸽子|周六|静安寺/.test(selectedText)) return {suggestionLabels:['A · 委婉拒绝','B · 暂时不确定','C · 积极参加'],suggestions:['哈哈我也想见大家！不过这周六确实有点安排，可能去不了了。你们先聚，下次我一定提前留时间！','哈哈我努力不放鸽子！不过这周六还有点事没确定，我明天跟你说准信可以吗？','哈哈这次一定到！你们定好时间地点发我，我提前安排好！']};
  if (/方案|问题|考虑/.test(selectedText)) return {suggestions:['您觉得具体是哪部分需要调整？','我再梳理一下方案，稍后和您确认。','我们方便一起过一下具体的问题吗？']};
  if (/邮箱|发给|发送/.test(selectedText)) return {suggestions:['收到，我看完后回复你。','好的，我稍后查看。','谢谢，收到后我们再一起确认。']};
  return {suggestions:['你可以再具体说说吗？','我想先确认一下你的意思。','我们方便再详细聊一下吗？']};
 },
 async express({selectedText}) {
  await delay();
  const suggestionLabels=['更简短','更委婉','更直接'];
  if (/不去|不想去|不参加|不想参加|去不了|没法去|不能去|你们自己去/.test(selectedText)) return {suggestionLabels,suggestions:['这次我就不去了，你们玩得开心！','不好意思呀，这次聚会我就先不参加了。你们玩得开心，下次有机会我们再一起聚！','这次聚会我不参加，你们按原计划安排就好。']};
  if (/不认同|不赞同/.test(selectedText)) return {suggestionLabels,suggestions:['我有不同看法。','这个想法我有些保留，希望能和你再讨论一下。','我不认同这个想法，建议先讨论清楚再决定。']};
  // Other local demo inputs also expose all three styles, preserving the selected intent.
  const text=selectedText.trim().replace(/[。！？?！]+$/u,'');
  const concise=text.replace(/^(我觉得|我认为|我的想法是[：:]?|其实|就是)\s*/u,'').replace(/，?(你们自己去吧|就这样吧)$/u,'')||text;
  return {suggestionLabels,suggestions:[`${concise}。`,`我想和你商量一下：${text}。希望你能理解。`,`我的意思是：${text}。`]};
 }
};

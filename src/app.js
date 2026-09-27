import {initialState,reducer} from './state.js';
import {ai} from './ai.js';
const $=s=>document.querySelector(s);
const overlay=$('#overlay'),input=$('#intent'),messages=$('#messages');
const labels={interpret:'解读',reply:'回复建议',express:'表达优化'};
const originalMessages=messages.innerHTML;
let state=initialState(),anchor={left:0,top:0,bottom:0};
const icon=(name)=>{const img=document.createElement('img');img.src=`./assets/${name}.svg`;img.alt='';return img;};
function button(text,cls,action){const b=document.createElement('button');b.textContent=text;b.className=cls;b.addEventListener('click',action);return b;}
function dispatch(event){state=reducer(state,event);render();}
function announce(text){$('#announcement').textContent=text;}
function position(){const box=overlay.getBoundingClientRect();overlay.style.left=`${Math.max(12,Math.min(anchor.left,innerWidth-box.width-12))}px`;overlay.style.top=`${Math.max(12,anchor.top-box.height-10 < 12 ? Math.min(anchor.bottom+10,innerHeight-box.height-12) : anchor.top-box.height-10)}px`;}
function render(){
 $('.chat').classList.toggle('analyzing',state.phase==='loading');
 $('.chat').setAttribute('aria-busy',String(state.phase==='loading'));
 $('#send').disabled=!state.intent.trim();
 overlay.replaceChildren();overlay.hidden=state.phase==='idle';
 if(overlay.hidden)return;
 overlay.className='ai-surface';
 if(state.phase==='quick'){
  overlay.classList.add('quick');
  const b=button(labels[state.kind],'quick-action',run);
  b.prepend(icon('sparkle'));overlay.append(b);
  overlay.classList.toggle('reply-mode',state.kind!=='interpret');
  const toggle=button('','menu-toggle',()=>{dispatch({type:'MENU'});overlay.querySelector(state.menuOpen?'[role="menuitemradio"]':'.menu-toggle')?.focus({preventScroll:true});});
  toggle.append(icon('down'));toggle.setAttribute('aria-label','切换 AI 功能');toggle.setAttribute('aria-haspopup','menu');toggle.setAttribute('aria-expanded',String(state.menuOpen));overlay.append(toggle);
  if(state.menuOpen){
   const menu=document.createElement('div');menu.className='mode-menu';menu.setAttribute('role','menu');menu.setAttribute('aria-label','AI 功能');
   for(const [kind,label] of Object.entries(labels)){
    const item=button(label,'mode-option',()=>{dispatch({type:'MODE',kind});overlay.querySelector('.quick-action').focus({preventScroll:true});});
    item.setAttribute('role','menuitemradio');item.setAttribute('aria-checked',String(state.kind===kind));menu.append(item);
   }overlay.append(menu);
  }
 }else if(state.phase==='loading'){
  overlay.classList.add('toast');overlay.setAttribute('role','status');
  const img=icon('loading');img.className='spinner';overlay.append(img,document.createTextNode(state.kind==='interpret'?'正在分析当前聊天':state.kind==='reply'?'正在生成回复建议':'正在优化表达'));
 }else{
  overlay.removeAttribute('role');overlay.classList.add(state.kind==='interpret'?'interpret-card':'expression-card');
  const head=document.createElement('div');head.className='card-header';
  const title=document.createElement('span');title.className='card-title';title.append(icon('sparkle'),document.createTextNode(labels[state.kind]));
  const retry=button('重新思考','regenerate',run);retry.prepend(icon('reload'));head.append(title,retry);overlay.append(head);
  if(state.phase==='error'){const p=document.createElement('p');p.className='interpret-text';p.textContent='暂时未能完成，请重试。';overlay.append(p);}
  else if(state.kind==='interpret'){const p=document.createElement('p');p.className='interpret-text';p.textContent=state.result.interpretation;overlay.append(p);}
  else state.result.suggestions.forEach(text=>overlay.append(button(text,'suggestion',()=>{
   dispatch({type:'APPLY',text});input.value=state.intent;input.focus();input.setSelectionRange(input.value.length,input.value.length);announce('已替换输入内容，请检查后手动发送。');
  })));
 }
 position();
 const menu=overlay.querySelector('.mode-menu');if(menu){const rect=overlay.getBoundingClientRect();menu.classList.toggle('below',rect.top<130);menu.style.left=`${Math.min(0,innerWidth-12-rect.left-144)}px`;}
}
async function run(){
 if(state.phase==='loading')return;

 dispatch({type:'START'});const requestId=state.requestId;
 try{
  const result=await ai[state.kind]({selectedText:state.selectedText,context:[],language:'zh-CN'});
  dispatch({type:'RESULT',requestId,result});
  if(state.requestId===requestId){announce(state.kind==='interpret'?result.interpretation:'表达建议已生成，点击可替换输入。');overlay.querySelector('button')?.focus({preventScroll:true});}
 }catch{dispatch({type:'ERROR',requestId});}
}
function selection(){
 if(document.activeElement===input && input.selectionStart!==input.selectionEnd){
  const text=input.value.slice(input.selectionStart,input.selectionEnd).trim();if(!text)return;
  anchor=input.getBoundingClientRect();dispatch({type:'SELECT',kind:'express',text});return;
 }
 const selected=window.getSelection();
 if(!selected || selected.isCollapsed)return;
 const range=selected.getRangeAt(0);
 const parent=range.commonAncestorContainer.nodeType===1?range.commonAncestorContainer:range.commonAncestorContainer.parentElement;
 if(parent.closest('#overlay,button') || !selected.toString().trim())return;
 anchor=range.getBoundingClientRect();dispatch({type:'SELECT',kind:'interpret',text:selected.toString().trim()});
}
document.addEventListener('pointerup',event=>{if(!overlay.contains(event.target))selection();});
document.addEventListener('keyup',event=>{if(!overlay.contains(event.target)&&(event.key==='Shift'||event.key.startsWith('Arrow')||(event.ctrlKey&&event.key==='a')))selection();});
overlay.addEventListener('pointerdown',event=>event.preventDefault());
document.addEventListener('pointerdown',event=>{if(!overlay.contains(event.target)&&state.phase!=='idle')dispatch({type:'DISMISS'});});
document.addEventListener('keydown',event=>{
 if(event.key==='Escape'){if(state.phase==='quick'&&state.menuOpen){dispatch({type:'MENU'});overlay.querySelector('.menu-toggle').focus();}else{dispatch({type:'DISMISS'});input.focus();}}
 if(state.phase==='quick'&&state.menuOpen&&['ArrowDown','ArrowUp','Home','End'].includes(event.key)){
  event.preventDefault();const items=[...overlay.querySelectorAll('[role="menuitemradio"]')];const current=items.indexOf(document.activeElement);
  items[event.key==='Home'?0:event.key==='End'?items.length-1:(current+(event.key==='ArrowDown'?1:-1)+items.length)%items.length].focus();
 }
 if(event.key==='Tab'&&state.phase==='quick'&&!overlay.contains(document.activeElement)){event.preventDefault();overlay.querySelector('button').focus();}
});
input.addEventListener('input',()=>dispatch({type:'EDIT',value:input.value}));
function send(){
 if(!state.intent.trim())return;
 const row=document.createElement('div');row.className='message outgoing';
 const bubble=document.createElement('div');bubble.className='bubble';bubble.textContent=state.intent.trim();
 const avatar=document.createElement('span');avatar.className='avatar self';avatar.textContent='我';row.append(bubble,avatar);messages.append(row);
 dispatch({type:'SEND'});input.value='';input.focus();messages.scrollTop=messages.scrollHeight;announce('消息已发送到演示会话。');
}
$('#send').addEventListener('click',send);
input.addEventListener('keydown',event=>{if(event.ctrlKey&&event.key==='Enter'&&!event.isComposing){event.preventDefault();send();}});
$('#reset').addEventListener('click',()=>{dispatch({type:'RESET'});input.value='';messages.innerHTML=originalMessages;window.getSelection()?.removeAllRanges();announce('已重置演示。');});
window.addEventListener('resize',()=>dispatch({type:'DISMISS'}));
messages.addEventListener('scroll',()=>{if(state.phase!=='idle')dispatch({type:'DISMISS'});});
window.addEventListener('scroll',()=>{if(state.phase!=='idle')dispatch({type:'DISMISS'});});
render();

if (window.snkWindow) {
 const controls=$('#desktop-controls');controls.hidden=false;document.body.classList.add('desktop-app');
 const topButton=$('#toggle-top');
 const setTop=active=>{topButton.setAttribute('aria-pressed',String(active));topButton.textContent=active?'置顶':'未置顶';topButton.title=active?'取消置顶':'保持置顶';};
 window.snkWindow.isAlwaysOnTop().then(setTop);
 topButton.addEventListener('click',async()=>setTop(await window.snkWindow.toggleAlwaysOnTop()));
 $('#minimize-window').addEventListener('click',()=>window.snkWindow.minimize());
 $('#hide-window').addEventListener('click',()=>window.snkWindow.hide());
}



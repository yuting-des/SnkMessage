export const initialState = () => ({phase:'idle',kind:null,menuOpen:false,selectedText:'',intent:'',result:null,requestId:0,suggestionsExpanded:false,highlight:false,adjustment:null});
export function reducer(state,event) {
 switch(event.type) {
  case 'SELECT': return {...state,phase:'quick',kind:event.kind,menuOpen:false,selectedText:event.text,result:null,suggestionsExpanded:false,adjustment:null,requestId:state.requestId+1};
  case 'MENU': return state.phase==='quick' ? {...state,menuOpen:!state.menuOpen} : state;
  case 'MODE': return state.phase==='quick' && ['interpret','reply','express'].includes(event.kind) ? {...state,kind:event.kind,menuOpen:false} : state;
  case 'EDIT': return {...state,intent:event.value,phase:'idle',result:null,requestId:state.requestId+1};
  case 'START': return {...state,phase:'loading',kind:event.kind??state.kind,adjustment:event.adjustment??null,highlight:event.highlight??true,suggestionsExpanded:false,requestId:state.requestId+1};
  case 'REVEAL': return state.phase==='result' && state.kind==='interpret' && state.result.suggestions?.length===3 ? {...state,suggestionsExpanded:true} : state;
  case 'RESULT': return event.requestId===state.requestId && state.phase==='loading' ? {...state,phase:'result',result:event.result} : state;
  case 'ERROR': return event.requestId===state.requestId ? {...state,phase:'error'} : state;
  case 'APPLY': return (['reply','express'].includes(state.kind) || (state.kind==='interpret' && state.suggestionsExpanded)) && state.phase==='result' && state.result.suggestions.includes(event.text) ? {...state,intent:event.text,phase:'idle',result:null,requestId:state.requestId+1} : state;
  case 'DISMISS': return {...state,phase:'idle',result:null,requestId:state.requestId+1};
  case 'SEND': return state.intent.trim() ? {...initialState(),requestId:state.requestId+1} : state;
  case 'RESET': return {...initialState(),requestId:state.requestId+1};
  default:return state;
 }
}


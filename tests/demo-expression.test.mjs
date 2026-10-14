import {test} from 'node:test';
import assert from 'node:assert/strict';
import {ai} from '../src/ai.js';

test('short refusal and other demo drafts always expose three distinct expression styles',async()=>{
 const drafts=['不去','去不了','不参加','我周六不想去了，你们自己去吧。','不认同','明天再说'];
 const results=await Promise.all(drafts.map(selectedText=>ai.express({selectedText})));
 for(const result of results){
  assert.deepEqual(result.suggestionLabels,['更简短','更委婉','更直接']);
  assert.equal(result.suggestions.length,3);
  assert.equal(new Set(result.suggestions).size,3);
 }
 for(const result of results.slice(0,4)){
  assert.ok(result.suggestions.every(text=>/不去|不参加/.test(text)));
  assert.ok(result.suggestions[0].length<result.suggestions[1].length);
  assert.ok(!result.suggestions.some(text=>/累|休息|有安排/.test(text)));
 }
});

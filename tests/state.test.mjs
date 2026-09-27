import {test} from 'node:test';
import assert from 'node:assert/strict';
import {initialState,reducer} from '../src/state.js';
test('late AI result cannot reopen dismissed overlay',()=>{let s=reducer(initialState(),{type:'SELECT',kind:'interpret',text:'消息'});s=reducer(s,{type:'START'});const id=s.requestId;s=reducer(s,{type:'DISMISS'});assert.equal(reducer(s,{type:'RESULT',requestId:id,result:{interpretation:'旧结果'}}).phase,'idle');});
test('applying a suggestion changes the draft without sending',()=>{let s={...initialState(),kind:'express',phase:'result',intent:'原意图',result:{suggestions:['新的表达']}};s=reducer(s,{type:'APPLY',text:'新的表达'});assert.equal(s.intent,'新的表达');assert.equal(s.phase,'idle');s=reducer(s,{type:'SEND'});assert.equal(s.intent,'');});
test('editing while loading invalidates result',()=>{let s=reducer(initialState(),{type:'START'});const id=s.requestId;s=reducer(s,{type:'EDIT',value:'新想法'});s=reducer(s,{type:'RESULT',requestId:id,result:{suggestions:['过期']}});assert.equal(s.phase,'idle');assert.equal(s.intent,'新想法');});
test('empty draft cannot be sent and reset invalidates pending results',()=>{let s=initialState();assert.equal(reducer(s,{type:'SEND'}),s);s=reducer(s,{type:'START'});const id=s.requestId;s=reducer(s,{type:'RESET'});assert.equal(reducer(s,{type:'RESULT',requestId:id,result:{}}).phase,'idle');});

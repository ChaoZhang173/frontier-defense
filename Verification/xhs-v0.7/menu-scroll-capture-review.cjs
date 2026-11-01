'use strict';
const fs=require('fs'),path=require('path'),vm=require('vm'),crypto=require('crypto'),assert=require('assert');
const baseline=process.argv.includes('--baseline');
const sourcePath=baseline?path.join(__dirname,'menu-scroll-v0.6.1-snapshot.js'):path.resolve(__dirname,'../../XhsMiniTool/dist/menu-scroll.js');
const source=fs.readFileSync(sourcePath,'utf8'),sha256=crypto.createHash('sha256').update(source).digest('hex');
function createHarness(rotation,kind){
 let time=1000,nextRAF=1;const frames=new Map(),trace=[],listeners={},active=new Set(),pending=new Map(),override=new Map();
 const child={tagName:kind==='text'?'P':kind==='button'?'BUTTON':'ARTICLE',closest(){return null}};
 const panel={tagName:'SECTION',hidden:false,scrollTop:500,scrollHeight:3000,clientHeight:400,
  closest(){return null},addEventListener(type,fn){(listeners[type]||(listeners[type]=[])).push(fn)},
  setPointerCapture(id){if(!active.has(id))throw new Error('inactive pointer');pending.set(id,panel);trace.push({type:'setPointerCapture',id,target:'SECTION'})},
  hasPointerCapture(id){return pending.get(id)===panel},
  releasePointerCapture(id){if(pending.get(id)===panel){pending.delete(id);trace.push({type:'releasePointerCapture',id,target:'SECTION'})}}
 };
 const target=kind==='blank'?panel:child;
 function dispatch(type,id,x,y,eventTarget,extra={}){
  const e=Object.assign({type,pointerId:id,pointerType:'touch',button:0,buttons:1,target:eventTarget,currentTarget:panel,clientX:x,clientY:y,cancelable:!['gotpointercapture','lostpointercapture','pointercancel'].includes(type),detail:type==='click'?1:0,preventDefault(){if(this.cancelable)this.defaultPrevented=true},stopImmediatePropagation(){this.stopped=true}},extra);
  for(const fn of listeners[type]||[]){fn(e);if(e.stopped)break}
  trace.push({type,id,target:eventTarget.tagName,x,y,scrollTop:panel.scrollTop,panelHasCapture:panel.hasPointerCapture(id),prevented:!!e.defaultPrevented});return e;
 }
 function processPending(id,x,y){
  const old=override.get(id),next=pending.get(id);
  if(old&&old!==next)dispatch('lostpointercapture',id,x,y,old);
  const afterLost=pending.get(id);
  if(afterLost&&old!==afterLost)dispatch('gotpointercapture',id,x,y,afterLost);
  if(pending.has(id))override.set(id,pending.get(id));else override.delete(id);
 }
 const ctx={window:{},document:{hidden:false},performance:{now:()=>time},requestAnimationFrame:fn=>{let id=nextRAF++;frames.set(id,fn);return id},cancelAnimationFrame:id=>frames.delete(id)};
 vm.runInNewContext(source,ctx,{filename:sourcePath});const api=ctx.window.FrontierMenuScroll.create(panel,()=>rotation);
 return{panel,target,trace,api,frames,pending,override,
  event(type,x=100,y=100,id=1){time+=16;processPending(id,x,y);if(type==='pointerdown'){active.add(id);pending.set(id,target)}const e=dispatch(type,id,x,y,override.get(id)||target);if(type==='pointerup'||type==='pointercancel'){pending.delete(id);processPending(id,x,y);active.delete(id)}return e},
  captureLoss(id=1){pending.delete(id);processPending(id,150,100)},
  click(detail=1){return dispatch('click',1,100,100,target,{detail,cancelable:true})},
  step(){time+=16;const cbs=Array.from(frames.values());frames.clear();cbs.forEach(cb=>cb(time))},
  advance(ms){time+=ms},
  runMoves(delta=10,count=24,id=1){for(let n=1;n<=count;n++)this.event('pointermove',100+delta*n,100,id)}
 };
}
const cases=[];function test(name,fn){try{const details=fn();cases.push({name,status:'PASS',details})}catch(e){cases.push({name,status:'FAIL',message:e.message})}}
function near(a,b,message){assert(Math.abs(a-b)<1e-6,`${message||'scroll position'}: expected ${b}, got ${a}`)}
for(const rotation of [90,-90])for(const kind of ['text','button','card','blank'])test(`${rotation}deg ${kind}: 24 continuous moves preserve 240px travel`,()=>{const h=createHarness(rotation,kind);h.event('pointerdown');h.runMoves(rotation===90?10:-10);near(h.panel.scrollTop,740);h.event('pointerup',rotation===90?340:-140);assert(h.frames.size>0,'expected fling');assert(!h.panel.hasPointerCapture(1));const lost=h.trace.filter(t=>t.type==='lostpointercapture');return{finalScrollTop:h.panel.scrollTop,lostTargets:lost.map(t=>t.target),moveCount:h.trace.filter(t=>t.type==='pointermove').length}});
for(const rotation of [90,-90])test(`${rotation}deg child capture transfer does not discard direction reversal`,()=>{const h=createHarness(rotation,'text');const sign=rotation===90?1:-1;h.event('pointerdown');for(let n=1;n<=12;n++)h.event('pointermove',100+sign*n*10);near(h.panel.scrollTop,620);for(let n=11;n>=0;n--)h.event('pointermove',100+sign*n*10);near(h.panel.scrollTop,500);h.event('pointerup');return{finalScrollTop:h.panel.scrollTop}});
for(const type of ['actual-panel-capture-loss','pointercancel','stop'])test(type+' terminates active owner while preserving next gesture',()=>{const h=createHarness(90,'button');h.event('pointerdown');h.event('pointermove',120);h.event('pointermove',140);near(h.panel.scrollTop,540);if(type==='actual-panel-capture-loss')h.captureLoss();else if(type==='pointercancel')h.event('pointercancel',140);else h.api.stop();h.event('pointermove',180);h.event('pointerup',180);near(h.panel.scrollTop,540);assert.equal(h.frames.size,0);assert(!h.panel.hasPointerCapture(1));h.event('pointerdown',100,100,2);h.runMoves(10,4,2);near(h.panel.scrollTop,580);h.event('pointerup',140,100,2);return{finalScrollTop:h.panel.scrollTop}});
test('Child capture transfer preserves drag click suppression and keyboard click',()=>{const h=createHarness(90,'button');h.event('pointerdown');h.runMoves(10,12);near(h.panel.scrollTop,620);h.event('pointerup',220);assert(h.click().defaultPrevented);assert(!h.click(0).defaultPrevented);h.event('pointerdown',100,100,2);h.event('pointerup',100,100,2);assert(!h.click().defaultPrevented)});
test('Secondary pointer cannot disrupt captured owner across continuous moves',()=>{const h=createHarness(90,'card');h.event('pointerdown');h.event('pointermove',120);h.event('pointermove',140);h.event('pointerdown',100,100,2);h.runMoves(20,3,2);h.event('pointerup',160,100,2);h.event('pointermove',160);h.event('pointermove',180);near(h.panel.scrollTop,580);h.event('pointerup',180);assert(h.frames.size>0)});
test('Native portrait does not capture or prevent any continuous move',()=>{const h=createHarness(0,'button');h.event('pointerdown');h.runMoves(10,24);h.event('pointerup',340);near(h.panel.scrollTop,500);assert(!h.trace.some(e=>e.type==='setPointerCapture'));assert(h.trace.filter(e=>e.type==='pointermove').every(e=>!e.prevented));assert.equal(h.frames.size,0)});
for(const [name,start,sign,edge] of [['bottom',2590,1,2600],['top',10,-1,0]])test(name+' edge absorbs repeated drag without debt and reverses immediately',()=>{const h=createHarness(90,'text');h.panel.scrollTop=start;h.event('pointerdown');h.runMoves(sign*10,30);near(h.panel.scrollTop,edge);h.event('pointermove',100+sign*290);near(h.panel.scrollTop,edge-sign*10);h.event('pointerup',100+sign*290);return{edge,reversedScrollTop:h.panel.scrollTop}});
test('Inertia stops at boundary without extra scheduled frames',()=>{const h=createHarness(90,'button');h.panel.scrollTop=2580;h.event('pointerdown');h.event('pointermove',106);h.event('pointermove',112);h.event('pointerup',112);let frames=0;while(h.frames.size&&frames<100){h.step();frames++}near(h.panel.scrollTop,2600);assert.equal(h.frames.size,0);assert(frames<100);return{coastFrames:frames,scrollTop:h.panel.scrollTop}});
const report={checkedAt:new Date().toISOString(),baseline,sourcePath,sha256,sourceUnchanged:sha256===crypto.createHash('sha256').update(fs.readFileSync(sourcePath)).digest('hex'),testType:'Actual source evaluated with deferred implicit/pending pointer capture and descendant event propagation; full continuous event sequences',specification:'https://www.w3.org/TR/pointerevents3/#process-pending-pointer-capture',limitations:['Not physical iPhone event telemetry','DOM geometry and native scrolling are mocked; capture state/order and bubbled target/currentTarget follow W3C'],summary:{pass:cases.filter(t=>t.status==='PASS').length,fail:cases.filter(t=>t.status==='FAIL').length,total:cases.length},cases};
fs.writeFileSync(path.join(__dirname,baseline?'menu-scroll-capture-baseline.json':'menu-scroll-capture-review.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report,null,2));process.exitCode=report.summary.fail?1:0;

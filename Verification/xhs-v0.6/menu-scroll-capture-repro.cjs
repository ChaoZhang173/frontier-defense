'use strict';
const fs=require('fs'),vm=require('vm'),crypto=require('crypto'),path=require('path');
const sourcePath=path.resolve(__dirname,'../../XhsMiniTool/dist/menu-scroll.js');
const snapshot=path.join(__dirname,'menu-scroll-v0.6.1-snapshot.js');
const source=fs.existsSync(snapshot)?fs.readFileSync(snapshot,'utf8'):fs.readFileSync(sourcePath,'utf8');
if(!fs.existsSync(snapshot))fs.writeFileSync(snapshot,source);
const hash=crypto.createHash('sha256').update(source).digest('hex');
function trial(transferCapture){
 let time=1000,callbacks={},next=1;const listeners={},child={tagName:'BUTTON',closest(){return null}};
 const panel={tagName:'SECTION',hidden:false,scrollTop:100,scrollHeight:1600,clientHeight:300,captured:new Set(),addEventListener(t,f){(listeners[t]||(listeners[t]=[])).push(f)},setPointerCapture(id){this.captured.add(id)},hasPointerCapture(id){return this.captured.has(id)},releasePointerCapture(id){this.captured.delete(id)}};
 const ctx={window:{},document:{hidden:false},performance:{now:()=>time},requestAnimationFrame:f=>{callbacks[next]=f;return next++},cancelAnimationFrame:id=>delete callbacks[id]};
 vm.runInNewContext(source,ctx,{filename:snapshot});ctx.window.FrontierMenuScroll.create(panel,()=>90);
 const trace=[];
 function emit(type,x,target=child){time+=16;const event={type,clientX:x,clientY:100,pointerId:1,pointerType:'touch',button:0,target,currentTarget:panel,cancelable:true,preventDefault(){this.prevented=true},stopImmediatePropagation(){}};for(const f of listeners[type]||[])f(event);trace.push({type,target:target.tagName,x,scrollTop:panel.scrollTop,prevented:!!event.prevented});}
 emit('pointerdown',100);emit('pointermove',106);
 if(transferCapture)emit('lostpointercapture',120,child);
 for(const x of [120,140,160,180,200])emit('pointermove',x,transferCapture?panel:child);
 emit('pointerup',200,panel);
 return{transferCapture,finalScrollTop:panel.scrollTop,expectedScrollTop:200,pendingInertia:Object.keys(callbacks).length,trace};
}
const result={checkedAt:new Date().toISOString(),sourcePath,snapshot,sha256:hash,sourceStillMatchesLive:hash===crypto.createHash('sha256').update(fs.readFileSync(sourcePath)).digest('hex'),mechanism:'Touch child implicit capture is transferred to panel at first move; former child lostpointercapture bubbles into panel listener before the next pointermove.',sourceReference:'https://www.w3.org/TR/pointerevents3/#process-pending-pointer-capture',trials:[trial(false),trial(true)],limitations:['Standards-based event dispatch reproduction, not real iOS capture telemetry','Snapshot preserves v0.6.1 code for repro while parent edits implementation']};
fs.writeFileSync(path.join(__dirname,'menu-scroll-capture-repro.json'),JSON.stringify(result,null,2)+'\n');console.log(JSON.stringify(result,null,2));

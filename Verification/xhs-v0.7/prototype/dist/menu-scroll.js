(function(root){'use strict';
// Native scrolling remains enabled for upright panels. Rotated panels use the
// same logical axis as the game, including drag inertia and click cancellation.
function create(panel,getRotation){
 let drag=null,raf=null,velocity=0,lastFrame=0,suppressClickUntil=0;
 const now=()=>performance.now();
 function stopInertia(){if(raf!==null)cancelAnimationFrame(raf);raf=null;velocity=0;}
 function release(id){if(panel.hasPointerCapture&&panel.hasPointerCapture(id))panel.releasePointerCapture(id);}
 function stop(){stopInertia();if(drag){let id=drag.id;drag=null;release(id);}}
 function logicalY(dx,dy){let r=getRotation();return r===90?-dx:r===-90?dx:dy;}
 function scrollBy(amount){let before=panel.scrollTop,max=Math.max(0,panel.scrollHeight-panel.clientHeight);panel.scrollTop=Math.max(0,Math.min(max,before+amount));return panel.scrollTop-before;}
 function coast(t){raf=null;if(panel.hidden||document.hidden||getRotation()===0){stopInertia();return;}let dt=Math.min(40,Math.max(0,t-lastFrame));lastFrame=t;let amount=velocity*dt,moved=scrollBy(amount);velocity*=Math.exp(-dt/180);if(Math.abs(moved-amount)>.5)velocity=0;if(Math.abs(velocity)>.02&&Math.abs(moved)>.01)raf=requestAnimationFrame(coast);else velocity=0;}
 panel.addEventListener('pointerdown',e=>{
  if(drag||e.button>0)return;
  stopInertia();suppressClickUntil=0;if(getRotation()===0)return;
  // A range thumb keeps its own dragging behavior.
  if(e.target.closest&&e.target.closest('input[type="range"],textarea,select'))return;
  drag={id:e.pointerId,x:e.clientX,y:e.clientY,startX:e.clientX,startY:e.clientY,time:now(),moved:false};
 },true);
 panel.addEventListener('pointermove',e=>{
  if(!drag||e.pointerId!==drag.id)return;
  let t=now(),dy=logicalY(e.clientX-drag.x,e.clientY-drag.y),total=logicalY(e.clientX-drag.startX,e.clientY-drag.startY);
  if(!drag.moved){if(Math.abs(total)<5)return;drag.moved=true;dy=total;if(panel.setPointerCapture)panel.setPointerCapture(e.pointerId);}
  if(e.cancelable)e.preventDefault();let dt=Math.max(8,t-drag.time),moved=scrollBy(-dy),instant=moved/dt;
  // Reaching an edge must not retain velocity or accumulate overscroll debt.
  velocity=Math.abs(moved+dy)>.5?0:velocity*.25+instant*.75;
  drag.x=e.clientX;drag.y=e.clientY;drag.time=t;suppressClickUntil=t+500;
 },{capture:true,passive:false});
 function finish(e,cancelled){if(!drag||e.pointerId!==drag.id)return;let old=drag;drag=null;if(old.moved){suppressClickUntil=now()+500;if(e.cancelable)e.preventDefault();}release(old.id);if(cancelled||!old.moved||now()-old.time>100){stopInertia();return;}lastFrame=now();if(Math.abs(velocity)>.02)raf=requestAnimationFrame(coast);}
 panel.addEventListener('pointerup',e=>finish(e,false),true);
 panel.addEventListener('pointercancel',e=>finish(e,true),true);
 // Touch starts with implicit capture on the touched child (text/card/button).
 // Transferring it to this panel emits a bubbling lost event on that child;
 // that event is not the end of our drag. Only losing the panel's own capture is.
 panel.addEventListener('lostpointercapture',e=>{if(e.target===panel)finish(e,true);},true);
 panel.addEventListener('click',e=>{if(now()<suppressClickUntil&&e.detail!==0){e.preventDefault();e.stopImmediatePropagation();}},true);
 panel.addEventListener('wheel',e=>{if(getRotation()===0)return;stop();let units=e.deltaMode===1?16:e.deltaMode===2?panel.clientHeight:1;scrollBy((e.deltaY||e.deltaX)*units);if(e.cancelable)e.preventDefault();},{passive:false});
 return{stop};
}
root.FrontierMenuScroll={create};
if(typeof module!=='undefined'&&module.exports)module.exports={create};
})(typeof window!=='undefined'?window:globalThis);

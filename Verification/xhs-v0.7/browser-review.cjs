const fs=require('fs');
const {chromium}=require('/Users/czhang/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const base='/Users/czhang/Documents/Games/FrontierDefense',out=base+'/Verification/xhs-v0.7';
(async()=>{
const browser=await chromium.launch({headless:true,executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'});
const report={method:'Real local desktop Chrome/WebGL via file URL, isolated headless profile. Mobile sizes emulate viewport and touch only; not a Xiaohongshu/iPhone runtime. Controlled QA hooks expose game and view without changing shipped files.',checks:[]};
for(const cfg of [{name:'desktop',width:1366,height:900,touch:false},{name:'phone-landscape',width:844,height:390,touch:true},{name:'phone-portrait',width:390,height:844,touch:true}]){
 const page=await browser.newPage({viewport:{width:cfg.width,height:cfg.height},hasTouch:cfg.touch,deviceScaleFactor:1});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{let core,View;Object.defineProperty(window,'FrontierCore',{configurable:true,get(){return core},set(value){core=value;const Original=value.Game;value.Game=class extends Original{constructor(...args){super(...args);window.__qaGame=this}}}});Object.defineProperty(window,'FrontierView',{configurable:true,get(){return View},set(value){View=class extends value{constructor(...args){super(...args);window.__qaView=this}}}})});
 await page.goto('file://'+base+'/XhsMiniTool/dist/index.html');if(cfg.name==='phone-portrait')await page.locator('#start-panel [data-screen="portrait"]').click();
 await page.locator('#start').click();await page.waitForTimeout(300);await page.screenshot({path:out+'/'+cfg.name+'-play.png'});
 const start=await page.evaluate(()=>({errors:document.querySelector('#error-panel').hidden,hud:document.querySelector('#operations-status').textContent,world:__qaGame.enemyBases.length,canvas:[__qaView.renderer.domElement.width,__qaView.renderer.domElement.height]}));
 report.checks.push({name:cfg.name+' starts real 3D game',pass:!errors.length&&start.errors&&start.world===6,...start,pageErrors:errors.slice()});
 if(!cfg.touch)await page.keyboard.press('KeyE');else await page.locator('#interact').click();await page.waitForTimeout(100);
 const before=await page.evaluate(()=>({max:__qaGame.command.max,hp:__qaGame.command.hp,credits:__qaGame.credits}));
 const upgrade=page.locator('.upgrade-card').filter({hasText:'指挥中心 · 结构加固'});await upgrade.locator('button').click();
 const after=await page.evaluate(()=>({max:__qaGame.command.max,hp:__qaGame.command.hp,credits:__qaGame.credits}));
 report.checks.push({name:cfg.name+' full-health HQ fortification by real terminal click',pass:after.max-before.max===650&&after.hp-before.hp===650&&before.credits-after.credits===120,before,after});
 await page.locator('[data-tab="build"]').click();await page.screenshot({path:out+'/'+cfg.name+'-terminal.png'});
 const repairText=await page.locator('#terminal-content').innerText();report.checks.push({name:cfg.name+' completed buildings offer fortification',pass:repairText.includes('西侧地堡 · 结构加固')&&repairText.includes('东侧地堡 · 结构加固')&&!repairText.includes('补给 · 维修')});
 await page.locator('#close-terminal').click();
 if(cfg.name==='desktop'){
  await page.evaluate(()=>{const v=__qaView,update=v.update.bind(v);v.update=function(dt,aim){update(dt,aim);this.camera.position.set(-76,104,47);this.camera.lookAt(0,-2,-55);this.renderer.render(this.scene,this.camera)}});
  await page.waitForTimeout(100);await page.screenshot({path:out+'/battlefield-overview.png'});
  await page.evaluate(()=>{const v=__qaView;v.update=function(){this.camera.position.set(-40,14,-59);this.camera.lookAt(-27,0,-82);this.renderer.render(this.scene,this.camera)}});
  await page.waitForTimeout(100);await page.screenshot({path:out+'/portal-base.png'});
 }
 await page.close();
}
await browser.close();report.pass=report.checks.every(x=>x.pass);fs.writeFileSync(out+'/browser-review.json',JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report,null,2));if(!report.pass)process.exitCode=1;
})().catch(e=>{console.error(e);process.exit(1)});

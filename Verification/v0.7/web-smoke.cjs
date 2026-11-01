const {chromium}=require('/Users/czhang/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs=require('fs');
const path=require('path');
(async()=>{
 const root=path.resolve(__dirname,'../..');
 const target='file://'+path.join(root,'Builds/OfflineWeb/边境防线.html');
 const browser=await chromium.launch({executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
 const context=await browser.newContext({viewport:{width:1440,height:944},deviceScaleFactor:1,offline:true});
 const page=await context.newPage();const errors=[],warnings=[],network=[];
 page.on('pageerror',e=>errors.push(String(e)));
 page.on('console',m=>{if(m.type()==='error')errors.push(m.text());else if(m.type()==='warning')warnings.push(m.text());});
 page.on('request',r=>{if(/^https?:/.test(r.url()))network.push(r.url());});
 let report={url:target,offline:true,renderer:'Chrome headless with SwiftShader',errors,warnings,network};
 try{
  await page.goto(target,{waitUntil:'domcontentloaded',timeout:60000});
  await page.waitForFunction(()=>getComputedStyle(document.querySelector('#loading')).display==='none',{},{timeout:120000});
  await page.screenshot({path:path.join(__dirname,'web-briefing.png')});
  await page.mouse.click(220,700);
  await page.waitForTimeout(1500);
  await page.keyboard.down('w');await page.waitForTimeout(1500);await page.keyboard.up('w');
  await page.screenshot({path:path.join(__dirname,'web-playing.png')});
  await page.keyboard.press('Escape');await page.waitForTimeout(300);
  await page.screenshot({path:path.join(__dirname,'web-paused.png')});
  report.loaded=true;report.canvas=await page.locator('#unity-canvas').evaluate(c=>({width:c.width,height:c.height}));
  report.passed=errors.length===0&&network.length===0;
 }catch(e){report.passed=false;report.failure=String(e);await page.screenshot({path:path.join(__dirname,'web-error.png')}).catch(()=>{});}
 fs.writeFileSync(path.join(__dirname,'web-smoke.json'),JSON.stringify(report,null,2));
 await browser.close();console.log(JSON.stringify(report));process.exitCode=report.passed?0:1;
})().catch(e=>{console.error(e);process.exitCode=1;});

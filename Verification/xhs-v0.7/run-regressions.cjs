'use strict';
const fs=require('fs'),path=require('path'),crypto=require('crypto'),{spawnSync}=require('child_process');
const standard=['world-review','support-review','input-review','fire-aim-review','scroll-integration-review','minimap-input-review','projection-review','map-direction-review','shoulder-review','scene-review','menu-scroll-review','menu-scroll-capture-review'];
const requested=process.argv.slice(2),suites=requested.length?requested:standard;
const results=[];
for(const name of suites){
 if(!standard.includes(name))throw Error('Unknown regression suite: '+name);
 const started=Date.now(),file=path.join(__dirname,name+'.cjs'),reportPath=path.join(__dirname,name+'.json');
 const result=spawnSync(process.execPath,[file],{cwd:path.resolve(__dirname,'../..'),encoding:'utf8',timeout:90000,maxBuffer:8*1024*1024});
 fs.writeFileSync(path.join(__dirname,name+'.log'),(result.stdout||'')+(result.stderr||''));
 let checks=[],passed=0,total=0,error=result.error?result.error.message:null;
 try{const report=JSON.parse(fs.readFileSync(reportPath,'utf8'));checks=report.checks||report.results||report.cases||[];total=checks.length;passed=checks.filter(c=>c.pass===true||c.status==='PASS').length;if(!total&&report.summary){total=report.summary.total;passed=report.summary.pass;}}
 catch(e){error=e.message;}
 const fail=checks.filter(c=>c.pass!==true&&c.status!=='PASS').map(c=>({name:c.name||c.test,error:c.error||null}));
 const record={name,pass:!error&&result.status===0&&total>0&&passed===total,passed,total,failed:fail,exitCode:result.status,seconds:+((Date.now()-started)/1000).toFixed(3),error};
 results.push(record);console.log(name+' '+passed+'/'+total+(record.pass?' PASS':' FAIL'));if(error)console.log(error);
}
const directory=path.resolve(__dirname,'../../XhsMiniTool/dist'),sources={};
for(const name of ['core.js','support.js','scene.js','app.js','menu-scroll.js','index.html','style.css'])sources[name]=crypto.createHash('sha256').update(fs.readFileSync(path.join(directory,name))).digest('hex');
const report={at:new Date().toISOString(),command:process.execPath+' '+__filename,method:'Native simulation plus Node VM DOM/canvas/camera regressions. Browser/device and unmodified campaign balance reports are separate. Per-suite logs and JSON kept beside this summary.',sources,summary:{suitesPassed:results.filter(r=>r.pass).length,suitesTotal:results.length,checksPassed:results.reduce((n,r)=>n+r.passed,0),checksTotal:results.reduce((n,r)=>n+r.total,0)},pass:results.every(r=>r.pass),results};
fs.writeFileSync(path.join(__dirname,'regression-summary.json'),JSON.stringify(report,null,2)+'\n');process.exitCode=report.pass?0:1;

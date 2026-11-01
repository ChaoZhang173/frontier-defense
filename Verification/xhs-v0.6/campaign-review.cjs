const fs=require('fs'),vm=require('vm'),crypto=require('crypto');
const base='/Users/czhang/Documents/Games/FrontierDefense',code=fs.readFileSync(base+'/XhsMiniTool/dist/core.js','utf8'),ctx={};vm.createContext(ctx);vm.runInContext(code,ctx);const {Game,dist}=ctx.FrontierCore;
function simulate(mode){let g=new Game(),elapsed=0,maxEnemies=0,records=[],lastWave=-1,managed=-1,shots=0,issues=[],lastSpawns=0;const dt=.1;
function shop(){if(!g.nearCommand)return false;g.interact();if(!g.terminal)return false;for(const a of ['hire','hire','build:reserve','weapon','build:factory','tech:1','tech:0','tech:2','build:west','defense','defense','armor','production','weapon','weapon','firepower','repair:command','repair:east','repair:west','repair:factory','repair:barracks','repair:reserve'])g.buy(a);g.interact();return true}
g.start();if(mode==='managed'){shop();managed=0}
for(let i=0;i<18000&&!['victory','defeat'].includes(g.phase);i++){
 let p=g.player,input={};
 if(mode==='managed'&&p.alive){
   let needsShop=g.phase==='intermission'&&managed!==g.wave;
   if(needsShop){if(g.nearCommand){if(shop())managed=g.wave}else g.go(p,{x:0,z:-1.1},5.8,dt)}
   else {
    let targets=g.enemies.filter(e=>g.visible(p,e)).sort((a,b)=>{const score=t=>t.z*1.2+dist(p,t)+(t.role==='heavy'?-7:0);return score(a)-score(b)});
    let t=targets[0],front=g.enemies.slice().sort((a,b)=>a.z-b.z)[0];
    let goal={x:front?(front.lane?13.5:-13.5):g.wave===2?13.5:-13.5,z:18};
    if(p.hp<60)goal={x:0,z:5};
    if(dist(p,goal)>1.8)g.go(p,goal,5.8,dt);
    if(t){let a=g.aimOrigin(p),b=g.aimOrigin(t);p.yaw=Math.atan2(b.x-a.x,b.z-a.z);p.pitch=-Math.atan2(b.y-a.y,dist(p,t));input={yaw:p.yaw,pitch:p.pitch,fire:true,aim:true,aimPoint:b};}
    if(g.squad.filter(e=>e.role==='marine').length<2&&!g.nearCommand&&!g.nearbyBunker()){
      const m=g.marines.find(e=>!e.bunker&&dist(e,p)<3.6&&g.visible(p,e));if(m){p.yaw=Math.atan2(m.x-p.x,m.z-p.z);g.interact()}
    }
   }
 }
 let oldShots=p.ammo;g.tick(dt,input);if(p.ammo<oldShots)shots+=oldShots-p.ammo;elapsed+=dt;maxEnemies=Math.max(maxEnemies,g.enemies.length);
 if(g.enemies.length>65)issues.push('enemy-cap');
 if(!Number.isFinite(p.x)||!Number.isFinite(p.z)||!Number.isFinite(g.command.hp))throw Error('nonfinite state');
 if(g.wave!==lastWave){records.push({wave:g.wave,phase:g.phase,time:+g.time.toFixed(1),command:g.command.hp,credits:g.credits,spawned:g.totalSpawned,tankProduced:g.tankProduced});lastWave=g.wave;console.log(mode+' '+JSON.stringify(records.at(-1)));}
 g.events=[];
}
return{mode,phase:g.phase,wave:g.wave,seconds:+g.time.toFixed(1),spawned:g.totalSpawned,maxEnemies,kills:g.kills,playerKills:g.playerKills,deaths:g.deaths,command:g.command.hp,credits:g.credits,tankProduced:g.tankProduced,shots,records,issues,squad:g.squad.length,upgrades:{weapon:g.weapon,defense:g.defense,armor:g.armor,firepower:g.firepower,production:g.production},tech:g.tech,siteStates:g.sites.map(s=>[s.id,s.state])};}
const results=[];for(let mode of ['afk','managed'])results.push(simulate(mode));const report={at:new Date().toISOString(),coreSha256:crypto.createHash('sha256').update(code).digest('hex'),method:'Real core simulation with dt=0.1. AFK has no player action; managed bot uses collision-aware go(), actual fire/reload, interact and paid buy() with earned supplies. No HP, damage, currency, wave counts or enemies altered. View/touch/browser not exercised.',results};fs.writeFileSync(base+'/Verification/xhs-v0.6/campaign-review.json',JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report,null,2));

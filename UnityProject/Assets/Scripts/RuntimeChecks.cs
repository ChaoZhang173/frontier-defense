using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Frontier
{
    // Command-line diagnostics run only when explicitly requested by the build tools.
    public sealed class RuntimeChecks : MonoBehaviour
    {
        [Serializable] public sealed class Report
        {
            public string mode, outcome, version;public bool passed;public float simulatedSeconds,hqHealth;
            public int wave,spawned,kills,playerKills,deaths,peakEnemies,peakMarines,squadKills,squadHired,squadPeak,criticalHits;
            public int barracksLevel,bunkerArmorLevel,bunkerFirepowerLevel,readySites,completedResearch;
            public int mercenaryContracts,recruitedGuards,mercenaryRespawns,peakMarineCapacity;
            public int[] spawnPointUses;
            public int tanksProduced,tanksLost,tankKills,tankShots,enemyTowersDestroyed;
            public int suppressedEnemies,portalsDestroyed,expeditionsProduced,expeditionDepartures,expeditionShots,workersAlive;public float repairedHealth;
            public bool tankDeployed;
            public int suppliesEarned,suppliesSpent,endingSupplies,guardLosses,squadLosses,buildingsLost,peakFrontlineEnemies;
            public float headquartersDamage,bunkerDamage,barracksDamage,dualLanePressureSeconds;
            public List<BattleStats.WaveResult> waves;

            public List<string> actors=new List<string>();public List<string> checks=new List<string>();public List<string> failures=new List<string>();
        }
        public static string Argument(string key)
        {
            foreach(string arg in Environment.GetCommandLineArgs())if(arg.StartsWith(key+"="))return arg.Substring(key.Length+1);
            return "";
        }
        public static bool Requested=>Argument("--frontier-check")!="";
        static Report restartReport;
        GameWorld world;Report report;string mode;int frames;bool finished,capturing;BalanceDriver balance;
        public void Setup(GameWorld w)
        {
            world=w;mode=Argument("--frontier-check");report=new Report{mode=mode,version=Application.version};
            Application.targetFrameRate=-1;
        }
        public void Step(float dt)
        {
            if(finished||frames++<4)return;
            if(mode=="capture")
            {
                if(!capturing){capturing=true;StartCoroutine(Capture());}return;
            }
            if(frames==6)
            {
                if(mode=="squad"||mode=="fortification"||mode=="construction"||mode=="guards"||mode=="navigation"||mode=="interaction")
                {
                    if(restartReport!=null)
                    {
                        report=restartReport;restartReport=null;
                        Check(world.Squad.Count==0&&world.Squad.Kills==0&&world.Wave==1&&world.Credits==160,"Restart clears the previous squad, orders, credits and battle state");
                        if(mode=="fortification")FortificationChecks.CheckRestart(world,Check);
                        if(mode=="construction")ConstructionChecks.CheckRestart(world,Check);
                        Check(world.Squad.Contracts==0&&world.Squad.RecruitedGuardCount==0&&world.Fort.GuardInside==0,"Restart clears mercenary contracts, recruited guards and automatic bunker occupants");
                        Check(!world.Tech.IsComplete(Research.MercenaryTraining),"Restart clears mercenary-specific training");
                        Complete();return;
                    }
                    try
                    {
                        if(mode=="squad")SquadChecks.Run(world,Check);
                        else if(mode=="construction")ConstructionChecks.Run(world,Check);
                        else if(mode=="guards")GuardBunkerChecks.Run(world,Check);
                        else if(mode=="navigation")NavigationChecks.Run(world,Check);
                        else if(mode=="interaction")InteractionChecks.Run(world,Check);
                        else FortificationChecks.Run(world,Check);
                    }
                    catch(Exception exception){Check(false,mode+" diagnostic exception: "+exception);Complete();return;}
                    restartReport=report;finished=true;world.Restart();return;
                }
                if(mode=="expedition"||mode=="repair"||mode=="enemy_bases"){try{if(mode=="expedition")ExpeditionChecks.Run(world,Check);else if(mode=="repair")RepairChecks.Run(world,Check);else EnemyBaseChecks.Run(world,Check);}catch(Exception exception){Check(false,mode+" diagnostic exception: "+exception);}Complete();return;}
                if(mode=="assault"||mode=="tank"||mode=="enemy_defense"||mode=="central")
                {
                    try{if(mode=="assault")AssaultChecks.Run(world,Check);else if(mode=="tank")TankChecks.Run(world,Check);else if(mode=="enemy_defense")EnemyDefenseChecks.Run(world,Check);else CentralDefenseChecks.Run(world,Check);}catch(Exception exception){Check(false,"assault diagnostic exception: "+exception);}
                    Complete();return;
                }
                if(mode=="mechanics"){Mechanics();Complete();return;}
                world.BeginCampaign();
                if(mode=="victory"||mode=="balance_rifle"||mode=="balance_squad"||mode=="balance_prepared"){balance=new BalanceDriver(world,mode);balance.Begin();}
                if(mode=="defeat")
                {
                    world.Barracks.Damage(10000,null);world.Bunker.Damage(10000,null);
                    foreach(var h in world.Entities.ToArray())if(h!=null&&h.Role==Role.Marine)h.Damage(10000,null);
                }
            }
            for(int n=0;n<24&&!world.Ended;n++)
            {
                balance?.Step();
                Physics.SyncTransforms();world.Tick(.05f);world.Effects.Tick(.05f);
                if(world.SimTime>1500){Check(false,"Simulation finishes within 25 minutes");Complete();return;}
            }
            if(world.Ended)
            {
                if(mode.StartsWith("balance_"))Check(world.Ended,"The comparison strategy reaches a real victory or defeat");
                else Check(mode=="victory"?world.Phase==Phase.Victory:world.Phase==Phase.Defeat,"Expected "+mode+" outcome");
                if(mode=="victory")
                {
                    Check(WavePlan.TotalCount==608&&world.TotalSpawned+world.Assault.TotalSuppressed==WavePlan.TotalCount,"All 608 scheduled invaders were spawned or cancelled by destroyed portals");
                    Check(world.Wave==5&&world.EnemiesAlive==0&&world.RemainingToSpawn==0,"Victory waits for the complete fifth wave");
                    Check(world.PlayerKills>20,"Player bot uses actual rifle hits");
                    Check(world.Squad.Contracts==2&&world.Squad.Recruited>=2&&world.Squad.PeakCount==4&&world.Squad.Kills>0,"Two contracted mercenaries and two free recruited guards participate in the full five-wave campaign");
                    Check(world.EnemyDefenses.Bases.Count==6,"All six portal bases exist in the full campaign");
                    Check(world.Construction.Completed>=2&&world.PeakMarineCapacity==12,"Real battle supplies build a factory and reinforcement barracks alongside the initial twin bunkers");
                    Check(world.Tanks.Produced>=1&&world.Tanks.Shots>0&&world.Tanks.Kills>0,"The factory automatically deploys a tank that attacks and earns real kills");
                    Check(world.Tech.IsComplete(Research.ArmorPiercing)&&world.Tech.IsComplete(Research.InfantryArmor),"The prepared strategy researches actual combat technologies before winning");
                }
                Check(world.PeakMarines<=world.PeakMarineCapacity,"Garrison respects its operational capacity");
                Check(world.PeakEnemies<=GameWorld.EnemyLimit,"Concurrent enemy count stays bounded");
                Complete();
            }
        }
        void Check(bool success,string label){if(success)report.checks.Add(label);else report.failures.Add(label);}
        void Mechanics()
        {
            Physics.SyncTransforms();
            Check(world.Bunkers.Count==2&&world.MarinesAlive==4&&world.Repairs.WorkersAlive==2&&world.EnemyDefenses.Towers.Count==12,"HQ, barracks, two forward bunkers, four guards, two workers and twelve enemy towers exist");
            int total=0;for(int i=1;i<=5;i++)total+=WavePlan.Create(i).Count;
            Check(total==608&&total==WavePlan.TotalCount,"Five wave definitions contain exactly 608 invaders");
            int[] expected={56,80,116,156,200};
            for(int i=0;i<expected.Length;i++)Check(WavePlan.Create(i+1).Count==expected[i]&&WavePlan.Counts[i]==expected[i],"Wave "+(i+1)+" schedules "+expected[i]+" invaders");
            for(int i=0;i<14;i++)world.SpawnMarine();
            Check(world.MarinesAlive==8,"Production refuses units beyond its cap");
            float hp=world.Command.Current;world.Command.Damage(100,world.Player.Self);
            Check(world.Command.Current==hp,"Friendly rifle damage cannot harm the base");
            world.Phase=Phase.Intermission;world.TryOpenCommandTerminal();world.Credits=0;
            Check(!world.Buy(0)&&world.Credits==0&&world.WeaponLevel==0,"Insufficient credits do not purchase or go negative");
            world.Credits=10000;for(int i=0;i<10;i++)world.Buy(0);
            Check(world.WeaponLevel==4,"Player upgrade has a hard level cap");
            int before=world.Credits;Check(!world.Buy(0)&&world.Credits==before,"Maxed upgrades never charge credits");
            world.Command.Current=2200;world.Buy(2);
            Check(world.Command.Current==2850&&world.Command.Maximum==2950,"Command fortification adds exactly 650 maximum and current health");
            world.Command.Heal(10000);before=world.Credits;Check(world.Buy(2)&&world.Command.Current==3600&&world.Command.Maximum==3600&&world.Credits<before,"Full-health command fortification still increases its health cap");
            Health enemy=world.SpawnEnemy(Role.Runner,0,new Vector3(0,0,-17));Physics.SyncTransforms();
            float enemyHp=enemy.Current;
            Combat.Rifle(world.Player.Self,world.Player.Self.Aim,(enemy.Aim-world.Player.Self.Aim).normalized,1000,true);
            Check(enemy.Current==enemyHp,"HQ collider blocks rifle fire at an enemy behind it");
            enemy.Damage(9999,null);
            world.Phase=Phase.Briefing;world.WeaponLevel=0;world.Wave=0;world.BeginCampaign();
            before=world.Credits;Check(!world.Buy(1)&&world.Credits==before,"Purchases require an open command terminal during an assault");
            world.Tick(.05f);
            Check(world.Phase==Phase.Assault&&world.EnemiesAlive==0&&world.RemainingToSpawn==WavePlan.Create(1).Count,"An empty field does not finish a wave with pending spawns");
            world.SetPaused(true);float now=world.SimTime;world.Tick(10);
            Check(world.SimTime==now,"Pause freezes simulation time");world.SetPaused(false);
            world.Player.Self.Damage(10000,null);
            for(int i=0;i<125;i++){world.Tick(.05f);world.Effects.Tick(.05f);}
            Check(world.Deaths==1&&world.Player.Self.Alive&&world.Player.Self.Current>0,"Player respawns after six seconds while the battle continues");
            Check(world.Player.Ammo==30,"Respawn restores a complete magazine");
            world.Player.Ammo=4;world.Player.StartReload();
            for(int i=0;i<35;i++)world.Player.Tick(.05f);
            Check(world.Player.Ammo==30&&world.Player.ReloadLeft==0,"Reload completes after its timer");
            world.Command.Damage(10000,null);
            Check(world.Phase==Phase.Defeat,"Destroying the command center ends the mission");
        }
        IEnumerator Capture()
        {
            string path=Argument("--frontier-output");if(path=="")path=Application.persistentDataPath+"/capture.png";
            string scene=Argument("--frontier-scene");
            if(scene.StartsWith("v07-"))
            {
                world.BeginCampaign();world.WaveBannerLeft=0;world.Phase=Phase.Intermission;world.IntermissionLeft=1000;
                if(scene=="v07-base"){FortificationChecks.PlacePlayer(world,new Vector3(0,0,5));world.Player.Yaw=0;world.Player.Pitch=7;}
                if(scene=="v07-enemy"){FortificationChecks.PlacePlayer(world,new Vector3(-27,-4,66));world.Player.Yaw=0;world.Player.Pitch=-3;}
                if(scene=="v07-terminal"){CheckCommand.Enter(world);}
                if(scene=="v07-workers"){world.Command.Damage(450,null);for(int i=0;i<150;i++){world.SimTime+=.05f;world.Repairs.Tick(.05f);Physics.SyncTransforms();}FortificationChecks.PlacePlayer(world,new Vector3(-9,0,4));world.Player.Yaw=165;world.Player.Pitch=8;}
                world.Player.UpdateCamera(1);
            }
            if(scene.StartsWith("v06-"))
            {
                world.BeginCampaign();world.WaveBannerLeft=0;
                if(scene=="v06-enemy")
                {
                    FortificationChecks.PlacePlayer(world,new Vector3(-13.5f,-4,55));world.Player.Yaw=0;world.Player.Pitch=0;world.Player.UpdateCamera(1);
                }
                else
                {
                    world.Phase=Phase.Intermission;world.IntermissionLeft=1000;world.Credits=10000;
                    CheckCommand.Enter(world);world.Construction.Build(0);world.Construction.Build(1);world.Construction.Build(4);CheckCommand.Leave(world);
                    SquadChecks.FillSquad(world);FortificationChecks.PlacePlayer(world,new Vector3(0,0,23));world.Player.Yaw=0;world.Squad.GiveOrder(SquadOrder.Hold);
                    for(int step=0;step<2400;step++)
                    {
                        Physics.SyncTransforms();world.Tick(.05f);world.Effects.Tick(.05f);
                        if(step%240==0)yield return null;
                    }
                    world.ToastLeft=0;world.IntermissionLeft=35;
                    if(scene=="v06-terminal"||scene=="v06-factory"||scene=="v06-squad")
                    {
                        CheckCommand.Enter(world);yield return null;
                        typeof(GameHud).GetField("terminalPage",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(world.Hud,scene=="v06-factory"?1:scene=="v06-squad"?2:0);
                    }
                    else
                    {
                        FortificationChecks.PlacePlayer(world,new Vector3(-6,0,14));world.Player.Yaw=35;world.Player.Pitch=8;
                        if(scene=="v06-central")world.Phase=Phase.Assault;
                    }
                    world.Player.UpdateCamera(1);
                }
            }
            if(scene=="assault-warning"||scene=="assault-battle")
            {
                world.BeginCampaign();var strategy=new BalanceDriver(world,"victory");strategy.Begin();
                for(int step=0;step<24000&&!world.Ended;step++)
                {
                    strategy.Step();Physics.SyncTransforms();world.Tick(.05f);world.Effects.Tick(.05f);
                    if(world.Wave>=3&&world.Phase==Phase.Assault)
                    {
                        int front=0;foreach(var h in world.Entities)if(h!=null&&h.Alive&&h.Faction==Faction.Invaders&&h.transform.position.z<=31)front++;
                        if(scene=="assault-warning"||front>=12)break;
                    }
                    if(step%240==0)yield return null;
                }
                world.Player.Bot=false;
                if(scene=="assault-battle")world.WaveBannerLeft=0;
                world.Player.UpdateCamera(1);
            }
            if(scene=="construction"||scene=="technology"||scene=="expanded")ConstructionChecks.PrepareCapture(world,scene);
            if(scene=="ramps")
            {
                world.BeginCampaign();world.WaveBannerLeft=0;
                FortificationChecks.PlacePlayer(world,new Vector3(25.5f,-4,31));
                world.Player.Yaw=-90;world.Player.Pitch=2;world.Player.UpdateCamera(1);
            }
            if(scene=="guards")
            {
                world.BeginCampaign();world.Phase=Phase.Intermission;world.IntermissionLeft=1000;world.Credits=1000;
                world.Construction.Build(1);
                for(int i=0;i<1600;i++){Physics.SyncTransforms();world.Tick(.05f);world.Effects.Tick(.05f);}
                world.Phase=Phase.Assault;FortificationChecks.PlacePlayer(world,world.Fort.EntranceFor(world.Bunker));
                world.Fort.TryTogglePlayer();world.WaveBannerLeft=0;world.Player.Pitch=3;world.Player.UpdateCamera(1);
            }
            if(scene=="terminal"||scene=="battle"||scene=="fort"||scene=="squad")
            {
                world.BeginCampaign();world.TryOpenCommandTerminal();world.Credits=1000;
                world.Squad.Hire();world.Squad.Hire();
                if(scene=="battle"||scene=="fort"||scene=="squad")
                {
                    world.SetPaused(false);SquadChecks.FillSquad(world);
                    if(scene=="squad"){FortificationChecks.PlacePlayer(world,new Vector3(0,0,3));world.TryOpenCommandTerminal();}
                }
            }
            if(scene=="upgrade")
            {
                world.BeginCampaign();world.Phase=Phase.Intermission;world.IntermissionLeft=45;world.Credits=1600;
                SquadChecks.FillSquad(world);
                world.Upgrades.Buy(BaseUpgrade.BarracksProduction);world.Upgrades.Buy(BaseUpgrade.BunkerArmor);world.Upgrades.Buy(BaseUpgrade.BunkerFirepower);
                world.Barracks.Damage(380,null);world.Bunker.Damage(460,null);world.WaveBannerLeft=0;
                world.Player.ReleaseControl();world.Player.UpdateCamera(1);
            }
            if(scene=="fort")
            {
                FortificationChecks.PlacePlayer(world,world.Fort.Entrance);
                world.Fort.OrderSquadInside();
                for(int step=0;step<240;step++)
                {
                    world.SimTime+=.05f;Physics.SyncTransforms();world.Fort.Tick(.05f);world.Squad.Tick(.05f);world.Effects.Tick(.05f);
                    if(world.Fort.SquadInside==4)break;
                }
                world.Fort.TryTogglePlayer();
                world.SpawnEnemy(Role.Runner,1,new Vector3(13,0,21));world.SpawnEnemy(Role.Spitter,1,new Vector3(16,0,23));
                world.SpawnEnemy(Role.Colossus,1,new Vector3(18,0,26));
                world.Player.Yaw=0;world.Player.Pitch=5;world.WaveBannerLeft=0;world.Player.UpdateCamera(1);
            }
            if(scene=="battle")
            {
                world.Player.Bot=true;
                for(int frame=0;frame<60;frame++)
                {
                    for(int step=0;step<10;step++){Physics.SyncTransforms();world.Tick(.05f);world.Effects.Tick(.05f);}
                    yield return null;
                }
                world.Player.Bot=false;world.Player.Yaw=-1;world.Player.Pitch=11;
                world.Player.UpdateCamera(1);
            }
            yield return null;
            if(scene=="squad")typeof(GameHud).GetField("terminalPage",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(world.Hud,2);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            for(int i=0;i<20;i++)yield return null;
            Debug.Log("FRONTIER_CAPTURE "+path);finished=true;Application.Quit(0);
        }
        void Complete()
        {
            finished=true;world.Stats.Snapshot(world.Phase==Phase.Victory);
            report.outcome=world.Phase.ToString();report.passed=report.failures.Count==0;
            report.waves=world.Stats.Waves;report.suppliesEarned=world.Stats.SuppliesEarned;
            report.endingSupplies=world.Credits;report.suppliesSpent=160+report.suppliesEarned-world.Credits;
            report.guardLosses=world.Stats.GuardLosses;report.squadLosses=world.Stats.SquadLosses;report.buildingsLost=world.Stats.BuildingsLost;
            report.headquartersDamage=world.Stats.HeadquartersDamage;report.bunkerDamage=world.Stats.BunkerDamage;report.barracksDamage=world.Stats.BarracksDamage;
            report.peakFrontlineEnemies=world.Stats.PeakFrontlineEnemies;report.dualLanePressureSeconds=world.Stats.DualLanePressureSeconds;
            report.tanksProduced=world.Tanks.Produced;report.tanksLost=world.Tanks.Lost;report.tankKills=world.Tanks.Kills;report.tankShots=world.Tanks.Shots;report.tankDeployed=world.Tanks.Deployed;report.enemyTowersDestroyed=world.EnemyDefenses.DestroyedCount;
            report.suppressedEnemies=world.Assault.TotalSuppressed;report.portalsDestroyed=world.EnemyDefenses.DestroyedPortals;report.expeditionsProduced=world.Expeditions.Produced;report.expeditionDepartures=world.Expeditions.Departures;report.expeditionShots=world.Expeditions.Shots;report.workersAlive=world.Repairs.WorkersAlive;report.repairedHealth=world.Repairs.TotalRepaired;
            report.simulatedSeconds=world.SimTime;report.hqHealth=world.Command.Current;report.wave=world.Wave;
            report.spawned=world.TotalSpawned;report.kills=world.Kills;report.playerKills=world.PlayerKills;report.deaths=world.Deaths;
            report.peakEnemies=world.PeakEnemies;report.peakMarines=world.PeakMarines;
            report.mercenaryContracts=world.Squad.Contracts;report.recruitedGuards=world.Squad.Recruited;report.mercenaryRespawns=world.Squad.MercenaryRespawns;report.peakMarineCapacity=world.PeakMarineCapacity;report.spawnPointUses=world.SpawnPointUses;
            report.squadKills=world.Squad.Kills;report.squadHired=world.Squad.Hired;report.squadPeak=world.Squad.PeakCount;report.criticalHits=world.Player.CriticalHits;
            report.barracksLevel=world.Upgrades.BarracksLevel;report.bunkerArmorLevel=world.Upgrades.ArmorLevel;report.bunkerFirepowerLevel=world.Upgrades.FirepowerLevel;
            foreach(var site in world.Construction.Sites)if(site.State==SiteState.Ready)report.readySites++;
            foreach(Research research in Enum.GetValues(typeof(Research)))if(world.Tech.IsComplete(research))report.completedResearch++;
            foreach(var h in world.Entities)if(h!=null&&h.Alive)report.actors.Add(h.Role+" "+h.transform.position+" hp="+h.Current+" moved="+(h.Brain!=null?h.Brain.Travelled:0)+" visible="+Combat.Visible(world.Player.Self,h));
            string path=Argument("--frontier-output");if(path=="")path=Application.persistentDataPath+"/"+mode+".json";
            File.WriteAllText(path,JsonUtility.ToJson(report,true));
            Debug.Log("FRONTIER_CHECK "+JsonUtility.ToJson(report));Application.Quit(report.passed?0:2);
        }
    }
}

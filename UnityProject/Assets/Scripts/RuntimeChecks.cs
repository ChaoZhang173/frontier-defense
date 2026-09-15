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
            public int wave,spawned,kills,playerKills,deaths,peakEnemies,peakMarines;
            public List<string> actors=new List<string>();public List<string> checks=new List<string>();public List<string> failures=new List<string>();
        }
        public static string Argument(string key)
        {
            foreach(string arg in Environment.GetCommandLineArgs())if(arg.StartsWith(key+"="))return arg.Substring(key.Length+1);
            return "";
        }
        public static bool Requested=>Argument("--frontier-check")!="";
        GameWorld world;Report report;string mode;int frames;bool finished,capturing;
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
                if(mode=="mechanics"){Mechanics();Complete();return;}
                world.Player.Bot=mode=="victory";world.BeginCampaign();
                if(mode=="defeat")
                {
                    world.Barracks.Damage(10000,null);world.Bunker.Damage(10000,null);
                    foreach(var h in world.Entities.ToArray())if(h!=null&&h.Role==Role.Marine)h.Damage(10000,null);
                }
            }
            for(int n=0;n<24&&!world.Ended;n++)
            {
                if(world.Phase==Phase.Intermission)
                {
                    if(mode=="victory")
                    {
                        if(world.Command.Current<world.Command.Maximum-600)world.Buy(2);
                        world.Buy(0);world.Buy(1);
                    }
                    world.StartWave();
                }
                Physics.SyncTransforms();world.Tick(.05f);world.Effects.Tick(.05f);
                if(world.SimTime>1500){Check(false,"Simulation finishes within 25 minutes");Complete();return;}
            }
            if(world.Ended)
            {
                Check(mode=="victory"?world.Phase==Phase.Victory:world.Phase==Phase.Defeat,"Expected "+mode+" outcome");
                if(mode=="victory")
                {
                    Check(world.TotalSpawned==198&&world.Kills==198,"All 198 scheduled invaders spawned and defeated");
                    Check(world.Wave==5&&world.EnemiesAlive==0&&world.RemainingToSpawn==0,"Victory waits for the complete fifth wave");
                    Check(world.PlayerKills>20,"Player bot uses actual rifle hits");
                }
                Check(world.PeakMarines<=8,"Garrison respects eight-unit cap");
                Check(world.PeakEnemies<=55,"Concurrent enemy count stays bounded");
                Complete();
            }
        }
        void Check(bool success,string label){if(success)report.checks.Add(label);else report.failures.Add(label);}
        void Mechanics()
        {
            Physics.SyncTransforms();
            Check(world.Entities.Count==8,"HQ, barracks, bunker, player and four initial defenders exist");
            int total=0;for(int i=1;i<=5;i++)total+=WavePlan.Create(i).Count;
            Check(total==198,"Five wave definitions contain exactly 198 invaders");
            for(int i=0;i<14;i++)world.SpawnMarine();
            Check(world.MarinesAlive==8,"Production refuses units beyond its cap");
            float hp=world.Command.Current;world.Command.Damage(100,world.Player.Self);
            Check(world.Command.Current==hp,"Friendly rifle damage cannot harm the base");
            world.Phase=Phase.Intermission;world.Credits=0;
            Check(!world.Buy(0)&&world.Credits==0&&world.WeaponLevel==0,"Insufficient credits do not purchase or go negative");
            world.Credits=10000;for(int i=0;i<10;i++)world.Buy(0);
            Check(world.WeaponLevel==4,"Player upgrade has a hard level cap");
            int before=world.Credits;Check(!world.Buy(0)&&world.Credits==before,"Maxed upgrades never charge credits");
            world.Command.Current=2200;world.Buy(2);
            Check(world.Command.Current==2300,"Repairs clamp at maximum health");
            before=world.Credits;Check(!world.Buy(2)&&world.Credits==before,"Full-health repair never charges credits");
            Health enemy=world.SpawnEnemy(Role.Runner,0,new Vector3(0,0,-17));Physics.SyncTransforms();
            float enemyHp=enemy.Current;
            Combat.Rifle(world.Player.Self,world.Player.Self.Aim,(enemy.Aim-world.Player.Self.Aim).normalized,1000,true);
            Check(enemy.Current==enemyHp,"HQ collider blocks rifle fire at an enemy behind it");
            enemy.Damage(9999,null);
            world.Phase=Phase.Briefing;world.WeaponLevel=0;world.Wave=0;world.BeginCampaign();
            before=world.Credits;Check(!world.Buy(1)&&world.Credits==before,"Purchases are disabled during an assault");
            world.Tick(.05f);
            Check(world.Phase==Phase.Assault&&world.EnemiesAlive==0&&world.RemainingToSpawn==18,"An empty field does not finish a wave with pending spawns");
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
            if(Argument("--frontier-scene")=="battle")
            {
                world.Player.Bot=true;world.BeginCampaign();
                for(int frame=0;frame<60;frame++)
                {
                    for(int step=0;step<10;step++){Physics.SyncTransforms();world.Tick(.05f);world.Effects.Tick(.05f);}
                    yield return null;
                }
                world.Player.Bot=false;world.Player.Yaw=-1;world.Player.Pitch=11;
                world.Player.UpdateCamera(1);
            }
            yield return null;yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            for(int i=0;i<20;i++)yield return null;
            Debug.Log("FRONTIER_CAPTURE "+path);finished=true;Application.Quit(0);
        }
        void Complete()
        {
            finished=true;report.outcome=world.Phase.ToString();report.passed=report.failures.Count==0;
            report.simulatedSeconds=world.SimTime;report.hqHealth=world.Command.Current;report.wave=world.Wave;
            report.spawned=world.TotalSpawned;report.kills=world.Kills;report.playerKills=world.PlayerKills;report.deaths=world.Deaths;
            report.peakEnemies=world.PeakEnemies;report.peakMarines=world.PeakMarines;
            foreach(var h in world.Entities)if(h!=null&&h.Alive)report.actors.Add(h.Role+" "+h.transform.position+" hp="+h.Current+" moved="+(h.Brain!=null?h.Brain.Travelled:0)+" visible="+Combat.Visible(world.Player.Self,h));
            string path=Argument("--frontier-output");if(path=="")path=Application.persistentDataPath+"/"+mode+".json";
            File.WriteAllText(path,JsonUtility.ToJson(report,true));
            Debug.Log("FRONTIER_CHECK "+JsonUtility.ToJson(report));Application.Quit(report.passed?0:2);
        }
    }
}

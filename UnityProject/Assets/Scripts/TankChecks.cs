using System;
using UnityEngine;

namespace Frontier
{
    public static class TankChecks
    {
        public static void Run(GameWorld w,Action<bool,string> check)
        {
            var tanks=w.Tanks;var construction=w.Construction;var site=construction.Sites[TankDirector.FactorySiteId];
            check(site.Id==4&&site.Role==Role.Factory&&site.State==SiteState.Empty&&tanks.Tank==null&&tanks.Produced==0,
                "A fresh mission has an empty fifth factory site and no tank");
            check(construction.Cost(4)==650&&site.Duration==28&&construction.RepairCost(4)==180&&TankDirector.ProductionDuration==24,
                "The factory advertises 650 supplies, 28 seconds construction, 24 seconds production and a 180-supply repair");
            w.BeginCampaign();w.Player.Bot=false;w.Player.ReleaseControl();w.Phase=Phase.Intermission;w.IntermissionLeft=10000;
            FreezeActors(w);int capacity=w.MarineCapacity;StepWorld(w,25);
            check(tanks.Tank==null&&tanks.Produced==0&&tanks.ProductionLeft==24,"Without an operational factory no tank is produced and its timer does not run");
            w.Credits=10000;int credits=w.Credits;
            check(!construction.Build(4)&&w.Credits==credits,"A wave break alone cannot issue a factory order away from the command terminal");
            check(CheckCommand.Enter(w),"The player opens the real command terminal to order the factory");
            w.Credits=649;
            check(!construction.Build(4)&&w.Credits==649&&site.State==SiteState.Empty,"Insufficient factory funds neither charge nor begin construction");
            w.Credits=10000;credits=w.Credits;
            check(construction.SpaceClear(4)&&construction.Build(4)&&w.Credits==credits-650&&site.State==SiteState.Constructing,
                "The unobstructed garage footprint accepts a real 650-supply construction order");
            if(site.State!=SiteState.Constructing)return;
            Health foundation=site.Structure;float constructionLeft=site.SecondsLeft;w.Tick(40);
            check(site.Structure==foundation&&foundation.Role==Role.Construction&&site.SecondsLeft==constructionLeft&&tanks.Produced==0,
                "The paused command terminal freezes construction and cannot prematurely produce a tank");
            credits=w.Credits;check(!construction.Build(4)&&w.Credits==credits,"An active garage construction cannot be purchased a second time");
            CheckCommand.Leave(w);StepWorld(w,27.7f);
            check(site.State==SiteState.Constructing&&tanks.Tank==null,"Factory construction remains unfinished until its full 28 seconds elapse");
            StepWorld(w,.5f);
            check(site.State==SiteState.Ready&&site.Structure!=foundation&&site.Structure.Role==Role.Factory&&site.Structure.IsBuilding&&
                site.Structure.Maximum==1400&&tanks.Operational&&w.MarineCapacity==capacity,
                "The completed 1400-health factory becomes a building without changing the guard limit");
            Health factory=site.Structure;factory.Damage(850,null);CheckCommand.Enter(w);credits=w.Credits;
            check(construction.RepairSite(4)&&factory.Current==1250&&w.Credits==credits-180,
                "A factory repair at the command terminal spends 180 and restores exactly 700 health");
            float production=tanks.ProductionLeft;w.Tick(30);
            check(tanks.ProductionLeft==production&&tanks.Tank==null,"Tank production freezes while the command terminal is open");
            CheckCommand.Leave(w);StepWorld(w,Mathf.Max(0,production-.3f));
            check(tanks.Tank==null&&tanks.Produced==0,"The first tank is not deployed before its complete production timer");
            // A real allied body blocks the exit. Production waits at zero rather than spawning on it.
            Health blocker=w.Entities.Find(h=>h!=null&&h.Alive&&h.Role==Role.Marine);
            Vector3 original=blocker.transform.position;w.Fort.ReleaseGuard(blocker);blocker.transform.position=tanks.SpawnPoint;Physics.SyncTransforms();
            StepWorld(w,.6f);
            check(tanks.Tank==null&&tanks.ProductionLeft==0&&tanks.Status.Contains("出口受阻"),
                "A guard occupying the physical garage exit delays the completed tank without discarding production");
            blocker.transform.position=original;Physics.SyncTransforms();StepWorld(w,.05f);
            Health first=tanks.Tank;
            check(first!=null&&first.Alive&&first.Role==Role.Tank&&first.Faction==Faction.Defense&&first.Brain==null&&
                first.Maximum==1000&&tanks.Produced==1&&Vector3.Distance(first.transform.position,tanks.SpawnPoint)<.01f,
                "Clearing the exit spawns exactly one 1000-health friendly tank at the garage door with its independent controller");
            if(first==null)return;
            Vector3 start=first.transform.position;bool clear=true;float maximumStep=0;
            for(int i=0;i<900&&!tanks.Deployed;i++)
            {
                Vector3 before=first.transform.position;StepWorld(w,.05f);
                maximumStep=Mathf.Max(maximumStep,Vector3.Distance(before,first.transform.position));
                clear&=!Physics.CheckBox(first.transform.position+Vector3.up*1.45f,new Vector3(2.35f,1.30f,2.35f),
                    Quaternion.identity,Combat.WorldMask,QueryTriggerInteraction.Ignore);
            }
            check(clear&&maximumStep<=TankDirector.Speed*.05f+.001f,
                "The complete tank hull drives around the reactor and buildings without clipping or teleporting");
            check(tanks.Deployed&&Vector3.Distance(first.transform.position,tanks.DeploymentPoint)<=.3f&&
                tanks.Travelled>=Vector3.Distance(start,tanks.DeploymentPoint)-.3f,
                "The tank travels from the factory to the real central defense position within 45 seconds");
            if(!tanks.Deployed)Debug.Log("TANK_ROUTE start="+start+" final="+first.transform.position+" travelled="+tanks.Travelled+" maximumStep="+maximumStep);
            StepWorld(w,30);
            check(tanks.Tank==first&&tanks.Produced==1&&AliveTanks(w)==1&&w.MarineCapacity==capacity,
                "An operating factory never produces a second living tank and the tank consumes no guard slots");
            CheckCannon(w,check);

            int lost=tanks.Lost;first.Damage(10000,null);
            check(tanks.Tank==null&&tanks.Lost==lost+1&&AliveTanks(w)==0&&tanks.ProductionLeft==24,
                "A real tank death removes its live slot and starts the ordinary 24-second replacement timer");
            StepWorld(w,8);production=tanks.ProductionLeft;factory.Damage(10000,null);
            check(!tanks.Operational&&site.State==SiteState.Destroyed,"Destroying the factory stops its production and leaves its original site rebuildable");
            StepWorld(w,30);
            check(tanks.Tank==null&&tanks.ProductionLeft==production,"Factory downtime retains replacement progress without producing a tank");
            CheckCommand.Enter(w);credits=w.Credits;
            check(!construction.RepairSite(4)&&w.Credits==credits,"Repair cannot revive a destroyed factory");
            check(construction.Build(4)&&w.Credits==credits-650&&site.Structure!=factory,"Reconstruction charges a new factory investment and uses a new foundation identity");
            CheckCommand.Leave(w);StepWorld(w,27.7f);
            check(!tanks.Operational&&tanks.ProductionLeft==production,"Replacement production remains suspended while the factory is under reconstruction");
            StepWorld(w,.5f);
            check(tanks.Operational&&site.Structure!=factory&&tanks.ProductionLeft<production,"Completing the replacement factory automatically resumes retained production progress");
            StepWorld(w,Mathf.Max(0,tanks.ProductionLeft-.3f));
            check(tanks.Tank==null,"The replacement tank still requires all remaining active factory production time");
            StepWorld(w,.5f);Health replacement=tanks.Tank;
            check(replacement!=null&&replacement!=first&&replacement.Alive&&AliveTanks(w)==1&&tanks.Produced==2,
                "The factory replaces a destroyed tank with one new live vehicle at its physical exit");
            if(replacement==null)return;
            site.Structure.Damage(10000,null);StepWorld(w,45);
            check(tanks.Tank==replacement&&replacement.Alive&&tanks.Deployed&&!tanks.Operational,
                "Losing the factory preserves the existing tank, which continues driving and holding its central position");
            int shots=tanks.Shots;Vector3 position=replacement.transform.position;w.SetPaused(true);w.Tick(30);tanks.Tick(30);
            check(tanks.Shots==shots&&replacement.transform.position==position,"A general pause also freezes the independent tank controller");
            w.SetPaused(false);
        }

        static void CheckCannon(GameWorld w,Action<bool,string> check)
        {
            var tanks=w.Tanks;Health tank=tanks.Tank;
            Health runner=Enemy(w,Role.Runner,new Vector3(-5,0,19));
            Health heavy=Enemy(w,Role.Colossus,new Vector3(0,-4,43));
            Health splash=Enemy(w,Role.Spitter,new Vector3(1.65f,-4,43));
            float hp=heavy.Current,splashHp=splash.Current;int shots=tanks.Shots;
            StepTank(w,.05f);
            check(tanks.Target==heavy,"The cannon prioritizes a visible colossus over a closer ordinary attacker");
            StepTank(w,5);
            check(tanks.Shots>shots&&heavy.Current<hp&&tanks.MuzzlePosition.y>1.2f,
                "Actual moving tank shells clear the lowered central wall and damage a target outside the base");
            check(splash.Current<splashHp,"A real shell impact also damages a nearby enemy through its visible blast radius");
            runner.Damage(100000,null);splash.Damage(100000,null);
            // Existing allied units can stand near an impact without taking splash damage.
            Health ally=w.Entities.Find(h=>h!=null&&h.Alive&&h.Role==Role.Marine);w.Fort.ReleaseGuard(ally);
            Vector3 original=ally.transform.position;ally.transform.position=new Vector3(-1.5f,-4,43);Physics.SyncTransforms();
            float allyHp=ally.Current;StepTank(w,4);
            check(ally.Current==allyHp,"The cannon's real blast excludes allied units near the impact");
            ally.transform.position=original;Physics.SyncTransforms();
            var wall=Art.Part(w.transform,"Tank diagnostic shot blocker",new Vector3(0,0,33),new Vector3(18,12,1),2,collision:true);
            Physics.SyncTransforms();StepTank(w,2);hp=heavy.Current;shots=tanks.Shots;StepTank(w,4);
            check(heavy.Current==hp&&tanks.Shots==shots,"A solid wall blocks target acquisition and prevents firing at the hidden enemy");
            wall.gameObject.SetActive(false);UnityEngine.Object.Destroy(wall.gameObject);Physics.SyncTransforms();
            shots=tanks.Shots;for(int i=0;i<100&&tanks.Shots==shots;i++)StepTank(w,.05f);
            hp=heavy.Current;
            wall=Art.Part(w.transform,"Tank diagnostic in-flight blocker",new Vector3(0,0,33),new Vector3(18,12,1),2,collision:true);
            Physics.SyncTransforms();StepTank(w,1.2f);
            check(tanks.Shots>shots&&heavy.Current==hp,"A shell already in flight hits a newly inserted world obstacle instead of passing through it");
            wall.gameObject.SetActive(false);UnityEngine.Object.Destroy(wall.gameObject);Physics.SyncTransforms();
            heavy.Current=1;int kills=tanks.Kills;StepTank(w,5);
            check(!heavy.Alive&&tanks.Kills==kills+1,"An actual cannon kill is recorded once by the tank controller");
            tank.Heal(tank.Maximum);
        }

        static Health Enemy(GameWorld w,Role role,Vector3 point)
        {
            Health h=w.SpawnEnemy(role,0,point);h.Brain=null;h.WeakRadius=0;h.Current=h.Maximum=10000;Physics.SyncTransforms();return h;
        }
        static int AliveTanks(GameWorld w)
        {
            int count=0;foreach(var h in w.Entities)if(h!=null&&h.Alive&&h.Role==Role.Tank)count++;return count;
        }
        static void FreezeActors(GameWorld w)
        {
            foreach(var h in w.Entities)if(h!=null)h.Brain=null;
        }
        static void StepWorld(GameWorld w,float seconds)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds/.05f);i++)
            {
                float dt=Mathf.Min(.05f,seconds-i*.05f);if(dt<=0)break;
                FreezeActors(w);Physics.SyncTransforms();w.Tick(dt);w.Effects.Tick(dt);
            }
        }
        static void StepTank(GameWorld w,float seconds)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds/.05f);i++)
            {
                float dt=Mathf.Min(.05f,seconds-i*.05f);if(dt<=0)break;
                w.SimTime+=dt;Physics.SyncTransforms();w.Tanks.Tick(dt);w.Effects.Tick(dt);
            }
        }
    }
}

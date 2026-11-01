using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public static class EnemyBaseChecks
    {
        public static void Run(GameWorld world,Action<bool,string> check)
        {
            var defenses=world.EnemyDefenses;
            check(defenses!=null&&defenses.Bases.Count==6&&defenses.Towers.Count==12,"Six persistent enemy bases each own a portal, nest and two defense towers");
            if(defenses==null||defenses.Bases.Count!=6)return;
            int scheduled=world.EnemiesAlive,spawned=world.TotalSpawned;
            bool layout=true,tiers=true,solids=true;
            foreach(var site in defenses.Bases)
            {
                layout&=Vector3.Distance(site.Position,GameWorld.EnemySpawnPoints[site.Id])<.01f&&site.Towers.Count==2;
                tiers&=site.Portal.Maximum==EnemyDefenseDirector.PortalHealthAt(site.Id);
                foreach(var tower in site.Towers)tiers&=tower.Maximum==EnemyDefenseDirector.TowerHealthAt(site.Id)&&tower.BaseId==site.Id;
                foreach(var building in new[]{site.Portal,site.Nest,site.Towers[0],site.Towers[1]})
                    solids&=building.Alive&&building.IsBuilding&&building.Faction==Faction.Invaders&&building.GetComponent<Collider>()!=null&&!building.GetComponent<Collider>().isTrigger;
            }
            check(layout&&tiers&&solids,"Portal bases use the expanded positions, tiered durability and real hostile collision geometry");
            check(scheduled==0&&spawned==0&&world.EnemyMobileCount==12,"Twelve initial local sentries do not consume scheduled assault-wave counts");
            check(EnemyDefenseDirector.Range==30&&EnemyDefenseDirector.TowerDamageAt(0)==28&&EnemyDefenseDirector.TowerDamageAt(2)==34&&EnemyDefenseDirector.TowerDamageAt(4)==40,"All base tiers can return fire at the assault infantry's 25-metre range");

            Physics.SyncTransforms();bool ground=true;
            foreach(var point in new[]{new Vector3(-56,-4,130),new Vector3(56,-4,130),new Vector3(0,-4,134),new Vector3(-13.5f,-4,44)})
                ground&=Physics.Raycast(point+Vector3.up*6,Vector3.down,out var hit,8,Combat.WorldMask,QueryTriggerInteraction.Ignore)&&Mathf.Abs(hit.point.y-GroundSurface.Height(point.x,point.z))<.02f;
            check(ground&&GroundSurface.WalkableTerrain(-58,134)&&GroundSurface.WalkableTerrain(58,134)&&!GroundSurface.WalkableTerrain(61,134),"Expanded far-map terrain and physical ground agree within the shared movement bounds");
            bool ruins=true;foreach(var ruin in EnemyFronts.Ruins)
                ruins&=Physics.CheckBox(ruin.center,ruin.extents*.9f,Quaternion.identity,Combat.WorldMask,QueryTriggerInteraction.Ignore);
            check(ruins&&EnemyFronts.Ruins.Length==8,"All eight visible ruined outposts have matching solid collision volumes");
            foreach(var site in defenses.Bases)
            {
                Vector3 from=new Vector3(site.Position.x<0?-13.5f:13.5f,0,22),goal=site.Position+Vector3.back*14;
                var path=new List<Vector3>();bool route=world.Squad.Navigation.Plan(from,goal,path,.42f);Vector3 previous=from;
                foreach(var point in path){route&=world.Squad.Navigation.Clear(previous,point,.42f);previous=point;}
                check(route&&path.Count>0&&Vector3.Distance(path[path.Count-1],goal)<1.5f,"Friendly troops can walk through an exit and around ruins to portal base "+site.Id);
            }
            check(world.Squad.Navigation.Fits(new Vector3(13.5f,-4,46),.74f)&&world.Squad.Navigation.Fits(new Vector3(13.5f,GroundSurface.Height(13.5f,32),32),.74f),"Large enemy navigation clearance stays above canyon ground and the sloped ramps");

            world.BeginCampaign();world.Phase=Phase.Intermission;world.IntermissionLeft=1000;
            world.Player.Bot=false;world.Player.ReleaseControl();
            Health selected=defenses.Bases[0].Towers[1];
            FortificationChecks.PlacePlayer(world,selected.transform.position+Vector3.back*26.5f);world.Player.Self.Heal(10000);
            int shots=defenses.Shots;float hp=world.Player.Self.Current;
            for(int i=0;i<60;i++){world.SimTime+=.05f;Physics.SyncTransforms();defenses.Tick(.05f);world.Effects.Tick(.05f);}
            check(defenses.Shots>shots&&world.Player.Self.Current<hp,"Real tower projectiles retaliate against a target 26.5 metres away instead of allowing free-range structure sniping");
            FortificationChecks.PlacePlayer(world,new Vector3(0,0,-17));
            shots=defenses.Shots;int mobile=world.EnemyMobileCount;world.SetPaused(true);defenses.Tick(60);
            check(defenses.Shots==shots&&world.EnemyMobileCount==mobile,"Pausing freezes both tower attacks and base-garrison replenishment");world.SetPaused(false);
            defenses.Tick(29);defenses.Tick(29);
            bool guarded=true;foreach(var site in defenses.Bases)guarded&=GuardCount(world,site.Id)==4;
            check(guarded&&world.EnemiesAlive==scheduled&&world.TotalSpawned==spawned,"Each living portal-and-nest pair replenishes up to four local guards without modifying assault counts");

            var disabled=defenses.Bases[0];disabled.Nest.Damage(10000,world.Player.Self);
            foreach(var entity in world.Entities.ToArray())if(entity!=null&&entity.Alive&&entity.BaseGuard&&entity.BaseId==disabled.Id)entity.Damage(10000,world.Player.Self);
            defenses.Tick(60);
            check(disabled.Portal.Alive&&GuardCount(world,disabled.Id)==0,"Destroying a garrison nest stops local reinforcements while its portal can remain alive");
            var portal=defenses.Bases[1].Portal;var navigation=world.Squad.Navigation;int credits=world.Credits;
            check(!defenses.OnKilled(portal),"A living portal cannot register a destruction reward");
            portal.Damage(10000,world.Player.Self);
            check(!defenses.IsPortalAlive(1)&&!portal.gameObject.activeSelf&&world.Squad.Navigation!=navigation,"Destroying a portal removes its luminous model and collider and rebuilds troop navigation");
            check(defenses.NearestAlivePortal(portal.transform.position)!=portal&&defenses.DestroyedPortals==1,"Further expeditions choose another live portal after their previous target falls");
            check(world.Credits==credits+EnemyDefenseDirector.PortalReward&&!defenses.OnKilled(portal),"Portal destruction grants one bounty and rejects duplicate death registration");
            check(world.EnemiesAlive==scheduled&&world.TotalSpawned==spawned,"Destroying bases and their local sentries cannot complete or inflate a scheduled mobile wave");

            // A siege beast must use its advertised range against field infantry,
            // even when no friendly building is near enough to become its objective.
            var siege=world.SpawnEnemy(Role.Siege,0,new Vector3(-58,-4,123));
            FortificationChecks.PlacePlayer(world,new Vector3(-58,-4,94));
            world.Player.Self.Heal(10000);float playerHp=world.Player.Self.Current;
            for(int i=0;i<55;i++){world.SimTime+=.05f;Physics.SyncTransforms();siege.Brain.Tick(.05f);world.Effects.Tick(.05f);}
            check(siege.Brain.Target==world.Player.Self&&world.Player.Self.Current<playerHp,
                "A siege beast acquires and damages field infantry 29 metres away without needing a nearby building target");
        }

        static int GuardCount(GameWorld world,int id)
        {
            int count=0;foreach(var entity in world.Entities)if(entity!=null&&entity.Alive&&entity.BaseGuard&&entity.BaseId==id)count++;return count;
        }
    }
}

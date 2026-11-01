using System;
using UnityEngine;

namespace Frontier
{
    public static class EnemyDefenseChecks
    {
        const float StepSize=.05f;

        public static void CheckRestart(GameWorld w,Action<bool,string> check)
        {
            var defenses=w.EnemyDefenses;
            bool restored=defenses!=null&&defenses.Towers.Count==12&&defenses.DestroyedCount==0&&defenses.Shots==0;
            if(defenses!=null)foreach(var tower in defenses.Towers)
                restored&=tower!=null&&tower.Alive&&tower.Current==EnemyDefenseDirector.TowerHealthAt(tower.BaseId);
            check(restored,"A fresh campaign restores twelve undamaged portal defenses and clears destruction and firing records");
        }

        public static void Run(GameWorld w,Action<bool,string> check)
        {
            CheckRestart(w,check);
            var defenses=w.EnemyDefenses;
            if(defenses==null||defenses.Towers.Count!=12)return;
            int enemies=w.EnemiesAlive,total=w.TotalSpawned,kills=w.Kills;
            bool layout=true,buildings=true;
            for(int i=0;i<defenses.Towers.Count;i++)
            {
                Health tower=defenses.Towers[i];
                float gap=Vector3.Distance(tower.transform.position,GameWorld.EnemySpawnPoints[tower.BaseId]);
                layout&=gap>=10&&gap<=11&&tower.transform.position.z<GameWorld.EnemySpawnPoints[tower.BaseId].z&&
                    tower.GetComponent<Collider>()!=null&&!tower.GetComponent<Collider>().isTrigger;
                buildings&=tower.Role==Role.EnemyTower&&tower.Faction==Faction.Invaders&&tower.IsBuilding&&tower.Brain==null;
            }
            check(layout,"Each portal has two solid defenses on its forward flanks, clear of the central departure lane");
            check(buildings,"Rift defenses are stationary hostile buildings without a moving infantry brain");
            check(enemies==0&&total==0&&kills==0,"Constructing all twelve hostile defenses does not add assault enemies, spawns or kills");
            check(EnemyDefenseDirector.TowerHealth==900&&EnemyDefenseDirector.Range==30&&EnemyDefenseDirector.DamagePerShot==28&&
                EnemyDefenseDirector.FireInterval==1.5f&&EnemyDefenseDirector.Reward==150,
                "Rift defense durability, range, shot strength, interval and reward match their advertised balance values");

            w.BeginCampaign();w.Phase=Phase.Intermission;w.IntermissionLeft=1000;
            w.Player.Bot=false;w.Player.ReleaseControl();ParkPlayer(w);
            Step(w,1);
            check(defenses.Shots==0,"Distant base defenders are outside every rift defense's acquisition range");
            Health selected=defenses.Towers[0];Vector3 original=selected.transform.position;
            // Isolate one tower for exact projectile/cooldown assertions; the separate
            // enemy-base checks exercise the unmodified twelve-tower layout.
            for(int i=1;i<defenses.Towers.Count;i++)defenses.Towers[i].transform.position=new Vector3(500+i*10,0,500);
            Physics.SyncTransforms();
            Vector3 near=original+Vector3.left*12;
            FortificationChecks.PlacePlayer(w,near);w.Player.Self.Heal(10000);Physics.SyncTransforms();
            float hp=w.Player.Self.Current;int shots=defenses.Shots;
            Step(w,1.1f);
            check(defenses.TargetOf(selected)==w.Player.Self&&defenses.Shots>shots,
                "A rift defense acquires the actual nearby player and launches projectiles during the between-wave interval");
            check(Mathf.Abs(hp-w.Player.Self.Current-EnemyDefenseDirector.DamagePerShot)<.05f,
                "The real traveling projectile collides with the player and applies one full advertised hit");
            check(selected.transform.position==original,"Tracking and firing leave the defense building at its original position");

            // Use the clear inner flank; the outer flank now contains a solid ruin.
            FortificationChecks.PlacePlayer(w,original+Vector3.right*(EnemyDefenseDirector.Range+3));
            hp=w.Player.Self.Current;shots=defenses.Shots;Step(w,2);
            check(defenses.Shots==shots&&defenses.TargetOf(selected)==null&&w.Player.Self.Current==hp,
                "A player beyond the 30-meter range is neither acquired nor damaged");
            FortificationChecks.PlacePlayer(w,original+Vector3.right*(EnemyDefenseDirector.Range-.4f));
            hp=w.Player.Self.Current;Step(w,1.65f);
            check(w.Player.Self.Current<hp,"A player just inside the range boundary is hit by an actual defense projectile");

            FortificationChecks.PlacePlayer(w,near);Step(w,.9f);w.Player.Self.Heal(10000);
            GameObject cover=MakeCover(original+Vector3.left*6);
            shots=defenses.Shots;hp=w.Player.Self.Current;Step(w,2);
            check(defenses.Shots==shots&&defenses.TargetOf(selected)==null&&w.Player.Self.Current==hp,
                "A solid wall blocks acquisition and fire instead of allowing the defense to shoot through cover");
            cover.SetActive(false);Physics.SyncTransforms();
            shots=defenses.Shots;hp=w.Player.Self.Current;Step(w,.05f);
            check(defenses.Shots==shots+1&&w.Player.Self.Current==hp,
                "A fired projectile requires travel time before damage reaches the target");
            cover.SetActive(true);Physics.SyncTransforms();Step(w,.9f);
            check(w.Player.Self.Current==hp,"Cover introduced after launch intercepts the projectile before it reaches the player");
            cover.SetActive(false);UnityEngine.Object.Destroy(cover);Physics.SyncTransforms();

            w.Player.Self.IsSheltered=true;shots=defenses.Shots;hp=w.Player.Self.Current;Step(w,2);
            check(defenses.Shots==shots&&defenses.TargetOf(selected)==null&&w.Player.Self.Current==hp,
                "A sheltered player is excluded from defense acquisition and damage");
            w.Player.Self.IsSheltered=false;ParkPlayer(w);

            foreach(Role role in new[]{Role.Marine,Role.Squad,Role.Tank})
            {
                Health probe=MakeProbe(w,role,Faction.Defense,near);hp=probe.Current;Step(w,1.15f);
                check(defenses.TargetOf(selected)==probe&&probe.Current<hp,
                    "The real defense acquisition and projectile path attack a nearby "+role+" health and collider");
                probe.IsSheltered=true;hp=probe.Current;shots=defenses.Shots;Step(w,1.5f);
                check(defenses.Shots==shots&&probe.Current==hp,
                    "A sheltered "+role+" cannot be selected or struck by rift defense fire");
                RemoveProbe(w,probe);Step(w,1);
            }

            Health invader=MakeProbe(w,Role.Runner,Faction.Invaders,near);
            shots=defenses.Shots;hp=invader.Current;Step(w,1.5f);
            check(defenses.Shots==shots&&defenses.TargetOf(selected)==null,
                "A rift defense does not acquire a nearby member of its own faction");
            w.Effects.Bolt(selected.Aim,invader.Aim,selected,EnemyDefenseDirector.DamagePerShot,EnemyDefenseDirector.ProjectileSpeed);
            Step(w,.9f);
            check(invader.Current==hp,"A real defense projectile intercepted by an invader does not inflict friendly damage");
            hp=selected.Current;selected.Damage(100,invader);
            check(selected.Current==hp,"Invader damage cannot damage a rift defense of the same faction");
            RemoveProbe(w,invader);Step(w,1);

            FortificationChecks.PlacePlayer(w,near);w.Player.Self.Heal(10000);
            shots=defenses.Shots;hp=w.Player.Self.Current;w.SetPaused(true);defenses.Tick(20);
            check(defenses.Shots==shots&&w.Player.Self.Current==hp,"A direct defense update while paused cannot fire or damage a target");
            w.SetPaused(false);w.Player.ReleaseControl();
            w.Phase=Phase.Briefing;defenses.Tick(20);
            check(defenses.Shots==shots,"Rift defenses stay inactive during the briefing");
            w.Phase=Phase.Victory;defenses.Tick(20);
            check(defenses.Shots==shots,"Rift defenses stop firing after victory");
            w.Phase=Phase.Defeat;defenses.Tick(20);
            check(defenses.Shots==shots,"Rift defenses stop firing after defeat");
            w.Phase=Phase.Intermission;ParkPlayer(w);

            int credits=w.Credits,remaining=w.RemainingToSpawn;
            enemies=w.EnemiesAlive;total=w.TotalSpawned;kills=w.Kills;
            check(!defenses.OnKilled(selected),"A living defense cannot be registered as destroyed or claim a reward");
            selected.Damage(10000,w.Player.Self);
            check(!selected.Alive&&defenses.DestroyedCount==1&&w.Credits==credits+EnemyDefenseDirector.Reward,
                "Destroying the actual hostile building grants its 150-supply reward exactly once");
            credits=w.Credits;selected.Damage(10000,w.Player.Self);bool repeated=defenses.OnKilled(selected);w.OnKilled(selected,w.Player.Self);
            check(!repeated&&defenses.DestroyedCount==1&&w.Credits==credits,
                "Repeated damage and repeated world or director death notifications cannot award a second bounty");
            check(w.EnemiesAlive==enemies&&w.TotalSpawned==total&&w.Kills==kills&&w.RemainingToSpawn==remaining,
                "Defense destruction leaves living assault counts, scheduled spawns, enemy kills and the pending wave queue unchanged");
            foreach(var tower in defenses.Towers)if(tower.Alive)tower.Damage(10000,w.Player.Self);
            check(defenses.DestroyedCount==12&&w.Credits==credits+11*EnemyDefenseDirector.Reward,
                "Each of the other eleven defenses grants its own single bounty");
            int destroyed=defenses.DestroyedCount;shots=defenses.Shots;
            w.StartWave();defenses.Tick(5);
            bool remainDestroyed=defenses.Towers.Count==12;
            foreach(var tower in defenses.Towers)remainDestroyed&=!tower.Alive;
            check(remainDestroyed&&defenses.DestroyedCount==destroyed&&defenses.Shots==shots,
                "Destroyed defenses remain destroyed across wave transitions and cannot resume firing");
            check(w.EnemiesAlive==enemies&&w.TotalSpawned==total&&w.Kills==kills&&w.RemainingToSpawn==WavePlan.Counts[1],
                "Starting the next wave creates only its normal pending assault queue and never respawns hostile buildings");
        }

        static Health MakeProbe(GameWorld w,Role role,Faction faction,Vector3 position)
        {
            Transform root=Art.Root("Defense projectile check "+role,w.transform,position);
            var health=root.gameObject.AddComponent<Health>();health.Setup(role,faction,500,1.2f,.5f);
            var collider=root.gameObject.AddComponent<CapsuleCollider>();collider.center=Vector3.up;collider.height=2;collider.radius=.5f;collider.isTrigger=true;
            root.gameObject.layer=Combat.UnitLayer;Physics.SyncTransforms();return health;
        }

        static void RemoveProbe(GameWorld w,Health probe)
        {
            w.Entities.Remove(probe);probe.gameObject.SetActive(false);UnityEngine.Object.Destroy(probe.gameObject);Physics.SyncTransforms();
        }

        static GameObject MakeCover(Vector3 position)
        {
            var cover=GameObject.CreatePrimitive(PrimitiveType.Cube);cover.name="Projectile occlusion check";
            cover.transform.position=position+Vector3.up*2;cover.transform.localScale=new Vector3(1,7,6);
            cover.layer=Combat.WorldLayer;Physics.SyncTransforms();return cover;
        }

        static void ParkPlayer(GameWorld w){FortificationChecks.PlacePlayer(w,new Vector3(0,0,-17));Physics.SyncTransforms();}
        static void Step(GameWorld w,float seconds)
        {
            int count=Mathf.CeilToInt(seconds/StepSize);
            for(int i=0;i<count;i++)
            {
                float dt=Mathf.Min(StepSize,seconds-i*StepSize);if(dt<=0)break;
                w.SimTime+=dt;Physics.SyncTransforms();w.EnemyDefenses.Tick(dt);w.Effects.Tick(dt);
            }
        }
    }
}

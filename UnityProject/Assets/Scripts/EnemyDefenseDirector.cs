using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Rift defenses persist across waves and are separate from the assault spawn queue.
    public sealed class EnemyDefenseDirector
    {
        public const float TowerHealth=650,Range=22,DamagePerShot=22,FireInterval=1.25f,ProjectileSpeed=24;
        public const int Reward=220;
        readonly GameWorld world;
        readonly List<Health> towers=new List<Health>();
        readonly HashSet<Health> destroyed=new HashSet<Health>();
        readonly float[] cooldowns=new float[6];
        readonly Health[] targets=new Health[6];
        public IReadOnlyList<Health> Towers => towers;
        public int DestroyedCount => destroyed.Count;
        public int Shots { get; private set; }

        public EnemyDefenseDirector(GameWorld owner)
        {
            world=owner;
            for(int i=0;i<GameWorld.EnemySpawnPoints.Length;i++)
            {
                // Behind the rift leaves its forward departure and rally route clear.
                Vector3 position=GameWorld.EnemySpawnPoints[i]+Vector3.forward*5;
                position.y=GameWorld.GroundHeight(position.x,position.z);
                Transform root=Art.Root("Rift defense "+i,world.transform,position);
                Health tower=root.gameObject.AddComponent<Health>();
                tower.Setup(Role.EnemyTower,Faction.Invaders,TowerHealth,2.75f,1.6f);tower.Lane=i%2;
                tower.Rig=EnemyDefenseArt.Build(root);
                var collider=root.gameObject.AddComponent<BoxCollider>();
                collider.center=new Vector3(0,1.5f,0);collider.size=new Vector3(3.2f,3,2.6f);
                root.gameObject.layer=Combat.WorldLayer;
                towers.Add(tower);cooldowns[i]=.35f+i*.08f;
            }
        }

        public Health TargetOf(Health tower)
        {
            int index=towers.IndexOf(tower);return index>=0?targets[index]:null;
        }

        public void Tick(float dt)
        {
            if(dt<=0||!world.Active)return;
            for(int i=0;i<towers.Count;i++)
            {
                Health tower=towers[i];
                if(tower==null||!tower.Alive){targets[i]=null;continue;}
                cooldowns[i]=Mathf.Max(0,cooldowns[i]-dt);
                Health target=Acquire(tower);targets[i]=target;
                tower.Rig.Animate(0,dt);
                if(target==null)continue;
                tower.Rig.AimAt(target.Aim);
                if(cooldowns[i]>0)continue;
                Vector3 muzzle=tower.Rig.Muzzle.position;
                Vector3 direction=(target.Aim-muzzle).normalized;
                // Check the muzzle too: an offset barrel never bypasses intervening cover.
                if(Combat.Trace(tower.Aim,(muzzle-tower.Aim).normalized,Vector3.Distance(muzzle,tower.Aim),tower,out _))continue;
                if(Combat.Trace(muzzle,direction,Vector3.Distance(muzzle,target.Aim)+.1f,tower,out var hit)&&
                    hit.collider.GetComponentInParent<Health>()!=target)continue;
                world.Effects.Bolt(muzzle,target.Aim,tower,DamagePerShot,ProjectileSpeed);
                tower.Rig.Recoil();world.Sounds.DistantShot(muzzle);
                cooldowns[i]=FireInterval;Shots++;
            }
        }

        Health Acquire(Health tower)
        {
            Health best=null;float distanceBest=Range;
            foreach(var candidate in world.Entities)
            {
                if(candidate==null||!candidate.Alive||candidate.IsSheltered||candidate.Faction==tower.Faction||candidate.IsBuilding)continue;
                if(candidate.Role!=Role.Player&&candidate.Role!=Role.Marine&&candidate.Role!=Role.Squad&&candidate.Role!=Role.Tank)continue;
                float distance=Vector3.Distance(tower.Aim,candidate.Aim);
                if(distance>distanceBest||!Combat.Visible(tower,candidate))continue;
                distanceBest=distance;best=candidate;
            }
            return best;
        }

        // The world gives the advertised reward only when this first-death registration succeeds.
        public bool OnKilled(Health tower)
        {
            if(tower==null||tower.Alive||tower.Role!=Role.EnemyTower||!towers.Contains(tower)||!destroyed.Add(tower))return false;
            targets[towers.IndexOf(tower)]=null;
            tower.Rig?.Animate(0,1);
            return true;
        }
    }

    public static class EnemyDefenseArt
    {
        public static ArtRig Build(Transform root)
        {
            Art.Part(root,"Defense pedestal",new Vector3(0,.22f,0),new Vector3(3.8f,.44f,3.2f),9);
            Art.Part(root,"Armored tower core",new Vector3(0,1.2f,0),new Vector3(2.6f,1.9f,2.2f),8);
            Art.Part(root,"Tower crown",new Vector3(0,2.3f,0),new Vector3(3.2f,.35f,2.7f),9);
            for(int side=-1;side<=1;side+=2)
            {
                Art.Part(root,"Energy conduit",new Vector3(side*1.32f,1.35f,0),new Vector3(.08f,1.6f,.5f),10);
                Art.Spike(root,new Vector3(side*1.6f,.3f,0),new Vector3(.6f,2.6f,.75f),9,new Vector3(0,0,-side*14));
            }
            ArtRig rig=root.gameObject.AddComponent<ArtRig>();rig.Turret=true;
            rig.Body=Art.Root("Tracking weapon",root,new Vector3(0,2.65f,0));
            Art.Part(rig.Body,"Weapon housing",Vector3.zero,new Vector3(1.9f,.6f,1.4f),8);
            rig.Gun=Art.Part(rig.Body,"Acid lance",new Vector3(0,.1f,.9f),new Vector3(.65f,.5f,1.9f),9);
            Art.Part(rig.Gun,"Lance emitter",new Vector3(0,0,.5f),new Vector3(.72f,.65f,.07f),10);
            rig.Muzzle=Art.Root("Tower muzzle",rig.Body,new Vector3(0,.1f,1.9f));
            rig.Finish();return rig;
        }
    }
}

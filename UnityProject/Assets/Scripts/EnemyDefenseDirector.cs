using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public sealed class EnemyBaseRecord
    {
        public int Id;
        public Vector3 Position;
        public Health Portal,Nest;
        public readonly List<Health> Towers=new List<Health>(2);
        public float GuardTimer=28;
    }

    // Hostile bases persist independently of the finite assault-wave queue.
    public sealed class EnemyDefenseDirector
    {
        public const float TowerHealth=900,Range=30,DamagePerShot=28,FireInterval=1.5f,ProjectileSpeed=24;
        public const int Reward=150,PortalReward=300,NestReward=90;
        readonly GameWorld world;
        readonly List<EnemyBaseRecord> bases=new List<EnemyBaseRecord>(6);
        readonly List<Health> towers=new List<Health>(12);
        readonly HashSet<Health> destroyed=new HashSet<Health>();
        readonly List<float> cooldowns=new List<float>(12);
        readonly List<Health> targets=new List<Health>(12);
        int destroyedTowers;
        public IReadOnlyList<EnemyBaseRecord> Bases=>bases;
        public IReadOnlyList<Health> Towers=>towers;
        public int DestroyedCount=>destroyedTowers;
        public int DestroyedPortals { get; private set; }
        public int Shots { get; private set; }

        public static float TowerHealthAt(int baseId)=>baseId<2?900:baseId<4?1300:1700;
        public static float TowerDamageAt(int baseId)=>baseId<2?28:baseId<4?34:40;
        public static float PortalHealthAt(int baseId)=>baseId<2?1800:baseId<4?2600:3600;
        public static int RewardFor(Role role)=>role==Role.EnemyPortal?PortalReward:role==Role.EnemyNest?NestReward:Reward;

        public EnemyDefenseDirector(GameWorld owner)
        {
            world=owner;
            for(int i=0;i<GameWorld.EnemySpawnPoints.Length;i++)
            {
                Vector3 point=GameWorld.EnemySpawnPoints[i];point.y=GroundSurface.Height(point.x,point.z);
                var site=new EnemyBaseRecord {Id=i,Position=point,GuardTimer=28+i*.35f};
                site.Portal=Building("Portal "+i,Role.EnemyPortal,point,PortalHealthAt(i),3.3f,3.1f,new Vector3(6.2f,6.7f,2.8f),i);
                site.Portal.Rig=EnemyDefenseArt.Portal(site.Portal.transform);
                Vector3 nest=point+new Vector3(point.x<0?-8:8,0,3);
                site.Nest=Building("Garrison nest "+i,Role.EnemyNest,nest,500+i/2*150,1.5f,2.4f,new Vector3(5.1f,3,4.4f),i);
                site.Nest.Rig=EnemyDefenseArt.Nest(site.Nest.transform);
                for(int side=-1;side<=1;side+=2)
                {
                    Vector3 position=point+new Vector3(side*7,0,-8);
                    Health tower=Building("Base "+i+" defense "+side,Role.EnemyTower,position,TowerHealthAt(i),2.75f,1.6f,new Vector3(3.2f,3.3f,2.6f),i);
                    tower.Rig=EnemyDefenseArt.Build(tower.transform);site.Towers.Add(tower);towers.Add(tower);
                    cooldowns.Add(.35f+i*.08f);targets.Add(null);
                }
                bases.Add(site);
            }
            Physics.SyncTransforms();
            // Initial sentries patrol locally, never inflating scheduled wave counts.
            foreach(var site in bases)for(int slot=0;slot<2;slot++)SpawnGuard(site,slot);
        }

        Health Building(string name,Role role,Vector3 position,float hp,float height,float radius,Vector3 size,int baseId)
        {
            Transform root=Art.Root(name,world.transform,position);
            Health health=root.gameObject.AddComponent<Health>();health.Setup(role,Faction.Invaders,hp,height,radius);
            health.Lane=baseId%2;health.BaseId=baseId;
            var collider=root.gameObject.AddComponent<BoxCollider>();collider.center=Vector3.up*(size.y*.5f);collider.size=size;
            root.gameObject.layer=Combat.WorldLayer;return health;
        }

        public bool IsPortalAlive(int id)=>id>=0&&id<bases.Count&&bases[id].Portal!=null&&bases[id].Portal.Alive;
        public Health NearestAlivePortal(Vector3 position)
        {
            Health nearest=null;float best=float.PositiveInfinity;
            foreach(var site in bases)
            {
                if(site.Portal==null||!site.Portal.Alive)continue;
                float distance=(site.Position-position).sqrMagnitude;
                if(distance<best){best=distance;nearest=site.Portal;}
            }
            return nearest;
        }

        public Health TargetOf(Health tower){int index=towers.IndexOf(tower);return index>=0?targets[index]:null;}

        void SpawnGuard(EnemyBaseRecord site,int slot)
        {
            if(world.EnemyMobileCount>=100||!IsPortalAlive(site.Id)||site.Nest==null||!site.Nest.Alive)return;
            Vector3 point=site.Position+new Vector3((slot%2==0?-1:1)*2.8f,0,-4.5f-slot/2*2);
            point.y=GroundSurface.Height(point.x,point.z);
            Role role=slot%3==0?Role.Spitter:slot%3==1?Role.Stalker:Role.Runner;
            Health guard=world.SpawnEnemy(role,site.Id%2,point,-1,true);
            if(guard==null)return;
            guard.BaseId=site.Id;guard.BaseGuard=true;
            guard.Brain.Station=site.Position+new Vector3(slot%2==0?-9:9,0,-14-slot/2*2);
        }

        public void Tick(float dt)
        {
            if(dt<=0||!world.Active)return;
            foreach(var site in bases)
            {
                if(!IsPortalAlive(site.Id)||site.Nest==null||!site.Nest.Alive)continue;
                int count=0;foreach(var entity in world.Entities)
                    if(entity!=null&&entity.Alive&&entity.BaseGuard&&entity.BaseId==site.Id)count++;
                if(count>=4)continue;
                site.GuardTimer=Mathf.Max(0,site.GuardTimer-dt);
                if(site.GuardTimer<=0&&world.EnemyMobileCount<100){SpawnGuard(site,count);site.GuardTimer=28;}
            }
            for(int i=0;i<towers.Count;i++)
            {
                Health tower=towers[i];
                if(tower==null||!tower.Alive){targets[i]=null;continue;}
                cooldowns[i]=Mathf.Max(0,cooldowns[i]-dt);
                Health target=Acquire(tower);targets[i]=target;tower.Rig.Animate(0,dt);
                if(target==null)continue;
                tower.Rig.AimAt(target.Aim);if(cooldowns[i]>0)continue;
                Vector3 muzzle=tower.Rig.Muzzle.position,direction=(target.Aim-muzzle).normalized;
                if(Combat.Trace(tower.Aim,(muzzle-tower.Aim).normalized,Vector3.Distance(muzzle,tower.Aim),tower,out _))continue;
                if(Combat.Trace(muzzle,direction,Vector3.Distance(muzzle,target.Aim)+.1f,tower,out var hit)&&hit.collider.GetComponentInParent<Health>()!=target)continue;
                world.Effects.Bolt(muzzle,target.Aim,tower,TowerDamageAt(tower.BaseId),ProjectileSpeed);
                tower.Rig.Recoil();world.Sounds.DistantShot(muzzle);cooldowns[i]=FireInterval;Shots++;
            }
        }

        Health Acquire(Health tower)
        {
            Health best=null;float distanceBest=Range;
            foreach(var candidate in world.Entities)
            {
                if(candidate==null||!candidate.Alive||candidate.IsSheltered||candidate.Faction==tower.Faction||candidate.IsBuilding)continue;
                float distance=Vector3.Distance(tower.Aim,candidate.Aim);
                if(distance>distanceBest||!Combat.Visible(tower,candidate))continue;
                distanceBest=distance;best=candidate;
            }
            return best;
        }

        public bool OnKilled(Health victim)
        {
            if(victim==null||victim.Alive||victim.BaseId<0||victim.BaseId>=bases.Count||destroyed.Contains(victim))return false;
            EnemyBaseRecord site=bases[victim.BaseId];
            if(victim!=site.Portal&&victim!=site.Nest&&!site.Towers.Contains(victim))return false;
            destroyed.Add(victim);
            if(victim.Role==Role.EnemyTower){destroyedTowers++;targets[towers.IndexOf(victim)]=null;}
            if(victim.Role==Role.EnemyPortal)DestroyedPortals++;
            foreach(var collider in victim.GetComponentsInChildren<Collider>())collider.enabled=false;
            if(victim.Role==Role.EnemyTower)
                foreach(var renderer in victim.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=Art.Mat(1);
            else victim.gameObject.SetActive(false);
            Physics.SyncTransforms();world.Squad?.RebuildNavigation();return true;
        }
    }

    public static class EnemyDefenseArt
    {
        public static ArtRig Portal(Transform root)
        {
            var rig=root.gameObject.AddComponent<ArtRig>();rig.Body=Art.Root("Live portal aperture",root,Vector3.zero);
            Art.Part(rig.Body,"Portal foundation",new Vector3(0,.2f,0),new Vector3(7.5f,.2f,6.5f),9,PrimitiveType.Cylinder);
            for(int i=0;i<12;i++)
            {
                float angle=i*Mathf.PI/6;Vector3 center=new Vector3(Mathf.Sin(angle)*2.7f,3.35f+Mathf.Cos(angle)*2.7f,0);
                Art.Part(rig.Body,"Portal carapace segment",center,new Vector3(.85f,1.65f,1.3f),8,euler:new Vector3(0,0,-angle*Mathf.Rad2Deg));
                Art.Part(rig.Body,"Live energy conduit",new Vector3(Mathf.Sin(angle)*2.34f,3.35f+Mathf.Cos(angle)*2.34f,-.02f),new Vector3(.13f,.85f,1.35f),10,euler:new Vector3(0,0,-angle*Mathf.Rad2Deg));
            }
            Art.Part(rig.Body,"Portal membrane",new Vector3(0,3.35f,0),new Vector3(4.55f,4.8f,.52f),9,PrimitiveType.Sphere);
            Art.Part(rig.Body,"Portal plasma",new Vector3(0,3.35f,0),new Vector3(3.35f,4.2f,.6f),10,PrimitiveType.Sphere);
            Art.Part(rig.Body,"Portal heart",new Vector3(0,3.35f,0),new Vector3(1.45f,2.55f,.7f),11,PrimitiveType.Sphere);
            rig.Finish();return rig;
        }

        public static ArtRig Nest(Transform root)
        {
            var rig=root.gameObject.AddComponent<ArtRig>();rig.Body=Art.Root("Garrison shell",root,Vector3.zero);
            Art.Part(rig.Body,"Nest foundation",new Vector3(0,.12f,0),new Vector3(5.8f,.12f,5.2f),9,PrimitiveType.Cylinder);
            Art.Part(rig.Body,"Nest carapace",new Vector3(0,1.3f,-.2f),new Vector3(5.1f,2.6f,4.4f),8,PrimitiveType.Sphere);
            Art.Part(rig.Body,"Garrison mouth",new Vector3(0,.9f,-2.25f),new Vector3(2.1f,1.55f,.2f),9);
            Art.Part(rig.Body,"Garrison glow",new Vector3(0,1.7f,-2.36f),new Vector3(1.8f,.14f,.05f),10);
            for(int side=-1;side<=1;side+=2)
            {
                Art.Spike(rig.Body,new Vector3(side*2,.8f,0),new Vector3(.55f,2.4f,.75f),11,new Vector3(0,0,-side*18));
                Art.Part(rig.Body,"Hatching sac",new Vector3(side*1.7f,.5f,-1.3f),new Vector3(1.15f,1,1.15f),9,PrimitiveType.Sphere);
            }
            rig.Finish();return rig;
        }

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
            rig.Muzzle=Art.Root("Tower muzzle",rig.Body,new Vector3(0,.1f,1.9f));rig.Finish();return rig;
        }
    }
}

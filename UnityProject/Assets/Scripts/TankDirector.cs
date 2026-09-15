using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public sealed class TankDirector
    {
        public const int FactorySiteId=4;
        public const float ProductionDuration=24,MaximumHealth=1000,CannonDamage=105,FireInterval=2.8f,Range=48,SplashRadius=2.8f;
        public const float Speed=3.2f,NavigationHalfWidth=2.4f;
        readonly GameWorld world;
        readonly List<Shell> shells=new List<Shell>();
        readonly TankNavigation navigation=new TankNavigation();
        readonly List<Vector3> path=new List<Vector3>();
        SquadNavigation previousNavigation;
        TankVisual visual;
        float fireLeft,repathLeft;
        int waypoint;
        bool exitBlocked;
        public Health Tank { get; private set; }
        public Health Target { get; private set; }
        public float ProductionLeft { get; private set; }=ProductionDuration;
        public int Produced { get; private set; }
        public int Lost { get; private set; }
        public int Kills { get; private set; }
        public int Shots { get; private set; }
        public float Travelled { get; private set; }
        public Vector3 DeploymentPoint => new Vector3(0,0,21);
        public Vector3 SpawnPoint => FactorySite.Position+Vector3.forward*(ConstructionDirector.Size(Role.Factory).z*.5f+2.65f);
        public Vector3 MuzzlePosition => visual!=null?visual.Muzzle.position:Vector3.zero;
        public bool Deployed => Tank!=null&&Tank.Alive&&FlatDistance(Tank.transform.position,DeploymentPoint)<=.3f;
        BuildSite FactorySite => world.Construction.Sites[FactorySiteId];
        public bool Operational => FactorySite.State==SiteState.Ready&&FactorySite.Structure!=null&&FactorySite.Structure.Alive;
        public string Status => Tank!=null&&Tank.Alive?(Deployed?"中央防线 · 自动开火":"部署途中 · 前往中央阵位"):
            !Operational?(FactorySite.State==SiteState.Constructing?"工厂施工中":"工厂未投产"):
            exitBlocked?"出口受阻 · 等待通道清空":"自动生产 · "+Mathf.CeilToInt(ProductionLeft)+" 秒";

        sealed class Shell
        {
            public Transform Body;
            public Health Owner;
            public Vector3 Direction;
            public float Life=2.4f;
        }

        public TankDirector(GameWorld owner){world=owner;}

        public void Tick(float dt)
        {
            if(!world.Active||dt<=0)return;
            TickShells(dt);
            if(Tank==null||!Tank.Alive)
            {
                if(!Operational)return;
                ProductionLeft=Mathf.Max(0,ProductionLeft-dt);
                if(ProductionLeft>0)return;
                exitBlocked=!ExitClear();
                if(!exitBlocked)Produce();
                return;
            }
            fireLeft=Mathf.Max(0,fireLeft-dt);
            Move(dt);
            Target=FindTarget();
            if(Target==null)return;
            visual.AimAt(Target.Aim,dt);
            if(fireLeft<=0&&CanShoot(Target))Fire(Target);
        }

        bool ExitClear()
        {
            Physics.SyncTransforms();
            if(!navigation.Fits(SpawnPoint))return false;
            foreach(var h in world.Entities)
            {
                if(h==null||!h.Alive||h.IsSheltered||h.IsBuilding)continue;
                if(FlatDistance(h.transform.position,SpawnPoint)<NavigationHalfWidth+h.Radius+.15f)return false;
            }
            return true;
        }

        void Produce()
        {
            // The tank enters the simulation at the physical garage exit, then drives its entire route.
            Transform root=Art.Root("Sentinel / defensive tank",world.transform,SpawnPoint);
            root.gameObject.layer=Combat.UnitLayer;
            Tank=root.gameObject.AddComponent<Health>();Tank.Setup(Role.Tank,Faction.Defense,MaximumHealth,2.0f,1.9f);
            var hull=root.gameObject.AddComponent<BoxCollider>();hull.center=new Vector3(0,.98f,0);hull.size=new Vector3(3.1f,1.8f,3.65f);
            var turret=root.gameObject.AddComponent<BoxCollider>();turret.center=new Vector3(0,2.12f,0);turret.size=new Vector3(2.1f,.85f,2.05f);
            visual=TankArt.Tank(root);Produced++;ProductionLeft=ProductionDuration;Travelled=0;
            fireLeft=.6f;repathLeft=0;path.Clear();waypoint=0;previousNavigation=null;exitBlocked=false;
            world.Effects.Burst(SpawnPoint+Vector3.up,Art.Cyan,1.2f);
            world.Notice("坦克已出厂 · 正前往中央防线");
        }

        void Move(float dt)
        {
            Vector3 position=Tank.transform.position;
            if(Deployed)return;
            repathLeft=Mathf.Max(0,repathLeft-dt);
            if(previousNavigation!=world.Squad.Navigation)
            {
                previousNavigation=world.Squad.Navigation;navigation.Rebuild();path.Clear();waypoint=0;repathLeft=0;
            }
            if(waypoint>=path.Count&&repathLeft<=0)
            {
                navigation.Plan(position,DeploymentPoint,path);waypoint=0;repathLeft=1;
            }
            while(waypoint<path.Count&&FlatDistance(position,path[waypoint])<.12f)waypoint++;
            if(waypoint>=path.Count)return;
            while(waypoint+1<path.Count&&navigation.Clear(position,path[waypoint+1]))waypoint++;
            Vector3 difference=path[waypoint]-position;difference.y=0;
            if(difference.sqrMagnitude<.00001f)return;
            Vector3 next=position+difference.normalized*Mathf.Min(difference.magnitude,Speed*dt);next.y=0;
            if(!navigation.Clear(position,next))
            {
                path.Clear();waypoint=0;repathLeft=.4f;return;
            }
            Tank.transform.position=next;Travelled+=Vector3.Distance(position,next);
            Tank.transform.rotation=Quaternion.RotateTowards(Tank.transform.rotation,Quaternion.LookRotation(difference),80*dt);
            Physics.SyncTransforms();
        }

        Health FindTarget()
        {
            Health chosen=null;float best=float.MaxValue;
            foreach(var h in world.Entities)
            {
                if(h==null||!h.Alive||h.IsSheltered||h.Faction==Tank.Faction)continue;
                float distance=Vector3.Distance(Tank.Aim,h.Aim);
                if(distance>Range+h.Radius||!CanShoot(h))continue;
                float score=distance+(h.Role==Role.Colossus?0:h.IsBuilding?70:130);
                if(score<best){best=score;chosen=h;}
            }
            return chosen;
        }

        bool CanShoot(Health target)
        {
            // Checking the breech as well as the muzzle prevents the barrel from firing through a wall.
            Vector3 breech=Tank.transform.position+Vector3.up*2.6f;
            Vector3 barrel=MuzzlePosition-breech;
            if(barrel.sqrMagnitude>.001f&&Combat.Trace(breech,barrel.normalized,barrel.magnitude,Tank,out _,true))return false;
            return VisibleFrom(breech,target)&&VisibleFrom(MuzzlePosition,target);
        }
        bool VisibleFrom(Vector3 start,Health target)
        {
            Vector3 delta=target.Aim-start;
            return delta.sqrMagnitude>.001f&&(!Combat.Trace(start,delta.normalized,delta.magnitude+.1f,Tank,out var hit,true)||
                hit.collider.GetComponentInParent<Health>()==target);
        }
        void Fire(Health target)
        {
            Vector3 start=MuzzlePosition,direction=(target.Aim-start).normalized;
            Transform body=Art.Part(world.transform,"Tank shell",start,new Vector3(.18f,.18f,.62f),7,PrimitiveType.Sphere);
            body.rotation=Quaternion.LookRotation(direction);
            shells.Add(new Shell{Body=body,Owner=Tank,Direction=direction});
            Shots++;fireLeft=FireInterval;
            world.Effects.Spark(start,Art.Amber,.6f);world.Sounds.DistantShot(start);
        }
        void TickShells(float dt)
        {
            for(int i=shells.Count-1;i>=0;i--)
            {
                var shell=shells[i];Vector3 movement=shell.Direction*(38*dt);shell.Life-=dt;
                if(Combat.Trace(shell.Body.position,shell.Direction,movement.magnitude+.12f,shell.Owner,out var hit,true))
                {
                    Explode(hit.point+hit.normal*.035f,hit.collider.GetComponentInParent<Health>(),shell.Owner);shell.Life=0;
                }
                if(shell.Life<=0){Object.Destroy(shell.Body.gameObject);shells.RemoveAt(i);}
                else shell.Body.position+=movement;
            }
        }
        void Explode(Vector3 point,Health direct,Health owner)
        {
            // Each victim is considered once. World geometry also blocks the splash, and allies are excluded.
            for(int i=world.Entities.Count-1;i>=0;i--)
            {
                Health h=world.Entities[i];
                if(h==null||!h.Alive||h.IsSheltered||h.Faction!=Faction.Invaders)continue;
                float distance=Vector3.Distance(point,h.Aim);
                if(h!=direct&&distance>SplashRadius)continue;
                if(h!=direct)
                {
                    Vector3 delta=h.Aim-point;
                    if(Combat.Trace(point,delta.normalized,delta.magnitude+.05f,direct,out var hit,true)&&
                        hit.collider.GetComponentInParent<Health>()!=h)continue;
                }
                float damage=h==direct?CannonDamage:CannonDamage*.65f*(1-distance/(SplashRadius*1.4f));
                h.Damage(damage,owner);
                if(!h.Alive)Kills++;
            }
            world.Effects.Burst(point,Art.Amber,1.65f);
        }

        public void OnKilled(Health victim)
        {
            if(victim!=Tank)return;
            Lost++;Tank=null;Target=null;visual=null;ProductionLeft=ProductionDuration;
            path.Clear();waypoint=0;exitBlocked=false;
            world.Notice(Operational?"坦克被摧毁 · 工厂正在补产":"坦克被摧毁 · 重建工厂后恢复生产");
        }
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}

        // Infantry capsules do not describe a tracked vehicle. A conservative square contains the entire
        // rotating hull, with its bottom lifted above the deck so the floor remains traversable.
        sealed class TankNavigation
        {
            const int MinX=-24,MinZ=-18,Width=49,Height=43,Count=Width*Height;
            readonly bool[] walkable=new bool[Count],closed=new bool[Count];
            readonly float[] costs=new float[Count];
            readonly int[] previous=new int[Count];
            readonly List<int> open=new List<int>(),reverse=new List<int>();
            static readonly Vector3 Extents=new Vector3(NavigationHalfWidth,1.35f,NavigationHalfWidth);
            static Vector3 Point(int id)=>new Vector3(MinX+id%Width,0,MinZ+id/Width);
            public void Rebuild()
            {
                Physics.SyncTransforms();
                for(int i=0;i<Count;i++)walkable[i]=Fits(Point(i));
            }
            public bool Fits(Vector3 point)
            {
                if(Mathf.Abs(point.x)>26.5f-NavigationHalfWidth||point.z<-21+NavigationHalfWidth||point.z>24.4f)return false;
                return !Physics.CheckBox(point+Vector3.up*1.45f,Extents,Quaternion.identity,Combat.WorldMask,QueryTriggerInteraction.Ignore);
            }
            public bool Clear(Vector3 start,Vector3 end)
            {
                if(!Fits(start)||!Fits(end))return false;
                Vector3 delta=end-start;delta.y=0;
                return delta.sqrMagnitude<.00001f||!Physics.BoxCast(start+Vector3.up*1.45f,Extents,delta.normalized,
                    Quaternion.identity,delta.magnitude,Combat.WorldMask,QueryTriggerInteraction.Ignore);
            }
            int Nearest(Vector3 point)
            {
                int found=-1;float best=float.MaxValue;
                for(int i=0;i<Count;i++)if(walkable[i])
                {
                    float distance=(Point(i)-point).sqrMagnitude;
                    if(distance<best&&Clear(point,Point(i))){best=distance;found=i;}
                }
                return found;
            }
            public bool Plan(Vector3 start,Vector3 goal,List<Vector3> route)
            {
                route.Clear();if(Clear(start,goal)){route.Add(goal);return true;}
                int first=Nearest(start),last=Nearest(goal);if(first<0||last<0)return false;
                open.Clear();reverse.Clear();
                for(int i=0;i<Count;i++){costs[i]=float.MaxValue;closed[i]=false;previous[i]=-1;}
                costs[first]=0;open.Add(first);
                while(open.Count>0)
                {
                    int at=0;float best=float.MaxValue;
                    for(int i=0;i<open.Count;i++)
                    {
                        int id=open[i];float score=costs[id]+Vector3.Distance(Point(id),Point(last));
                        if(score<best){best=score;at=i;}
                    }
                    int current=open[at];open.RemoveAt(at);
                    if(current==last)
                    {
                        for(int id=last;id>=0;id=previous[id])reverse.Add(id);
                        for(int i=reverse.Count-1;i>=0;i--)route.Add(Point(reverse[i]));
                        if((route[route.Count-1]-goal).sqrMagnitude>.001f)route.Add(goal);
                        return true;
                    }
                    if(closed[current])continue;closed[current]=true;
                    int x=current%Width,z=current/Width;
                    for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                    {
                        if(dx==0&&dz==0)continue;int nx=x+dx,nz=z+dz;
                        if(nx<0||nx>=Width||nz<0||nz>=Height)continue;
                        int next=nz*Width+nx;
                        if(!walkable[next]||closed[next]||!Clear(Point(current),Point(next)))continue;
                        float cost=costs[current]+(dx!=0&&dz!=0?1.414214f:1);
                        if(cost>=costs[next])continue;
                        previous[next]=current;costs[next]=cost;open.Add(next);
                    }
                }
                return false;
            }
        }
    }
}

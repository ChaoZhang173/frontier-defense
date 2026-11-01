using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public enum ExpeditionState { Rally, Attacking, Reserve }

    // Expedition soldiers have their own production and movement controller. They
    // never enter the guard garrison, mercenary contracts, or tank production slot.
    public sealed class ExpeditionDirector
    {
        public const int FormationSize=6,MaxUnits=24;
        public const float MaximumHealth=195,ProductionDuration=12,ReserveProductionDuration=9;
        public const float Range=25,FireInterval=.66f,ArrivalDistance=.85f,Speed=4.3f;
        public static readonly Vector3 RallyCenter=new Vector3(0,0,12);
        readonly GameWorld world;
        readonly List<Health> members=new List<Health>();
        readonly List<Soldier> soldiers=new List<Soldier>();
        readonly List<Soldier> arrived=new List<Soldier>(FormationSize);
        int sourceIndex;

        sealed class Soldier
        {
            public Health Unit,Portal,Target;
            public readonly GuardNavigation Navigation=new GuardNavigation();
            public ExpeditionState State;
            public Vector3 RallyPoint;
            public int RallySlot,RouteStage,BaseId=-1,Wave;
            public float Lane,FireLeft,ThinkLeft,Travelled;
        }

        public ExpeditionDirector(GameWorld owner)
        {
            world=owner;
            ExpeditionArt.RallyArea(world.transform);
        }

        public IReadOnlyList<Health> Members=>members;
        public float ProductionLeft {get;private set;}=ProductionDuration;
        public int Produced {get;private set;}
        public int Departures {get;private set;}
        public int Shots {get;private set;}
        public int AliveCount {get {int n=0;foreach(var s in soldiers)if(Living(s))n++;return n;}}
        public int RallyCount=>Count(ExpeditionState.Rally);
        public int AttackingCount=>Count(ExpeditionState.Attacking);
        public int ReserveCount=>Count(ExpeditionState.Reserve);
        public bool Operational {get {foreach(var h in world.BarracksBuildings)if(h!=null&&h.Alive&&h.Role==Role.Barracks)return true;return false;}}
        public string Status=>world.EnemyDefenses.NearestAlivePortal(RallyCenter)==null?"传送门已清除 · 部队回防":
            !Operational?"等待兵营恢复生产":"集结 "+RallyCount+"/"+FormationSize+" · 出征 "+AttackingCount+" · "+Mathf.CeilToInt(ProductionLeft)+" 秒补员";

        int Count(ExpeditionState state){int n=0;foreach(var s in soldiers)if(Living(s)&&s.State==state)n++;return n;}
        static bool Living(Soldier s)=>s.Unit!=null&&s.Unit.Alive;
        Soldier Find(Health unit)=>soldiers.Find(s=>s.Unit==unit);
        public ExpeditionState StateOf(Health unit){var s=Find(unit);return s==null?ExpeditionState.Reserve:s.State;}
        public Vector3 RallyPointOf(Health unit){var s=Find(unit);return s==null?RallyCenter:s.RallyPoint;}
        public int BaseIdOf(Health unit){var s=Find(unit);return s==null?-1:s.BaseId;}
        public float TravelledOf(Health unit){var s=Find(unit);return s==null?0:s.Travelled;}
        public Health TargetOf(Health unit){var s=Find(unit);return s==null?null:s.Target;}

        public void Tick(float dt)
        {
            if(!world.Active||dt<=0)return;
            for(int i=soldiers.Count-1;i>=0;i--)if(!Living(soldiers[i]))
            {
                members.Remove(soldiers[i].Unit);soldiers.RemoveAt(i);
            }
            Health portal=world.EnemyDefenses.NearestAlivePortal(RallyCenter);
            if(portal==null)ReturnToBase();
            else
            {
                if(Operational&&AliveCount<MaxUnits)
                {
                    ProductionLeft=Mathf.Max(0,ProductionLeft-dt);
                    if(ProductionLeft<=0&&Produce())ProductionLeft=world.Construction.ReserveOperational?ReserveProductionDuration:ProductionDuration;
                }
                LaunchAssembled(portal);
            }
            foreach(var soldier in soldiers)if(Living(soldier))TickSoldier(soldier,dt);
        }

        bool Produce()
        {
            var producers=world.BarracksBuildings;
            for(int attempt=0;attempt<producers.Count;attempt++)
            {
                Health producer=producers[(sourceIndex+attempt)%producers.Count];
                if(producer==null||!producer.Alive||producer.Role!=Role.Barracks)continue;
                for(int offset=0;offset<3;offset++)
                {
                    float side=offset==0?0:offset==1?-1.7f:1.7f;
                    Vector3 point=producer.transform.TransformPoint(new Vector3(side,0,ConstructionDirector.Size(Role.Barracks).z*.5f+1.1f));
                    point.y=GameWorld.GroundHeight(point.x,point.z);
                    if(!world.Squad.Navigation.Fits(point,.42f)||Occupied(point))continue;
                    int slot=0;
                    while(soldiers.Exists(s=>Living(s)&&s.State==ExpeditionState.Rally&&s.RallySlot==slot))slot++;
                    Transform root=Art.Root("Expedition / assault marine "+Produced,world.transform,point);
                    root.gameObject.layer=Combat.UnitLayer;
                    var unit=root.gameObject.AddComponent<Health>();
                    unit.Setup(Role.AssaultMarine,Faction.Defense,MaximumHealth,1.2f,.45f);
                    unit.Slot=slot;unit.Rig=ExpeditionArt.Marine(root);
                    var body=root.gameObject.AddComponent<CapsuleCollider>();body.center=new Vector3(0,1,0);body.height=2;body.radius=.42f;body.isTrigger=true;
                    soldiers.Add(new Soldier{Unit=unit,State=ExpeditionState.Rally,RallySlot=slot,RallyPoint=FormationPoint(slot),FireLeft=.25f});
                    members.Add(unit);Produced++;sourceIndex=(sourceIndex+attempt+1)%producers.Count;
                    world.Effects.Burst(point+Vector3.up*.1f,ExpeditionArt.Green,.6f);
                    Physics.SyncTransforms();return true;
                }
            }
            return false;
        }

        bool Occupied(Vector3 point)
        {
            foreach(var h in world.Entities)
                if(h!=null&&h.Alive&&!h.IsBuilding&&!h.IsSheltered&&FlatDistance(point,h.transform.position)<h.Radius+.55f)return true;
            return false;
        }

        public static Vector3 FormationPoint(int slot)=>RallyCenter+new Vector3((slot%3-1)*2.3f,0,(slot/3)*1.8f-.9f);

        void LaunchAssembled(Health portal)
        {
            arrived.Clear();
            foreach(var s in soldiers)
                if(Living(s)&&s.State==ExpeditionState.Rally&&FlatDistance(s.Unit.transform.position,s.RallyPoint)<=ArrivalDistance)
                    arrived.Add(s);
            if(arrived.Count<FormationSize)return;
            Departures++;
            for(int i=0;i<FormationSize;i++)AssignAttack(arrived[i],portal,Departures);
            world.Notice("突击队集结完成 · 6 人出发进攻传送门基地");
        }

        void AssignAttack(Soldier s,Health portal,int wave)
        {
            s.State=ExpeditionState.Attacking;s.Portal=portal;s.BaseId=portal.BaseId;s.Wave=wave;
            Vector3 position=s.Unit.transform.position;
            s.RouteStage=position.z>44?3:position.z>30?2:position.z>21?1:0;
            s.Lane=portal.transform.position.x<0?-13.5f:portal.transform.position.x>0?13.5f:position.x<0?-13.5f:13.5f;
        }

        void ReturnToBase()
        {
            for(int i=0;i<soldiers.Count;i++)
            {
                var s=soldiers[i];if(s.State==ExpeditionState.Reserve)continue;
                s.State=ExpeditionState.Reserve;s.Portal=null;s.BaseId=-1;
                s.RallyPoint=new Vector3((i%6-2.5f)*2,0,9+(i/6)*2);
            }
        }

        void TickSoldier(Soldier s,float dt)
        {
            Health unit=s.Unit;s.FireLeft=Mathf.Max(0,s.FireLeft-dt);s.ThinkLeft-=dt;
            if(s.State==ExpeditionState.Attacking&&(s.Portal==null||!s.Portal.Alive))
            {
                Health portal=world.EnemyDefenses.NearestAlivePortal(unit.transform.position);
                if(portal!=null)AssignAttack(s,portal,s.Wave);
            }
            if(s.ThinkLeft<=0||!ValidTarget(unit,s.Target))
            {
                s.Target=Acquire(unit);s.ThinkLeft=.2f;
            }
            bool engaging=ValidTarget(unit,s.Target)&&Combat.Visible(unit,s.Target);
            if(engaging)
            {
                Face(unit,s.Target.transform.position,dt);unit.Rig?.AimAt(s.Target.Aim);
                if(s.FireLeft<=0&&CanShoot(unit,s.Target))
                {
                    Vector3 muzzle=unit.Rig!=null&&unit.Rig.Muzzle!=null?unit.Rig.Muzzle.position:unit.Aim;
                    float damage=(12+3*world.DefenseLevel)*(s.Target.IsBuilding?.5f:1f);
                    Combat.Rifle(unit,muzzle,(s.Target.Aim-muzzle).normalized,damage,false);
                    s.FireLeft=FireInterval;Shots++;unit.Rig?.Recoil();
                }
            }
            if(engaging&&s.State==ExpeditionState.Attacking){unit.Rig?.Animate(0,dt);return;}
            Vector3 goal=s.RallyPoint;
            if(s.State==ExpeditionState.Attacking&&s.Portal!=null&&s.Portal.Alive)
            {
                float lane=s.Lane+(s.RallySlot%3-1)*1.25f;
                Vector3[] route={new Vector3(lane,0,22),new Vector3(lane,GameWorld.GroundHeight(lane,32),32),new Vector3(lane,-4,46)};
                while(s.RouteStage<route.Length&&FlatDistance(unit.transform.position,route[s.RouteStage])<1.4f)s.RouteStage++;
                goal=s.RouteStage<route.Length?route[s.RouteStage]:s.Portal.transform.position+new Vector3((s.RallySlot%3-1)*1.5f,0,-14);
            }
            Vector3 before=unit.transform.position;
            unit.transform.position=s.Navigation.Step(unit,goal,dt,Speed,.42f);
            Vector3 motion=unit.transform.position-before;
            float moved=motion.magnitude;s.Travelled+=moved;
            if(moved>.00001f)Face(unit,unit.transform.position+motion,dt);
            unit.Rig?.Animate(moved/Mathf.Max(.0001f,dt),dt);
            if(moved>.00001f)Physics.SyncTransforms();
        }

        Health Acquire(Health unit)
        {
            Health target=null;float best=float.MaxValue;
            foreach(var h in world.Entities)
            {
                if(!ValidTarget(unit,h)||!Combat.Visible(unit,h))continue;
                float score=Vector3.Distance(unit.Aim,h.Aim)+(h.IsBuilding?5:0)-(world.Squad.FocusTarget==h?10:0);
                if(score<best){best=score;target=h;}
            }
            return target;
        }

        static bool ValidTarget(Health unit,Health target)=>target!=null&&target.Alive&&!target.IsSheltered&&target.Faction!=unit.Faction&&Vector3.Distance(unit.Aim,target.Aim)<=Range+target.Radius;
        static bool CanShoot(Health unit,Health target)
        {
            Vector3 muzzle=unit.Rig!=null&&unit.Rig.Muzzle!=null?unit.Rig.Muzzle.position:unit.Aim;
            Vector3 barrel=muzzle-unit.Aim;
            if(barrel.sqrMagnitude>.001f&&Combat.Trace(unit.Aim,barrel.normalized,barrel.magnitude,unit,out _,true))return false;
            Vector3 delta=target.Aim-muzzle;
            return !Combat.Trace(muzzle,delta.normalized,delta.magnitude+.1f,unit,out var hit,true)||hit.collider.GetComponentInParent<Health>()==target;
        }
        static void Face(Health unit,Vector3 point,float dt)
        {
            Vector3 delta=point-unit.transform.position;delta.y=0;
            if(delta.sqrMagnitude>.001f)unit.transform.rotation=Quaternion.Slerp(unit.transform.rotation,Quaternion.LookRotation(delta),Mathf.Min(1,dt*9));
        }
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
    }
}

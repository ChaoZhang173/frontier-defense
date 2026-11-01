using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public sealed class RepairWorker
    {
        public Health Self, Target, Repairing;
        public Vector3 ServicePoint, TargetPosition;
        public float ThinkLeft, BeamLeft, Travelled, Repaired;
        public readonly GuardNavigation Navigation=new GuardNavigation();
    }

    // Independent civilian workers never enter guard production or squad recruitment.
    public sealed class RepairDirector
    {
        public const int WorkerLimit=2;
        public const float RepairRate=6,ReplacementDuration=20,WorkerSpeed=3.6f;
        readonly GameWorld world;
        readonly Vector3[] home={new Vector3(-6.5f,0,0),new Vector3(6.5f,0,0)};
        readonly float[] replacement={-1,-1};
        readonly List<Vector3> route=new List<Vector3>(80);
        public readonly List<RepairWorker> Workers=new List<RepairWorker>(WorkerLimit);
        public float TotalRepaired {get;private set;}
        public int Replacements {get;private set;}
        public int WorkersAlive {get{int n=0;foreach(var w in Workers)if(w.Self!=null&&w.Self.Alive)n++;return n;}}
        public int RepairingCount {get{int n=0;foreach(var w in Workers)if(w.Self!=null&&w.Self.Alive&&w.Repairing!=null)n++;return n;}}
        public float ReplacementLeft(int slot)=>slot>=0&&slot<WorkerLimit?Mathf.Max(0,replacement[slot]):0;

        public RepairDirector(GameWorld owner)
        {
            world=owner;
            for(int slot=0;slot<WorkerLimit;slot++)Workers.Add(CreateWorker(slot));
        }
        RepairWorker CreateWorker(int slot)
        {
            Transform root=Art.Root("基地维修工 "+(slot+1),world.transform,home[slot]);
            root.gameObject.layer=Combat.UnitLayer;
            var health=root.gameObject.AddComponent<Health>();health.Setup(Role.Worker,Faction.Defense,130,1.2f,.42f);
            health.Slot=slot;health.Rig=Art.Marine(root,false);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
                if(renderer.name=="Helmet"||renderer.name=="Chest plating"||renderer.name=="Shoulder armor"||renderer.name=="Thigh")renderer.sharedMaterial=Art.Mat(7);
            var rig=health.Rig;rig.Gun.gameObject.SetActive(false);
            rig.Gun=Art.Root("Repair tool",rig.Body,new Vector3(.39f,-.13f,.22f));
            Art.Part(rig.Gun,"Welding grip",new Vector3(0,0,.22f),new Vector3(.24f,.24f,.46f),2);
            Art.Part(rig.Gun,"Welding nozzle",new Vector3(0,0,.49f),new Vector3(.12f,.12f,.16f),7);
            Art.Part(rig.Body,"Service pack",new Vector3(0,.15f,-.56f),new Vector3(.58f,.55f,.16f),7);
            rig.Muzzle=Art.Root("Repair spark",rig.Gun,new Vector3(0,0,.59f));rig.Finish();
            var collider=root.gameObject.AddComponent<CapsuleCollider>();collider.center=new Vector3(0,1,0);collider.height=2;collider.radius=.4f;collider.isTrigger=true;
            return new RepairWorker{Self=health};
        }
        public static bool InsideBase(Vector3 p)=>Mathf.Abs(p.x)<=26&&p.z>=-21&&p.z<=26;
        public bool CanRepair(Health target)=>target!=null&&target.Alive&&target.Current>0&&target.Current<target.Maximum&&
            target.Faction==Faction.Defense&&target.Role!=Role.Construction&&InsideBase(target.transform.position)&&
            (target.IsBuilding||target.IsMechanical);
        bool VisibleFrom(Health worker,Vector3 position,Health target)
        {
            Vector3 origin=position+Vector3.up*worker.AimHeight,delta=target.Aim-origin;
            if(delta.sqrMagnitude<.001f)return true;
            return !Combat.Trace(origin,delta.normalized,delta.magnitude+.1f,worker,out var hit,true)||hit.collider.GetComponentInParent<Health>()==target;
        }
        public bool TryServicePoint(RepairWorker worker,Health target,out Vector3 point)
        {
            point=Vector3.zero;
            var navigation=world.Squad!=null?world.Squad.Navigation:null;if(navigation==null||target==null)return false;
            Vector3 position=target.transform.position;float halfX=target.Radius,halfZ=target.Radius;
            if(target.IsBuilding)
            {
                var box=target.GetComponent<BoxCollider>();
                if(box!=null){halfX=box.bounds.extents.x;halfZ=box.bounds.extents.z;}
                else {var site=world.Construction.SiteFor(target);var size=site==null?new Vector3(9.5f,5.3f,8.2f):ConstructionDirector.Size(site.Role);halfX=size.x*.5f;halfZ=size.z*.5f;}
            }
            float gap=worker.Self.Radius+.65f,best=float.MaxValue;bool found=false;
            for(int i=0;i<8;i++)
            {
                int dx=i==0?-1:i==1?1:i<4?0:(i%2==0?-1:1),dz=i<2?0:i==2?-1:i==3?1:i<6?-1:1;
                Vector3 candidate=new Vector3(position.x+dx*(halfX+gap),0,position.z+dz*(halfZ+gap));
                candidate.y=GroundSurface.Height(candidate.x,candidate.z);
                if(!InsideBase(candidate)||!navigation.Fits(candidate,GuardNavigation.Radius)||!VisibleFrom(worker.Self,candidate,target))continue;
                float d=FlatDistance(worker.Self.transform.position,candidate);if(d>=best)continue;
                if(!navigation.Plan(worker.Self.transform.position,candidate,route,GuardNavigation.Radius))continue;
                // A service approach must remain within the compound, even when the map permits a longer detour.
                bool confined=true;foreach(var waypoint in route)if(!InsideBase(waypoint)){confined=false;break;}
                if(!confined)continue;
                found=true;best=d;point=candidate;
            }
            return found;
        }
        void ChooseTarget(RepairWorker worker)
        {
            Health selected=null;Vector3 service=Vector3.zero;float best=float.MaxValue;
            foreach(var target in world.Entities)
            {
                if(!CanRepair(target)||!TryServicePoint(worker,target,out var candidate))continue;
                bool occupied=Workers.Exists(other=>other!=worker&&other.Self!=null&&other.Self.Alive&&other.Target==target);
                float score=FlatDistance(worker.Self.transform.position,candidate)+(occupied?5:0)-(1-target.Current/target.Maximum)*(target.Role==Role.Command?9:4);
                if(score>=best)continue;best=score;selected=target;service=candidate;
            }
            worker.Target=selected;worker.ServicePoint=service;
            worker.TargetPosition=selected!=null?selected.transform.position:Vector3.zero;
            worker.ThinkLeft=.6f;
        }
        public void Tick(float dt)
        {
            if(!world.Active||dt<=0)return;
            for(int slot=0;slot<WorkerLimit;slot++)
            {
                var worker=Workers[slot];
                if(worker.Self==null||!worker.Self.Alive)
                {
                    worker.Repairing=null;worker.Target=null;
                    if(replacement[slot]<0)replacement[slot]=ReplacementDuration;
                    else replacement[slot]=Mathf.Max(0,replacement[slot]-dt);
                    if(replacement[slot]==0&&world.Command!=null&&world.Command.Alive)
                    {
                        Workers[slot]=CreateWorker(slot);replacement[slot]=-1;Replacements++;
                        world.Notice("维修工已补充 · 恢复基地维修");
                    }
                    continue;
                }
                worker.Repairing=null;worker.ThinkLeft-=dt;worker.BeamLeft-=dt;
                if(!CanRepair(worker.Target))
                {
                    if(worker.Target!=null||worker.ThinkLeft<=0)ChooseTarget(worker);
                }
                var target=worker.Target;
                if(target==null){Move(worker,home[slot],dt);continue;}
                if(FlatDistance(target.transform.position,worker.TargetPosition)>.35f)
                {
                    if(!TryServicePoint(worker,target,out var point)){worker.Target=null;continue;}
                    worker.ServicePoint=point;worker.TargetPosition=target.transform.position;
                }
                if(FlatDistance(worker.Self.transform.position,worker.ServicePoint)>.65f){Move(worker,worker.ServicePoint,dt);continue;}
                worker.Self.Rig?.Animate(0,dt);
                if(!InsideBase(worker.Self.transform.position)||!CanRepair(target)||!VisibleFrom(worker.Self,worker.Self.transform.position,target))continue;
                float before=target.Current;target.Heal(RepairRate*dt);float restored=target.Current-before;
                TotalRepaired+=restored;worker.Repaired+=restored;worker.Repairing=target;
                Face(worker.Self,target.transform.position,dt);worker.Self.Rig?.AimAt(target.Aim);
                if(worker.BeamLeft<=0&&world.Effects!=null)
                {
                    worker.BeamLeft=.12f;var muzzle=worker.Self.Rig!=null?worker.Self.Rig.Muzzle:null;
                    world.Effects.Tracer(muzzle!=null?muzzle.position:worker.Self.Aim,target.Aim,Art.Amber,.045f);
                }
            }
        }
        void Move(RepairWorker worker,Vector3 goal,float dt)
        {
            Vector3 before=worker.Self.transform.position,next=worker.Navigation.Step(worker.Self,goal,dt*(WorkerSpeed/GuardNavigation.Speed));
            if(InsideBase(next))worker.Self.transform.position=next;
            Vector3 delta=worker.Self.transform.position-before;float travelled=delta.magnitude;
            worker.Travelled+=travelled;
            if(travelled>.0001f)Face(worker.Self,before+delta*2,dt);
            worker.Self.Rig?.Animate(travelled/Mathf.Max(dt,.0001f),dt);
        }
        static void Face(Health worker,Vector3 goal,float dt)
        {
            Vector3 delta=goal-worker.transform.position;delta.y=0;
            if(delta.sqrMagnitude>.001f)worker.transform.rotation=Quaternion.Slerp(worker.transform.rotation,Quaternion.LookRotation(delta),Mathf.Min(1,dt*9));
        }
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier
{
    public enum SquadOrder { Follow, Hold, Bunker }
    public sealed class SquadDirector : MonoBehaviour
    {
        public const int Capacity=3,HireCost=120;
        public readonly SquadMember[] Members=new SquadMember[Capacity];
        public static readonly string[] Names={"猎隼","岩盾","脉冲"};
        public SquadOrder Order {get;private set;}
        public Health FocusTarget {get;private set;}
        public Vector3 HoldPoint {get;private set;}
        public int Kills,Hired,Losses,PeakCount;
        public int Count {get {int n=0;foreach(var m in Members)if(m!=null&&m.Self.Alive)n++;return n;}}
        public bool CanHire=>GameWorld.I.Command.Alive&&GameWorld.I.Player.Self.Alive&&!GameWorld.I.Ended&&
            ((GameWorld.I.Phase==Phase.Intermission&&!GameWorld.I.Paused)||GameWorld.I.CommandTerminalOpen);
        public SquadNavigation Navigation {get;private set;}
        float focusUntil,holdYaw;
        Vector3 rally;
        LineRenderer marker;
        public void Setup()
        {
            Navigation=new SquadNavigation();
            var go=new GameObject("Squad defense perimeter");go.transform.SetParent(transform);
            marker=go.AddComponent<LineRenderer>();marker.sharedMaterial=Resources.Load<Material>("EffectsGlow");
            marker.positionCount=49;marker.startWidth=marker.endWidth=.06f;marker.startColor=marker.endColor=Art.Amber;
            marker.shadowCastingMode=ShadowCastingMode.Off;marker.receiveShadows=false;marker.enabled=false;
        }
        public bool Hire()
        {
            var w=GameWorld.I;
            if(!CanHire||Count>=Capacity)return false;
            if(w.Credits<HireCost){w.Notice("补给不足 · 雇佣队员需要 120 补给");return false;}
            int slot=0;while(slot<Capacity&&Members[slot]!=null&&Members[slot].Self.Alive)slot++;
            if(slot==Capacity)return false;
            Vector3 spawn=Navigation.Resolve(new Vector3(-2+slot*2,0,-.5f));
            Transform root=Art.Root("Squad / "+Names[slot],w.transform,spawn);root.gameObject.layer=Combat.UnitLayer;
            var health=root.gameObject.AddComponent<Health>();health.Setup(Role.Squad,Faction.Defense,220,1.25f,.5f);health.Slot=slot;
            health.Rig=Art.Marine(root,false,true);
            var collider=root.gameObject.AddComponent<CapsuleCollider>();collider.center=Vector3.up;collider.height=2;collider.radius=.42f;collider.isTrigger=true;
            var member=root.gameObject.AddComponent<SquadMember>();member.Setup(this,health,slot);Members[slot]=member;
            w.Credits-=HireCost;Hired++;PeakCount=Mathf.Max(PeakCount,Count);
            w.Effects.Burst(health.Aim,Art.Amber,.7f);w.Sounds.Play(SoundId.Command,.6f);
            w.Notice(Names[slot]+" 已加入小队 · F 跟随 / G 驻守");return true;
        }
        public bool GiveOrder(SquadOrder order)
        {
            var w=GameWorld.I;
            if(w.Paused||w.Ended||w.Phase!=Phase.Assault||!w.Player.Self.Alive)return false;
            if(Count==0){w.Notice("小队尚未组建 · 靠近指挥中心按 E 雇佣");return false;}
            if(order==SquadOrder.Bunker&&(w.Fort==null||!w.Fort.NearEntrance))return false;
            if(order!=SquadOrder.Bunker)w.Fort?.ReleaseSquad();
            Order=order;HoldPoint=Navigation.Resolve(w.Fort!=null&&w.Fort.PlayerInside?w.Fort.Entrance:w.Player.transform.position);holdYaw=w.Player.Yaw;
            foreach(var m in Members)if(m!=null)m.Replan();
            if(order==SquadOrder.Hold)
            {
                for(int i=0;i<49;i++){float a=i*Mathf.PI/24;Vector3 p=HoldPoint+new Vector3(Mathf.Cos(a)*3,0,Mathf.Sin(a)*3);p.y=GameWorld.GroundHeight(p.x,p.z)+.055f;marker.SetPosition(i,p);}
            }
            marker.enabled=order==SquadOrder.Hold;
            w.Notice(order==SquadOrder.Bunker?"小队指令 · 前往地堡后门入驻":order==SquadOrder.Follow?"小队指令 · 跟随我":"小队指令 · 守住此处");w.Sounds.Play(SoundId.Command,.5f);return true;
        }
        public void MarkTarget(Health target)
        {
            if(target==null||!target.Alive||target.Faction!=Faction.Invaders)return;
            FocusTarget=target;focusUntil=GameWorld.I.SimTime+5;
        }
        public Vector3 Destination(int slot)
        {
            var p=GameWorld.I.Player;
            if(Order==SquadOrder.Bunker&&GameWorld.I.Bunker.Alive)return GameWorld.I.Fort.Entrance;
            Vector3 center=Order==SquadOrder.Hold?HoldPoint:rally;
            float yaw=Order==SquadOrder.Hold?holdYaw:p.Yaw;
            Vector3 offset=slot==0?new Vector3(-2.6f,0,-2.6f):slot==1?new Vector3(2.6f,0,-2.6f):new Vector3(0,0,-4.6f);
            Vector3 goal=center+Quaternion.Euler(0,yaw,0)*offset;
            goal.y=GameWorld.GroundHeight(goal.x,goal.z);return Navigation.Resolve(goal);
        }
        public void Tick(float dt)
        {
            var w=GameWorld.I;if(w.Player.Self.Alive)rally=w.Fort!=null&&w.Fort.PlayerInside?w.Fort.Entrance:w.Player.transform.position;
            if(FocusTarget==null||!FocusTarget.Alive||w.SimTime>focusUntil)FocusTarget=null;
            foreach(var member in Members)
            {
                if(member==null||!member.Self.Alive)continue;
                if(w.SimTime-member.Self.LastDamageTime>6)member.Self.Heal(7*dt);
                member.Tick(dt);
            }
            if(Count==0)marker.enabled=false;
        }
        public void OnLost(Health h)
        {
            GameWorld.I.Fort?.ForgetSquad(Members[h.Slot]);
            Losses++;Members[h.Slot]=null;
            GameWorld.I.Notice(Names[h.Slot]+" 阵亡 · 可在指挥中心补充队员");
        }
        public void OnBunkerUnavailable()
        {
            if(Order!=SquadOrder.Bunker)return;
            Order=SquadOrder.Hold;HoldPoint=GameWorld.I.Fort.Entrance;holdYaw=0;
            foreach(var member in Members)if(member!=null)member.Replan();
            marker.enabled=false;
        }
        public void Regroup(){foreach(var m in Members)if(m!=null){m.Self.Heal(100);m.Replan();}}
    }

    public sealed class SquadMember : MonoBehaviour
    {
        public Health Self {get;private set;}
        public Health Target {get;private set;}
        public int Slot,Shots;
        public float Travelled,DamageDealt;
        SquadDirector director;
        readonly List<Vector3> path=new List<Vector3>();
        float replan,think,cooldown;
        int waypoint;
        Vector3 plannedGoal;
        Renderer[] armor;
        Collider[] colliders;
        bool[] colliderStates;
        public void Setup(SquadDirector squad,Health health,int slot)
        {
            director=squad;Self=health;Slot=slot;replan=-1;
            armor=Self.Rig.GetComponentsInChildren<Renderer>();colliders=GetComponentsInChildren<Collider>();colliderStates=new bool[colliders.Length];
        }
        public void Replan(){replan=-1;}
        public void Tick(float dt)
        {
            if(!Self.Alive)return;
            var w=GameWorld.I;think-=dt;cooldown-=dt;replan-=dt;
            if(w.Fort!=null&&w.Fort.IsSquadInside(this)){TickBunker(dt);return;}
            var focus=director.FocusTarget;
            if(ValidTarget(focus))Target=focus;
            else if(think<=0||!ValidTarget(Target))
            {
                think=.35f;Target=null;float best=27;
                foreach(var h in w.Entities)if(ValidTarget(h))
                {
                    float d=Vector3.Distance(h.Aim,Self.Aim);if(d<best){best=d;Target=h;}
                }
            }
            Vector3 before=transform.position;Move(dt);
            float moved=Vector3.Distance(before,transform.position);Travelled+=moved;
            if(w.Fort!=null&&w.Fort.IsSquadApproaching(this)&&w.Fort.TryAdmitSquad(this)){TickBunker(dt);return;}
            if(Target!=null)
            {
                Face(Target.Aim,dt);Self.Rig.AimAt(Target.Aim);
                if(cooldown<=0&&ValidTarget(Target))
                {
                    float hp=Target.Current;Vector3 muzzle=Self.Rig.Muzzle.position;
                    Combat.Rifle(Self,muzzle,(Target.Aim-muzzle).normalized,13+3*w.DefenseLevel,false);Shots++;
                    if(Target!=null)DamageDealt+=Mathf.Max(0,hp-Target.Current);
                    cooldown=.68f;Self.Rig.Recoil();
                }
            }
            else if(moved>.001f)Face(transform.position+(transform.position-before),dt);
            Self.Rig.Animate(moved/Mathf.Max(dt,.001f),dt);
        }
        bool ValidTarget(Health h)
        {
            var fort=GameWorld.I.Fort;
            if(fort!=null&&fort.IsSquadInside(this))return fort.CanSeeFromPort(Self,fort.SquadMuzzle(Slot),h,30);
            return h!=null&&h.Alive&&!h.IsSheltered&&h.Faction==Faction.Invaders&&Vector3.Distance(h.Aim,Self.Aim)<27&&Combat.Visible(Self,h);
        }
        public void EnterBunker(Vector3 position)
        {
            if(Self.IsSheltered)return;
            Self.IsSheltered=true;transform.position=position;Target=null;path.Clear();waypoint=0;think=0;replan=-1;
            foreach(var r in armor)if(r!=null)r.enabled=false;
            for(int i=0;i<colliders.Length;i++)if(colliders[i]!=null){colliderStates[i]=colliders[i].enabled;colliders[i].enabled=false;}
        }
        public void LeaveBunker(Vector3 position)
        {
            if(Self==null)return;
            Self.IsSheltered=false;transform.position=position;Target=null;think=0;path.Clear();waypoint=0;replan=-1;
            foreach(var r in armor)if(r!=null)r.enabled=Self.Alive;
            for(int i=0;i<colliders.Length;i++)if(colliders[i]!=null)colliders[i].enabled=Self.Alive&&colliderStates[i];
        }
        void TickBunker(float dt)
        {
            var w=GameWorld.I;var fort=w.Fort;Vector3 muzzle=fort.SquadMuzzle(Slot);
            var focus=director.FocusTarget;
            if(ValidTarget(focus))Target=focus;
            else if(think<=0||!ValidTarget(Target))
            {
                think=.35f;Target=null;float nearest=30;
                foreach(var h in w.Entities)if(ValidTarget(h))
                {
                    float distance=Vector3.Distance(h.Aim,muzzle);if(distance<nearest){nearest=distance;Target=h;}
                }
            }
            if(w.Phase!=Phase.Assault||Target==null||cooldown>0||!ValidTarget(Target))return;
            float hp=Target.Current;
            Combat.Rifle(Self,muzzle,(Target.Aim-muzzle).normalized,13+3*w.DefenseLevel,false);Shots++;
            if(Target!=null)DamageDealt+=Mathf.Max(0,hp-Target.Current);
            cooldown=.68f;
        }
        void Move(float dt)
        {
            Vector3 goal=director.Destination(Slot);
            if(replan<=0&&(path.Count==0||Vector3.Distance(goal,plannedGoal)>1.3f||waypoint>=path.Count))
            {
                replan=.6f+Slot*.07f;plannedGoal=goal;waypoint=0;director.Navigation.Plan(transform.position,goal,path);
            }
            if(waypoint>=path.Count)return;
            if(Vector3.Distance(transform.position,path[waypoint])<.3f&&
                (waypoint+1>=path.Count||director.Navigation.Clear(transform.position,path[waypoint+1])))
            {waypoint++;if(waypoint>=path.Count)return;}
            // String pulling keeps movement smooth while preserving corner clearance.
            if(waypoint+1<path.Count&&director.Navigation.Clear(transform.position,path[waypoint+1]))waypoint++;
            Vector3 delta=path[waypoint]-transform.position;delta.y=0;
            float speed=director.Order==SquadOrder.Follow?7.6f:6;
            if(Vector3.Distance(transform.position,goal)>12)speed=9.1f;
            Vector3 step=delta.normalized*Mathf.Min(delta.magnitude,speed*dt);
            Vector3 separation=Vector3.zero;
            foreach(var h in GameWorld.I.Entities)
            {
                if(h==null||h==Self||!h.Alive||h.IsBuilding||h.IsSheltered)continue;
                Vector3 away=transform.position-h.transform.position;away.y=0;float length=away.magnitude;
                if(length>.02f&&length<1.1f)separation+=away/length*(1.1f-length)*2*dt;
            }
            Vector3 next=transform.position+step+separation;next.y=GameWorld.GroundHeight(next.x,next.z);
            if(!director.Navigation.Walkable(next)||director.Navigation.Blocked(transform.position,next))
            {
                next=transform.position+step;next.y=GameWorld.GroundHeight(next.x,next.z);
                if(!director.Navigation.Walkable(next)||director.Navigation.Blocked(transform.position,next)){replan=-1;path.Clear();return;}
            }
            transform.position=next;
        }
        void Face(Vector3 target,float dt)
        {
            Vector3 d=target-transform.position;d.y=0;
            if(d.sqrMagnitude>.0001f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(d),Mathf.Min(1,dt*12));
        }
    }
}

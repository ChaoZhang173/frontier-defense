using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier
{
    public enum SquadOrder { Follow, Hold, Bunker }
    public sealed class SquadDirector : MonoBehaviour
    {
        public const int Capacity=4,MercenaryCapacity=2,GuardCapacity=2,HireCost=120;
        public const float MercenaryRespawnDelay=12;
        public readonly SquadMember[] Members=new SquadMember[Capacity];
        public static readonly string[] Names={"猎隼","岩盾","脉冲","游隼"};
        readonly bool[] contracts=new bool[MercenaryCapacity];
        readonly float[] respawnLeft=new float[MercenaryCapacity];
        public SquadOrder Order {get;private set;}
        public Health FocusTarget {get;private set;}
        public Vector3 HoldPoint {get;private set;}
        public int Kills,Hired,Losses,PeakCount,Recruited,MercenaryRespawns;
        public int Count {get {int n=0;foreach(var m in Members)if(m!=null&&m.Self!=null&&m.Self.Alive)n++;return n;}}
        public int MercenaryCount {get {int n=0;for(int i=0;i<MercenaryCapacity;i++)if(LivingMember(i))n++;return n;}}
        public int RecruitedGuardCount {get {int n=0;for(int i=MercenaryCapacity;i<Capacity;i++)if(LivingMember(i))n++;return n;}}
        public int Contracts {get {int n=0;foreach(bool bought in contracts)if(bought)n++;return n;}}
        public bool IsMercenarySlot(int slot)=>slot>=0&&slot<MercenaryCapacity;
        public bool HasContract(int slot)=>IsMercenarySlot(slot)&&contracts[slot];
        public float RespawnRemaining(int slot)=>HasContract(slot)&&!LivingMember(slot)?Mathf.Max(0,respawnLeft[slot]):0;
        bool LivingMember(int slot)=>slot>=0&&slot<Members.Length&&Members[slot]!=null&&Members[slot].Self!=null&&Members[slot].Self.Alive;
        public bool CanHire=>Contracts<MercenaryCapacity&&GameWorld.I.CanManage;
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
        public void RebuildNavigation()
        {
            Navigation=new SquadNavigation();
            foreach(var member in Members)if(member!=null)member.Replan();
        }
        public bool Hire()
        {
            var w=GameWorld.I;
            if(!CanHire)return false;
            if(w.Credits<HireCost){w.Notice("补给不足 · 雇佣兵合同需要 120 补给");return false;}
            int slot=0;while(slot<MercenaryCapacity&&contracts[slot])slot++;
            if(slot==MercenaryCapacity)return false;
            contracts[slot]=true;w.Credits-=HireCost;Hired++;
            SpawnMercenary(slot,false);
            w.Notice(Names[slot]+" 已签约 · 阵亡后 12 秒免费重返战场");return true;
        }
        void SpawnMercenary(int slot,bool respawn)
        {
            var w=GameWorld.I;
            Vector3 spawn=Navigation.Resolve(new Vector3(-2+slot*2,0,-.5f));
            Transform root=Art.Root("Mercenary / "+Names[slot],w.transform,spawn);root.gameObject.layer=Combat.UnitLayer;
            var health=root.gameObject.AddComponent<Health>();health.Setup(Role.Squad,Faction.Defense,280,1.25f,.5f);health.Slot=slot;
            health.Rig=Art.Marine(root,false,true);
            var collider=root.gameObject.AddComponent<CapsuleCollider>();collider.center=Vector3.up;collider.height=2;collider.radius=.42f;collider.isTrigger=true;
            var member=root.gameObject.AddComponent<SquadMember>();member.Setup(this,health,slot);Members[slot]=member;
            respawnLeft[slot]=0;PeakCount=Mathf.Max(PeakCount,Count);
            w.Effects.Burst(health.Aim,Art.Amber,.7f);w.Sounds.Play(SoundId.Command,.6f);
            if(respawn){MercenaryRespawns++;w.Notice(Names[slot]+" 已在指挥中心复活 · 合同继续生效");}
        }
        public bool CanRecruitGuard(Health guard)
        {
            var w=GameWorld.I;
            if(w==null||!w.Active||!w.Playing||w.Player==null||!w.Player.Self.Alive||RecruitedGuardCount>=GuardCapacity)return false;
            if(guard==null||!guard.Alive||guard.Role!=Role.Marine||guard.Faction!=Faction.Defense||!w.Entities.Contains(guard))return false;
            if(guard.Rig==null||guard.GetComponent<SquadMember>()!=null)return false;
            return Vector3.Distance(w.Player.transform.position,guard.transform.position)<=4&&Combat.Visible(w.Player.Self,guard);
        }
        public bool RecruitGuard(Health guard)
        {
            var w=GameWorld.I;
            if(!CanRecruitGuard(guard))
            {
                if(w!=null&&RecruitedGuardCount>=GuardCapacity)w.Notice("招募守卫已满 · 最多 2 名普通守卫");
                return false;
            }
            int slot=MercenaryCapacity;while(slot<Capacity&&LivingMember(slot))slot++;
            if(slot==Capacity)return false;
            // Transfer this exact guard: release its post, then preserve its health and model.
            w.Fort?.ReleaseGuard(guard);
            if(!w.DetachGuard(guard))return false;
            var oldBrain=guard.Brain!=null?guard.Brain:guard.GetComponent<ActorBrain>();
            if(oldBrain!=null){oldBrain.enabled=false;Destroy(oldBrain);}
            guard.Brain=null;guard.Role=Role.Squad;guard.Slot=slot;
            guard.gameObject.name="Recruited guard / "+Names[slot];
            var member=guard.gameObject.AddComponent<SquadMember>();member.Setup(this,guard,slot);Members[slot]=member;
            Recruited++;PeakCount=Mathf.Max(PeakCount,Count);
            w.Effects.Burst(guard.Aim,Art.Amber,.6f);w.Sounds.Play(SoundId.Command,.6f);
            w.Notice(Names[slot]+" 已免费加入小队 · 保留当前生命，阵亡后不复活");return true;
        }
        public void ApplyMercenaryTraining()
        {
            for(int i=0;i<MercenaryCapacity;i++)if(LivingMember(i))Members[i].ApplyMercenaryTraining();
        }
        public bool GiveOrder(SquadOrder order)
        {
            var w=GameWorld.I;
            if(w.Paused||w.Ended||!w.Playing||!w.Player.Self.Alive)return false;
            if(Count==0){w.Notice(Contracts>0?"雇佣兵正在复活 · 也可招募普通守卫":"小队尚未组建 · 可雇佣或招募普通守卫");return false;}
            if(order==SquadOrder.Bunker&&(w.Fort==null||!w.Fort.TryAssignSquadBunker()))return false;
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
            var fort=GameWorld.I.Fort;
            if(Order==SquadOrder.Bunker&&fort!=null&&fort.SquadBunker!=null&&fort.SquadBunker.Alive)return fort.SquadEntrance;
            if(Order==SquadOrder.Hold&&CentralDefense.Contains(HoldPoint))
                return Navigation.Resolve(CentralDefense.SquadPost(slot));
            Vector3 center=Order==SquadOrder.Hold?HoldPoint:rally;
            float yaw=Order==SquadOrder.Hold?holdYaw:p.Yaw;
            Vector3 offset=slot==0?new Vector3(-2.6f,0,-2.6f):slot==1?new Vector3(2.6f,0,-2.6f):slot==2?new Vector3(-1.65f,0,-4.8f):new Vector3(1.65f,0,-4.8f);
            Vector3 goal=center+Quaternion.Euler(0,yaw,0)*offset;
            goal.y=GameWorld.GroundHeight(goal.x,goal.z);return Navigation.Resolve(goal);
        }
        public void Tick(float dt)
        {
            var w=GameWorld.I;if(w==null||!w.Active||dt<=0)return;
            if(w.Command!=null&&w.Command.Alive)
            {
                for(int i=0;i<MercenaryCapacity;i++)
                {
                    if(!contracts[i]||LivingMember(i))continue;
                    respawnLeft[i]=Mathf.Max(0,respawnLeft[i]-dt);
                    if(respawnLeft[i]<=0)SpawnMercenary(i,true);
                }
            }
            if(w.Player.Self.Alive)rally=w.Fort!=null&&w.Fort.PlayerInside?w.Fort.Entrance:w.Player.transform.position;
            if(FocusTarget==null||!FocusTarget.Alive||w.SimTime>focusUntil)FocusTarget=null;
            foreach(var member in Members)
            {
                if(member==null||member.Self==null||!member.Self.Alive)continue;
                if(w.SimTime-member.Self.LastDamageTime>6)member.Self.Heal(7*dt);
                member.Tick(dt);
            }
            if(Count==0)marker.enabled=false;
        }
        public void OnLost(Health h)
        {
            if(h==null||h.Slot<0||h.Slot>=Capacity)return;
            int slot=h.Slot;var member=Members[slot];
            if(member==null||member.Self!=h)return;
            GameWorld.I.Fort?.ForgetSquad(member);
            Losses++;Members[slot]=null;
            if(HasContract(slot))
            {
                respawnLeft[slot]=MercenaryRespawnDelay;
                GameWorld.I.Notice(Names[slot]+" 阵亡 · 12 秒后在指挥中心免费复活");
            }
            else GameWorld.I.Notice(Names[slot]+" 阵亡 · 可重新招募一名普通守卫");
        }
        public void OnBunkerUnavailable(){OnBunkerUnavailable(GameWorld.I.Fort.SquadEntrance);}
        public void OnBunkerUnavailable(Vector3 entrance)
        {
            if(Order!=SquadOrder.Bunker)return;
            Order=SquadOrder.Hold;HoldPoint=Navigation.Resolve(entrance);holdYaw=0;
            foreach(var member in Members)if(member!=null)member.Replan();
            marker.enabled=false;
        }
        public void Regroup(){foreach(var m in Members)if(m!=null){m.Self.Heal(100);m.Replan();}}
    }

    public sealed class SquadMember : MonoBehaviour
    {
        public Health Self {get;private set;}
        public Health Target {get;private set;}
        public bool IsMercenary {get;private set;}
        public float RifleDamage=>(IsMercenary?18:10)+3*GameWorld.I.DefenseLevel+(IsMercenary&&trainingApplied?6:0);
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
        bool trainingApplied;
        public void Setup(SquadDirector squad,Health health,int slot)
        {
            director=squad;Self=health;Slot=slot;IsMercenary=squad.IsMercenarySlot(slot);replan=-1;
            armor=Self.Rig.GetComponentsInChildren<Renderer>();colliders=GetComponentsInChildren<Collider>();colliderStates=new bool[colliders.Length];
            if(IsMercenary&&GameWorld.I.Tech!=null&&GameWorld.I.Tech.IsComplete(Research.MercenaryTraining))ApplyMercenaryTraining();
        }
        public void ApplyMercenaryTraining()
        {
            if(!IsMercenary||trainingApplied||Self==null||!Self.Alive)return;
            trainingApplied=true;Self.Maximum+=80;Self.Heal(80);
        }
        public void Replan(){replan=-1;path.Clear();waypoint=0;}
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
                    Combat.Rifle(Self,muzzle,(Target.Aim-muzzle).normalized,RifleDamage,false);Shots++;
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
            if(!w.Playing||Target==null||cooldown>0||!ValidTarget(Target))return;
            float hp=Target.Current;
            Combat.Rifle(Self,muzzle,(Target.Aim-muzzle).normalized,RifleDamage,false);Shots++;
            if(Target!=null)DamageDealt+=Mathf.Max(0,hp-Target.Current);
            cooldown=.68f;
        }
        void Move(float dt)
        {
            const float bodyRadius=.42f;
            Vector3 goal=director.Destination(Slot);
            if(replan<=0&&(path.Count==0||Vector3.Distance(goal,plannedGoal)>1.3f||waypoint>=path.Count))
            {
                replan=.6f+Slot*.07f;plannedGoal=goal;waypoint=0;director.Navigation.Plan(transform.position,goal,path,bodyRadius);
            }
            if(waypoint>=path.Count)return;
            if(Vector3.Distance(transform.position,path[waypoint])<.3f&&
                (waypoint+1>=path.Count||director.Navigation.Clear(transform.position,path[waypoint+1],bodyRadius)))
            {waypoint++;if(waypoint>=path.Count)return;}
            // String pulling keeps movement smooth while preserving corner clearance.
            if(waypoint+1<path.Count&&director.Navigation.Clear(transform.position,path[waypoint+1],bodyRadius))waypoint++;
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
            if(!director.Navigation.Fits(next,bodyRadius)||!director.Navigation.Clear(transform.position,next,bodyRadius))
            {
                next=transform.position+step;next.y=GameWorld.GroundHeight(next.x,next.z);
                if(!director.Navigation.Fits(next,bodyRadius)||!director.Navigation.Clear(transform.position,next,bodyRadius)){replan=-1;path.Clear();return;}
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

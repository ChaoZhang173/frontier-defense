using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Occupancy belongs to a building instance, so rebuilding a destroyed bunker
    // creates fresh stations without moving the soldiers stationed in another one.
    public sealed class BunkerDirector : MonoBehaviour
    {
        public const int Capacity=5;
        public const float HalfArc=70,MinPitch=-25,MaxPitch=25;
        public bool PlayerInside {get;private set;}
        public Health PlayerBunker {get;private set;}
        public Health SquadBunker {get;private set;}
        public Health DisplayBunker=>PlayerInside&&PlayerBunker!=null?PlayerBunker:NearestEntranceBunker()??DefaultBunker;
        public int SquadInside {get {int count=0;foreach(var m in occupants)if(m!=null&&m.Self!=null&&m.Self.Alive)count++;return count;}}
        public int GuardInside {get {int count=0;foreach(var guard in guards)if(ValidGuard(guard)&&guard.Inside)count++;return count;}}
        public int Occupants=>(PlayerInside?1:0)+SquadInside+GuardInside;
        public Vector3 Entrance=>EntranceFor(PlayerInside?PlayerBunker:DisplayBunker);
        public Vector3 SquadEntrance=>SquadBunker!=null?EntranceFor(SquadBunker):lastSquadEntrance;
        public Vector3 PlayerMuzzle=>Port(PlayerBunker!=null?PlayerBunker:DisplayBunker,0);
        public bool NearEntrance=>CanUsePlayer()&&(PlayerInside&&Usable(PlayerBunker)||NearestEntranceBunker()!=null);
        readonly SquadMember[] occupants=new SquadMember[SquadDirector.Capacity];
        readonly Health[] occupantBunkers=new Health[SquadDirector.Capacity];
        readonly Dictionary<Health,Vector3> entrances=new Dictionary<Health,Vector3>();
        readonly List<GuardStation> guards=new List<GuardStation>(16);
        readonly Dictionary<Health,GuardStation> guardLookup=new Dictionary<Health,GuardStation>();
        sealed class GuardStation
        {
            public Health Unit,Bunker,Target;
            public ActorBrain Brain;
            public Vector3 OriginalStation;
            public bool Inside,Returning;
            public int Port=-1;
            public float RetryLeft,Think,ShotLeft,ApproachLeft;
            public Renderer[] Renderers;
            public Collider[] Colliders;
            public bool[] RendererStates,ColliderStates;
        }
        GameWorld world;
        Vector3 lastSquadEntrance;

        public void Setup(){world=GameWorld.I;lastSquadEntrance=EntranceFor(DefaultBunker);}
        Health DefaultBunker
        {
            get
            {
                if(world==null)return null;
                if(world.Bunker!=null)return world.Bunker;
                foreach(var bunker in world.Bunkers)if(bunker!=null)return bunker;
                return null;
            }
        }
        bool CanUsePlayer()=>world!=null&&world.Player!=null&&world.Player.Self!=null&&world.Player.Self.Alive&&world.Squad!=null;
        bool CanOrder=>CanUsePlayer()&&world.Active&&world.Phase==Phase.Assault;
        static bool SameBunker(Health a,Health b)=>ReferenceEquals(a,b);
        static bool Usable(Health bunker)=>bunker!=null&&bunker.Alive&&bunker.Role==Role.Bunker&&bunker.gameObject.activeInHierarchy;

        // The navigation grid is rebuilt when construction changes the terrain.
        // Resolve against its current version; retain the last doorway for teardown.
        public Vector3 EntranceFor(Health bunker)
        {
            if(ReferenceEquals(bunker,null))return Vector3.zero;
            if(bunker==null)return entrances.TryGetValue(bunker,out var previous)?previous:Vector3.zero;
            Vector3 point=bunker.transform.TransformPoint(new Vector3(0,0,-4.2f));
            if(world!=null&&world.Squad!=null&&world.Squad.Navigation!=null)point=world.Squad.Navigation.Resolve(point);
            entrances[bunker]=point;return point;
        }
        Health NearestEntranceBunker()
        {
            if(!CanUsePlayer())return null;
            Health nearest=null;float best=3.2f;
            foreach(var bunker in world.Bunkers)ConsiderEntrance(bunker,ref nearest,ref best);
            // Preserve compatibility with a world configured with only the original field.
            ConsiderEntrance(world.Bunker,ref nearest,ref best);
            return nearest;
        }
        void ConsiderEntrance(Health bunker,ref Health nearest,ref float best)
        {
            if(!Usable(bunker))return;
            Vector3 position=world.Player.transform.position,door=EntranceFor(bunker);
            float distance=Vector3.Distance(position,door);
            if(distance>=best||world.Squad.Navigation.Blocked(position,door))return;
            nearest=bunker;best=distance;
        }
        Health InteractionBunker=>PlayerInside&&Usable(PlayerBunker)?PlayerBunker:NearestEntranceBunker();
        public int OccupantsFor(Health bunker)=>(!ReferenceEquals(bunker,null)&&PlayerInside&&SameBunker(PlayerBunker,bunker)?1:0)+SquadInsideFor(bunker)+GuardInsideFor(bunker);
        public int SquadInsideFor(Health bunker)
        {
            if(ReferenceEquals(bunker,null))return 0;
            int count=0;
            for(int i=0;i<occupants.Length;i++)if(SameBunker(occupantBunkers[i],bunker)&&occupants[i]!=null&&occupants[i].Self!=null&&occupants[i].Self.Alive)count++;
            return count;
        }
        static bool ValidGuard(GuardStation guard)=>guard!=null&&guard.Unit!=null&&guard.Unit.Alive&&guard.Unit.Role==Role.Marine&&guard.Brain!=null&&guard.Unit.Brain==guard.Brain;
        GuardStation FindGuard(Health unit)
        {
            if(ReferenceEquals(unit,null))return null;
            return guardLookup.TryGetValue(unit,out var guard)?guard:null;
        }
        public int GuardInsideFor(Health bunker)
        {
            if(ReferenceEquals(bunker,null))return 0;
            int count=0;
            foreach(var guard in guards)if(ValidGuard(guard)&&guard.Inside&&SameBunker(guard.Bunker,bunker))count++;
            return count;
        }
        public bool IsGuardInside(Health unit){var guard=FindGuard(unit);return ValidGuard(guard)&&guard.Inside;}
        // A binding includes a guard who is still walking to the rear door.
        public Health GuardBunker(Health unit){var guard=FindGuard(unit);return ValidGuard(guard)?guard.Bunker:null;}
        internal string GuardDiagnostic(Health unit)
        {
            var guard=FindGuard(unit);if(guard==null)return "untracked";
            return "inside="+guard.Inside+" returning="+guard.Returning+" bound="+(guard.Bunker!=null?guard.Bunker.name:"none")+
                " port="+guard.Port+" retry="+guard.RetryLeft.ToString("F2")+" approach="+guard.ApproachLeft.ToString("F2")+
                " occupants="+(guard.Bunker!=null?OccupantsFor(guard.Bunker):0)+" reserved="+(guard.Bunker!=null?ReservedSquadSeats(guard.Bunker):0);
        }
        public Vector3 GuardMuzzle(Health unit)
        {
            var guard=FindGuard(unit);
            if(ValidGuard(guard)&&guard.Inside)return Port(guard.Bunker,PortX(guard.Port));
            return unit!=null?(unit.Rig!=null&&unit.Rig.Muzzle!=null?unit.Rig.Muzzle.position:unit.Aim):Vector3.zero;
        }
        int ReservedSquadSeats(Health bunker)
        {
            if(world==null||world.Squad==null||world.Squad.Order!=SquadOrder.Bunker||!SameBunker(SquadBunker,bunker))return 0;
            int count=0;
            foreach(var member in world.Squad.Members)
                if(member!=null&&member.Self!=null&&member.Self.Alive&&!IsSquadInside(member))count++;
            return count;
        }
        bool GuardRoom(Health bunker,GuardStation current=null)
        {
            int committed=OccupantsFor(bunker)+ReservedSquadSeats(bunker);
            // Existing approaches keep their order when a player takes a seat.
            // A new approach counts every earlier claim, so guards do not overbook.
            foreach(var guard in guards)
            {
                if(ReferenceEquals(guard,current))break;
                if(ValidGuard(guard)&&!guard.Inside&&SameBunker(guard.Bunker,bunker))committed++;
            }
            return committed<Capacity;
        }
        bool MakeRoomForPriority(Health bunker)
        {
            if(OccupantsFor(bunker)<Capacity)return true;
            // Yield only when the player or one arriving squad member actually needs
            // a seat; issuing a squad order does not empty an occupied bunker.
            for(int i=guards.Count-1;i>=0;i--)
                if(ValidGuard(guards[i])&&guards[i].Inside&&SameBunker(guards[i].Bunker,bunker))
                {UnbindGuard(guards[i],12);return OccupantsFor(bunker)<Capacity;}
            return false;
        }
        int PriorityPortMask(Health bunker)
        {
            int mask=PlayerInside&&SameBunker(PlayerBunker,bunker)?1<<2:0;
            for(int i=0;i<occupants.Length;i++)
                if(occupants[i]!=null&&SameBunker(occupantBunkers[i],bunker))mask|=1<<SquadPort(i);
            return mask;
        }
        int FreeGuardPort(Health bunker)
        {
            int mask=PriorityPortMask(bunker);
            foreach(var guard in guards)if(ValidGuard(guard)&&guard.Inside&&SameBunker(guard.Bunker,bunker))mask|=1<<guard.Port;
            for(int port=0;port<Capacity;port++)if((mask&(1<<port))==0)return port;
            return -1;
        }
        void ReassignGuardPorts(Health bunker)
        {
            int mask=PriorityPortMask(bunker);
            foreach(var guard in guards)if(ValidGuard(guard)&&guard.Inside&&SameBunker(guard.Bunker,bunker))
            {
                int port=0;while(port<Capacity&&(mask&(1<<port))!=0)port++;
                if(port>=Capacity)break;
                guard.Port=port;mask|=1<<port;
                guard.Unit.transform.position=bunker.transform.TransformPoint(new Vector3(PortX(port),0,1.8f));
            }
        }
        Health FindGuardBunker(GuardStation guard)
        {
            Health nearest=null;float best=float.MaxValue;
            foreach(var bunker in world.Bunkers)ConsiderGuardBunker(guard,bunker,ref nearest,ref best);
            ConsiderGuardBunker(guard,world.Bunker,ref nearest,ref best);
            return nearest;
        }
        void ConsiderGuardBunker(GuardStation guard,Health bunker,ref Health nearest,ref float best)
        {
            if(!Usable(bunker)||!GuardRoom(bunker))return;
            Vector3 position=bunker.transform.position;
            if(guard.Unit.Lane==0?position.x>=0:position.x<=0)return;
            if(Vector3.Distance(guard.Brain.Station,position)>16)return;
            float distance=Vector3.Distance(guard.Unit.transform.position,position);
            if(distance>16||distance>=best)return;
            nearest=bunker;best=distance;
        }
        void AdmitGuard(GuardStation guard)
        {
            int port=FreeGuardPort(guard.Bunker);if(port<0)return;
            guard.Renderers=guard.Unit.GetComponentsInChildren<Renderer>();
            guard.Colliders=guard.Unit.GetComponentsInChildren<Collider>();
            guard.RendererStates=new bool[guard.Renderers.Length];guard.ColliderStates=new bool[guard.Colliders.Length];
            for(int i=0;i<guard.Renderers.Length;i++)if(guard.Renderers[i]!=null)
            {guard.RendererStates[i]=guard.Renderers[i].enabled;guard.Renderers[i].enabled=false;}
            for(int i=0;i<guard.Colliders.Length;i++)if(guard.Colliders[i]!=null)
            {guard.ColliderStates[i]=guard.Colliders[i].enabled;guard.Colliders[i].enabled=false;}
            guard.Port=port;guard.Inside=true;guard.Returning=false;guard.Target=null;guard.Think=0;
            guard.Unit.IsSheltered=true;
            guard.Unit.transform.position=guard.Bunker.transform.TransformPoint(new Vector3(PortX(port),0,1.8f));
            world.Effects.Burst(EntranceFor(guard.Bunker)+Vector3.up,Art.Cyan,.3f);
        }
        void UnbindGuard(GuardStation guard,float retry)
        {
            bool wasInside=guard.Inside,wasAssigned=!ReferenceEquals(guard.Bunker,null);
            Health bunker=guard.Bunker;int port=guard.Port;
            guard.Inside=false;guard.Bunker=null;guard.Target=null;guard.Port=-1;guard.Think=0;
            guard.RetryLeft=Mathf.Max(guard.RetryLeft,retry);guard.ApproachLeft=0;
            guard.Returning=(wasAssigned||guard.Returning)&&guard.Unit!=null&&guard.Unit.Alive&&guard.Unit.Role==Role.Marine;
            if(!wasInside||guard.Unit==null)return;
            guard.Unit.IsSheltered=false;guard.Unit.transform.position=ExitPosition(bunker,port);
            if(guard.Renderers!=null)for(int i=0;i<guard.Renderers.Length;i++)if(guard.Renderers[i]!=null)
                guard.Renderers[i].enabled=guard.Unit.Alive&&guard.RendererStates[i];
            if(guard.Colliders!=null)for(int i=0;i<guard.Colliders.Length;i++)if(guard.Colliders[i]!=null)
                guard.Colliders[i].enabled=guard.Unit.Alive&&guard.ColliderStates[i];
        }
        public void ReleaseGuard(Health unit)
        {
            var guard=FindGuard(unit);if(guard!=null)UnbindGuard(guard,12);
        }
        public bool TickGuard(ActorBrain brain,float dt)
        {
            if(world==null||brain==null||brain.Self==null||!brain.Self.Alive||brain.Self.Role!=Role.Marine||brain.Self.Brain!=brain)return false;
            var guard=FindGuard(brain.Self);
            if(!world.Active||dt<=0)return guard!=null&&(guard.Inside||guard.Returning||guard.Bunker!=null);
            if(guard==null)
            {
                guard=new GuardStation {Unit=brain.Self,Brain=brain,OriginalStation=brain.Station};
                guards.Add(guard);guardLookup.Add(guard.Unit,guard);
            }
            guard.RetryLeft=Mathf.Max(0,guard.RetryLeft-dt);
            if(!ReferenceEquals(guard.Bunker,null)&&!Usable(guard.Bunker))UnbindGuard(guard,12);
            if(guard.Inside){TickGuardWeapon(guard,dt);return true;}
            if(guard.Returning)
            {
                Vector3 post=guard.OriginalStation;post.y=GameWorld.GroundHeight(post.x,post.z);
                if(!world.Squad.Navigation.Fits(post,GuardNavigation.Radius))post=world.Squad.Navigation.Resolve(post);
                if(Vector3.Distance(guard.Unit.transform.position,post)>.8f)
                {brain.NavigateTo(guard.OriginalStation,dt);return true;}
                guard.Returning=false;
            }
            if(guard.Bunker==null)
            {
                if(guard.RetryLeft>0)return false;
                Health bunker=FindGuardBunker(guard);
                if(bunker==null){guard.RetryLeft=1;return false;}
                guard.Bunker=bunker;guard.OriginalStation=brain.Station;guard.ApproachLeft=22;
            }
            if(!GuardRoom(guard.Bunker,guard)){UnbindGuard(guard,12);return true;}
            Vector3 entrance=EntranceFor(guard.Bunker);
            if(Vector3.Distance(guard.Unit.transform.position,entrance)<=.85f&&!world.Squad.Navigation.Blocked(guard.Unit.transform.position,entrance))
            {
                AdmitGuard(guard);
                if(guard.Inside)TickGuardWeapon(guard,dt);
                return true;
            }
            brain.NavigateTo(entrance,dt);guard.ApproachLeft-=dt;
            if(guard.ApproachLeft<=0)UnbindGuard(guard,8);
            return true;
        }
        void TickGuardWeapon(GuardStation guard,float dt)
        {
            guard.Think-=dt;guard.ShotLeft-=dt;
            Vector3 muzzle=GuardMuzzle(guard.Unit);
            if(guard.Think<=0||!CanSeeFromPort(guard.Unit,muzzle,guard.Target,25))
            {
                guard.Think=.35f;guard.Target=null;float nearest=25;
                foreach(var target in world.Entities)if(CanSeeFromPort(guard.Unit,muzzle,target,nearest))
                {nearest=Vector3.Distance(muzzle,target.Aim);guard.Target=target;}
            }
            guard.Unit.Rig?.Animate(0,dt);
            if(world.Phase!=Phase.Assault||guard.Target==null||guard.ShotLeft>0||!CanSeeFromPort(guard.Unit,muzzle,guard.Target,25))return;
            Combat.Rifle(guard.Unit,muzzle,(guard.Target.Aim-muzzle).normalized,10+3*world.DefenseLevel,false);
            guard.ShotLeft=.58f+Random.Range(0,.12f);guard.Unit.Rig?.Recoil();
        }
        Vector3 Port(Health bunker,float x)=>bunker!=null?bunker.transform.TransformPoint(new Vector3(x,1.8f,3.17f)):Vector3.zero;
        static int SquadPort(int slot)=>slot<2?slot:slot+1;
        static float PortX(int port)=>(port-2)*1.1f;
        public Vector3 SquadMuzzle(int slot)
        {
            Health bunker=slot>=0&&slot<occupantBunkers.Length?occupantBunkers[slot]:null;
            if(bunker==null)bunker=SquadBunker!=null?SquadBunker:DisplayBunker;
            return Port(bunker,PortX(SquadPort(slot)));
        }
        public bool IsSquadInside(SquadMember member)=>member!=null&&member.Slot>=0&&member.Slot<occupants.Length&&occupants[member.Slot]==member;
        public bool IsSquadApproaching(SquadMember member)=>world!=null&&world.Squad!=null&&Usable(SquadBunker)&&member!=null&&member.Self!=null&&member.Self.Alive&&member.Slot>=0&&member.Slot<occupants.Length&&world.Squad.Members[member.Slot]==member&&!IsSquadInside(member)&&world.Squad.Order==SquadOrder.Bunker;

        public bool TryTogglePlayer()
        {
            if(!CanOrder)return false;
            if(PlayerInside)
            {
                ExitPlayer();world.Notice("已离开地堡 · 小队可继续驻防");world.Sounds.Play(SoundId.Command,.5f);return true;
            }
            Health bunker=InteractionBunker;if(bunker==null)return false;
            if(!MakeRoomForPriority(bunker)){world.Notice("这座地堡已满 · 每座最多驻守 "+Capacity+" 人");return true;}
            PlayerBunker=bunker;PlayerInside=true;
            world.Player.EnterBunker(bunker.transform.TransformPoint(new Vector3(0,0,2.25f)));
            ReassignGuardPorts(bunker);
            world.Notice("已进驻地堡 · 鼠标瞄准 / 左键射击 / R 换弹 / E 离开");
            world.Sounds.Play(SoundId.Command,.6f);return true;
        }
        public bool OrderSquadInside()
        {
            if(!CanOrder)return false;
            if(!NearEntrance){world.Notice("靠近地堡后门按 B · 命令小队入驻");return false;}
            if(world.Squad.Count==0){world.Notice("小队尚未组建 · 靠近指挥中心按 E 雇佣");return false;}
            return world.Squad.GiveOrder(SquadOrder.Bunker);
        }
        public bool TryAssignSquadBunker()
        {
            if(!CanOrder)return false;
            Health bunker=InteractionBunker;if(bunker==null)return false;
            if(!SameBunker(SquadBunker,bunker))ReleaseSquad();
            SquadBunker=bunker;lastSquadEntrance=EntranceFor(bunker);return true;
        }
        public bool TryAdmitSquad(SquadMember member)
        {
            if(world==null||!world.Active||world.Phase!=Phase.Assault||!IsSquadApproaching(member))return false;
            // The destination stays tied to the order even when the player changes bunker.
            Vector3 door=SquadEntrance;
            if(Vector3.Distance(member.transform.position,door)>.85f||world.Squad.Navigation.Blocked(member.transform.position,door))return false;
            if(!MakeRoomForPriority(SquadBunker))return false;
            occupants[member.Slot]=member;occupantBunkers[member.Slot]=SquadBunker;
            member.EnterBunker(SquadBunker.transform.TransformPoint(new Vector3(member.Slot-1,0,1.8f)));
            ReassignGuardPorts(SquadBunker);
            world.Effects.Burst(door+Vector3.up,Art.Amber,.35f);return true;
        }
        public bool WithinArc(Vector3 direction)=>WithinArc(PlayerInside?PlayerBunker:DisplayBunker,direction);
        public bool WithinArc(Health bunker,Vector3 direction)
        {
            if(bunker==null||direction.sqrMagnitude<.0001f)return false;
            Vector3 local=bunker.transform.InverseTransformDirection(direction.normalized);
            float yaw=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
            float pitch=-Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg;
            return Mathf.Abs(yaw)<=HalfArc+.01f&&pitch>=MinPitch-.01f&&pitch<=MaxPitch+.01f;
        }
        public void ClampPlayerAim(ref float yaw,ref float pitch)
        {
            Health bunker=PlayerBunker!=null?PlayerBunker:DisplayBunker;
            float forward=bunker!=null?bunker.transform.eulerAngles.y:0;
            yaw=forward+Mathf.Clamp(Mathf.DeltaAngle(forward,yaw),-HalfArc,HalfArc);
            pitch=Mathf.Clamp(pitch,MinPitch,MaxPitch);
        }
        Health ObserverBunker(Health observer)
        {
            if(observer!=null&&observer.Role==Role.Player&&PlayerInside)return PlayerBunker;
            if(observer!=null&&observer.Role==Role.Squad)
            {
                int slot=observer.Slot;
                if(slot>=0&&slot<occupants.Length&&occupants[slot]!=null&&occupants[slot].Self==observer)return occupantBunkers[slot];
                return SquadBunker;
            }
            if(observer!=null&&observer.Role==Role.Marine)return GuardBunker(observer);
            return DisplayBunker;
        }
        public bool CanSeeFromPort(Health observer,Vector3 origin,Health target,float range)
        {
            if(target==null||!target.Alive||target.IsSheltered||target.Faction!=Faction.Invaders)return false;
            Vector3 delta=target.Aim-origin;
            if(delta.magnitude>range||!WithinArc(ObserverBunker(observer),delta))return false;
            bool passFriendlies=observer==null||observer.Role!=Role.Marine;
            return !Combat.Trace(origin,delta.normalized,delta.magnitude+.1f,observer,out var hit,passFriendlies)||hit.collider.GetComponentInParent<Health>()==target;
        }
        Vector3 ExitPosition(Health bunker,int slot)
        {
            Vector3 offset=slot<0?Vector3.zero:new Vector3((slot-1)*1.4f,0,-1.8f);
            if(bunker!=null)offset=bunker.transform.TransformVector(offset);
            Vector3 position=EntranceFor(bunker)+offset;
            if(world!=null&&world.Squad!=null&&world.Squad.Navigation!=null)position=world.Squad.Navigation.Resolve(position);
            position.y=GameWorld.GroundHeight(position.x,position.z)+.08f;return position;
        }
        public void ExitPlayer()
        {
            if(!PlayerInside)return;
            Health bunker=PlayerBunker;PlayerInside=false;PlayerBunker=null;
            if(world!=null&&world.Player!=null)world.Player.LeaveBunker(ExitPosition(bunker,-1));
        }
        void ReleaseSlot(int slot)
        {
            var member=occupants[slot];var bunker=occupantBunkers[slot];occupants[slot]=null;occupantBunkers[slot]=null;
            if(member!=null)member.LeaveBunker(ExitPosition(bunker,slot));
        }
        public void ReleaseSquad()
        {
            if(!ReferenceEquals(SquadBunker,null))lastSquadEntrance=EntranceFor(SquadBunker);
            for(int i=0;i<occupants.Length;i++)ReleaseSlot(i);
            SquadBunker=null;
        }
        public void ForgetSquad(SquadMember member){if(IsSquadInside(member))ReleaseSlot(member.Slot);}
        public void Tick(float dt)
        {
            if(world==null)return;
            if(PlayerInside&&!Usable(PlayerBunker))OnDestroyed(PlayerBunker);
            if(!ReferenceEquals(SquadBunker,null)&&!Usable(SquadBunker))OnDestroyed(SquadBunker);
            if(PlayerInside&&(world.Player==null||world.Player.Self==null||!world.Player.Self.Alive))ExitPlayer();
            for(int i=0;i<occupants.Length;i++)
            {
                if(occupants[i]==null){occupantBunkers[i]=null;continue;}
                if(!Usable(occupantBunkers[i]))ReleaseSlot(i);
                else if(occupants[i].Self==null||!occupants[i].Self.Alive)ReleaseSlot(i);
            }
            for(int i=guards.Count-1;i>=0;i--)
            {
                var guard=guards[i];
                if(!ValidGuard(guard))
                {
                    UnbindGuard(guard,0);
                    if(!ReferenceEquals(guard.Unit,null))guardLookup.Remove(guard.Unit);
                    guards.RemoveAt(i);continue;
                }
                if(!ReferenceEquals(guard.Bunker,null)&&!Usable(guard.Bunker))UnbindGuard(guard,12);
            }
        }
        public void OnDestroyed(){OnDestroyed(DefaultBunker);}
        public void OnDestroyed(Health victim)
        {
            if(ReferenceEquals(victim,null))return;
            bool occupied=OccupantsFor(victim)>0;
            if(PlayerInside&&SameBunker(PlayerBunker,victim))ExitPlayer();
            for(int i=0;i<occupants.Length;i++)if(SameBunker(occupantBunkers[i],victim))ReleaseSlot(i);
            foreach(var guard in guards)if(SameBunker(guard.Bunker,victim))UnbindGuard(guard,12);
            if(SameBunker(SquadBunker,victim))
            {
                lastSquadEntrance=EntranceFor(victim);SquadBunker=null;
                if(world!=null&&world.Squad!=null)world.Squad.OnBunkerUnavailable(lastSquadEntrance);
            }
            if(occupied&&world!=null)world.Notice("一座地堡失守 · 该堡驻军已从后门撤离");
        }
        void OnDisable()
        {
            ExitPlayer();ReleaseSquad();
            foreach(var guard in guards)UnbindGuard(guard,0);
            guards.Clear();guardLookup.Clear();
        }
    }
}

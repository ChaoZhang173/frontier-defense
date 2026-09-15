using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Occupancy belongs to a building instance, so rebuilding a destroyed bunker
    // creates fresh stations without moving the soldiers stationed in another one.
    public sealed class BunkerDirector : MonoBehaviour
    {
        public const int Capacity=4;
        public const float HalfArc=70,MinPitch=-25,MaxPitch=25;
        public bool PlayerInside {get;private set;}
        public Health PlayerBunker {get;private set;}
        public Health SquadBunker {get;private set;}
        public Health DisplayBunker=>PlayerInside&&PlayerBunker!=null?PlayerBunker:NearestEntranceBunker()??DefaultBunker;
        public int SquadInside {get {int count=0;foreach(var m in occupants)if(m!=null&&m.Self!=null&&m.Self.Alive)count++;return count;}}
        public int Occupants=>(PlayerInside?1:0)+SquadInside;
        public Vector3 Entrance=>EntranceFor(PlayerInside?PlayerBunker:DisplayBunker);
        public Vector3 SquadEntrance=>SquadBunker!=null?EntranceFor(SquadBunker):lastSquadEntrance;
        public Vector3 PlayerMuzzle=>Port(PlayerBunker!=null?PlayerBunker:DisplayBunker,0);
        public bool NearEntrance=>CanUsePlayer()&&(PlayerInside&&Usable(PlayerBunker)||NearestEntranceBunker()!=null);
        readonly SquadMember[] occupants=new SquadMember[SquadDirector.Capacity];
        readonly Health[] occupantBunkers=new Health[SquadDirector.Capacity];
        readonly Dictionary<Health,Vector3> entrances=new Dictionary<Health,Vector3>();
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
        public int OccupantsFor(Health bunker)=>(!ReferenceEquals(bunker,null)&&PlayerInside&&SameBunker(PlayerBunker,bunker)?1:0)+SquadInsideFor(bunker);
        public int SquadInsideFor(Health bunker)
        {
            if(ReferenceEquals(bunker,null))return 0;
            int count=0;
            for(int i=0;i<occupants.Length;i++)if(SameBunker(occupantBunkers[i],bunker)&&occupants[i]!=null&&occupants[i].Self!=null&&occupants[i].Self.Alive)count++;
            return count;
        }
        Vector3 Port(Health bunker,float x)=>bunker!=null?bunker.transform.TransformPoint(new Vector3(x,1.8f,3.17f)):Vector3.zero;
        public Vector3 SquadMuzzle(int slot)
        {
            Health bunker=slot>=0&&slot<occupantBunkers.Length?occupantBunkers[slot]:null;
            if(bunker==null)bunker=SquadBunker!=null?SquadBunker:DisplayBunker;
            return Port(bunker,slot==0?-2.05f:slot==1?-.95f:1.8f);
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
            if(OccupantsFor(bunker)>=Capacity){world.Notice("这座地堡已满 · 每座最多驻守 4 人");return true;}
            PlayerBunker=bunker;PlayerInside=true;
            world.Player.EnterBunker(bunker.transform.TransformPoint(new Vector3(0,0,2.25f)));
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
            if(world==null||!world.Active||world.Phase!=Phase.Assault||!IsSquadApproaching(member)||OccupantsFor(SquadBunker)>=Capacity)return false;
            // The destination stays tied to the order even when the player changes bunker.
            Vector3 door=SquadEntrance;
            if(Vector3.Distance(member.transform.position,door)>.85f||world.Squad.Navigation.Blocked(member.transform.position,door))return false;
            occupants[member.Slot]=member;occupantBunkers[member.Slot]=SquadBunker;
            member.EnterBunker(SquadBunker.transform.TransformPoint(new Vector3(member.Slot-1,0,1.8f)));
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
            return DisplayBunker;
        }
        public bool CanSeeFromPort(Health observer,Vector3 origin,Health target,float range)
        {
            if(target==null||!target.Alive||target.IsSheltered||target.Faction!=Faction.Invaders)return false;
            Vector3 delta=target.Aim-origin;
            if(delta.magnitude>range||!WithinArc(ObserverBunker(observer),delta))return false;
            return !Combat.Trace(origin,delta.normalized,delta.magnitude+.1f,observer,out var hit,true)||hit.collider.GetComponentInParent<Health>()==target;
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
        }
        public void OnDestroyed(){OnDestroyed(DefaultBunker);}
        public void OnDestroyed(Health victim)
        {
            if(ReferenceEquals(victim,null))return;
            bool occupied=OccupantsFor(victim)>0;
            if(PlayerInside&&SameBunker(PlayerBunker,victim))ExitPlayer();
            for(int i=0;i<occupants.Length;i++)if(SameBunker(occupantBunkers[i],victim))ReleaseSlot(i);
            if(SameBunker(SquadBunker,victim))
            {
                lastSquadEntrance=EntranceFor(victim);SquadBunker=null;
                if(world!=null&&world.Squad!=null)world.Squad.OnBunkerUnavailable(lastSquadEntrance);
            }
            if(occupied&&world!=null)world.Notice("一座地堡失守 · 该堡驻军已从后门撤离");
        }
        void OnDisable(){ExitPlayer();ReleaseSquad();}
    }
}

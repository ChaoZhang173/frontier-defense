using UnityEngine;

namespace Frontier
{
    // The four firing positions are outside the solid building collider. Shots still
    // use the ordinary world trace, so neither the bunker nor another wall is ignored.
    public sealed class BunkerDirector : MonoBehaviour
    {
        public const int Capacity=4;
        public const float HalfArc=70,MinPitch=-25,MaxPitch=25;
        public bool PlayerInside {get;private set;}
        public int SquadInside {get {int count=0;foreach(var m in occupants)if(m!=null&&m.Self!=null&&m.Self.Alive)count++;return count;}}
        public int Occupants=>(PlayerInside?1:0)+SquadInside;
        public Vector3 Entrance {get;private set;}
        public Vector3 PlayerMuzzle=>Port(0);
        public bool NearEntrance
        {
            get
            {
                if(world==null||world.Player==null||world.Player.Self==null||!world.Player.Self.Alive||world.Bunker==null||!world.Bunker.Alive||world.Squad==null)return false;
                if(PlayerInside)return true;
                Vector3 position=world.Player.transform.position;
                return Vector3.Distance(position,Entrance)<3.2f&&!world.Squad.Navigation.Blocked(position,Entrance);
            }
        }
        readonly SquadMember[] occupants=new SquadMember[SquadDirector.Capacity];
        GameWorld world;
        bool destroyedHandled;

        public void Setup()
        {
            world=GameWorld.I;
            Entrance=world.Squad.Navigation.Resolve(world.Bunker.transform.position+new Vector3(0,0,-4.2f));
        }
        bool CanOrder=>world!=null&&world.Active&&world.Phase==Phase.Assault&&world.Player!=null&&world.Player.Self!=null&&world.Player.Self.Alive;
        Vector3 Port(float x)=>world.Bunker.transform.TransformPoint(new Vector3(x,1.8f,3.17f));
        public Vector3 SquadMuzzle(int slot)=>Port(slot==0?-2.05f:slot==1?-.95f:1.8f);
        public bool IsSquadInside(SquadMember member)=>member!=null&&member.Slot>=0&&member.Slot<occupants.Length&&occupants[member.Slot]==member;
        public bool IsSquadApproaching(SquadMember member)=>world!=null&&world.Squad!=null&&world.Bunker!=null&&world.Bunker.Alive&&member!=null&&member.Self!=null&&member.Self.Alive&&member.Slot>=0&&member.Slot<occupants.Length&&world.Squad.Members[member.Slot]==member&&!IsSquadInside(member)&&world.Squad.Order==SquadOrder.Bunker;

        public bool TryTogglePlayer()
        {
            if(!CanOrder)return false;
            if(PlayerInside)
            {
                ExitPlayer();world.Notice("已离开地堡 · 小队可继续驻防");world.Sounds.Play(SoundId.Command,.5f);return true;
            }
            if(!NearEntrance)return false;
            if(Occupants>=Capacity){world.Notice("地堡已满 · 最多驻守 4 人");return true;}
            PlayerInside=true;
            world.Player.EnterBunker(world.Bunker.transform.position+new Vector3(0,0,2.25f));
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
        public bool TryAdmitSquad(SquadMember member)
        {
            if(world==null||!world.Active||world.Phase!=Phase.Assault||!IsSquadApproaching(member)||Occupants>=Capacity)return false;
            // Every member must physically reach the rear door before it can occupy a port.
            if(Vector3.Distance(member.transform.position,Entrance)>.85f||world.Squad.Navigation.Blocked(member.transform.position,Entrance))return false;
            occupants[member.Slot]=member;
            member.EnterBunker(world.Bunker.transform.position+new Vector3(member.Slot-1,0,1.8f));
            world.Effects.Burst(Entrance+Vector3.up,Art.Amber,.35f);
            return true;
        }
        public bool WithinArc(Vector3 direction)
        {
            if(direction.sqrMagnitude<.0001f)return false;
            Vector3 local=world.Bunker.transform.InverseTransformDirection(direction.normalized);
            float yaw=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
            float pitch=-Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg;
            return Mathf.Abs(yaw)<=HalfArc+.01f&&pitch>=MinPitch-.01f&&pitch<=MaxPitch+.01f;
        }
        public void ClampPlayerAim(ref float yaw,ref float pitch)
        {
            float forward=world.Bunker.transform.eulerAngles.y;
            yaw=forward+Mathf.Clamp(Mathf.DeltaAngle(forward,yaw),-HalfArc,HalfArc);
            pitch=Mathf.Clamp(pitch,MinPitch,MaxPitch);
        }
        public bool CanSeeFromPort(Health observer,Vector3 origin,Health target,float range)
        {
            if(target==null||!target.Alive||target.IsSheltered||target.Faction!=Faction.Invaders)return false;
            Vector3 delta=target.Aim-origin;
            if(delta.magnitude>range||!WithinArc(delta))return false;
            return !Combat.Trace(origin,delta.normalized,delta.magnitude+.1f,observer,out var hit,true)||hit.collider.GetComponentInParent<Health>()==target;
        }
        Vector3 ExitPosition(int slot)
        {
            Vector3 offset=slot<0?Vector3.zero:new Vector3((slot-1)*1.4f,0,-1.8f);
            Vector3 resolved=world.Squad!=null&&world.Squad.Navigation!=null?world.Squad.Navigation.Resolve(Entrance+offset):Entrance+offset;
            resolved.y=GameWorld.GroundHeight(resolved.x,resolved.z)+.08f;
            return resolved;
        }
        public void ExitPlayer()
        {
            if(!PlayerInside)return;
            PlayerInside=false;
            if(world!=null&&world.Player!=null)world.Player.LeaveBunker(ExitPosition(-1));
        }
        public void ReleaseSquad()
        {
            for(int i=0;i<occupants.Length;i++)
            {
                var member=occupants[i];occupants[i]=null;
                if(member!=null&&world!=null)member.LeaveBunker(ExitPosition(i));
            }
        }
        public void ForgetSquad(SquadMember member)
        {
            if(!IsSquadInside(member))return;
            occupants[member.Slot]=null;member.LeaveBunker(ExitPosition(member.Slot));
        }
        public void Tick(float dt)
        {
            if(world==null)return;
            if(world.Bunker==null||!world.Bunker.Alive){OnDestroyed();return;}
            if(PlayerInside&&(world.Player==null||world.Player.Self==null||!world.Player.Self.Alive))ExitPlayer();
            for(int i=0;i<occupants.Length;i++)if(occupants[i]!=null&&(occupants[i].Self==null||!occupants[i].Self.Alive))ForgetSquad(occupants[i]);
        }
        public void OnDestroyed()
        {
            if(destroyedHandled)return;
            destroyedHandled=true;
            bool occupied=Occupants>0;
            ExitPlayer();ReleaseSquad();if(world!=null&&world.Squad!=null)world.Squad.OnBunkerUnavailable();
            if(occupied&&world!=null)world.Notice("地堡失守 · 全体驻军已从后门撤离");
        }
        void OnDisable()
        {
            ExitPlayer();ReleaseSquad();
        }
    }
}

using UnityEngine;
using System.Collections.Generic;

namespace Frontier
{
    public sealed class PlayerMotor : MonoBehaviour
    {
        public Health Self;
        public Camera View;
        public int Ammo=30;
        public float ReloadLeft, HitTimer, KillTimer, CriticalTimer, DamageFlash;
        public float RecoilPitch {get;private set;}
        public float ReticleSpread=>Aiming?4+RecoilPitch*2:8+horizontalVelocity.magnitude*.7f+RecoilPitch*3;
        public Health AimTarget {get;private set;}
        public bool BarrelBlocked {get;private set;}
        public int TotalShots,CriticalHits;
        public float LastConfirmedDamage;
        public sealed class HitFeedback { public Vector3 Position;public float Damage,Life;public bool Critical;public int TargetId; }
        public readonly List<HitFeedback> Feedback=new List<HitFeedback>(8);
        public bool Aiming, Sprinting, Bot;
        public float Yaw, Pitch=9;
        public Vector3 AimPoint;
        CharacterController controller;
        float gravity, cooldown, recoil, recoilYaw, botThink,controlRequested,ads;
        int cameraFrame=-1;
        Vector3 horizontalVelocity;
        public bool LostControl=>Cursor.lockState!=CursorLockMode.Locked && Time.unscaledTime-controlRequested>.8f;
        Health botTarget;
        Renderer[] armor;

        public void Setup(Health health, CharacterController cc)
        {
            Self=health;controller=cc;armor=Self.Rig.GetComponentsInChildren<Renderer>();
            var cameraObject=new GameObject("Shoulder camera");
            cameraObject.transform.SetParent(GameWorld.I.transform);
            View=cameraObject.AddComponent<Camera>();View.tag="MainCamera";
            View.fieldOfView=66;View.nearClipPlane=.12f;View.farClipPlane=220;
            View.backgroundColor=new Color(.31f,.37f,.41f);
            cameraObject.AddComponent<AudioListener>();
            UpdateCamera(1);
        }

        public void Tick(float dt)
        {
            cooldown-=dt;HitTimer=Mathf.Max(0,HitTimer-dt);KillTimer=Mathf.Max(0,KillTimer-dt);CriticalTimer=Mathf.Max(0,CriticalTimer-dt);
            RecoilPitch=Mathf.MoveTowards(RecoilPitch,0,dt*2.5f);recoilYaw=Mathf.MoveTowards(recoilYaw,0,dt*1.5f);
            for(int i=Feedback.Count-1;i>=0;i--){Feedback[i].Life-=dt;if(Feedback[i].Life<=0)Feedback.RemoveAt(i);}
            DamageFlash=Mathf.MoveTowards(DamageFlash,0,dt*1.3f);recoil=Mathf.MoveTowards(recoil,0,dt*4);
            if(ReloadLeft>0){ReloadLeft-=dt;if(ReloadLeft<=0){ReloadLeft=0;Ammo=30;GameWorld.I.Sounds.Play(SoundId.Ready,.32f);}}
            if(Bot){if(GameWorld.I.Fort!=null&&GameWorld.I.Fort.PlayerInside)BotBunkerTick(dt);else BotTick(dt);return;}
            if(GameWorld.I.Phase!=Phase.Assault){horizontalVelocity=Vector3.zero;Self.Rig.Animate(0,dt);return;}
            if(Cursor.lockState!=CursorLockMode.Locked)return;
            Aiming=Input.GetMouseButton(1);Sprinting=Input.GetKey(KeyCode.LeftShift)&&!Aiming;
            float sensitivity=GameWorld.I.Sensitivity*(Aiming?.68f:1);
            Yaw+=Input.GetAxisRaw("Mouse X")*sensitivity;
            Pitch=Mathf.Clamp(Pitch-Input.GetAxisRaw("Mouse Y")*sensitivity,-35,65);
            if(Input.GetKeyDown(KeyCode.F))GameWorld.I.Squad.GiveOrder(SquadOrder.Follow);
            if(Input.GetKeyDown(KeyCode.G))GameWorld.I.Squad.GiveOrder(SquadOrder.Hold);
            if(Input.GetKeyDown(KeyCode.B))GameWorld.I.Fort.OrderSquadInside();
            if(GameWorld.I.Fort.PlayerInside)
            {
                Sprinting=false;horizontalVelocity=Vector3.zero;
                GameWorld.I.Fort.ClampPlayerAim(ref Yaw,ref Pitch);
                UpdateCamera(dt);UpdateAim();
                if(Input.GetKeyDown(KeyCode.R))StartReload();
                if(Input.GetMouseButton(0))Fire();
                return;
            }
            Vector3 input=new Vector3(Input.GetAxisRaw("Horizontal"),0,Input.GetAxisRaw("Vertical"));
            input=Vector3.ClampMagnitude(input,1);
            Vector3 direction=Quaternion.Euler(0,Yaw,0)*input;
            float speed=Sprinting?8.2f:Aiming?3.8f:5.8f;
            horizontalVelocity=Vector3.MoveTowards(horizontalVelocity,direction*speed,dt*(input.sqrMagnitude>.01f?38:52));
            if(controller.isGrounded&&gravity<0)gravity=-2;
            gravity-=24*dt;controller.Move((horizontalVelocity+Vector3.up*gravity)*dt);
            transform.rotation=Quaternion.Euler(0,Yaw,0);
            if(transform.position.y < -12 || transform.position.sqrMagnitude>15000)Respawn();
            UpdateCamera(dt);UpdateAim();Self.Rig.AimAt(AimPoint);Self.Rig.Animate(horizontalVelocity.magnitude,dt);
            if(Input.GetKeyDown(KeyCode.R))StartReload();
            if(Input.GetMouseButton(0)&&!Sprinting)Fire();
        }

        void UpdateAim()
        {
            Ray ray=View.ViewportPointToRay(new Vector3(.5f,.5f,0));
            AimPoint=ray.origin+ray.direction*100;
            AimTarget=null;
            if(Combat.Trace(ray.origin,ray.direction,110,Self,out var hit,true))
            {
                AimPoint=hit.point;var h=hit.collider.GetComponentInParent<Health>();
                if(h!=null&&h.Alive&&h.Faction==Faction.Invaders)AimTarget=h;
            }
            if(GameWorld.I.Fort!=null&&GameWorld.I.Fort.PlayerInside){BarrelBlocked=false;return;}
            Vector3 reach=Self.Rig.Muzzle.position-Self.Aim;
            BarrelBlocked=Physics.Raycast(Self.Aim,reach.normalized,reach.magnitude,Combat.WorldMask,QueryTriggerInteraction.Ignore);
        }

        public bool Fire()
        {
            if(!Self.Alive||!GameWorld.I.Active||GameWorld.I.Phase!=Phase.Assault||cooldown>0||ReloadLeft>0)return false;
            var fort=GameWorld.I.Fort;
            bool stationed=fort!=null&&fort.PlayerInside;
            Vector3 muzzle=stationed?fort.PlayerMuzzle:Self.Rig.Muzzle.position;
            if(stationed&&!fort.WithinArc(AimPoint-muzzle))return false;
            if(Ammo<=0){StartReload();return false;}
            Ammo--;TotalShots++;cooldown=.115f;recoil=1;
            if(AimTarget!=null)GameWorld.I.Squad.MarkTarget(AimTarget);
            // Check the short segment too: the barrel must never shoot through a wall.
            Vector3 start=Self.Aim, reach=muzzle-start;
            if(!stationed&&Physics.Raycast(start,reach.normalized,out var obstruction,reach.magnitude,Combat.WorldMask,QueryTriggerInteraction.Ignore))
                muzzle=obstruction.point-reach.normalized*.06f;
            Vector3 shot=(AimPoint-muzzle).normalized;
            if(!Bot)
            {
                float spread=Aiming?.0006f:.0025f+horizontalVelocity.magnitude*.0003f;
                shot=(shot+View.transform.right*Random.Range(-spread,spread)+View.transform.up*Random.Range(-spread,spread)).normalized;
            }
            Combat.Rifle(Self,muzzle,shot,GameWorld.I.RifleDamage,true);Self.Rig.Recoil();
            if(!Bot){RecoilPitch=Mathf.Min(2.2f,RecoilPitch+(Aiming?.21f:.42f));recoilYaw=Mathf.Clamp(recoilYaw+Random.Range(-.09f,.09f),-.25f,.25f);}
            if(Ammo==0)StartReload();
            return true;
        }

        public void StartReload()
        {
            if(Ammo>=30||ReloadLeft>0||!Self.Alive)return;
            ReloadLeft=1.65f;GameWorld.I.Sounds.Play(SoundId.Reload,.35f);
        }

        public void UpdateCamera(float dt)
        {
            if(View==null||Self==null)return;
            var w=GameWorld.I;
            if(w==null)return;
            if(w.Phase==Phase.Briefing || w.Ended)
            {
                Vector3 position=new Vector3(-33,25,-30);
                View.transform.position=position;
                View.transform.LookAt(new Vector3(0,1,14));View.fieldOfView=58;
                SetArmorVisible(Self.Alive);return;
            }
            if(!Self.Alive)
            {
                View.transform.position=Vector3.Lerp(View.transform.position,transform.position+new Vector3(-4,9,-6),Mathf.Min(1,dt*2));
                View.transform.LookAt(transform.position+Vector3.up);return;
            }
            if(cameraFrame!=Time.frameCount)
            {
                ads=Mathf.MoveTowards(ads,Aiming?1:0,dt*6);cameraFrame=Time.frameCount;
            }
            if(w.Fort!=null&&w.Fort.PlayerInside)
            {
                float stationYaw=Yaw+recoilYaw,stationPitch=Pitch-RecoilPitch-recoil*.12f;
                w.Fort.ClampPlayerAim(ref stationYaw,ref stationPitch);
                View.transform.SetPositionAndRotation(w.Fort.PlayerMuzzle,Quaternion.Euler(stationPitch,stationYaw,0));
                View.fieldOfView=Mathf.Lerp(66,49,ads);SetArmorVisible(false);return;
            }
            Quaternion rotation=Quaternion.Euler(Pitch-RecoilPitch-recoil*.12f,Yaw+recoilYaw,0);
            Vector3 pivot=transform.position+Vector3.up*1.55f;
            Vector3 desired=pivot+rotation*new Vector3(Mathf.Lerp(.68f,.57f,ads),.22f,Mathf.Lerp(-4.9f,-2.8f,ads));
            Vector3 difference=desired-pivot;
            if(Physics.SphereCast(pivot,.21f,difference.normalized,out var hit,difference.magnitude,Combat.WorldMask,QueryTriggerInteraction.Ignore))
                desired=pivot+difference.normalized*Mathf.Max(.16f,hit.distance-.12f);
            View.transform.SetPositionAndRotation(desired,rotation);
            View.fieldOfView=Mathf.Lerp(66,49,ads);
            SetArmorVisible((desired-pivot).sqrMagnitude>.65f);
        }

        void SetArmorVisible(bool visible){if(armor==null)return;if(Self!=null&&Self.IsSheltered)visible=false;foreach(var r in armor)if(r!=null)r.enabled=visible;}
        public void Hurt(float amount){DamageFlash=Mathf.Clamp01(DamageFlash+amount/75);}
        public void HitConfirm(bool killed,bool critical,float damage,Vector3 position,int targetId)
        {
            HitTimer=.16f;if(killed)KillTimer=.42f;if(critical){CriticalTimer=.35f;CriticalHits++;}LastConfirmedDamage=damage;
            var sounds=GameWorld.I.Sounds;sounds.Play(killed?SoundId.Kill:critical?SoundId.Critical:SoundId.Hit,killed?.55f:critical?.48f:.30f);
            HitFeedback existing=Feedback.Find(h=>h.TargetId==targetId&&h.Life>.45f);
            if(existing!=null){existing.Damage+=damage;existing.Life=.7f;existing.Critical|=critical;existing.Position=position;}
            else
            {
                if(Feedback.Count>=8)Feedback.RemoveAt(0);
                Feedback.Add(new HitFeedback{Position=position,Damage=damage,Life=.7f,Critical=critical,TargetId=targetId});
            }
        }
        public void EnterBunker(Vector3 position)
        {
            controller.enabled=false;Self.IsSheltered=true;transform.position=position;
            Yaw=GameWorld.I.Bunker.transform.eulerAngles.y;Pitch=2;RecoilPitch=0;recoilYaw=0;recoil=0;
            gravity=0;horizontalVelocity=Vector3.zero;Aiming=false;Sprinting=false;AimTarget=null;BarrelBlocked=false;
            AimPoint=GameWorld.I.Fort.PlayerMuzzle+Quaternion.Euler(Pitch,Yaw,0)*Vector3.forward*100;
            SetArmorVisible(false);UpdateCamera(1);
        }
        public void LeaveBunker(Vector3 position)
        {
            if(Self==null)return;
            if(controller!=null)controller.enabled=false;Self.IsSheltered=false;transform.position=position;
            gravity=0;horizontalVelocity=Vector3.zero;Aiming=false;Sprinting=false;AimTarget=null;
            if(controller!=null)controller.enabled=Self.Alive;SetArmorVisible(Self.Alive);
            if(Self.Alive)UpdateCamera(1);
        }
        public void PlayerKilled(){GameWorld.I.Fort?.ExitPlayer();Self.IsSheltered=false;controller.enabled=false;SetArmorVisible(false);Aiming=false;ReloadLeft=0;}
        public void Respawn()
        {
            GameWorld.I.Fort?.ExitPlayer();Self.IsSheltered=false;
            controller.enabled=false;transform.position=new Vector3(0,.1f,3);transform.rotation=Quaternion.identity;
            Self.Alive=true;Self.Current=Self.Maximum;Self.LastDamageTime=GameWorld.I.SimTime;
            controller.enabled=true;Ammo=30;ReloadLeft=0;gravity=0;Yaw=0;Pitch=9;DamageFlash=0;RecoilPitch=0;recoilYaw=0;ads=0;horizontalVelocity=Vector3.zero;AimTarget=null;Feedback.Clear();
            SetArmorVisible(true);GameWorld.I.Effects.Burst(Self.Aim,Art.Cyan,1);
        }
        public void RequestControl(){controlRequested=Time.unscaledTime;cooldown=Mathf.Max(cooldown,.18f);if(GameWorld.I.Diagnostic)return;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        public void ReleaseControl(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;Aiming=false;horizontalVelocity=Vector3.zero;}

        void BotTick(float dt)
        {
            if(GameWorld.I.Phase!=Phase.Assault)return;
            botThink-=dt;
            if(botThink<=0||botTarget==null||!botTarget.Alive)
            {
                botThink=.4f;botTarget=GameWorld.I.FindTarget(Self,90);
            }
            Vector3 destination=new Vector3(0,0,15);
            if(botTarget!=null)destination=new Vector3(botTarget.Lane==0?-11.5f:11.5f,0,Combat.Visible(Self,botTarget)?17:24);
            Vector3 direction=destination-transform.position;direction.y=0;
            if(direction.magnitude>1)controller.Move(direction.normalized*5.7f*dt+Vector3.down*3*dt);
            if(botTarget!=null)
            {
                AimPoint=botTarget.Aim;
                Vector3 d=AimPoint-Self.Aim;Yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;Pitch=-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg;
                transform.rotation=Quaternion.Euler(0,Yaw,0);Self.Rig.AimAt(AimPoint);
                if(Combat.Visible(Self,botTarget))Fire();
            }
            Self.Rig.Animate(direction.magnitude>1?5:0,dt);
        }
        void BotBunkerTick(float dt)
        {
            var w=GameWorld.I;if(w.Phase!=Phase.Assault)return;
            var fort=w.Fort;botThink-=dt;
            if(botThink<=0||!fort.CanSeeFromPort(Self,fort.PlayerMuzzle,botTarget,90))
            {
                botThink=.35f;botTarget=null;float nearest=90;
                foreach(var h in w.Entities)if(fort.CanSeeFromPort(Self,fort.PlayerMuzzle,h,nearest))
                {nearest=Vector3.Distance(fort.PlayerMuzzle,h.Aim);botTarget=h;}
            }
            if(botTarget==null)return;
            AimPoint=botTarget.Aim;AimTarget=botTarget;
            Vector3 delta=AimPoint-fort.PlayerMuzzle;Yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
            Pitch=-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg;
            fort.ClampPlayerAim(ref Yaw,ref Pitch);UpdateCamera(dt);Fire();
        }
    }
}

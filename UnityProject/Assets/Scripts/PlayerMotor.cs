using UnityEngine;

namespace Frontier
{
    public sealed class PlayerMotor : MonoBehaviour
    {
        public Health Self;
        public Camera View;
        public int Ammo=30;
        public float ReloadLeft, HitTimer, KillTimer, DamageFlash;
        public bool Aiming, Sprinting, Bot;
        public float Yaw, Pitch=9;
        public Vector3 AimPoint;
        CharacterController controller;
        float gravity, cooldown, recoil, botThink,controlRequested;
        public bool LostControl=>Cursor.lockState!=CursorLockMode.Locked && Time.unscaledTime-controlRequested>.8f;
        Health botTarget;
        Renderer[] armor;

        public void Setup(Health health, CharacterController cc)
        {
            Self=health;controller=cc;armor=Self.Rig.GetComponentsInChildren<Renderer>();
            var cameraObject=new GameObject("Shoulder camera");
            cameraObject.transform.SetParent(GameWorld.I.transform);
            View=cameraObject.AddComponent<Camera>();View.tag="MainCamera";
            View.fieldOfView=62;View.nearClipPlane=.12f;View.farClipPlane=220;
            View.backgroundColor=new Color(.31f,.37f,.41f);
            cameraObject.AddComponent<AudioListener>();
            UpdateCamera(1);
        }

        public void Tick(float dt)
        {
            cooldown-=dt;HitTimer=Mathf.Max(0,HitTimer-dt);KillTimer=Mathf.Max(0,KillTimer-dt);
            DamageFlash=Mathf.MoveTowards(DamageFlash,0,dt*1.3f);recoil=Mathf.MoveTowards(recoil,0,dt*4);
            if(ReloadLeft>0){ReloadLeft-=dt;if(ReloadLeft<=0){ReloadLeft=0;Ammo=30;}}
            if(Bot){BotTick(dt);return;}
            if(GameWorld.I.Phase!=Phase.Assault){Self.Rig.Animate(0,dt);return;}
            if(Cursor.lockState!=CursorLockMode.Locked)return;
            Yaw+=Input.GetAxisRaw("Mouse X")*GameWorld.I.Sensitivity;
            Pitch=Mathf.Clamp(Pitch-Input.GetAxisRaw("Mouse Y")*GameWorld.I.Sensitivity,-35,65);
            Aiming=Input.GetMouseButton(1);Sprinting=Input.GetKey(KeyCode.LeftShift)&&!Aiming;
            Vector3 input=new Vector3(Input.GetAxisRaw("Horizontal"),0,Input.GetAxisRaw("Vertical"));
            input=Vector3.ClampMagnitude(input,1);
            Vector3 direction=Quaternion.Euler(0,Yaw,0)*input;
            float speed=Sprinting?8.5f:Aiming?3.4f:5.7f;
            if(controller.isGrounded&&gravity<0)gravity=-2;
            gravity-=24*dt;controller.Move((direction*speed+Vector3.up*gravity)*dt);
            transform.rotation=Quaternion.Euler(0,Yaw,0);
            if(transform.position.y < -12 || transform.position.sqrMagnitude>15000)Respawn();
            UpdateAim();Self.Rig.AimAt(AimPoint);Self.Rig.Animate(input.magnitude*speed,dt);
            if(Input.GetKeyDown(KeyCode.R))StartReload();
            if(Input.GetMouseButton(0)&&!Sprinting)Fire();
        }

        void UpdateAim()
        {
            Ray ray=View.ViewportPointToRay(new Vector3(.5f,.5f,0));
            AimPoint=ray.origin+ray.direction*100;
            if(Combat.Trace(ray.origin,ray.direction,110,Self,out var hit))AimPoint=hit.point;
        }

        public bool Fire()
        {
            if(!Self.Alive||cooldown>0||ReloadLeft>0)return false;
            if(Ammo<=0){StartReload();return false;}
            Ammo--;cooldown=.115f;recoil=1;
            Vector3 muzzle=Self.Rig.Muzzle.position;
            // Check the short segment too: the barrel must never shoot through a wall.
            Vector3 start=Self.Aim, reach=muzzle-start;
            if(Physics.Raycast(start,reach.normalized,out var obstruction,reach.magnitude,Combat.WorldMask,QueryTriggerInteraction.Ignore))
                muzzle=obstruction.point-reach.normalized*.06f;
            Vector3 shot=(AimPoint-muzzle).normalized;
            if(!Bot)
            {
                float spread=Aiming?.0012f:.005f;
                shot=(shot+View.transform.right*Random.Range(-spread,spread)+View.transform.up*Random.Range(-spread,spread)).normalized;
            }
            Combat.Rifle(Self,muzzle,shot,GameWorld.I.RifleDamage,true);Self.Rig.Recoil();
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
            if(View==null)return;
            var w=GameWorld.I;
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
            Quaternion rotation=Quaternion.Euler(Pitch-recoil*.45f,Yaw,0);
            Vector3 pivot=transform.position+Vector3.up*1.55f;
            Vector3 desired=pivot+rotation*new Vector3(Aiming?.58f:.68f,.25f,Aiming?-2.65f:-4.7f);
            Vector3 difference=desired-pivot;
            if(Physics.SphereCast(pivot,.21f,difference.normalized,out var hit,difference.magnitude,Combat.WorldMask,QueryTriggerInteraction.Ignore))
                desired=pivot+difference.normalized*Mathf.Max(.16f,hit.distance-.12f);
            View.transform.SetPositionAndRotation(desired,rotation);
            View.fieldOfView=Mathf.Lerp(View.fieldOfView,Aiming?48:62,Mathf.Min(1,dt*12));
            SetArmorVisible((desired-pivot).sqrMagnitude>.65f);
        }

        void SetArmorVisible(bool visible){foreach(var r in armor)if(r!=null)r.enabled=visible;}
        public void Hurt(float amount){DamageFlash=Mathf.Clamp01(DamageFlash+amount/75);}
        public void HitConfirm(bool killed,bool critical){HitTimer=critical?.25f:.13f;if(killed)KillTimer=.35f;}
        public void PlayerKilled(){controller.enabled=false;SetArmorVisible(false);Aiming=false;ReloadLeft=0;}
        public void Respawn()
        {
            controller.enabled=false;transform.position=new Vector3(0,.1f,3);transform.rotation=Quaternion.identity;
            Self.Alive=true;Self.Current=Self.Maximum;Self.LastDamageTime=GameWorld.I.SimTime;
            controller.enabled=true;Ammo=30;ReloadLeft=0;gravity=0;Yaw=0;Pitch=9;DamageFlash=0;
            SetArmorVisible(true);GameWorld.I.Effects.Burst(Self.Aim,Art.Cyan,1);
        }
        public void RequestControl(){controlRequested=Time.unscaledTime;if(GameWorld.I.Diagnostic)return;Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        public void ReleaseControl(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;Aiming=false;}

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
    }
}

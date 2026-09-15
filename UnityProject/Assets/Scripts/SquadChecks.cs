using System;
using UnityEngine;

namespace Frontier
{
    public static class SquadChecks
    {
        public static void Run(GameWorld w,Action<bool,string> check)
        {
            var squad=w.Squad;
            w.BeginCampaign();
            check(squad.Count==0&&w.MarinesAlive==4,"Garrison and squad begin with separate rosters");
            int credits=w.Credits;
            check(!squad.Hire()&&w.Credits==credits,"Hiring requires the command terminal or intermission");
            check(w.TryOpenCommandTerminal()&&w.Paused,"The nearby command terminal pauses the battle");
            check(squad.Hire()&&squad.Count==1&&w.Credits==credits-SquadDirector.HireCost,"Hiring creates one living squad member and deducts exactly 120");
            credits=w.Credits;
            check(!squad.Hire()&&w.Credits==credits,"Insufficient funds never create a squad member");
            w.Credits=1000;squad.Hire();squad.Hire();credits=w.Credits;
            check(squad.Count==3&&!squad.Hire()&&w.Credits==credits,"Squad cap rejects a fourth recruit without charging");
            check(w.MarinesAlive==4,"Recruiting never consumes an automatic garrison slot");
            Vector3 pausedPosition=squad.Members[0].transform.position;float pausedTime=w.SimTime;
            w.Tick(5);
            check(w.SimTime==pausedTime&&squad.Members[0].transform.position==pausedPosition,"Squad movement freezes with the paused simulation");
            check(!squad.GiveOrder(SquadOrder.Hold),"Orders cannot change while the battle is paused");
            w.SetPaused(false);

            PlacePlayer(w,new Vector3(-19,0,-15));w.Player.Yaw=180;Step(w,20);
            check(Arrived(squad),"All three followers route around the HQ and barracks");
            check(ClearOfBuildings(squad),"Followers remain outside building collision volumes");
            PlacePlayer(w,new Vector3(13,0,18));w.Player.Yaw=0;Step(w,22);
            check(Arrived(squad),"Followers cross the base and navigate around the bunker");
            check(squad.GiveOrder(SquadOrder.Hold),"A living player can issue the hold-position order");
            Step(w,3);Vector3 hold=squad.HoldPoint;
            PlacePlayer(w,new Vector3(-13,0,18));Step(w,10);
            check(squad.Order==SquadOrder.Hold&&squad.HoldPoint==hold&&Arrived(squad),"A held squad remains at its order location when the player moves away");
            squad.GiveOrder(SquadOrder.Follow);PlacePlayer(w,new Vector3(-13, -4,55));Step(w,25);
            check(Arrived(squad)&&squad.Members[0].transform.position.y<-3.9f,"Follow resumes and all members traverse the west ramp");
            PlacePlayer(w,new Vector3(13,-4,55));Step(w,12);
            PlacePlayer(w,new Vector3(13,0,18));Step(w,25);
            check(Arrived(squad)&&ClearOfBuildings(squad),"Squad returns uphill through the east entrance without crossing walls");

            PlacePlayer(w,new Vector3(0,0,18));Step(w,14);
            var near=w.SpawnEnemy(Role.Runner,0,new Vector3(-3,0,23));near.Current=near.Maximum=2000;
            var focus=w.SpawnEnemy(Role.Spitter,1,new Vector3(3,0,24));focus.Current=focus.Maximum=2000;
            Physics.SyncTransforms();Step(w,.2f);squad.MarkTarget(focus);Step(w,.05f);
            bool assigned=true;foreach(var m in squad.Members)assigned&=m.Target==focus;
            check(assigned,"Every squad member prioritizes the player's marked visible target");
            float hp=focus.Current,nearHp=near.Current;Step(w,1.5f);
            check(focus.Current<hp&&near.Current==nearHp,"Squad bullets deal real damage to the marked target instead of the nearer alternative");
            focus.Damage(10000,squad.Members[0].Self);Step(w,.1f);
            check(squad.Kills==1&&squad.FocusTarget==null,"Squad kills are counted and dead focus targets are cleared");
            near.Damage(10000,null);

            var lost=squad.Members[1].Self;credits=w.Credits;lost.Damage(10000,null);
            check(squad.Count==2&&w.MarinesAlive==4&&squad.Losses==1,"A squad casualty frees only its own slot");
            w.Phase=Phase.Intermission;
            check(squad.Hire()&&squad.Count==3&&squad.Members[1].Slot==1&&w.Credits==credits-120,"Intermission recruitment fills the vacant slot at the normal price");
            w.Phase=Phase.Assault;
            w.Player.Self.Damage(10000,null);
            check(!squad.GiveOrder(SquadOrder.Hold)&&!w.TryOpenCommandTerminal(),"A dead player cannot command or recruit");
            Step(w,2);check(squad.Count==3,"The player's death does not delete their squad");w.Player.Respawn();
            PlacePlayer(w,new Vector3(0,0,3));
            var ally=squad.Members[0].Self;ally.transform.position=new Vector3(0,0,8);
            var foe=w.SpawnEnemy(Role.Spitter,0,new Vector3(0,0,14));Physics.SyncTransforms();
            hp=foe.Current;float allyHp=ally.Current;
            Combat.Rifle(w.Player.Self,w.Player.Self.Aim,(foe.Aim-w.Player.Self.Aim).normalized,25,true);
            check(foe.Current<hp&&ally.Current==allyHp,"Player rounds pass through squadmates without damage or obstruction");
            foe.Damage(10000,null);

            var heavy=w.SpawnEnemy(Role.Colossus,0,new Vector3(-8,0,18));Physics.SyncTransforms();
            hp=heavy.Current;Vector3 core=heavy.transform.TransformPoint(heavy.WeakPoint);
            Combat.Rifle(w.Player.Self,w.Player.Self.Aim,(core-w.Player.Self.Aim).normalized,25,true);
            check(Mathf.Abs(hp-heavy.Current-29.7f)<.1f&&w.Player.CriticalTimer>0,"A ray through the exposed core applies armor and the 1.65 weak-point multiplier");
            hp=heavy.Current;Combat.Rifle(w.Player.Self,w.Player.Self.Aim,(heavy.Aim-w.Player.Self.Aim).normalized,25,true);
            check(Mathf.Abs(hp-heavy.Current-18)<.1f,"A body shot uses armor reduction without a false weak-point bonus");
            w.Player.Tick(.3f);w.Player.AimPoint=heavy.Aim;
            int ammo=w.Player.Ammo;bool fired=w.Player.Fire();
            check(fired&&w.Player.Ammo==ammo-1&&w.Player.RecoilPitch>0,"Firing consumes one round and produces recoverable recoil");
            for(int i=0;i<20;i++)w.Player.Tick(.1f);
            check(w.Player.RecoilPitch==0,"Recoil settles after firing stops");
            w.SetPaused(true);ammo=w.Player.Ammo;
            check(!w.Player.Fire()&&w.Player.Ammo==ammo,"Paused firing cannot spend ammunition");w.SetPaused(false);
            heavy.Damage(10000,null);
            var blocked=w.SpawnEnemy(Role.Runner,0,new Vector3(0,0,-17));Physics.SyncTransforms();hp=blocked.Current;
            Combat.Rifle(w.Player.Self,w.Player.Self.Aim,(blocked.Aim-w.Player.Self.Aim).normalized,1000,true);
            check(blocked.Current==hp,"Ignoring friendly units never allows shooting through the HQ");
            bool wall=Combat.Trace(new Vector3(0,1,3),Vector3.back,15,null,out var wallHit);
            check(wall&&wallHit.collider.gameObject.layer==Combat.WorldLayer,"A projectile whose owner is gone still collides with world geometry");
        }
        static void PlacePlayer(GameWorld w,Vector3 p)
        {
            var cc=w.Player.GetComponent<CharacterController>();cc.enabled=false;w.Player.transform.position=p+Vector3.up*.08f;cc.enabled=true;Physics.SyncTransforms();
        }
        static void Step(GameWorld w,float seconds)
        {
            int steps=Mathf.CeilToInt(seconds/.05f);
            for(int i=0;i<steps;i++){w.SimTime+=.05f;Physics.SyncTransforms();w.Squad.Tick(.05f);w.Effects.Tick(.05f);}
        }
        static bool Arrived(SquadDirector s)
        {
            bool arrived=true;
            foreach(var m in s.Members)
            {
                if(m==null){arrived=false;continue;}
                if(Vector3.Distance(m.transform.position,s.Destination(m.Slot))>1.6f)
                {
                    arrived=false;Debug.Log("SQUAD_ROUTE time="+GameWorld.I.SimTime+" slot="+m.Slot+" position="+m.transform.position+" destination="+s.Destination(m.Slot)+" travelled="+m.Travelled);
                }
            }
            return arrived;
        }
        static bool ClearOfBuildings(SquadDirector s)
        {
            foreach(var m in s.Members)if(Physics.CheckCapsule(m.transform.position+Vector3.up*.65f,m.transform.position+Vector3.up*1.5f,.35f,Combat.WorldMask,QueryTriggerInteraction.Ignore))return false;
            return true;
        }
    }
}

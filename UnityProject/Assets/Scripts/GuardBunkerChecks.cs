using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public static class GuardBunkerChecks
    {
        public static void Run(GameWorld w,Action<bool,string> check)
        {
            check(w.Fort.GuardInside==0&&w.Fort.Occupants==0&&BunkerDirector.Capacity==5,"A fresh mission has five seats per bunker and no automatic occupants");
            w.BeginCampaign();w.Phase=Phase.Intermission;w.IntermissionLeft=1000;w.Credits=10000;
            CheckCommand.Enter(w);bool built=w.Construction.Build(0)&&w.Construction.Build(1);CheckCommand.Leave(w);
            for(int i=0;i<445;i++){w.SimTime+=.05f;w.Construction.Tick(.05f);}
            check(built&&w.Construction.ReserveOperational&&w.Construction.Sites[0].State==SiteState.Ready,"Real construction creates both a western bunker and enough ordinary guard slots for occupancy checks");
            FillPosts(w);w.Phase=Phase.Assault;
            var fort=w.Fort;Health east=w.Bunker,west=w.Construction.Sites[0].Structure;
            var eastGuards=SelectGuards(w,1,5);var westGuards=SelectGuards(w,0,5);
            check(eastGuards.Count==5&&westGuards.Count==5,"Both lanes have five real registered ordinary guards available");
            if(eastGuards.Count<5||westGuards.Count<5)return;
            CheckSearchBounds(w,eastGuards[0],westGuards[0],east,check);
            bool doors=true,clear=true;int arrivals=0;
            StepGuards(w,eastGuards,30,ref doors,ref clear,ref arrivals);
            Debug.Log("GUARD_INITIAL arrivals="+arrivals+" doors="+doors+" clear="+clear+" inside="+fort.GuardInsideFor(east)+" occupants="+fort.OccupantsFor(east));
            LogGuards(w,eastGuards,"initial");
            check(arrivals==5&&doors&&clear&&fort.GuardInsideFor(east)==5&&fort.OccupantsFor(east)==5,
                "Five same-lane guards physically navigate outside walls to the rear door before occupying its five seats");
            bool walked=true;foreach(var guard in eastGuards)walked&=guard.Brain.Travelled>1&&fort.GuardBunker(guard)==east;
            check(walked,"Every admitted guard accumulates actual walking and remains bound to the nearby east bunker");
            CheckGuardWeapon(w,eastGuards[0],east,check);

            FortificationChecks.PlacePlayer(w,fort.EntranceFor(east));
            int originalGuards=fort.GuardInsideFor(east);
            bool entered=fort.TryTogglePlayer();Health yielded=null;int outside=0;
            foreach(var guard in eastGuards)if(!fort.IsGuardInside(guard)){yielded=guard;outside++;}
            check(originalGuards==5&&entered&&outside==1&&fort.PlayerInside&&fort.GuardInsideFor(east)==4&&fort.OccupantsFor(east)==5,
                "One player entering five occupied guard seats yields exactly one guard without clearing the other four");
            fort.ExitPlayer();
            if(yielded!=null)
            {
                FortificationChecks.PlacePlayer(w,yielded.transform.position+Vector3.back*1.3f);
                int count=w.MarinesAlive,credits=w.Credits;var oldBrain=yielded.Brain;
                bool recruited=w.Squad.RecruitGuard(yielded);
                check(recruited&&yielded.Role==Role.Squad&&yielded.Brain==null&&!yielded.IsSheltered&&
                    !fort.IsGuardInside(yielded)&&fort.GuardBunker(yielded)==null&&w.MarinesAlive==count-1&&w.Credits==credits,
                    "A visibly yielded guard can be recruited near the rear door while preserving its health object and releasing its original guard post");
                check(!fort.TickGuard(oldBrain,.1f),"The old guard brain cannot reacquire a bunker after that same unit becomes a squad member");
            }
            check(SquadChecks.FillSquad(w)&&w.Squad.Count==4,"Two mercenary contracts and two recruited guards form the real four-person squad");
            FillPosts(w);eastGuards=SelectGuards(w,1,5);StepGuards(w,eastGuards,30);
            if(fort.GuardInsideFor(east)!=5)LogGuards(w,eastGuards,"refill");
            check(fort.GuardInsideFor(east)==5,"Ordinary production can refill the freed post and its now-available bunker seat");

            // Leaving the player seat creates a real vacancy while the squad is still en route.
            FortificationChecks.PlacePlayer(w,fort.EntranceFor(east));fort.TryTogglePlayer();
            bool ordered=fort.OrderSquadInside();fort.ExitPlayer();StepGuards(w,eastGuards,18);
            check(ordered&&fort.SquadInsideFor(east)==0&&fort.GuardInsideFor(east)==4&&fort.OccupantsFor(east)==4,
                "A vacant player seat stays reserved for the incoming squad even after yielded guards finish their cooldown");
            w.Squad.GiveOrder(SquadOrder.Follow);StepGuards(w,eastGuards,25);
            if(fort.GuardInsideFor(east)!=5)LogGuards(w,eastGuards,"cancel_order");
            check(fort.GuardInsideFor(east)==5,"Cancelling the pending squad order makes the unused seats available to guards again");
            FortificationChecks.PlacePlayer(w,fort.EntranceFor(east));ordered=fort.OrderSquadInside();
            check(ordered&&fort.GuardInsideFor(east)==5&&fort.SquadInsideFor(east)==0,"Issuing a squad order keeps all five guards stationed until squad members actually arrive");
            int enteredSquad=0,yieldedGuards=0;bool oneAtATime=true,capacity=true;
            for(int step=0;step<600&&fort.SquadInsideFor(east)<4;step++)
            {
                w.SimTime+=.05f;Physics.SyncTransforms();fort.Tick(.05f);
                foreach(var member in w.Squad.Members)
                {
                    if(member==null)continue;
                    int beforeSquad=fort.SquadInsideFor(east),beforeGuards=fort.GuardInsideFor(east);
                    member.Tick(.05f);
                    int added=fort.SquadInsideFor(east)-beforeSquad,removed=beforeGuards-fort.GuardInsideFor(east);
                    if(added>0){enteredSquad+=added;yieldedGuards+=removed;oneAtATime&=added==1&&removed==1;}
                    else oneAtATime&=removed==0;
                    capacity&=fort.OccupantsFor(east)<=5;
                }
                foreach(var guard in eastGuards)if(guard!=null&&guard.Alive&&guard.Role==Role.Marine)guard.Brain.Tick(.05f);
                w.Effects.Tick(.05f);
            }
            check(enteredSquad==4&&yieldedGuards==4&&oneAtATime&&capacity&&fort.SquadInsideFor(east)==4&&fort.GuardInsideFor(east)==1&&fort.OccupantsFor(east)==5,
                "Four squad members arriving through navigation yield four guards one at a time and never exceed five occupants");
            fort.TryTogglePlayer();StepGuards(w,eastGuards,20);
            check(fort.PlayerInside&&fort.SquadInsideFor(east)==4&&fort.GuardInsideFor(east)==0&&fort.OccupantsFor(east)==5,
                "The player takes the final ordinary-guard seat beside the four-person squad and guards cannot take priority seats back");
            bool returned=true;
            foreach(var guard in eastGuards)if(guard!=null&&guard.Role==Role.Marine)
            {
                Vector3 station=guard.Brain.Station;station.y=GameWorld.GroundHeight(station.x,station.z);
                if(!w.Squad.Navigation.Fits(station,GuardNavigation.Radius))station=w.Squad.Navigation.Resolve(station);
                returned&=!guard.IsSheltered&&Vector3.Distance(guard.transform.position,station)<1.3f;
            }
            if(!returned)LogGuards(w,eastGuards,"return_to_post");
            check(returned,"Yielded guards restore vulnerability and walk back to their original resolved defense posts");

            fort.ExitPlayer();w.Squad.GiveOrder(SquadOrder.Follow);StepGuards(w,eastGuards,25);
            westGuards=SelectGuards(w,0,5);StepGuards(w,westGuards,30);
            if(fort.GuardInsideFor(east)!=5||fort.GuardInsideFor(west)!=5){LogGuards(w,eastGuards,"dual_east");LogGuards(w,westGuards,"dual_west");}
            check(fort.GuardInsideFor(east)==5&&fort.GuardInsideFor(west)==5&&fort.Occupants==10,
                "The two bunkers independently accommodate five ordinary guards each");
            west.Damage(10000,null);
            bool released=true;foreach(var guard in westGuards)released&=!guard.IsSheltered&&!fort.IsGuardInside(guard)&&fort.GuardBunker(guard)==null;
            check(released&&fort.GuardInsideFor(west)==0&&fort.GuardInsideFor(east)==5,
                "Destroying the west bunker releases only its own guards while every east-bunker guard remains protected");
            StepGuards(w,westGuards,18);w.Phase=Phase.Intermission;FortificationChecks.PlacePlayer(w,new Vector3(0,0,3));
            CheckCommand.Enter(w);bool rebuilding=w.Construction.Build(0);CheckCommand.Leave(w);
            for(int i=0;i<365;i++){w.SimTime+=.05f;w.Construction.Tick(.05f);}
            Health replacement=w.Construction.Sites[0].Structure;w.Phase=Phase.Assault;StepGuards(w,westGuards,30);
            if(fort.GuardInsideFor(replacement)!=5)LogGuards(w,westGuards,"rebuilt_west");
            check(rebuilding&&replacement!=west&&replacement.Alive&&fort.GuardInsideFor(west)==0&&fort.GuardInsideFor(replacement)==5&&fort.GuardInsideFor(east)==5,
                "After real reconstruction, guards can walk into the new bunker identity without restoring old bindings or disturbing the other bunker");
        }
        static void CheckSearchBounds(GameWorld w,Health eastGuard,Health westGuard,Health east,Action<bool,string> check)
        {
            Vector3 position=eastGuard.transform.position,station=eastGuard.Brain.Station;
            eastGuard.transform.position=east.transform.position+Vector3.back*6;eastGuard.Brain.Station=east.transform.position+Vector3.forward*18;
            bool handled=w.Fort.TickGuard(eastGuard.Brain,1.1f);
            check(!handled&&w.Fort.GuardBunker(eastGuard)==null,"A nearby guard whose assigned post is over sixteen meters from the bunker cannot leave that post for it");
            eastGuard.transform.position=new Vector3(0,0,3);eastGuard.Brain.Station=east.transform.position+Vector3.forward*10;
            handled=w.Fort.TickGuard(eastGuard.Brain,1.1f);
            check(!handled&&w.Fort.GuardBunker(eastGuard)==null,"A guard currently farther than sixteen meters cannot remotely claim a bunker even when its post is nearby");
            eastGuard.transform.position=position;eastGuard.Brain.Station=station;
            position=westGuard.transform.position;station=westGuard.Brain.Station;
            westGuard.transform.position=east.transform.position+Vector3.back*6;westGuard.Brain.Station=westGuard.transform.position;
            handled=w.Fort.TickGuard(westGuard.Brain,1.1f);
            check(!handled&&w.Fort.GuardBunker(westGuard)==null,"A western-lane guard refuses the nearby east bunker instead of crossing sides");
            westGuard.transform.position=position;westGuard.Brain.Station=station;Physics.SyncTransforms();
        }
        static void CheckGuardWeapon(GameWorld w,Health guard,Health bunker,Action<bool,string> check)
        {
            Health foe=w.SpawnEnemy(Role.Spitter,1,bunker.transform.position+Vector3.forward*14);foe.Current=foe.Maximum=5000;foe.WeakRadius=0;
            float hp=guard.Current;guard.Damage(100,foe);guard.Damage(100,null);
            bool hidden=true;foreach(var r in guard.GetComponentsInChildren<Renderer>())hidden&=!r.enabled;
            foreach(var c in guard.GetComponentsInChildren<Collider>())hidden&=!c.enabled;
            check(guard.Current==hp&&hidden&&!Combat.Visible(foe,guard)&&w.FindTarget(foe,100)!=guard,
                "A garrisoned ordinary guard is hidden, has no personal collision, resists damage and cannot be acquired by enemies");
            Physics.SyncTransforms();hp=foe.Current;w.Fort.TickGuard(guard.Brain,.8f);
            check(Mathf.Abs(hp-foe.Current-(10+3*w.DefenseLevel))<.01f,"A stationed guard fires an actual rifle round for its ordinary-guard damage from its own aperture");
            Vector3 muzzle=w.Fort.GuardMuzzle(guard);var wall=new GameObject("Guard aperture test wall");wall.layer=Combat.WorldLayer;
            wall.transform.position=new Vector3(bunker.transform.position.x,2,muzzle.z+4);wall.AddComponent<BoxCollider>().size=new Vector3(7,4,.8f);
            Physics.SyncTransforms();hp=foe.Current;w.Fort.TickGuard(guard.Brain,.8f);
            check(foe.Current==hp,"A solid wall in front of a guard aperture blocks its real guard rifle damage");
            wall.SetActive(false);UnityEngine.Object.Destroy(wall);foe.transform.position=bunker.transform.position+Vector3.back*8;Physics.SyncTransforms();
            hp=foe.Current;w.Fort.TickGuard(guard.Brain,.8f);check(foe.Current==hp,"A stationed ordinary guard cannot fire through the rear of its bunker");foe.Damage(10000,null);
        }
        static void FillPosts(GameWorld w){for(int slot=0;slot<w.MarineCapacity;slot++)w.SpawnMarine(slot,false);}
        static List<Health> SelectGuards(GameWorld w,int lane,int maximum)
        {
            var result=new List<Health>();
            Health bunker=w.Bunkers.Find(h=>h!=null&&h.Alive&&(lane==0?h.transform.position.x<0:h.transform.position.x>0));
            foreach(var h in w.Entities)if(EligibleGuard(h,lane,bunker)&&w.Fort.IsGuardInside(h)&&result.Count<maximum)result.Add(h);
            foreach(var h in w.Entities)if(EligibleGuard(h,lane,bunker)&&!result.Contains(h)&&result.Count<maximum)result.Add(h);
            return result;
        }
        static bool EligibleGuard(Health guard,int lane,Health bunker)=>guard!=null&&guard.Alive&&guard.Role==Role.Marine&&guard.Lane==lane&&guard.Brain!=null&&bunker!=null&&Vector3.Distance(guard.Brain.Station,bunker.transform.position)<=16;
        static void LogGuards(GameWorld w,List<Health> guards,string stage)
        {
            foreach(var guard in guards)if(guard!=null&&guard.Brain!=null)
            {
                Health bunker=w.Fort.GuardBunker(guard);
                Debug.Log("GUARD_ROUTE stage="+stage+" slot="+guard.Slot+" position="+guard.transform.position.ToString("F3")+
                    " station="+guard.Brain.Station.ToString("F3")+" door="+(bunker!=null?w.Fort.EntranceFor(bunker).ToString("F3"):"none")+
                    " moved="+guard.Brain.Travelled.ToString("F3")+" fits="+w.Squad.Navigation.Fits(guard.transform.position,.42f)+" "+w.Fort.GuardDiagnostic(guard));
            }
        }
        static void StepGuards(GameWorld w,List<Health> guards,float seconds)
        {bool doors=true,clear=true;int arrivals=0;StepGuards(w,guards,seconds,ref doors,ref clear,ref arrivals);}
        static void StepGuards(GameWorld w,List<Health> guards,float seconds,ref bool doors,ref bool clear,ref int arrivals)
        {
            for(int step=0;step<Mathf.CeilToInt(seconds/.05f);step++)
            {
                w.SimTime+=.05f;Physics.SyncTransforms();w.Fort.Tick(.05f);
                foreach(var guard in guards)
                {
                    if(guard==null||!guard.Alive||guard.Role!=Role.Marine||guard.Brain==null)continue;
                    bool wasInside=w.Fort.IsGuardInside(guard);Vector3 before=guard.transform.position;
                    guard.Brain.Tick(.05f);
                    if(!wasInside&&w.Fort.IsGuardInside(guard))
                    {arrivals++;doors&=Vector3.Distance(before,w.Fort.EntranceFor(w.Fort.GuardBunker(guard)))<=.86f;}
                    if(!w.Fort.IsGuardInside(guard))clear&=w.Squad.Navigation.Fits(guard.transform.position,.42f);
                    foreach(var bunker in w.Bunkers)clear&=w.Fort.OccupantsFor(bunker)<=BunkerDirector.Capacity;
                }
                w.Effects.Tick(.05f);
            }
        }
    }
}

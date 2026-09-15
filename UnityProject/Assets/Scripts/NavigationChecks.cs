using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public static class NavigationChecks
    {
        public static void Run(GameWorld w,Action<bool,string> check)
        {
            w.BeginCampaign();w.Phase=Phase.Intermission;w.IntermissionLeft=10000;w.Credits=10000;
            check(Mathf.Abs(w.Bunker.transform.position.x-20.5f)<.01f,"The east bunker leaves the infantry approach lane open at x = 20.5");
            check(w.Construction.Build(1),"The navigation scenario constructs a real reinforcement barracks");
            for(int i=0;i<445;i++){w.SimTime+=.05f;w.Construction.Tick(.05f);Physics.SyncTransforms();}
            check(w.Construction.Sites[1].State==SiteState.Ready&&w.MarineCapacity==12,"The reinforcement barracks unlocks all 12 real garrison destinations");
            CheckAllPosts(w,w.Barracks,"original",check);
            CheckAllPosts(w,w.Construction.Sites[1].Structure,"reinforcement",check);
            CheckConstructionReplan(w,check);
            CheckRamps(w,check);
        }

        static void CheckAllPosts(GameWorld w,Health producer,string name,Action<bool,string> check)
        {
            foreach(var h in w.Entities.ToArray())if(h!=null&&h.Alive&&h.Role==Role.Marine)
            {w.Fort.ReleaseGuard(h);h.Damage(10000,null);}
            var guards=new Health[12];var starts=new Vector3[12];var clear=new bool[12];
            bool genuine=true;
            for(int slot=0;slot<guards.Length;slot++)
            {
                // Use the production factory with its actual barracks exit and assigned post.
                guards[slot]=w.SpawnMarine(slot,true,producer);clear[slot]=true;
                Vector3 expected=producer.transform.TransformPoint(new Vector3((slot%3-1)*.5f,0,5.2f));
                genuine&=guards[slot]!=null&&Vector3.Distance(guards[slot].transform.position,expected)<.01f&&guards[slot].Brain.Station==GameWorld.DefensePosts[slot];
                starts[slot]=guards[slot]!=null?guards[slot].transform.position:expected;
            }
            check(genuine&&w.MarinesAlive==12,"All 12 "+name+"-barracks guards are real units born at that building's exit with their assigned posts");
            check(w.EnemiesAlive==0&&w.Fort.GuardInside==0,"The "+name+"-barracks route scenario isolates navigation from enemy combat and automatic bunker assignments");
            for(int step=0;step<800;step++)
            {
                w.SimTime+=.05f;Physics.SyncTransforms();
                for(int slot=0;slot<guards.Length;slot++)
                {
                    var guard=guards[slot];if(guard==null){clear[slot]=false;continue;}
                    // Deliberately bypass TickGuard: this scenario requires arrival at the original post.
                    guard.Brain.NavigateTo(guard.Brain.Station,.05f);
                    clear[slot]&=BodyClear(guard.transform.position);
                }
                Physics.SyncTransforms();w.Effects.Tick(.05f);
            }
            for(int slot=0;slot<guards.Length;slot++)
            {
                var guard=guards[slot];Vector3 post=GameWorld.DefensePosts[slot];
                bool arrived=guard!=null&&guard.Alive&&Vector3.Distance(guard.transform.position,post)<=.8f&&
                    guard.Brain.Travelled>=Vector3.Distance(starts[slot],post)-.8f;
                check(arrived&&clear[slot],"The "+name+"-barracks guard at post "+slot+" walks its complete route within 40 seconds, stays clear of buildings and finishes within 0.8 metres");
                if(!arrived||!clear[slot])Debug.Log("GUARD_NAV source="+name+" slot="+slot+" start="+starts[slot]+" post="+post+" final="+
                    (guard!=null?guard.transform.position:Vector3.zero)+" travelled="+(guard!=null?guard.Brain.Travelled:0)+" clear="+clear[slot]);
            }
        }

        static void CheckConstructionReplan(GameWorld w,Action<bool,string> check)
        {
            var site=w.Construction.Sites[0];Health guard=w.Entities.Find(h=>h!=null&&h.Alive&&h.Role==Role.Marine&&h.Slot==0);
            Vector3 start=site.Position+Vector3.back*8,goal=site.Position+Vector3.forward*14;
            for(int i=0;i<400;i++){Physics.SyncTransforms();guard.Brain.NavigateTo(start,.05f);}
            check(Vector3.Distance(guard.transform.position,start)<=.8f,"The replanning guard physically walks to the start of the future construction crossing");
            var previousNavigation=w.Squad.Navigation;
            check(previousNavigation.Clear(start,goal,.42f),"The guard's original route crosses a currently empty construction footprint");
            guard.Brain.NavigateTo(goal,.05f);float travelled=guard.Brain.Travelled;
            check(w.Construction.Build(0)&&w.Squad.Navigation!=previousNavigation,"Starting construction replaces the shared navigation grid after the guard has already planned its route");
            bool clear=true;
            for(int i=0;i<700;i++)
            {
                Physics.SyncTransforms();guard.Brain.NavigateTo(goal,.05f);clear&=BodyClear(guard.transform.position);
            }
            check(clear&&Vector3.Distance(guard.transform.position,goal)<=.8f&&guard.Brain.Travelled>travelled+10,
                "An already-moving guard discards the old grid route, walks around the new foundation and reaches its destination without clipping or stalling");
            if(!clear||Vector3.Distance(guard.transform.position,goal)>.8f||guard.Brain.Travelled<=travelled+10)
                Debug.Log("GUARD_REPLAN_NAV start="+start+" goal="+goal+" final="+guard.transform.position+" walked="+(guard.Brain.Travelled-travelled)+" clear="+clear);
        }

        static void CheckRamps(GameWorld w,Action<bool,string> check)
        {
            var controller=w.Player.GetComponent<CharacterController>();
            check(!w.Squad.Navigation.Clear(new Vector3(0,0,24),new Vector3(0,-4,31),.42f)&&
                !GroundSurface.Traversable(new Vector3(0,0,26),new Vector3(0,-4,26.35f)),
                "Navigation rejects the central cliff and fortification instead of treating the canyon as a continuous flat floor");
            var route=new List<Vector3>();Vector3 basin=new Vector3(0,-4,31),deck=new Vector3(0,0,20);
            bool routed=w.Squad.Navigation.Plan(basin,deck,route,.42f);Vector3 previous=basin;
            foreach(Vector3 point in route){routed&=w.Squad.Navigation.Clear(previous,point,.42f);previous=point;}
            check(routed&&route.Count>1,"A canyon-to-base route uses a continuous walkable slope around the cliff");
            for(int lane=0;lane<2;lane++)
            {
                float x=lane==0?-GroundSurface.LaneCenter:GroundSurface.LaneCenter;
                bool landing=Physics.Raycast(new Vector3(x,2,25.95f),Vector3.down,out var landingHit,4,Combat.WorldMask,QueryTriggerInteraction.Ignore);
                check(landing&&Mathf.Abs(landingHit.point.y)<.01f,"The "+(lane==0?"west":"east")+" ramp landing physically closes the deck-to-ramp seam at z = 25.95");
                for(int approach=0;approach<3;approach++)
                {
                    string label=(lane==0?"west":"east")+" "+(approach==0?"front":approach==1?"left side":"right side");
                    Vector3 start=approach==0?new Vector3(x,-4,44):new Vector3(x+(approach==1?-12:12),-4,31);
                    Vector3 join=new Vector3(x,GroundSurface.Height(x,31),31),top=new Vector3(x,0,23);
                    FortificationChecks.PlacePlayer(w,start);
                    float vertical=-2;bool supported=true,matched=true;float walked=0;
                    // Position only the initial fixture. Both ascent and descent use the real controller.
                    bool up=WalkPlayer(w,controller,join,ref vertical,ref supported,ref matched,ref walked)&&
                        WalkPlayer(w,controller,top,ref vertical,ref supported,ref matched,ref walked);
                    check(up&&Mathf.Abs(w.Player.transform.position.y)<.25f&&walked>8,
                        "The player's CharacterController walks up the "+label+" ramp and through its gate onto the base");
                    bool down=WalkPlayer(w,controller,join,ref vertical,ref supported,ref matched,ref walked)&&
                        WalkPlayer(w,controller,start,ref vertical,ref supported,ref matched,ref walked);
                    check(down&&Mathf.Abs(w.Player.transform.position.y+4)<.25f,
                        "The player's CharacterController walks back down the "+label+" ramp to the canyon floor");
                    check(supported&&matched,"The complete "+label+" traversal remains supported by real collision geometry matching GroundSurface.Height");
                    if(!up||!down||!supported||!matched)Debug.Log("RAMP_NAV route="+label+" final="+w.Player.transform.position+" up="+up+" down="+down+" grounded="+supported+" height="+matched+" walked="+walked);
                }
            }
        }

        static bool WalkPlayer(GameWorld w,CharacterController controller,Vector3 goal,ref float vertical,ref bool supported,ref bool matched,ref float walked)
        {
            const float dt=.02f;
            for(int step=0;step<1000;step++)
            {
                Vector3 before=w.Player.transform.position,direction=goal-before;direction.y=0;
                if(direction.magnitude<=.3f)return true;
                if(controller.isGrounded&&vertical<0)vertical=-2;
                vertical-=24*dt;
                controller.Move(direction.normalized*Mathf.Min(direction.magnitude,5.8f*dt)+Vector3.up*vertical*dt);
                Physics.SyncTransforms();Vector3 position=w.Player.transform.position;walked+=Vector3.Distance(before,position);
                if(step<15)continue;
                bool floor=Physics.Raycast(position+Vector3.up*.4f,Vector3.down,out var hit,1.1f,Combat.WorldMask,QueryTriggerInteraction.Ignore);
                supported&=floor&&hit.normal.y>.55f&&Mathf.Abs(position.y-hit.point.y)<.35f;
                if(floor)
                {
                    float expected=GroundSurface.Height(position.x,position.z);
                    bool heightMatches=Mathf.Abs(hit.point.y-expected)<.16f;
                    if(matched&&!heightMatches)Debug.Log("RAMP_HEIGHT_MISMATCH player="+position.ToString("F4")+" hit="+hit.point.ToString("F4")+
                        " expected="+expected.ToString("F4")+" collider="+hit.collider.name+" normal="+hit.normal.ToString("F4"));
                    matched&=heightMatches;
                }
            }
            return false;
        }
        static bool BodyClear(Vector3 point)=>!Physics.CheckCapsule(point+Vector3.up*.65f,point+Vector3.up*1.5f,.42f,Combat.WorldMask,QueryTriggerInteraction.Ignore);
    }
}

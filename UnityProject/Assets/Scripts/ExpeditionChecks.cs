using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Uses the real expedition controller, physics, navigation and rifle traces.
    // Hostile controllers are deliberately not advanced: this verifies behavior,
    // while campaign runs separately evaluate combat balance.
    public static class ExpeditionChecks
    {
        const float StepSize=.05f;
        public static void Run(GameWorld w,Action<bool,string> check)
        {
            var expeditions=w.Expeditions;
            check(expeditions!=null&&expeditions.AliveCount==0&&expeditions.Produced==0&&expeditions.ProductionLeft==12,
                "A fresh mission starts with an independent empty expedition roster and an twelve-second production timer");
            check(ExpeditionDirector.FormationSize==6&&ExpeditionDirector.MaxUnits==24&&ExpeditionDirector.MaximumHealth==195,
                "Expeditions form six-member squads with 195 health per marine and a separate 24-soldier limit");
            if(expeditions==null)return;
            w.BeginCampaign();w.Phase=Phase.Intermission;w.IntermissionLeft=10000;w.Player.Bot=false;w.Player.ReleaseControl();
            foreach(var h in w.Entities)if(h!=null)h.Brain=null;
            while(w.MarinesAlive<w.MarineCapacity)w.SpawnMarine(deploy:false);
            int guards=w.MarinesAlive,capacity=w.MarineCapacity,squad=w.Squad.Count;
            Step(w,11.8f);
            check(expeditions.AliveCount==0,"No expedition recruit exists before twelve active production seconds");
            Step(w,.3f);
            Health first=expeditions.Members.Count>0?expeditions.Members[0]:null;
            check(first!=null&&first.Alive&&first.Role==Role.AssaultMarine&&first.Faction==Faction.Defense&&first.Maximum==195&&first.Brain==null,
                "An operational barracks creates one physical assault marine with health, a collider and its own controller");
            check(first!=null&&first.GetComponent<CapsuleCollider>()!=null&&FlatDistance(first.transform.position,w.Barracks.transform.position)<7,
                "The recruit appears at a real barracks exit instead of teleporting directly into the formation");
            check(w.MarinesAlive==guards&&w.MarineCapacity==capacity&&w.Squad.Count==squad&&w.Tanks.Tank==null,
                "A full guard roster does not block expedition recruitment or consume guard, squad or tank slots");
            if(first==null)return;

            float left=expeditions.ProductionLeft;Vector3 pausedPosition=first.transform.position;
            w.SetPaused(true);expeditions.Tick(30);w.Tick(30);
            check(expeditions.ProductionLeft==left&&first.transform.position==pausedPosition&&expeditions.Shots==0,
                "Pause freezes expedition production, movement and gunfire even when the controller is called directly");
            w.SetPaused(false);check(CheckCommand.Enter(w),"The actual command terminal opens for expedition pause verification");
            left=expeditions.ProductionLeft;expeditions.Tick(30);
            check(expeditions.ProductionLeft==left&&first.transform.position==pausedPosition,
                "Opening the command terminal also freezes the independent expedition controller");
            CheckCommand.Leave(w);

            // Before each departure capture the positions that the controller must
            // inspect, rather than assuming spawned soldiers have already arrived.
            bool everyArrival=true,collisionFree=true,noTeleport=true;int departure=0;
            Dictionary<Health,Vector3> before=new Dictionary<Health,Vector3>();
            Dictionary<Health,float> remaining=new Dictionary<Health,float>();
            for(int step=0;step<1800&&expeditions.Departures==0;step++)
            {
                before.Clear();remaining.Clear();
                foreach(var h in expeditions.Members)if(h!=null&&h.Alive)
                {
                    before[h]=h.transform.position;
                    if(expeditions.StateOf(h)==ExpeditionState.Rally)remaining[h]=FlatDistance(h.transform.position,expeditions.RallyPointOf(h));
                }
                int previous=expeditions.Departures;Step(w,StepSize);
                foreach(var h in expeditions.Members)if(h!=null&&h.Alive)
                {
                    collisionFree&=w.Squad.Navigation.Fits(h.transform.position,.42f);
                    if(before.TryGetValue(h,out var old))noTeleport&=Vector3.Distance(old,h.transform.position)<=ExpeditionDirector.Speed*StepSize+.002f;
                }
                if(expeditions.Departures>previous)
                {
                    departure=expeditions.Departures;int launched=0;
                    foreach(var h in expeditions.Members)if(h!=null&&h.Alive&&expeditions.StateOf(h)==ExpeditionState.Attacking)
                    {
                        launched++;everyArrival&=remaining.TryGetValue(h,out var gap)&&gap<=ExpeditionDirector.ArrivalDistance+.001f;
                    }
                    everyArrival&=launched==6;
                }
            }
            check(departure==1&&expeditions.AttackingCount==6&&everyArrival,
                "The first expedition departs only after six different living soldiers physically reach their marked rally positions");
            check(collisionFree&&noTeleport&&expeditions.TravelledOf(first)>10,
                "Rally movement uses collision-safe walking at the advertised speed and records actual distance without teleporting");
            check(w.MarinesAlive==guards&&w.Squad.Count==squad&&expeditions.AliveCount>=6,
                "Assembling and departing six assault soldiers leaves every original guard and squad slot unchanged");

            Health objective=w.EnemyDefenses.NearestAlivePortal(ExpeditionDirector.RallyCenter);
            float hp=objective!=null?objective.Current:0;int shots=expeditions.Shots;float maximumZ=0;
            for(int step=0;step<2800&&objective!=null&&objective.Alive&&objective.Current>=hp;step++)
            {
                Step(w,StepSize);
                foreach(var h in expeditions.Members)if(h!=null&&h.Alive)maximumZ=Mathf.Max(maximumZ,h.transform.position.z);
            }
            check(objective!=null&&objective.Current<hp&&expeditions.Shots>shots&&maximumZ>50,
                "A real expedition exits through a ramp, crosses the battlefield and damages a living portal with actual rifle traces");
            if(objective!=null&&objective.Alive)objective.Damage(1000000,null);
            Health expected=w.EnemyDefenses.NearestAlivePortal(first.transform.position);Step(w,StepSize);
            check(expected!=null&&expeditions.BaseIdOf(first)==expected.BaseId,
                "Destroying the current portal immediately retargets survivors toward the nearest living portal base");

            // Keep the remaining real defenders durable so the production-cap
            // check cannot end by exhausting all enemy objectives first.
            foreach(var h in w.Entities)if(h!=null&&h.Alive&&h.Faction==Faction.Invaders)h.Current=h.Maximum=1000000;
            for(int step=0;step<5000&&expeditions.AliveCount<ExpeditionDirector.MaxUnits;step++)Step(w,StepSize);
            int atCap=expeditions.Produced;Step(w,12);
            check(expeditions.AliveCount==24&&expeditions.Produced==atCap&&w.MarinesAlive==guards,
                "Twenty-four live assault soldiers stop further recruitment while the full independent guard roster remains intact");
            var lost=expeditions.Members[expeditions.Members.Count-1];lost.Damage(100000,null);Step(w,StepSize);
            check(expeditions.AliveCount==23&&w.MarinesAlive==guards&&w.Squad.Count==squad,
                "Losing an assault marine frees only its expedition slot and cannot decrement the guard or squad counts");
            float retained=expeditions.ProductionLeft;
            w.Barracks.Damage(100000,null);Step(w,10);
            check(!expeditions.Operational&&expeditions.ProductionLeft==retained&&expeditions.AliveCount==23,
                "Losing every barracks freezes expedition replacement progress without removing deployed survivors");

            // Complete the actual reserve-barracks construction path before
            // checking its faster production cadence.
            w.Credits=10000;CheckCommand.Enter(w);
            bool ordered=w.Construction.Build(1);CheckCommand.Leave(w);
            for(int i=0;i<450;i++){w.SimTime+=StepSize;Physics.SyncTransforms();w.Construction.Tick(StepSize);w.Effects.Tick(StepSize);}
            check(ordered&&w.Construction.ReserveOperational,"A real reserve barracks finishes construction and restores an expedition production source");
            Step(w,retained+.15f);
            check(expeditions.AliveCount==24&&Mathf.Abs(expeditions.ProductionLeft-9)<.06f,
                "The reserve barracks replaces a lost assault soldier and starts a nine-second recruitment interval");

            foreach(var b in w.EnemyDefenses.Bases)if(b.Portal!=null&&b.Portal.Alive)b.Portal.Damage(10000000,null);
            int produced=expeditions.Produced;Step(w,StepSize);
            check(expeditions.RallyCount==0&&expeditions.AttackingCount==0&&expeditions.ReserveCount==expeditions.AliveCount,
                "After the last portal falls all survivors become reserves instead of accumulating an impossible oversized rally group");
            foreach(var h in w.Entities)if(h!=null&&h.Alive&&h.Faction==Faction.Invaders)h.IsSheltered=true;
            Step(w,120);
            bool home=true;foreach(var h in expeditions.Members)if(h!=null&&h.Alive)home&=FlatDistance(h.transform.position,expeditions.RallyPointOf(h))<=.9f;
            check(home&&expeditions.Produced==produced,"Surviving expeditions physically return to distinct central reserve positions and no new soldiers are produced");
        }

        static void Step(GameWorld world,float seconds)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds/StepSize);i++)
            {
                float dt=Mathf.Min(StepSize,seconds-i*StepSize);if(dt<=0)break;
                world.SimTime+=dt;Physics.SyncTransforms();world.Expeditions.Tick(dt);world.Effects.Tick(dt);
            }
        }
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
    }
}

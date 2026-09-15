using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Exercises the actual scheduler and actors; only unrelated combat is suspended.
    public static class AssaultChecks
    {
        const float StepSize=.05f;

        sealed class Audit
        {
            public readonly GameWorld World;
            public readonly Dictionary<Health,ActorBrain> Brains=new Dictionary<Health,ActorBrain>();
            public readonly Dictionary<int,float> Born=new Dictionary<int,float>();
            public readonly Dictionary<int,List<Role>> Roles=new Dictionary<int,List<Role>>();
            public readonly Dictionary<int,List<float>> Times=new Dictionary<int,List<float>>();
            public readonly HashSet<int> Warned=new HashSet<int>();
            public readonly int[] SourceStart=new int[6];
            public int Count;
            public bool SourcesCorrect=true,WarningsCorrect=true,BindingsCorrect=true,CounterCorrect=true,DualLane;
            public AssaultGroup Moving;
            public Health Held;

            public Audit(GameWorld world)
            {
                World=world;Array.Copy(world.SpawnPointUses,SourceStart,6);Observe();
            }

            public void Observe()
            {
                var assault=World.Assault;
                foreach(var batch in assault.Schedule)
                {
                    float until=batch.At-assault.Elapsed;
                    if(until>=0&&until<3.9f)
                    {
                        bool shown=(assault.WarningLaneMask&(1<<batch.Lane))!=0;
                        WarningsCorrect&=shown;
                        if(shown)Warned.Add(batch.Index);
                    }
                }
                WarningsCorrect&=(assault.WarningLaneMask&~3)==0&&assault.WarningCount>=0;
                foreach(var group in assault.Groups)
                {
                    foreach(var unit in group.Members)
                    {
                        if(unit==null||!unit.Alive)continue;
                        int id=unit.GetInstanceID();
                        if(Born.ContainsKey(id))continue;
                        Born.Add(id,assault.Elapsed);Count++;
                        if(!Roles.ContainsKey(group.Batch.Index))
                        {
                            Roles.Add(group.Batch.Index,new List<Role>());
                            Times.Add(group.Batch.Index,new List<float>());
                            WarningsCorrect&=Warned.Contains(group.Batch.Index);
                        }
                        Roles[group.Batch.Index].Add(unit.Role);
                        Times[group.Batch.Index].Add(assault.Elapsed);
                        SourcesCorrect&=unit.Lane==group.Batch.Lane&&group.Batch.SourceSite%2==unit.Lane&&
                            Vector3.Distance(unit.transform.position,GameWorld.EnemySpawnPoints[group.Batch.SourceSite])<2.3f;
                        BindingsCorrect&=unit.Brain!=null&&unit.Brain.Group==group;
                    }
                }
                CounterCorrect&=World.SpawnedThisWave==Count&&assault.Spawned==Count&&
                    World.RemainingToSpawn+Count==WavePlan.Counts[World.Wave-1];
                DualLane|=World.LaneEnemies(0)>0&&World.LaneEnemies(1)>0;
            }

            public void Step(float seconds)
            {
                int steps=Mathf.CeilToInt(seconds/StepSize);
                for(int i=0;i<steps&&!World.Ended;i++)
                {
                    float dt=Mathf.Min(StepSize,seconds-i*StepSize);
                    if(dt<=0)break;
                    Freeze();
                    if(Moving!=null)foreach(var unit in Moving.Members)
                        if(unit!=null&&unit.Alive&&unit!=Held&&Brains.TryGetValue(unit,out var brain))unit.Brain=brain;
                    Observe();World.Tick(dt);World.Effects.Tick(dt);Observe();Freeze();
                }
            }

            public void Freeze()
            {
                foreach(var unit in World.Entities)
                {
                    if(unit==null||!unit.Alive)continue;
                    if(unit.Brain!=null)
                    {
                        if(unit.Faction==Faction.Invaders)Brains[unit]=unit.Brain;
                        unit.Brain=null;
                    }
                }
            }
        }

        public static void Run(GameWorld w,Action<bool,string> check)
        {
            w.BeginCampaign();w.Player.Bot=false;w.Player.ReleaseControl();
            w.ProductionLeft=10000;
            PlacePlayer(w,new Vector3(0,0,-17));
            foreach(var unit in w.Entities)
                if(unit!=null&&unit.Alive&&unit.Role==Role.Marine)
                    unit.transform.position=new Vector3(-23+unit.Slot*.8f,0,-18);
            Physics.SyncTransforms();
            int scheduledTotal=0,extraSpawns=0;
            check(WavePlan.TotalCount==408&&GameWorld.EnemyLimit==65,
                "The grouped assault retains 408 scheduled invaders and a 65-enemy concurrency limit");

            for(int wave=1;wave<=5;wave++)
            {
                if(wave>1)w.StartWave();
                check(w.Phase==Phase.Assault&&w.Wave==wave,"Wave "+wave+" starts through the normal campaign transition");
                if(w.Phase!=Phase.Assault||w.Wave!=wave)return;
                var audit=new Audit(w);CheckPlan(w,check);
                if(wave==1)extraSpawns=CheckPauseAndCapacity(w,audit,check);
                else if(wave==3)CheckFormation(w,audit,check,0);
                else if(wave==4)CheckFormation(w,audit,check,1);
                else if(wave==5)CheckFormation(w,audit,check,2);
                audit.Moving=null;audit.Held=null;
                CompleteWave(w,audit,check);
                scheduledTotal+=audit.Count;
                check(audit.Count==WavePlan.Counts[wave-1]&&audit.CounterCorrect,
                    "Wave "+wave+" spawns every scheduled order exactly once and keeps its remaining count consistent");
                check(audit.SourcesCorrect&&audit.BindingsCorrect,
                    "Wave "+wave+" actors appear at their announced rift with the correct lane and real group binding");
                check(audit.WarningsCorrect,
                    "Wave "+wave+" warns about each approaching batch before its first actor appears");
                CheckBirths(w,audit,check,wave);
                if(wave>=3)check(audit.DualLane,"Wave "+wave+" has real living attackers on both lanes simultaneously");
                if(w.Ended&&wave<5)return;
            }
            check(scheduledTotal==408&&w.TotalSpawned==408+extraSpawns&&w.Kills==w.TotalSpawned,
                "All 408 grouped campaign enemies and the separately counted cap-test enemies really spawn and die");
            check(Array.TrueForAll(w.SpawnPointUses,n=>n>0),"The complete campaign rotates actual spawns through all six rifts");
            check(w.Phase==Phase.Victory&&w.Wave==5&&w.EnemiesAlive==0&&w.RemainingToSpawn==0,
                "Victory occurs only after the complete fifth-wave queue and its final living enemy are cleared");
            check(w.PeakEnemies<=GameWorld.EnemyLimit,"Actual concurrent enemies never exceed 65 during queue backpressure and the campaign");
        }

        static void CheckPlan(GameWorld w,Action<bool,string> check)
        {
            int count=0;bool valid=true,sorted=true;float at=-1;
            var expected=new int[2,3];var actual=new int[2,3];
            foreach(var order in WavePlan.Create(w.Wave))expected[order.Lane,RoleIndex(order.Role)]++;
            foreach(var batch in w.Assault.Schedule)
            {
                valid&=batch.Index>=0&&batch.Count>1&&batch.Lane>=0&&batch.Lane<=1&&
                    batch.SourceSite>=0&&batch.SourceSite<6&&batch.SourceSite%2==batch.Lane;
                sorted&=batch.At>=at;at=batch.At;
                foreach(var role in batch.Roles){actual[batch.Lane,RoleIndex(role)]++;count++;}
            }
            check(valid&&sorted&&count==WavePlan.Counts[w.Wave-1]&&w.Assault.BatchCount==w.Assault.Schedule.Count,
                "Wave "+w.Wave+" has ordered, nonempty multi-enemy batches on valid lane-specific rifts");
            bool same=true;for(int lane=0;lane<2;lane++)for(int role=0;role<3;role++)same&=actual[lane,role]==expected[lane,role];
            check(same,"Wave "+w.Wave+" preserves the original role and lane totals when regrouping spawn orders");
            check(w.Assault.Schedule.Count>0&&w.Assault.Schedule[0].At>=4,
                "Wave "+w.Wave+" gives at least four seconds of preparation before the first batch");
        }

        static int CheckPauseAndCapacity(GameWorld w,Audit audit,Action<bool,string> check)
        {
            audit.Step(1);
            check(w.EnemiesAlive==0&&w.RemainingToSpawn==40&&w.Phase==Phase.Assault,
                "An empty battlefield cannot finish a wave while its warned spawn queue is still pending");
            float time=w.SimTime,elapsed=w.Assault.Elapsed,countdown=w.Assault.Countdown;
            int warning=w.Assault.WarningLaneMask,remaining=w.RemainingToSpawn;
            w.SetPaused(true);w.Tick(5);w.Assault.Tick(5);
            check(w.SimTime==time&&w.Assault.Elapsed==elapsed&&w.Assault.Countdown==countdown&&
                w.Assault.WarningLaneMask==warning&&w.RemainingToSpawn==remaining&&w.SpawnedThisWave==0,
                "World and direct director updates freeze warning, batch and spawn timers while paused");
            w.SetPaused(false);w.Player.ReleaseControl();
            var blockers=new List<Health>();
            for(int i=0;i<GameWorld.EnemyLimit;i++)
                blockers.Add(w.SpawnEnemy(Role.Runner,i%2,new Vector3(-32+(i%10)*.15f,-4,77+(i/10)*.15f)));
            audit.Step(8);
            check(w.EnemiesAlive==65&&w.SpawnedThisWave==0&&w.RemainingToSpawn==40&&w.Assault.Spawned==0,
                "Sixty-five real live enemies hold the first batch without consuming or discarding any queued order");
            blockers[0].Damage(10000,null);audit.Step(.5f);
            check(w.EnemiesAlive==65&&w.SpawnedThisWave==1&&w.RemainingToSpawn==39,
                "Freeing one real concurrency slot admits exactly one pending enemy and refills the cap");
            foreach(var blocker in blockers)if(blocker!=null&&blocker.Alive)blocker.Damage(10000,null);
            audit.Step(.5f);
            check(w.SpawnedThisWave>1&&w.RemainingToSpawn==40-w.SpawnedThisWave,
                "Releasing the remaining blockers resumes the same batch with no skipped or duplicated orders");
            return blockers.Count;
        }

        static void CheckFormation(GameWorld w,Audit audit,Action<bool,string> check,int mode)
        {
            AssaultGroup group=null;
            for(int i=0;i<400&&group==null;i++)
            {
                audit.Step(StepSize);
                foreach(var candidate in w.Assault.Groups)
                    if(HasRole(candidate.Batch.Roles,Role.Colossus)&&HasRole(candidate.Batch.Roles,Role.Runner))
                    {group=candidate;break;}
            }
            check(group!=null,"Wave "+w.Wave+" actually creates a mixed armored assault group");
            if(group==null)return;
            audit.Moving=group;
            if(mode==0)
            {
                float forming=group.FormingTime,elapsed=w.Assault.Elapsed;Vector3 anchor=group.Anchor;
                var positions=Positions(group);w.SetPaused(true);w.Tick(3);w.Assault.Tick(3);
                check(group.FormingTime==forming&&group.Anchor==anchor&&w.Assault.Elapsed==elapsed&&Unchanged(group,positions),
                    "Pausing during real assembly freezes its timeout, anchor and every existing member position");
                w.SetPaused(false);w.Player.ReleaseControl();
            }
            for(int i=0;i<200&&!group.SpawnComplete;i++)audit.Step(StepSize);
            check(group.SpawnComplete&&group.AliveCount==group.Batch.Count,
                "The mixed group receives every real batch member before its formation check");
            if(!group.SpawnComplete||group.AliveCount==0)return;

            if(mode==2)
            {
                foreach(var unit in group.Members)if(unit!=null&&unit.Alive&&unit!=group.Leader){audit.Held=unit;break;}
                check(audit.Held!=null&&group.State.ToString()=="Forming","A real nonleader can be held away while its group is still assembling");
                if(audit.Held==null)return;
                audit.Held.transform.position=new Vector3(group.Anchor.x+20,-4,group.Anchor.z+15);Physics.SyncTransforms();
                for(int i=0;i<Mathf.CeilToInt((AssaultGroup.FormationTimeout+1)/StepSize)&&group.State.ToString()=="Forming";i++)audit.Step(StepSize);
                check(group.State.ToString()!="Forming"&&group.FormingTime>=AssaultGroup.FormationTimeout&&group.ReadyCount<group.AliveCount,
                    "A physically missing member triggers the assembly deadline instead of keeping the group waiting forever");
                audit.Step(AssaultGroup.AdvanceTimeout+1);
                check(group.State.ToString()=="Engaged",
                    "A persistently blocked straggler cannot hold coordinated advance beyond its independent deadline");
                Health mover=group.Leader;Vector3 before=mover!=null?mover.transform.position:Vector3.zero;
                audit.Step(2);
                check(mover!=null&&mover.Alive&&Vector3.Distance(before,mover.transform.position)>2,
                    "Surviving attackers really resume movement after the stalled formation releases them");
                return;
            }

            for(int i=0;i<Mathf.CeilToInt((AssaultGroup.FormationTimeout+1)/StepSize)&&group.State.ToString()=="Forming";i++)audit.Step(StepSize);
            check(group.State.ToString()=="Advancing"&&group.FormingTime<AssaultGroup.FormationTimeout&&group.ReadyCount==group.AliveCount,
                "Unblocked armored and escort actors reach their actual assembly positions before advancing together");
            check(group.Leader!=null&&group.Leader.Alive&&group.Leader.Role==Role.Colossus,
                "A live armored member leads the mixed formation");
            if(group.Leader==null||!group.Leader.Alive)return;
            if(mode==1)
            {
                Health former=group.Leader;former.Damage(10000,null);audit.Step(StepSize);
                Health successor=group.Leader;
                check(!former.Alive&&successor!=null&&successor!=former&&successor.Alive,
                    "Killing the real armored leader elects a living replacement without deleting its escorts");
                if(successor==null)return;
                Vector3 before=successor.transform.position;audit.Step(8);
                check(successor.Alive&&Vector3.Distance(before,successor.transform.position)>8&&group.State.ToString()!="Forming",
                    "The replacement leader and surviving group continue their actual approach after the leader casualty");
                return;
            }
            var starts=Positions(group);Vector3 anchorStart=group.Anchor;float maximumGap=0;bool close=true;
            for(int i=0;i<160;i++)
            {
                audit.Step(StepSize);
                foreach(var unit in group.Members)if(unit!=null&&unit.Alive)
                {
                    float gap=FlatDistance(unit.transform.position,group.MemberGoal(unit));
                    maximumGap=Mathf.Max(maximumGap,gap);close&=gap<4;
                }
            }
            bool moved=true;float armoredZ=0,spitterZ=0;int armoredCount=0,spitterCount=0;
            foreach(var unit in group.Members)if(unit!=null&&unit.Alive)
            {
                moved&=starts.TryGetValue(unit.GetInstanceID(),out var before)&&before.z-unit.transform.position.z>8;
                if(unit.Role==Role.Colossus){armoredZ+=unit.transform.position.z;armoredCount++;}
                if(unit.Role==Role.Spitter){spitterZ+=unit.transform.position.z;spitterCount++;}
            }
            Debug.Log("ASSAULT_FORMATION wave="+w.Wave+" batch="+group.Batch.Index+" forming="+group.FormingTime+
                " anchorTravel="+(anchorStart.z-group.Anchor.z)+" maximumSlotGap="+maximumGap+" state="+group.State);
            check(moved&&anchorStart.z-group.Anchor.z>8&&close,
                "Every real armored and escort actor advances over eight metres while remaining within four metres of its moving slot");
            check(armoredCount>0&&spitterCount>0&&spitterZ/spitterCount-armoredZ/armoredCount>3,
                "Actual ranged escorts follow at least three metres behind the armored front instead of overtaking it");
        }

        static void CompleteWave(GameWorld w,Audit audit,Action<bool,string> check)
        {
            float deadline=w.Assault.Elapsed+300;bool checkedLast=false;
            while(w.Phase==Phase.Assault&&w.Assault.Elapsed<deadline)
            {
                if(w.RemainingToSpawn==0)
                {
                    Health last=null;
                    foreach(var unit in w.Entities.ToArray())if(unit!=null&&unit.Alive&&unit.Faction==Faction.Invaders)
                    {if(last==null)last=unit;else unit.Damage(10000,null);}
                    if(last!=null)
                    {
                        audit.Step(.25f);
                        check(w.Phase==Phase.Assault&&w.EnemiesAlive==1&&last.Alive,
                            "Wave "+w.Wave+" waits for its last real surviving enemy after all orders have spawned");
                        checkedLast=true;last.Damage(10000,null);audit.Step(StepSize);break;
                    }
                }
                foreach(var unit in w.Entities.ToArray())
                    if(unit!=null&&unit.Alive&&unit.Faction==Faction.Invaders&&
                        audit.Born.TryGetValue(unit.GetInstanceID(),out var born)&&w.Assault.Elapsed-born>12)
                        unit.Damage(10000,null);
                audit.Step(StepSize);
            }
            check(checkedLast&&w.Phase==(w.Wave==5?Phase.Victory:Phase.Intermission),
                "Wave "+w.Wave+" completes through the normal transition within the bounded scheduling check");
        }

        static void CheckBirths(GameWorld w,Audit audit,Action<bool,string> check,int wave)
        {
            bool roles=true,burst=false;int[] expectedSources=new int[6];
            foreach(var batch in w.Assault.Schedule)
            {
                expectedSources[batch.SourceSite]+=batch.Count;
                if(!audit.Roles.TryGetValue(batch.Index,out var actual)){roles=false;continue;}
                var expected=new int[3];var got=new int[3];
                foreach(var role in batch.Roles)expected[RoleIndex(role)]++;
                foreach(var role in actual)got[RoleIndex(role)]++;
                for(int i=0;i<3;i++)roles&=expected[i]==got[i];
                var times=audit.Times[batch.Index];
                for(int i=3;i<times.Count;i++)if(times[i]-times[i-3]<3)burst=true;
            }
            bool sources=true;for(int i=0;i<6;i++)sources&=w.SpawnPointUses[i]-audit.SourceStart[i]==expectedSources[i];
            check(roles&&sources,"Wave "+wave+" actually spawns the exact batch roles and the scheduled count at each rift");
            check(burst,"Wave "+wave+" delivers at least four members of one assault batch within three seconds");
        }

        static bool HasRole(IReadOnlyList<Role> roles,Role wanted){foreach(var role in roles)if(role==wanted)return true;return false;}
        static int RoleIndex(Role role)=>role==Role.Runner?0:role==Role.Spitter?1:2;
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        static Dictionary<int,Vector3> Positions(AssaultGroup group)
        {
            var result=new Dictionary<int,Vector3>();foreach(var unit in group.Members)
                if(unit!=null&&unit.Alive)result[unit.GetInstanceID()]=unit.transform.position;return result;
        }
        static bool Unchanged(AssaultGroup group,Dictionary<int,Vector3> positions)
        {
            foreach(var unit in group.Members)if(unit!=null&&unit.Alive&&
                (!positions.TryGetValue(unit.GetInstanceID(),out var before)||before!=unit.transform.position))return false;
            return true;
        }
        static void PlacePlayer(GameWorld w,Vector3 position)
        {
            var controller=w.Player.GetComponent<CharacterController>();controller.enabled=false;
            w.Player.transform.position=position;controller.enabled=true;w.Player.UpdateCamera(.05f);
        }
    }
}

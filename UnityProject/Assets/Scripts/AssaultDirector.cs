using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public enum AssaultGroupState { Forming, Advancing, Engaged }

    public sealed class AssaultBatch
    {
        public int Index { get; internal set; }
        public int Lane { get; internal set; }
        public int SourceSite { get; internal set; }
        public float At { get; internal set; }
        readonly List<Role> roles;
        public IReadOnlyList<Role> Roles => roles;
        public int Count => roles.Count;
        internal AssaultBatch(int index,int lane,int site,float at,List<Role> units)
        { Index=index;Lane=lane;SourceSite=site;At=at;roles=units; }
    }

    // A real formation shared by its members; no enemy is teleported to the rally point.
    public sealed class AssaultGroup
    {
        public const float FormationTimeout=20f,AdvanceTimeout=30f,ReadyRadius=1.65f;
        readonly GameWorld world;
        readonly List<Health> members=new List<Health>();
        readonly Dictionary<Health,Vector3> offsets=new Dictionary<Health,Vector3>();
        int heavyCount,runnerCount,spitterCount,route;
        float contactLeft;
        public AssaultBatch Batch { get; private set; }
        public AssaultGroupState State { get; private set; }
        public IReadOnlyList<Health> Members => members;
        public Health Leader { get; private set; }
        public bool SpawnComplete { get; internal set; }
        public float FormingTime { get; private set; }
        public float AdvanceTime { get; private set; }
        public bool TimedOut { get; private set; }
        public Vector3 Anchor { get; private set; }
        public int AliveCount { get { int n=0;foreach(var h in members)if(h!=null&&h.Alive)n++;return n; } }
        public int ReadyCount
        {
            get
            {
                int n=0;foreach(var h in members)
                    if(h!=null&&h.Alive&&FlatDistance(h.transform.position,MemberGoal(h))<=ReadyRadius)n++;
                return n;
            }
        }

        internal AssaultGroup(GameWorld owner,AssaultBatch batch)
        {
            world=owner;Batch=batch;State=AssaultGroupState.Forming;
            Anchor=GameWorld.EnemySpawnPoints[batch.SourceSite]+Vector3.back*14;
        }

        internal void Add(Health health)
        {
            members.Add(health);Vector3 offset;
            if(health.Role==Role.Colossus)
            {
                int i=heavyCount++;
                offset=new Vector3((i%3-1)*2.4f,0,-1.4f+(i/3)*2.6f);
            }
            else if(health.Role==Role.Spitter||health.Role==Role.Siege)
            {
                int i=spitterCount++;
                offset=new Vector3((i%3-1)*2.2f,0,5.2f+(i/3)*1.6f);
            }
            else
            {
                int i=runnerCount++;
                offset=new Vector3((i%4-1.5f)*1.8f,0,1f+(i/4)*1.6f);
            }
            offsets.Add(health,offset);health.Brain.AssignAssault(this);SelectLeader();
        }

        public Vector3 MemberGoal(Health health)
        {
            Vector3 goal=Anchor;
            if(health!=null&&offsets.TryGetValue(health,out var offset))goal+=offset;
            goal.y=GameWorld.GroundHeight(goal.x,goal.z);return goal;
        }

        internal void Tick(float dt)
        {
            if(State==AssaultGroupState.Engaged)return;
            SelectLeader();
            if(AliveCount==0)
            {
                if(SpawnComplete)Engage();
                return;
            }
            contactLeft-=dt;
            if(contactLeft<=0)
            {
                contactLeft=.3f;
                foreach(var member in members)
                {
                    if(member==null||!member.Alive)continue;
                    Health defender=world.FindTarget(member,18);
                    if(defender!=null&&Combat.Visible(member,defender)){Engage();return;}
                }
            }
            if(State==AssaultGroupState.Forming)
            {
                FormingTime+=dt;
                if((SpawnComplete&&ReadyCount==AliveCount)||FormingTime>=FormationTimeout)
                {
                    TimedOut=FormingTime>=FormationTimeout;State=AssaultGroupState.Advancing;
                }
                return;
            }
            AdvanceTime+=dt;
            if(AdvanceTime>=AdvanceTimeout){TimedOut=true;Engage();return;}
            float speed=float.MaxValue;bool cohesive=true;
            foreach(var h in members)
            {
                if(h==null||!h.Alive)continue;
                speed=Mathf.Min(speed,ActorBrain.MovementSpeed(h.Role));
                if(FlatDistance(h.transform.position,MemberGoal(h))>8)cohesive=false;
            }
            if(!cohesive||speed==float.MaxValue)return;
            Vector3 goal=world.EnemyWaypoint(Batch.Lane,route,0);
            if(FlatDistance(Anchor,goal)<.5f&&route<1)goal=world.EnemyWaypoint(Batch.Lane,++route,0);
            Vector3 next=Vector3.MoveTowards(Anchor,goal,speed*dt);
            next.y=GameWorld.GroundHeight(next.x,next.z);Anchor=next;
            if(Anchor.z<=28)Engage();
        }

        public void Engage() { State=AssaultGroupState.Engaged; }

        void SelectLeader()
        {
            if(Leader!=null&&Leader.Alive&&Leader.Role==Role.Colossus)return;
            Health candidate=null;float speed=float.MaxValue;
            foreach(var h in members)
            {
                if(h==null||!h.Alive)continue;
                float current=ActorBrain.MovementSpeed(h.Role);
                if(current<speed){candidate=h;speed=current;}
            }
            Leader=candidate;
        }
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
    }

    public sealed class AssaultDirector
    {
        public const float InitialWarning=4f,MemberInterval=.16f;
        public const float MidWavePairInterval=20f,FinalWavePairInterval=18f;
        readonly GameWorld world;
        readonly List<AssaultBatch> schedule=new List<AssaultBatch>();
        readonly List<AssaultGroup> groups=new List<AssaultGroup>();
        int nextBatch,memberIndex;
        float memberLeft;
        AssaultGroup deploying;
        public IReadOnlyList<AssaultBatch> Schedule => schedule;
        public IReadOnlyList<AssaultGroup> Groups => groups;
        public float Elapsed { get; private set; }
        public int Suppressed {get;private set;}
        public int TotalSuppressed {get;private set;}
        public int Spawned { get; private set; }
        public int Remaining { get; private set; }
        public int RemainingToSpawn => Remaining;
        public int BatchCount => schedule.Count;
        public int LaunchedBatches => nextBatch;
        public float Countdown => deploying!=null?0:nextBatch<schedule.Count?Mathf.Max(0,schedule[nextBatch].At-Elapsed):0;
        public string PhaseText => deploying!=null ? "敌军出动" : nextBatch<schedule.Count ? "下一轮突击" : "清除剩余敌军";
        public int WarningLaneMask
        {
            get
            {
                if(deploying!=null)
                {
                    int activeMask=1<<deploying.Batch.Lane;
                    if(nextBatch<schedule.Count&&schedule[nextBatch].At-Elapsed<=InitialWarning)
                        activeMask|=1<<schedule[nextBatch].Lane;
                    return activeMask;
                }
                if(nextBatch>=schedule.Count)return 0;
                int mask=1<<schedule[nextBatch].Lane;
                if(nextBatch+1<schedule.Count&&schedule[nextBatch+1].At-schedule[nextBatch].At<=4.01f)
                    mask|=1<<schedule[nextBatch+1].Lane;
                return mask;
            }
        }
        public int WarningCount
        {
            get
            {
                if(deploying!=null)
                {
                    int activeCount=deploying.Batch.Count-memberIndex;
                    if(nextBatch<schedule.Count&&schedule[nextBatch].At-Elapsed<=InitialWarning)activeCount+=schedule[nextBatch].Count;
                    return activeCount;
                }
                if(nextBatch>=schedule.Count)return 0;
                int n=schedule[nextBatch].Count;
                if(nextBatch+1<schedule.Count&&schedule[nextBatch+1].At-schedule[nextBatch].At<=4.01f)n+=schedule[nextBatch+1].Count;
                return n;
            }
        }
        public string WarningText
        {
            get
            {
                int mask=WarningLaneMask;
                if(mask==0)return "本波敌军已全部出动";
                string direction=mask==3?"东西双线":mask==1?"西侧入口":"东侧入口";
                int armor=0,spitters=0;
                if(deploying!=null)
                {
                    CountRoles(deploying.Batch,memberIndex,ref armor,ref spitters);
                    if(nextBatch<schedule.Count&&schedule[nextBatch].At-Elapsed<=InitialWarning)
                        CountRoles(schedule[nextBatch],0,ref armor,ref spitters);
                }
                else if(nextBatch<schedule.Count)
                {
                    CountRoles(schedule[nextBatch],0,ref armor,ref spitters);
                    if(nextBatch+1<schedule.Count&&schedule[nextBatch+1].At-schedule[nextBatch].At<=4.01f)
                        CountRoles(schedule[nextBatch+1],0,ref armor,ref spitters);
                }
                string detail=armor>0?" · 重甲 "+armor:"";
                if(spitters>0)detail+=" · 远程 "+spitters;
                return direction+" · "+WarningCount+" 敌人"+detail;
            }
        }

        public AssaultDirector(GameWorld owner){world=owner;}
        public void StartWave(int wave)
        {
            schedule.Clear();groups.Clear();nextBatch=memberIndex=Spawned=0;memberLeft=Elapsed=0;deploying=null;
            Suppressed=0;Remaining=WavePlan.Counts[wave-1];
            var lanes=new[]{new List<Role>(),new List<Role>()};
            foreach(var order in WavePlan.Create(wave))lanes[order.Lane].Add(order.Role);
            int groupsPerLane=wave==2?5:4;
            int[] cursors={0,0},sources={0,0},created={0,0};int index=0;
            while(cursors[0]<lanes[0].Count||cursors[1]<lanes[1].Count)
            {
                int lane=wave==1?0:wave==2?1:index%2;
                if(cursors[lane]>=lanes[lane].Count)lane=1-lane;
                int count=Mathf.CeilToInt((lanes[lane].Count-cursors[lane])/(float)(groupsPerLane-created[lane]));
                created[lane]++;
                var roles=lanes[lane].GetRange(cursors[lane],count);cursors[lane]+=count;
                // Heavy units leave the rift first, so followers meet them instead of outrunning them.
                roles.Sort((a,b)=>Rank(a).CompareTo(Rank(b)));
                float at=InitialWarning+(wave<3?index*(wave==1?18:17):(index/2)*(wave==5?FinalWavePairInterval:MidWavePairInterval)+(index%2)*InitialWarning);
                schedule.Add(new AssaultBatch(index,lane,lane+2*(sources[lane]++%3),at,roles));index++;
            }
        }

        public void Tick(float dt)
        {
            if(dt<=0||!world.Active||world.Phase!=Phase.Assault)return;
            Elapsed+=dt;
            while(deploying==null&&nextBatch<schedule.Count&&!world.EnemyDefenses.IsPortalAlive(schedule[nextBatch].SourceSite)){int skipped=schedule[nextBatch++].Count;Remaining-=skipped;Suppressed+=skipped;TotalSuppressed+=skipped;}
            foreach(var group in groups)group.Tick(dt);
            if(deploying==null&&nextBatch<schedule.Count&&Elapsed>=schedule[nextBatch].At)
            {
                deploying=new AssaultGroup(world,schedule[nextBatch++]);groups.Add(deploying);memberIndex=0;memberLeft=0;
            }
            if(deploying==null)return;
            if(!world.EnemyDefenses.IsPortalAlive(deploying.Batch.SourceSite)){int skipped=deploying.Batch.Count-memberIndex;Remaining-=skipped;Suppressed+=skipped;TotalSuppressed+=skipped;deploying.SpawnComplete=true;deploying=null;return;}
            memberLeft-=dt;
            // Capacity pauses the queue; its next role and source remain unchanged until a slot opens.
            while(memberLeft<=0&&world.EnemyMobileCount<GameWorld.EnemyLimit)
            {
                var batch=deploying.Batch;
                Health enemy=world.SpawnEnemy(batch.Roles[memberIndex],batch.Lane,sourceSite:batch.SourceSite);
                deploying.Add(enemy);memberIndex++;Spawned++;Remaining--;world.SpawnedThisWave++;
                memberLeft+=MemberInterval;
                if(memberIndex==batch.Count)
                {
                    deploying.SpawnComplete=true;deploying=null;break;
                }
            }
            // Do not accumulate a huge burst debt while the battlefield is full.
            if(world.EnemyMobileCount>=GameWorld.EnemyLimit)memberLeft=Mathf.Max(memberLeft,0);
        }

        static int Rank(Role role)=>role==Role.Colossus?0:role==Role.Siege?1:role==Role.Spitter?2:3;
        static void CountRoles(AssaultBatch batch,int start,ref int armor,ref int spitters)
        {
            for(int i=start;i<batch.Count;i++)
                if(batch.Roles[i]==Role.Colossus)armor++;else if(batch.Roles[i]==Role.Spitter)spitters++;
        }
    }
}

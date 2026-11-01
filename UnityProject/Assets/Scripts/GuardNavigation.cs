using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Each guard stores only its route. The shared grid is rebuilt once per construction change.
    public sealed class GuardNavigation
    {
        public const float Speed=3.7f,Radius=.42f,ArrivalDistance=.55f;
        readonly List<Vector3> path=new List<Vector3>(64);
        SquadNavigation navigation;
        Vector3 requestedGoal,resolvedGoal,progressAnchor;
        int waypoint;
        float retryLeft,blockedTime,progressTime,activeRadius=Radius;
        bool hasGoal;

        public Vector3 Step(Health unit,Vector3 goal,float dt,float speed=Speed,float radius=Radius)
        {
            Vector3 position=unit.transform.position;
            var world=GameWorld.I;var current=world.Squad!=null?world.Squad.Navigation:null;
            if(current==null||dt<=0)return position;
            retryLeft=Mathf.Max(0,retryLeft-dt);
            if(current!=navigation||!hasGoal||Mathf.Abs(activeRadius-radius)>.001f||FlatDistance(goal,requestedGoal)>.65f)
            {
                navigation=current;requestedGoal=goal;activeRadius=radius;
                Vector3 exactGoal=AtGround(goal);
                resolvedGoal=navigation.Fits(exactGoal,activeRadius)?exactGoal:navigation.Resolve(exactGoal,activeRadius);hasGoal=true;
                Invalidate();retryLeft=0;progressAnchor=position;progressTime=0;
            }
            if(FlatDistance(position,resolvedGoal)<=ArrivalDistance)
            {
                blockedTime=progressTime=0;return position;
            }
            if(waypoint>=path.Count&&retryLeft<=0)
            {
                navigation.Plan(position,resolvedGoal,path,activeRadius);waypoint=0;
                if(path.Count>0&&FlatDistance(path[path.Count-1],resolvedGoal)>.001f&&navigation.Clear(path[path.Count-1],resolvedGoal,activeRadius))
                    path.Add(resolvedGoal);
                retryLeft=.45f+Mathf.Max(0,unit.Slot)*.025f;
            }
            if(waypoint>=path.Count)return position;
            while(waypoint<path.Count&&FlatDistance(position,path[waypoint])<.25f)
            {
                if(waypoint+1<path.Count&&!navigation.Clear(position,path[waypoint+1],activeRadius))break;
                waypoint++;
            }
            if(waypoint>=path.Count)return position;
            // String pulling skips every visible grid corner but never cuts a solid corner.
            while(waypoint+1<path.Count&&navigation.Clear(position,path[waypoint+1],activeRadius))waypoint++;
            Vector3 delta=path[waypoint]-position;delta.y=0;
            if(delta.sqrMagnitude<.00001f)return position;
            float distance=Mathf.Min(delta.magnitude,speed*dt);
            Vector3 direction=delta.normalized,separation=Vector3.zero;
            foreach(var other in world.Entities)
            {
                if(other==null||other==unit||!other.Alive||other.IsBuilding||other.IsSheltered||other.Role==Role.Player)continue;
                Vector3 away=position-other.transform.position;away.y=0;float gap=away.magnitude;
                if(gap>.02f&&gap<.9f)separation+=away/gap*(.9f-gap);
            }
            Vector3 preferred=(direction+Vector3.ClampMagnitude(separation,.65f)).normalized;
            Vector3 next=AtGround(position+preferred*distance);
            bool safe=CanMove(position,next);
            if(!safe)
            {
                next=AtGround(position+direction*distance);
                safe=CanMove(position,next);
            }
            if(!safe)
            {
                blockedTime+=dt;
                if(blockedTime>=.35f){Invalidate();retryLeft=.1f+Mathf.Max(0,unit.Slot)*.02f;}
                return position;
            }
            blockedTime=0;progressTime+=dt;
            if(progressTime>=1.1f)
            {
                if(FlatDistance(next,progressAnchor)<.3f){Invalidate();retryLeft=.12f;}
                progressAnchor=next;progressTime=0;
            }
            return next;
        }

        bool CanMove(Vector3 from,Vector3 to)=>navigation.Fits(to,activeRadius)&&navigation.Clear(from,to,activeRadius);
        void Invalidate(){path.Clear();waypoint=0;blockedTime=0;}
        static Vector3 AtGround(Vector3 point){point.y=GroundSurface.Height(point.x,point.z);return point;}
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
    }
}

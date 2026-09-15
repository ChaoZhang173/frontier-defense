using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // A small, deterministic navigation grid for the outpost and its two ramps.
    // Buildings remain obstacles after destruction; new construction can rebuild this grid.
    public sealed class SquadNavigation
    {
        const int MinX=-38,MinZ=-22,Width=77,Height=103,Count=Width*Height;
        readonly bool[] walkable=new bool[Count];
        readonly float[] distance=new float[Count];
        readonly int[] seen=new int[Count],closed=new int[Count],parent=new int[Count];
        readonly List<Node> heap=new List<Node>(512);
        readonly List<Vector3> reverse=new List<Vector3>(150);
        readonly RaycastHit[] hits=new RaycastHit[16];
        int search;
        struct Node { public int Id;public float Score;public Node(int id,float score){Id=id;Score=score;} }

        public SquadNavigation()
        {
            Physics.SyncTransforms();
            for(int i=0;i<Count;i++)
            {
                Vector3 p=Point(i);
                walkable[i]=OnTerrain(p)&&!Physics.CheckCapsule(p+Vector3.up*.65f,p+Vector3.up*1.5f,.43f,Combat.WorldMask,QueryTriggerInteraction.Ignore);
            }
        }
        static bool OnTerrain(Vector3 p)=>GroundSurface.WalkableTerrain(p.x,p.z);
        static Vector3 Point(int id)
        {
            float x=id%Width+MinX,z=id/Width+MinZ;
            return new Vector3(x,GroundSurface.Height(x,z),z);
        }
        static int Index(Vector3 p)
        {
            int x=Mathf.RoundToInt(p.x)-MinX,z=Mathf.RoundToInt(p.z)-MinZ;
            return x<0||x>=Width||z<0||z>=Height?-1:z*Width+x;
        }
        public bool Walkable(Vector3 p)
        {
            int id=Index(p);if(!OnTerrain(p)||id<0)return false;
            Vector3 node=Point(id);
            if(new Vector2(node.x-p.x,node.z-p.z).sqrMagnitude<.000001f)return walkable[id];
            // Grid occupancy describes node centres, not every point in their square.
            // Continuous motion around a corner must use the actor's real clearance.
            return Fits(p,.43f);
        }
        int Nearest(Vector3 p,bool reachable=false,float radius=.36f)
        {
            int id=Index(p);if(id>=0&&walkable[id]&&(!reachable||Clear(p,Point(id),radius)))return id;
            float best=float.MaxValue;int chosen=-1;
            for(int i=0;i<Count;i++)if(walkable[i])
            {
                Vector3 d=Point(i)-p;float score=d.x*d.x+d.z*d.z;
                if(score<best&&(!reachable||Clear(p,Point(i),radius))){best=score;chosen=i;}
            }
            return chosen;
        }
        public Vector3 Resolve(Vector3 p){int id=Nearest(p);return id<0?p:Point(id);}
        public bool Clear(Vector3 a,Vector3 b,float radius=.36f)
        {
            float length=new Vector2(b.x-a.x,b.z-a.z).magnitude;int steps=Mathf.Max(1,Mathf.CeilToInt(length/.35f));
            Vector3 previous=a;previous.y=GroundSurface.Height(a.x,a.z);
            for(int i=1;i<=steps;i++)
            {
                Vector3 point=Vector3.Lerp(a,b,(float)i/steps);point.y=GroundSurface.Height(point.x,point.z);
                if(!Fits(point,radius)||!GroundSurface.Traversable(previous,point))return false;
                previous=point;
            }
            return !Blocked(a,b,radius);
        }
        public bool Blocked(Vector3 a,Vector3 b,float radius=.36f)
        {
            Vector3 d=b-a;if(d.sqrMagnitude<.00001f)return false;
            int count=Physics.CapsuleCastNonAlloc(a+Vector3.up*.65f,a+Vector3.up*1.5f,radius,d.normalized,hits,d.magnitude,Combat.WorldMask,QueryTriggerInteraction.Ignore);
            if(count>=hits.Length)return true;
            for(int i=0;i<count;i++)if(hits[i].normal.y<.55f)return true;
            return false;
        }
        public bool Fits(Vector3 point,float radius=.42f)
        {
            if(!OnTerrain(point)||Index(point)<0)return false;
            point.y=GroundSurface.Height(point.x,point.z);
            return !Physics.CheckCapsule(point+Vector3.up*.65f,point+Vector3.up*1.5f,radius,Combat.WorldMask,QueryTriggerInteraction.Ignore);
        }

        public bool Plan(Vector3 from,Vector3 goal,List<Vector3> output,float radius=.36f)
        {
            output.Clear();int start=Nearest(from,true,radius),end=Nearest(goal);if(start<0||end<0)return false;
            Vector3 target=Point(end);
            if(Clear(from,target,radius)){output.Add(target);return true;}
            search++;heap.Clear();seen[start]=search;distance[start]=0;parent[start]=-1;Push(start,Heuristic(start,end));
            bool found=false;
            while(heap.Count>0)
            {
                int current=Pop();if(closed[current]==search)continue;closed[current]=search;
                if(current==end){found=true;break;}
                int x=current%Width,z=current/Width;
                for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                {
                    if(dx==0&&dz==0)continue;int nx=x+dx,nz=z+dz;
                    if(nx<0||nx>=Width||nz<0||nz>=Height)continue;
                    int next=nz*Width+nx;if(!walkable[next]||closed[next]==search)continue;
                    if(dx!=0&&dz!=0&&(!walkable[z*Width+nx]||!walkable[nz*Width+x]))continue;
                    if(!GroundSurface.Traversable(Point(current),Point(next))||Blocked(Point(current),Point(next),radius))continue;
                    float cost=distance[current]+(dx!=0&&dz!=0?1.414214f:1);
                    if(seen[next]==search&&distance[next]<=cost)continue;
                    seen[next]=search;distance[next]=cost;parent[next]=current;Push(next,cost+Heuristic(next,end));
                }
            }
            if(!found)return false;
            output.Add(Point(start));
            reverse.Clear();for(int id=end;id!=start;id=parent[id])reverse.Add(Point(id));
            for(int i=reverse.Count-1;i>=0;i--)output.Add(reverse[i]);
            return true;
        }
        static float Heuristic(int a,int b)
        {
            int dx=Mathf.Abs(a%Width-b%Width),dz=Mathf.Abs(a/Width-b/Width);
            return Mathf.Max(dx,dz)+.414214f*Mathf.Min(dx,dz);
        }
        void Push(int id,float score)
        {
            var node=new Node(id,score);int i=heap.Count;heap.Add(node);
            while(i>0){int p=(i-1)/2;if(heap[p].Score<=score)break;heap[i]=heap[p];i=p;}heap[i]=node;
        }
        int Pop()
        {
            int result=heap[0].Id,last=heap.Count-1;var node=heap[last];heap.RemoveAt(last);if(last==0)return result;
            int i=0;
            while(i*2+1<heap.Count)
            {
                int child=i*2+1;if(child+1<heap.Count&&heap[child+1].Score<heap[child].Score)child++;
                if(heap[child].Score>=node.Score)break;heap[i]=heap[child];i=child;
            }
            heap[i]=node;return result;
        }
    }
}

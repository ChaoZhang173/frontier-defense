using UnityEngine;

namespace Frontier
{
    public static class EnemyFronts
    {
        // The displayed ruins and their blocking volumes come from one definition.
        public static readonly Bounds[] Ruins={
            new Bounds(new Vector3(0,-2.2f,61),new Vector3(10,3.6f,5)),
            new Bounds(new Vector3(-31,-2.6f,57),new Vector3(10,2.8f,3)),
            new Bounds(new Vector3(31,-2.6f,57),new Vector3(10,2.8f,3)),
            new Bounds(new Vector3(-6,-2.8f,94),new Vector3(3,2.4f,13)),
            new Bounds(new Vector3(6,-2.8f,94),new Vector3(3,2.4f,13)),
            new Bounds(new Vector3(-51,-1.7f,74),new Vector3(7,4.6f,9)),
            new Bounds(new Vector3(51,-1.7f,74),new Vector3(7,4.6f,9)),
            new Bounds(new Vector3(0,-2.5f,112),new Vector3(8,3,3))
        };

        public static void Build(Transform parent)
        {
            Transform landscape=Art.Root("Expanded front",parent,Vector3.zero);
            foreach(var ruin in Ruins)
            {
                Art.Part(landscape,"Ruined relay wall",ruin.center,ruin.size,3,collision:true);
                Art.Part(landscape,"Broken relay coping",ruin.center+Vector3.up*(ruin.extents.y+.05f),new Vector3(ruin.size.x,.1f,ruin.size.z),2);
                Art.Part(landscape,"Faded relay stripe",ruin.center+new Vector3(0,.3f,-ruin.extents.z-.012f),new Vector3(Mathf.Min(2,ruin.size.x*.65f),.15f,.035f),13);
            }
            for(int side=-1;side<=1;side+=2)
            {
                Road(landscape,new[]{new Vector2(side*13.5f,40),new Vector2(side*24,59),new Vector2(side*27,82),new Vector2(side*31,89),new Vector2(side*44,92),new Vector2(side*44,104),new Vector2(side*16,121)},5.5f);
                Road(landscape,new[]{new Vector2(side*27,82),new Vector2(side*16,121)},4.5f);
                Road(landscape,new[]{new Vector2(side*13.5f,44),new Vector2(side*24,49),new Vector2(side*40,52),new Vector2(side*43,60),new Vector2(side*44,82),new Vector2(side*44,104)},3.5f);
            }
            Road(landscape,new[]{new Vector2(-27,82),new Vector2(0,74),new Vector2(27,82)},4);
            Road(landscape,new[]{new Vector2(-16,121),new Vector2(0,128),new Vector2(16,121)},4);
            for(int i=0;i<GameWorld.EnemySpawnPoints.Length;i++)
            {
                Vector3 point=GameWorld.EnemySpawnPoints[i];point.y=GroundSurface.Height(point.x,point.z);
                Transform pad=Art.Root("Enemy base ground "+i,landscape,point);
                // Flat, unlit soil stays after destruction; the luminous portal belongs to live Health.
                Art.Part(pad,"Infested ground",new Vector3(0,-.025f,0),new Vector3(24,.025f,24),9,PrimitiveType.Cylinder);
                for(int n=0;n<10;n++)
                {
                    float angle=n*Mathf.PI/5;
                    Art.Part(pad,"Camp boundary scar",new Vector3(Mathf.Cos(angle)*10.5f,.01f,Mathf.Sin(angle)*10.5f),new Vector3(1.5f,.03f,.5f),13,euler:new Vector3(0,-angle*Mathf.Rad2Deg,0));
                }
            }
        }

        static void Road(Transform parent,Vector2[] points,float width)
        {
            for(int i=1;i<points.Length;i++)
            {
                Vector2 delta=points[i]-points[i-1],mid=(points[i]+points[i-1])*.5f;
                Art.Part(parent,"Canyon route",new Vector3(mid.x,GroundSurface.CanyonHeight+.015f,mid.y),new Vector3(width,.025f,delta.magnitude+.05f),13,euler:new Vector3(0,Mathf.Atan2(delta.x,delta.y)*Mathf.Rad2Deg,0));
            }
        }
    }
}

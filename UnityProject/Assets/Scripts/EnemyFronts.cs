using UnityEngine;

namespace Frontier
{
    public static class EnemyFronts
    {
        public static void Build(Transform parent)
        {
            // The original two rifts remain; add four visible peripheral sources.
            for(int i=2;i<GameWorld.EnemySpawnPoints.Length;i++)
            {
                Transform rift=Art.Root("Peripheral rift "+i,parent,GameWorld.EnemySpawnPoints[i]);
                Art.Part(rift,"Rift foundation",Vector3.zero,new Vector3(5,.12f,5),9,PrimitiveType.Cylinder);
                Art.Part(rift,"Rift core",Vector3.up*.14f,new Vector3(3.7f,.04f,3.7f),10,PrimitiveType.Cylinder);
                for(int n=0;n<6;n++)
                {
                    float angle=n*Mathf.PI/3;
                    Art.Spike(rift,new Vector3(Mathf.Cos(angle)*2.7f,0,Mathf.Sin(angle)*2.7f),new Vector3(.75f,3+n%2,.75f),9,new Vector3(12,n*60,0));
                }
            }
        }
    }
}

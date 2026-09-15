using UnityEngine;

namespace Frontier
{
    public static class CentralDefense
    {
        public const float WallHeight=.7f;
        public static bool Contains(Vector3 point)=>Mathf.Abs(point.x)<8&&point.z>=19&&point.z<25.6f;
        public static Vector3 SquadPost(int slot)=>new Vector3(slot==0?-6:slot==1?6:slot==2?-3:3,0,25);
    }
}

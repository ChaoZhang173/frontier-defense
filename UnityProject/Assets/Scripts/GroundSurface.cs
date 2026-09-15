using UnityEngine;

namespace Frontier
{
    // These three planar faces are also used by Art.Ramp's collision mesh.
    public static class GroundSurface
    {
        public const float RampStart=26, RampEnd=40, CanyonHeight=-4;
        public const float RampHalfWidth=4.5f, SideWidth=6, LaneCenter=13.5f;

        public static float Height(float x,float z)
        {
            if(z<=RampStart)return 0;
            if(z>=RampEnd)return CanyonHeight;
            float centerHeight=CanyonHeight*(z-RampStart)/(RampEnd-RampStart);
            float laneDistance=Mathf.Min(Mathf.Abs(x-LaneCenter),Mathf.Abs(x+LaneCenter));
            float sideDistance=Mathf.Max(0,laneDistance-RampHalfWidth);
            return Mathf.Max(CanyonHeight,centerHeight+sideDistance*CanyonHeight/SideWidth);
        }

        public static bool WalkableTerrain(float x,float z)
        {
            if(z<=RampStart)return Mathf.Abs(x)<=26.5f&&z>=-21;
            return Mathf.Abs(x)<=38&&z<=80;
        }

        public static bool Traversable(Vector3 from,Vector3 to)
        {
            float horizontal=new Vector2(to.x-from.x,to.z-from.z).magnitude;
            if(horizontal<.0001f)return true;
            // The side faces reach 36 degrees. The remaining margin still rejects cliffs.
            return Mathf.Abs(Height(to.x,to.z)-Height(from.x,from.z))<=horizontal*.9f+.025f;
        }
    }
}

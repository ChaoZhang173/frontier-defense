using UnityEngine;

namespace Frontier
{
    public sealed class TankVisual
    {
        public Transform Turret,Barrel,Muzzle;
        public void AimAt(Vector3 point,float dt)
        {
            Vector3 direction=point-Turret.position;Vector3 flat=direction;flat.y=0;
            if(flat.sqrMagnitude>.001f)
                Turret.rotation=Quaternion.RotateTowards(Turret.rotation,Quaternion.LookRotation(flat),100*dt);
            float pitch=-Mathf.Atan2(direction.y,Mathf.Max(.01f,flat.magnitude))*Mathf.Rad2Deg;
            Barrel.localRotation=Quaternion.Euler(Mathf.Clamp(pitch,-24,22),0,0);
        }
    }

    public static class TankArt
    {
        public static void FactoryBuilding(Transform root)
        {
            Art.Part(root,"Vehicle factory foundation",new Vector3(0,.2f,0),new Vector3(8,.4f,8),1);
            Art.Part(root,"Vehicle assembly hall",new Vector3(0,2.35f,-.35f),new Vector3(7.6f,4.2f,7.1f),0);
            Art.Part(root,"Heavy garage door",new Vector3(0,1.85f,3.23f),new Vector3(5.3f,3.3f,.12f),1);
            for(int i=0;i<6;i++)
                Art.Part(root,"Garage door rib",new Vector3(0,.55f+i*.50f,3.32f),new Vector3(5.1f,.13f,.12f),2);
            Art.Part(root,"Production light",new Vector3(0,3.72f,3.4f),new Vector3(5.4f,.13f,.14f),6);
            for(int side=-1;side<=1;side+=2)
            {
                Art.Part(root,"Garage door armor",new Vector3(side*3,1.9f,3.3f),new Vector3(.5f,3.8f,.4f),7);
                Art.Part(root,"Factory roof exhaust",new Vector3(side*2.7f,4.65f,-1),new Vector3(1.25f,.55f,3),1);
                for(int i=0;i<4;i++)
                    Art.Part(root,"Factory roof vent",new Vector3(side*2.7f,4.95f,-2+i*.65f),new Vector3(1,.10f,.18f),11);
                Art.Part(root,"Exit guide",new Vector3(side*1.75f,.03f,4.6f),new Vector3(.15f,.03f,1.2f),7);
            }
            Art.Part(root,"Factory roof armor",new Vector3(0,4.5f,0),new Vector3(7.9f,.3f,7.7f),2);
            Art.Part(root,"Factory identification stripe",new Vector3(0,4.68f,1.2f),new Vector3(5.4f,.035f,.4f),7);
            // A tank silhouette on the roof makes the production building distinct from infantry barracks.
            Art.Part(root,"Factory tank emblem hull",new Vector3(0,4.72f,-.4f),new Vector3(1.8f,.07f,1.2f),6);
            Art.Part(root,"Factory tank emblem gun",new Vector3(0,4.73f,.65f),new Vector3(.25f,.08f,1.3f),6);
        }

        public static TankVisual Tank(Transform root)
        {
            Art.Part(root,"Armored lower hull",new Vector3(0,.9f,0),new Vector3(2.5f,1.05f,3.5f),0);
            Art.Part(root,"Sloped front armor",new Vector3(0,1.17f,1.4f),new Vector3(2.6f,.65f,.7f),2,euler:new Vector3(-22,0,0));
            Art.Part(root,"Engine deck",new Vector3(0,1.47f,-1.03f),new Vector3(2.35f,.17f,1.15f),1);
            for(int side=-1;side<=1;side+=2)
            {
                Art.Part(root,"Continuous track",new Vector3(side*1.22f,.57f,0),new Vector3(.65f,1.0f,3.65f),1);
                Art.Part(root,"Track armor skirt",new Vector3(side*1.28f,1.08f,0),new Vector3(.55f,.45f,3.5f),2);
                for(int i=0;i<6;i++)
                    Art.Part(root,"Road wheel",new Vector3(side*1.565f,.57f,-1.3f+i*.52f),new Vector3(.52f,.12f,.52f),3,
                        PrimitiveType.Cylinder,new Vector3(0,0,90));
                Art.Part(root,"Tank headlamp",new Vector3(side*.9f,1.2f,1.8f),new Vector3(.28f,.15f,.09f),6);
                Art.Part(root,"Rear light",new Vector3(side*1.1f,1.0f,-1.85f),new Vector3(.22f,.12f,.08f),7);
            }
            var visual=new TankVisual();visual.Turret=Art.Root("Independent cannon turret",root,new Vector3(0,1.62f,0));
            Art.Part(visual.Turret,"Turret ring",Vector3.zero,new Vector3(2.1f,.22f,2.1f),1,PrimitiveType.Cylinder);
            Art.Part(visual.Turret,"Turret armor",new Vector3(0,.38f,0),new Vector3(2.05f,.85f,2.0f),0);
            Art.Part(visual.Turret,"Turret command light",new Vector3(0,.85f,-.25f),new Vector3(.8f,.09f,.55f),6);
            Art.Part(visual.Turret,"Commander hatch",new Vector3(.52f,.85f,-.45f),new Vector3(.55f,.10f,.55f),2,PrimitiveType.Cylinder);
            visual.Barrel=Art.Root("Cannon elevation",visual.Turret,new Vector3(0,.98f,.50f));
            Art.Part(visual.Barrel,"Cannon mantlet",new Vector3(0,0,.36f),new Vector3(.72f,.65f,.8f),2);
            Art.Part(visual.Barrel,"Cannon barrel",new Vector3(0,0,1.35f),new Vector3(.24f,.24f,2.2f),11);
            Art.Part(visual.Barrel,"Muzzle brake",new Vector3(0,0,2.48f),new Vector3(.43f,.38f,.5f),1);
            Art.Part(visual.Barrel,"Cannon muzzle light",new Vector3(0,0,2.75f),new Vector3(.18f,.16f,.035f),7);
            visual.Muzzle=Art.Root("Tank cannon muzzle",visual.Barrel,new Vector3(0,0,2.83f));
            return visual;
        }
    }
}

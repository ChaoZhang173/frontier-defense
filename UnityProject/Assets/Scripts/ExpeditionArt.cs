using UnityEngine;

namespace Frontier
{
    public static class ExpeditionArt
    {
        public static readonly Color Green=new Color(.35f,1f,.56f);
        static Material accent;
        static Material Accent
        {
            get
            {
                if(accent==null)
                {
                    accent=new Material(Art.Mat(6)){name="Expedition green identification"};
                    accent.color=Green;
                    if(accent.HasProperty("_EmissionColor"))accent.SetColor("_EmissionColor",Green*.8f);
                }
                return accent;
            }
        }
        public static ArtRig Marine(Transform root)
        {
            ArtRig rig=Art.Marine(root,false);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>())
                if(renderer.name=="Shoulder stripe"||renderer.name=="Visor"||renderer.name=="Chest light")renderer.sharedMaterial=Accent;
            var patch=Art.Part(rig.Body,"Expedition pack identification",new Vector3(0,.18f,-.55f),new Vector3(.42f,.18f,.025f),6);
            patch.GetComponent<Renderer>().sharedMaterial=Accent;
            rig.Finish();return rig;
        }
        public static void RallyArea(Transform world)
        {
            Transform root=Art.Root("Expedition rally / six assembly positions",world,Vector3.zero);
            for(int slot=0;slot<ExpeditionDirector.FormationSize;slot++)
            {
                Vector3 point=ExpeditionDirector.FormationPoint(slot)+Vector3.up*.065f;
                for(int side=-1;side<=1;side+=2)
                {
                    var edge=Art.Part(root,"Rally position",point+Vector3.right*(side*.6f),new Vector3(.055f,.025f,1.05f),6);
                    edge.GetComponent<Renderer>().sharedMaterial=Accent;
                    edge=Art.Part(root,"Rally position",point+Vector3.forward*(side*.52f),new Vector3(1.2f,.025f,.055f),6);
                    edge.GetComponent<Renderer>().sharedMaterial=Accent;
                }
            }
        }
    }
}

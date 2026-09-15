using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier
{
    public static class Art
    {
        public static readonly Color Cyan = new Color(.12f, .91f, 1f);
        public static readonly Color Amber = new Color(1f, .64f, .23f);
        public static readonly Color Pink = new Color(1f, .23f, .29f);
        public static readonly Color[] Palette = {
            new Color(.12f,.37f,.52f), new Color(.055f,.12f,.17f), new Color(.22f,.29f,.32f),
            new Color(.25f,.32f,.34f), new Color(.24f,.23f,.22f), new Color(.43f,.30f,.20f),
            Cyan, Amber, new Color(.54f,.16f,.12f), new Color(.27f,.095f,.10f),
            Pink, new Color(.72f,.79f,.77f), new Color(.025f,.13f,.19f), new Color(.52f,.37f,.24f)
        };
        static readonly Dictionary<int, Material> Materials = new Dictionary<int, Material>();
        static Mesh cone;

        public static Material Mat(int index)
        {
            if (Materials.TryGetValue(index, out var mat)) return mat;
            mat = Resources.Load<Material>("Materials/P" + index);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.color = Palette[index]; }
            Materials[index] = mat; return mat;
        }

        public static Transform Root(string name, Transform parent, Vector3 position)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t;
        }

        public static Transform Part(Transform parent, string name, Vector3 position, Vector3 scale, int material,
            PrimitiveType primitive = PrimitiveType.Cube, Vector3 euler = default, bool collision = false)
        {
            var go = GameObject.CreatePrimitive(primitive); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = position;
            go.transform.localScale = scale; go.transform.localRotation = Quaternion.Euler(euler);
            go.GetComponent<Renderer>().sharedMaterial = Mat(material);
            // The typed reference keeps the sphere's native collider in stripped Web builds.
            Collider collider = primitive==PrimitiveType.Sphere?go.GetComponent<SphereCollider>():go.GetComponent<Collider>();
            if (!collision) Object.Destroy(collider); else go.layer = Combat.WorldLayer;
            return go.transform;
        }

        public static Transform Spike(Transform parent, Vector3 position, Vector3 scale, int material, Vector3 euler)
        {
            if (cone == null)
            {
                cone = new Mesh { name = "Six-sided armor spike" };
                var vertices = new List<Vector3>(); var triangles = new List<int>();
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3, b = (i + 1) * Mathf.PI / 3;
                    int k = vertices.Count;
                    vertices.Add(new Vector3(Mathf.Cos(a)*.5f,0,Mathf.Sin(a)*.5f));
                    vertices.Add(Vector3.up); vertices.Add(new Vector3(Mathf.Cos(b)*.5f,0,Mathf.Sin(b)*.5f));
                    triangles.AddRange(new [] { k,k+1,k+2 });
                    k = vertices.Count;
                    vertices.Add(Vector3.zero); vertices.Add(new Vector3(Mathf.Cos(a)*.5f,0,Mathf.Sin(a)*.5f));
                    vertices.Add(new Vector3(Mathf.Cos(b)*.5f,0,Mathf.Sin(b)*.5f));
                    triangles.AddRange(new [] { k,k+2,k+1 });
                }
                cone.SetVertices(vertices); cone.SetTriangles(triangles,0); cone.RecalculateNormals();
            }
            var t = Root("Carapace spike", parent, position); t.localScale = scale; t.localRotation = Quaternion.Euler(euler);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = cone;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mat(material); return t;
        }

        public static void Layer(Transform root, int layer)
        {
            root.gameObject.layer = layer; foreach (Transform t in root) Layer(t, layer);
        }

        public static Transform BuildSiteMarker(Transform parent,Vector3 position,Role role)
        {
            Transform root=Root("Expansion site",parent,position);
            Vector3 size=ConstructionDirector.Size(role);
            for(int side=-1;side<=1;side+=2)
            {
                Part(root,"Site boundary",new Vector3(side*(size.x*.5f+.25f),.045f,0),new Vector3(.12f,.035f,size.z+.6f),6);
                Part(root,"Site boundary",new Vector3(0,.045f,side*(size.z*.5f+.25f)),new Vector3(size.x+.6f,.035f,.12f),6);
                for(int end=-1;end<=1;end+=2)
                    Part(root,"Site beacon",new Vector3(side*(size.x*.5f+.25f),.26f,end*(size.z*.5f+.25f)),new Vector3(.18f,.5f,.18f),7);
            }
            Part(root,"Build marker",new Vector3(0,.05f,0),new Vector3(1.8f,.035f,.13f),6);
            Part(root,"Build marker",new Vector3(0,.05f,0),new Vector3(.13f,.035f,1.8f),6);
            return root;
        }

        public static void ConstructionFrame(Transform root,Role role)
        {
            Vector3 size=ConstructionDirector.Size(role);
            Part(root,"Construction foundation",new Vector3(0,.18f,0),new Vector3(size.x,.36f,size.z),2);
            for(int side=-1;side<=1;side+=2)
            {
                for(int end=-1;end<=1;end+=2)
                {
                    Part(root,"Scaffold upright",new Vector3(side*(size.x*.5f-.18f),size.y*.5f,end*(size.z*.5f-.18f)),new Vector3(.22f,size.y,.22f),7);
                    Part(root,"Scaffold light",new Vector3(side*(size.x*.5f-.18f),size.y+.06f,end*(size.z*.5f-.18f)),new Vector3(.27f,.12f,.27f),6);
                }
                Part(root,"Scaffold beam",new Vector3(side*(size.x*.5f-.18f),size.y,0),new Vector3(.18f,.2f,size.z),7);
                Part(root,"Scaffold beam",new Vector3(0,size.y,side*(size.z*.5f-.18f)),new Vector3(size.x,.2f,.18f),7);
                Part(root,"Worksite barrier",new Vector3(0,.9f,side*(size.z*.5f-.1f)),new Vector3(size.x-.3f,.35f,.18f),7);
            }
            Part(root,"Delivered components",new Vector3(-1,.7f,-.4f),new Vector3(2,1,2),0);
            Part(root,"Delivered components",new Vector3(1.4f,.55f,.7f),new Vector3(1.4f,.7f,1.6f),3);
        }

        public static ArtRig Marine(Transform parent, bool player,bool squad=false)
        {
            var rig = parent.gameObject.AddComponent<ArtRig>(); rig.Humanoid = true;
            rig.Body = Root("Armored upper body",parent,new Vector3(0,1.12f,0));
            Part(rig.Body,"Chest plating",new Vector3(0,.1f,0),new Vector3(.86f,.76f,.52f),0);
            Part(rig.Body,"Chest inset",new Vector3(0,.15f,.277f),new Vector3(.49f,.24f,.035f),1);
            Part(rig.Body,"Chest light",new Vector3(0,.21f,.301f),new Vector3(.27f,.035f,.035f),6);
            Part(rig.Body,"Helmet",new Vector3(0,.73f,.015f),new Vector3(.56f,.52f,.54f),squad?11:0,PrimitiveType.Sphere);
            Part(rig.Body,"Visor",new Vector3(0,.755f,.258f),new Vector3(.44f,.15f,.10f),6);
            Part(rig.Body,"Jaw",new Vector3(0,.54f,.225f),new Vector3(.34f,.15f,.13f),1);
            Part(rig.Body,"Backpack",new Vector3(0,.17f,-.38f),new Vector3(.64f,.67f,.30f),2);
            for(int i=-1;i<=1;i+=2)
            {
                Part(rig.Body,"Shoulder armor",new Vector3(i*.57f,.30f,0),new Vector3(.42f,.48f,.58f),0,PrimitiveType.Cube,new Vector3(0,0,-i*12));
                Part(rig.Body,"Shoulder stripe",new Vector3(i*.58f,.55f,.03f),new Vector3(.28f,.035f,.38f),squad?6:player?7:11);
                Part(rig.Body,"Forearm",new Vector3(i*.44f,-.15f,.30f),new Vector3(.25f,.30f,.59f),1,PrimitiveType.Cube,new Vector3(-12,i*-18,0));
                Transform leg = Root("Leg",parent,new Vector3(i*.25f,.70f,0));
                Part(leg,"Thigh",new Vector3(0,-.18f,0),new Vector3(.31f,.46f,.35f),0);
                Part(leg,"Shin",new Vector3(0,-.49f,.01f),new Vector3(.33f,.34f,.36f),1);
                Part(leg,"Knee guard",new Vector3(0,-.35f,.2f),new Vector3(.29f,.22f,.10f),0);
                Part(leg,"Boot",new Vector3(0,-.64f,.10f),new Vector3(.36f,.14f,.52f),2);
                rig.AddLeg(leg);
            }
            Part(parent,"Belt",new Vector3(0,.89f,0),new Vector3(.67f,.22f,.42f),1);
            rig.Gun = Root("Rifle mount",rig.Body,new Vector3(.39f,-.02f,.25f));
            Part(rig.Gun,"Rifle chassis",new Vector3(0,0,.33f),new Vector3(.21f,.24f,.78f),1);
            Part(rig.Gun,"Rifle barrel",new Vector3(0,.015f,.9f),new Vector3(.10f,.10f,.48f),2);
            Part(rig.Gun,"Magazine",new Vector3(0,-.23f,.24f),new Vector3(.17f,.27f,.22f),2,PrimitiveType.Cube,new Vector3(-14,0,0));
            Part(rig.Gun,"Optic",new Vector3(0,.17f,.36f),new Vector3(.12f,.10f,.26f),6);
            rig.Muzzle = Root("Muzzle",rig.Gun,new Vector3(0,.015f,1.16f));
            if(squad)
            {
                Part(rig.Body,"Squad beacon",new Vector3(.31f,.52f,-.40f),new Vector3(.06f,.62f,.06f),7);
                Part(rig.Body,"Squad identification",new Vector3(0,.17f,-.54f),new Vector3(.42f,.25f,.025f),7);
            }
            rig.Finish(); return rig;
        }

        public static ArtRig Creature(Transform parent, Role role)
        {
            var rig=parent.gameObject.AddComponent<ArtRig>();
            bool large=role==Role.Colossus, ranged=role==Role.Spitter;
            float s=large?1.85f:ranged?1.1f:.82f;
            rig.Body=Root("Carapace",parent,new Vector3(0,large?1.35f:ranged?.86f:.54f,0));
            Part(rig.Body,"Abdomen",new Vector3(0,0,-.22f)*s,new Vector3(1.02f,.72f,1.38f)*s,9,PrimitiveType.Sphere);
            Part(rig.Body,"Front armor",new Vector3(0,.17f,.22f)*s,new Vector3(1.02f,.54f,.86f)*s,8,PrimitiveType.Sphere);
            Part(rig.Body,"Head",new Vector3(0,-.10f,.77f)*s,new Vector3(.58f,.39f,.57f)*s,8,PrimitiveType.Sphere);
            for(int side=-1;side<=1;side+=2)
            {
                Part(rig.Body,"Eye",new Vector3(side*.21f,.02f,1.015f)*s,new Vector3(.15f,.105f,.075f)*s,10);
                Spike(rig.Body,new Vector3(side*.25f,-.15f,.9f)*s,new Vector3(.21f,.48f,.19f)*s,11,new Vector3(80,side*25,0));
                for(int i=0;i<3;i++)
                {
                    Transform leg=Root("Articulated limb",parent,new Vector3(side*.44f*s,rig.Body.localPosition.y-.18f,(i-1)*.53f*s));
                    Part(leg,"Upper limb",new Vector3(side*.31f*s,-.08f,0),new Vector3(.68f,.15f,.17f)*s,8,PrimitiveType.Cube,new Vector3(0,(i-1)*side*12,-side*17));
                    Part(leg,"Lower limb",new Vector3(side*.56f*s,-.34f*s,.09f),new Vector3(.16f,.64f,.16f)*s,9,PrimitiveType.Cube,new Vector3(-18,0,-side*18));
                    rig.AddLeg(leg);
                }
            }
            int spikes=large?5:3;
            for(int i=0;i<spikes;i++) Spike(rig.Body,new Vector3(0,.34f,(-.56f+i*.3f))*s,new Vector3(.34f,large?.79f:.5f,.30f)*s,large?11:8,new Vector3(-24,0,0));
            if(ranged)
            {
                Part(rig.Body,"Acid organ",new Vector3(0,.33f,-.40f),new Vector3(.58f,.50f,.76f),10,PrimitiveType.Sphere);
                for(int i=-1;i<=1;i+=2) Spike(rig.Body,new Vector3(i*.32f,.36f,-.50f),new Vector3(.16f,.60f,.16f),8,new Vector3(-25,0,i*-20));
            }
            if(large)
            {
                Part(rig.Body,"Siege plate",new Vector3(0,.47f,.1f)*s,new Vector3(.94f,.28f,.82f)*s,13);
                Part(rig.Body,"Exposed core",new Vector3(0,.65f,.18f)*s,new Vector3(.35f,.09f,.35f)*s,10,PrimitiveType.Sphere);
            }
            rig.Finish();return rig;
        }

        public static void BuildEnvironment(Transform world)
        {
            Part(world,"Canyon basin",new Vector3(0,-4.6f,22),new Vector3(250,1,240),5,collision:true);
            Part(world,"Highland foundation",new Vector3(0,-2.2f,1),new Vector3(58,4f,50),4,collision:true);
            Part(world,"Defense deck",new Vector3(0,-.10f,1),new Vector3(57.8f,.2f,49.8f),3,collision:true);
            for(int x=-24;x<=24;x+=6) for(int z=-20;z<=22;z+=6)
            {
                Part(world,"Deck expansion seam",new Vector3(x,.014f,z),new Vector3(.035f,.015f,5.95f),2);
                Part(world,"Deck expansion seam",new Vector3(x+3,.014f,z-3),new Vector3(5.95f,.015f,.035f),2);
            }
            for(int lane=0;lane<2;lane++)
            {
                float x=lane==0?-13.5f:13.5f;
                Ramp(world,x);
                for(int z=-1;z<25;z+=3) Part(world,"Lane guidance",new Vector3(x,.027f,z),new Vector3(.16f,.02f,1.4f),7);
                for(int i=-1;i<=1;i+=2)
                {
                    Part(world,"Gate pier",new Vector3(x+i*4.6f,2.2f,24.2f),new Vector3(.75f,4.4f,1.2f),1,collision:true);
                    Part(world,"Gate beacon",new Vector3(x+i*4.6f,4.48f,24.2f),new Vector3(.50f,.18f,.73f),6);
                }
                Part(world,"Gate crossbeam",new Vector3(x,4.45f,24.2f),new Vector3(10,.46f,.75f),0,collision:true);
                Part(world,"Gate light",new Vector3(x,4.18f,23.82f),new Vector3(7.6f,.06f,.08f),6);
                Transform portal=Root("Rift "+lane,world,new Vector3(x,-4,64));
                Part(portal,"Rift base",Vector3.zero,new Vector3(6,.15f,6),9,PrimitiveType.Cylinder);
                Part(portal,"Rift glow",new Vector3(0,.16f,0),new Vector3(4.7f,.03f,4.7f),10,PrimitiveType.Cylinder);
                for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI/4;
                    Spike(portal,new Vector3(Mathf.Cos(a)*3,0,Mathf.Sin(a)*3),new Vector3(.9f,3.2f+UnityEngine.Random.value*2,.9f),9,new Vector3(10,i*45,0));
                }
            }
            Wall(world,-24,26,10,3.0f);Wall(world,0,26,18,CentralDefense.WallHeight);Wall(world,24,26,10,3.0f);
            for(int slot=0;slot<4;slot++)
            {
                Vector3 post=CentralDefense.SquadPost(slot);
                Part(world,"Central firing position",post+Vector3.up*.03f,new Vector3(1.1f,.02f,.6f),7);
            }
            Part(world,"Central tank deployment",new Vector3(0,.03f,21),new Vector3(3.8f,.02f,.14f),6);
            for(int side=-1;side<=1;side+=2)
            {
                Part(world,"Perimeter barrier",new Vector3(side*28.1f,1.25f,1),new Vector3(1.6f,2.5f,49),2,collision:true);
                for(int z=-21;z<=23;z+=8)
                {
                    Part(world,"Wall support",new Vector3(side*27.7f,1.75f,z),new Vector3(2.4f,3.5f,1),1,collision:true);
                    Part(world,"Perimeter lamp",new Vector3(side*26.9f,2.45f,z),new Vector3(.10f,.35f,.5f),6);
                }
            }
            Part(world,"Rear barrier",new Vector3(0,1.4f,-23.3f),new Vector3(57,2.8f,1.5f),2,collision:true);
            for(int i=0;i<65;i++)
            {
                float x=UnityEngine.Random.Range(-110f,110f),z=UnityEngine.Random.Range(-55f,125f);
                if(Mathf.Abs(x)<33 && z<82) continue;
                float h=UnityEngine.Random.Range(5f,24f),s=UnityEngine.Random.Range(5f,12f);
                Part(world,"Canyon monolith",new Vector3(x,-4+h*.4f,z),new Vector3(s,h,s*.75f),i%3==0?13:4,PrimitiveType.Cube,new Vector3(UnityEngine.Random.Range(-12,12),UnityEngine.Random.Range(0,180),UnityEngine.Random.Range(-9,9)));
                Part(world,"Eroded cap",new Vector3(x,-4+h*.84f,z),new Vector3(s*1.2f,h*.18f,s*.88f),5,PrimitiveType.Cube,new Vector3(0,UnityEngine.Random.Range(0,180),8));
            }
            for(int i=0;i<25;i++)
            {
                float x=UnityEngine.Random.Range(-65,65),z=UnityEngine.Random.Range(33,90);
                if(Mathf.Min(Mathf.Abs(x-13.5f),Mathf.Abs(x+13.5f))<7)continue;
                if(System.Array.Exists(GameWorld.EnemySpawnPoints,p=>new Vector2(p.x-x,p.z-z).sqrMagnitude<49))continue;
                Part(world,"Basalt shard",new Vector3(x,-3.4f,z),new Vector3(2,1.8f,2.6f),4,PrimitiveType.Cube,new Vector3(14,UnityEngine.Random.Range(0,180),25));
            }
            for(int i=0;i<6;i++)
            {
                var p=new Vector3(-23+(i%2)*2.1f,.8f,-18+(i/2)*2.3f);
                Part(world,"Supply container",p,new Vector3(1.8f,1.6f,1.9f),i%2==0?0:2,collision:true);
                Part(world,"Container stripe",p+new Vector3(0,0,.96f),new Vector3(1.3f,.14f,.02f),7);
            }
            Transform reactor=Root("Auxiliary generator",world,new Vector3(19,0,-3));
            Part(reactor,"Reactor pad",new Vector3(0,.2f,0),new Vector3(5,.4f,5),1,collision:true);
            Part(reactor,"Reactor casing",new Vector3(0,1.6f,0),new Vector3(2.7f,1.3f,2.7f),0,PrimitiveType.Cylinder,collision:true);
            Part(reactor,"Reactor core",new Vector3(0,3,0),new Vector3(1.3f,.40f,1.3f),6,PrimitiveType.Cylinder);
            Part(reactor,"Reactor crown",new Vector3(0,3.6f,0),new Vector3(2.5f,.22f,2.5f),2,PrimitiveType.Cylinder);
        }

        static void Wall(Transform world,float x,float z,float width,float height)
        {
            float depth=height<1?.6f:1.8f;
            Part(world,"North fortification",new Vector3(x,height/2,z),new Vector3(width,height,depth),2,collision:true);
            float railHeight=height<1?.075f:.25f;
            Part(world,"Armor rail",new Vector3(x,height+railHeight*.5f,z),new Vector3(width+.2f,railHeight,depth+.1f),1);
            for(float dx=-width/2+1;dx<width/2;dx+=3)
                Part(world,"Warning stripe",new Vector3(x+dx,height*.6f,z-depth*.5f-.02f),new Vector3(.32f,.45f,.03f),7,euler:new Vector3(0,0,25));
        }

        static void Ramp(Transform world,float x)
        {
            var t=Root("Approach ramp",world,new Vector3(x,0,0));
            float half=GroundSurface.RampHalfWidth,side=GroundSurface.SideWidth,top=GroundSurface.RampStart,bottom=GroundSurface.RampEnd;
            var mesh=new Mesh {name="Three-sided highland access ramp"};
            mesh.vertices=new [] {
                new Vector3(-half,0,top),new Vector3(half,0,top),
                new Vector3(-half,-4,bottom),new Vector3(half,-4,bottom),
                new Vector3(-half-side,-4,top),new Vector3(half+side,-4,top),
                new Vector3(-half,0,top-.2f),new Vector3(half,0,top-.2f)
            };
            // The small level landing overlaps the deck, which ends at z = 25.9.
            // Central approach plus two side faces whose entire outer edge meets the canyon floor.
            mesh.triangles=new [] {0,2,1,1,2,3,0,4,2,1,3,5,6,0,7,7,0,1};mesh.RecalculateNormals();
            mesh.uv=new [] {Vector2.zero,Vector2.right,Vector2.up,Vector2.one,new Vector2(-1,0),new Vector2(2,0),new Vector2(0,-.02f),new Vector2(1,-.02f)};
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=Mat(3);
            t.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;t.gameObject.layer=Combat.WorldLayer;
            for(int i=0;i<10;i++)
            {
                float z=26.5f+i*1.35f;
                Part(world,"Ramp tread",new Vector3(x,GroundSurface.Height(x,z)+.02f,z),new Vector3(8.7f,.045f,.15f),i%3==0?7:2,euler:new Vector3(15.94f,0,0));
            }
            for(int sign=-1;sign<=1;sign+=2)
            {
                Vector3 normal=new Vector3(sign*4f/side,1,4f/(bottom-top)).normalized;
                Vector3 rotation=Quaternion.FromToRotation(Vector3.up,normal).eulerAngles;
                for(int mark=0;mark<5;mark++)
                {
                    float sideX=x+sign*(half+.65f+mark*.75f),z=29;
                    Part(world,"Side ramp guidance",new Vector3(sideX,GroundSurface.Height(sideX,z)+.035f,z),new Vector3(.46f,.035f,.55f),mark%2==0?7:2,euler:rotation);
                }
            }
        }

        public static void CommandBuilding(Transform root)
        {
            Part(root,"Command platform",new Vector3(0,.3f,0),new Vector3(10,.6f,9),1);
            Part(root,"Command armored hull",new Vector3(0,1.9f,0),new Vector3(8.7f,3.2f,7.6f),0);
            Part(root,"Upper tier",new Vector3(0,3.65f,-.2f),new Vector3(6.7f,1.05f,5.4f),2);
            Part(root,"Bridge roof",new Vector3(0,4.45f,-.7f),new Vector3(5.8f,.55f,3.4f),0);
            Part(root,"Bridge glass",new Vector3(0,3.8f,2.54f),new Vector3(5.5f,.6f,.04f),6);
            Part(root,"Bulkhead",new Vector3(0,1.35f,3.82f),new Vector3(2.7f,2.2f,.18f),1);
            Part(root,"Door inset",new Vector3(0,1.45f,3.94f),new Vector3(2.1f,1.78f,.03f),2);
            Part(root,"Door seam",new Vector3(0,1.45f,3.97f),new Vector3(.045f,1.68f,.02f),6);
            for(int s=-1;s<=1;s+=2)
            {
                Part(root,"Wing armor",new Vector3(s*4.55f,1.35f,.3f),new Vector3(1.1f,2.6f,6.5f),1,euler:new Vector3(0,0,s*8));
                Part(root,"Landing brace",new Vector3(s*4.6f,.4f,2.5f),new Vector3(2,.8f,2),2);
                for(int i=0;i<3;i++)Part(root,"Command radiator",new Vector3(s*4.46f,2,-2+i*1.6f),new Vector3(.20f,1.1f,.66f),7);
            }
            Part(root,"Comms mast",new Vector3(-1.4f,6,-1),new Vector3(.14f,3.2f,.14f),2);
            Part(root,"Mast light",new Vector3(-1.4f,7.63f,-1),new Vector3(.23f,.20f,.23f),6,PrimitiveType.Sphere);
            Part(root,"Comms dish",new Vector3(1.55f,5.3f,-.7f),new Vector3(2.3f,.18f,2.3f),11,PrimitiveType.Cylinder,new Vector3(30,0,18));
        }

        public static void BarracksBuilding(Transform root)
        {
            Part(root,"Barracks pad",new Vector3(0,.2f,0),new Vector3(8,.4f,9),1);
            Part(root,"Barracks hull",new Vector3(0,1.9f,0),new Vector3(7,3.4f,7.8f),0);
            Part(root,"Barracks roof",new Vector3(0,3.7f,0),new Vector3(7.6f,.45f,8.3f),2);
            Part(root,"Deployment door",new Vector3(0,1.5f,3.96f),new Vector3(3.7f,2.65f,.12f),1);
            Part(root,"Deployment glow",new Vector3(0,2.92f,4.04f),new Vector3(3.8f,.10f,.10f),6);
            for(int i=-1;i<=1;i+=2)
            {
                Part(root,"Door bumper",new Vector3(i*2.14f,1.35f,4.04f),new Vector3(.36f,2.7f,.33f),7);
                Part(root,"Barracks ventilation",new Vector3(i*1.8f,4.07f,-1),new Vector3(2,.35f,3.6f),1);
                for(int j=0;j<5;j++)Part(root,"Vent fin",new Vector3(i*1.8f,4.28f,-2.3f+j*.58f),new Vector3(1.6f,.12f,.12f),11);
            }
            Part(root,"Identification stripe",new Vector3(0,3.94f,2.2f),new Vector3(6,.04f,.36f),7);
        }

        public static ArtRig BunkerBuilding(Transform root)
        {
            Part(root,"Bunker foundation",new Vector3(0,.28f,0),new Vector3(7.4f,.56f,6.1f),1);
            Part(root,"Bunker body",new Vector3(0,1.35f,0),new Vector3(6.7f,2.2f,5.4f),2);
            Part(root,"Bunker roof",new Vector3(0,2.6f,0),new Vector3(7.3f,.50f,6),0);
            Part(root,"Firing slit",new Vector3(0,1.8f,2.73f),new Vector3(5.6f,.30f,.10f),1);
            for(int i=-1;i<=1;i++)Part(root,"Slit light",new Vector3(i*1.8f,1.8f,2.79f),new Vector3(.55f,.09f,.03f),6);
            Part(root,"Garrison hatch",new Vector3(0,1.15f,-2.76f),new Vector3(1.7f,2.1f,.13f),1);
            Part(root,"Hatch inset",new Vector3(0,1.17f,-2.85f),new Vector3(1.32f,1.73f,.08f),12);
            for(int side=-1;side<=1;side+=2)
                Part(root,"Hatch guide",new Vector3(side*.98f,1.1f,-2.91f),new Vector3(.11f,2.14f,.14f),6);
            Part(root,"Hatch header",new Vector3(0,2.24f,-2.9f),new Vector3(2.05f,.16f,.17f),7);
            for(int i=0;i<4;i++)
                Part(root,"Four-seat marker",new Vector3(-.48f+i*.32f,2.25f,-3.01f),new Vector3(.14f,.07f,.03f),1);
            for(int i=0;i<3;i++)
                Part(root,"Boarding floor guide",new Vector3(0,.025f,-3.3f-i*.55f),new Vector3(1.25f,.04f,.16f),7);
            var rig=root.gameObject.AddComponent<ArtRig>();
            rig.Body=Root("Turret swivel",root,new Vector3(0,3.1f,0));
            Part(rig.Body,"Turret armor",Vector3.zero,new Vector3(1.6f,.58f,1.8f),1);
            for(int i=-1;i<=1;i+=2)Part(rig.Body,"Twin autocannon",new Vector3(i*.43f,.1f,1.2f),new Vector3(.18f,.18f,2.1f),2);
            rig.Muzzle=Root("Autocannon muzzle",rig.Body,new Vector3(.43f,.1f,2.3f));
            rig.Turret=true;rig.Finish();return rig;
        }
    }

    public sealed class ArtRig : MonoBehaviour
    {
        public Transform Body, Gun, Muzzle;
        public bool Humanoid, Turret;
        readonly List<Transform> legs=new List<Transform>();
        readonly List<Quaternion> rest=new List<Quaternion>();
        Vector3 bodyRest, gunRest;
        float phase,recoil,flash;
        Renderer[] renderers;
        MaterialPropertyBlock block;
        public void AddLeg(Transform leg){legs.Add(leg);rest.Add(leg.localRotation);}
        public void Finish(){bodyRest=Body.localPosition;if(Gun!=null)gunRest=Gun.localPosition;renderers=GetComponentsInChildren<Renderer>();block=new MaterialPropertyBlock();}
        public void Flash(){flash=.10f;}
        public void Recoil(){recoil=1;}
        public void AimAt(Vector3 target)
        {
            if(Turret)
            {
                Vector3 d=target-Body.position;d.y=0;
                if(d.sqrMagnitude>.01f)Body.rotation=Quaternion.LookRotation(d);
                return;
            }
            if(!Humanoid)return;
            Vector3 local=transform.InverseTransformPoint(target);
            float pitch=-Mathf.Atan2(local.y-1.25f,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg;
            Body.localRotation=Quaternion.Euler(Mathf.Clamp(pitch,-48,38)*.7f,0,0);
        }
        public void Animate(float speed,float dt)
        {
            phase+=dt*speed*(Humanoid?2.9f:4.8f);
            float amount=Mathf.Clamp01(speed/2.4f);
            for(int i=0;i<legs.Count;i++)
            {
                float swing=Mathf.Sin(phase+i*(Humanoid?Mathf.PI:2.6f))*amount;
                legs[i].localRotation=rest[i]*Quaternion.Euler(Humanoid?swing*29:swing*12,0,Humanoid?0:swing*13);
            }
            if(!Turret)Body.localPosition=bodyRest+Vector3.up*Mathf.Abs(Mathf.Sin(phase))*amount*.045f;
            recoil=Mathf.MoveTowards(recoil,0,dt*10);
            if(Gun!=null)Gun.localPosition=gunRest-Vector3.forward*recoil*.10f;
            bool was=flash>0;flash=Mathf.Max(0,flash-dt);
            if(was && renderers!=null)
            {
                foreach(var r in renderers)
                {
                    if(r==null)continue;
                    if(flash>0){block.SetColor("_BaseColor",Color.Lerp(r.sharedMaterial.color,Color.white,.75f));r.SetPropertyBlock(block);}
                    else r.SetPropertyBlock(null);
                }
            }
        }
    }
}

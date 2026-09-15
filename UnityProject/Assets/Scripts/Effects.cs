using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier
{
    public sealed class Effects : MonoBehaviour
    {
        sealed class RayEffect { public LineRenderer Line; public float Life,Total; }
        sealed class Mote { public Transform Body; public Vector3 Velocity; public float Life,Total,Scale; }
        sealed class Projectile { public Transform Body; public Health Owner; public Faction Faction; public Vector3 Velocity; public float Damage,Life; }
        readonly List<RayEffect> rays=new List<RayEffect>();
        readonly List<Mote> motes=new List<Mote>();
        readonly List<Projectile> bolts=new List<Projectile>();
        Material lineMaterial;int rayIndex,moteIndex;
        public void Setup()
        {
            lineMaterial=Resources.Load<Material>("EffectsGlow");
            for(int i=0;i<70;i++)
            {
                var go=new GameObject("Pooled tracer");go.transform.SetParent(transform);
                var line=go.AddComponent<LineRenderer>();line.sharedMaterial=lineMaterial;line.positionCount=2;line.useWorldSpace=true;
                line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;
                rays.Add(new RayEffect{Line=line});
            }
            for(int i=0;i<130;i++)
            {
                Transform p=Art.Part(transform,"Pooled spark",Vector3.zero,Vector3.one,7);
                p.gameObject.SetActive(false);motes.Add(new Mote{Body=p});
            }
        }
        public void Tracer(Vector3 start,Vector3 end,Color color,float width)
        {
            var r=rays[rayIndex++%rays.Count];r.Line.enabled=true;r.Line.SetPosition(0,start);r.Line.SetPosition(1,end);
            r.Line.startColor=r.Line.endColor=color;r.Line.startWidth=width;r.Line.endWidth=width*.35f;r.Life=r.Total=.055f;
        }
        public void Spark(Vector3 pos,Color color,float size){Emit(pos,color,size,3);}
        public void Burst(Vector3 pos,Color color,float size){Emit(pos,color,size,Mathf.Clamp(Mathf.RoundToInt(size*12),6,32));}
        void Emit(Vector3 pos,Color color,float size,int count)
        {
            for(int i=0;i<count;i++)
            {
                var m=motes[moteIndex++%motes.Count];m.Body.gameObject.SetActive(true);m.Body.position=pos+Random.insideUnitSphere*size*.2f;
                m.Body.rotation=Random.rotation;m.Velocity=(Random.insideUnitSphere+Vector3.up*.5f)*Mathf.Clamp(size*4,1,9);
                m.Life=m.Total=Random.Range(.18f,.48f);m.Scale=Mathf.Clamp(size*.16f,.035f,.27f);m.Body.localScale=Vector3.one*m.Scale;
                m.Body.GetComponent<Renderer>().sharedMaterial=Art.Mat(color==Art.Cyan?6:color==Art.Pink?10:7);
            }
        }
        public void Bolt(Vector3 start,Vector3 end,Health source,float damage,float speed)
        {
            if(bolts.Count>=45)return;
            Transform b=Art.Part(transform,"Acid projectile",start,new Vector3(.22f,.22f,.5f),10,PrimitiveType.Sphere);
            Vector3 d=(end-start).normalized;b.rotation=Quaternion.LookRotation(d);
            bolts.Add(new Projectile{Body=b,Owner=source,Faction=source.Faction,Velocity=d*speed,Damage=damage,Life=3});
        }
        public void Tick(float dt)
        {
            if(dt<=0)return;
            foreach(var r in rays)if(r.Life>0){r.Life-=dt;if(r.Life<=0)r.Line.enabled=false;}
            foreach(var m in motes)if(m.Life>0)
            {
                m.Life-=dt;if(m.Life<=0){m.Body.gameObject.SetActive(false);continue;}
                m.Body.position+=m.Velocity*dt;m.Velocity+=Vector3.down*9*dt;
                m.Body.localScale=Vector3.one*m.Scale*Mathf.Clamp01(m.Life/m.Total);
            }
            for(int i=bolts.Count-1;i>=0;i--)
            {
                var p=bolts[i];Vector3 delta=p.Velocity*dt;p.Life-=dt;
                if(Combat.Trace(p.Body.position,delta.normalized,delta.magnitude+.13f,p.Owner,out var hit))
                {
                    var target=hit.collider.GetComponentInParent<Health>();if(target!=null && target.Faction!=p.Faction)target.Damage(p.Damage,p.Owner);
                    Spark(hit.point,Art.Pink,.35f);p.Life=0;
                }
                if(p.Life<=0){Destroy(p.Body.gameObject);bolts.RemoveAt(i);}else p.Body.position+=delta;
            }
        }
    }
}

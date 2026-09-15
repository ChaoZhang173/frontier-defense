using System;
using UnityEngine;

namespace Frontier
{
    public enum SoundId { Rifle, Reload, Wave, Upgrade, Win, Down, Hit, Critical, Kill, Command, Ready }
    public sealed class Soundscape : MonoBehaviour
    {
        readonly AudioClip[] clips=new AudioClip[11];
        AudioSource effects, ambient;float lastDistant=-10;
        public void Setup()
        {
            effects=gameObject.AddComponent<AudioSource>();effects.spatialBlend=0;effects.playOnAwake=false;
            ambient=gameObject.AddComponent<AudioSource>();ambient.spatialBlend=0;ambient.playOnAwake=false;ambient.loop=true;
            var random=new System.Random(1731);
            for(int k=0;k<clips.Length;k++)
            {
                float duration=k==0?.17f:k==1?.28f:k==4?2.2f:k==5?1.4f:k>=6?(k==9?.32f:.13f):.8f;
                int n=(int)(22050*duration);float[] data=new float[n];
                for(int i=0;i<n;i++)
                {
                    float t=i/22050f,u=t/duration,noise=(float)(random.NextDouble()*2-1),v;
                    if(k==0)v=noise*.42f*Mathf.Exp(-t*55)+(Mathf.Sin(t*2*Mathf.PI*(170-100*u))*.58f+Mathf.Sin(t*2*Mathf.PI*820)*.12f)*Mathf.Exp(-t*28);
                    else if(k==1)v=noise*.25f*Mathf.Pow(Mathf.Abs(Mathf.Sin(t*55)),14)*Mathf.Exp(-t*7);
                    else if(k>=6)
                    {
                        float freq=k==6?950:k==7?1450:k==8?650:k==9?500:1800;
                        if(k==7||k==8||k==9)freq*=u>.45f?1.5f:1;
                        v=Mathf.Sin(t*freq*2*Mathf.PI)*Mathf.Sin(u*Mathf.PI)*Mathf.Exp(-u*2)*.45f;
                    }
                    else
                    {
                        float freq=k==2?220:k==3?440:k==4?330:150;
                        if(k==3||k==4)freq*=new float[]{1,1.25f,1.5f,2}[Mathf.Min(3,(int)(u*4))];
                        if(k==5)freq*=1-u*.6f;
                        v=(Mathf.Sin(t*freq*2*Mathf.PI)+Mathf.Sin(t*freq*3*Mathf.PI)*.25f)*Mathf.Sin(u*Mathf.PI)*.3f;
                    }
                    data[i]=v*.65f;
                }
                clips[k]=AudioClip.Create(((SoundId)k).ToString(),n,1,22050,false);clips[k].SetData(data,0);
            }
            float[] hum=new float[22050*4];
            for(int i=0;i<hum.Length;i++){float t=i/22050f;hum[i]=(Mathf.Sin(t*55*2*Mathf.PI)*.3f+Mathf.Sin(t*82.5f*2*Mathf.PI)*.15f)*(.65f+.25f*Mathf.Sin(t*Mathf.PI/2));}
            ambient.clip=AudioClip.Create("Outpost ventilation",hum.Length,1,22050,false);ambient.clip.SetData(hum,0);
        }
        public void Play(SoundId id,float volume)
        {
            if(GameWorld.I.Diagnostic)return;
            effects.PlayOneShot(clips[(int)id],volume*GameWorld.I.MasterVolume);
            if(!ambient.isPlaying)ambient.Play();
        }
        public void DistantShot(Vector3 position)
        {
            if(GameWorld.I.SimTime-lastDistant<.09f)return;lastDistant=GameWorld.I.SimTime;
            float distance=Vector3.Distance(position,GameWorld.I.Player.transform.position);
            Play(SoundId.Rifle,.15f*Mathf.Clamp01(1-distance/65));
        }
        public void Tick(){ambient.volume=GameWorld.I.MasterVolume*(GameWorld.I.Paused?.012f:.026f);}
        void OnDestroy(){foreach(var clip in clips)if(clip!=null)Destroy(clip);if(ambient!=null&&ambient.clip!=null)Destroy(ambient.clip);}
    }
}

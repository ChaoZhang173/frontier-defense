using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UniversalRenderPipelineAsset = UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
using UnityEngine.SceneManagement;

namespace Frontier
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameWorld : MonoBehaviour
    {
        public static GameWorld I { get; private set; }
        public static bool DeployOnLoad;
        public readonly List<Health> Entities = new List<Health>(100);
        public Health Command, Barracks, Bunker;
        public PlayerMotor Player;
        public Effects Effects;
        public Soundscape Sounds;
        public GameHud Hud;
        public RuntimeChecks Checks;
        public Phase Phase = Phase.Briefing;
        public bool Paused, Diagnostic;
        public int Wave, Credits = 80, WeaponLevel, DefenseLevel, Kills, PlayerKills, Deaths;
        public int EnemiesAlive, MarinesAlive, SpawnedThisWave, TotalSpawned, PeakEnemies, PeakMarines;
        public float SimTime, IntermissionLeft, ProductionLeft = 10, RespawnLeft;
        public float ToastLeft, WaveBannerLeft;
        public string Toast = "", Banner = "", Subtitle = "";
        public bool Ended => Phase == Phase.Victory || Phase == Phase.Defeat;
        public bool Active => !Paused && Phase != Phase.Briefing && !Ended;
        public int RemainingToSpawn => orders == null ? 0 : orders.Count - nextSpawn;
        public int EnemyCountForWave => Wave > 0 ? WavePlan.Counts[Wave - 1] : 0;
        public float RifleDamage => 25 + WeaponLevel * 6;
        public float MasterVolume = .55f;
        public float Sensitivity = 1.5f;
        public int Quality = 1;
        List<SpawnOrder> orders;
        int nextSpawn;
        float spawnLeft, missionDuration;
        readonly Health[] squad = new Health[8];
        public static readonly Vector3[] DefensePosts = {
            new Vector3(-13.5f,0,15.5f),new Vector3(13.5f,0,15.5f),
            new Vector3(-16.2f,0,12.5f),new Vector3(16.2f,0,12.5f),
            new Vector3(-11f,0,12.5f),new Vector3(11f,0,12.5f),
            new Vector3(-15,0,18.5f),new Vector3(15,0,18.5f)
        };

        void Awake()
        {
            I = this;
            UnityEngine.Random.InitState(741903);
            Time.timeScale = 1;
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            MasterVolume = PlayerPrefs.GetFloat("Frontier.Volume", .55f);
            Sensitivity = PlayerPrefs.GetFloat("Frontier.Sensitivity", 1.5f);
            Quality = PlayerPrefs.GetInt("Frontier.Quality", 1);
            Diagnostic = RuntimeChecks.Requested;
            ConfigureLighting();
            Art.BuildEnvironment(transform);
            Command = CreateBuilding(Role.Command, new Vector3(0,0,-7), 2300, new Vector3(9.5f,5.3f,8.2f));
            Barracks = CreateBuilding(Role.Barracks, new Vector3(-14,0,-9), 1100, new Vector3(7.5f,4.2f,8.3f));
            Bunker = CreateBuilding(Role.Bunker, new Vector3(14.5f,0,6), 1200, new Vector3(7.4f,3,6.1f));
            Effects = gameObject.AddComponent<Effects>(); Effects.Setup();
            Sounds = gameObject.AddComponent<Soundscape>(); Sounds.Setup();
            SpawnPlayer();
            for(int i=0;i<4;i++) SpawnMarine(i, false);
            Hud = gameObject.AddComponent<GameHud>();
            ApplyQuality(Quality);
            if(Diagnostic) { Checks = gameObject.AddComponent<RuntimeChecks>(); Checks.Setup(this); }
            else if(DeployOnLoad) { DeployOnLoad=false; BeginCampaign(); }
        }

        void ConfigureLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.36f,.49f,.58f);
            RenderSettings.ambientEquatorColor = new Color(.27f,.31f,.34f);
            RenderSettings.ambientGroundColor = new Color(.18f,.14f,.12f);
            RenderSettings.ambientIntensity = 1.0f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.38f,.39f,.39f); RenderSettings.fogDensity = .0052f;
            var sun = new GameObject("Low orbital sun").AddComponent<Light>(); sun.transform.SetParent(transform);
            sun.type=LightType.Directional;sun.color=new Color(1f,.80f,.60f);sun.intensity=2.2f;
            sun.transform.rotation=Quaternion.Euler(36,-32,0);sun.shadows=LightShadows.Soft;sun.shadowStrength=.8f;
            RenderSettings.sun=sun;
            Material sky=Resources.Load<Material>("FrontierSky"); if(sky!=null)RenderSettings.skybox=sky;
            QualitySettings.shadowDistance=72;QualitySettings.vSyncCount=0;
        }

        public void ApplyQuality(int level)
        {
            Quality=Mathf.Clamp(level,0,2);QualitySettings.SetQualityLevel(Mathf.Min(Quality,QualitySettings.names.Length-1),false);
            QualitySettings.shadowDistance=Quality==0?0:Quality==1?55:85;
            QualitySettings.shadows=Quality==0?ShadowQuality.Disable:ShadowQuality.All;
            QualitySettings.shadowResolution=Quality==2?ShadowResolution.High:ShadowResolution.Medium;
            if(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
            {
                pipeline.shadowDistance=Quality==0?0:Quality==1?55:85;
                pipeline.renderScale=Quality==0?.8f:1;pipeline.msaaSampleCount=Quality==2?4:2;
            }
            Application.targetFrameRate=60;
            PlayerPrefs.SetInt("Frontier.Quality",Quality);
        }

        Health CreateBuilding(Role role,Vector3 position,float hp,Vector3 size)
        {
            Transform root=Art.Root(role.ToString(),transform,position);
            Health h=root.gameObject.AddComponent<Health>();h.Setup(role,Faction.Defense,hp,size.y*.52f,Mathf.Max(size.x,size.z)*.45f);
            var collider=root.gameObject.AddComponent<BoxCollider>();collider.size=size;collider.center=new Vector3(0,size.y/2,0);
            root.gameObject.layer=Combat.WorldLayer;
            if(role==Role.Command)Art.CommandBuilding(root);
            if(role==Role.Barracks)Art.BarracksBuilding(root);
            if(role==Role.Bunker)
            {
                h.Rig=Art.BunkerBuilding(root);
                var brain=root.gameObject.AddComponent<ActorBrain>();brain.Setup(h,position);h.AimHeight=3.2f;
            }
            return h;
        }

        void SpawnPlayer()
        {
            Transform root=Art.Root("Vanguard / player",transform,new Vector3(0,.15f,3));
            root.gameObject.layer=Combat.PlayerLayer;
            Health h=root.gameObject.AddComponent<Health>();h.Setup(Role.Player,Faction.Defense,180,1.25f,.4f);
            h.Rig=Art.Marine(root,true);
            var cc=root.gameObject.AddComponent<CharacterController>();cc.center=new Vector3(0,1,0);cc.height=2;cc.radius=.36f;cc.stepOffset=.3f;cc.slopeLimit=48;
            Player=root.gameObject.AddComponent<PlayerMotor>();Player.Setup(h,cc);
            Physics.IgnoreLayerCollision(Combat.PlayerLayer,Combat.UnitLayer,true);
        }

        public Health SpawnMarine(int slot=-1,bool deploy=true)
        {
            if(MarinesAlive>=8)return null;
            if(slot<0)for(int i=0;i<squad.Length;i++)if(squad[i]==null||!squad[i].Alive){slot=i;break;}
            if(slot<0)return null;
            Vector3 spawn=deploy?new Vector3(-14+(slot%3-1)*.5f,0,-3.8f):DefensePosts[slot];
            Transform root=Art.Root("Auto-deployed marine",transform,spawn);
            Health h=root.gameObject.AddComponent<Health>();h.Setup(Role.Marine,Faction.Defense,170,1.2f,.5f);h.Lane=slot%2;h.Slot=slot;
            h.Rig=Art.Marine(root,false);
            var col=root.gameObject.AddComponent<CapsuleCollider>();col.center=new Vector3(0,1,0);col.height=2;col.radius=.42f;col.isTrigger=true;root.gameObject.layer=Combat.UnitLayer;
            var brain=root.gameObject.AddComponent<ActorBrain>();brain.Setup(h,DefensePosts[slot]);
            squad[slot]=h;MarinesAlive++;PeakMarines=Mathf.Max(PeakMarines,MarinesAlive);
            if(deploy)Effects.Burst(spawn+Vector3.up*.1f,Art.Cyan,.8f);
            return h;
        }

        public Health SpawnEnemy(Role role,int lane,Vector3? overridePosition=null)
        {
            float laneX=lane==0?-13.5f:13.5f;
            Vector3 p=overridePosition??new Vector3(laneX+UnityEngine.Random.Range(-2f,2f),-4,62+UnityEngine.Random.Range(-1f,1f));
            Transform root=Art.Root(role.ToString(),transform,p);root.rotation=Quaternion.Euler(0,180,0);
            float hp=role==Role.Colossus?790:role==Role.Spitter?160:95;
            float height=role==Role.Colossus?1.8f:role==Role.Spitter?.95f:.6f;
            float radius=role==Role.Colossus?1.12f:role==Role.Spitter?.68f:.52f;
            Health h=root.gameObject.AddComponent<Health>();h.Setup(role,Faction.Invaders,hp,height,radius);h.Lane=lane;
            h.Rig=Art.Creature(root,role);
            var col=root.gameObject.AddComponent<CapsuleCollider>();col.radius=radius;col.height=height*1.8f;col.center=Vector3.up*(height*.8f);col.isTrigger=true;root.gameObject.layer=Combat.UnitLayer;
            root.gameObject.AddComponent<ActorBrain>().Setup(h,Vector3.zero);
            EnemiesAlive++;TotalSpawned++;PeakEnemies=Mathf.Max(PeakEnemies,EnemiesAlive);
            return h;
        }

        public static float GroundHeight(float x,float z) => z<=26?0: z>=40?-4:-(z-26)*4f/14f;
        public Vector3 EnemyWaypoint(int lane,int index,float spread)
        {
            float x=(lane==0?-13.5f:13.5f)+spread;
            Vector3 point=index==0?new Vector3(x,0,38):index==1?new Vector3(x,0,20):index==2?new Vector3(x,0,9):new Vector3(lane==0?-4.4f:4.4f,0,-.7f);
            point.y=GroundHeight(point.x,point.z);return point;
        }

        public Health FindTarget(Health from,float range)
        {
            Health best=null;float scoreBest=float.MaxValue;
            foreach(var h in Entities)
            {
                if(h==null||!h.Alive||h.Faction==from.Faction)continue;
                float distance=Vector3.Distance(h.Aim,from.Aim)-h.Radius;
                if(distance>range)continue;
                float score=distance;
                if(from.Faction==Faction.Invaders)
                {
                    if(h.IsBuilding)score+=from.Role==Role.Colossus?-2.5f:5;
                    if(h.Role==Role.Player)score+=1.5f;
                }
                if(score>=scoreBest)continue;
                best=h;scoreBest=score;
            }
            return best;
        }

        void Update()
        {
            if(!Diagnostic && Input.GetKeyDown(KeyCode.Escape) && Phase!=Phase.Briefing && !Ended)SetPaused(!Paused);
            if(!Diagnostic && !Paused && Phase==Phase.Assault && Player.LostControl)SetPaused(true);
            float dt=Time.deltaTime;
            ToastLeft=Mathf.Max(0,ToastLeft-Time.unscaledDeltaTime);
            WaveBannerLeft=Mathf.Max(0,WaveBannerLeft-Time.unscaledDeltaTime);
            if(Diagnostic) { Checks.Step(Time.unscaledDeltaTime); }
            else if(Active)Tick(Mathf.Min(dt,.05f));
            if(!Diagnostic)Effects.Tick(Paused?0:dt);
            Player.UpdateCamera(Time.unscaledDeltaTime);
            Sounds.Tick();
        }

        public void Tick(float dt)
        {
            if(!Active)return;
            SimTime+=dt;missionDuration+=dt;
            if(Player.Self.Alive)
            {
                if(SimTime-Player.Self.LastDamageTime>5)Player.Self.Heal(10*dt);
                Player.Tick(dt);
            }
            else
            {
                RespawnLeft-=dt;
                if(RespawnLeft<=0) { Player.Respawn(); Notice("装甲已重启 · 重新加入防线"); }
            }
            if(Barracks.Alive && MarinesAlive<8)
            {
                ProductionLeft-=dt;
                if(ProductionLeft<=0){SpawnMarine();ProductionLeft=12;}
            }
            for(int i=0;i<Entities.Count;i++)
            {
                var h=Entities[i];if(h!=null && h.Alive && h.Brain!=null)h.Brain.Tick(dt);
                if(Ended)break;
            }
            Entities.RemoveAll(h=>h==null);
            Physics.SyncTransforms();
            if(Phase==Phase.Assault)
            {
                spawnLeft-=dt;
                if(EnemiesAlive<55 && nextSpawn<orders.Count && spawnLeft<=0)
                {
                    var order=orders[nextSpawn++];SpawnEnemy(order.Role,order.Lane);SpawnedThisWave++;
                    spawnLeft=WavePlan.Intervals[Wave-1];
                }
                if(nextSpawn==orders.Count && EnemiesAlive==0)FinishWave();
            }
            else if(Phase==Phase.Intermission)
            {
                IntermissionLeft-=dt;
                if(IntermissionLeft<=0)
                {
                    StartWave();
#if UNITY_WEBGL && !UNITY_EDITOR
                    if(!Diagnostic)SetPaused(true);
#endif
                }
            }
        }

        public void BeginCampaign()
        {
            if(Phase!=Phase.Briefing)return;
            StartWave();
        }

        public void StartWave()
        {
            if(Ended || (Phase==Phase.Assault))return;
            Wave++;if(Wave>5)return;
            orders=WavePlan.Create(Wave);nextSpawn=0;SpawnedThisWave=0;spawnLeft=3.5f;
            Phase=Phase.Assault;Paused=false;Time.timeScale=1;
            Banner="第 "+Wave+" 波 / "+WavePlan.Titles[Wave-1];
            Subtitle=Wave==1?"西侧入口出现信号 · 支援自动防线":Wave==2?"东侧接敌 · 优先处理酸脊兽":Wave>=4?"重甲单位逼近 · 集中火力攻击核心":"两侧同时接敌 · 留意战术地图";
            WaveBannerLeft=5;
            Player.RequestControl();Sounds.Play(SoundId.Wave,.55f);
        }

        void FinishWave()
        {
            Credits+=80+Wave*25;
            if(Wave==5) { Phase=Phase.Victory;Player.ReleaseControl();Sounds.Play(SoundId.Win,.8f);return; }
            Phase=Phase.Intermission;IntermissionLeft=30;
            Player.ReleaseControl();Player.Self.Heal(180);
            Notice("防线稳住了 · 获得 "+(80+Wave*25)+" 补给");
            Sounds.Play(SoundId.Upgrade,.65f);
        }

        public void OnKilled(Health victim,Health source)
        {
            if(victim.Role==Role.Player)
            {
                Deaths++;RespawnLeft=6;Player.PlayerKilled();Sounds.Play(SoundId.Down,.75f);return;
            }
            if(victim.Faction==Faction.Invaders)
            {
                EnemiesAlive=Mathf.Max(0,EnemiesAlive-1);Kills++;
                if(source!=null && source.Role==Role.Player)PlayerKills++;
                Credits+=victim.Role==Role.Colossus?36:victim.Role==Role.Spitter?13:7;
                Effects.Burst(victim.Aim,Art.Pink,victim.Role==Role.Colossus?1.6f:.65f);
            }
            else if(victim.Role==Role.Marine) { MarinesAlive=Mathf.Max(0,MarinesAlive-1);Effects.Burst(victim.Aim,Art.Cyan,.6f); }
            else
            {
                Effects.Burst(victim.Aim,Art.Amber,4);
                if(victim.Role==Role.Command)
                {
                    Phase=Phase.Defeat;Player.ReleaseControl();Sounds.Play(SoundId.Down,.9f);
                    foreach(var r in victim.GetComponentsInChildren<Renderer>())r.sharedMaterial=Art.Mat(1);
                    return;
                }
                Notice(victim.Role==Role.Barracks?"兵营失守 · 自动补员停止":"地堡失守 · 固定火力已中断");
            }
            if(victim.IsBuilding)
            {
                foreach(var r in victim.GetComponentsInChildren<Renderer>())r.sharedMaterial=Art.Mat(1);
                return;
            }
            victim.gameObject.SetActive(false);Destroy(victim.gameObject);
        }

        public int UpgradeCost(int kind) => kind==0?90+WeaponLevel*80:kind==1?110+DefenseLevel*90:120;
        public bool Buy(int kind)
        {
            if(Phase!=Phase.Intermission||kind<0||kind>2)return false;
            if((kind==0&&WeaponLevel>=4)||(kind==1&&DefenseLevel>=4))return false;
            if(kind==2&&Command.Current>=Command.Maximum)return false;
            int cost=UpgradeCost(kind);if(Credits<cost){Notice("补给不足");return false;}
            Credits-=cost;
            if(kind==0)WeaponLevel++;
            if(kind==1)DefenseLevel++;
            if(kind==2)Command.Heal(650);
            Sounds.Play(SoundId.Upgrade,.6f);return true;
        }

        public void SetPaused(bool pause)
        {
            if(Ended||Phase==Phase.Briefing)return;
            Paused=pause;Time.timeScale=pause?0:1;
            if(pause)Player.ReleaseControl();else if(Phase==Phase.Assault)Player.RequestControl();
        }
        public void Notice(string text){Toast=text;ToastLeft=4;}
        public void Restart(){Time.timeScale=1;DeployOnLoad=true;SceneManager.LoadScene(0);}
        public string DurationText(){int seconds=Mathf.FloorToInt(missionDuration);return (seconds/60).ToString("00")+":"+(seconds%60).ToString("00");}
        void OnApplicationFocus(bool focus){if(!focus&&!Diagnostic)SetPaused(true);}
        void OnApplicationPause(bool pause){if(pause&&!Diagnostic)SetPaused(true);}
        void OnDestroy(){if(I==this)I=null;Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    }
}

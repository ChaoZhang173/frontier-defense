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
        public readonly List<Health> Bunkers = new List<Health>();
        public readonly List<Health> BarracksBuildings = new List<Health>();
        public ConstructionDirector Construction;
        public TechnologyDirector Tech;
        public BattleStats Stats;
        public AssaultDirector Assault;
        public TankDirector Tanks;
        public EnemyDefenseDirector EnemyDefenses;
        public ExpeditionDirector Expeditions;
        public RepairDirector Repairs;
        public int CommandFortifyLevel;
        public int EnemyMobileCount { get { int count=0;foreach(var h in Entities)if(h!=null&&h.Alive&&h.Faction==Faction.Invaders&&!h.IsBuilding)count++;return count;} }
        public int MarineCapacity => 8 + (Construction != null && Construction.ReserveOperational ? 4 : 0);
        public int PeakMarineCapacity = 8;
        public PlayerMotor Player;
        public Effects Effects;
        public Soundscape Sounds;
        public GameHud Hud;
        public RuntimeChecks Checks;
        public SquadDirector Squad;
        public BunkerDirector Fort;
        public BaseUpgrades Upgrades;
        public const int EnemyLimit = 100;
        public float ProductionInterval => 12 - 2 * (Upgrades == null ? 0 : Upgrades.BarracksLevel);
        public float BunkerDamage => 15 + 4 * DefenseLevel + 5 * (Upgrades == null ? 0 : Upgrades.FirepowerLevel);
        public bool CommandTerminalOpen {get;private set;}
        public Phase Phase = Phase.Briefing;
        public bool Paused, Diagnostic;
        public int Wave, Credits = 160, WeaponLevel, DefenseLevel, Kills, PlayerKills, Deaths;
        public int EnemiesAlive, MarinesAlive, SpawnedThisWave, TotalSpawned, ScheduledKills, PeakEnemies, PeakMarines;
        public float SimTime, IntermissionLeft, ProductionLeft = 10, RespawnLeft;
        public float ToastLeft, WaveBannerLeft;
        public string Toast = "", Banner = "", Subtitle = "";
        public bool Ended => Phase == Phase.Victory || Phase == Phase.Defeat;
        public bool Playing => Phase==Phase.Assault || Phase==Phase.Intermission;
        public bool Active => !Paused && Playing;
        public bool CanManage => CommandTerminalOpen && Paused && Playing && !Ended && NearCommand && !Fort.PlayerInside;
        public int RemainingToSpawn => Assault==null?0:Assault.Remaining;
        public int EnemyCountForWave => Wave > 0 ? WavePlan.Counts[Wave - 1] : 0;
        public float RifleDamage => 25 + WeaponLevel * 6;
        public float MasterVolume = .55f;
        public float Sensitivity = 1.5f;
        public int Quality = 1;
        float missionDuration;
        readonly Health[] garrison = new Health[12];
        public static readonly Vector3[] DefensePosts = {
            new Vector3(-13.5f,0,15.5f),new Vector3(13.5f,0,15.5f),
            new Vector3(-16.2f,0,12.5f),new Vector3(16.2f,0,12.5f),
            new Vector3(-11f,0,12.5f),new Vector3(11f,0,12.5f),
            new Vector3(-15,0,18.5f),new Vector3(15,0,18.5f),
            new Vector3(-19,0,16),new Vector3(19,0,16),
            new Vector3(-11,0,19),new Vector3(11,0,19)
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
            Stats=new BattleStats(this);
            ConfigureLighting();
            Art.BuildEnvironment(transform);
            EnemyFronts.Build(transform);
            Command = CreateBuilding(Role.Command, new Vector3(0,0,-7), 2300, new Vector3(9.5f,5.3f,8.2f));
            Barracks = CreateBuilding(Role.Barracks, new Vector3(-14,0,-9), 1100, new Vector3(7.5f,4.2f,8.3f));
            Bunker = CreateBuilding(Role.Bunker, new Vector3(20.5f,0,20), 1200, new Vector3(7.4f,3,6.1f));
            CreateBuilding(Role.Bunker,new Vector3(-20.5f,0,20),1200,new Vector3(7.4f,3,6.1f));
            Effects = gameObject.AddComponent<Effects>(); Effects.Setup();
            Sounds = gameObject.AddComponent<Soundscape>(); Sounds.Setup();
            SpawnPlayer();
            for(int i=0;i<4;i++) SpawnMarine(i, false);
            Squad=gameObject.AddComponent<SquadDirector>();Squad.Setup();
            Upgrades=new BaseUpgrades(this);
            Fort=gameObject.AddComponent<BunkerDirector>();Fort.Setup();
            Construction=new ConstructionDirector(this);Tech=new TechnologyDirector(this);
            Assault=new AssaultDirector(this);Tanks=new TankDirector(this);
            EnemyDefenses=new EnemyDefenseDirector(this);Squad.RebuildNavigation();
            Expeditions=new ExpeditionDirector(this);Repairs=new RepairDirector(this);
            Hud = gameObject.AddComponent<GameHud>();
            ApplyQuality(Quality);
            if(Diagnostic) { Checks = gameObject.AddComponent<RuntimeChecks>(); Checks.Setup(this); }
            if(DeployOnLoad) { DeployOnLoad=false; BeginCampaign(); }
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

        public Health CreateBuilding(Role role,Vector3 position,float hp,Vector3 size)
        {
            Transform root=Art.Root(role.ToString(),transform,position);
            Health h=root.gameObject.AddComponent<Health>();h.Setup(role,Faction.Defense,hp,size.y*.52f,Mathf.Max(size.x,size.z)*.45f);
            var collider=root.gameObject.AddComponent<BoxCollider>();collider.size=size;collider.center=new Vector3(0,size.y/2,0);
            root.gameObject.layer=Combat.WorldLayer;
            if(role==Role.Command)Art.CommandBuilding(root);
            if(role==Role.Factory)TankArt.FactoryBuilding(root);
            if(role==Role.Barracks){Art.BarracksBuilding(root);BarracksBuildings.Add(h);}
            if(role==Role.Bunker)
            {
                Bunkers.Add(h);h.Rig=Art.BunkerBuilding(root);
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

        public Health SpawnMarine(int slot=-1,bool deploy=true,Health producer=null)
        {
            if(MarinesAlive>=MarineCapacity)return null;
            if(slot<0)for(int i=0;i<MarineCapacity;i++)if(garrison[i]==null||!garrison[i].Alive){slot=i;break;}
            if(slot<0||slot>=MarineCapacity||(garrison[slot]!=null&&garrison[slot].Alive))return null;
            if(producer==null)producer=Barracks;
            if(deploy&&(producer==null||!producer.Alive))return null;
            Vector3 spawn=deploy?producer.transform.TransformPoint(new Vector3((slot%3-1)*.5f,0,5.2f)):DefensePosts[slot];
            Transform root=Art.Root("Auto-deployed marine",transform,spawn);
            Health h=root.gameObject.AddComponent<Health>();h.Setup(Role.Marine,Faction.Defense,170,1.2f,.5f);h.Lane=slot%2;h.Slot=slot;
            h.Rig=Art.Marine(root,false);
            var col=root.gameObject.AddComponent<CapsuleCollider>();col.center=new Vector3(0,1,0);col.height=2;col.radius=.42f;col.isTrigger=true;root.gameObject.layer=Combat.UnitLayer;
            var brain=root.gameObject.AddComponent<ActorBrain>();brain.Setup(h,DefensePosts[slot]);
            garrison[slot]=h;MarinesAlive++;PeakMarines=Mathf.Max(PeakMarines,MarinesAlive);
            if(deploy)Effects.Burst(spawn+Vector3.up*.1f,Art.Cyan,.8f);
            return h;
        }

        public bool DetachGuard(Health guard)
        {
            if(guard==null||!guard.Alive||guard.Role!=Role.Marine||!Entities.Contains(guard))return false;
            int slot=guard.Slot;
            if(slot<0||slot>=garrison.Length||garrison[slot]!=guard)return false;
            garrison[slot]=null;MarinesAlive=Mathf.Max(0,MarinesAlive-1);return true;
        }
        public static readonly Vector3[] EnemySpawnPoints={
            new Vector3(-27,-4,82),new Vector3(27,-4,82),
            new Vector3(-44,-4,104),new Vector3(44,-4,104),
            new Vector3(-16,-4,121),new Vector3(16,-4,121)
        };
        readonly int[] laneSpawns=new int[2];
        public readonly int[] SpawnPointUses=new int[6];
        public Health SpawnEnemy(Role role,int lane,Vector3? overridePosition=null,int sourceSite=-1,bool baseGuard=false)
        {
            lane=Mathf.Clamp(lane,0,1);
            Vector3 p;
            if(overridePosition.HasValue)p=overridePosition.Value;
            else
            {
                int site=sourceSite>=0&&sourceSite<EnemySpawnPoints.Length&&sourceSite%2==lane?sourceSite:lane+2*(laneSpawns[lane]++%3);SpawnPointUses[site]++;
                p=EnemySpawnPoints[site]+new Vector3(UnityEngine.Random.Range(-1.5f,1.5f),0,-5+UnityEngine.Random.Range(-1f,1f));
            }
            Transform root=Art.Root(role.ToString(),transform,p);root.rotation=Quaternion.Euler(0,180,0);
            float hp=role==Role.Colossus?790:role==Role.Siege?420:role==Role.Spitter?160:role==Role.Stalker?120:95;
            float height=role==Role.Colossus?1.8f:role==Role.Siege?1.7f:role==Role.Spitter?.95f:role==Role.Stalker?.85f:.6f;
            float radius=role==Role.Colossus?1.12f:role==Role.Siege?1.05f:role==Role.Spitter?.68f:.52f;
            Health h=root.gameObject.AddComponent<Health>();h.Setup(role,Faction.Invaders,hp,height,radius);h.Lane=lane;h.BaseGuard=baseGuard;h.BaseId=sourceSite;
            h.Rig=Art.Creature(root,role);
            h.WeakPoint=role==Role.Colossus?new Vector3(0,2.55f,.33f):role==Role.Spitter?new Vector3(0,1.19f,-.40f):new Vector3(0,.56f,.79f);
            h.WeakRadius=role==Role.Colossus?.4f:role==Role.Spitter?.34f:.28f;
            var col=root.gameObject.AddComponent<CapsuleCollider>();col.radius=radius;col.height=height*1.8f;col.center=Vector3.up*(height*.8f);col.isTrigger=true;root.gameObject.layer=Combat.UnitLayer;
            root.gameObject.AddComponent<ActorBrain>().Setup(h,Vector3.zero);
            if(!baseGuard){EnemiesAlive++;TotalSpawned++;}PeakEnemies=Mathf.Max(PeakEnemies,EnemyMobileCount);
            return h;
        }

        public static float GroundHeight(float x,float z) => GroundSurface.Height(x,z);
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
                if(h==null||!h.Alive||h.IsSheltered||h.Faction==from.Faction)continue;
                float distance=Vector3.Distance(h.Aim,from.Aim)-h.Radius;
                if(distance>range)continue;
                if(from.Faction==Faction.Defense&&(from.Role==Role.Bunker||from.Role==Role.Marine)&&!Combat.Visible(from,h))continue;
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
            if(!Diagnostic && Input.GetKeyDown(KeyCode.E))
            {
                Interact();
            }
            if(!Diagnostic && !Paused && Playing && Player.LostControl)SetPaused(true);
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
            Construction.Tick(dt);Tech.Tick(dt);Tanks.Tick(dt);EnemyDefenses.Tick(dt);Expeditions.Tick(dt);Repairs.Tick(dt);
            if(Barracks!=null && Barracks.Alive && MarinesAlive<MarineCapacity)
            {
                ProductionLeft-=dt;
                if(ProductionLeft<=0){SpawnMarine();ProductionLeft=ProductionInterval;}
            }
            Fort.Tick(dt);
            Squad.Tick(dt);
            for(int i=0;i<Entities.Count;i++)
            {
                var h=Entities[i];if(h!=null && h.Alive && h.Brain!=null)h.Brain.Tick(dt);
                if(Ended)break;
            }
            Entities.RemoveAll(h=>h==null);
            Physics.SyncTransforms();
            if(Phase==Phase.Assault)
            {
                Assault.Tick(dt);
                Stats.Tick(dt);
                if(RemainingToSpawn==0 && EnemiesAlive==0)FinishWave();
            }
            else if(Phase==Phase.Intermission)
            {
                IntermissionLeft-=dt;
                if(IntermissionLeft<=0)
                {
                    StartWave();
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
            SpawnedThisWave=0;
            Assault.StartWave(Wave);Stats.StartWave();
            Phase=Phase.Assault;CommandTerminalOpen=false;Paused=false;Time.timeScale=1;
            Banner="第 "+Wave+" 波 / "+WavePlan.Titles[Wave-1];
            Subtitle=WavePlan.Preview(Wave);
            WaveBannerLeft=5;
            Player.RequestControl();Sounds.Play(SoundId.Wave,.55f);
        }

        void FinishWave()
        {
            int reward=WaveReward(Wave);Credits+=reward;Stats.Earn(reward);Stats.Snapshot(true);
            if(Wave==5) { Phase=Phase.Victory;Player.ReleaseControl();Sounds.Play(SoundId.Win,.8f);return; }
            Phase=Phase.Intermission;IntermissionLeft=45;
            Player.Self.Heal(180);Squad.Regroup();
            Notice("防线稳住了 · 获得 "+reward+" 补给 · 可前往指挥中心升级");
            Sounds.Play(SoundId.Upgrade,.65f);
        }

        public void OnKilled(Health victim,Health source)
        {
            Stats?.RecordLoss(victim);
            if(victim.Role==Role.Player)
            {
                Deaths++;RespawnLeft=6;Player.PlayerKilled();Sounds.Play(SoundId.Down,.75f);return;
            }
            if(victim.Role==Role.EnemyTower||victim.Role==Role.EnemyPortal||victim.Role==Role.EnemyNest)
            {
                if(EnemyDefenses.OnKilled(victim))
                {
                    int reward=victim.Role==Role.EnemyPortal?300:victim.Role==Role.EnemyNest?90:EnemyDefenseDirector.Reward;Credits+=reward;Stats.Earn(reward);
                    Notice(victim.Role==Role.EnemyPortal?"传送门摧毁 · 据点停止增援":"敌方防御建筑已摧毁 · +"+reward+" 补给");
                    Effects.Burst(victim.Aim,Art.Pink,3);
                }
                foreach(var r in victim.GetComponentsInChildren<Renderer>())r.sharedMaterial=Art.Mat(1);
                return;
            }
            if(victim.Role==Role.Tank)
            {
                Tanks.OnKilled(victim);Effects.Burst(victim.Aim,Art.Amber,2.5f);
                victim.gameObject.SetActive(false);Destroy(victim.gameObject);return;
            }
            if(victim.Faction==Faction.Invaders)
            {
                if(!victim.BaseGuard){EnemiesAlive=Mathf.Max(0,EnemiesAlive-1);ScheduledKills++;}Kills++;
                if(source!=null && source.Role==Role.Player)PlayerKills++;
                if(source!=null && source.Role==Role.Squad)Squad.Kills++;
                int reward=KillReward(victim.Role);Credits+=reward;Stats.Earn(reward);
                Effects.Burst(victim.Aim,Art.Pink,victim.Role==Role.Colossus?1.6f:.65f);
            }
            else if(victim.Role==Role.Marine) { Fort.ReleaseGuard(victim);MarinesAlive=Mathf.Max(0,MarinesAlive-1);Effects.Burst(victim.Aim,Art.Cyan,.6f); }
            else if(victim.Role==Role.Squad) { Squad.OnLost(victim);Effects.Burst(victim.Aim,Art.Amber,.6f); }
            else if(victim.Role==Role.AssaultMarine||victim.Role==Role.Worker){Effects.Burst(victim.Aim,Art.Cyan,.6f);}
            else
            {
                if(victim.Role==Role.Bunker)Fort.OnDestroyed(victim);
                Construction?.OnKilled(victim);Tanks?.OnKilled(victim);
                Effects.Burst(victim.Aim,Art.Amber,4);
                if(victim.Role==Role.Command)
                {
                    Phase=Phase.Defeat;Player.ReleaseControl();Sounds.Play(SoundId.Down,.9f);
                    foreach(var r in victim.GetComponentsInChildren<Renderer>())r.sharedMaterial=Art.Mat(1);
                    return;
                }

            }
            if(victim.IsBuilding)
            {
                foreach(var r in victim.GetComponentsInChildren<Renderer>())r.sharedMaterial=Art.Mat(1);
                return;
            }
            victim.gameObject.SetActive(false);Destroy(victim.gameObject);
        }

        public static int KillReward(Role role)=>role==Role.Colossus?48:role==Role.Siege?38:role==Role.Spitter?18:role==Role.Stalker?15:10;
        public static int WaveReward(int wave)=>140+wave*35;

        public int UpgradeCost(int kind) => kind==0?90+WeaponLevel*80:kind==1?110+DefenseLevel*90:120*(CommandFortifyLevel+1);
        public bool Buy(int kind)
        {
            if(!CanManage||kind<0||kind>2)return false;
            if((kind==0&&WeaponLevel>=4)||(kind==1&&DefenseLevel>=4))return false;
            if(kind==2&&CommandFortifyLevel>=3)return false;
            int cost=UpgradeCost(kind);if(Credits<cost){Notice("补给不足");return false;}
            Credits-=cost;
            if(kind==0)WeaponLevel++;
            if(kind==1)DefenseLevel++;
            if(kind==2){CommandFortifyLevel++;Command.Maximum+=650;Command.Heal(650);}
            Sounds.Play(SoundId.Upgrade,.6f);return true;
        }

        public void SetPaused(bool pause)
        {
            if(Ended||Phase==Phase.Briefing)return;
            Paused=pause;Time.timeScale=pause?0:1;
            if(!pause)CommandTerminalOpen=false;
            if(pause)Player.ReleaseControl();else if(Playing)Player.RequestControl();
        }
        public Health GuardRecruitTarget
        {
            get
            {
                if(Player==null||!Player.Self.Alive||Player.View==null||Paused||!Playing||Fort.PlayerInside)return null;
                Ray ray=Player.View.ViewportPointToRay(new Vector3(.5f,.5f,0));
                if(!Combat.Trace(ray.origin,ray.direction,14,Player.Self,out var hit))return null;
                var guard=hit.collider.GetComponentInParent<Health>();
                return guard!=null&&guard.Alive&&!guard.IsSheltered&&guard.Role==Role.Marine&&
                    Vector3.Distance(Player.transform.position,guard.transform.position)<=4?guard:null;
            }
        }
        public bool Interact()
        {
            if(CommandTerminalOpen){SetPaused(false);return true;}
            if(Fort.PlayerInside)return Fort.TryTogglePlayer();
            Health guard=GuardRecruitTarget;
            if(guard!=null)
            {
                if(!Squad.RecruitGuard(guard))Notice(Squad.RecruitedGuardCount>=SquadDirector.GuardCapacity?"随行守卫已满 · 最多招募 2 人":"无法招募 · 靠近并瞄准守卫");
                return true;
            }
            return Fort.TryTogglePlayer()||TryOpenCommandTerminal();
        }
        public bool NearCommand=>Command!=null&&Command.Alive&&Player.Self.Alive&&Vector3.Distance(Player.transform.position,new Vector3(0,0,-1.3f))<6;
        public int LaneEnemies(int lane)
        {
            int count=0;
            foreach(var h in Entities)if(h!=null&&h.Alive&&h.Faction==Faction.Invaders&&!h.IsBuilding&&h.Lane==lane&&(!h.BaseGuard||h.transform.position.z<50))count++;
            return count;
        }
        public bool LaneUnderAttack(int lane)
        {
            foreach(var h in Entities)
                if(h!=null&&h.Alive&&h.Faction==Faction.Invaders&&!h.IsBuilding&&h.Lane==lane&&h.transform.position.z<23)return true;
            return false;
        }
        public bool TryOpenCommandTerminal()
        {
            if(Paused||!Playing||!NearCommand||Fort.PlayerInside)return false;
            CommandTerminalOpen=true;SetPaused(true);return true;
        }
        public void Notice(string text){Toast=text;ToastLeft=4;}
        public void Restart(){Time.timeScale=1;DeployOnLoad=true;SceneManager.LoadScene(0);}
        public string DurationText(){int seconds=Mathf.FloorToInt(missionDuration);return (seconds/60).ToString("00")+":"+(seconds%60).ToString("00");}
        void OnApplicationFocus(bool focus){if(!focus&&!Diagnostic)SetPaused(true);}
        void OnApplicationPause(bool pause){if(pause&&!Diagnostic)SetPaused(true);}
        void OnDestroy(){if(I==this)I=null;Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    }
}

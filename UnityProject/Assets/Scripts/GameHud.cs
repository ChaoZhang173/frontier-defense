using UnityEngine;

namespace Frontier
{
    public sealed class GameHud : MonoBehaviour
    {
        readonly Color ink=new Color(.025f,.055f,.08f,.94f), muted=new Color(.55f,.66f,.70f),white=new Color(.91f,.96f,.96f);
        Font font;GUIStyle text,button;float width,height,scale;bool settings,terminalWasOpen;
        int terminalPage;
        GameWorld W=>GameWorld.I;
        void Initialize()
        {
            font=Resources.Load<Font>("Fonts/FrontierSans");
            text=new GUIStyle(GUI.skin.label){font=font,richText=true,wordWrap=false,alignment=TextAnchor.UpperLeft};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=17,alignment=TextAnchor.MiddleCenter};
            button.normal.background=Texture2D.whiteTexture;button.hover.background=Texture2D.whiteTexture;
            button.active.background=Texture2D.whiteTexture;button.normal.textColor=ink;button.hover.textColor=ink;button.active.textColor=ink;
        }
        void OnGUI()
        {
            if(W==null||W.Player==null)return;if(text==null)Initialize();
            scale=Mathf.Min(Screen.width/1440f,Screen.height/900f);
            width=Screen.width/scale;height=Screen.height/scale;
            GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,Vector3.one*scale);
            if(W.CommandTerminalOpen&&!terminalWasOpen)terminalPage=0;
            terminalWasOpen=W.CommandTerminalOpen;
            if(W.Phase==Phase.Briefing){Briefing();return;}
            if(W.Ended){Result();return;}
            if(W.Fort!=null&&W.Fort.PlayerInside)BunkerView();
            WorldLabels();Hud();
            if(W.Player.DamageFlash>0)Box(new Rect(0,0,width,height),new Color(.9f,.12f,.1f,W.Player.DamageFlash*.12f));
            if(!W.Player.Self.Alive)
            {
                Panel(new Rect(width/2-240,height/2-72,480,144));
                Label(width/2-220,height/2-53,440,30,"装甲离线",25,Art.Pink,TextAnchor.UpperCenter);
                Label(width/2-220,height/2-7,440,28,"指挥中心重启中 · "+Mathf.CeilToInt(W.RespawnLeft)+" 秒后重返战场",17,white,TextAnchor.UpperCenter);
                Label(width/2-220,height/2+30,440,25,"防线仍在战斗",14,muted,TextAnchor.UpperCenter);
            }
            if(W.Phase==Phase.Intermission&&!W.Paused)Intermission();
            if(W.CommandTerminalOpen)CommandTerminal();else if(W.Paused)Pause();
            if(W.ToastLeft>0&&!W.Paused)
            {
                float toastY=W.Phase==Phase.Intermission?248:W.Assault!=null?216:118;
                Panel(new Rect(width/2-290,toastY,580,43));
                Label(width/2-275,toastY+10,550,25,W.Toast,16,Art.Amber,TextAnchor.UpperCenter);
            }
        }
        void Briefing()
        {
            Box(new Rect(0,0,width*.52f,height),new Color(.017f,.042f,.066f,.9f));
            Label(60,50,600,30,"F R O N T I E R   /   0 7",18,Art.Cyan);
            Box(new Rect(60,102,44,3),Art.Amber);
            Label(58,height*.24f,600,90,"边境防线",66,white);
            Label(63,height*.24f+94,540,30,"守住基地，直到黎明。",23,Art.Amber);
            Label(63,height*.24f+156,570,40,"一名装甲步兵。两处入口。五波敌潮。",18,white);
            Label(63,height*.24f+195,570,80,"建造战车工厂，部署坦克与四人小队守住中央阵地。\n前往指挥中心升级；击毁敌巢炮塔获得额外补给。\n第三波起，警惕两路更密集的重甲突击。",17,muted);
            if(Button(new Rect(62,height-226,300,58),"部署到防线  →",Art.Cyan))W.BeginCampaign();
            if(Button(new Rect(378,height-226,144,58),"设置",new Color(.39f,.50f,.55f)))settings=true;
            Label(62,height-142,650,27,"W A S D  移动     鼠标  视角     左键  射击",16,white);
            Label(62,height-109,650,27,"右键  瞄准     R  换弹     Shift  冲刺     Esc  暂停",16,muted);
            Label(62,height-78,650,24,"E  招募守卫 / 交互     B  小队入驻     F  撤出跟随",15,Art.Amber);
            Label(62,height-39,750,24,"单人试玩  v0.6   /   机械工厂与中央防线   /   键盘与鼠标",13,muted);
            Label(width-355,52,295,28,"峡谷前哨 / 清晨 05:47",16,white,TextAnchor.UpperRight);
            Label(width-405,86,345,25,"五波共 "+WavePlan.TotalCount+" 名敌人 · 守住两路",14,muted,TextAnchor.UpperRight);
            if(settings){Box(new Rect(0,0,width,height),new Color(0,0,0,.55f));SettingsCard(true);}
        }
        void Hud()
        {
            Panel(new Rect(30,26,364,84));
            Label(48,40,300,24,"指挥中心",16,white);
            Label(255,41,120,23,Mathf.CeilToInt(W.Command.Current)+" / "+Mathf.CeilToInt(W.Command.Maximum),14,muted,TextAnchor.UpperRight);
            Bar(new Rect(48,77,328,10),W.Command.Current/W.Command.Maximum,W.Command.Current>700?Art.Cyan:Art.Pink);
            Panel(new Rect(width/2-180,26,360,84));
            Label(width/2-160,38,320,25,"第 "+W.Wave+" / 5 波  ·  "+WavePlan.Titles[Mathf.Max(0,W.Wave-1)],20,white,TextAnchor.UpperCenter);
            Label(width/2-160,76,320,22,W.Phase==Phase.Intermission?"波间整备":"场上 "+W.EnemiesAlive+"   /   增援待抵达 "+W.RemainingToSpawn,14,Art.Amber,TextAnchor.UpperCenter);
            AssaultPanel();
            MiniMap();
            SquadPanel();
            BunkerPanel();
            TankPanel();
            EngineeringPanel();
            Panel(new Rect(30,height-149,315,120));
            Label(48,height-135,200,23,"先锋 / 装甲步兵",16,white);
            Label(242,height-136,84,26,Mathf.CeilToInt(W.Player.Self.Current).ToString(),22,Art.Cyan,TextAnchor.UpperRight);
            Bar(new Rect(48,height-95,278,10),W.Player.Self.Current/W.Player.Self.Maximum,Art.Cyan);
            Label(48,height-68,280,24,"脱离伤害 5 秒后自动修复",13,muted);
            Panel(new Rect(width-300,height-149,270,120));
            Label(width-280,height-136,150,23,"磁轨步枪  Mk."+(W.WeaponLevel+1),15,white);
            Label(width-280,height-109,160,60,W.Player.Ammo.ToString("00"),45,W.Player.Ammo>6?white:Art.Amber);
            Label(width-198,height-90,130,29,"/ 30  ·  ∞",19,muted);
            if(W.Player.ReloadLeft>0)
            {
                Bar(new Rect(width-280,height-47,230,5),1-W.Player.ReloadLeft/1.65f,Art.Amber);
                Label(width-122,height-133,76,23,"装填中",14,Art.Amber,TextAnchor.UpperRight);
            }
            Label(width/2-220,height-66,440,25,"补给  "+W.Credits+"     驻军  "+W.MarinesAlive+" / "+W.MarineCapacity+"     击杀  "+W.Kills,16,white,TextAnchor.UpperCenter);
            Label(width/2-325,height-36,650,24,"R 换弹   E 交互   B 入驻地堡   F 跟随   G 驻守   Esc 暂停",13,muted,TextAnchor.UpperCenter);
            if(W.Playing && !W.Paused && W.Player.Self.Alive)
            {
                float x=width/2,y=height/2,gap=W.Player.ReticleSpread;
                Color c=W.Player.KillTimer>0?Art.Pink:W.Player.CriticalTimer>0?Art.Amber:W.Player.AimTarget!=null?Art.Cyan:white;
                Box(new Rect(x-gap-8,y-1,8,2),c);Box(new Rect(x+gap,y-1,8,2),c);
                Box(new Rect(x-1,y-gap-8,2,8),c);Box(new Rect(x-1,y+gap,2,8),c);
                Box(new Rect(x-1,y-1,2,2),c);
                if(W.Player.HitTimer>0||W.Player.KillTimer>0)
                {
                    for(int a=-1;a<=1;a+=2)for(int b=-1;b<=1;b+=2)Line(new Vector2(x+a*12,y+b*12),new Vector2(x+a*19,y+b*19),c,2);
                }
                if(W.Player.KillTimer>0)Label(x-90,y+36,180,25,"击杀确认",15,Art.Pink,TextAnchor.UpperCenter);
                else if(W.Player.CriticalTimer>0)Label(x-90,y+36,180,25,"弱点命中 ×1.65",15,Art.Amber,TextAnchor.UpperCenter);
                else if(W.Player.BarrelBlocked)Label(x-100,y+36,200,25,"枪口受阻",15,Art.Amber,TextAnchor.UpperCenter);
                HitFeedback();
                Health recruit=W.GuardRecruitTarget;
                if(recruit!=null)
                {
                    Panel(new Rect(x-245,height-239,490,69));
                    Label(x-230,height-229,460,26,W.Squad.CanRecruitGuard(recruit)?"E  免费招募这名守卫":W.Squad.RecruitedGuardCount>=SquadDirector.GuardCapacity?"随行守卫已满 · 最多 2 人":"移出遮挡后招募守卫",19,Art.Cyan,TextAnchor.UpperCenter);
                    Label(x-230,height-199,460,23,"加入小队后兵营会补员 · 阵亡后需重新招募",14,white,TextAnchor.UpperCenter);
                }
                else if(W.Fort!=null&&(W.Fort.PlayerInside||W.Fort.NearEntrance))
                {
                    Panel(new Rect(x-245,height-239,490,69));
                    Label(x-230,height-229,460,26,W.Fort.PlayerInside?"地堡驻守 · 射界 ±70° · E 退出":"E  进入"+BuildingName(W.Fort.DisplayBunker)+" · "+W.Fort.OccupantsFor(W.Fort.DisplayBunker)+" / "+BunkerDirector.Capacity+" 席位",19,Art.Cyan,TextAnchor.UpperCenter);
                    Label(x-230,height-199,460,23,"B  小队入驻      F  小队撤出并跟随",14,white,TextAnchor.UpperCenter);
                }
                else if(W.NearCommand)
                {
                    Panel(new Rect(x-192,height-228,384,49));
                    Label(x-180,height-213,360,26,"E 指挥中心 · 升级 / 建造 / 小队",17,Art.Amber,TextAnchor.UpperCenter);
                }
                else if(Mathf.Abs(W.Player.transform.position.x)<9&&W.Player.transform.position.z>19&&W.Player.transform.position.z<27)
                {
                    Panel(new Rect(x-245,height-228,490,49));
                    Label(x-230,height-213,460,26,"中央射击阵地 · G 小队驻守 · F 跟随",17,Art.Cyan,TextAnchor.UpperCenter);
                }
                if(W.Phase==Phase.Assault&&W.WaveBannerLeft>0)
                {
                    Label(x-400,274,800,40,W.Banner,29,white,TextAnchor.UpperCenter);
                    Label(x-400,319,800,25,W.Subtitle,16,Art.Amber,TextAnchor.UpperCenter);
                }
            }
        }
        void AssaultPanel()
        {
            if(W.Phase!=Phase.Assault||W.Assault==null)return;
            AssaultDirector assault=W.Assault;
            float x=width/2-270,y=118;
            Panel(new Rect(x,y,540,86));
            Color accent=assault.WarningLaneMask==3?Art.Pink:Art.Amber;
            Label(x+16,y+9,310,23,"进攻情报 · 已出动 "+assault.LaunchedBatches+" / "+assault.BatchCount+" 批",14,white);
            string status=assault.LaunchedBatches<assault.BatchCount&&assault.Countdown>0
                ?Mathf.CeilToInt(assault.Countdown)+" 秒后出动":assault.PhaseText;
            Label(x+330,y+9,194,23,status,14,accent,TextAnchor.UpperRight);
            Label(x+16,y+35,508,23,assault.WarningText,14,accent,TextAnchor.UpperCenter);
            Label(x+16,y+61,508,22,AssaultLaneStatus(assault,0)+"     /     "+AssaultLaneStatus(assault,1),13,muted,TextAnchor.UpperCenter);
        }
        string AssaultLaneStatus(AssaultDirector assault,int lane)
        {
            int forming=0,advancing=0,engaged=0;
            foreach(var group in assault.Groups)
            {
                if(group.Batch.Lane!=lane||group.AliveCount<=0)continue;
                if(group.State==AssaultGroupState.Forming)forming+=group.AliveCount;
                else if(group.State==AssaultGroupState.Advancing)advancing+=group.AliveCount;
                else engaged+=group.AliveCount;
            }
            string status="";
            if(forming>0)status="集结 "+forming;
            if(advancing>0)status+=(status.Length>0?" · ":"")+"推进 "+advancing;
            if(engaged>0)status+=(status.Length>0?" · ":"")+"交战 "+engaged;
            return (lane==0?"西侧 ":"东侧 ")+(status.Length==0?"暂无部队":status);
        }
        void MiniMap()
        {
            Rect outer=new Rect(width-253,26,223,288);Panel(outer);
            Label(outer.x+16,outer.y+12,192,25,"战术地图  /  "+W.DurationText(),14,muted);
            Rect map=new Rect(outer.x+16,outer.y+47,191,165);
            Box(map,new Color(.065f,.115f,.14f));
            MapRect(map,new Vector3(0,0,0),new Vector2(56,50),new Color(.13f,.22f,.25f));
            MapRect(map,new Vector3(-13.5f,0,36),new Vector2(9,23),new Color(.25f,.32f,.32f));
            MapRect(map,new Vector3(13.5f,0,36),new Vector2(9,23),new Color(.25f,.32f,.32f));
            foreach(var source in GameWorld.EnemySpawnPoints)
            {
                Vector2 p=MapPoint(map,source);
                Box(new Rect(p.x-3,p.y-3,6,6),Art.Pink);
            }
            foreach(var h in W.Entities)
            {
                if(h==null||!h.Alive||h.IsSheltered)continue;
                Vector2 p=MapPoint(map,h.transform.position);
                if(!map.Contains(p))continue;
                float s=h.Role==Role.Player?7:h.IsBuilding?8:h.Role==Role.Colossus?6:4;
                Color c=h.Faction==Faction.Invaders?Art.Pink:h.Role==Role.Player?Color.white:h.Role==Role.Squad||h.Role==Role.Construction?Art.Amber:Art.Cyan;
                Box(new Rect(p.x-s/2,p.y-s/2,s,s),c);
            }
            if(W.Construction!=null)foreach(var site in W.Construction.Sites)
            {
                Vector2 p=MapPoint(map,site.Position);if(!map.Contains(p))continue;
                Color color=site.State==SiteState.Destroyed?Art.Pink:site.State==SiteState.Constructing?Art.Amber:site.State==SiteState.Ready?Art.Cyan:muted;
                if(site.State==SiteState.Destroyed)
                {
                    Line(p-new Vector2(5,5),p+new Vector2(5,5),color,2);
                    Line(p+new Vector2(-5,5),p+new Vector2(5,-5),color,2);
                }
                else if(site.State==SiteState.Empty||site.State==SiteState.Constructing)
                {
                    Box(new Rect(p.x-5,p.y-5,10,1),color);Box(new Rect(p.x-5,p.y+4,10,1),color);
                    Box(new Rect(p.x-5,p.y-5,1,10),color);Box(new Rect(p.x+4,p.y-5,1,10),color);
                }
                if(site.Role==Role.Bunker)Label(p.x-28,p.y-22,56,20,site.Id==0?"西堡":"东堡",11,color,TextAnchor.UpperCenter);
            }
            for(int lane=0;lane<2;lane++)
            {
                float row=outer.y+220+lane*28;int enemies=W.LaneEnemies(lane);bool urgent=W.LaneUnderAttack(lane);
                Color color=urgent?Art.Pink:enemies>0?Art.Amber:Art.Cyan;
                if(urgent)Box(new Rect(outer.x+12,row-2,199,25),new Color(.38f,.10f,.13f,.65f));
                Label(outer.x+16,row,112,23,(lane==0?"A 西侧":"B 东侧")+"  "+enemies+" 敌",14,white);
                Label(outer.x+132,row,75,23,urgent?"防线告急":enemies>0?"敌军接近":"防线稳定",13,color,TextAnchor.UpperRight);
            }
            if(W.Squad.Order==SquadOrder.Hold&&W.Squad.Count>0)
            {
                Vector2 p=MapPoint(map,W.Squad.HoldPoint);Box(new Rect(p.x-6,p.y-1,12,2),Art.Amber);Box(new Rect(p.x-1,p.y-6,2,12),Art.Amber);
            }
        }
        Vector2 MapPoint(Rect r,Vector3 p)=>new Vector2(r.x+(p.x+32)/64*r.width,r.y+(80-p.z)/108*r.height);
        void MapRect(Rect r,Vector3 p,Vector2 size){Vector2 t=MapPoint(r,p);Box(new Rect(t.x-size.x/64*r.width/2,t.y-size.y/108*r.height/2,size.x/64*r.width,size.y/108*r.height),new Color(.12f,.21f,.23f));}
        void MapRect(Rect r,Vector3 p,Vector2 size,Color color){Vector2 t=MapPoint(r,p);Box(new Rect(t.x-size.x/64*r.width/2,t.y-size.y/108*r.height/2,size.x/64*r.width,size.y/108*r.height),color);}
        void WorldLabels()
        {
            if(W.Paused)return;
            foreach(var h in W.Entities)
            {
                if(h==null||!h.Alive||h.IsSheltered||h.Role==Role.Player)continue;
                bool building=h.IsBuilding;
                bool companion=h.Role==Role.Squad;
                if(!building&&!companion&&(h.Faction==Faction.Defense||W.SimTime-h.LastDamageTime>3))continue;
                if(Vector3.Distance(h.Aim,W.Player.transform.position)>42)continue;
                Vector3 p=W.Player.View.WorldToScreenPoint(h.Aim+Vector3.up*(building?3.1f:h.AimHeight*.8f));
                if(p.z<=0)continue;float x=p.x/scale,y=(Screen.height-p.y)/scale;
                if(x<20||x>width-20||y<120||y>height-160)continue;
                if(building)
                {
                    string name=BuildingName(h);
                    if(h.Role==Role.Bunker&&W.Fort!=null)name+=" · "+W.Fort.OccupantsFor(h)+" / "+BunkerDirector.Capacity+" 席位";
                    if(h.Role==Role.Construction&&W.Construction!=null)
                        foreach(var site in W.Construction.Sites)if(site.Structure==h){name+=" · "+Mathf.CeilToInt(site.SecondsLeft)+" 秒";break;}
                    Label(x-145,y-25,290,25,name,13,h.Faction==Faction.Invaders?Art.Pink:h.Role==Role.Construction?Art.Amber:Art.Cyan,TextAnchor.UpperCenter);
                }
                if(companion)Label(x-70,y-23,140,24,"0"+(h.Slot+1)+" / "+SquadDirector.Names[h.Slot],13,Art.Amber,TextAnchor.UpperCenter);
                Bar(new Rect(x-(building?45:24),y,building?90:48,4),h.Current/h.Maximum,h.Faction==Faction.Invaders?Art.Pink:companion?Art.Amber:Art.Cyan);
            }
            if(W.Construction!=null)foreach(var site in W.Construction.Sites)
            {
                if(site.State!=SiteState.Empty&&site.State!=SiteState.Destroyed)continue;
                if(Vector3.Distance(site.Position,W.Player.transform.position)>38)continue;
                Vector3 p=W.Player.View.WorldToScreenPoint(site.Position+Vector3.up*2.2f);
                if(p.z<=0)continue;float x=p.x/scale,y=(Screen.height-p.y)/scale;
                if(x<20||x>width-20||y<130||y>height-180)continue;
                Color color=site.State==SiteState.Destroyed?Art.Pink:Art.Amber;
                Label(x-130,y-22,260,23,site.Name+(site.State==SiteState.Destroyed?" · 可重建":" · 待建"),14,color,TextAnchor.UpperCenter);
                Label(x-130,y+4,260,23,"在指挥中心下达施工指令",12,muted,TextAnchor.UpperCenter);
            }
        }
        void Intermission()
        {
            float left=width/2-310,top=118;
            Panel(new Rect(left,top,620,116));
            int next=Mathf.Min(W.Wave+1,WavePlan.Counts.Length);
            Label(left+16,top+10,370,25,"下一波  "+next+" / 5  ·  "+WavePlan.Titles[next-1],18,Art.Cyan);
            Label(left+392,top+12,212,24,Mathf.CeilToInt(W.IntermissionLeft)+" 秒后自动开战",16,Art.Amber,TextAnchor.UpperRight);
            Label(left+16,top+41,588,23,WavePlan.Preview(next),14,white,TextAnchor.UpperCenter);
            Label(left+16,top+66,588,22,WaveAdvice(next),13,Art.Amber,TextAnchor.UpperCenter);
            Label(left+16,top+91,588,21,"自由整备 · 前往指挥中心按 E 升级、建造与维修",13,muted,TextAnchor.UpperCenter);
        }
        string WaveAdvice(int wave)
        {
            if(wave==1)return "守住西路，利用进攻间隙补强小队";
            if(wave==2)return "留意远程压制，先稳住东路火力";
            if(wave==3)return "两路重叠进攻，提前分配驻防";
            if(wave==4)return "重甲先行攻城，集中火力击破先头";
            return "双线连续突击，及时支援受压入口";
        }
        void PageTabs(float left,float top)
        {
            string[] names={"防线升级","建造科技","先锋小队"};
            for(int i=0;i<names.Length;i++)if(Button(new Rect(left+24+i*180,top+67,168,32),names[i],terminalPage==i?Art.Cyan:muted))terminalPage=i;
        }
        void DefenseUpgrades(float left,float top)
        {
            Label(left+25,top+202,680,24,"个人与防线",16,muted);
            string[] names={"磁轨增幅","协同火力","指挥中心维修"};
            string[] descriptions={"步枪每发伤害 +6\n当前伤害 "+W.RifleDamage.ToString("0"),"驻军、小队与地堡伤害提升\n强化整条防线的持续火力","恢复指挥中心 650 耐久\n当前 "+Mathf.CeilToInt(W.Command.Current)+" / "+Mathf.CeilToInt(W.Command.Maximum)};
            for(int i=0;i<3;i++)
            {
                float x=left+24+i*362,y=top+230;UpgradeCard(new Rect(x,y,348,128));
                Label(x+16,y+11,240,25,names[i],19,white);
                if(i<2)Label(x+237,y+15,95,23,(i==0?W.WeaponLevel:W.DefenseLevel)+" / 4 级",13,muted,TextAnchor.UpperRight);
                Paragraph(x+16,y+44,316,44,descriptions[i],14,muted);
                bool max=i==0?W.WeaponLevel>=4:i==1?W.DefenseLevel>=4:W.Command.Current>=W.Command.Maximum;
                bool can=!max&&W.CanManage&&W.Credits>=W.UpgradeCost(i);
                string caption=max?(i==2?"耐久充足":"已达满级"):Price(W.UpgradeCost(i));
                if(Button(new Rect(x+16,y+92,316,27),caption,Art.Amber,can))W.Buy(i);
            }

            Label(left+25,top+372,310,24,"同类建筑共同升级",16,muted);
            Label(left+400,top+374,696,23,"补员 "+W.ProductionInterval.ToString("0")+" 秒 / 人 · 驻军 "+W.MarinesAlive+" / "+W.MarineCapacity+" · 同类建筑共享强化",13,muted,TextAnchor.UpperRight);
            BaseUpgrade[] upgrades={BaseUpgrade.BarracksProduction,BaseUpgrade.BunkerArmor,BaseUpgrade.BunkerFirepower};
            string[] titles={"兵营 · 快速补员","地堡 · 强化装甲","地堡 · 自动炮强化"};
            for(int i=0;i<upgrades.Length;i++)
            {
                var kind=upgrades[i];float x=left+24+i*362,y=top+399;UpgradeCard(new Rect(x,y,348,142));
                Label(x+16,y+11,240,26,titles[i],18,Art.Cyan);
                Label(x+237,y+15,95,23,W.Upgrades.Level(kind)+" / "+BaseUpgrades.MaxLevel+" 级",13,muted,TextAnchor.UpperRight);
                Paragraph(x+16,y+46,316,44,W.Upgrades.Description(kind),14,muted);
                if(Button(new Rect(x+16,y+102,316,30),BasePurchaseLabel(kind),Art.Cyan,W.Upgrades.CanBuy(kind)))W.Upgrades.Buy(kind);
            }
            RepairCard(new Rect(left+24,top+554,348,79),BaseUpgrade.RepairBarracks,"主兵营维修");
            RepairCard(new Rect(left+386,top+554,348,79),BaseUpgrade.RepairBunker,"东侧地堡维修");
            UpgradeCard(new Rect(left+748,top+554,348,79));
            Label(left+764,top+564,316,24,"中央射击阵地",16,Art.Amber);
            Paragraph(left+764,top+593,316,34,"到正面矮墙后按 G，让小队展开防守。",13,muted);

        }
        void ConstructionPage(float left,float top)
        {
            if(W.Construction==null||W.Tech==null)return;
            for(int i=0;i<W.Construction.Sites.Length;i++)
            {
                BuildSite site=W.Construction.Sites[i];
                BuildSiteCard(new Rect(left+24,top+207+i*86,710,79),site);
            }
            for(int i=0;i<3;i++)ResearchCard(new Rect(left+748,top+207+i*145,348,134),(Research)i,W.Tech.Name((Research)i));
        }
        void BuildSiteCard(Rect rect,BuildSite site)
        {
            UpgradeCard(rect);
            Color accent=site.State==SiteState.Destroyed?Art.Pink:site.State==SiteState.Constructing?Art.Amber:Art.Cyan;
            Label(rect.x+14,rect.y+8,220,25,site.Name+" · "+SiteStateName(site.State),17,white);
            string purpose=site.Id==0?"西路火力与五人驻堡席位":site.Id==1?"驻军上限 12 人，解锁三项科技":site.Id==2?"自动补员，维持基地驻军":site.Id==4?"自动生产一辆坦克，部署中央防线":"东路火力与五人驻堡席位";
            Health structure=site.Structure;
            bool alive=structure!=null&&structure.Alive;
            Label(rect.x+234,rect.y+11,218,22,alive?"耐久 "+Mathf.CeilToInt(structure.Current)+" / "+Mathf.CeilToInt(structure.Maximum):"施工 "+Mathf.CeilToInt(site.Duration)+" 秒",12,accent,TextAnchor.UpperRight);
            float progress=site.State==SiteState.Constructing?1-site.SecondsLeft/Mathf.Max(1,site.Duration):alive?structure.Current/structure.Maximum:0;
            Bar(new Rect(rect.x+14,rect.y+63,438,4),progress,accent);
            string status=purpose;
            if(site.State==SiteState.Constructing)status="施工剩余 "+Mathf.CeilToInt(site.SecondsLeft)+" 秒"+(W.Paused?" · 返回战场后继续":" · 正在施工");
            else if(site.State==SiteState.Ready&&alive&&structure.Current<structure.Maximum)status="维修可恢复 "+(site.Role==Role.Bunker?600:site.Id==4?700:450)+" 耐久";
            else if(site.Id==4&&site.State==SiteState.Ready&&W.Tanks!=null)status="战车状态 · "+W.Tanks.Status;
            else if((site.State==SiteState.Empty||site.State==SiteState.Destroyed)&&!W.Construction.SpaceClear(site.Id))status="施工位置被占用 · 请先移开人员";
            Label(rect.x+14,rect.y+35,438,23,status,13,muted);
            string caption;
            bool can;
            if(site.State==SiteState.Empty||site.State==SiteState.Destroyed)
            {
                int cost=W.Construction.Cost(site.Id);
                caption=W.Credits<cost?cost+" 补给 · 不足":(site.State==SiteState.Destroyed?"重建 · ":"建造 · ")+cost+" 补给";can=W.Construction.CanBuild(site.Id);
                if(Button(new Rect(rect.x+470,rect.y+23,224,33),caption,Art.Cyan,can))W.Construction.Build(site.Id);
            }
            else if(site.State==SiteState.Ready)
            {
                bool damaged=alive&&structure.Current<structure.Maximum;
                int cost=W.Construction.RepairCost(site.Id);
                caption=!damaged?"耐久充足":W.Credits<cost?cost+" 补给 · 不足":"维修 · "+cost+" 补给";
                can=W.Construction.CanRepairSite(site.Id);
                if(Button(new Rect(rect.x+470,rect.y+23,224,33),caption,Art.Cyan,can))W.Construction.RepairSite(site.Id);
            }
            else Button(new Rect(rect.x+470,rect.y+23,224,33),"施工中",Art.Amber,false);
        }
        string SiteStateName(SiteState state)=>state==SiteState.Empty?"待建":state==SiteState.Constructing?"施工中":state==SiteState.Ready?"已建成":"已摧毁";
        void ResearchCard(Rect rect,Research research,string title)
        {
            UpgradeCard(rect);
            bool complete=W.Tech.IsComplete(research),active=W.Tech.ActiveResearch==(int)research;
            Color accent=complete?Art.Cyan:active?Art.Amber:muted;
            Label(rect.x+16,rect.y+7,316,23,title,16,white);
            Paragraph(rect.x+16,rect.y+32,316,34,W.Tech.Description(research),12,muted);
            Label(rect.x+16,rect.y+69,316,21,W.Tech.Status(research),12,accent);
            Bar(new Rect(rect.x+16,rect.y+92,316,3),complete?1:active?1-W.Tech.SecondsLeft/Mathf.Max(1,W.Tech.Duration):0,accent);
            string caption=complete?"研究已完成":active?"剩余 "+Mathf.CeilToInt(W.Tech.SecondsLeft)+" 秒":"研究 · "+Price(W.Tech.Cost(research));
            if(Button(new Rect(rect.x+16,rect.y+103,316,24),caption,Art.Cyan,W.Tech.CanResearch(research)))W.Tech.Research(research);
        }
        void UpgradeCard(Rect r){Box(r,new Color(.065f,.12f,.15f));}
        string Price(int cost)=>W.Credits<cost?cost+" 补给 · 补给不足":cost+" 补给";
        string BasePurchaseLabel(BaseUpgrade kind)
        {
            if((int)kind<3)
            {
                if(W.Upgrades.Level(kind)>=BaseUpgrades.MaxLevel)return "已达满级";
                bool ready=kind==BaseUpgrade.BarracksProduction?W.BarracksBuildings.Exists(h=>h!=null&&h.Alive):W.Bunkers.Exists(h=>h!=null&&h.Alive);
                if(!ready)return kind==BaseUpgrade.BarracksProduction?"需要已投产的兵营":"需要已建成的地堡";
            }
            else
            {
                Health building=kind==BaseUpgrade.RepairBarracks?W.Barracks:W.Bunker;
                if(building==null)return "建筑重建中";
                if(!building.Alive)return "建筑已摧毁";
                if(building.Current>=building.Maximum)return "耐久充足";
            }
            return Price(W.Upgrades.Cost(kind));
        }
        void RepairCard(Rect rect,BaseUpgrade kind,string title)
        {
            UpgradeCard(rect);
            Label(rect.x+16,rect.y+9,185,24,title,16,white);
            Paragraph(rect.x+16,rect.y+35,187,37,W.Upgrades.Description(kind),12,muted);
            string caption=BasePurchaseLabel(kind);
            if(W.Upgrades.Cost(kind)>W.Credits&&caption.Contains("补给不足"))caption="补给不足";
            if(Button(new Rect(rect.x+212,rect.y+18,120,43),caption,Art.Cyan,W.Upgrades.CanBuy(kind)))W.Upgrades.Buy(kind);
        }
        void Pause(){Box(new Rect(0,0,width,height),new Color(.01f,.03f,.05f,.7f));SettingsCard(false);}
        void SettingsCard(bool menu)
        {
            float x=width/2-280,y=height/2-232;Panel(new Rect(x,y,560,464));
            Label(x+30,y+25,500,40,menu?"任务设置":"防线已暂停",30,white);
            Label(x+30,y+78,500,25,menu?"设置会保存在此设备":"切回游戏后，点击继续即可恢复鼠标控制",15,muted);
            Label(x+30,y+126,290,25,"总音量",17,white);
            W.MasterVolume=GUI.HorizontalSlider(new Rect(x+210,y+136,275,20),W.MasterVolume,0,1);
            Label(x+30,y+185,290,25,"鼠标灵敏度",17,white);
            W.Sensitivity=GUI.HorizontalSlider(new Rect(x+210,y+195,275,20),W.Sensitivity,.3f,3.5f);
            Label(x+30,y+243,290,25,"画面质量",17,white);
            string[] choices={"流畅","均衡","精细"};
            for(int i=0;i<3;i++)if(Button(new Rect(x+210+i*95,y+242,85,34),choices[i],i==W.Quality?Art.Cyan:muted))W.ApplyQuality(i);
            Label(x+30,y+302,500,28,"E 交互 / 进出 · B 小队入驻 · F 跟随 · G 驻守",14,muted);
            if(Button(new Rect(x+30,y+350,500,56),menu?"返回任务简报":"继续防守  →",Art.Cyan))
            {
                PlayerPrefs.SetFloat("Frontier.Volume",W.MasterVolume);PlayerPrefs.SetFloat("Frontier.Sensitivity",W.Sensitivity);PlayerPrefs.Save();
                settings=false;if(!menu)W.SetPaused(false);
            }
            Label(x+30,y+422,500,24,"备用弹药无限 · 阵亡后 6 秒在基地重生",13,muted,TextAnchor.UpperCenter);
        }
        void Result()
        {
            Box(new Rect(0,0,width,height),new Color(.012f,.03f,.048f,.8f));
            bool victory=W.Phase==Phase.Victory;Color accent=victory?Art.Cyan:Art.Pink;
            Label(width/2-450,height*.17f,900,28,victory?"M I S S I O N   C O M P L E T E":"S I G N A L   L O S T",18,accent,TextAnchor.UpperCenter);
            Label(width/2-450,height*.17f+58,900,90,victory?"黎明属于我们":"前哨失守",61,white,TextAnchor.UpperCenter);
            Label(width/2-450,height*.17f+162,900,34,victory?"五波攻势已全部清除，指挥中心仍在运转。":"指挥中心已被摧毁。重新部署，改写战局。",20,muted,TextAnchor.UpperCenter);
            float top=height*.53f;
            string[] nums={W.Kills.ToString(),W.PlayerKills.ToString(),W.DurationText()};string[] labels={"防线总击杀","你的击杀","坚守时间"};
            for(int i=0;i<3;i++){float x=width/2-405+i*280;Panel(new Rect(x,top,250,110));Label(x,top+15,250,46,nums[i],34,accent,TextAnchor.UpperCenter);Label(x,top+73,250,23,labels[i],16,muted,TextAnchor.UpperCenter);}
            if(Button(new Rect(width/2-180,height-169,360,59),"重新部署  →",accent))W.Restart();
            Label(width/2-450,height-70,900,24,"小队击杀 "+W.Squad.Kills+"  ·  坦克击杀 "+(W.Tanks!=null?W.Tanks.Kills:0)+"  ·  敌方炮塔 "+(W.EnemyDefenses!=null?W.EnemyDefenses.DestroyedCount:0)+" / 6  ·  边境防线 v0.6",14,muted,TextAnchor.UpperCenter);
        }
        string MemberStatus(int slot)
        {
            var m=W.Squad.Members[slot];
            if(m!=null&&m.Self.Alive)return (m.Self.IsSheltered?"驻堡 · ":"")+Mathf.CeilToInt(m.Self.Current);
            if(slot<2&&W.Squad.HasContract(slot))return "复活 "+Mathf.CeilToInt(W.Squad.RespawnRemaining(slot))+" 秒";
            return slot<2?"待雇佣":"可招募";
        }
        void SquadPanel()
        {
            float x=30,y=132;Panel(new Rect(x,y,292,266));
            Label(x+18,y+14,170,26,"先锋小队  "+W.Squad.Count+" / 4",18,Art.Amber);
            Label(x+189,y+17,84,23,W.Squad.Order==SquadOrder.Bunker?"入驻地堡":W.Squad.Order==SquadOrder.Follow?"跟随":"驻守",14,white,TextAnchor.UpperRight);
            for(int i=0;i<SquadDirector.Capacity;i++)
            {
                float row=y+57+i*43;var m=W.Squad.Members[i];bool alive=m!=null&&m.Self.Alive;
                Label(x+18,row,155,24,(i<2?"精英 ":"守卫 ")+SquadDirector.Names[i],14,alive?white:muted);
                Label(x+175,row,98,24,MemberStatus(i),12,alive?Art.Amber:muted,TextAnchor.UpperRight);
                Bar(new Rect(x+18,row+27,255,3),alive?m.Self.Current/m.Self.Maximum:0,i<2?Art.Amber:Art.Cyan);
            }
            Label(x+18,y+235,270,24,"F 跟随  /  G 驻守  /  B 入驻",13,muted);
        }
        void BunkerView()
        {
            Color frame=new Color(.025f,.04f,.045f,.82f),edge=new Color(.17f,.33f,.34f,.75f);
            Box(new Rect(0,0,width,118),frame);
            Box(new Rect(0,height-158,width,158),frame);
            Box(new Rect(0,116,width,2),edge);
            Box(new Rect(0,height-158,width,2),edge);
            Box(new Rect(0,118,12,height-276),frame);
            Box(new Rect(width-12,118,12,height-276),frame);
        }
        string BuildingName(Health building)
        {
            if(building==null)return "地堡";
            if(building.Faction==Faction.Invaders)return "敌巢炮塔 · 摧毁奖励 "+EnemyDefenseDirector.Reward;
            return W.Construction!=null?W.Construction.NameFor(building):building.Role==Role.Command?"指挥中心":building.Role==Role.Barracks?"兵营":"地堡";
        }
        string Durability(Health building)=>building==null?"重建中":Mathf.CeilToInt(building.Current)+" / "+Mathf.CeilToInt(building.Maximum);
        void BunkerPanel()
        {
            float x=width-253,y=328;Panel(new Rect(x,y,223,165));
            Health bunker=W.Fort!=null?W.Fort.DisplayBunker:W.Bunker;
            bool alive=bunker!=null&&bunker.Alive;
            Label(x+16,y+12,120,25,BuildingName(bunker),17,alive?Art.Cyan:Art.Pink);
            Label(x+136,y+16,71,23,alive?"可驻守":"未就绪",13,alive?white:Art.Pink,TextAnchor.UpperRight);
            Label(x+16,y+45,191,23,bunker==null?"暂无可用地堡":Durability(bunker)+" 耐久",14,white);
            Bar(new Rect(x+16,y+73,191,5),alive?bunker.Current/bunker.Maximum:0,alive?Art.Cyan:Art.Pink);
            Label(x+16,y+89,191,23,"席位 "+(W.Fort!=null?W.Fort.OccupantsFor(bunker):0)+" / "+BunkerDirector.Capacity+" · 小队 "+(W.Fort!=null?W.Fort.SquadInsideFor(bunker):0)+" 人",13,white);
            Label(x+16,y+116,191,23,W.Fort!=null&&W.Fort.PlayerInside?"E 退出 · 你正在驻守":"E 进入 · 靠近后方入口",12,muted);
            Label(x+16,y+139,191,21,"守卫 "+W.Fort.GuardInsideFor(bunker)+" 人 · 优先为你让位",12,muted);
        }
        void EngineeringPanel()
        {
            if(W.Construction==null||W.Tech==null)return;
            int count=0;foreach(var site in W.Construction.Sites)if(site.State==SiteState.Constructing)count++;
            bool research=W.Tech.ActiveResearch>=0;if(count==0&&!research)return;
            float x=30,y=412;Panel(new Rect(x,y,292,48+count*34+(research?62:0)));
            Label(x+18,y+12,255,25,"基地工程",16,Art.Cyan);
            float row=y+43;
            foreach(var site in W.Construction.Sites)
            {
                if(site.State!=SiteState.Constructing)continue;
                Label(x+18,row,190,22,site.Name,13,white);
                Label(x+218,row,55,22,Mathf.CeilToInt(site.SecondsLeft)+" 秒",13,Art.Amber,TextAnchor.UpperRight);
                Bar(new Rect(x+18,row+25,255,3),1-site.SecondsLeft/Mathf.Max(1,site.Duration),Art.Amber);row+=34;
            }
            if(research)
            {
                Research kind=(Research)W.Tech.ActiveResearch;
                Label(x+18,row,255,22,"研究 · "+W.Tech.Name(kind),13,white);
                Label(x+18,row+25,255,23,W.Tech.Status(kind),12,Art.Amber);
                Bar(new Rect(x+18,row+51,255,3),1-W.Tech.SecondsLeft/Mathf.Max(1,W.Tech.Duration),Art.Amber);
            }
        }
        void TankPanel()
        {
            if(W.Tanks==null)return;
            float x=width-253,y=507;Panel(new Rect(x,y,223,124));
            Health tank=W.Tanks.Tank;bool alive=tank!=null&&tank.Alive;
            Color accent=alive?Art.Cyan:W.Tanks.Operational?Art.Amber:muted;
            Label(x+16,y+11,135,25,"基地战车",17,accent);
            Label(x+146,y+15,61,23,(alive?"1":"0")+" / 1",14,white,TextAnchor.UpperRight);
            Label(x+16,y+43,191,24,W.Tanks.Status,14,white);
            float progress=alive?tank.Current/tank.Maximum:W.Tanks.Operational?1-W.Tanks.ProductionLeft/TankDirector.ProductionDuration:0;
            Bar(new Rect(x+16,y+73,191,4),progress,accent);
            Label(x+16,y+88,191,25,alive?"耐久 "+Mathf.CeilToInt(tank.Current)+" / "+Mathf.CeilToInt(tank.Maximum):"建造工厂后自动生产",12,muted);
        }
        void CommandTerminal()
        {
            Box(new Rect(0,0,width,height),new Color(.01f,.03f,.05f,.75f));
            float left=width/2-560,top=height/2-364;Panel(new Rect(left,top,1120,728));
            Label(left+24,top+21,780,39,"指挥中心 / 基地指挥",28,white);
            Label(left+804,top+28,292,29,"可用补给  "+W.Credits,22,Art.Amber,TextAnchor.UpperRight);
            PageTabs(left,top);
            Label(left+578,top+72,518,24,W.Phase==Phase.Intermission?"整备暂停 · 剩余 "+Mathf.CeilToInt(W.IntermissionLeft)+" 秒":"战场已暂停 · 可随时返回防守",14,Art.Amber,TextAnchor.UpperRight);
            Box(new Rect(left+24,top+111,1072,79),new Color(.055f,.14f,.17f));
            string[] titles={"升级火力、强化建筑，随时维修受损防线","建造战车工厂，部署中央火力支援","四人小队 · 2 名雇佣兵 + 2 名随行守卫"};
            string[] hints={"个人与防线升级立即生效；所有升级、建造、雇佣与维修均在此下单。","工厂自动维持一辆坦克；增援兵营提高驻军上限，并解锁三项科技。","B 入驻附近地堡 · F 撤出跟随 · G 守住此处 · 射击标记集火目标"};
            Label(left+40,top+120,1038,27,titles[terminalPage],18,Art.Cyan);
            Label(left+40,top+154,1036,27,hints[terminalPage],15,white);
            if(terminalPage==0)DefenseUpgrades(left,top);else if(terminalPage==1)ConstructionPage(left,top);else SquadTerminalPage(left,top);
            Label(left+25,top+655,696,24,"返回战场后，施工、研究与坦克生产继续推进。",15,white);
            Label(left+25,top+686,696,23,"E / Esc 关闭终端 · 波间可自由部署，倒计时结束自动开战。",13,muted);
            if(W.Phase==Phase.Intermission)
            {
                if(Button(new Rect(left+748,top+653,166,51),"立即下一波",Art.Amber))W.StartWave();
                if(Button(new Rect(left+926,top+653,170,51),"返回战场  →",Art.Cyan))W.SetPaused(false);
            }
            else if(Button(new Rect(left+748,top+653,348,51),"返回战场  →",Art.Cyan))W.SetPaused(false);
        }
        void SquadTerminalPage(float left,float top)
        {
            for(int i=0;i<SquadDirector.Capacity;i++)
            {
                float x=left+24+i%2*543,y=top+207+i/2*156;var m=W.Squad.Members[i];bool alive=m!=null&&m.Self.Alive;
                UpgradeCard(new Rect(x,y,529,145));
                Label(x+20,y+14,340,30,(i<2?"精英雇佣兵 / ":"随行守卫 / ")+SquadDirector.Names[i],20,i<2?Art.Amber:Art.Cyan);
                Label(x+356,y+19,153,25,MemberStatus(i),15,alive?white:muted,TextAnchor.UpperRight);
                Paragraph(x+20,y+53,489,47,i<2?"120 补给签约 · 阵亡 12 秒后免费复活\n基础耐久 280 / 伤害 18 · 专属科技可再强化":"瞄准附近守卫按 E 免费招募\n基础耐久 170 / 伤害 10 · 阵亡后重新招募",14,muted);
                Bar(new Rect(x+20,y+117,489,5),alive?m.Self.Current/m.Self.Maximum:0,i<2?Art.Amber:Art.Cyan);
            }
            Label(left+25,top+538,696,28,"雇佣兵 "+W.Squad.MercenaryCount+" / 2 · 随行守卫 "+W.Squad.RecruitedGuardCount+" / 2 · 基地守卫 "+W.MarinesAlive+" / "+W.MarineCapacity,19,white);
            if(Button(new Rect(left+748,top+534,348,43),W.Squad.Contracts>=2?"雇佣兵合同已满":"雇佣精英 · "+Price(SquadDirector.HireCost),Art.Amber,W.Squad.CanHire&&W.Credits>=SquadDirector.HireCost))W.Squad.Hire();
            Paragraph(left+25,top+590,1070,43,"普通守卫会就近驻堡，并为玩家与小队让位。免费招募后，基地会自动补足空缺。\nB 小队入堡 · F 撤出跟随 · G 原地驻守。",15,muted);
        }
        void HitFeedback()
        {
            foreach(var feedback in W.Player.Feedback)
            {
                Vector3 p=W.Player.View.WorldToScreenPoint(feedback.Position+Vector3.up*(.7f+(1-feedback.Life/.7f)*.7f));if(p.z<=0)continue;
                Color c=feedback.Critical?Art.Amber:white;c.a=Mathf.Clamp01(feedback.Life*5);
                Label(p.x/scale-60,(Screen.height-p.y)/scale,120,32,Mathf.CeilToInt(feedback.Damage).ToString(),feedback.Critical?23:19,c,TextAnchor.UpperCenter);
            }
            Health target=W.Squad.FocusTarget;
            if(target!=null&&target.Alive&&W.Squad.Count>0&&Combat.Visible(W.Player.Self,target))
            {
                Vector3 p=W.Player.View.WorldToScreenPoint(target.Aim+Vector3.up*1.4f);if(p.z<=0)return;
                Label(p.x/scale-75,(Screen.height-p.y)/scale-24,150,24,"▼ 小队集火",14,Art.Amber,TextAnchor.UpperCenter);
            }
        }
        void Line(Vector2 a,Vector2 b,Color color,float thickness)
        {
            Matrix4x4 old=GUI.matrix;Vector2 d=b-a;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg,a);Box(new Rect(a.x,a.y-thickness/2,d.magnitude,thickness),color);GUI.matrix=old;
        }
        void Panel(Rect r){Box(r,ink);Box(new Rect(r.x,r.y,3,r.height),new Color(.15f,.36f,.40f,.9f));}
        void Bar(Rect r,float value,Color color){Box(r,new Color(.15f,.23f,.26f,.9f));Box(new Rect(r.x,r.y,r.width*Mathf.Clamp01(value),r.height),color);}
        void Box(Rect r,Color color){Color old=GUI.color;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        void Label(float x,float y,float w,float h,string value,int size,Color color,TextAnchor align=TextAnchor.UpperLeft)
        {
            text.fontSize=size;text.normal.textColor=color;text.alignment=align;GUI.Label(new Rect(x,y,w,h),value,text);
        }
        void Paragraph(float x,float y,float w,float h,string value,int size,Color color)
        {
            text.wordWrap=true;Label(x,y,w,h,value,size,color);text.wordWrap=false;
        }
        bool Button(Rect r,string value,Color color,bool enabled=true)
        {
            Color old=GUI.backgroundColor;GUI.backgroundColor=enabled?color:new Color(.25f,.31f,.33f);GUI.enabled=enabled;
            bool clicked=GUI.Button(r,value,button);GUI.enabled=true;GUI.backgroundColor=old;return clicked;
        }
    }
}

using UnityEngine;

namespace Frontier
{
    public sealed class GameHud : MonoBehaviour
    {
        readonly Color ink=new Color(.025f,.055f,.08f,.94f), muted=new Color(.55f,.66f,.70f),white=new Color(.91f,.96f,.96f);
        Font font;GUIStyle text,button;float width,height,scale;bool settings;
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
                float toastY=W.Phase==Phase.Intermission?height-64:118;
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
            Label(63,height*.24f+195,570,60,"带领三人小队，进驻地堡或支援防线。\n波间升级兵营、强化地堡，抵御更密集的敌潮。",17,muted);
            if(Button(new Rect(62,height-226,300,58),"部署到防线  →",Art.Cyan))W.BeginCampaign();
            if(Button(new Rect(378,height-226,144,58),"设置",new Color(.39f,.50f,.55f)))settings=true;
            Label(62,height-142,650,27,"W A S D  移动     鼠标  视角     左键  射击",16,white);
            Label(62,height-109,650,27,"右键  瞄准     R  换弹     Shift  冲刺     Esc  暂停",16,muted);
            Label(62,height-78,650,24,"E  交互 / 进出地堡     B  小队入驻     F  撤出跟随",15,Art.Amber);
            Label(62,height-39,750,24,"单人试玩  v0.3   /   地堡与防线经营   /   键盘与鼠标",13,muted);
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
            MiniMap();
            SquadPanel();
            BunkerPanel();
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
            Label(width/2-220,height-66,440,25,"补给  "+W.Credits+"     驻军  "+W.MarinesAlive+" / 8     击杀  "+W.Kills,16,white,TextAnchor.UpperCenter);
            Label(width/2-325,height-36,650,24,"R 换弹   E 交互   B 入驻地堡   F 跟随   G 驻守   Esc 暂停",13,muted,TextAnchor.UpperCenter);
            if(W.Phase==Phase.Assault && !W.Paused && W.Player.Self.Alive)
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
                if(W.Fort!=null&&(W.Fort.PlayerInside||W.Fort.NearEntrance))
                {
                    Panel(new Rect(x-245,height-239,490,69));
                    Label(x-230,height-229,460,26,W.Fort.PlayerInside?"地堡驻守 · 射界 ±70° · E 退出":"E  进入地堡  ·  席位 "+W.Fort.Occupants+" / 4",19,Art.Cyan,TextAnchor.UpperCenter);
                    Label(x-230,height-199,460,23,"B  小队入驻      F  小队撤出并跟随",14,white,TextAnchor.UpperCenter);
                }
                else if(W.NearCommand)
                {
                    Panel(new Rect(x-192,height-228,384,49));
                    Label(x-180,height-213,360,26,"E  指挥中心 · 雇佣小队队员",18,Art.Amber,TextAnchor.UpperCenter);
                }
                if(W.WaveBannerLeft>0)
                {
                    Label(x-400,178,800,40,W.Banner,29,white,TextAnchor.UpperCenter);
                    Label(x-400,223,800,25,W.Subtitle,16,Art.Amber,TextAnchor.UpperCenter);
                }
            }
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
            foreach(var h in W.Entities)
            {
                if(h==null||!h.Alive)continue;
                Vector2 p=MapPoint(map,h.transform.position);
                if(!map.Contains(p))continue;
                float s=h.Role==Role.Player?7:h.IsBuilding?8:h.Role==Role.Colossus?6:4;
                Color c=h.Faction==Faction.Invaders?Art.Pink:h.Role==Role.Player?Color.white:h.Role==Role.Squad?Art.Amber:Art.Cyan;
                Box(new Rect(p.x-s/2,p.y-s/2,s,s),c);
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
        Vector2 MapPoint(Rect r,Vector3 p)=>new Vector2(r.x+(p.x+32)/64*r.width,r.y+(72-p.z)/100*r.height);
        void MapRect(Rect r,Vector3 p,Vector2 size){Vector2 t=MapPoint(r,p);Box(new Rect(t.x-size.x/64*r.width/2,t.y-size.y/100*r.height/2,size.x/64*r.width,size.y/100*r.height),new Color(.12f,.21f,.23f));}
        void MapRect(Rect r,Vector3 p,Vector2 size,Color color){Vector2 t=MapPoint(r,p);Box(new Rect(t.x-size.x/64*r.width/2,t.y-size.y/100*r.height/2,size.x/64*r.width,size.y/100*r.height),color);}
        void WorldLabels()
        {
            if(W.Paused||W.Phase==Phase.Intermission)return;
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
                    string name=h.Role==Role.Command?"指挥中心":h.Role==Role.Barracks?"兵营 · "+W.MarinesAlive+"/8":"地堡 · "+(W.Fort!=null?W.Fort.Occupants:0)+" / 4 席位";
                    Label(x-95,y-25,190,25,name,13,Art.Cyan,TextAnchor.UpperCenter);
                }
                if(companion)Label(x-70,y-23,140,24,"0"+(h.Slot+1)+" / "+SquadDirector.Names[h.Slot],13,Art.Amber,TextAnchor.UpperCenter);
                Bar(new Rect(x-(building?45:24),y,building?90:48,4),h.Current/h.Maximum,h.Faction==Faction.Invaders?Art.Pink:companion?Art.Amber:Art.Cyan);
            }
        }
        void Intermission()
        {
            Box(new Rect(0,0,width,height),new Color(.01f,.025f,.04f,.64f));
            float left=width/2-560,top=height/2-364;
            Panel(new Rect(left,top,1120,728));
            Label(left+24,top+21,780,39,"第 "+W.Wave+" 波已清除 / 整备防线",28,white);
            Label(left+804,top+28,292,29,"可用补给  "+W.Credits,22,Art.Amber,TextAnchor.UpperRight);
            Label(left+25,top+65,1070,24,"根据下一波情报选择升级；部署与火力，都由你决定。",15,muted);
            Box(new Rect(left+24,top+96,1072,86),new Color(.055f,.14f,.17f));
            int next=Mathf.Min(W.Wave+1,WavePlan.Counts.Length);
            Label(left+40,top+107,1038,28,"下一波  "+next+" / 5  ·  "+WavePlan.Titles[next-1],19,Art.Cyan);
            Paragraph(left+40,top+141,1036,36,WavePlan.Preview(next),16,white);

            Label(left+25,top+195,680,24,"个人与防线",16,muted);
            string[] names={"磁轨增幅","协同火力","指挥中心维修"};
            string[] descriptions={"步枪每发伤害 +6\n当前伤害 "+W.RifleDamage.ToString("0"),"驻军、小队与地堡伤害提升\n强化整条防线的持续火力","恢复指挥中心 650 耐久\n当前 "+Mathf.CeilToInt(W.Command.Current)+" / "+Mathf.CeilToInt(W.Command.Maximum)};
            for(int i=0;i<3;i++)
            {
                float x=left+24+i*362,y=top+224;UpgradeCard(new Rect(x,y,348,128));
                Label(x+16,y+11,240,25,names[i],19,white);
                if(i<2)Label(x+237,y+15,95,23,(i==0?W.WeaponLevel:W.DefenseLevel)+" / 4 级",13,muted,TextAnchor.UpperRight);
                Paragraph(x+16,y+44,316,44,descriptions[i],14,muted);
                bool max=i==0?W.WeaponLevel>=4:i==1?W.DefenseLevel>=4:W.Command.Current>=W.Command.Maximum;
                bool can=!max&&!W.Paused&&W.Credits>=W.UpgradeCost(i);
                string caption=max?(i==2?"耐久充足":"已达满级"):Price(W.UpgradeCost(i));
                if(Button(new Rect(x+16,y+92,316,27),caption,Art.Amber,can))W.Buy(i);
            }

            Label(left+25,top+367,310,24,"建筑专属升级",16,muted);
            Label(left+400,top+369,696,23,"兵营补员 "+W.ProductionInterval.ToString("0")+" 秒 / 人  ·  地堡 "+Mathf.CeilToInt(W.Bunker.Current)+" / "+Mathf.CeilToInt(W.Bunker.Maximum),13,muted,TextAnchor.UpperRight);
            BaseUpgrade[] upgrades={BaseUpgrade.BarracksProduction,BaseUpgrade.BunkerArmor,BaseUpgrade.BunkerFirepower};
            string[] titles={"兵营 · 快速补员","地堡 · 强化装甲","地堡 · 自动炮强化"};
            for(int i=0;i<upgrades.Length;i++)
            {
                var kind=upgrades[i];float x=left+24+i*362,y=top+396;UpgradeCard(new Rect(x,y,348,142));
                Label(x+16,y+11,240,26,titles[i],18,Art.Cyan);
                Label(x+237,y+15,95,23,W.Upgrades.Level(kind)+" / "+BaseUpgrades.MaxLevel+" 级",13,muted,TextAnchor.UpperRight);
                Paragraph(x+16,y+46,316,44,W.Upgrades.Description(kind),14,muted);
                if(Button(new Rect(x+16,y+102,316,30),BasePurchaseLabel(kind),Art.Cyan,W.Upgrades.CanBuy(kind)))W.Upgrades.Buy(kind);
            }
            RepairCard(new Rect(left+24,top+554,348,79),BaseUpgrade.RepairBarracks,"兵营维修");
            RepairCard(new Rect(left+386,top+554,348,79),BaseUpgrade.RepairBunker,"地堡维修");
            UpgradeCard(new Rect(left+748,top+554,348,79));
            Label(left+764,top+564,316,24,"小队增援  "+W.Squad.Count+" / 3",16,Art.Amber);
            if(Button(new Rect(left+764,top+593,316,29),W.Squad.Count>=3?"小队已满编":"雇佣队员 · "+Price(SquadDirector.HireCost),Art.Amber,W.Squad.CanHire&&W.Squad.Count<3&&W.Credits>=SquadDirector.HireCost))W.Squad.Hire();

            Label(left+25,top+654,676,24,"开战后操作：B 小队入堡   ·   F 撤出跟随   ·   G 驻守",15,white);
            Label(left+25,top+686,676,23,"整备剩余 "+Mathf.CeilToInt(W.IntermissionLeft)+" 秒  ·  建筑摧毁后无法维修",13,muted);
            if(Button(new Rect(left+748,top+653,348,51),"部署下一波  →",Art.Cyan))W.StartWave();
        }
        void UpgradeCard(Rect r){Box(r,new Color(.065f,.12f,.15f));}
        string Price(int cost)=>W.Credits<cost?cost+" 补给 · 补给不足":cost+" 补给";
        string BasePurchaseLabel(BaseUpgrade kind)
        {
            Health building=kind==BaseUpgrade.BarracksProduction||kind==BaseUpgrade.RepairBarracks?W.Barracks:W.Bunker;
            if(building==null||!building.Alive)return "建筑已摧毁";
            if((int)kind<3&&W.Upgrades.Level(kind)>=BaseUpgrades.MaxLevel)return "已达满级";
            if((int)kind>=3&&building.Current>=building.Maximum)return "耐久充足";
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
            Label(width/2-450,height-70,900,24,"小队击杀 "+W.Squad.Kills+"  ·  弱点命中 "+W.Player.CriticalHits+"  ·  边境防线 v0.3",14,muted,TextAnchor.UpperCenter);
        }
        void SquadPanel()
        {
            float x=30,y=132;Panel(new Rect(x,y,292,220));
            Label(x+18,y+14,170,26,"先锋小队  "+W.Squad.Count+" / 3",18,Art.Amber);
            Label(x+189,y+17,84,23,W.Squad.Order==SquadOrder.Bunker?"入驻地堡":W.Squad.Order==SquadOrder.Follow?"跟随":"驻守",14,white,TextAnchor.UpperRight);
            for(int i=0;i<3;i++)
            {
                float row=y+57+i*43;var m=W.Squad.Members[i];bool alive=m!=null&&m.Self.Alive;
                Label(x+18,row,155,24,"0"+(i+1)+"  "+SquadDirector.Names[i],15,alive?white:muted);
                Label(x+189,row,84,24,alive?(m.Self.IsSheltered?"驻堡 · ":"")+Mathf.CeilToInt(m.Self.Current):"待雇佣",13,alive?Art.Amber:muted,TextAnchor.UpperRight);
                Bar(new Rect(x+18,row+27,255,3),alive?m.Self.Current/m.Self.Maximum:0,Art.Amber);
            }
            Label(x+18,y+190,270,24,"F 跟随  /  G 驻守  /  B 入驻",13,muted);
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
        void BunkerPanel()
        {
            float x=width-253,y=328;Panel(new Rect(x,y,223,165));
            bool alive=W.Bunker!=null&&W.Bunker.Alive;
            Label(x+16,y+12,120,25,"前沿地堡",17,alive?Art.Cyan:Art.Pink);
            Label(x+136,y+16,71,23,alive?"可驻守":"已摧毁",13,alive?white:Art.Pink,TextAnchor.UpperRight);
            Label(x+16,y+45,191,23,Mathf.CeilToInt(W.Bunker.Current)+" / "+Mathf.CeilToInt(W.Bunker.Maximum)+" 耐久",14,white);
            Bar(new Rect(x+16,y+73,191,5),W.Bunker.Current/W.Bunker.Maximum,alive?Art.Cyan:Art.Pink);
            Label(x+16,y+89,191,23,"席位 "+(W.Fort!=null?W.Fort.Occupants:0)+" / 4  ·  小队 "+(W.Fort!=null?W.Fort.SquadInside:0)+" 人",13,white);
            Label(x+16,y+116,191,23,W.Fort!=null&&W.Fort.PlayerInside?"E 退出 · 你正在驻守":"E 进入 · 靠近后方入口",12,muted);
            Label(x+16,y+139,191,21,"B 小队入驻 / F 撤出跟随",12,muted);
        }
        void CommandTerminal()
        {
            Box(new Rect(0,0,width,height),new Color(.01f,.03f,.05f,.75f));
            float x=width/2-360,y=height/2-243;Panel(new Rect(x,y,720,486));
            Label(x+30,y+27,660,38,"指挥中心 / 小队终端",29,white);
            Label(x+30,y+78,660,26,"战场已暂停 · 雇佣 120 补给 / 人 · 最多 3 名队员",17,muted);
            for(int i=0;i<3;i++)
            {
                float left=x+30+i*225;var m=W.Squad.Members[i];bool alive=m!=null&&m.Self.Alive;
                Box(new Rect(left,y+126,210,136),new Color(.07f,.135f,.17f));
                Label(left+18,y+141,175,28,"0"+(i+1)+" / "+SquadDirector.Names[i],20,alive?Art.Amber:muted);
                Label(left+18,y+188,175,26,alive?"已入队 · 生命 "+Mathf.CeilToInt(m.Self.Current):"等待增援",15,white);
                Bar(new Rect(left+18,y+231,174,5),alive?m.Self.Current/m.Self.Maximum:0,Art.Amber);
            }
            Label(x+30,y+292,290,29,"可用补给  "+W.Credits,22,Art.Amber);
            if(Button(new Rect(x+370,y+280,320,52),W.Squad.Count>=3?"小队已满编":"雇佣一名队员  ·  120",Art.Amber,W.Squad.CanHire&&W.Squad.Count<3&&W.Credits>=SquadDirector.HireCost))W.Squad.Hire();
            Label(x+30,y+351,660,48,"B：入驻地堡    F：撤出跟随    G：守住此处\n射击敌人即可标记集火目标；队员脱离伤害后自动修复。",16,muted);
            if(Button(new Rect(x+30,y+419,660,43),"返回防线  →",Art.Cyan))W.SetPaused(false);
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

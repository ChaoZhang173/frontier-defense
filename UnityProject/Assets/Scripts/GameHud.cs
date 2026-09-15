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
            if(W.Paused)Pause();
            if(W.ToastLeft>0&&!W.Paused)
            {
                Panel(new Rect(width/2-265,118,530,43));
                Label(width/2-250,128,500,25,W.Toast,16,Art.Amber,TextAnchor.UpperCenter);
            }
        }
        void Briefing()
        {
            Box(new Rect(0,0,width*.52f,height),new Color(.017f,.042f,.066f,.9f));
            Label(60,50,600,30,"F R O N T I E R   /   0 7",18,Art.Cyan);
            Box(new Rect(60,102,44,3),Art.Amber);
            Label(58,height*.24f,600,90,"边境防线",66,white);
            Label(63,height*.24f+94,540,30,"守住基地，直到黎明。",23,Art.Amber);
            Label(63,height*.24f+156,570,40,"一名装甲步兵。两处入口。五波异星攻势。",18,white);
            Label(63,height*.24f+195,570,60,"兵营持续补员，地堡提供火力支援。\n在波间升级装备，让指挥中心撑到最后。",17,muted);
            if(Button(new Rect(62,height-226,300,58),"部署到防线  →",Art.Cyan))W.BeginCampaign();
            if(Button(new Rect(378,height-226,144,58),"设置",new Color(.39f,.50f,.55f)))settings=true;
            Label(62,height-142,650,27,"W A S D  移动     鼠标  视角     左键  射击",16,white);
            Label(62,height-109,650,27,"右键  瞄准     R  换弹     Shift  冲刺     Esc  暂停",16,muted);
            Label(62,height-49,750,24,"单人试玩  v0.1   /   原创科幻基地防守   /   建议键盘与鼠标",13,muted);
            Label(width-355,52,295,28,"峡谷前哨 / 清晨 05:47",16,white,TextAnchor.UpperRight);
            Label(width-355,86,295,25,"任务时长约 10–15 分钟",14,muted,TextAnchor.UpperRight);
            if(settings){Box(new Rect(0,0,width,height),new Color(0,0,0,.55f));SettingsCard(true);}
        }
        void Hud()
        {
            Panel(new Rect(30,26,364,84));
            Label(48,40,300,24,"指挥中心",16,white);
            Label(255,41,120,23,Mathf.CeilToInt(W.Command.Current)+" / 2300",14,muted,TextAnchor.UpperRight);
            Bar(new Rect(48,77,328,10),W.Command.Current/W.Command.Maximum,W.Command.Current>700?Art.Cyan:Art.Pink);
            Panel(new Rect(width/2-180,26,360,84));
            Label(width/2-160,38,320,25,"第 "+W.Wave+" / 5 波  ·  "+WavePlan.Titles[Mathf.Max(0,W.Wave-1)],20,white,TextAnchor.UpperCenter);
            Label(width/2-160,76,320,22,W.Phase==Phase.Intermission?"波间整备":"场上 "+W.EnemiesAlive+"   /   增援待抵达 "+W.RemainingToSpawn,14,Art.Amber,TextAnchor.UpperCenter);
            MiniMap();
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
            Label(width/2-245,height-36,490,24,"R 换弹    右键瞄准    Shift 冲刺    Esc 暂停",13,muted,TextAnchor.UpperCenter);
            if(W.Phase==Phase.Assault && !W.Paused && W.Player.Self.Alive)
            {
                float x=width/2,y=height/2,gap=W.Player.Aiming?5:9;
                Color c=W.Player.KillTimer>0?Art.Pink:W.Player.HitTimer>0?Art.Amber:white;
                Box(new Rect(x-gap-8,y-1,8,2),c);Box(new Rect(x+gap,y-1,8,2),c);
                Box(new Rect(x-1,y-gap-8,2,8),c);Box(new Rect(x-1,y+gap,2,8),c);
                Box(new Rect(x-1,y-1,2,2),c);
                if(W.Player.KillTimer>0)Label(x-65,y+38,130,25,"击杀确认",14,Art.Pink,TextAnchor.UpperCenter);
                if(W.WaveBannerLeft>0)
                {
                    Label(x-400,178,800,40,W.Banner,29,white,TextAnchor.UpperCenter);
                    Label(x-400,223,800,25,W.Subtitle,16,Art.Amber,TextAnchor.UpperCenter);
                }
            }
        }
        void MiniMap()
        {
            Rect outer=new Rect(width-253,26,223,254);Panel(outer);
            Label(outer.x+16,outer.y+12,192,25,"战术地图  /  "+W.DurationText(),14,muted);
            Rect map=new Rect(outer.x+16,outer.y+47,191,165);
            Box(map,new Color(.065f,.115f,.14f));
            MapRect(map,new Vector3(0,0,0),new Vector2(56,50),new Color(.13f,.22f,.25f));
            MapRect(map,new Vector3(-13.5f,0,36),new Vector2(9,23),new Color(.25f,.32f,.32f));
            MapRect(map,new Vector3(13.5f,0,36),new Vector2(9,23),new Color(.25f,.32f,.32f));
            int west=0,east=0;
            foreach(var h in W.Entities)
            {
                if(h==null||!h.Alive)continue;
                Vector2 p=MapPoint(map,h.transform.position);
                if(!map.Contains(p))continue;
                float s=h.Role==Role.Player?7:h.IsBuilding?8:h.Role==Role.Colossus?6:4;
                Color c=h.Faction==Faction.Invaders?Art.Pink:h.Role==Role.Player?Color.white:Art.Cyan;
                Box(new Rect(p.x-s/2,p.y-s/2,s,s),c);
                if(h.Faction==Faction.Invaders){if(h.Lane==0)west++;else east++;}
            }
            Label(outer.x+16,outer.y+220,191,24,"A 西 "+west+"          B 东 "+east,14,Art.Amber,TextAnchor.UpperCenter);
        }
        Vector2 MapPoint(Rect r,Vector3 p)=>new Vector2(r.x+(p.x+32)/64*r.width,r.y+(72-p.z)/100*r.height);
        void MapRect(Rect r,Vector3 p,Vector2 size){Vector2 t=MapPoint(r,p);Box(new Rect(t.x-size.x/64*r.width/2,t.y-size.y/100*r.height/2,size.x/64*r.width,size.y/100*r.height),new Color(.12f,.21f,.23f));}
        void MapRect(Rect r,Vector3 p,Vector2 size,Color color){Vector2 t=MapPoint(r,p);Box(new Rect(t.x-size.x/64*r.width/2,t.y-size.y/100*r.height/2,size.x/64*r.width,size.y/100*r.height),color);}
        void WorldLabels()
        {
            if(W.Paused||W.Phase==Phase.Intermission)return;
            foreach(var h in W.Entities)
            {
                if(h==null||!h.Alive||h.Role==Role.Player)continue;
                bool building=h.IsBuilding;
                if(!building && (h.Faction==Faction.Defense||W.SimTime-h.LastDamageTime>3))continue;
                if(Vector3.Distance(h.Aim,W.Player.transform.position)>42)continue;
                Vector3 p=W.Player.View.WorldToScreenPoint(h.Aim+Vector3.up*(building?3.1f:h.AimHeight*.8f));
                if(p.z<=0)continue;float x=p.x/scale,y=(Screen.height-p.y)/scale;
                if(x<20||x>width-20||y<120||y>height-160)continue;
                if(building)
                {
                    string name=h.Role==Role.Command?"指挥中心":h.Role==Role.Barracks?"兵营 · "+W.MarinesAlive+"/8":"地堡 · 自动防御";
                    Label(x-95,y-25,190,25,name,13,Art.Cyan,TextAnchor.UpperCenter);
                }
                Bar(new Rect(x-(building?45:24),y,building?90:48,4),h.Current/h.Maximum,h.Faction==Faction.Invaders?Art.Pink:Art.Cyan);
            }
        }
        void Intermission()
        {
            float left=width/2-447,top=height/2-215;
            Panel(new Rect(left,top,894,390));
            Label(left+28,top+23,820,39,"第 "+W.Wave+" 波已清除 / 整备防线",28,white);
            Label(left+29,top+72,820,25,"选择补给升级。剩余 "+Mathf.CeilToInt(W.IntermissionLeft)+" 秒后下一波抵达。",16,muted);
            string[] names={"磁轨增幅","协同火力","紧急维修"};
            string[] descriptions={"步枪伤害 +6\n当前等级 "+W.WeaponLevel+" / 4","驻军与地堡伤害提升\n当前等级 "+W.DefenseLevel+" / 4","指挥中心恢复 650 耐久\n当前耐久 "+Mathf.CeilToInt(W.Command.Current)};
            for(int i=0;i<3;i++)
            {
                float x=left+28+i*282;Box(new Rect(x,top+124,272,170),new Color(.075f,.14f,.17f));
                Label(x+17,top+137,240,28,names[i],20,Art.Cyan);
                Label(x+17,top+178,240,57,descriptions[i],15,muted);
                bool max=i==0?W.WeaponLevel>=4:i==1?W.DefenseLevel>=4:W.Command.Current>=W.Command.Maximum;
                bool can=!max&&W.Credits>=W.UpgradeCost(i);
                if(Button(new Rect(x+16,top+244,240,35),max?"已完成":W.UpgradeCost(i)+" 补给",Art.Amber,can))W.Buy(i);
            }
            Label(left+29,top+329,340,27,"可用补给  "+W.Credits,20,Art.Amber);
            if(Button(new Rect(left+582,top+323,284,43),"继续防守  →",Art.Cyan))W.StartWave();
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
            Label(x+30,y+302,500,28,"WASD 移动 · 左键射击 · 右键瞄准 · R 换弹",14,muted);
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
            Label(width/2-450,height-70,900,24,"感谢试玩 · 边境防线 v0.1",14,muted,TextAnchor.UpperCenter);
        }
        void Panel(Rect r){Box(r,ink);Box(new Rect(r.x,r.y,3,r.height),new Color(.15f,.36f,.40f,.9f));}
        void Bar(Rect r,float value,Color color){Box(r,new Color(.15f,.23f,.26f,.9f));Box(new Rect(r.x,r.y,r.width*Mathf.Clamp01(value),r.height),color);}
        void Box(Rect r,Color color){Color old=GUI.color;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        void Label(float x,float y,float w,float h,string value,int size,Color color,TextAnchor align=TextAnchor.UpperLeft)
        {
            text.fontSize=size;text.normal.textColor=color;text.alignment=align;GUI.Label(new Rect(x,y,w,h),value,text);
        }
        bool Button(Rect r,string value,Color color,bool enabled=true)
        {
            Color old=GUI.backgroundColor;GUI.backgroundColor=enabled?color:new Color(.25f,.31f,.33f);GUI.enabled=enabled;
            bool clicked=GUI.Button(r,value,button);GUI.enabled=true;GUI.backgroundColor=old;return clicked;
        }
    }
}

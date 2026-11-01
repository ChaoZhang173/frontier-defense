using System;
using UnityEngine;

namespace Frontier
{
    public static class CentralDefenseChecks
    {
        public static void Run(GameWorld w,Action<bool,string> check)
        {
            w.BeginCampaign();w.Credits=10000;
            FortificationChecks.PlacePlayer(w,new Vector3(0,0,18));
            int credits=w.Credits;
            check(!w.TryOpenCommandTerminal()&&!w.Buy(0)&&!w.Upgrades.Buy(BaseUpgrade.BunkerArmor)&&!w.Construction.Build(0)&&!w.Squad.Hire()&&w.Credits==credits,
                "All purchases require visiting the command center; the front line cannot buy remotely");
            w.Phase=Phase.Intermission;w.IntermissionLeft=45;
            check(!w.TryOpenCommandTerminal()&&!w.Buy(0)&&!w.Upgrades.Buy(BaseUpgrade.BunkerArmor)&&!w.Construction.Build(0)&&!w.Squad.Hire()&&w.Credits==credits,
                "Intermission does not grant remote access to upgrades, construction or hiring");
            FortificationChecks.PlacePlayer(w,new Vector3(0,0,3));
            check(w.Interact()&&w.CommandTerminalOpen&&w.Paused&&w.CanManage,"E opens the nearby command terminal during intermission");
            float time=w.SimTime,left=w.IntermissionLeft;w.Tick(10);
            check(w.SimTime==time&&w.IntermissionLeft==left,"Opening the terminal freezes the preparation countdown and world simulation");
            check(w.Buy(0)&&w.WeaponLevel==1&&w.Credits==credits-90,"Personal weapon upgrades work in the command terminal");
            check(w.Upgrades.Buy(BaseUpgrade.BunkerArmor)&&w.Upgrades.ArmorLevel==1,"Building upgrades work in the same command terminal");
            check(w.Construction.Build(1)&&w.Construction.Sites[1].State==SiteState.Constructing,"Construction is available in the command terminal");
            check(w.Squad.Hire()&&w.Squad.Hire()&&w.Squad.Count==2,"Mercenary contracts are available in the command terminal");
            float construction=w.Construction.Sites[1].SecondsLeft;w.Tick(30);
            check(w.Construction.Sites[1].SecondsLeft==construction,"Terminal construction waits for the battlefield to resume");
            check(w.Interact()&&!w.CommandTerminalOpen&&!w.Paused,"E closes the terminal and resumes free movement");
            w.SetPaused(true);credits=w.Credits;
            check(!w.CanManage&&!w.Buy(1)&&!w.Upgrades.Buy(BaseUpgrade.BunkerFirepower)&&w.Credits==credits,"A general pause does not grant terminal purchasing rights");
            w.SetPaused(false);
            for(int i=0;i<445;i++)w.Tick(.05f);
            check(w.Construction.ReserveOperational&&w.IntermissionLeft<23&&w.IntermissionLeft>22,"Preparation time passes while the reinforcement barracks completes normally");
            FortificationChecks.PlacePlayer(w,new Vector3(0,0,3));w.Interact();credits=w.Credits;
            check(w.Tech.Research(Research.InfantryArmor)&&w.Credits==credits-240,"Research uses the command terminal after its prerequisite finishes");
            w.Interact();
            check(SquadChecks.FillSquad(w),"Four real squad members can be assembled for central defense");
            w.SetPaused(false);w.Phase=Phase.Intermission;w.IntermissionLeft=1000;
            FortificationChecks.PlacePlayer(w,new Vector3(0,0,23));w.Player.Yaw=0;
            check(w.Squad.GiveOrder(SquadOrder.Hold),"G can deploy a squad during free intermission");
            bool reached=false,clear=true;
            for(int i=0;i<900;i++)
            {
                w.SimTime+=.05f;Physics.SyncTransforms();w.Squad.Tick(.05f);
                reached=true;
                foreach(var m in w.Squad.Members)
                {
                    clear&=!Physics.CheckCapsule(m.transform.position+Vector3.up*.65f,m.transform.position+Vector3.up*1.5f,.35f,Combat.WorldMask,QueryTriggerInteraction.Ignore);
                    reached&=Vector3.Distance(m.transform.position,CentralDefense.SquadPost(m.Slot))<.85f;
                }
                if(reached)break;
            }
            check(reached&&clear,"The full squad walks to four central firing positions without crossing walls");
            bool corridor=true;foreach(var m in w.Squad.Members)corridor&=Mathf.Abs(m.transform.position.x)>2.1f;
            check(corridor,"Central squad positions leave a clear lane for the tank");
            // Aim from the wall positions into both approach ramps using ordinary rifle traces.
            bool squadShots=true;
            for(int lane=0;lane<2;lane++)
            {
                var enemy=w.SpawnEnemy(Role.Spitter,lane,new Vector3(lane==0?-13.5f:13.5f,GameWorld.GroundHeight(lane==0?-13.5f:13.5f,36),36));
                enemy.WeakRadius=0;enemy.Current=enemy.Maximum=5000;
                var member=w.Squad.Members[lane];member.Self.Rig.AimAt(enemy.Aim);Physics.SyncTransforms();float hp=enemy.Current;
                Vector3 muzzle=member.Self.Rig.Muzzle.position;
                Combat.Rifle(member.Self,muzzle,(enemy.Aim-muzzle).normalized,30,false);
                if(enemy.Current>=hp&&Combat.Trace(muzzle,(enemy.Aim-muzzle).normalized,50,member.Self,out var blocked,true))Debug.Log("CENTRAL_SHOT slot="+member.Slot+" muzzle="+muzzle+" aim="+enemy.Aim+" collider="+blocked.collider.name+" hit="+blocked.point);
                squadShots&=enemy.Current<hp;
                enemy.Damage(10000,null);
            }
            check(squadShots,"Squad rifle traces pass over the lowered wall into both exterior approaches");
            // Procedural art can be created during this simulation frame. Destroy schedules
            // removal for frame end, so its primitive colliders must already be disabled.
            bool artNonSolid=true;
            foreach(var member in w.Squad.Members)foreach(var collider in member.Self.Rig.GetComponentsInChildren<Collider>())
                if(collider.gameObject!=member.Self.gameObject&&collider.enabled&&!collider.isTrigger)
                {artNonSolid=false;Debug.Log("CENTRAL_TRANSIENT_COLLIDER name="+collider.name+" position="+collider.transform.position+" layer="+collider.gameObject.layer);}
            check(artNonSolid,"Newly created squad artwork has no active solid primitive colliders before deferred destruction runs");
            FortificationChecks.PlacePlayer(w,new Vector3(-6,0,25));
            Health target=w.SpawnEnemy(Role.Spitter,0,new Vector3(-13.5f,GameWorld.GroundHeight(-13.5f,36),36));target.Current=target.Maximum=5000;target.WeakRadius=0;
            w.Player.Bot=true;w.Player.BotDestination=w.Player.transform.position;w.Player.Tick(.3f);w.Player.BotDestination=null;w.Player.AimPoint=target.Aim;w.Player.Self.Rig.AimAt(target.Aim);Physics.SyncTransforms();float targetHp=target.Current;
            bool fired=w.Player.Fire();if(target.Current>=targetHp&&Combat.Trace(w.Player.Self.Rig.Muzzle.position,(target.Aim-w.Player.Self.Rig.Muzzle.position).normalized,50,w.Player.Self,out var blockedPlayer,true))Debug.Log("CENTRAL_PLAYER fired="+fired+" muzzle="+w.Player.Self.Rig.Muzzle.position+" aim="+target.Aim+" collider="+blockedPlayer.collider.name+" hit="+blockedPlayer.point);
            check(fired&&target.Current<targetHp,"The player's actual rifle fires over the wall during intermission");w.Player.Bot=false;
            target.Damage(10000,null);
            var controller=w.Player.GetComponent<CharacterController>();
            for(int i=0;i<50;i++)w.Player.MoveOnFoot(new Vector3(0,-.15f,.15f));
            if(w.Player.transform.position.z>=25.7f)Debug.Log("CENTRAL_BARRIER final="+w.Player.transform.position+" stepOffset="+controller.stepOffset+" grounded="+controller.isGrounded);
            check(w.Player.transform.position.z<25.7f,"The low wall still physically stops a player from walking off the central cliff");
            CheckBunkerFireLines(w,check);
            CheckBunkerVisibleTarget(w,check);
            // Finish the real first-wave queue and confirm that the next preparation phase is non-modal.
            w.Phase=Phase.Assault;w.Wave=1;w.Assault.StartWave(1);w.SpawnedThisWave=0;
            for(int i=0;i<1000&&w.Phase==Phase.Assault;i++)
            {
                w.Assault.Tick(.1f);
                foreach(var h in w.Entities.ToArray())if(h!=null&&h.Alive&&h.Faction==Faction.Invaders&&!h.IsBuilding)h.Damage(10000,w.Player.Self);
                w.Tick(.001f);
            }
            check(w.Phase==Phase.Intermission&&!w.Paused&&!w.CommandTerminalOpen&&w.IntermissionLeft==45,"Clearing a real wave starts free preparation without opening a modal upgrade screen");
            check(w.Squad.Order==SquadOrder.Hold&&CentralDefense.Contains(w.Squad.HoldPoint),"Wave completion preserves the squad's central defense order");
            w.IntermissionLeft=.05f;w.Tick(.06f);
            check(w.Phase==Phase.Assault&&w.Wave==2&&!w.CommandTerminalOpen&&!w.Paused,"The next wave starts automatically when free preparation ends");
            check(GameWorld.KillReward(Role.Runner)==10&&GameWorld.KillReward(Role.Spitter)==18&&GameWorld.KillReward(Role.Colossus)==48&&GameWorld.WaveReward(1)==175,
                "The release exposes the increased 10/18/48 kill income and 175 first-wave bonus");
        }

        static void CheckBunkerFireLines(GameWorld world,Action<bool,string> check)
        {
            foreach(var bunker in world.Bunkers)
            {
                if(bunker==null||!bunker.Alive)continue;
                float side=Mathf.Sign(bunker.transform.position.x);
                // The nearby central depression at x=+/-3,z=36 is naturally hidden by
                // the ramp's side face from some occupied ports. Keep that terrain solid;
                // x=+/-6,z=40 is above its sight horizon and within every port's 25m range.
                Vector2[] lanes={new Vector2(side*13.5f,36),new Vector2(side*6,40),new Vector2(side*20.5f,42)};
                string[] names={"ramp","central canyon","front canyon"};
                for(int i=0;i<lanes.Length;i++)
                {
                    Vector2 lane=lanes[i];
                    Health enemy=world.SpawnEnemy(Role.Runner,side<0?0:1,new Vector3(lane.x,GroundSurface.Height(lane.x,lane.y),lane.y));
                    enemy.Current=enemy.Maximum=5000;enemy.WeakRadius=0;Physics.SyncTransforms();
                    float before=enemy.Current;
                    // Tick the actual automatic weapon, including acquisition, aim and muzzle trace.
                    for(int step=0;step<4;step++)bunker.Brain.Tick(.25f);
                    bool cannon=enemy.Current<before;
                    if(!cannon&&Combat.Trace(bunker.Rig.Muzzle.position,(enemy.Aim-bunker.Rig.Muzzle.position).normalized,40,bunker,out var hit))
                        Debug.Log("BUNKER_FRONT_LINE side="+side+" target="+names[i]+" muzzle="+bunker.Rig.Muzzle.position+" collider="+hit.collider.name+" hit="+hit.point);
                    check(cannon,(side<0?"West":"East")+" bunker automatic cannon fires real hits over the lowered front wall toward the "+names[i]);
                    bool ports=true;int occupied=0;
                    foreach(var guard in world.Entities)
                    {
                        if(guard==null||!guard.Alive||!world.Fort.IsGuardInside(guard)||world.Fort.GuardBunker(guard)!=bunker)continue;
                        occupied++;Vector3 muzzle=world.Fort.GuardMuzzle(guard);before=enemy.Current;
                        bool visible=world.Fort.CanSeeFromPort(guard,muzzle,enemy,25);
                        if(visible)Combat.Rifle(guard,muzzle,(enemy.Aim-muzzle).normalized,10,false);
                        ports&=visible&&enemy.Current<before;
                        if(!visible&&Combat.Trace(muzzle,(enemy.Aim-muzzle).normalized,40,guard,out var blocked))
                            Debug.Log("BUNKER_PORT_LINE side="+side+" target="+names[i]+" muzzle="+muzzle+" collider="+blocked.collider.name+" hit="+blocked.point);
                    }
                    check(occupied>0&&ports,(side<0?"West":"East")+" occupied bunker firing ports have clear real rifle traces toward the "+names[i]);
                    enemy.Damage(10000,null);
                }
            }
        }

        static void CheckBunkerVisibleTarget(GameWorld world,Action<bool,string> check)
        {
            Health bunker=null;
            foreach(var candidate in world.Bunkers)
                if(candidate!=null&&candidate.Alive&&candidate.transform.position.x>0){bunker=candidate;break;}
            if(bunker==null){check(false,"The east bunker is available for the occluded-target acquisition check");return;}
            // The short, steep trace toward the nearer enemy still meets the low front
            // wall. The farther enemy is visible down the open ramp. Both are in range.
            var blocked=world.SpawnEnemy(Role.Runner,1,new Vector3(20.5f,GroundSurface.Height(20.5f,32),32));
            var visible=world.SpawnEnemy(Role.Runner,1,new Vector3(13.5f,GroundSurface.Height(13.5f,40),40));
            blocked.Current=blocked.Maximum=visible.Current=visible.Maximum=5000;
            blocked.WeakRadius=visible.WeakRadius=0;Physics.SyncTransforms();
            bool fixture=Vector3.Distance(bunker.Aim,blocked.Aim)<Vector3.Distance(bunker.Aim,visible.Aim)
                &&!Combat.Visible(bunker,blocked)&&Combat.Visible(bunker,visible);
            if(!fixture)Debug.Log("BUNKER_TARGET_FIXTURE blocked="+Combat.Visible(bunker,blocked)+" visible="+Combat.Visible(bunker,visible)+" origin="+bunker.Aim+" near="+blocked.Aim+" far="+visible.Aim);
            check(fixture,"A nearer enemy is physically occluded while a farther enemy is visible within bunker range");
            float before=visible.Current,blockedBefore=blocked.Current;
            for(int step=0;step<8;step++)bunker.Brain.Tick(.25f);
            check(bunker.Brain.Target==visible&&visible.Current<before&&blocked.Current==blockedBefore,
                "The automatic bunker cannon acquires and damages the visible enemy despite a nearer occluded enemy");
            blocked.Damage(10000,null);visible.Damage(10000,null);
        }
    }
}

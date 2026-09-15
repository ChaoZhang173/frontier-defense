using System;
using UnityEngine;

namespace Frontier
{
    public static class ConstructionChecks
    {
        public static void Run(GameWorld w,Action<bool,string> check)
        {
            CheckRestart(w,check);w.BeginCampaign();
            CheckConstruction(w,check);
            CheckProductionAndTechnology(w,check);
            CheckIndependentBunkers(w,check);
        }

        public static void CheckRestart(GameWorld w,Action<bool,string> check)
        {
            check(w.Construction.Sites[0].State==SiteState.Empty&&w.Construction.Sites[1].State==SiteState.Empty&&
                w.Construction.Sites[2].State==SiteState.Ready&&w.Construction.Sites[3].State==SiteState.Ready&&
                w.Construction.Sites[4].State==SiteState.Empty,
                "A fresh mission restores the three empty sites and the two original completed buildings");
            check(w.Bunkers.Count==1&&w.BarracksBuildings.Count==1&&w.MarineCapacity==8&&w.PeakMarineCapacity==8,
                "Restart removes expanded buildings and restores the eight-defender capacity");
            check(!w.Tech.IsComplete(Research.InfantryArmor)&&!w.Tech.IsComplete(Research.ArmorPiercing)&&w.Tech.ActiveResearch==-1,
                "Restart clears completed technologies and the active research queue");
            check(w.Fort.Occupants==0&&w.Fort.PlayerBunker==null&&w.Fort.SquadBunker==null,
                "Restart clears both player and squad bunker bindings");
            FortificationChecks.CheckRestart(w,check);
        }

        static void CheckConstruction(GameWorld w,Action<bool,string> check)
        {
            var construction=w.Construction;
            check(construction.Cost(0)==320&&construction.Cost(1)==400&&construction.Cost(2)==400&&construction.Cost(3)==320&&
                construction.Sites[0].Duration==18&&construction.Sites[1].Duration==22,
                "Construction sites advertise the fixed 320/400 supply costs and 18/22 second durations");
            int credits=w.Credits;
            check(!construction.CanBuild(0)&&!construction.Build(0)&&w.Credits==credits,"An assault without the command terminal cannot issue construction orders");
            PrepareIntermission(w);w.Credits=10000;
            check(!construction.CanBuild(-1)&&!construction.Build(-1)&&!construction.CanBuild(5)&&!construction.Build(5),"Invalid construction site identifiers are rejected");
            check(!construction.Build(0)&&w.Credits==10000,"A wave break does not permit construction away from the command terminal");
            CheckCommand.Enter(w);
            check(!construction.Build(2)&&!construction.Build(3)&&w.Credits==10000,"Occupied original building sites cannot be purchased again");
            credits=w.Credits;
            check(!w.Tech.CanResearch(Research.InfantryArmor)&&!w.Tech.Research(Research.InfantryArmor)&&w.Credits==credits,
                "Research requires a completed living reinforcement barracks");
            var site=construction.Sites[0];Vector3 originalPlayer=w.Player.transform.position;
            CheckCommand.Leave(w);
            FortificationChecks.PlacePlayer(w,site.Position);
            check(!construction.SpaceClear(0)&&!construction.CanBuild(0)&&!construction.Build(0)&&w.Credits==credits,"A player standing in the construction footprint blocks the site and cannot remotely purchase it");
            FortificationChecks.PlacePlayer(w,originalPlayer);CheckCommand.Enter(w);
            Health defender=w.Entities.Find(h=>h!=null&&h.Alive&&h.Role==Role.Marine);Vector3 originalDefender=defender.transform.position;
            defender.transform.position=site.Position;Physics.SyncTransforms();
            check(construction.CanManage&&!construction.SpaceClear(0)&&!construction.CanBuild(0)&&!construction.Build(0)&&w.Credits==credits,"A defender standing in the construction footprint blocks a purchase at the command terminal");
            defender.transform.position=originalDefender;Physics.SyncTransforms();
            CheckCommand.Leave(w);w.SetPaused(true);
            check(!construction.Build(0)&&w.Credits==credits,"A general pause cannot issue construction orders");w.SetPaused(false);
            CheckCommand.Enter(w);w.Credits=319;
            check(!construction.CanBuild(0)&&!construction.Build(0)&&w.Credits==319&&site.State==SiteState.Empty,
                "Insufficient building supplies cannot start construction or spend credits");
            w.Credits=10000;check(SquadChecks.FillSquad(w),"Construction scenarios use two contracted soldiers and two genuinely recruited guards");
            CheckCommand.Leave(w);w.Phase=Phase.Assault;
            // Prime a real route through a never-built site before adding its collision volume.
            FortificationChecks.PlacePlayer(w,site.Position+Vector3.forward*14);
            foreach(var member in w.Squad.Members)member.transform.position=site.Position+new Vector3(member.Slot-1,0,-9);
            Physics.SyncTransforms();w.Squad.GiveOrder(SquadOrder.Hold);StepSquad(w,.1f);
            var initialNavigation=w.Squad.Navigation;
            check(initialNavigation.Clear(site.Position+Vector3.back*9,site.Position+Vector3.forward*14),
                "Before first construction, the selected squad route can cross the empty footprint");
            FortificationChecks.PlacePlayer(w,new Vector3(0,0,3));
            check(w.TryOpenCommandTerminal()&&w.Paused,"Construction can be ordered at the real paused command terminal");
            credits=w.Credits;bool started=construction.Build(0);Health foundation=site.Structure;
            check(started&&w.Credits==credits-320&&site.State==SiteState.Constructing&&foundation!=null&&foundation.Alive&&foundation.Role==Role.Construction&&foundation.IsBuilding,
                "A command-terminal purchase spends 320 and creates an attackable construction foundation");
            check(foundation.Brain==null&&!w.Bunkers.Contains(foundation)&&!w.BarracksBuildings.Contains(foundation)&&w.MarineCapacity==8,
                "An unfinished structure neither attacks nor adds production capacity");
            check(w.Squad.Navigation!=initialNavigation&&!w.Squad.Navigation.Walkable(site.Position),
                "First construction replaces the empty-site navigation and blocks the previously traversable footprint");
            credits=w.Credits;float remaining=site.SecondsLeft,now=w.SimTime;w.Tick(30);
            check(site.SecondsLeft==remaining&&w.SimTime==now,"Construction time freezes while the command terminal pauses the battlefield");
            check(!construction.Build(0)&&w.Credits==credits,"A repeated order cannot charge twice for the same active construction site");
            w.SetPaused(false);PrepareIntermission(w);
            bool clear=true;
            for(int step=0;step<240;step++)
            {
                StepSquad(w,.05f);
                foreach(var member in w.Squad.Members)
                    clear&=w.Squad.Navigation.Walkable(member.transform.position)&&
                        !Physics.CheckCapsule(member.transform.position+Vector3.up*.65f,member.transform.position+Vector3.up*1.5f,.35f,Combat.WorldMask,QueryTriggerInteraction.Ignore);
            }
            check(clear,"Squad members abandon a pre-construction crossing route and remain outside the new foundation");
            float hp=foundation.Current;foundation.Damage(40,w.Player.Self);
            check(foundation.Current==hp,"Friendly fire cannot damage a construction foundation");
            Health attacker=w.SpawnEnemy(Role.Runner,0,site.Position+Vector3.left*4.2f);Physics.SyncTransforms();
            Health acquiredTarget=w.FindTarget(attacker,15);bool acquired=acquiredTarget==foundation;attacker.Brain.Tick(.1f);
            if(!acquired||foundation.Current>=hp)
                Debug.Log("FOUNDATION_TARGET attacker="+attacker.transform.position+" selected="+(acquiredTarget!=null?acquiredTarget.Role.ToString():"none")+
                    " targetPosition="+(acquiredTarget!=null?acquiredTarget.transform.position:Vector3.zero)+" hp="+foundation.Current+" before="+hp);
            check(acquired&&foundation.Current<hp,"An actual enemy selects the foundation as a building target and deals attack damage to it");
            foundation.Damage(10000,null);
            check(site.State==SiteState.Destroyed&&!foundation.Alive&&w.Credits==credits,"Destroyed construction loses the investment and leaves a rebuildable site");
            attacker.Damage(10000,null);

            CheckCommand.Enter(w);w.Upgrades.Buy(BaseUpgrade.BunkerArmor);w.Upgrades.Buy(BaseUpgrade.BunkerFirepower);
            var navigation=w.Squad.Navigation;PrepareIntermission(w);credits=w.Credits;
            CheckCommand.Enter(w);
            LogBlockedSite(w,0);check(construction.Build(0)&&w.Credits==credits-320&&site.Structure!=foundation,
                "Rebuilding a lost foundation charges a new investment and creates a distinct health object");
            check(w.Squad.Navigation!=navigation&&!w.Squad.Navigation.Walkable(site.Position),
                "Starting construction rebuilds navigation and marks the footprint as blocked");
            foundation=site.Structure;navigation=w.Squad.Navigation;StepWorld(w,17.7f);
            check(site.State==SiteState.Constructing&&site.Structure==foundation,"A bunker remains an unfinished foundation before its full 18-second build time");
            StepWorld(w,.5f);Health west=site.Structure;
            check(site.State==SiteState.Ready&&west!=foundation&&west.Alive&&west.Role==Role.Bunker&&w.Bunkers.Contains(west)&&w.Bunkers.Count==2,
                "Finishing construction installs a new functional bunker in the world's bunker roster");
            check(w.Squad.Navigation!=navigation&&!w.Squad.Navigation.Walkable(site.Position),"Completion refreshes navigation while retaining the solid building footprint");
            check(west.Maximum==1600&&w.Bunker.Maximum==1600&&w.BunkerDamage==20,
                "A newly completed bunker inherits the same global armor and firepower upgrades as the original bunker");
            west.Damage(500,null);hp=w.Bunker.Current;credits=w.Credits;CheckCommand.Enter(w);
            check(w.Upgrades.Buy(BaseUpgrade.RepairBunker,west)&&w.Credits==credits-120&&west.Current==west.Maximum&&w.Bunker.Current==hp,
                "A targeted bunker repair spends 120 and repairs only the selected building");
            west.Damage(80,null);w.Bunker.Damage(120,null);float westHp=west.Current,eastHp=w.Bunker.Current;
            check(w.Upgrades.Buy(BaseUpgrade.BunkerArmor)&&west.Maximum==2000&&w.Bunker.Maximum==2000&&west.Current==westHp+400&&w.Bunker.Current==eastHp+400,
                "A later global armor upgrade increases both existing bunkers' durability and heals each one by 400");
            west.Heal(1000);w.Bunker.Heal(1000);
            credits=w.Credits;
            check(!construction.Build(0)&&w.Credits==credits,"A completed expansion site cannot be built over or charged again");
        }

        static void CheckProductionAndTechnology(GameWorld w,Action<bool,string> check)
        {
            var construction=w.Construction;var site=construction.Sites[1];PrepareIntermission(w);CheckCommand.Enter(w);
            int credits=w.Credits;
            check(construction.Build(1)&&w.Credits==credits-400,"The reinforcement barracks starts for its advertised 400 supplies");
            check(!w.Tech.Research(Research.InfantryArmor)&&w.MarineCapacity==8,"An unfinished reinforcement barracks unlocks neither research nor additional garrison slots");
            StepWorld(w,21.7f);check(site.State==SiteState.Constructing,"The reinforcement barracks does not finish before 22 seconds");
            StepWorld(w,.5f);
            check(site.State==SiteState.Ready&&site.Structure.Role==Role.Barracks&&w.BarracksBuildings.Count==2&&w.MarineCapacity==12&&w.PeakMarineCapacity==12,
                "The completed reinforcement barracks joins production and raises the garrison capacity to 12");
            int count=w.MarinesAlive;w.ProductionLeft=12;construction.ProductionLeft=12;StepWorld(w,11.75f);
            check(w.MarinesAlive==count,"Neither barracks produces before its independent production timer elapses");
            StepWorld(w,.4f);
            check(w.MarinesAlive==Mathf.Min(12,count+2),"The two independent barracks each produce a defender when their timers elapse");
            for(int i=0;i<20;i++)w.SpawnMarine();
            check(w.MarinesAlive==12&&w.PeakMarines==12&&w.SpawnMarine()==null,"Expanded production fills 12 slots and rejects every additional defender");
            CheckReserveDeployment(w,check);

            var tech=w.Tech;
            check(tech.Cost(Research.InfantryArmor)==240&&tech.Cost(Research.ArmorPiercing)==280,"Research advertises the 240/280 supply costs");
            CheckCommand.Enter(w);w.Credits=239;credits=w.Credits;
            check(!tech.CanResearch(Research.InfantryArmor)&&!tech.Research(Research.InfantryArmor)&&w.Credits==credits&&tech.ActiveResearch==-1,
                "Insufficient research supplies leave the queue idle and do not charge");
            CheckCommand.Leave(w);w.Credits=10000;credits=w.Credits;w.Phase=Phase.Assault;
            check(!tech.Research(Research.InfantryArmor)&&w.Credits==credits,"Research cannot be ordered during an assault outside the command terminal");
            PrepareIntermission(w);w.SetPaused(true);
            check(!tech.Research(Research.InfantryArmor)&&w.Credits==credits,"A general pause cannot start research");w.SetPaused(false);
            Health marine=w.Entities.Find(h=>h!=null&&h.Alive&&h.Role==Role.Marine);
            Health[] infantry={w.Player.Self,marine,w.Squad.Members[0].Self};
            foreach(Health unit in infantry)
            {
                if(unit.Role==Role.Marine)w.Fort.ReleaseGuard(unit);
                unit.Heal(unit.Maximum);float hp=unit.Current;unit.Damage(40,null);
                check(Mathf.Abs(hp-unit.Current-40)<.01f,"Before research, "+unit.Role+" takes the full infantry damage amount");unit.Heal(unit.Maximum);
            }
            CheckCommand.Enter(w);
            check(tech.Research(Research.InfantryArmor)&&w.Credits==credits-240&&tech.ActiveResearch==(int)Research.InfantryArmor&&tech.Duration==16,
                "Infantry armor research spends 240 and occupies the single 16-second research queue");
            credits=w.Credits;
            check(!tech.CanResearch(Research.ArmorPiercing)&&!tech.Research(Research.ArmorPiercing)&&!tech.Research(Research.InfantryArmor)&&w.Credits==credits,
                "A busy research queue rejects both competing and repeated orders without charging");
            StepWorld(w,5);float remaining=tech.SecondsLeft;w.SetPaused(true);w.Tick(20);
            check(tech.SecondsLeft==remaining,"Paused simulation freezes research progress");w.SetPaused(false);
            Health oldBarracks=site.Structure;int defenders=w.MarinesAlive;oldBarracks.Damage(10000,null);
            check(w.MarineCapacity==8&&w.PeakMarineCapacity==12&&w.MarinesAlive==defenders,
                "Losing the extra barracks removes additional capacity while retaining existing defenders and the recorded peak");
            remaining=tech.SecondsLeft;StepWorld(w,3);
            check(tech.SecondsLeft==remaining&&!tech.IsComplete(Research.InfantryArmor)&&tech.ActiveResearch==(int)Research.InfantryArmor,
                "Destroying the prerequisite barracks suspends research without discarding the queued work");
            w.Fort.ReleaseGuard(marine);marine.Damage(10000,null);defenders=w.MarinesAlive;StepWorld(w,13);
            check(w.MarinesAlive==defenders&&w.MarinesAlive>8,"With more than eight survivors, the original barracks cannot replace losses while the extra barracks is destroyed");
            CheckCommand.Enter(w);credits=w.Credits;
            check(!w.Upgrades.Buy(BaseUpgrade.RepairBarracks,oldBarracks)&&w.Credits==credits,"A targeted repair cannot revive a destroyed reinforcement barracks");
            check(construction.Build(1)&&w.Credits==credits-400,"The destroyed reinforcement barracks can be rebuilt for a new 400-supply investment");
            StepWorld(w,21.7f);
            check(tech.SecondsLeft==remaining&&!tech.IsComplete(Research.InfantryArmor),"Research remains suspended throughout prerequisite reconstruction");
            StepWorld(w,.5f);
            check(site.State==SiteState.Ready&&site.Structure!=oldBarracks&&w.MarineCapacity==12&&tech.SecondsLeft<remaining,
                "A new barracks health object restores capacity and automatically resumes the retained research queue");
            float left=tech.SecondsLeft;StepWorld(w,Mathf.Max(0,left-.3f));
            check(!tech.IsComplete(Research.InfantryArmor),"Infantry armor does not complete before all remaining research time elapses");
            StepWorld(w,.5f);
            check(tech.IsComplete(Research.InfantryArmor)&&tech.ActiveResearch==-1,"Finishing infantry armor marks it complete and releases the research queue");
            foreach(Health unit in infantry)
            {
                if(unit==null||!unit.Alive)continue;
                if(unit.Role==Role.Marine)w.Fort.ReleaseGuard(unit);
                unit.Heal(unit.Maximum);float hp=unit.Current;unit.Damage(40,null);
                check(Mathf.Abs(hp-unit.Current-34)<.01f,"Completed infantry armor reduces actual "+unit.Role+" damage by 15 percent");unit.Heal(unit.Maximum);
            }
            marine=w.Entities.Find(h=>h!=null&&h.Alive&&h.Role==Role.Marine);w.Fort.ReleaseGuard(marine);float marineHp=marine.Current;marine.Damage(40,null);
            check(Mathf.Abs(marineHp-marine.Current-34)<.01f,"Surviving and newly produced marines also receive the completed infantry armor bonus");marine.Heal(1000);
            float buildingHp=w.Bunker.Current;w.Bunker.Damage(40,null);
            check(Mathf.Abs(buildingHp-w.Bunker.Current-40)<.01f,"Infantry armor does not incorrectly reduce damage to buildings");w.Bunker.Heal(1000);
            CheckCommand.Enter(w);credits=w.Credits;
            check(!tech.CanResearch(Research.InfantryArmor)&&!tech.Research(Research.InfantryArmor)&&w.Credits==credits,"A completed technology cannot be researched or paid for twice");
            Health heavy=w.SpawnEnemy(Role.Colossus,0,new Vector3(-8,0,20));heavy.Current=heavy.Maximum=5000;heavy.WeakRadius=0;
            float hpBefore=heavy.Current;heavy.Damage(100,w.Player.Self);
            check(Mathf.Abs(hpBefore-heavy.Current-72)<.01f,"Before armor-piercing research, real player damage uses the original 0.72 colossus armor multiplier");
            heavy.Damage(10000,null);
            CheckCommand.Leave(w);w.Phase=Phase.Assault;FortificationChecks.PlacePlayer(w,new Vector3(0,0,3));
            check(w.TryOpenCommandTerminal(),"The player can open the command terminal to issue a research order");credits=w.Credits;
            check(tech.Research(Research.ArmorPiercing)&&w.Credits==credits-280&&tech.Duration==20,"Armor-piercing research can start at the command terminal for 280 supplies and a 20-second timer");
            left=tech.SecondsLeft;w.Tick(30);check(tech.SecondsLeft==left,"Research ordered at the terminal waits until the paused battlefield resumes");PrepareIntermission(w);
            StepWorld(w,19.7f);check(!tech.IsComplete(Research.ArmorPiercing),"Armor-piercing research respects its full 20-second duration");StepWorld(w,.5f);
            check(tech.IsComplete(Research.ArmorPiercing)&&tech.ActiveResearch==-1,"Completing the second technology leaves both upgrades active and the queue idle");
            heavy=w.SpawnEnemy(Role.Colossus,0,new Vector3(-8,0,20));heavy.Current=heavy.Maximum=5000;heavy.WeakRadius=0;
            Health[] shooters={w.Player.Self,marine,w.Squad.Members[0].Self,w.Bunker,construction.Sites[0].Structure};
            foreach(Health source in shooters)
            {
                hpBefore=heavy.Current;heavy.Damage(100,source);
                check(Mathf.Abs(hpBefore-heavy.Current-90)<.01f,"Armor piercing changes real "+source.Role+" damage against colossi to the 0.90 multiplier");
            }
            hpBefore=heavy.Current;heavy.Damage(100,null);
            check(Mathf.Abs(hpBefore-heavy.Current-72)<.01f,"Damage without a friendly attacker retains the original colossus armor multiplier");heavy.Damage(10000,null);
        }

        static void CheckIndependentBunkers(GameWorld w,Action<bool,string> check)
        {
            CheckCommand.Leave(w);var fort=w.Fort;Health east=w.Bunker,west=w.Construction.Sites[0].Structure;
            ReleaseGuards(w);
            w.Phase=Phase.Assault;FortificationChecks.PlacePlayer(w,fort.EntranceFor(west));
            check(fort.OrderSquadInside()&&fort.SquadBunker==west,"The west entrance binds the squad order to the west bunker");
            FortificationChecks.PlacePlayer(w,fort.EntranceFor(east));check(fort.TryTogglePlayer(),"The player independently enters the original east bunker");StepSquad(w,20);
            check(fort.PlayerBunker==east&&fort.SquadBunker==west&&fort.SquadInsideFor(west)==4&&fort.OccupantsFor(west)==4&&fort.OccupantsFor(east)==1,
                "The player's relocation never retargets the squad: east holds the player and west holds four squad members");
            Health eastTarget=w.SpawnEnemy(Role.Spitter,1,east.transform.position+new Vector3(0,0,14));eastTarget.Current=eastTarget.Maximum=5000;eastTarget.WeakRadius=0;
            Health westTarget=w.SpawnEnemy(Role.Spitter,0,west.transform.position+new Vector3(0,0,14));westTarget.Current=westTarget.Maximum=5000;westTarget.WeakRadius=0;
            float eastHp=eastTarget.Current,westHp=westTarget.Current;w.Player.Tick(.3f);w.Player.AimPoint=eastTarget.Aim;Physics.SyncTransforms();
            bool fired=w.Player.Fire();StepSquad(w,1.5f);
            check(fired&&eastTarget.Current<eastHp&&westTarget.Current<westHp,"The player and squad simultaneously deal actual aperture damage from their separately bound bunkers");
            eastTarget.Damage(10000,null);westTarget.Damage(10000,null);
            west.Damage(10000,null);
            bool released=true;foreach(var member in w.Squad.Members)released&=!member.Self.IsSheltered;
            check(fort.PlayerBunker==east&&fort.PlayerInside&&w.Player.Self.IsSheltered&&fort.OccupantsFor(east)==1&&fort.OccupantsFor(west)==0&&released,
                "Destroying the west bunker evacuates its squad while leaving the east-bunker player protected and stationed");
            PrepareIntermission(w);int beforeRebuild=w.Credits;
            check(!w.Construction.Build(0)&&w.Credits==beforeRebuild&&fort.PlayerBunker==east,
                "The sheltered east-bunker player cannot remotely order a west-bunker reconstruction");
            CheckCommand.Enter(w);LogBlockedSite(w,0);
            check(w.Construction.Build(0),"Returning to the command terminal permits west-bunker reconstruction");
            CheckCommand.Leave(w);FortificationChecks.PlacePlayer(w,fort.EntranceFor(east));fort.TryTogglePlayer();
            StepWorld(w,18.2f);ReleaseGuards(w);
            Health newWest=w.Construction.Sites[0].Structure;
            check(newWest!=west&&newWest.Alive&&fort.OccupantsFor(west)==0&&fort.OccupantsFor(newWest)==0&&newWest.Maximum==2000,
                "A rebuilt west bunker uses a new health identity, inherits upgrades and never inherits obsolete occupants");
            w.Phase=Phase.Assault;fort.ExitPlayer();FortificationChecks.PlacePlayer(w,fort.EntranceFor(newWest));fort.OrderSquadInside();
            FortificationChecks.PlacePlayer(w,fort.EntranceFor(east));fort.TryTogglePlayer();StepSquad(w,20);
            check(fort.SquadBunker==newWest&&fort.SquadInsideFor(newWest)==4,"The squad can bind and occupy the newly rebuilt west bunker");
            east.Damage(10000,null);
            check(!fort.PlayerInside&&!w.Player.Self.IsSheltered&&w.Player.GetComponent<CharacterController>().enabled&&fort.SquadBunker==newWest&&fort.SquadInsideFor(newWest)==4&&fort.OccupantsFor(east)==0,
                "Destroying the east bunker evacuates only its player and preserves all west-bunker squad occupants");
            PrepareIntermission(w);CheckCommand.Enter(w);LogBlockedSite(w,3);check(w.Construction.Build(3),"The destroyed original east-bunker site accepts a reconstruction order at the command terminal");StepWorld(w,18.2f);ReleaseGuards(w);
            Health newEast=w.Construction.Sites[3].Structure;
            check(newEast!=east&&newEast==w.Bunker&&newEast.Alive&&fort.OccupantsFor(east)==0&&fort.SquadInsideFor(newWest)==4,
                "Rebuilding the original bunker replaces the world's original-bunker reference without disturbing the other garrison");
            w.Phase=Phase.Assault;FortificationChecks.PlacePlayer(w,fort.EntranceFor(newEast));
            check(fort.TryTogglePlayer()&&fort.PlayerBunker==newEast&&fort.OccupantsFor(newEast)==1,
                "The player can enter the replacement east bunker without being bound to its destroyed predecessor");
            Health originalBarracks=w.Barracks;originalBarracks.Damage(10000,null);PrepareIntermission(w);int credits=w.Credits;
            check(!w.Construction.Build(2)&&w.Credits==credits&&fort.PlayerBunker==newEast,
                "An east-bunker occupant cannot remotely reconstruct the original barracks during a wave break");
            CheckCommand.Enter(w);
            check(w.Construction.Sites[2].State==SiteState.Destroyed&&w.Construction.Build(2)&&w.Credits==credits-400,
                "The original barracks also has its own rebuildable site and a 400-supply reconstruction cost");
            CheckCommand.Leave(w);FortificationChecks.PlacePlayer(w,fort.EntranceFor(newEast));fort.TryTogglePlayer();
            StepWorld(w,22.2f);
            check(w.Barracks!=originalBarracks&&w.Construction.Sites[2].Structure==w.Barracks&&w.Barracks.Alive&&w.BarracksBuildings.Contains(w.Barracks)&&
                fort.PlayerBunker==newEast&&fort.SquadInsideFor(newWest)==4,
                "Reconstructing the original barracks replaces its world reference and navigation without evicting either bunker garrison");
        }

        static void CheckReserveDeployment(GameWorld w,Action<bool,string> check)
        {
            PrepareIntermission(w);
            ReleaseGuards(w);
            Health reserve=w.Construction.Sites[1].Structure;
            Health[] recruits=new Health[2];Vector3[] spawns=new Vector3[2];
            check(w.EnemiesAlive==0,"The reinforcement deployment route scenario begins without enemies interrupting movement");
            for(int slot=0;slot<2;slot++)
            {
                Health previous=w.Entities.Find(h=>h!=null&&h.Alive&&h.Role==Role.Marine&&h.Slot==slot);
                previous.Damage(10000,null);
                // Let the real reserve production loop claim this vacancy before the original barracks.
                float primaryTimer=w.ProductionLeft;w.ProductionLeft=120;w.Construction.ProductionLeft=.05f;
                StepWorld(w,.05f);w.ProductionLeft=primaryTimer;
                recruits[slot]=w.Entities.Find(h=>h!=null&&h.Alive&&h.Role==Role.Marine&&h.Slot==slot);
                spawns[slot]=reserve.transform.TransformPoint(new Vector3((slot%3-1)*.5f,0,5.2f));
                bool spawned=recruits[slot]!=null&&recruits[slot]!=previous&&
                    Vector3.Distance(recruits[slot].transform.position,spawns[slot])<.25f&&
                    recruits[slot].Brain.Station==GameWorld.DefensePosts[slot];
                check(spawned,"The reserve barracks itself produces a new "+(slot==0?"west":"east")+"-post defender at its physical exit");
            }
            bool[] clear={true,true},arrived={false,false};
            ReleaseGuards(w);
            for(int step=0;step<800;step++)
            {
                StepGuardNavigation(w,.05f);
                for(int slot=0;slot<2;slot++)
                {
                    Health recruit=recruits[slot];if(recruit==null){clear[slot]=false;continue;}
                    clear[slot]&=!Physics.CheckCapsule(recruit.transform.position+Vector3.up*.65f,recruit.transform.position+Vector3.up*1.5f,
                        .35f,Combat.WorldMask,QueryTriggerInteraction.Ignore);
                    if(Vector3.Distance(recruit.transform.position,GameWorld.DefensePosts[slot])<=.8f)arrived[slot]=true;
                }
            }
            for(int slot=0;slot<2;slot++)
            {
                Health recruit=recruits[slot];Vector3 post=GameWorld.DefensePosts[slot];
                bool stationed=recruit!=null&&recruit.Alive&&Vector3.Distance(recruit.transform.position,post)<=.8f&&
                    recruit.Brain.Travelled>=Vector3.Distance(spawns[slot],post)-.8f;
                check(clear[slot],"The reserve defender heading "+(slot==0?"west":"east")+" never crosses the HQ or bunker collision volumes");
                check(arrived[slot]&&stationed,"The reserve defender reaches and remains within 0.8 metres of its "+(slot==0?"west":"east")+" post within 40 seconds");
                if(!clear[slot]||!stationed)
                    Debug.Log("RESERVE_ROUTE slot="+slot+" start="+spawns[slot]+" destination="+post+" final="+(recruit!=null?recruit.transform.position:Vector3.zero)+
                        " travelled="+(recruit!=null?recruit.Brain.Travelled:0)+" clear="+clear[slot]+" arrived="+arrived[slot]);
            }
        }

        public static void PrepareCapture(GameWorld w,string scene)
        {
            w.BeginCampaign();PrepareIntermission(w);w.Credits=2200;
            SquadChecks.FillSquad(w);
            CheckCommand.Enter(w);w.Construction.Build(0);w.Construction.Build(1);
            if(scene=="construction")StepWorld(w,5);
            else
            {
                StepWorld(w,22.2f);CheckCommand.Enter(w);w.Tech.Research(Research.InfantryArmor);
                if(scene=="technology")StepWorld(w,6);
                else
                {
                    StepWorld(w,16.2f);CheckCommand.Enter(w);w.Tech.Research(Research.ArmorPiercing);StepWorld(w,20.2f);
                    for(int i=0;i<12;i++)w.SpawnMarine();
                }
            }
            w.Phase=Phase.Assault;w.WaveBannerLeft=0;FortificationChecks.PlacePlayer(w,new Vector3(0,0,3));
            if(scene=="expanded")
            {
                FortificationChecks.PlacePlayer(w,new Vector3(-20,0,-16));w.Player.Yaw=27;w.Player.Pitch=12;
                w.SpawnEnemy(Role.Runner,0,new Vector3(-13,0,24));w.SpawnEnemy(Role.Spitter,1,new Vector3(14,0,24));
                w.Player.UpdateCamera(1);
            }
            else w.TryOpenCommandTerminal();
        }

        static void LogBlockedSite(GameWorld w,int id)
        {
            if(w.Construction.CanBuild(id))return;
            var site=w.Construction.Sites[id];Vector3 size=ConstructionDirector.Size(site.Role);
            Debug.Log("BLOCKED_BUILD site="+id+" state="+site.State+" manage="+w.Construction.CanManage+" clear="+w.Construction.SpaceClear(id)+" credits="+w.Credits);
            foreach(var h in w.Entities)
            {
                if(h==null||!h.Alive||h.IsSheltered||h.IsBuilding)continue;
                Vector3 d=h.transform.position-site.Position;
                if(Mathf.Abs(d.x)<size.x*.5f+h.Radius+.2f&&Mathf.Abs(d.z)<size.z*.5f+h.Radius+.2f)
                    Debug.Log("BLOCKED_BUILD unit="+h.Role+" slot="+h.Slot+" pos="+h.transform.position+" radius="+h.Radius);
            }
            foreach(var c in Physics.OverlapBox(site.Position+Vector3.up*(size.y*.5f+.1f),new Vector3(size.x*.5f,size.y*.5f-.05f,size.z*.5f),Quaternion.identity,Combat.WorldMask,QueryTriggerInteraction.Ignore))
                if(c.GetComponentInParent<Health>()!=site.Structure)Debug.Log("BLOCKED_BUILD collider="+c.name+" pos="+c.transform.position);
        }
        static void PrepareIntermission(GameWorld w)
        {
            if(w.Paused)w.SetPaused(false);w.Phase=Phase.Intermission;w.IntermissionLeft=1000;
        }
        static void ReleaseGuards(GameWorld w)
        {
            foreach(var h in w.Entities)if(h!=null&&h.Alive&&h.Role==Role.Marine)w.Fort.ReleaseGuard(h);
        }
        static void StepGuardNavigation(GameWorld w,float dt)
        {
            w.SimTime+=dt;Physics.SyncTransforms();
            foreach(var h in w.Entities)if(h!=null&&h.Alive&&h.Role==Role.Marine&&h.Brain!=null)h.Brain.NavigateTo(h.Brain.Station,dt);
            w.Effects.Tick(dt);Physics.SyncTransforms();
        }
        static void StepWorld(GameWorld w,float seconds)
        {
            CheckCommand.Leave(w);
            for(int step=0;step<Mathf.CeilToInt(seconds/.05f);step++){Physics.SyncTransforms();w.Tick(.05f);w.Effects.Tick(.05f);}
        }
        static void StepSquad(GameWorld w,float seconds)
        {
            for(int step=0;step<Mathf.CeilToInt(seconds/.05f);step++)
            {
                w.SimTime+=.05f;Physics.SyncTransforms();w.Fort.Tick(.05f);w.Squad.Tick(.05f);w.Effects.Tick(.05f);
            }
        }
    }
}

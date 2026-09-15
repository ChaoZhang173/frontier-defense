using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Frontier
{
    // Deterministic scenarios exercise the same purchases, navigation and bullets as play.
    public static class FortificationChecks
    {
        public static void Run(GameWorld w,Action<bool,string> check)
        {
            CheckRestart(w,check);
            w.BeginCampaign();w.Phase=Phase.Intermission;w.IntermissionLeft=1000;
            CheckCommand.Enter(w);CheckUpgrades(w,check);
            CheckGarrison(w,check);
        }

        public static void CheckRestart(GameWorld w,Action<bool,string> check)
        {
            check(w.Upgrades.BarracksLevel==0&&w.Upgrades.ArmorLevel==0&&w.Upgrades.FirepowerLevel==0,
                "A fresh mission resets all three building upgrade levels");
            check(w.Bunker.Alive&&w.Bunker.Maximum==1200&&w.Bunker.Current==1200&&w.Barracks.Alive&&w.Barracks.Current==1100,
                "A fresh mission restores destroyed buildings and their original health limits");
            check(w.Fort.Occupants==0&&w.Fort.SquadInside==0&&!w.Fort.PlayerInside&&!w.Player.Self.IsSheltered&&
                w.Player.GetComponent<CharacterController>().enabled&&w.ProductionInterval==12&&w.BunkerDamage==15,
                "A fresh mission clears bunker occupancy, shelter, camera control and building bonuses");
        }

        static void CheckUpgrades(GameWorld w,Action<bool,string> check)
        {
            var upgrades=w.Upgrades;
            BaseUpgrade[] kinds={BaseUpgrade.BarracksProduction,BaseUpgrade.BunkerArmor,BaseUpgrade.BunkerFirepower};
            int[] firstPrices={200,180,220},secondPrices={320,300,360};
            for(int i=0;i<kinds.Length;i++)
            {
                w.Credits=firstPrices[i]-1;int funds=w.Credits;
                check(upgrades.Cost(kinds[i])==firstPrices[i]&&!upgrades.CanBuy(kinds[i])&&!upgrades.Buy(kinds[i])&&w.Credits==funds&&upgrades.Level(kinds[i])==0,
                    kinds[i]+" has its advertised price and rejects insufficient funds without charging");
            }
            w.Credits=10000;int before=w.Credits;
            w.Barracks.Damage(10,null);w.Bunker.Damage(10,null);
            CheckCommand.Leave(w);w.Phase=Phase.Assault;
            bool rejected=true;
            foreach(BaseUpgrade kind in Enum.GetValues(typeof(BaseUpgrade)))rejected&=!upgrades.CanBuy(kind)&&!upgrades.Buy(kind);
            check(rejected&&w.Credits==before&&w.Barracks.Current==w.Barracks.Maximum-10&&w.Bunker.Current==w.Bunker.Maximum-10,
                "Every building purchase requires the terminal during an assault");
            w.Phase=Phase.Intermission;w.SetPaused(true);rejected=true;
            foreach(BaseUpgrade kind in kinds)rejected&=!upgrades.Buy(kind);
            check(rejected&&w.Credits==before,"Paused building purchases do not spend supplies");w.SetPaused(false);
            CheckCommand.Enter(w);check(!upgrades.Buy((BaseUpgrade)(-1))&&!upgrades.Buy((BaseUpgrade)99)&&w.Credits==before,"Invalid building upgrade identifiers are rejected");
            w.Barracks.Heal(10);w.Bunker.Heal(10);

            // Only the existing automatic turret ticks here, so damage is attributable to it.
            Health target=w.SpawnEnemy(Role.Spitter,1,w.Bunker.transform.position+new Vector3(0,0,14));target.Current=target.Maximum=5000;target.WeakRadius=0;
            Physics.SyncTransforms();float hp=target.Current;w.Bunker.Brain.Tick(.5f);
            float baseHit=hp-target.Current;
            check(Mathf.Abs(baseHit-15)<.01f,"The unmodified automatic bunker cannon deals its real 15-point hit");
            before=w.Credits;bool purchased=upgrades.Buy(BaseUpgrade.BunkerFirepower);
            Physics.SyncTransforms();hp=target.Current;w.Bunker.Brain.Tick(.3f);float upgradedHit=hp-target.Current;
            check(purchased&&before-w.Credits==220&&upgrades.FirepowerLevel==1&&Mathf.Abs(upgradedHit-baseHit-5)<.01f,
                "Buying the first firepower level spends 220 and increases the next actual cannon hit by 5");
            target.Damage(10000,null);

            w.Bunker.Damage(300,null);hp=w.Bunker.Current;float maximum=w.Bunker.Maximum;
            before=w.Credits;purchased=upgrades.Buy(BaseUpgrade.BunkerArmor);
            check(purchased&&before-w.Credits==180&&upgrades.ArmorLevel==1&&w.Bunker.Maximum==maximum+400&&w.Bunker.Current==hp+400,
                "The first armor level spends 180, adds 400 maximum durability and restores 400");
            w.ProductionLeft=12;before=w.Credits;purchased=upgrades.Buy(BaseUpgrade.BarracksProduction);
            check(purchased&&before-w.Credits==200&&upgrades.BarracksLevel==1&&w.ProductionInterval==10&&w.ProductionLeft==10,
                "The first barracks level spends 200 and shortens the current and future production timer to 10 seconds");
            for(int i=0;i<kinds.Length;i++)
            {
                before=w.Credits;purchased=upgrades.Cost(kinds[i])==secondPrices[i]&&upgrades.Buy(kinds[i]);
                check(purchased&&before-w.Credits==secondPrices[i]&&upgrades.Level(kinds[i])==BaseUpgrades.MaxLevel,
                    kinds[i]+" charges its second-level price and reaches the two-level cap");
                before=w.Credits;
                check(!upgrades.CanBuy(kinds[i])&&!upgrades.Buy(kinds[i])&&w.Credits==before,
                    kinds[i]+" rejects a third level without charging");
            }
            check(w.ProductionInterval==8&&w.Bunker.Maximum==2000&&w.BunkerDamage==25,"Fully upgraded buildings expose the final 8-second, 2000-health and 25-damage values");

            // Exercise production through the real world clock, not a derived property alone.
            Health defender=w.Entities.Find(h=>h!=null&&h.Alive&&h.Role==Role.Marine);defender.Damage(10000,null);
            int count=w.MarinesAlive;w.ProductionLeft=w.ProductionInterval;
            StepWorld(w,7.75f);
            check(w.MarinesAlive==count,"Upgraded barracks do not produce a defender before eight seconds");
            StepWorld(w,.4f);
            check(w.MarinesAlive==count+1&&w.ProductionLeft>7.7f&&w.ProductionLeft<=8,
                "After eight seconds the real production loop spawns one defender and resets to the upgraded interval");

            w.Barracks.Current=w.Barracks.Maximum-800;w.Bunker.Current=w.Bunker.Maximum-900;
            hp=w.Barracks.Current;before=w.Credits;purchased=upgrades.Buy(BaseUpgrade.RepairBarracks);
            check(purchased&&before-w.Credits==100&&w.Barracks.Current==hp+450,"Barracks repair spends 100 and restores 450 durability");
            hp=w.Bunker.Current;before=w.Credits;purchased=upgrades.Buy(BaseUpgrade.RepairBunker);
            check(purchased&&before-w.Credits==120&&w.Bunker.Current==hp+600,"Bunker repair spends 120 and restores 600 durability");
            upgrades.Buy(BaseUpgrade.RepairBarracks);upgrades.Buy(BaseUpgrade.RepairBunker);
            check(w.Barracks.Current==w.Barracks.Maximum&&w.Bunker.Current==w.Bunker.Maximum,"Wave-break repairs clamp at each building's upgraded maximum");
            before=w.Credits;
            check(!upgrades.CanBuy(BaseUpgrade.RepairBarracks)&&!upgrades.Buy(BaseUpgrade.RepairBarracks)&&
                !upgrades.CanBuy(BaseUpgrade.RepairBunker)&&!upgrades.Buy(BaseUpgrade.RepairBunker)&&w.Credits==before,
                "Repairing a full-health building is rejected without charging");
            w.Barracks.Damage(1,null);w.Bunker.Damage(1,null);w.Credits=99;
            check(!upgrades.Buy(BaseUpgrade.RepairBarracks)&&!upgrades.Buy(BaseUpgrade.RepairBunker)&&w.Credits==99&&
                w.Barracks.Current==w.Barracks.Maximum-1&&w.Bunker.Current==w.Bunker.Maximum-1,
                "Insufficient repair supplies preserve both credits and building damage");
            w.Credits=10000;w.Barracks.Heal(1);w.Bunker.Heal(1);
        }

        static void CheckGarrison(GameWorld w,Action<bool,string> check)
        {
            CheckCommand.Leave(w);var fort=w.Fort;var player=w.Player;
            foreach(var guard in w.Entities)if(guard!=null&&guard.Role==Role.Marine)fort.ReleaseGuard(guard);
            bool assembled=SquadChecks.FillSquad(w);
            check(assembled,"Real hiring and guard recruitment prepare all four members for the bunker scenarios");
            if(!assembled)return;
            w.Phase=Phase.Assault;
            PlacePlayer(w,new Vector3(0,0,3));
            check(!fort.NearEntrance&&!fort.TryTogglePlayer()&&!fort.PlayerInside,"The player cannot enter a bunker remotely");
            PlacePlayer(w,fort.Entrance);check(fort.NearEntrance,"The bunker entrance recognizes a nearby living player");
            w.SetPaused(true);check(!fort.TryTogglePlayer()&&!fort.PlayerInside,"A paused game cannot admit a player into the bunker");w.SetPaused(false);
            check(fort.TryTogglePlayer()&&fort.PlayerInside&&fort.Occupants==1&&player.Self.IsSheltered&&!player.GetComponent<CharacterController>().enabled,
                "Entering the bunker activates shelter and the stationary player firing position");
            player.UpdateCamera(1);
            check(Vector3.Distance(player.View.transform.position,fort.PlayerMuzzle)<1,"Entering switches to the view at the bunker firing aperture");
            Health foe=w.SpawnEnemy(Role.Spitter,1,w.Bunker.transform.position+new Vector3(0,0,14));foe.Current=foe.Maximum=5000;foe.WeakRadius=0;
            float hp=player.Self.Current;player.Self.Damage(100,foe);player.Self.Damage(100,null);
            check(player.Self.Current==hp&&!Combat.Visible(foe,player.Self)&&w.FindTarget(foe,100)!=player.Self,
                "Sheltered players resist incoming damage and are excluded from visibility and enemy targeting");
            player.Tick(.3f);player.AimPoint=foe.Aim;hp=foe.Current;int ammo=player.Ammo;Physics.SyncTransforms();
            check(player.Fire()&&player.Ammo==ammo-1&&foe.Current<hp,"The player fires a real damaging rifle round through the front bunker aperture");
            player.Tick(.3f);player.AimPoint=fort.PlayerMuzzle+Vector3.back*15;ammo=player.Ammo;
            check(!player.Fire()&&player.Ammo==ammo,"A target behind the bunker firing arc cannot be fired at or consume ammunition");
            player.AimPoint=fort.PlayerMuzzle+new Vector3(0,20,10);
            check(!player.Fire()&&player.Ammo==ammo,"A target outside the vertical firing arc cannot consume ammunition");
            GameObject obstacle=CreateObstruction(fort.PlayerMuzzle);Physics.SyncTransforms();
            player.AimPoint=foe.Aim;hp=foe.Current;ammo=player.Ammo;
            check(player.Fire()&&player.Ammo==ammo-1&&foe.Current==hp,"World geometry in front of the aperture blocks a real player round");
            obstacle.SetActive(false);UnityEngine.Object.Destroy(obstacle);foe.Damage(10000,null);

            check(fort.OrderSquadInside()&&fort.SquadInside==0,"The bunker order begins an approach without remotely teleporting the squad inside");
            LogSquadApproach(w,"initial_start");
            StepSquad(w,18);
            bool protectedSquad=true;foreach(var member in w.Squad.Members)protectedSquad&=member!=null&&member.Self.IsSheltered;
            if(fort.SquadInside!=4||fort.Occupants!=5||!protectedSquad)LogSquadApproach(w,"initial_failed");
            check(fort.SquadInside==4&&fort.Occupants==5&&protectedSquad,"All four squad members navigate to the door and join the player within the five-person capacity");
            fort.OrderSquadInside();StepSquad(w,.2f);
            check(fort.SquadInside==4&&fort.Occupants==5,"Repeating the bunker order never duplicates occupants or exceeds capacity");
            foe=w.SpawnEnemy(Role.Spitter,1,w.Bunker.transform.position+new Vector3(0,0,14));foe.Current=foe.Maximum=5000;foe.WeakRadius=0;
            protectedSquad=true;
            foreach(var member in w.Squad.Members)
            {
                hp=member.Self.Current;member.Self.Damage(100,foe);
                protectedSquad&=member.Self.Current==hp&&!Combat.Visible(foe,member.Self)&&w.FindTarget(foe,100)!=member.Self;
            }
            check(protectedSquad,"Sheltered squad members resist damage and are excluded from enemy acquisition");
            hp=foe.Current;StepSquad(w,1.5f);
            check(foe.Current<hp,"Garrisoned squad members deal actual damage through their firing apertures");
            foe.transform.position=w.Bunker.transform.position+new Vector3(0,0,-8);hp=foe.Current;StepSquad(w,1);
            check(foe.Current==hp,"Garrisoned squad members do not fire through the rear bunker wall");
            foe.transform.position=w.Bunker.transform.position+new Vector3(0,0,14);obstacle=CreateObstruction(fort.PlayerMuzzle);hp=foe.Current;StepSquad(w,1.5f);
            check(foe.Current==hp,"An obstacle in front of bunker apertures also blocks squad fire");
            obstacle.SetActive(false);UnityEngine.Object.Destroy(obstacle);foe.Damage(10000,null);

            check(fort.TryTogglePlayer()&&!fort.PlayerInside&&!player.Self.IsSheltered&&player.GetComponent<CharacterController>().enabled&&fort.Occupants==4,
                "Exiting restores player movement and vulnerability while the squad remains inside");
            player.UpdateCamera(1);
            check(Vector3.Distance(player.View.transform.position,fort.PlayerMuzzle)>2,"Exiting restores the player's shoulder camera");
            hp=player.Self.Current;player.Self.Damage(5,null);
            check(player.Self.Current==hp-5,"A player outside the bunker can take damage again");
            check(w.Squad.GiveOrder(SquadOrder.Follow),"The follow order recalls a garrisoned squad");StepSquad(w,.2f);
            protectedSquad=false;foreach(var member in w.Squad.Members)protectedSquad|=member.Self.IsSheltered;
            check(fort.Occupants==0&&!protectedSquad,"Recalling the squad clears every sheltered slot");
            PlacePlayer(w,fort.Entrance);fort.TryTogglePlayer();fort.OrderSquadInside();StepSquad(w,8);
            if(fort.Occupants!=5)LogSquadApproach(w,"reentry_failed");
            check(fort.Occupants==5,"The player and recalled squad can enter the bunker again");
            w.Bunker.Damage(10000,null);
            protectedSquad=false;bool allAlive=player.Self.Alive;
            foreach(var member in w.Squad.Members){protectedSquad|=member.Self.IsSheltered;allAlive&=member.Self.Alive;}
            check(!fort.PlayerInside&&fort.Occupants==0&&!player.Self.IsSheltered&&!protectedSquad&&allAlive&&player.GetComponent<CharacterController>().enabled,
                "Destroying an occupied bunker evacuates every living occupant and restores movement and vulnerability");
            check(!fort.TryTogglePlayer()&&!fort.OrderSquadInside(),"A destroyed bunker rejects player entry and squad garrison orders");
            w.Barracks.Damage(10000,null);w.Phase=Phase.Intermission;CheckCommand.Enter(w);int credits=w.Credits;
            var uncappedUpgrades=new BaseUpgrades(w);
            bool rejected=true;foreach(BaseUpgrade kind in Enum.GetValues(typeof(BaseUpgrade)))rejected&=!uncappedUpgrades.CanBuy(kind)&&!uncappedUpgrades.Buy(kind);
            check(rejected&&w.Credits==credits,"Destroyed buildings cannot be upgraded or repaired even with upgrade capacity and sufficient supplies");
        }

        static GameObject CreateObstruction(Vector3 muzzle)
        {
            var obstacle=new GameObject("Diagnostic aperture obstruction");obstacle.layer=Combat.WorldLayer;
            obstacle.transform.position=new Vector3(muzzle.x,2,muzzle.z+4);
            var collider=obstacle.AddComponent<BoxCollider>();collider.size=new Vector3(7,4,.8f);return obstacle;
        }
        static void LogSquadApproach(GameWorld w,string stage)
        {
            var fort=w.Fort;var navigation=w.Squad.Navigation;Vector3 door=fort.SquadEntrance;
            Debug.Log("FORT_APPROACH stage="+stage+" player="+fort.PlayerInside+" squad="+fort.SquadInside+" guards="+fort.GuardInside+
                " occupants="+fort.Occupants+" entrance="+door.ToString("F3")+" order="+w.Squad.Order);
            // Read the live route for diagnostics without replanning or moving a tested member.
            const BindingFlags fields=BindingFlags.Instance|BindingFlags.NonPublic;
            foreach(var member in w.Squad.Members)if(member!=null)
            {
                Vector3 position=member.transform.position,goal=w.Squad.Destination(member.Slot);
                var route=typeof(SquadMember).GetField("path",fields)?.GetValue(member) as List<Vector3>;
                int waypoint=(int)(typeof(SquadMember).GetField("waypoint",fields)?.GetValue(member)??-1);
                object planned=typeof(SquadMember).GetField("plannedGoal",fields)?.GetValue(member);
                Vector3 next=route!=null&&waypoint>=0&&waypoint<route.Count?route[waypoint]:goal;
                string routeText=route==null?"unavailable":string.Join(" > ",route.ConvertAll(point=>point.ToString("F3")));
                Debug.Log("FORT_ROUTE stage="+stage+" slot="+member.Slot+" position="+position.ToString("F3")+" goal="+goal.ToString("F3")+
                    " plannedGoal="+planned+" waypoint="+waypoint+" next="+next.ToString("F3")+" inside="+fort.IsSquadInside(member)+
                    " alive="+member.Self.Alive+" approaching="+fort.IsSquadApproaching(member)+" sheltered="+member.Self.IsSheltered+
                    " moved="+member.Travelled.ToString("F3")+" doorDistance="+Vector3.Distance(position,door).ToString("F3")+
                    " fits42="+navigation.Fits(position,.42f)+" fits43="+navigation.Fits(position,.43f)+
                    " doorBlocked="+navigation.Blocked(position,door)+" nextBlocked36="+navigation.Blocked(position,next,.36f)+
                    " nextBlocked43="+navigation.Blocked(position,next,.43f)+" route="+routeText);
            }
        }
        public static void PlacePlayer(GameWorld w,Vector3 point)
        {
            var controller=w.Player.GetComponent<CharacterController>();controller.enabled=false;
            w.Player.transform.position=point+Vector3.up*.08f;controller.enabled=true;Physics.SyncTransforms();
        }
        static void StepWorld(GameWorld w,float seconds)
        {
            bool terminal=w.CommandTerminalOpen;if(terminal)CheckCommand.Leave(w);
            for(int step=0;step<Mathf.CeilToInt(seconds/.05f);step++){Physics.SyncTransforms();w.Tick(.05f);w.Effects.Tick(.05f);}
            if(terminal)CheckCommand.Enter(w);
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

using System;
using UnityEngine;

namespace Frontier
{
    public static class RepairChecks
    {
        public static void Run(GameWorld w,Action<bool,string> check)
        {
            var repairs=w.Repairs;var construction=w.Construction;
            check(repairs!=null&&repairs.WorkersAlive==2&&repairs.Workers.Count==2&&repairs.Workers.TrueForAll(r=>r.Self.Role==Role.Worker&&r.Self.Brain==null),
                "Two independent worker bodies exist without guard combat controllers");
            check(w.MarinesAlive==4&&w.MarineCapacity==8&&w.Squad.Count==0,
                "Initial workers consume neither defending marine positions nor player squad positions");
            check(construction.Sites[0].State==SiteState.Ready&&construction.Sites[3].State==SiteState.Ready&&w.Bunkers.Count==2&&
                Mathf.Abs(construction.Sites[0].Position.z-20)<.01f&&Mathf.Abs(construction.Sites[3].Position.z-20)<.01f,
                "Both initial bunker structures are bound to their forward construction sites");
            w.BeginCampaign();w.Phase=Phase.Intermission;w.IntermissionLeft=10000;CheckCommand.Leave(w);
            foreach(var h in w.Entities)if(h!=null)h.Brain=null;
            w.Command.Current=w.Command.Maximum-500;float commandHp=w.Command.Current;
            Step(w,.05f);
            check(w.Command.Current==commandHp&&repairs.TotalRepaired==0,"Workers must physically approach damage before any health is restored");
            bool safe=true;float largestStep=0;
            for(int i=0;i<300;i++)
            {
                Vector3 a=repairs.Workers[0].Self.transform.position,b=repairs.Workers[1].Self.transform.position;Step(w,.05f);
                largestStep=Mathf.Max(largestStep,Vector3.Distance(a,repairs.Workers[0].Self.transform.position),Vector3.Distance(b,repairs.Workers[1].Self.transform.position));
                foreach(var worker in repairs.Workers)safe&=RepairDirector.InsideBase(worker.Self.transform.position)&&w.Squad.Navigation.Fits(worker.Self.transform.position,GuardNavigation.Radius);
            }
            check(safe&&largestStep<=RepairDirector.WorkerSpeed*.05f+.001f&&repairs.Workers.Exists(r=>r.Travelled>1),
                "Repair workers walk collision-safe routes inside the base without teleporting");
            check(w.Command.Current>commandHp&&w.Command.Current<=commandHp+2*RepairDirector.RepairRate*15.05f+.02f&&repairs.RepairingCount>0,
                "Reached workers slowly restore buildings at no more than six health per worker-second");
            var westBlock=Art.Part(w.transform,"Repair diagnostic west screen",new Vector3(-5.25f,3,-7),new Vector3(.2f,6,11),2,collision:true);
            var eastBlock=Art.Part(w.transform,"Repair diagnostic east screen",new Vector3(5.25f,3,-7),new Vector3(.2f,6,11),2,collision:true);
            var frontBlock=Art.Part(w.transform,"Repair diagnostic front screen",new Vector3(0,3,-2.35f),new Vector3(12,6,.2f),2,collision:true);
            var rearBlock=Art.Part(w.transform,"Repair diagnostic rear screen",new Vector3(0,3,-11.65f),new Vector3(12,6,.2f),2,collision:true);
            Physics.SyncTransforms();float blockedHp=w.Command.Current;Step(w,.5f);
            check(w.Command.Current==blockedHp,"An inserted solid screen stops an already positioned worker from repairing through cover");
            foreach(var block in new[]{westBlock,eastBlock,frontBlock,rearBlock}){block.gameObject.SetActive(false);UnityEngine.Object.Destroy(block.gameObject);}
            Physics.SyncTransforms();Step(w,.1f);
            check(w.Command.Current>blockedHp,"Removing the repair obstruction resumes maintenance through a clear line of sight");
            float restored=repairs.TotalRepaired;float hp=w.Command.Current;Vector3 position=repairs.Workers[0].Self.transform.position;
            w.SetPaused(true);repairs.Tick(10);w.Tick(10);
            check(w.Command.Current==hp&&repairs.TotalRepaired==restored&&repairs.Workers[0].Self.transform.position==position,
                "General pause freezes repair amounts and civilian movement");
            CheckCommand.Enter(w);repairs.Tick(10);w.Tick(10);
            check(w.Command.Current==hp&&repairs.TotalRepaired==restored,"The real command terminal freezes maintenance just like other production");
            CheckCommand.Leave(w);w.Command.Current=w.Command.Maximum-.2f;Step(w,.1f);
            check(w.Command.Current==w.Command.Maximum,"Multiple workers clamp repairs to maximum health without overheal");

            Health tank=Fixture(w,Role.Tank,Faction.Defense,new Vector3(10,0,8),1000,500);
            Step(w,15);check(tank.Current>500&&tank.Current<tank.Maximum,"Workers approach and slowly repair a mechanical tank inside the compound");
            tank.transform.position=new Vector3(12,0,55);Physics.SyncTransforms();hp=tank.Current;Step(w,4);
            check(tank.Current==hp&&repairs.Workers.TrueForAll(r=>r.Repairing!=tank&&RepairDirector.InsideBase(r.Self.transform.position)),
                "A tank leaving the base loses maintenance and workers stay inside the compound");
            Health future=Fixture(w,Role.Squad,Faction.Defense,new Vector3(-7,0,8),500,200);future.Mechanical=true;
            Step(w,12);check(future.Current>200,"The mechanical flag permits future vehicle types to receive maintenance");Retire(w,future);Retire(w,tank);
            Health infantry=Fixture(w,Role.Marine,Faction.Defense,new Vector3(5,0,10),170,50);
            Health foundation=Fixture(w,Role.Construction,Faction.Defense,new Vector3(9,0,12),500,50);
            Health enemy=Fixture(w,Role.Tank,Faction.Invaders,new Vector3(-7,0,12),1000,50);
            Health dead=Fixture(w,Role.Factory,Faction.Defense,new Vector3(-5,0,12),1000,0);dead.Alive=false;
            Step(w,5);
            check(infantry.Current==50&&foundation.Current==50&&enemy.Current==50&&dead.Current==0&&
                !repairs.CanRepair(infantry)&&!repairs.CanRepair(foundation)&&!repairs.CanRepair(enemy)&&!repairs.CanRepair(dead),
                "Maintenance excludes infantry, construction, enemies, and destroyed buildings");
            Retire(w,infantry);Retire(w,foundation);Retire(w,enemy);Retire(w,dead);

            Health retiring=repairs.Workers[0].Self;int marines=w.MarinesAlive,capacity=w.MarineCapacity,credits=w.Credits;retiring.Damage(10000,null);
            Step(w,.05f);Step(w,19.5f);
            check(repairs.WorkersAlive==1&&repairs.ReplacementLeft(0)>0&&w.MarinesAlive==marines&&w.MarineCapacity==capacity,
                "A worker death uses its own replacement timer and leaves defender capacity untouched");
            float remaining=repairs.ReplacementLeft(0);w.SetPaused(true);repairs.Tick(30);
            check(repairs.ReplacementLeft(0)==remaining,"Worker replacement time freezes while paused");CheckCommand.Leave(w);Step(w,.7f);
            check(repairs.WorkersAlive==2&&repairs.Workers[0].Self!=retiring&&repairs.Replacements==1&&w.Credits==credits,
                "After twenty active seconds a new worker body is supplied at no cost");
            CheckFortification(w,check);
        }

        static void CheckFortification(GameWorld w,Action<bool,string> check)
        {
            var c=w.Construction;Health west=c.Sites[0].Structure,east=c.Sites[3].Structure,barracks=w.Barracks;
            w.Credits=10000;CheckCommand.Leave(w);int credits=w.Credits;
            check(!c.Fortify(west)&&w.Credits==credits,"Structural reinforcement requires access to the command terminal");
            CheckCommand.Enter(w);w.Command.Current-=123;float hqHp=w.Command.Current,hqMax=w.Command.Maximum;credits=w.Credits;
            check(w.Buy(2)&&w.Command.Maximum==hqMax+650&&w.Command.Current==hqHp+650&&w.Credits==credits-120&&w.CommandFortifyLevel==1,
                "Headquarters reinforcement adds 650 maximum and current health while preserving existing damage");
            credits=w.Credits;check(w.UpgradeCost(2)==240&&w.Buy(2)&&w.UpgradeCost(2)==360&&w.Buy(2)&&w.Credits==credits-600,
                "Headquarters reinforcement escalates through its second and third prices");
            credits=w.Credits;hqMax=w.Command.Maximum;check(!w.Buy(2)&&w.CommandFortifyLevel==3&&w.Credits==credits&&w.Command.Maximum==hqMax,
                "Headquarters maximum health upgrades stop at three paid levels");
            west.Current=west.Maximum-120;float hp=west.Current,max=west.Maximum;credits=w.Credits;
            check(c.Fortify(west)&&west.Maximum==max+600&&west.Current==hp+600&&w.Credits==credits-120&&c.FortifyLevel(west)==1,
                "First bunker reinforcement adds 600 maximum health and exactly 600 current health for 120 supplies");
            check(c.FortifyLevel(east)==0&&east.Maximum==1200,"Reinforcement belongs to the selected physical construction site");
            float missing=west.Maximum-west.Current;check(Mathf.Abs(missing-120)<.01f,"Reinforcement preserves pre-existing damage instead of fully healing the building");
            west.Current=west.Maximum;credits=w.Credits;max=west.Maximum;
            check(w.Upgrades.Cost(BaseUpgrade.RepairBunker,west)==240&&w.Upgrades.Buy(BaseUpgrade.RepairBunker,west)&&west.Maximum==max+600&&west.Current==west.Maximum&&w.Credits==credits-240,
                "The former bunker repair action reinforces even a fully healthy building with the second-level price");
            max=barracks.Maximum;hp=barracks.Current;credits=w.Credits;
            check(w.Upgrades.Buy(BaseUpgrade.RepairBarracks,barracks)&&barracks.Maximum==max+450&&barracks.Current==hp+450&&w.Credits==credits-100,
                "The former barracks repair action now grants 450 maximum and current health");
            credits=w.Credits;check(c.RepairSite(0)&&c.FortifyLevel(0)==3&&w.Credits==credits-360,"Third site reinforcement uses its escalating 360-supply cost");
            credits=w.Credits;max=west.Maximum;hp=west.Current;
            check(!c.CanFortify(west)&&!c.RepairSite(0)&&west.Maximum==max&&west.Current==hp&&w.Credits==credits,
                "Three purchased levels cap reinforcement without charging or changing health");
            w.Credits=119;max=east.Maximum;check(!c.Fortify(east)&&w.Credits==119&&east.Maximum==max,"Insufficient funds cannot buy a reinforcement level");w.Credits=10000;
            check(w.Upgrades.Buy(BaseUpgrade.BunkerArmor),"Global bunker armor remains separately purchasable");
            west.Damage(100000,null);credits=w.Credits;
            check(c.Sites[0].State==SiteState.Destroyed&&!c.Fortify(west)&&c.FortifyLevel(0)==3&&w.Credits==credits,
                "A destroyed site retains purchased levels but cannot be revived by reinforcement");
            // Clear potential service staff from the build footprint using fixture positioning only.
            foreach(var worker in w.Repairs.Workers){worker.Self.transform.position=new Vector3(worker.Self.Slot==0?-6.5f:6.5f,0,0);worker.Target=null;}
            foreach(var unit in w.Entities)if(unit!=null&&unit.Alive&&!unit.IsBuilding&&unit.Role!=Role.Player&&Mathf.Abs(unit.transform.position.x+20.5f)<6&&Mathf.Abs(unit.transform.position.z-20)<6)
                unit.transform.position=new Vector3(0,0,10);
            Physics.SyncTransforms();check(c.Build(0),"The destroyed forward bunker accepts a real reconstruction order");CheckCommand.Leave(w);
            for(int i=0;i<365;i++){Physics.SyncTransforms();c.Tick(.05f);}
            Health rebuilt=c.Sites[0].Structure;
            check(c.Sites[0].State==SiteState.Ready&&rebuilt!=west&&rebuilt.Maximum==1200+3*600+400*w.Upgrades.ArmorLevel&&rebuilt.Current==rebuilt.Maximum&&c.FortifyLevel(rebuilt)==3,
                "Reconstruction inherits cumulative site reinforcement and the independent global armor bonus exactly once");
            CheckCommand.Enter(w);credits=w.Credits;
            check(!w.Upgrades.Buy(BaseUpgrade.RepairBunker,west)&&w.Credits==credits,"An obsolete destroyed building identity cannot purchase reinforcement for its replacement");
            check(c.Build(4),"The empty factory site can begin its real construction");Health foundation=c.Sites[4].Structure;credits=w.Credits;
            check(!c.Fortify(foundation)&&w.Credits==credits,"A foundation cannot be reinforced before the building is complete");CheckCommand.Leave(w);
            for(int i=0;i<565;i++){Physics.SyncTransforms();c.Tick(.05f);}Health factory=c.Sites[4].Structure;
            CheckCommand.Enter(w);max=factory.Maximum;factory.Current-=200;hp=factory.Current;credits=w.Credits;
            check(c.RepairSite(4)&&factory.Maximum==max+700&&factory.Current==hp+700&&w.Credits==credits-180&&c.FortifyLevel(4)==1,
                "Factory reinforcement adds exactly 700 maximum and current health and has its own persistent level");
            CheckCommand.Leave(w);
        }
        static Health Fixture(GameWorld w,Role role,Faction faction,Vector3 point,float maximum,float current)
        {
            var root=Art.Root("Repair diagnostic "+role,w.transform,point);var h=root.gameObject.AddComponent<Health>();h.Setup(role,faction,maximum,1.2f,.7f);h.Current=current;return h;
        }
        static void Retire(GameWorld w,Health h){w.Entities.Remove(h);h.gameObject.SetActive(false);UnityEngine.Object.Destroy(h.gameObject);}
        static void Step(GameWorld w,float seconds)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds/.05f);i++)
            {
                float dt=Mathf.Min(.05f,seconds-i*.05f);if(dt<=0)break;
                w.SimTime+=dt;Physics.SyncTransforms();w.Repairs.Tick(dt);w.Effects.Tick(dt);Physics.SyncTransforms();
            }
        }
    }
}

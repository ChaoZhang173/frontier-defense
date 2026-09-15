using System;
using UnityEngine;

namespace Frontier
{
    public static class SquadChecks
    {
        public static void Run(GameWorld w,Action<bool,string> check)
        {
            var squad=w.Squad;
            w.BeginCampaign();
            check(squad.Count==0&&w.MarinesAlive==4&&squad.Contracts==0,
                "Garrison and squad begin separately with no inherited mercenary contracts");
            check(SquadDirector.Capacity==4&&SquadDirector.MercenaryCapacity==2&&SquadDirector.GuardCapacity==2&&
                SquadDirector.Names.Length==4&&squad.IsMercenarySlot(0)&&squad.IsMercenarySlot(1)&&!squad.IsMercenarySlot(2)&&!squad.IsMercenarySlot(3),
                "The four slots are reserved for two mercenaries and two recruited guards");
            int credits=w.Credits;
            check(!squad.Hire()&&w.Credits==credits,"Hiring requires the command terminal or intermission");
            check(w.TryOpenCommandTerminal()&&w.Paused,"The nearby command terminal pauses the battle");
            check(squad.Hire()&&squad.Count==1&&squad.Contracts==1&&squad.HasContract(0)&&w.Credits==credits-SquadDirector.HireCost,
                "The first 120-supply purchase creates one living mercenary and one permanent contract");
            credits=w.Credits;
            check(!squad.Hire()&&squad.Contracts==1&&w.Credits==credits,"Insufficient funds never create a contract or squad member");
            w.Credits=1000;bool second=squad.Hire();credits=w.Credits;
            check(second&&squad.MercenaryCount==2&&squad.Contracts==2&&!squad.CanHire&&!squad.Hire()&&w.Credits==credits,
                "Exactly two paid contracts are available and repeated hiring cannot charge for a third");
            check(w.MarinesAlive==4,"Hiring mercenaries never consumes an automatic garrison slot");
            w.SetPaused(false);

            Health guard=w.Entities.Find(h=>h!=null&&h.Alive&&h.Role==Role.Marine&&!h.IsSheltered);
            int oldSlot=guard.Slot,guardId=guard.GetInstanceID(),entities=w.Entities.Count,baseCount=w.MarinesAlive;
            var oldBrain=guard.Brain;guard.Damage(133,null);float guardHp=guard.Current,guardMax=guard.Maximum;
            credits=w.Credits;bool recruited=RecruitNearby(w,guard);
            check(recruited&&squad.Members[2]!=null&&squad.Members[2].Self==guard&&guard.GetInstanceID()==guardId&&w.Entities.Count==entities,
                "Recruitment transfers the existing guard health object rather than spawning a replacement");
            check(recruited&&guard.Role==Role.Squad&&guard.Slot==2&&guard.Current==guardHp&&guardHp==37&&guard.Maximum==guardMax&&guardMax==170&&w.Credits==credits,
                "The free guard transfer preserves its wounded 37/170 health without spending supplies");
            check(recruited&&guard.Brain==null&&(oldBrain==null||!oldBrain.enabled)&&w.MarinesAlive==baseCount-1,
                "Recruitment disables the old guard brain and removes one member from the base garrison");
            Health replacement=w.SpawnMarine(oldSlot,false);
            check(replacement!=null&&replacement!=guard&&replacement.Role==Role.Marine&&replacement.Slot==oldSlot&&w.MarinesAlive==baseCount,
                "The recruited guard's actual garrison slot can be filled by normal barracks spawning");
            bool full=FillSquad(w);
            check(full&&squad.Count==4&&squad.MercenaryCount==2&&squad.RecruitedGuardCount==2&&squad.Contracts==2,
                "Real hiring and recruitment produce the full two-mercenary plus two-guard squad");
            if(!full)return;
            check(squad.Members[0].IsMercenary&&squad.Members[1].IsMercenary&&!squad.Members[2].IsMercenary&&!squad.Members[3].IsMercenary&&
                squad.Members[0].Self.Maximum==280&&squad.Members[1].Self.Maximum==280&&squad.Members[2].Self.Maximum==170&&
                squad.Members[0].RifleDamage==18&&squad.Members[2].RifleDamage==10,
                "Mercenaries begin stronger at 280 health and 18 damage while transferred guards retain 170 health and 10 damage");
            baseCount=w.MarinesAlive;credits=w.Credits;
            Health extra=w.Entities.Find(h=>h!=null&&h.Alive&&h.Role==Role.Marine&&!h.IsSheltered);
            bool overfill=extra!=null&&RecruitNearby(w,extra);
            check(extra!=null&&!overfill&&!squad.RecruitGuard(extra)&&squad.RecruitedGuardCount==2&&w.MarinesAlive==baseCount&&w.Credits==credits,
                "A third guard cannot be recruited, removed from the base roster or charged");
            w.SetPaused(true);Vector3 pausedPosition=squad.Members[0].transform.position;float pausedTime=w.SimTime;
            w.Tick(5);
            check(w.SimTime==pausedTime&&squad.Members[0].transform.position==pausedPosition,"Squad movement freezes with the paused simulation");
            check(!squad.GiveOrder(SquadOrder.Hold),"Orders cannot change while the battle is paused");
            w.SetPaused(false);

            PlacePlayer(w,new Vector3(-19,0,-15));w.Player.Yaw=180;Step(w,20);
            check(Arrived(squad),"All four mixed squad members route around the HQ and barracks");
            check(ClearOfBuildings(squad),"Followers remain outside building collision volumes");
            PlacePlayer(w,new Vector3(13,0,18));w.Player.Yaw=0;Step(w,22);
            check(Arrived(squad),"All four followers cross the base and navigate around the bunker");
            check(squad.GiveOrder(SquadOrder.Hold),"A living player can issue the hold-position order");
            Step(w,3);Vector3 hold=squad.HoldPoint;
            PlacePlayer(w,new Vector3(-13,0,18));Step(w,10);
            check(squad.Order==SquadOrder.Hold&&squad.HoldPoint==hold&&Arrived(squad),"A held squad remains at its order location when the player moves away");
            squad.GiveOrder(SquadOrder.Follow);PlacePlayer(w,new Vector3(-13, -4,55));Step(w,25);
            check(Arrived(squad)&&squad.Members[0].transform.position.y<-3.9f,"Follow resumes and all members traverse the west ramp");
            PlacePlayer(w,new Vector3(13,-4,55));Step(w,12);
            PlacePlayer(w,new Vector3(13,0,18));Step(w,25);
            check(Arrived(squad)&&ClearOfBuildings(squad),"Squad returns uphill through the east entrance without crossing walls");

            PlacePlayer(w,new Vector3(0,0,18));Step(w,14);
            var near=w.SpawnEnemy(Role.Runner,0,new Vector3(-3,0,23));near.Current=near.Maximum=2000;
            var focus=w.SpawnEnemy(Role.Spitter,1,new Vector3(3,0,24));focus.Current=focus.Maximum=2000;
            Physics.SyncTransforms();Step(w,.2f);squad.MarkTarget(focus);Step(w,.05f);
            bool assigned=true;foreach(var m in squad.Members)assigned&=m.Target==focus;
            check(assigned,"Every squad member prioritizes the player's marked visible target");
            float hp=focus.Current,nearHp=near.Current;Step(w,1.5f);
            check(focus.Current<hp&&near.Current==nearHp,"Squad bullets deal real damage to the marked target instead of the nearer alternative");
            focus.Damage(10000,squad.Members[0].Self);Step(w,.1f);
            check(squad.Kills==1&&squad.FocusTarget==null,"Squad kills are counted and dead focus targets are cleared");
            near.Damage(10000,null);

            var lostGuard=squad.Members[3].Self;credits=w.Credits;baseCount=w.MarinesAlive;int losses=squad.Losses;
            lostGuard.Damage(10000,null);
            check(squad.Count==3&&squad.RecruitedGuardCount==1&&squad.Members[3]==null&&w.MarinesAlive==baseCount&&squad.Losses==losses+1,
                "A recruited-guard casualty clears only its squad slot and never decrements the base roster twice");
            Step(w,12.3f);
            check(squad.Members[3]==null&&squad.Count==3&&!squad.HasContract(3)&&squad.RespawnRemaining(3)==0&&w.Credits==credits,
                "A fallen ordinary guard does not respawn after the mercenary timer or create a paid contract");
            full=FillSquad(w);
            check(full&&w.MarinesAlive==baseCount-1&&w.Credits==credits&&squad.RecruitedGuardCount==2,
                "A fallen guard is replaced by another free real transfer from the remaining base garrison");
            if(!full)return;

            var lost=squad.Members[1].Self;int lostId=lost.GetInstanceID();credits=w.Credits;baseCount=w.MarinesAlive;losses=squad.Losses;
            lost.Damage(10000,null);
            check(squad.Count==3&&w.MarinesAlive==baseCount&&squad.Losses==losses+1&&squad.Contracts==2&&squad.HasContract(1)&&
                Mathf.Abs(squad.RespawnRemaining(1)-12)<.001f,
                "A mercenary death retains its contract and starts a twelve-second free respawn without consuming a base defender");
            w.Phase=Phase.Intermission;
            check(!squad.CanHire&&!squad.Hire()&&w.Credits==credits&&squad.Contracts==2,
                "A dead contracted mercenary cannot be repurchased or charged again during intermission");
            w.Phase=Phase.Assault;w.SetPaused(true);float remaining=squad.RespawnRemaining(1);pausedTime=w.SimTime;
            w.Tick(5);squad.Tick(5);
            check(squad.Members[1]==null&&squad.RespawnRemaining(1)==remaining&&w.SimTime==pausedTime,
                "Both world and squad updates leave the mercenary respawn timer frozen while paused");
            w.SetPaused(false);Step(w,11.75f);
            check(squad.Members[1]==null&&squad.RespawnRemaining(1)>0&&squad.RespawnRemaining(1)<.3f&&w.Credits==credits,
                "A mercenary does not respawn before its full twelve seconds");
            w.Player.Self.Damage(10000,null);
            check(!squad.GiveOrder(SquadOrder.Hold)&&!w.TryOpenCommandTerminal(),"A dead player cannot command or recruit");
            Step(w,.4f);
            check(squad.Count==4&&squad.Members[1]!=null&&squad.Members[1].Self.GetInstanceID()!=lostId&&squad.Members[1].Self.Maximum==280&&
                Vector3.Distance(squad.Members[1].transform.position,new Vector3(0,0,-.5f))<4&&squad.RespawnRemaining(1)==0&&
                squad.Contracts==2&&squad.Hired==2&&w.Credits==credits,
                "The contracted mercenary returns near HQ after twelve seconds for free even while the player is dead");
            Step(w,2);check(squad.Count==4,"The player's death does not delete any of the four squad members");w.Player.Respawn();
            PlacePlayer(w,new Vector3(0,0,3));
            var ally=squad.Members[0].Self;ally.transform.position=new Vector3(0,0,8);
            var foe=w.SpawnEnemy(Role.Spitter,0,new Vector3(0,0,14));Physics.SyncTransforms();
            hp=foe.Current;float allyHp=ally.Current;
            Combat.Rifle(w.Player.Self,w.Player.Self.Aim,(foe.Aim-w.Player.Self.Aim).normalized,25,true);
            check(foe.Current<hp&&ally.Current==allyHp,"Player rounds pass through squadmates without damage or obstruction");
            foe.Damage(10000,null);

            var heavy=w.SpawnEnemy(Role.Colossus,0,new Vector3(-8,0,18));Physics.SyncTransforms();
            hp=heavy.Current;Vector3 core=heavy.transform.TransformPoint(heavy.WeakPoint);
            Combat.Rifle(w.Player.Self,w.Player.Self.Aim,(core-w.Player.Self.Aim).normalized,25,true);
            check(Mathf.Abs(hp-heavy.Current-29.7f)<.1f&&w.Player.CriticalTimer>0,"A ray through the exposed core applies armor and the 1.65 weak-point multiplier");
            hp=heavy.Current;Combat.Rifle(w.Player.Self,w.Player.Self.Aim,(heavy.Aim-w.Player.Self.Aim).normalized,25,true);
            check(Mathf.Abs(hp-heavy.Current-18)<.1f,"A body shot uses armor reduction without a false weak-point bonus");
            w.Player.Tick(.3f);w.Player.AimPoint=heavy.Aim;
            int ammo=w.Player.Ammo;bool fired=w.Player.Fire();
            check(fired&&w.Player.Ammo==ammo-1&&w.Player.RecoilPitch>0,"Firing consumes one round and produces recoverable recoil");
            for(int i=0;i<20;i++)w.Player.Tick(.1f);
            check(w.Player.RecoilPitch==0,"Recoil settles after firing stops");
            w.SetPaused(true);ammo=w.Player.Ammo;
            check(!w.Player.Fire()&&w.Player.Ammo==ammo,"Paused firing cannot spend ammunition");w.SetPaused(false);
            heavy.Damage(10000,null);
            var blocked=w.SpawnEnemy(Role.Runner,0,new Vector3(0,0,-17));Physics.SyncTransforms();hp=blocked.Current;
            Combat.Rifle(w.Player.Self,w.Player.Self.Aim,(blocked.Aim-w.Player.Self.Aim).normalized,1000,true);
            check(blocked.Current==hp,"Ignoring friendly units never allows shooting through the HQ");
            bool wall=Combat.Trace(new Vector3(0,1,3),Vector3.back,15,null,out var wallHit);
            check(wall&&wallHit.collider.gameObject.layer==Combat.WorldLayer,"A projectile whose owner is gone still collides with world geometry");
            blocked.Damage(10000,null);
            CheckMercenaryTraining(w,check);

            var finalMercenary=squad.Members[0].Self;finalMercenary.Damage(10000,null);
            remaining=squad.RespawnRemaining(0);w.Command.Damage(10000,null);Step(w,13);
            check(w.Phase==Phase.Defeat&&squad.Members[0]==null&&squad.RespawnRemaining(0)==remaining,
                "A destroyed headquarters ends the mission and prevents contracted mercenaries from respawning");
        }
        static void CheckMercenaryTraining(GameWorld w,Action<bool,string> check)
        {
            var squad=w.Squad;w.Phase=Phase.Assault;PlacePlayer(w,new Vector3(0,0,18));
            float mercenaryHit=MemberHit(w,squad.Members[0]),guardHit=MemberHit(w,squad.Members[2]);
            check(Mathf.Abs(mercenaryHit-18)<.01f&&Mathf.Abs(guardHit-10)<.01f&&mercenaryHit>guardHit,
                "Real untrained mercenary and recruited-guard bullets apply their distinct 18 and 10 point damage");
            int defense=w.DefenseLevel;w.DefenseLevel=defense+1;
            float upgradedMercenaryHit=MemberHit(w,squad.Members[0]),upgradedGuardHit=MemberHit(w,squad.Members[2]);
            check(Mathf.Abs(upgradedMercenaryHit-mercenaryHit-3)<.01f&&Mathf.Abs(upgradedGuardHit-guardHit-3)<.01f,
                "One shared defense level adds three actual rifle damage to both squad classes");
            w.DefenseLevel=defense;

            w.Phase=Phase.Intermission;w.Credits=10000;
            bool built=w.Construction.Build(1);w.Construction.Tick(22.1f);
            check(built&&w.Construction.ReserveOperational,"A real completed reinforcement barracks unlocks the mercenary training scenario");
            int credits=w.Credits;bool started=w.Tech.Research(Research.MercenaryTraining);
            check(started&&w.Tech.ActiveResearch==(int)Research.MercenaryTraining&&w.Credits==credits-300&&w.Tech.Duration==22,
                "Elite mercenary training uses its real 300-supply purchase and twenty-two-second research timer");
            w.Tech.Tick(21.75f);
            check(!w.Tech.IsComplete(Research.MercenaryTraining)&&squad.Members[0].Self.Maximum==280&&squad.Members[0].RifleDamage==18+3*defense,
                "Training bonuses are not granted before research finishes");
            squad.Members[0].Self.Damage(100,null);squad.Members[1].Self.Damage(60,null);squad.Members[2].Self.Damage(20,null);
            float firstHp=squad.Members[0].Self.Current,secondHp=squad.Members[1].Self.Current;
            float guardHp=squad.Members[2].Self.Current,guardMaximum=squad.Members[2].Self.Maximum,guardDamage=squad.Members[2].RifleDamage;
            w.Tech.Tick(.4f);
            check(w.Tech.IsComplete(Research.MercenaryTraining)&&w.Tech.ActiveResearch==-1&&
                squad.Members[0].Self.Maximum==360&&squad.Members[1].Self.Maximum==360&&
                squad.Members[0].Self.Current==firstHp+80&&squad.Members[1].Self.Current==secondHp+80,
                "Completing research adds eighty maximum health and heals exactly eighty on both living mercenaries");
            check(squad.Members[2].Self.Current==guardHp&&squad.Members[2].Self.Maximum==guardMaximum&&
                squad.Members[2].RifleDamage==guardDamage&&squad.Members[3].Self.Maximum==170,
                "Mercenary-specific training does not heal, strengthen or increase the health limit of ordinary recruited guards");
            firstHp=squad.Members[0].Self.Current;secondHp=squad.Members[1].Self.Current;
            squad.ApplyMercenaryTraining();squad.ApplyMercenaryTraining();credits=w.Credits;
            check(squad.Members[0].Self.Maximum==360&&squad.Members[1].Self.Maximum==360&&
                squad.Members[0].Self.Current==firstHp&&squad.Members[1].Self.Current==secondHp&&
                !w.Tech.Research(Research.MercenaryTraining)&&w.Credits==credits,
                "Repeated training application and duplicate research cannot stack health, healing or supply charges");
            w.Phase=Phase.Assault;
            float trainedHit=MemberHit(w,squad.Members[0]),unmodifiedGuardHit=MemberHit(w,squad.Members[2]);
            check(Mathf.Abs(trainedHit-mercenaryHit-6)<.01f&&Mathf.Abs(unmodifiedGuardHit-guardHit)<.01f,
                "Completed training adds six damage to an actual mercenary bullet and leaves guard bullet damage unchanged");

            var lost=squad.Members[0].Self;credits=w.Credits;lost.Damage(10000,null);Step(w,12.15f);
            check(squad.Members[0]!=null&&squad.Members[0].Self!=lost&&squad.Members[0].Self.Maximum==360&&
                squad.Members[0].Self.Current==360&&squad.Members[0].RifleDamage==24+3*defense&&w.Credits==credits,
                "A free mercenary respawn is born with the researched 360 health and twenty-four damage");
            firstHp=squad.Members[0].Self.Current;squad.ApplyMercenaryTraining();
            check(squad.Members[0].Self.Maximum==360&&squad.Members[0].Self.Current==firstHp&&squad.Members[0].RifleDamage==24+3*defense,
                "Applying training after a trained respawn cannot grant a second bonus");

            w.Phase=Phase.Intermission;started=w.Tech.Research(Research.InfantryArmor);w.Tech.Tick(16.1f);
            bool protectedInfantry=started&&w.Tech.IsComplete(Research.InfantryArmor);
            w.Player.Self.Heal(1000);float hp=w.Player.Self.Current;w.Player.Self.Damage(20,null);
            protectedInfantry&=Mathf.Abs(hp-w.Player.Self.Current-17)<.01f;
            foreach(var member in squad.Members)
            {
                member.Self.Heal(1000);hp=member.Self.Current;member.Self.Damage(20,null);
                protectedInfantry&=Mathf.Abs(hp-member.Self.Current-17)<.01f;
            }
            Health baseGuard=w.Entities.Find(h=>h!=null&&h.Alive&&h.Role==Role.Marine);
            if(baseGuard!=null){baseGuard.Heal(1000);hp=baseGuard.Current;baseGuard.Damage(20,null);protectedInfantry&=Mathf.Abs(hp-baseGuard.Current-17)<.01f;}
            else protectedInfantry=false;
            check(protectedInfantry,"Completed infantry armor reduces a real twenty-point hit to seventeen for the player, both mercenaries, both recruits and a base guard");
            w.Phase=Phase.Assault;
        }
        static float MemberHit(GameWorld w,SquadMember member)
        {
            Vector3 oldPosition=member.transform.position;
            member.transform.position=new Vector3(member.Slot%2==0?-3:3,0,18);member.Replan();
            Health target=w.SpawnEnemy(Role.Spitter,0,new Vector3(0,0,25));target.Current=target.Maximum=5000;target.WeakRadius=0;
            Physics.SyncTransforms();w.Squad.MarkTarget(target);float hp=target.Current;int shots=member.Shots;
            member.Tick(1);float hit=hp-target.Current;
            if(member.Shots!=shots+1)Debug.Log("SQUAD_DAMAGE slot="+member.Slot+" shots="+(member.Shots-shots)+" damage="+hit+" target="+member.Target);
            target.Damage(10000,null);member.transform.position=oldPosition;member.Replan();Physics.SyncTransforms();return hit;
        }
        // Test setup still uses the real purchases and real guard transfers. No roster is assigned directly.
        // Call with a living player outside a bunker and enough funds. Prefer field guards; if none can
        // join, create an ordinary guard through the real base spawn API before transferring it.
        public static bool FillSquad(GameWorld w)
        {
            if(w==null||w.Player==null||!w.Player.Self.Alive||w.Ended||w.Fort.PlayerInside)return false;
            Phase originalPhase=w.Phase;bool originalPaused=w.Paused,originalTerminal=w.CommandTerminalOpen;
            Vector3 originalPosition=w.Player.transform.position;float originalYaw=w.Player.Yaw;
            try
            {
                w.Phase=Phase.Intermission;w.SetPaused(false);
                while(w.Squad.Contracts<SquadDirector.MercenaryCapacity)if(!w.Squad.Hire())return false;
                if(w.Squad.MercenaryCount!=SquadDirector.MercenaryCapacity)return false;
                w.Phase=Phase.Assault;
                while(w.Squad.RecruitedGuardCount<SquadDirector.GuardCapacity)
                {
                    bool recruited=false;
                    foreach(var guard in w.Entities)
                    {
                        if(guard==null||!guard.Alive||guard.Role!=Role.Marine||guard.IsSheltered)continue;
                        if(RecruitNearby(w,guard)){recruited=true;break;}
                    }
                    if(!recruited)
                    {
                        Health reinforcement=w.SpawnMarine(deploy:false);
                        if(reinforcement==null||!RecruitNearby(w,reinforcement))return false;
                    }
                }
                return w.Squad.Count==SquadDirector.Capacity;
            }
            finally
            {
                PlacePlayer(w,originalPosition-Vector3.up*.08f);w.Player.Yaw=originalYaw;w.Phase=originalPhase;
                if(originalTerminal)w.TryOpenCommandTerminal();else w.SetPaused(originalPaused);
            }
        }
        static bool RecruitNearby(GameWorld w,Health guard)
        {
            Vector3[] offsets={Vector3.back*2,Vector3.left*2,Vector3.right*2,Vector3.forward*2};
            foreach(var offset in offsets)
            {
                PlacePlayer(w,w.Squad.Navigation.Resolve(guard.transform.position+offset));
                if(w.Squad.CanRecruitGuard(guard))return w.Squad.RecruitGuard(guard);
            }
            return false;
        }
        static void PlacePlayer(GameWorld w,Vector3 p)
        {
            var cc=w.Player.GetComponent<CharacterController>();cc.enabled=false;w.Player.transform.position=p+Vector3.up*.08f;cc.enabled=true;Physics.SyncTransforms();
        }
        static void Step(GameWorld w,float seconds)
        {
            int steps=Mathf.CeilToInt(seconds/.05f);
            for(int i=0;i<steps;i++)
            {
                float dt=Mathf.Min(.05f,seconds-i*.05f);if(w.Active)w.SimTime+=dt;
                Physics.SyncTransforms();w.Squad.Tick(dt);w.Effects.Tick(w.Active?dt:0);
            }
        }
        static bool Arrived(SquadDirector s)
        {
            bool arrived=true;
            foreach(var m in s.Members)
            {
                if(m==null){arrived=false;continue;}
                if(Vector3.Distance(m.transform.position,s.Destination(m.Slot))>1.6f)
                {
                    arrived=false;Debug.Log("SQUAD_ROUTE time="+GameWorld.I.SimTime+" slot="+m.Slot+" position="+m.transform.position+" destination="+s.Destination(m.Slot)+" travelled="+m.Travelled);
                }
            }
            return arrived;
        }
        static bool ClearOfBuildings(SquadDirector s)
        {
            foreach(var m in s.Members)if(Physics.CheckCapsule(m.transform.position+Vector3.up*.65f,m.transform.position+Vector3.up*1.5f,.35f,Combat.WorldMask,QueryTriggerInteraction.Ignore))return false;
            return true;
        }
    }
}

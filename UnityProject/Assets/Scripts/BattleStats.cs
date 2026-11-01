using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Observes real combat; no balancing decisions depend on these measurements.
    public sealed class BattleStats
    {
        [Serializable] public sealed class WaveResult
        {
            public int wave,spawned,killed,peakEnemies,peakFrontlineEnemies,guardLosses,squadLosses,buildingsLost,playerDeaths;
            public int suppliesEarned,suppliesSpent,endingSupplies;
            public float seconds,dualLanePressureSeconds,hqDamage,bunkerDamage,barracksDamage,hqHealth;
            public bool cleared;
        }
        readonly GameWorld world;
        public readonly List<WaveResult> Waves=new List<WaveResult>();
        public int SuppliesEarned,GuardLosses,SquadLosses,BuildingsLost,PeakFrontlineEnemies;
        public float HeadquartersDamage,BunkerDamage,BarracksDamage,DualLanePressureSeconds;
        WaveResult current;
        int startCredits,startEarned,startSpawned,startKills,startDeaths;
        public BattleStats(GameWorld owner){world=owner;}
        public void StartWave()
        {
            Snapshot(false);
            current=new WaveResult{wave=world.Wave};Waves.Add(current);
            startCredits=world.Credits;startEarned=SuppliesEarned;startSpawned=world.TotalSpawned;
            startKills=world.ScheduledKills;startDeaths=world.Deaths;
        }
        public void Earn(int amount){SuppliesEarned+=amount;}
        public void RecordDamage(Health victim,float amount)
        {
            if(victim.Faction!=Faction.Defense)return;
            if(victim.Role==Role.Command){HeadquartersDamage+=amount;if(current!=null)current.hqDamage+=amount;}
            else if(victim.Role==Role.Bunker){BunkerDamage+=amount;if(current!=null)current.bunkerDamage+=amount;}
            else if(victim.Role==Role.Barracks){BarracksDamage+=amount;if(current!=null)current.barracksDamage+=amount;}
        }
        public void RecordLoss(Health victim)
        {
            if(victim.Faction!=Faction.Defense)return;
            if(victim.Role==Role.Marine){GuardLosses++;if(current!=null)current.guardLosses++;}
            else if(victim.Role==Role.Squad){SquadLosses++;if(current!=null)current.squadLosses++;}
            else if(victim.IsBuilding){BuildingsLost++;if(current!=null)current.buildingsLost++;}
        }
        public void Tick(float dt)
        {
            if(current==null||world.Phase==Phase.Intermission)return;
            int west=0,east=0;
            foreach(var h in world.Entities)
            {
                if(h==null||!h.Alive||h.Faction!=Faction.Invaders||h.IsBuilding||h.transform.position.z>31)continue;
                if(h.Lane==0)west++;else east++;
            }
            int front=west+east;
            PeakFrontlineEnemies=Mathf.Max(PeakFrontlineEnemies,front);
            current.peakFrontlineEnemies=Mathf.Max(current.peakFrontlineEnemies,front);
            current.peakEnemies=Mathf.Max(current.peakEnemies,world.EnemiesAlive);
            current.seconds+=dt;
            if(west>=3&&east>=3){DualLanePressureSeconds+=dt;current.dualLanePressureSeconds+=dt;}
            Snapshot(false);
        }
        public void Snapshot(bool cleared)
        {
            if(current==null)return;
            current.spawned=world.TotalSpawned-startSpawned;current.killed=world.ScheduledKills-startKills;
            current.playerDeaths=world.Deaths-startDeaths;current.suppliesEarned=SuppliesEarned-startEarned;
            current.endingSupplies=world.Credits;current.suppliesSpent=startCredits+current.suppliesEarned-world.Credits;
            current.hqHealth=world.Command.Current;current.cleared|=cleared;
        }
    }
}

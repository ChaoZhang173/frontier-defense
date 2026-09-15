using UnityEngine;

namespace Frontier
{
    // Diagnostic strategies share the same aiming and movement bot. Purchases use
    // the ordinary game actions and earned credits, including real build times.
    public sealed class BalanceDriver
    {
        readonly GameWorld world;
        readonly bool prepared,withSquad;
        int plannedWave=-1;
        public BalanceDriver(GameWorld owner,string mode)
        {
            world=owner;prepared=mode=="victory";withSquad=mode!="balance_rifle";
        }
        public void Begin()
        {
            world.Player.Bot=true;
            if(withSquad){world.TryOpenCommandTerminal();world.Squad.Hire();world.SetPaused(false);}
        }
        public void Step()
        {
            if(world.Phase==Phase.Intermission)
            {
                if(plannedWave!=world.Wave){plannedWave=world.Wave;Prepare();}
                if(prepared)ResearchNext();
                return;
            }
            if(withSquad&&world.Phase==Phase.Assault&&world.Squad.RecruitedGuardCount<2)
                foreach(var guard in world.Entities.ToArray())
                    if(world.Squad.CanRecruitGuard(guard)){world.Squad.RecruitGuard(guard);break;}
        }
        void Prepare()
        {
            if(world.Command.Current<world.Command.Maximum-600)world.Buy(2);
            if(prepared)
            {
                if(world.Construction.Sites[0].State==SiteState.Empty||world.Construction.Sites[0].State==SiteState.Destroyed)world.Construction.Build(0);
                if(world.Wave>=2&&(world.Construction.Sites[1].State==SiteState.Empty||world.Construction.Sites[1].State==SiteState.Destroyed))world.Construction.Build(1);
                for(int id=2;id<4;id++)if(world.Construction.Sites[id].State==SiteState.Destroyed)world.Construction.Build(id);
                ResearchNext();
            }
            if(withSquad)world.Squad.Hire();
            foreach(var bunker in world.Bunkers)
                if(bunker!=null&&bunker.Alive&&bunker.Current<bunker.Maximum-500)world.Upgrades.Buy(BaseUpgrade.RepairBunker,bunker);
            foreach(var barracks in world.BarracksBuildings)
                if(barracks!=null&&barracks.Alive&&barracks.Current<barracks.Maximum-350)world.Upgrades.Buy(BaseUpgrade.RepairBarracks,barracks);
            if(prepared)
            {
                if(world.Wave>=3)world.Upgrades.Buy(BaseUpgrade.BunkerArmor);
                if(world.Wave>=3)world.Upgrades.Buy(BaseUpgrade.BunkerFirepower);
                if(world.Wave>=4)world.Upgrades.Buy(BaseUpgrade.BarracksProduction);
            }
            else if(withSquad)
            {
                // Preserve the v0.5 non-expansion strategy as a comparison.
                if(world.Wave==1)world.Upgrades.Buy(BaseUpgrade.BunkerArmor);
                if(world.Wave==2)world.Upgrades.Buy(BaseUpgrade.BarracksProduction);
                if(world.Wave==3)world.Upgrades.Buy(BaseUpgrade.BunkerFirepower);
                if(world.Wave==4)
                {
                    world.Upgrades.Buy(BaseUpgrade.BunkerArmor);
                    world.Upgrades.Buy(BaseUpgrade.BunkerFirepower);
                    world.Upgrades.Buy(BaseUpgrade.BarracksProduction);
                }
            }
            world.Buy(0);world.Buy(1);
        }
        void ResearchNext()
        {
            if(!world.Tech.Available||world.Tech.ActiveResearch>=0)return;
            foreach(var research in new[]{Research.ArmorPiercing,Research.InfantryArmor,Research.MercenaryTraining})
                if(!world.Tech.IsComplete(research)){world.Tech.Research(research);break;}
        }
    }
}

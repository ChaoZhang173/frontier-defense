using UnityEngine;

namespace Frontier
{
    public enum BaseUpgrade { BarracksProduction, BunkerArmor, BunkerFirepower, RepairBarracks, RepairBunker }

    public sealed class BaseUpgrades
    {
        public const int MaxLevel = 2;
        readonly GameWorld world;
        public int BarracksLevel { get; private set; }
        public int ArmorLevel { get; private set; }
        public int FirepowerLevel { get; private set; }
        public BaseUpgrades(GameWorld owner) { world = owner; }
        public int Level(BaseUpgrade kind) => kind == BaseUpgrade.BarracksProduction ? BarracksLevel :
            kind == BaseUpgrade.BunkerArmor ? ArmorLevel : kind == BaseUpgrade.BunkerFirepower ? FirepowerLevel : 0;
        public int Cost(BaseUpgrade kind)
        {
            switch (kind)
            {
                case BaseUpgrade.BarracksProduction: return 200 + BarracksLevel * 120;
                case BaseUpgrade.BunkerArmor: return 180 + ArmorLevel * 120;
                case BaseUpgrade.BunkerFirepower: return 220 + FirepowerLevel * 140;
                case BaseUpgrade.RepairBarracks: return 100;
                case BaseUpgrade.RepairBunker: return 120;
                default: return 0;
            }
        }
        Health Building(BaseUpgrade kind,Health selected=null) => selected!=null ? selected :
            kind == BaseUpgrade.BarracksProduction || kind == BaseUpgrade.RepairBarracks ? world.Barracks : world.Bunker;
        bool Available(BaseUpgrade kind,Health selected=null)
        {
            if ((int)kind < 0 || (int)kind > 4 || world.Phase != Phase.Intermission || world.Paused || world.Ended) return false;
            if((int)kind<3)
            {
                var buildings=kind==BaseUpgrade.BarracksProduction?world.BarracksBuildings:world.Bunkers;
                return Level(kind)<MaxLevel && buildings.Exists(h=>h!=null&&h.Alive);
            }
            Health building=Building(kind,selected);
            var registered=kind==BaseUpgrade.RepairBarracks?world.BarracksBuildings:world.Bunkers;
            return building!=null&&building.Alive&&registered.Contains(building)&&building.Current<building.Maximum;
        }
        public bool CanBuy(BaseUpgrade kind,Health selected=null) => Available(kind,selected) && world.Credits >= Cost(kind);
        public bool Buy(BaseUpgrade kind,Health selected=null)
        {
            if (!Available(kind,selected)) return false;
            int price = Cost(kind);
            if (world.Credits < price) { world.Notice("补给不足 · 先选择最需要的防线升级"); return false; }
            world.Credits -= price;
            switch (kind)
            {
                case BaseUpgrade.BarracksProduction:
                    BarracksLevel++;
                    world.ProductionLeft = Mathf.Min(world.ProductionLeft, world.ProductionInterval);
                    if(world.Construction!=null)world.Construction.ProductionLeft=Mathf.Min(world.Construction.ProductionLeft,world.ProductionInterval);
                    break;
                case BaseUpgrade.BunkerArmor:
                    ArmorLevel++;
                    foreach(var bunker in world.Bunkers)if(bunker!=null&&bunker.Alive){bunker.Maximum+=400;bunker.Heal(400);}
                    break;
                case BaseUpgrade.BunkerFirepower: FirepowerLevel++; break;
                case BaseUpgrade.RepairBarracks: Building(kind,selected).Heal(450); break;
                case BaseUpgrade.RepairBunker: Building(kind,selected).Heal(600); break;
            }
            world.Sounds.Play(SoundId.Upgrade, .6f);
            world.Notice((int)kind < 3 ? "建筑升级完成 · 下一波立即生效" : "维修完成 · 防线耐久已恢复");
            return true;
        }
        public string Description(BaseUpgrade kind,Health selected=null)
        {
            var h=Building(kind,selected);
            string durability=h!=null&&h.Alive?h.Current.ToString("0")+" / "+h.Maximum.ToString("0"):"已摧毁 · 可在建造页重建";
            switch (kind)
            {
                case BaseUpgrade.BarracksProduction: return "全体兵营补员 " + world.ProductionInterval.ToString("0") + " 秒 → " + Mathf.Max(8, world.ProductionInterval - 2).ToString("0") + " 秒\n当前驻军上限 " + world.MarineCapacity+" 人";
                case BaseUpgrade.BunkerArmor: return "全体地堡耐久上限 +400，并恢复 400\n新建或重建的地堡继承装甲升级";
                case BaseUpgrade.BunkerFirepower: return "全体地堡自动炮每发伤害 +5\n当前伤害 " + world.BunkerDamage.ToString("0");
                case BaseUpgrade.RepairBarracks: return "恢复兵营 450 耐久\n" + durability;
                case BaseUpgrade.RepairBunker: return "恢复地堡 600 耐久\n" + durability;
                default: return "";
            }
        }
    }
}

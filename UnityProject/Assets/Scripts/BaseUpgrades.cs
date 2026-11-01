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
        public int Level(BaseUpgrade kind,Health selected=null) => kind == BaseUpgrade.BarracksProduction ? BarracksLevel :
            kind == BaseUpgrade.BunkerArmor ? ArmorLevel : kind == BaseUpgrade.BunkerFirepower ? FirepowerLevel :
            world.Construction!=null?world.Construction.FortifyLevel(Building(kind,selected)):0;
        public int Cost(BaseUpgrade kind,Health selected=null)
        {
            switch (kind)
            {
                case BaseUpgrade.BarracksProduction: return 200 + BarracksLevel * 120;
                case BaseUpgrade.BunkerArmor: return 180 + ArmorLevel * 120;
                case BaseUpgrade.BunkerFirepower: return 220 + FirepowerLevel * 140;
                case BaseUpgrade.RepairBarracks: return world.Construction!=null?world.Construction.FortifyCost(Building(kind,selected)):100;
                case BaseUpgrade.RepairBunker: return world.Construction!=null?world.Construction.FortifyCost(Building(kind,selected)):120;
                default: return 0;
            }
        }
        Health Building(BaseUpgrade kind,Health selected=null) => selected!=null ? selected :
            kind == BaseUpgrade.BarracksProduction || kind == BaseUpgrade.RepairBarracks ? world.Barracks : world.Bunker;
        bool Available(BaseUpgrade kind,Health selected=null)
        {
            if ((int)kind < 0 || (int)kind > 4 || !world.CanManage) return false;
            if((int)kind<3)
            {
                var buildings=kind==BaseUpgrade.BarracksProduction?world.BarracksBuildings:world.Bunkers;
                return Level(kind)<MaxLevel && buildings.Exists(h=>h!=null&&h.Alive);
            }
            Health building=Building(kind,selected);
            var registered=kind==BaseUpgrade.RepairBarracks?world.BarracksBuildings:world.Bunkers;
            return building!=null&&building.Alive&&registered.Contains(building)&&world.Construction!=null&&world.Construction.CanFortify(building);
        }
        public bool CanBuy(BaseUpgrade kind,Health selected=null) => Available(kind,selected) && world.Credits >= Cost(kind,selected);
        public bool Buy(BaseUpgrade kind,Health selected=null)
        {
            if (!Available(kind,selected)) return false;
            if(kind==BaseUpgrade.RepairBarracks||kind==BaseUpgrade.RepairBunker)return world.Construction.Fortify(Building(kind,selected));
            int price = Cost(kind,selected);
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
            }
            world.Sounds.Play(SoundId.Upgrade, .6f);
            world.Notice("建筑升级完成 · 已应用到防线");
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
                case BaseUpgrade.RepairBarracks: return "兵营最大耐久 +450，同时恢复 450\n加固 "+Level(kind,selected)+" / 3 · 重建继承\n"+durability;
                case BaseUpgrade.RepairBunker: return "地堡最大耐久 +600，同时恢复 600\n加固 "+Level(kind,selected)+" / 3 · 重建继承\n"+durability;
                default: return "";
            }
        }
    }
}

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
        Health Building(BaseUpgrade kind) => kind == BaseUpgrade.BarracksProduction || kind == BaseUpgrade.RepairBarracks ? world.Barracks : world.Bunker;
        bool Available(BaseUpgrade kind)
        {
            if ((int)kind < 0 || (int)kind > 4 || world.Phase != Phase.Intermission || world.Paused || world.Ended) return false;
            Health building = Building(kind);
            if (building == null || !building.Alive) return false;
            return (int)kind < 3 ? Level(kind) < MaxLevel : building.Current < building.Maximum;
        }
        public bool CanBuy(BaseUpgrade kind) => Available(kind) && world.Credits >= Cost(kind);
        public bool Buy(BaseUpgrade kind)
        {
            if (!Available(kind)) return false;
            int price = Cost(kind);
            if (world.Credits < price) { world.Notice("补给不足 · 先选择最需要的防线升级"); return false; }
            world.Credits -= price;
            switch (kind)
            {
                case BaseUpgrade.BarracksProduction:
                    BarracksLevel++;
                    world.ProductionLeft = Mathf.Min(world.ProductionLeft, world.ProductionInterval);
                    break;
                case BaseUpgrade.BunkerArmor:
                    ArmorLevel++;
                    world.Bunker.Maximum += 400;
                    world.Bunker.Heal(400);
                    break;
                case BaseUpgrade.BunkerFirepower: FirepowerLevel++; break;
                case BaseUpgrade.RepairBarracks: world.Barracks.Heal(450); break;
                case BaseUpgrade.RepairBunker: world.Bunker.Heal(600); break;
            }
            world.Sounds.Play(SoundId.Upgrade, .6f);
            world.Notice((int)kind < 3 ? "建筑升级完成 · 下一波立即生效" : "维修完成 · 防线耐久已恢复");
            return true;
        }
        public string Description(BaseUpgrade kind)
        {
            switch (kind)
            {
                case BaseUpgrade.BarracksProduction: return "补员间隔 " + world.ProductionInterval.ToString("0") + " 秒 → " + Mathf.Max(8, world.ProductionInterval - 2).ToString("0") + " 秒\n驻军上限保持 8 人";
                case BaseUpgrade.BunkerArmor: return "耐久上限 +400，并恢复 400\n当前上限 " + world.Bunker.Maximum.ToString("0");
                case BaseUpgrade.BunkerFirepower: return "自动炮每发伤害 +5\n当前伤害 " + world.BunkerDamage.ToString("0");
                case BaseUpgrade.RepairBarracks: return "恢复兵营 450 耐久\n" + world.Barracks.Current.ToString("0") + " / " + world.Barracks.Maximum.ToString("0");
                case BaseUpgrade.RepairBunker: return "恢复地堡 600 耐久\n" + world.Bunker.Current.ToString("0") + " / " + world.Bunker.Maximum.ToString("0");
                default: return "";
            }
        }
    }
}

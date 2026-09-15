using UnityEngine;

namespace Frontier
{
    public enum Research { InfantryArmor, ArmorPiercing, MercenaryTraining }
    public sealed class TechnologyDirector
    {
        readonly GameWorld world;
        readonly bool[] completed=new bool[3];
        public int ActiveResearch { get; private set; }=-1;
        public float SecondsLeft { get; private set; }
        public float Duration { get; private set; }
        public bool Available=>world.Construction.ReserveOperational;
        public TechnologyDirector(GameWorld owner){world=owner;}
        bool Valid(Research kind)=>(int)kind>=0&&(int)kind<completed.Length;
        public bool IsComplete(Research kind)=>Valid(kind)&&completed[(int)kind];
        public int Cost(Research kind)=>!Valid(kind)?0:kind==Frontier.Research.InfantryArmor?240:kind==Frontier.Research.ArmorPiercing?280:300;
        public string Name(Research kind)=>kind==Frontier.Research.InfantryArmor?"步兵复合装甲":kind==Frontier.Research.ArmorPiercing?"穿甲弹药":"精英雇佣兵训练";
        public bool CanResearch(Research kind)=>Valid(kind)&&world.Construction.CanManage&&Available&&ActiveResearch<0&&!IsComplete(kind)&&world.Credits>=Cost(kind);
        public bool Research(Research kind)
        {
            if(!CanResearch(kind))return false;
            world.Credits-=Cost(kind);ActiveResearch=(int)kind;
            Duration=SecondsLeft=kind==Frontier.Research.InfantryArmor?16:kind==Frontier.Research.ArmorPiercing?20:22;
            world.Notice("研究已开始 · 返回战场后继续推进");world.Sounds.Play(SoundId.Command,.6f);return true;
        }
        public void Tick(float dt)
        {
            if(!world.Active||dt<=0||ActiveResearch<0||!Available)return;
            SecondsLeft=Mathf.Max(0,SecondsLeft-dt);
            if(SecondsLeft>0)return;
            completed[ActiveResearch]=true;
            if(ActiveResearch==(int)Frontier.Research.MercenaryTraining)world.Squad.ApplyMercenaryTraining();
            world.Notice(ActiveResearch==0?"复合装甲研究完成 · 全体步兵受伤降低 15%":ActiveResearch==1?"穿甲弹研究完成 · 全军对重甲目标更有效":"精英训练完成 · 雇佣兵耐久 +80，伤害 +6");
            ActiveResearch=-1;world.Sounds.Play(SoundId.Upgrade,.7f);
        }
        public string Description(Research kind)=>kind==Frontier.Research.InfantryArmor?
            "玩家、小队和驻军受伤降低 15%\n研究 16 秒 · 本局持续生效":
            kind==Frontier.Research.ArmorPiercing?"友军对重甲敌人的伤害提高 25%\n护甲减伤 28% → 10% · 研究 20 秒":
            "仅雇佣兵：耐久 +80，伤害 +6\n研究 22 秒 · 免费复活后保留";
        public string Status(Research kind)
        {
            if(!Valid(kind))return "无效科技";
            if(IsComplete(kind))return "已完成";
            if(ActiveResearch==(int)kind)return Available?"研究中 · "+Mathf.CeilToInt(SecondsLeft)+" 秒":"研究暂停 · 重建增援兵营";
            if(!Available)return "需要已投产的增援兵营";
            if(ActiveResearch>=0)return "另一项研究进行中";
            return world.Credits<Cost(kind)?"补给不足":"可研究";
        }
    }
}

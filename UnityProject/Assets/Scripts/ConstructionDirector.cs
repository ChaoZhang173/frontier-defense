using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public enum SiteState { Empty, Constructing, Ready, Destroyed }
    public sealed class BuildSite
    {
        public int Id;
        public string Name;
        public Role Role;
        public Vector3 Position;
        public SiteState State;
        public Health Structure;
        public float SecondsLeft, Duration;
        public Transform Marker;
    }

    public sealed class ConstructionDirector
    {
        readonly GameWorld world;
        public readonly BuildSite[] Sites;
        public float ProductionLeft=12;
        public int Completed, LostSites;
        readonly Collider[] obstacles=new Collider[40];
        public bool ReserveOperational => Sites[1].State==SiteState.Ready && Sites[1].Structure!=null && Sites[1].Structure.Alive;
        public bool CanManage => world.CanManage;

        public ConstructionDirector(GameWorld owner)
        {
            world=owner;
            Sites=new[] {
                MakeSite(0,"西侧地堡",Role.Bunker,new Vector3(-20.5f,0,6),null),
                MakeSite(1,"增援兵营",Role.Barracks,new Vector3(9,0,-16),null),
                MakeSite(2,"主兵营",Role.Barracks,world.Barracks.transform.position,world.Barracks),
                MakeSite(3,"东侧地堡",Role.Bunker,world.Bunker.transform.position,world.Bunker),
                MakeSite(4,"战车工厂",Role.Factory,new Vector3(20,0,-15),null)
            };
        }
        BuildSite MakeSite(int id,string name,Role role,Vector3 position,Health existing)
        {
            var site=new BuildSite {Id=id,Name=name,Role=role,Position=position,Structure=existing,
                State=existing==null?SiteState.Empty:SiteState.Ready,Duration=role==Role.Factory?28:role==Role.Bunker?18:22};
            site.Marker=Art.BuildSiteMarker(world.transform,position,role);
            site.Marker.gameObject.SetActive(existing==null);
            if(existing!=null)existing.gameObject.name=name;
            return site;
        }
        public static Vector3 Size(Role role)=>role==Role.Factory?new Vector3(8,4.8f,8):
            role==Role.Bunker?new Vector3(7.4f,3,6.1f):new Vector3(7.5f,4.2f,8.3f);
        bool Valid(int id)=>id>=0 && id<Sites.Length;
        public int Cost(int id)=>Valid(id)?(Sites[id].Role==Role.Factory?650:Sites[id].Role==Role.Bunker?320:400):0;
        bool CanStart(int id)=>Valid(id)&&CanManage&&(Sites[id].State==SiteState.Empty||Sites[id].State==SiteState.Destroyed);
        public bool SpaceClear(int id)
        {
            if(!Valid(id))return false;
            var site=Sites[id];Vector3 size=Size(site.Role);
            foreach(var h in world.Entities)
            {
                if(h==null||!h.Alive||h.IsSheltered||h.IsBuilding)continue;
                Vector3 delta=h.transform.position-site.Position;
                if(Mathf.Abs(delta.x)<size.x*.5f+h.Radius+.2f && Mathf.Abs(delta.z)<size.z*.5f+h.Radius+.2f)return false;
            }
            Physics.SyncTransforms();
            int count=Physics.OverlapBoxNonAlloc(site.Position+Vector3.up*(size.y*.5f+.1f),
                new Vector3(size.x*.5f,size.y*.5f-.05f,size.z*.5f),obstacles,Quaternion.identity,Combat.WorldMask,QueryTriggerInteraction.Ignore);
            if(count>=obstacles.Length)return false;
            for(int i=0;i<count;i++)
            {
                var h=obstacles[i].GetComponentInParent<Health>();
                if(h!=site.Structure || h==null)return false;
            }
            return true;
        }
        public bool CanBuild(int id)=>CanStart(id)&&world.Credits>=Cost(id)&&SpaceClear(id);
        public bool Build(int id)
        {
            if(!CanStart(id))return false;
            if(world.Credits<Cost(id)){world.Notice("补给不足 · 建造需要 "+Cost(id));return false;}
            if(!SpaceClear(id)){world.Notice("施工位置被占用 · 请先让人员离开地面标记");return false;}
            var site=Sites[id];world.Credits-=Cost(id);
            Retire(site.Structure);
            Transform root=Art.Root(site.Name+" · 施工中",world.transform,site.Position);
            root.gameObject.layer=Combat.WorldLayer;
            var h=root.gameObject.AddComponent<Health>();
            h.Setup(Role.Construction,Faction.Defense,site.Role==Role.Factory?900:site.Role==Role.Bunker?650:750,1.6f,Size(site.Role).x*.45f);
            var box=root.gameObject.AddComponent<BoxCollider>();box.size=Size(site.Role);box.center=Vector3.up*(box.size.y*.5f);
            Art.ConstructionFrame(root,site.Role);
            site.Structure=h;site.State=SiteState.Constructing;site.SecondsLeft=site.Duration;
            site.Marker.gameObject.SetActive(true);
            world.Squad.RebuildNavigation();
            world.Notice(site.Name+" 已开工 · "+site.Duration.ToString("0")+" 秒，返回战场后继续施工");
            world.Sounds.Play(SoundId.Command,.6f);return true;
        }
        void Retire(Health h)
        {
            if(h==null)return;
            world.Entities.Remove(h);world.Bunkers.Remove(h);world.BarracksBuildings.Remove(h);
            h.gameObject.SetActive(false);Object.Destroy(h.gameObject);
        }
        void Complete(BuildSite site)
        {
            Retire(site.Structure);
            float hp=site.Role==Role.Factory?1400:site.Role==Role.Bunker?1200+400*world.Upgrades.ArmorLevel:1100;
            site.Structure=world.CreateBuilding(site.Role,site.Position,hp,Size(site.Role));
            site.Structure.gameObject.name=site.Name;
            site.State=SiteState.Ready;site.SecondsLeft=0;site.Marker.gameObject.SetActive(false);
            if(site.Id==2){world.Barracks=site.Structure;world.ProductionLeft=world.ProductionInterval;}
            if(site.Id==3)world.Bunker=site.Structure;
            if(site.Id==1)ProductionLeft=world.ProductionInterval;
            world.PeakMarineCapacity=Mathf.Max(world.PeakMarineCapacity,world.MarineCapacity);
            world.Squad.RebuildNavigation();Completed++;
            world.Effects.Burst(site.Position+Vector3.up,Art.Cyan,2.4f);
            world.Notice(site.Name+" 已投产"+(site.Id==4?" · 自动生产一辆坦克":site.Id==1?" · 驻军上限 12，科技研究已解锁":" · 防线火力就绪"));
            world.Sounds.Play(SoundId.Upgrade,.65f);
        }
        public void Tick(float dt)
        {
            if(!world.Active||dt<=0)return;
            foreach(var site in Sites)
            {
                if(site.State!=SiteState.Constructing||site.Structure==null||!site.Structure.Alive)continue;
                site.SecondsLeft=Mathf.Max(0,site.SecondsLeft-dt);
                if(site.SecondsLeft<=0)Complete(site);
            }
            if(ReserveOperational&&world.MarinesAlive<world.MarineCapacity)
            {
                ProductionLeft-=dt;
                if(ProductionLeft<=0){world.SpawnMarine(producer:Sites[1].Structure);ProductionLeft=world.ProductionInterval;}
            }
        }
        public void OnKilled(Health h)
        {
            foreach(var site in Sites)if(site.Structure==h&&site.State!=SiteState.Destroyed)
            {
                site.State=SiteState.Destroyed;site.SecondsLeft=0;LostSites++;
                site.Marker.gameObject.SetActive(true);
                world.Notice(site.Name+" 已被摧毁 · 可在指挥中心原址重建");
                return;
            }
        }
        BaseUpgrade RepairKind(int id)=>Sites[id].Role==Role.Bunker?BaseUpgrade.RepairBunker:BaseUpgrade.RepairBarracks;
        public int RepairCost(int id)=>Valid(id)?(Sites[id].Role==Role.Factory?180:world.Upgrades.Cost(RepairKind(id))):0;
        public bool CanRepairSite(int id)
        {
            if(!Valid(id)||Sites[id].State!=SiteState.Ready)return false;
            if(Sites[id].Role!=Role.Factory)return world.Upgrades.CanBuy(RepairKind(id),Sites[id].Structure);
            var h=Sites[id].Structure;
            return CanManage&&h!=null&&h.Alive&&h.Current<h.Maximum&&world.Credits>=RepairCost(id);
        }
        public bool RepairSite(int id)
        {
            if(!CanRepairSite(id))return false;
            if(Sites[id].Role!=Role.Factory)return world.Upgrades.Buy(RepairKind(id),Sites[id].Structure);
            world.Credits-=RepairCost(id);Sites[id].Structure.Heal(700);
            world.Notice("战车工厂维修完成 · 恢复 700 耐久");world.Sounds.Play(SoundId.Upgrade,.6f);return true;
        }
        public string Status(int id)
        {
            if(!Valid(id))return "无效位置";
            var s=Sites[id];
            if(s.State==SiteState.Constructing)return "施工中 · "+Mathf.CeilToInt(s.SecondsLeft)+" 秒";
            if(s.State==SiteState.Ready)return "已投产";
            if(!SpaceClear(id))return "位置被占用 · 先移开人员";
            return s.State==SiteState.Destroyed?"已摧毁 · 可原址重建":"空地 · 可下达施工指令";
        }
        public string NameFor(Health h)
        {
            foreach(var site in Sites)if(site.Structure==h)return site.Name+(site.State==SiteState.Constructing?" · 施工中":"");
            return h.Role==Role.Command?"指挥中心":h.Role==Role.Factory?"战车工厂":h.Role==Role.Barracks?"兵营":"地堡";
        }
    }
}

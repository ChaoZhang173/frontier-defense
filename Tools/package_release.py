"""Package an already built release and verify archive integrity."""
from pathlib import Path
import json, plistlib, subprocess, zipfile

root = Path(__file__).resolve().parents[1]
version = "0.6"
readme = """边境防线 v0.6 · 战车工厂与中央防线

五波共460名敌人（40 / 58 / 92 / 120 / 150），六处敌巢成组进攻，第三波起双线重叠。守住指挥中心，消灭全部进攻部队即可胜利。

WASD移动；Shift冲刺；鼠标转向；左键射击；右键瞄准；R换弹；Esc暂停。
E：瞄准4米内守卫免费招募；靠近指挥中心打开/关闭终端；靠近地堡后门进入或退出。
B命令小队实际走到目标地堡后门入驻；F跟随/撤出；G驻守。
来到正面中央矮墙附近按G，四名队员会分列矮墙后，中央给坦克留出通道。矮墙能挡身体，玩家、小队和坦克可从上方射击。

战车工厂650补给，施工28秒。建好后自动用24秒生产一辆坦克，并从出口行驶到中央阵位。
坦克最多一辆，1000耐久，优先攻击重甲，炮弹有范围伤害且受真实墙体遮挡。坦克不占小队或基地守卫人数。
坦克阵亡后工厂自动补产；工厂损坏时停产，重建后继续，现存坦克继续战斗。工厂耐久1400，维修180补给恢复700。

第一至第四波结束后获得奖励并进入45秒自由整备，不会弹出升级菜单；第五波清场即胜利。可移动、射击、招募和部署小队，倒计时结束自动开战。
所有个人强化、防线升级、建造、科技、雇佣合同和维修都要前往指挥中心按E。终端中暂停战斗、整备倒计时、施工、研究和复活；关闭后继续。

小队最多4人：2名精英雇佣兵、2名免费招募守卫。雇佣兵每份合同120补给，阵亡12秒后免费复活；普通守卫保留当前生命且不占基地驻军上限，阵亡后需重新招募。
每堡5席，守卫就近驻本侧地堡，玩家和小队到达后按所需人数让位。西侧地堡320补给/18秒，增援兵营400补给/22秒。增援兵营增加驻军上限8→12，并解锁复合装甲、穿甲弹药和精英雇佣兵训练。

击杀迅行兽/酸脊兽/铁甲兽分别获得10/18/48补给；第1至第5波奖励175/210/245/280/315。
每处敌巢后方有防御塔，共6座：650耐久、22米射程，会攻击靠近的玩家方单位。每摧毁一座额外获得220补给，本局不重生。防御塔属于可选进攻目标，不影响波次清场与胜利。

开局160补给；玩家阵亡6秒后重生，指挥中心被毁则失败。
电脑键鼠试玩，手机触屏未适配。Windows已构建，尚未Windows真机验证。
在线试玩：https://frontier-defense-outpost-07.fumeshroom.chatgpt.site
字体许可见Licenses目录。
"""
for platform in ("Mac", "Windows", "Web"):
    folder = root / "Builds" / platform
    (folder / "试玩说明.txt").write_text(readme)
    if platform != "Web":
        launch = "打开 Frontier Defense.app。" if platform == "Mac" else "完整解压本文件夹，然后打开 Frontier Defense.exe。不要只复制exe，旁边的数据与动态库都是运行所需文件。"
        (folder / "开始试玩.txt").write_text(f"边境防线 v{version}\n\n{launch}\n操作详见试玩说明.txt。\n")

out = root / "Releases"
out.mkdir(exist_ok=True)
reports=[]
for platform in ("Mac", "Windows", "Web"):
    folder = root / "Builds" / platform
    archive = out / f"FrontierDefense-{version}-{platform}.zip"
    if platform == "Mac":
        app = folder / "Frontier Defense.app"
        info=plistlib.loads((app/"Contents/Info.plist").read_bytes())
        assert info["CFBundleShortVersionString"] == "0.6.0", info
        subprocess.run(["/usr/bin/ditto","-c","-k","--keepParent",str(app),str(archive)],check=True)
        with zipfile.ZipFile(archive,"a",compression=zipfile.ZIP_DEFLATED) as z:
            for p in folder.rglob("*"):
                relative=p.relative_to(folder)
                if p.is_file() and (relative.parts[0]=="Licenses" or len(relative.parts)==1):
                    z.write(p,str(relative))
    else:
        with zipfile.ZipFile(archive,"w",compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
            for p in sorted(folder.rglob("*")):
                relative=p.relative_to(folder)
                if not p.is_file() or any("DoNotShip" in part for part in relative.parts) or p.name==".DS_Store" or p.suffix in {".pdb",".log"}: continue
                z.write(p,str(relative))
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None
        assert not any("DoNotShip" in n for n in z.namelist())
        reports.append({"platform":platform,"archive":str(archive.relative_to(root)),"bytes":archive.stat().st_size,"files":len(z.namelist()),"integrity":"passed"})
(root/"Verification/v0.6/releases.json").write_text(json.dumps(reports,ensure_ascii=False,indent=2))
print(json.dumps(reports,ensure_ascii=False,indent=2))

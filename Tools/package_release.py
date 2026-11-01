"""Package an already built release and verify archive integrity."""
from pathlib import Path
import json, plistlib, subprocess, zipfile
from build_offline_web import build as build_offline_web

root = Path(__file__).resolve().parents[1]
version = "0.7"
readme = """边境防线 v0.7 · 传送门基地与自动反攻

WASD移动；Shift冲刺；鼠标转向；左键射击；右键瞄准；R换弹；Esc暂停。
E：瞄准4米内守卫免费招募；靠近指挥中心打开终端；靠近地堡后门进出。
B命令小队入驻指定地堡；F跟随；G驻守。中央矮墙附近按G展开防线。

地图扩展至东西120米、纵深160米，增加废墟、两翼道路及六座传送门基地。
每个据点包含可摧毁的传送门、兵巢、两座炮塔和驻防部队，纵深据点更坚固。
摧毁传送门会取消该据点尚未出动的增援；摧毁兵巢停止当地守军补员。
五波计划56/80/116/156/200，共608名敌人；实际出兵随传送门摧毁而减少。
新增潜袭兽与远程攻城兽，同时活动敌兵上限100，含据点驻军。

两个地堡开局建成，前移到两处入口外侧，与中央矮墙共同承受正面压力。
兵营独立生产突击兵，每12秒一人，增援兵营投产后每9秒一人。
6人实际到达绿色集结区后自动进攻传送门基地，突击兵上限24。
突击兵不占基地守卫8/12人、先锋小队4人或坦克名额。

2名维修工在基地中巡回修理存活的建筑和机械单位，每人每秒6耐久。
工人需要走到目标旁边，阵亡20秒后免费补员；不能修复已毁建筑或基地外单位。
原建筑维修购买项改为结构加固，同时增加最大耐久和等量当前耐久，每处最多3级，重建保留。
每级指挥中心+650、兵营+450、地堡+600、工厂+700；价格随等级递增。
全体地堡装甲强化仍独立生效。所有购买需靠近指挥中心按E。

战车工厂650补给/28秒，建成24秒自动生产一辆坦克，阵亡后补产。
两份雇佣兵合同各120补给，阵亡12秒后免费复活；两名随行守卫可在战场免费招募。
第一到第四波结束进入45秒自由整备；第五波清场后胜利，指挥中心失守则失败。
暂停和终端会停止集结、战斗、施工、研究、维修与复活计时。

本版为Unity Mac与桌面网页版，使用电脑键鼠操作。字体许可见Licenses。
"""

for platform in ("Mac", "Web"):
    folder = root / "Builds" / platform
    (folder / "试玩说明.txt").write_text(readme)
    if platform == "Mac":
        launch = "打开 Frontier Defense.app。"
        (folder / "开始试玩.txt").write_text(f"边境防线 v{version}\n\n{launch}\n操作详见试玩说明.txt。\n")

out = root / "Releases"
out.mkdir(exist_ok=True)
reports=[]
for platform in ("Mac", "Web"):
    folder = root / "Builds" / platform
    archive = out / f"FrontierDefense-{version}-{platform}.zip"
    if platform == "Mac":
        app = folder / "Frontier Defense.app"
        info=plistlib.loads((app/"Contents/Info.plist").read_bytes())
        assert info["CFBundleShortVersionString"] == "0.7.0", info
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
offline = build_offline_web(root / "Builds/Web", root / "Builds/OfflineWeb",
    out / f"FrontierDefense-{version}-OfflineWeb.zip",
    root / f"Verification/v{version}/offline-web-static.json")
reports.append({"platform": "OfflineWeb", "archive": str(Path(offline["archive"]).relative_to(root)),
                "bytes": offline["archiveBytes"], "integrity": "passed",
                "staticChecksPassed": offline["staticChecksPassed"]})
(root/"Verification/v0.7/releases.json").write_text(json.dumps(reports,ensure_ascii=False,indent=2))
print(json.dumps(reports,ensure_ascii=False,indent=2))

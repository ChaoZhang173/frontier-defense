"""Package an already built release and verify archive integrity."""
from pathlib import Path
import json, plistlib, subprocess, zipfile

root = Path(__file__).resolve().parents[1]
version = "0.3"
readme = """边境防线 v0.3 · 地堡与防线经营

五波共308个敌人，比v0.2增加约56%。守住指挥中心，清除全部敌人。

WASD 移动；Shift冲刺；鼠标转向；左键射击；右键瞄准；R换弹。
E：靠近指挥中心正门打开小队终端；靠近地堡后门进入地堡，驻守时再次按E退出。
B：靠近后门或已在地堡内时命令小队入驻；队员会走到门口再进入。
F：小队跟随 / 撤出跟随。G：小队驻守 / 撤出驻守。Esc暂停。
小队命令及地堡进出在开战后使用。

地堡有四个席位（玩家+三名小队队员）。正面射界左右各70度。
入驻时地堡承受伤害，地堡摧毁后从后门撤出。

45秒波间整备可查看下一波情报、雇佣队员、升级武器及建筑、维修基地。
兵营补员：12→10→8秒；地堡装甲：每级+400；地堡自动炮：每级每发+5。
建筑被毁后无法维修。队员阵亡后需要重新雇佣。

开局160补给，队员每人120补给，最多3人，与兵营8名驻军分开计算。
玩家阵亡6秒后重生；指挥中心被毁则失败。

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
        assert info["CFBundleShortVersionString"] == "0.3.0", info
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
(root/"Verification/v0.3/releases.json").write_text(json.dumps(reports,ensure_ascii=False,indent=2))
print(json.dumps(reports,ensure_ascii=False,indent=2))

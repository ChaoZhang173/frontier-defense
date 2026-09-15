"""Package an already built release and verify archive integrity."""
from pathlib import Path
import json, plistlib, subprocess, zipfile

root = Path(__file__).resolve().parents[1]
version = "0.5.1"
readme = """边境防线 v0.5.1 · 敌军攻势

五波共408个敌人，从基地外围6处敌巢成组出击。查看顶部进攻预警，第三波起注意东西线重叠攻势。重甲与护卫在坡脚集结后一起推进。守住指挥中心，清除全部敌人。

WASD移动；Shift冲刺；鼠标转向；左键射击；右键瞄准；R换弹；Esc暂停。
E：瞄准4米内的守卫免费招募；靠近指挥中心打开终端；靠近地堡后门进入，驻守时再次按E退出。
B：靠近目标地堡后门或已驻堡时命令小队入驻；队员实际走到门口再进入。
F：小队跟随/撤出跟随。G：小队驻守/撤出驻守。小队指令和地堡进出在开战后使用。

小队最多4人：2名精英雇佣兵、2名免费招募守卫。
雇佣兵120补给签约，基础280耐久/18伤害，阵亡12秒后在指挥中心免费复活。
招募守卫基础170耐久/10伤害，保留当前生命，不占基地驻军上限，兵营会补员；阵亡后需重新招募。

每座地堡5席。守卫优先寻找本侧16米内地堡，实际走到后门入驻；玩家和小队进门时按所需席位让出守卫。
地堡正面水平射界左右各70度。入驻由地堡承伤，地堡被毁后人员从后门撤出。
东侧初始地堡向东移动，驻军使用共享寻路；两处基地入口为正面和左右侧可通行的三面坡。

45秒波间整备可查看下一波情报、雇佣、升级和维修。战斗中打开终端会暂停全局，返回后继续施工/研究/复活。
西侧地堡320补给/18秒，增援兵营400补给/22秒。工地可被攻击；主兵营和两座地堡被毁后可原址重建。
增援兵营投产后独立补员，基地驻军上限8→12，并解锁3项科技：
复合装甲240补给/16秒，全体步兵受伤降低15%；穿甲弹280补给/20秒，对重甲目标伤害提高25%。
精英雇佣兵训练300补给/22秒，仅雇佣兵耐久+80、伤害+6，复活后保留。
一次研究一项。增援兵营被毁时未完成研究暂停，重建后继续；已完成科技本局持续。

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
        assert info["CFBundleShortVersionString"] == "0.5.1", info
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
(root/"Verification/v0.5.1/releases.json").write_text(json.dumps(reports,ensure_ascii=False,indent=2))
print(json.dumps(reports,ensure_ascii=False,indent=2))

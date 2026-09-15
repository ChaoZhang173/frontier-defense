# 边境防线 · Frontier Defense v0.3

原创 3D 第三人称基地防守试玩：五波共 308 个敌人、两处入口、三人小队。使用全球版 Unity 6000.3.24f1 / URP 17.3.0，电脑键鼠操作。

## 开始试玩

- **网页：** [在线试玩](https://frontier-defense-outpost-07.fumeshroom.chatgpt.site)。
- **Mac：** 打开 `Builds/Mac/Frontier Defense.app`，或解压 `Releases/FrontierDefense-0.3-Mac.zip`。
- **Windows：** 完整解压 `Releases/FrontierDefense-0.3-Windows.zip`，打开其中的 `Frontier Defense.exe`。
- `Releases/FrontierDefense-0.3-Web.zip` 可用于其他静态 HTTP(S) 托管，不能双击 index.html 直接运行。

| 操作 | 按键 |
| --- | --- |
| 移动 / 冲刺 | WASD / Shift |
| 转动视角 / 射击 | 鼠标 / 左键 |
| 精确瞄准 / 换弹 | 右键 / R |
| 指挥中心小队终端 | 靠近指挥中心正门按 E |
| 进入 / 退出地堡 | 靠近地堡后门按 E；驻守时 E 退出 |
| 命令小队入驻地堡 | 靠近后门或驻守时按 B |
| 小队跟随 / 撤出跟随 | F |
| 小队驻守 / 撤出驻守 | G |
| 暂停 / 释放鼠标 | Esc |

小队指令和地堡进出在开战后使用；波间通过整备界面购买升级、维修或补充队员。

## v0.3 新增

- **更多敌人：** 每波 30 / 44 / 60 / 76 / 98 个，总数从 198 增至 308，约增加 56%。加密出兵节奏，保留 55 个同时存活敌人的上限，超出时等待生成。
- **四席地堡：** 玩家和三名队员可入驻。玩家用固定射孔视角继续使用步枪；队员实际走到后门再入驻，并从各自射孔自动攻击。地堡承受攻击，驻守单位受保护；地堡摧毁后从后门撤离。
- **射界与遮挡：** 正面左右各 70°、俯仰各 25°；射孔子弹仍受建筑和墙体阻挡。F/G 可召回已入驻的小队。
- **建筑经营：** 波间可缩短兵营补员间隔、提高地堡耐久和自动炮伤害，也可维修兵营与地堡。三项专属升级各两级，摧毁的建筑不能维修。
- **战场情报：** 45 秒整备期展示下一波方向、数量、敌种；战斗中显示两路敌数及防线告急状态，地堡显示耐久和驻守人数。

## 核心玩法

守住指挥中心并消灭全部五波敌人。兵营周期补员，地堡自动攻击；开局 160 补给，可花 120 雇佣第一名队员，最多三人，与兵营的八名驻军分开计数。玩家射击可标记小队集火目标。

玩家脱战五秒后自动修复，阵亡六秒后重生；队员脱战后自动修复，阵亡需要重新雇佣。指挥中心被毁则失败。波间保留原有步枪强化、协同火力和指挥中心维修。

尚未实现自由建造、机械工厂/星港、空军、驾驶载具、联机和手机触屏操作。角色与场景仍为原创几何体原型。

## 验证与继续开发

实际验证结果和设备限制见 [测试报告](Docs/测试报告.md)，版本与发布状态见 [开发进度](Docs/开发进度.md)。Windows 构建成功不等于 Windows 真机验证；手机尚未适配。

Unity 工程：`UnityProject/`，场景：`Assets/Scenes/Frontier.unity`。用全球版 Hub 添加工程并选择 6000.3.24f1。

构建入口：`Assets/Editor/FrontierBuild.cs`。程序参数 `--frontier-check=fortification|squad|mechanics|victory|afk|defeat` 和 `--frontier-output=/绝对路径/报告.json` 可运行诊断；正常启动不启用。截图使用 `--frontier-check=capture --frontier-scene=fort|upgrade|battle|terminal`。

在工程根目录运行 `python3 Tools/serve_web.py`，访问 `http://127.0.0.1:8765` 可本机复测。在线试玩无需本机服务。

发布沿用已有 Sites 站点，静态目录为 `WebSite/dist`。0.1、0.2 试玩包保留用于回退。详细机制及数值见 [v0.3 设计](Docs/v0.3设计.md)。

字体 Frontier Sans 是 Noto Sans SC 的重命名精简衍生版本，许可见 `UnityProject/Assets/ThirdParty/` 与试玩包的 Licenses。字体更新工具为 `Tools/update_font.py`。模型和音效没有使用星际争霸素材。

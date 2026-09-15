# 边境防线 · Frontier Defense v0.2

原创 3D 第三人称基地防守试玩：五波敌人、两处入口、三人小队。使用全球版 Unity 6000.3.24f1 / URP 17.3.0，电脑键鼠操作。

## 开始试玩

- **网页：** [在线试玩](https://frontier-defense-outpost-07.fumeshroom.chatgpt.site)，可直接将链接发给朋友。
- **Mac：** 打开 [Frontier Defense.app](Builds/Mac/Frontier%20Defense.app)，或使用 [Mac 试玩包](Releases/FrontierDefense-0.2-Mac.zip)。
- **Windows：** 完整解压 [Windows 试玩包](Releases/FrontierDefense-0.2-Windows.zip)，打开其中的 `Frontier Defense.exe`。已成功构建，尚未在 Windows 实机运行。
- [Web 构建包](Releases/FrontierDefense-0.2-Web.zip) 可用于其他静态 HTTP(S) 托管；不能双击 index.html 直接运行。

| 操作 | 按键 |
| --- | --- |
| 移动 / 冲刺 | WASD / Shift |
| 转动视角 / 射击 | 鼠标 / 左键 |
| 精确瞄准 | 右键 |
| 换弹 | R，弹匣打空也会自动换弹 |
| 指挥中心小队终端 | 靠近正门按 E |
| 小队跟随 | F |
| 小队驻守 | G，在玩家当前位置建立防守点 |
| 暂停 / 释放鼠标 | Esc |

## v0.2 新增

- 开局 160 补给，可立即花 120 雇佣第一名队员，最多 3 人；波间也可补员。小队与兵营的 8 名驻军分别计算。
- 队员跟随、驻守、绕建筑和通过坡道，移动时可射击；射击敌人即可标记集火目标。
- 队员各有 220 生命，脱战自动修复；阵亡后需重新雇佣。玩家阵亡不会清空小队。
- 加减速、平滑瞄准、可恢复后坐力、动态准星、枪口闪光，以及普通命中、弱点命中和击杀的不同声音与画面反馈。弱点伤害为 1.65 倍，重甲减伤仍生效。
- 玩家和小队子弹可穿过友军单位，建筑与墙体仍会挡住子弹。

守住指挥中心并消灭五波共 198 个敌人。兵营周期补员，地堡自动攻击；波间可升级步枪、防线火力或维修指挥中心。玩家脱战 5 秒后自动修复，阵亡 6 秒后重生；指挥中心被毁则失败。

尚未实现自由建造、机械工厂/星港、空军、驾驶载具、进驻地堡、联机和手机触屏操作。模型与场景仍为原创几何体原型。

## 验证

- 小队与射击专项 32 项检查、原有机制 17 项，以及完整五波胜利、无人操作失败、破坏防线失败检查通过。
- 完整五波共击杀 198 个敌人，小队确实参与战斗；自动测试不代表真人手感或最终难度评价。
- Mac 已实际渲染。Web 的浏览器操作检查见 [测试报告](Docs/测试报告.md)。Windows 仅完成构建。
- 三个平台构建均零错误。Mac / Web 零警告；Windows 18 条本机不支持光线追踪着色器编译的警告，当前前向渲染试玩不使用该功能。

## 继续开发

Unity 工程位于 `UnityProject/`，场景为 `Assets/Scenes/Frontier.unity`。用全球版 Hub 添加工程并选择 6000.3.24f1。

构建入口：`Assets/Editor/FrontierBuild.cs`。程序参数 `--frontier-check=squad|mechanics|victory|afk|defeat` 和 `--frontier-output=/绝对路径/报告.json` 可运行诊断；正常启动不启用。

在工程根目录运行 `python3 Tools/serve_web.py` 可启动本机网页复测，访问 `http://127.0.0.1:8765`；在线试玩不需要本机服务。

发布沿用已有 Sites 站点，静态目录为 `WebSite/dist`，部署记录见 [开发进度](Docs/开发进度.md)。旧的 0.1 试玩包保留用于回退。

字体 Frontier Sans 是 Noto Sans SC 的重命名精简衍生版本，许可见 `UnityProject/Assets/ThirdParty/` 与各试玩包的 Licenses。字体更新工具为 `Tools/update_font.py`。模型和音效没有使用星际争霸素材。

环境安装与清理记录：[环境准备](Docs/环境准备记录.md)、[清理记录](Docs/安装清理记录.json)。

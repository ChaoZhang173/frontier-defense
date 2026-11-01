# 小红书 v0.7 实验原型归档

本目录保留 2026-09-22 的实验源码、检查脚本和历史报告，不作为主版本发布。主兼容版源码为 [`../../XhsMiniTool/`](../../XhsMiniTool/README.md)，版本为 v0.6.4。

2026-11-01 的 GitHub 上传检查确认：

- `prototype/dist/` 保留现有实验源码，未修改玩法。
- [原始 v0.7.0 ZIP](https://github.com/ChaoZhang173/frontier-defense/releases/download/snapshot-2026-11-01/FrontierDefense-0.7.0-XHS-prototype.zip) 原封不动上传，仅在下载文件名中增加 `prototype` 标识。SHA-256 为 `782308bd82b69eea5ba4df9dd078504a068b992702e8944c684b225e1bf1a625`。
- ZIP 内的 `core.js` 与 `prototype/dist/core.js` 不同：目录中另有敌方基地分级、驻军和攻城兽出场波次等改动。其他 8 个打包文件与目录一致。没有用目录内容覆盖旧包，也没有把旧报告视为这些后续改动的验证。
- 本目录的 `release-manifest.json`、`regression-summary.json`、截图与测试输出均为历史证据，报告里的绝对路径和文件哈希仅对应当时运行；不能直接代表当前源码全部通过验证。
- `prototype/README.md` 和 `prototype/package.py` 保留从原工作目录归档时的内容，其中部分路径仍指向当时的 `XhsMiniTool/` 位置，不是当前目录可直接复用的打包入口。研究原型时请以 `prototype/dist/` 实际文件为准，独立打包并重新验证。
- 未确认小红书平台发布、iPhone 或 Android 真机表现。此次仅公开归档。

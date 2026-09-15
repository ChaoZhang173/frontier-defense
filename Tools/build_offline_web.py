#!/usr/bin/env python3
"""Package the existing Unity build as one HTML file opened directly on a Mac.

No Unity rebuild, web server, browser flags or downloaded runtime is required.
Only the Python standard library is used. The published web build is read-only.
"""

from __future__ import annotations

import argparse
import base64
import gzip
import hashlib
from html import escape, unescape
import json
from pathlib import Path
import re
import shutil
import zipfile


ROOT = Path(__file__).resolve().parents[1]
VERSION = "0.6"
ASSETS = (
    ("loader", "Web.loader.js", "application/javascript"),
    ("framework", "Web.framework.js.unityweb", "application/javascript"),
    ("data", "Web.data.unityweb", "application/octet-stream"),
    ("code", "Web.wasm.unityweb", "application/wasm"),
)

BOOT = r"""
(() => {
  'use strict';
  const canvas = document.getElementById('unity-canvas');
  const status = document.getElementById('status');
  const bar = document.getElementById('bar');
  const objectUrls = [];
  let instance;
  const yieldFrame = () => new Promise(resolve => setTimeout(resolve, 0));
  function progress(fraction, text) {
    bar.style.width = Math.round(fraction * 100) + '%';
    status.textContent = text;
  }
  function fail(reason) {
    document.getElementById('loading').style.display = 'flex';
    status.textContent = '离线游戏暂时未能启动';
    const error = document.getElementById('error');
    error.style.display = 'block';
    error.textContent = '请在支持 WebGL 2 的 Mac 桌面浏览器中直接打开完整的 HTML 文件。\n' + String(reason);
  }
  // Escape and focus changes can legitimately decline a pointer-lock request.
  const captureMouse = canvas.requestPointerLock?.bind(canvas);
  if (captureMouse) canvas.requestPointerLock = function(options) {
    const declined = error => console.warn('Mouse capture declined; click Continue in the pause menu.', error);
    try {
      const request = captureMouse(options);
      if (request && typeof request.catch === 'function') request.catch(declined);
    } catch (error) { declined(error); }
  };
  async function unpack(id, offset, weight) {
    const node = document.getElementById('offline-' + id);
    let encoded = node.textContent.trim();
    const mime = node.dataset.mime;
    const length = Number(node.dataset.bytes);
    node.remove();
    const bytes = new Uint8Array(length);
    // Decode small, four-character-aligned pieces to avoid a second full-size binary string.
    const chunkSize = 131072;
    let position = 0;
    for (let start = 0; start < encoded.length; start += chunkSize) {
      const binary = atob(encoded.slice(start, start + chunkSize));
      for (let i = 0; i < binary.length; i++) bytes[position++] = binary.charCodeAt(i);
      if ((start / chunkSize) % 16 === 0) {
        const fraction = offset + weight * Math.min(1, (start + chunkSize) / encoded.length);
        progress(fraction, '正在读取离线游戏… ' + Math.round(fraction * 100) + '%');
        await yieldFrame();
      }
    }
    if (position !== length) throw new Error('内嵌游戏文件不完整：' + id);
    encoded = '';
    const url = URL.createObjectURL(new Blob([bytes], {type: mime}));
    objectUrls.push(url);
    return url;
  }
  async function launch() {
    if (typeof WebAssembly !== 'object') throw new Error('此浏览器未启用 WebAssembly。');
    await yieldFrame();
    const urls = {};
    const parts = [
      ['loader', 0, .005], ['framework', .005, .015],
      ['data', .02, .08], ['code', .10, .30]
    ];
    for (const [name, offset, weight] of parts) urls[name] = await unpack(name, offset, weight);
    progress(.40, '正在启动离线装甲系统…');
    // A Blob belongs to this document even when the HTML itself was opened with file:.
    // Unity receives no file: resource URL and therefore performs no local-file fetch.
    await new Promise((resolve, reject) => {
      const script = document.createElement('script');
      script.src = urls.loader;
      script.onload = resolve;
      script.onerror = () => reject(new Error('内嵌启动程序无法运行。'));
      document.body.appendChild(script);
    });
    instance = await createUnityInstance(canvas, {
      dataUrl: urls.data,
      frameworkUrl: urls.framework,
      codeUrl: urls.code,
      // This project has no StreamingAssets; keep the unused base local and self-contained.
      streamingAssetsUrl: 'data:application/octet-stream;base64,',
      companyName: 'Frontier Workshop', productName: 'Frontier Defense', productVersion: '0.6.0',
      cacheControl: () => 'no-store',
      devicePixelRatio: Math.min(window.devicePixelRatio || 1, 1.5),
      showBanner: (message, type) => { if (type === 'error') fail(message); else console.warn(message); }
    }, value => progress(.40 + value * .60,
      value < .9 ? '正在装载离线前哨… ' + Math.round(40 + value * 60) + '%' : '正在进入前哨基地…'));
    document.getElementById('loading').style.display = 'none';
    canvas.focus();
    // Every resource has been loaded into Unity; release the temporary package blobs.
    objectUrls.splice(0).forEach(url => URL.revokeObjectURL(url));
  }
  document.getElementById('fullscreen').onclick = () => { if (instance) instance.SetFullscreen(1); };
  const licensePanel = document.getElementById('license-panel');
  const closeLicenses = () => { licensePanel.hidden = true; document.getElementById('license-open').focus(); };
  document.getElementById('license-open').onclick = () => {
    licensePanel.hidden = false;
    document.getElementById('license-close').focus();
  };
  document.getElementById('license-close').onclick = closeLicenses;
  document.addEventListener('keydown', event => {
    if (!licensePanel.hidden && event.key === 'Escape') {
      event.preventDefault(); event.stopPropagation(); closeLicenses();
    }
  }, true);
  canvas.addEventListener('contextmenu', event => event.preventDefault());
  launch().catch(fail);
})();
"""


README = """边境防线 v0.6 · 离线网页版

如何开始
1. 先解压这个压缩包。
2. 在 Mac 上双击“边境防线.html”，用支持 WebGL 2 的桌面浏览器打开。
3. 等待游戏完成装载，然后点击游戏内的开始按钮。

完整游戏已经包含在 HTML 文件里。首次打开也不需要网络、账号、安装程序或本地服务器。
如果双击没有进入浏览器，可以右键 HTML 文件，选择“打开方式”，再选择桌面浏览器。

分享给朋友
可以直接分享整个压缩包，也可以只分享“边境防线.html”。
朋友需要先把完整文件保存到 Mac，再在浏览器里打开；聊天软件内的文件预览不等于运行游戏。
本包面向 Mac 桌面浏览器，使用键盘和鼠标。

操作
WASD：移动；鼠标：转向与瞄准；鼠标左键：射击；R：换弹。
E：交互、招募守卫、进入地堡或在指挥中心打开升级界面。
F：小队跟随；G：小队驻守；B：小队进入附近地堡；Esc：暂停。
右下角“全屏试玩”按钮可切换全屏。退出全屏或切换窗口后，按游戏提示继续。

本版本
战车工厂自动维持最多一辆坦克，驶向中央防线；全部科技在指挥中心升级。
中央矮墙支持玩家与小队防守；敌巢附近炮塔会攻击靠近的友军，摧毁可获补给。

完整许可与署名已内嵌在 HTML 中，点击页面底部“许可与署名”即可查看。
压缩包同时保留 Licenses 文件夹中的原文。
"""


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def build(source: Path, output: Path, archive: Path, report_path: Path) -> dict:
    template = (source / "index.html").read_text(encoding="utf-8")
    if "productVersion:'0.6.0'" not in template:
        raise ValueError("The offline package is configured for the v0.6.0 game build.")
    streaming = source / "StreamingAssets"
    if streaming.exists() and any(p.is_file() for p in streaming.rglob("*")):
        raise ValueError("StreamingAssets were added; embed them before producing a single-file package.")
    source_assets = []
    payloads = []
    for key, filename, mime in ASSETS:
        packed = (source / "Build" / filename).read_bytes()
        raw = gzip.decompress(packed) if packed.startswith(b"\x1f\x8b") else packed
        if key == "code" and not raw.startswith(b"\0asm\x01\0\0\0"):
            raise ValueError("The wasm payload is not a valid uncompressed WebAssembly module.")
        if key == "data" and not raw.startswith(b"UnityWebData1.0\0"):
            raise ValueError("The data payload does not contain the expected Unity archive header.")
        digest = sha256(raw)
        encoded = base64.b64encode(raw).decode("ascii")
        payloads.append(
            f'<script type="application/octet-stream" id="offline-{key}" '
            f'data-mime="{mime}" data-bytes="{len(raw)}" data-sha256="{digest}">\n'
            f'{encoded}\n</script>'
        )
        source_assets.append({"key": key, "source": filename, "mime": mime,
                              "sourceBytes": len(packed), "bytes": len(raw),
                              "sourceSha256": sha256(packed), "sha256": digest})

    # Keep the current game frame, loading panel, controls and fullscreen button.
    start = template.index("  <script>")
    end = template.index("  </script>", start) + len("  </script>")
    shell = template[:start] + "\n".join(payloads) + "\n<script>\n" + BOOT + "</script>" + template[end:]
    shell = shell.replace("<title>边境防线 · Frontier Defense</title>", "<title>边境防线 · 离线网页版</title>")
    shell = shell.replace("正在连接前哨基地…", "正在读取离线游戏…")
    shell = shell.replace("首次加载需要下载游戏文件，请稍候。<br>电脑浏览器试玩 · 建议使用键盘和鼠标",
                          "游戏已完整保存在此文件中，无需联网。<br>Mac 桌面浏览器 · 使用键盘和鼠标")
    shell = shell.replace("边境防线 v0.6 · 战车工厂与中央防线", "边境防线 v0.6 · 离线网页版")
    license_files = sorted(path for path in (source / "Licenses").rglob("*") if path.is_file())
    license_text = "\n\n".join(
        f"===== {path.relative_to(source / 'Licenses')} =====\n\n{path.read_text(encoding='utf-8')}"
        for path in license_files
    )
    if not license_text or "SIL OPEN FONT LICENSE" not in license_text:
        raise ValueError("The standalone HTML must include the original attribution and complete font license.")
    license_markup = (
        '<section id="license-panel" role="dialog" aria-modal="true" aria-labelledby="license-title" hidden>'
        '<div id="license-heading"><h2 id="license-title">许可与署名</h2>'
        '<button id="license-close" type="button">关闭</button></div>'
        f'<pre id="offline-license-text">{escape(license_text)}</pre></section>'
    )
    shell = shell.replace('<button id="fullscreen">', '<button id="license-open" type="button">许可与署名</button><button id="fullscreen">')
    shell = shell.replace('</footer>', '</footer>' + license_markup)
    shell = shell.replace('</style>', '''
    #license-panel{position:fixed;inset:8vh 8vw;z-index:10;padding:22px;background:#102732;border:1px solid #557785;border-radius:8px;box-shadow:0 0 0 100vmax #000b}
    #license-heading{display:flex;align-items:center;justify-content:space-between;gap:20px;height:42px;margin-bottom:12px}
    #license-title{font-size:21px;margin:0}#offline-license-text{height:calc(100% - 54px);overflow:auto;white-space:pre-wrap;word-break:break-word;font:13px/1.55 system-ui,sans-serif;margin:0;color:#d5e6e9}
    </style>''')
    output.mkdir(parents=True, exist_ok=True)
    html_path = output / "边境防线.html"
    html_path.write_text(shell, encoding="utf-8")
    (output / "开始试玩.txt").write_text(README, encoding="utf-8")
    shutil.copytree(source / "Licenses", output / "Licenses", dirs_exist_ok=True)

    # Verify the generated file independently of the encoding loop: recover each embedded asset,
    # compare its complete bytes against the original build, and reject external DOM resources.
    html = html_path.read_text(encoding="utf-8")
    checks = []
    for item in source_assets:
        pattern = rf'<script type="application/octet-stream" id="offline-{item["key"]}"[^>]*>\s*([A-Za-z0-9+/=]+)\s*</script>'
        match = re.search(pattern, html)
        if match is None:
            raise ValueError(f'Missing embedded {item["key"]} payload.')
        restored = base64.b64decode(match.group(1), validate=True)
        if len(restored) != item["bytes"] or sha256(restored) != item["sha256"]:
            raise ValueError(f'Embedded {item["key"]} differs from the source build.')
        checks.append(f'{item["key"]}: complete decoded bytes match the source build')
    if re.search(r'<(?:script|link|iframe|img|source|audio|video)\b[^>]*(?:src|href)\s*=', html, re.I):
        raise ValueError("The generated HTML contains an external DOM resource.")
    embedded_licenses = re.search(r'<pre id="offline-license-text">(.*?)</pre>', html, re.S)
    if embedded_licenses is None or unescape(embedded_licenses.group(1)) != license_text:
        raise ValueError("The standalone HTML must preserve the complete, readable license text.")
    checks.append("The single HTML retains complete readable attribution and font-license text")
    checks.extend(["No external DOM resource URLs", "All Unity payload URLs are generated Blob URLs",
                   "Gzip is fully decompressed before embedding", "Unity runtime bytes are unchanged",
                   "No local server or browser setting changes", "No StreamingAssets remain outside the HTML"])
    archive.parent.mkdir(parents=True, exist_ok=True)
    folder = f"边境防线-{VERSION}-离线网页"
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as bundle:
        for path in (html_path, output / "开始试玩.txt", *sorted((output / "Licenses").rglob("*"))):
            if path.is_file():
                bundle.write(path, str(Path(folder) / path.relative_to(output)))
    with zipfile.ZipFile(archive) as bundle:
        broken = bundle.testzip()
        if broken is not None:
            raise ValueError(f"Archive integrity check failed: {broken}")
        if bundle.read(f"{folder}/边境防线.html") != html_path.read_bytes():
            raise ValueError("Archived HTML differs from the verified standalone file.")
    checks.append("ZIP integrity and contained HTML match the verified standalone file")
    report = {"version": "0.6.0", "source": str(source), "html": str(html_path),
              "archive": str(archive), "htmlBytes": html_path.stat().st_size,
              "archiveBytes": archive.stat().st_size, "htmlSha256": sha256(html_path.read_bytes()),
              "archiveSha256": sha256(archive.read_bytes()), "assets": source_assets,
              "staticChecksPassed": len(checks), "checks": checks,
              "browserVerification": "This tool performs no browser execution. Separate acceptance evidence, when present, is in offline-web-browser.json and must be matched to its artifact hash."}
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return report


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=ROOT / "Builds/Web")
    parser.add_argument("--output", type=Path, default=ROOT / "Builds/OfflineWeb")
    parser.add_argument("--archive", type=Path, default=ROOT / f"Releases/FrontierDefense-{VERSION}-OfflineWeb.zip")
    parser.add_argument("--report", type=Path, default=ROOT / f"Verification/v{VERSION}/offline-web-static.json")
    args = parser.parse_args()
    result = build(args.source.resolve(), args.output.resolve(), args.archive.resolve(), args.report.resolve())
    print(json.dumps({key: value for key, value in result.items() if key not in ("assets", "checks")},
                     ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()

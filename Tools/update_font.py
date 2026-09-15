"""Regenerate the renamed OFL font subset from the UI's current characters.

Run with fontTools installed. The upstream font is cached outside Assets at
UnityProject/Library/FontSource/NotoSansSC.ttf so it is not shipped to players.
"""
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools import subset

root = Path(__file__).resolve().parents[1] / "UnityProject"
characters = set(chr(i) for i in range(32, 127))
for path in (root / "Assets").rglob("*"):
    if path.suffix in {".cs", ".html"}:
        characters.update(path.read_text())
font = TTFont(root / "Library/FontSource/NotoSansSC.ttf")
instantiateVariableFont(font, {"wght": 450}, inplace=True)
for record in font["name"].names:
    if record.nameID in {1, 3, 4, 6, 16, 17}:
        name = "FrontierSans" if record.nameID == 6 else "Regular" if record.nameID == 17 else "Frontier Sans"
        record.string = name.encode(record.getEncoding(), errors="replace")
options = subset.Options()
options.name_IDs = ["*"]
options.name_legacy = True
options.name_languages = ["*"]
subsetter = subset.Subsetter(options=options)
subsetter.populate(text="".join(characters))
subsetter.subset(font)
# CJK glyph bounds occupy 880/-120; these metrics avoid extra blank leading in IMGUI.
font["hhea"].ascent, font["hhea"].descent, font["hhea"].lineGap = 880, -120, 0
font["OS/2"].sTypoAscender, font["OS/2"].sTypoDescender, font["OS/2"].sTypoLineGap = 880, -120, 0
font["OS/2"].usWinAscent, font["OS/2"].usWinDescent = 880, 120
out = root / "Assets/Resources/Fonts/FrontierSans.ttf"
font.save(out)
print(f"Font updated: {len(characters)} characters, {out.stat().st_size} bytes")

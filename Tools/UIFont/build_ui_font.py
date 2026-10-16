"""Builds Canopy UI Sans: Liberation Sans plus symbol blocks from Noto Sans Symbols 2 and
Noto Sans Math, merged into ONE font per style for the node editor's IMGUI skin.

Why one font: Unity's legacy Font fallback (the importer's fallback font references /
Font.m_FallbackFonts) is ignored by built players, which fall back to OS fonts instead
(MS Gothic on Windows), so any glyph missing from the primary font rendered as a box in
builds even though the editor (with its much broader fallback chain) drew it fine.
Merging makes the primary font self-sufficient: no fallback, same result everywhere.

Usage:  pip install fonttools  &&  python Tools/UIFont/build_ui_font.py
Writes Assets/Plugins/Node_Editor/Resources/Fonts/CanopyUISans-{Regular,Bold}.ttf and then
scans the project's string literals for non-ASCII characters the font doesn't cover.

Licensing: all sources are SIL OFL 1.1 (licenses in sources/). Liberation reserves the name
"Liberation", so the merged derivative must use a different family name.
"""
import pathlib
import re

from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont

HERE = pathlib.Path(__file__).resolve().parent
SOURCES = HERE / "sources"
PROJECT = HERE.parents[1]
OUT_DIR = PROJECT / "Assets" / "Plugins" / "Node_Editor" / "Resources" / "Fonts"

FAMILY = "Canopy UI Sans"
STYLES = {"Regular": "LiberationSans-Regular.ttf", "Bold": "LiberationSans-Bold.ttf"}
# Earlier donors win when both have a glyph. Symbol fonts have one weight; Bold reuses them.
DONORS = ["NotoSansSymbols2-Regular.ttf", "NotoSansMath-Regular.ttf"]
# Unicode blocks pulled from the donors (only codepoints the base font lacks are added)
BLOCKS = [
    (0x2190, 0x21FF),  # Arrows                      ↶ ↺
    (0x2200, 0x22FF),  # Mathematical Operators      ∞ ≈
    (0x2300, 0x23FF),  # Miscellaneous Technical     ⏮ ⏯ ⏱
    (0x2500, 0x257F),  # Box Drawing
    (0x2580, 0x259F),  # Block Elements
    (0x25A0, 0x25FF),  # Geometric Shapes            ▶ ◀ ● ■
    (0x2600, 0x26FF),  # Miscellaneous Symbols       ⚠ ♪
    (0x2700, 0x27BF),  # Dingbats                    ❚
    (0x27F0, 0x27FF),  # Supplemental Arrows-A       ⟲ ⟳
    (0x2900, 0x297F),  # Supplemental Arrows-B
    (0x2B00, 0x2BFF),  # Miscellaneous Symbols and Arrows
]
# Per-glyph device-metric tables sized to the original glyph count; stale after merging
DROP_TABLES = ["hdmx", "LTSH", "VDMX"]
SCAN_ROOTS = [PROJECT / "Assets" / "Scripts", PROJECT / "Assets" / "Plugins" / "Node_Editor"]


def wanted_codepoints():
    cps = set()
    for lo, hi in BLOCKS:
        cps.update(range(lo, hi + 1))
    return cps


def copy_glyphs(base, donor, wanted):
    """Copies donor glyphs for wanted codepoints the base lacks: outlines decomposed and
    scaled to the base's units-per-em, unhinted. Returns {codepoint: new glyph name}."""
    scale = base["head"].unitsPerEm / donor["head"].unitsPerEm
    base_cmap = base.getBestCmap()
    donor_cmap = donor.getBestCmap()
    donor_glyphs = donor.getGlyphSet()
    glyf, hmtx = base["glyf"], base["hmtx"]
    order = base.getGlyphOrder()
    added = {}
    for cp in sorted(wanted):
        if cp in base_cmap or cp not in donor_cmap:
            continue
        src = donor_cmap[cp]
        recording = DecomposingRecordingPen(donor_glyphs)
        donor_glyphs[src].draw(recording)
        pen = TTGlyphPen(None)
        recording.replay(TransformPen(pen, (scale, 0, 0, scale, 0, 0)))
        glyph = pen.glyph()
        name = f"uni{cp:04X}" if cp <= 0xFFFF else f"u{cp:05X}"
        glyf[name] = glyph
        if name not in order:
            order.append(name)
        glyph.recalcBounds(glyf)
        lsb = getattr(glyph, "xMin", 0)
        hmtx[name] = (round(donor["hmtx"][src][0] * scale), lsb)
        added[cp] = name
    base.setGlyphOrder(order)
    for table in base["cmap"].tables:
        if table.isUnicode():
            for cp, name in added.items():
                if cp <= 0xFFFF or table.format in (12, 13):  # format 4 is BMP-only
                    table.cmap[cp] = name
    return added


def rename(font, style, donor_copyrights):
    names = font["name"]
    copyright_notice = names.getDebugName(0) or ""
    version = names.getDebugName(5) or "Version 1.0"
    ps_name = f"CanopyUISans-{style}"
    for name_id in (0, 1, 2, 3, 4, 6, 10, 16, 17, 21, 22):
        names.removeNames(nameID=name_id)
    values = {
        # OFL: keep every source's copyright notice
        0: "; ".join([copyright_notice] + donor_copyrights),
        1: FAMILY,
        2: style,
        3: f"{ps_name};{version}",
        4: f"{FAMILY} {style}",
        6: ps_name,
        10: "Liberation Sans with symbol glyphs from Noto Sans Symbols 2 and Noto Sans Math, "
            "merged for Lightweaver's node editor so no font fallback is needed. "
            "Not an official Liberation or Noto release.",
    }
    for name_id, value in values.items():
        names.setName(value, name_id, 3, 1, 0x409)  # Windows, Unicode BMP, en-US
        names.setName(value, name_id, 1, 0, 0)      # Macintosh, Roman, English


def scan_project_strings():
    """Every non-ASCII char inside a string literal, with first location."""
    string = re.compile(r'"((?:[^"\\]|\\.)*)"')
    found = {}
    for root in SCAN_ROOTS:
        for path in root.rglob("*.cs"):
            text = path.read_text(encoding="utf-8", errors="replace")
            for lineno, line in enumerate(text.splitlines(), 1):
                for literal in string.findall(line.split("//")[0]):
                    for ch in literal:
                        if ord(ch) > 127 and ch not in found:
                            found[ch] = f"{path.relative_to(PROJECT)}:{lineno}"
    return found


def main():
    wanted = wanted_codepoints()
    donors = [TTFont(SOURCES / d) for d in DONORS]
    donor_copyrights = [d["name"].getDebugName(0) for d in donors if d["name"].getDebugName(0)]
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    covered = None
    for style, source in STYLES.items():
        base = TTFont(SOURCES / source)
        for tag in DROP_TABLES:
            if tag in base:
                del base[tag]
        total = 0
        for donor_name, donor in zip(DONORS, donors):
            added = copy_glyphs(base, donor, wanted)
            total += len(added)
            print(f"{style}: +{len(added)} glyphs from {donor_name}")
        rename(base, style, donor_copyrights)
        out = OUT_DIR / f"CanopyUISans-{style}.ttf"
        base.save(out)
        cps = set(TTFont(out).getBestCmap())
        covered = cps if covered is None else covered & cps
        print(f"{style}: {total} glyphs added -> {out.relative_to(PROJECT)} ({out.stat().st_size // 1024} KB)")

    missing = {ch: loc for ch, loc in scan_project_strings().items() if ord(ch) not in covered}
    if missing:
        print("\nWARNING: string literals use characters this font can't draw "
              "(fine for logs; a box if shown in node UI):")
        for ch, loc in sorted(missing.items()):
            print(f"  U+{ord(ch):04X} {ch}  first at {loc}")
    else:
        print("\nAll non-ASCII characters in project string literals are covered.")


if __name__ == "__main__":
    main()

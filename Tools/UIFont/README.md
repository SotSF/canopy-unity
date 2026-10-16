# Canopy UI Sans

The node editor's IMGUI font: Liberation Sans with the symbol blocks of Noto Sans Symbols 2
and Noto Sans Math merged in, so every glyph lives in one font file.

It's merged rather than using Unity's font fallback because built players ignore a font's
fallback references and fall back to OS fonts (MS Gothic on Windows). Symbols such as ⏮ ⚠ ⟲
rendered fine in the editor, which has a much broader fallback chain, but showed as boxes in
builds.

## Rebuild

```
pip install fonttools
python Tools/UIFont/build_ui_font.py
```

The script writes `Assets/Plugins/Node_Editor/Resources/Fonts/CanopyUISans-{Regular,Bold}.ttf`.
It then scans the project's string literals and lists any non-ASCII character the font can't
draw. Run it after adding a new symbol to node UI. If the symbol is reported as missing, add its
Unicode block to `BLOCKS`. Emoji outside the Basic Multilingual Plane (e.g. 🆒) can't be drawn
by IMGUI at all, so use text instead.

## Licensing

All three sources are SIL OFL 1.1, and their licenses are in `sources/` and next to the shipped
fonts. Liberation reserves the font name "Liberation", which is why the merged derivative has a
different family name.

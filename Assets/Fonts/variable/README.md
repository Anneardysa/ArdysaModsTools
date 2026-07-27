# Variable font source

Build-time source for `Assets/Html/fonts.css`. **Not shipped and not installed** — the bytes reach
the app embedded in `fonts.css`, so this file has no csproj `Content` entry and never lands in the
build output.

It deliberately sits in a subfolder: `Installer/Services/FontInstaller.cs` globs
`Assets/Fonts/*.ttf` non-recursively to register fonts in `%WINDIR%\Fonts`, and this font is not
meant to be installed system-wide. Keeping it one level down leaves that glob matching exactly the
same four static faces it always did.

## JetBrainsMono-Variable.ttf

- **Family**: JetBrains Mono, version 2.304
- **Upstream filename**: `JetBrainsMono[wght].ttf` (renamed — the brackets are awkward in MSBuild
  globs and URLs)
- **Axis**: `wght` 100–800, default 400
- **SHA-256**: `662A196D58F1183BF2D77428B6D5283FE3F45161AB021BEA4036BC98E5CAC016`
- **License**: SIL Open Font License 1.1
- **Upstream**: https://github.com/JetBrains/JetBrainsMono

## Why variable rather than the static faces

The UI uses weights 400, 500, 600 and 700. The old Google Fonts request asked for
`wght@100..800`, so all four rendered as designed. Bundling only the static Regular + Bold that
`Assets/Fonts/` already carries would snap weight 600 — the second most common weight in the UI —
up to full bold, which is visible. One variable face reproduces the original rendering exactly and
is smaller than two static faces (396 KB of base64 vs 720 KB).

## Regenerating fonts.css

`Assets/Html/fonts.css` is generated, not hand-edited. If this font is updated, rebuild it as a
single `@font-face` with `font-weight: 100 800` and the TTF as a base64 `data:` URI — no `local()`
source (see the comment at the top of `fonts.css` for why).

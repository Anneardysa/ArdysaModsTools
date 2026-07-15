# AMT Design System — Monochrome Terminal

The shipped UI style, extracted from `Assets/Html/main_shell.html` (the canonical
reference implementation). It mirrors ardysamods.my.id: black/white terminal
aesthetic, JetBrains Mono everywhere, sharp corners, cut-corner notches, corner
brackets. Every new WebView2 panel must follow this document.

**Hard rules**

- Monochrome only. The single accent is **white**. Never reintroduce green (the old
  Voltagent theme is dead). The only non-monochrome colors allowed are the semantic
  exceptions listed at the bottom.
- `border-radius: 0` everywhere (dots/LEDs are the only circles).
- One typeface: JetBrains Mono, for both UI text and code.
- Dark only — there is no light theme.

---

## 1. Design tokens

Copy this `:root` block verbatim into every new panel's HTML.

```css
:root {
   /* surfaces */
   --canvas: #000000;
   --panel: rgba(10, 10, 10, 0.9);
   --panel-2: #050505;
   --panel-hover: rgba(20, 20, 20, 0.95);
   --hairline: #222222;
   --hairline-strong: #333333;
   --dashed: rgba(136, 136, 136, 0.4);

   /* brand — single white accent */
   --primary: #ffffff;
   --primary-soft: #ffffff;
   --primary-deep: #ffffff;
   --on-primary: #000000;
   --primary-glow: rgba(255, 255, 255, 0.16);
   --accent-dim: rgba(255, 255, 255, 0.1);

   /* cautionary yellow accent (confirm --warn variant only) */
   --warn-accent: #f5c451;
   --warn-glow: rgba(245, 196, 81, 0.22);

   /* text */
   --ink: #ffffff;          /* headings, active text */
   --ink-strong: #ffffff;
   --body: #888888;         /* body copy, secondary */
   --mute: #444444;         /* labels, disabled, decorations */

   /* semantic (in-product cues only) */
   --success: #ffffff;
   --warning: #888888;
   --error: #ffffff;
   --info: #888888;

   /* type */
   --sans: "JetBrains Mono", SFMono-Regular, Menlo, Consolas, monospace;
   --mono: "JetBrains Mono", SFMono-Regular, Menlo, Consolas, monospace;

   /* radius — sharp-cornered, always */
   --r-sm: 0px;
   --r-md: 0px;
   --r-pill: 0px;
   --clip: 12px;            /* signature cut-corner notch size */

   /* motion */
   --transition: all 0.18s ease;

   /* spacing scale */
   --sp-1: 4px;  --sp-2: 8px;  --sp-3: 12px;
   --sp-4: 16px; --sp-5: 20px; --sp-6: 24px;
}
```

CJK: when a Chinese locale is active, `i18n.js` sets `data-cjk` on `<html>`;
override `--sans`/`--mono` to append `"Microsoft YaHei", "Microsoft JhengHei",
"PingFang SC", "PingFang TC", "Noto Sans CJK SC"` fallbacks. Latin glyphs still
resolve to JetBrains Mono first.

## 2. Canvas & backdrop

- Body: `background: var(--canvas)` plus a 40px × 40px grid of 1px
  `rgba(255,255,255,0.03)` lines (two `linear-gradient` background-images).
- Animated wave backdrop (`.bgfx`): fixed, pointer-events none, a 200%-wide inline
  SVG sine path at `opacity: 0.05` scrolling left over 28s (`infiniteWave`).
  Disabled under `@media (prefers-reduced-motion: reduce)`.
- Window chrome: `border: 1px solid var(--hairline)` on `<body>`, custom title bar
  (44px, `-webkit-app-region: drag`, `rgba(0,0,0,0.6)` + `backdrop-filter: blur(10px)`).

## 3. Signature motifs

These are what make a panel read as AMT — use at least one on any new surface.

**Cut-corner notch** (`.notch`) — top-right corner of a panel is "cut": an absolute
`var(--clip)` square of `--canvas` with left/bottom `--hairline-strong` borders.
Used on the sidebar and major cards.

**Corner brackets** — `::before` top-left + `::after` bottom-right, 8–9px squares
drawn with two 1px `--ink` borders at `opacity: 0.55–0.6`; on hover they grow to
12–13px and go opaque. Used on promo cards, choices, toasts (toast uses TR/BL).

**Terminal punctuation** — decorative text in `--mute`:
- brand name prefixed with `// ` (`.b-name::before`)
- panel titles wrapped in `[` `]` (`.c-title::before/::after`)
- an `! IMPORTANT` mono kicker on callouts (`.confirm-note::before`)

**LED dot** — 6px circle, `background: var(--primary)`, `box-shadow: 0 0 6px var(--primary)`.
Marks a live/active panel title (console head, modal title).

**Left accent bar** — `border-left: 2px solid var(--primary)` marks emphasis:
toasts, callout notes, nav-btn hover (`inset 2px 0 0` box-shadow).

## 4. Typography

Everything uppercase-tracked or small; no font-size above ~15px in the shell.

| Role | Size | Weight | Extras |
|---|---|---|---|
| Card/section title | 14–15px | 700 | uppercase, letter-spacing 1–1.5px, `--ink` |
| Group/panel label | 11px | 600–700 | uppercase, letter-spacing 2px, `--mute` or `--body` |
| Button label | 11–12px | 600 | letter-spacing 0.5–1px |
| Body copy | 11–12px | 400–500 | line-height 1.55–1.7, `--body` |
| Badge / version / counter | 9–10px | 600–700 | mono, letter-spacing 0.5–1.5px, `--mute` |
| Console/log text | 12px | 400 | mono, line-height 1.65 |

Group labels get a hairline rule filling the remaining width
(`.group-label::after { flex:1; height:1px; background:var(--hairline) }`).

## 5. Components

### Buttons
- **Nav button** (`.nav-btn`): `--panel-2` bg, 1px `--hairline` border, `--body` text,
  9px × 12px padding. Hover: `--panel-hover` bg, `--ink-strong` text,
  `--hairline-strong` border, white 2px inset left bar; icon turns `--primary`.
  Active: `translateY(1px)`. Disabled: `opacity: 0.4; pointer-events: none`.
- **Primary button** (`.ob-btn.primary`): white bg, black text
  (`--primary`/`--on-primary`), hover adds `box-shadow: 0 0 16px var(--primary-glow)`.
- **Ghost button** (`.ob-btn.ghost`): transparent, `--body` text; hover
  `--accent-dim` bg + white border.
- **Icon button** (`.win-btn`): 34×30, transparent; hover `--accent-dim` bg +
  hairline border. Close button hover is the one red exception:
  `rgba(248,81,73,0.85)` bg.

### Cards
- **Panel** (sidebar, console): `--panel` bg, 1px `--hairline-strong` border.
- **Promo/action card**: `--panel-2` bg, corner brackets, dark gradient veil over
  imagery, uppercase title + `--body` subtitle. Hover: `--primary` border,
  `0 0 20px var(--primary-glow)` glow, bg image `scale(1.05)`, arrow slides 3px.
- **Choice card** (modal options): same brackets; hover lifts `translateY(-2px)`.
- **Status card**: `--panel-2`, hairline border, 9px status dot + ellipsized text.

### Console / log
`--panel` surface, header bar with `[ TITLE ]` + LED. Log lines are a 2px colored
left bar + message: default `--mute`/`--body`, success white, error
`#f85149` bar / `#ff9b96` text, warning `#e8c878`, progress `#8cc2ff`. Notices get
an amber-tinted box (`rgba(232,200,120,0.07)` bg).

### Modal
Overlay: `rgba(0,0,0,0.72)` + `blur(3px)`, `align-items: safe center` +
`overflow-y: auto` (footer must stay reachable on small screens — never plain
`center`). Modal: 420–460px, `--panel-2`, `--hairline-strong` border,
`0 24px 70px` shadow, `modalPop` entrance (12px rise + 0.98 scale, 0.22s).
Confirm variant adds `border-top: 2px solid var(--primary)`, a 52px glowing icon
box, centered text, and an action row of `.ob-btn`s. `--warn` variant swaps the
accent to `--warn-accent` (LED, icon, note bar, OK button).

### Toast
Bottom-right host (`right/bottom: 18px`), 280–360px cards: `--panel-2`, 2px white
left bar (error `#f85149`, info `--body`), TR/BL corner brackets, uppercase title
+ `--body` message, slide-in from the right (`toastIn` 0.28s). Use toasts instead
of tray balloons; use the shell confirm instead of native MessageBox.

### Menu (dropdown)
Fixed, `--panel-2`, hairline-strong border, heavy shadow
(`0 20px 60px rgba(0,0,0,0.7)` + faint inset ring), items hover `--accent-dim` +
`--primary` text, 1px `--hairline` separators, 0.14s drop-in.

### Onboarding spotlight
Transparent rect with `0 0 0 100vmax rgba(0,0,0,0.74)` box-shadow dims everything
around the target; corner brackets on the spot, pulsing glow, 300px card with
2px white top border. (DOM-native — GDI overlays can't capture WebView2.)

### Scrollbar
8px, transparent track, `--hairline` thumb (`--hairline-strong` on hover).

## 6. Motion

- Default transition: `var(--transition)` (0.18s ease). Menus/toasts/modals get
  bespoke 0.14–0.28s entrances; carousel slides 0.5s `cubic-bezier(0.4,0,0.2,1)`.
- Press feedback is always `transform: translateY(1px)`.
- Emphasis pulse: 2.4s `box-shadow` ring in `--primary-glow` (`.nav-btn.highlight`).
- Respect `prefers-reduced-motion: reduce` for looping/decorative animation
  (wave backdrop); one-shot entrances may stay.

## 7. Color exceptions (exhaustive)

Monochrome everywhere, except these functional cues:

| Use | Color |
|---|---|
| Destructive/error (close hover, error toast/log/border) | `#f85149` bg / `#ff8983`–`#ff9b96` text |
| Success confirmation banner ("path found") | `rgba(63,185,80,…)` / `#56d364` |
| Warning/notice (log warnings, patch-update state) | `#e8c878` / `rgba(227,179,65,0.55)` |
| Progress log text | `#8cc2ff` |
| Cautionary confirm variant | `--warn-accent` `#f5c451` |

Anything else — including "success" states in chrome — stays white/gray.

## 8. New panel checklist

1. Copy the `:root` tokens + reset + body grid background + scrollbar CSS.
2. Load JetBrains Mono (same Google Fonts URL as main_shell) + the CJK override.
3. `user-select: none`, `overflow: hidden` on body; scrollable regions opt in.
4. Use `data-i18n` keys, never hardcoded strings (i18n.js).
5. Use the shell toast/confirm patterns, not native MessageBox/tray.
6. At least one signature motif (notch, brackets, `[ ]` title, LED).
7. Small screens: modal overlays use `safe center` + scroll; content columns get
   `min-height: 0; overflow-y: auto`.

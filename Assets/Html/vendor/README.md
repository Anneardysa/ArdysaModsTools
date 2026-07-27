# Vendored web assets

Third-party files served to WebView2 from disk instead of a CDN, so the UI renders correctly with
no network access.

## tailwind.min.js

- **What**: Tailwind CSS Play CDN build, v3.4.17
- **Source**: `https://cdn.tailwindcss.com`
- **SHA-256**: `176E894661AA9CDC9A5CBA6C720044CBBF7B8BD80D1C9A142A7C24B1B6C50D15`
- **License**: MIT (Tailwind Labs)
- **Used by**: `Assets/Html/dota2_performance.html` (Performance Tweak page)

This is the runtime JIT build, kept as-is rather than swapped for a precompiled stylesheet.

A precompiled sheet would be far smaller (~10 KB vs 400 KB) and is technically viable: Tailwind's
CLI scans raw file text, so it picks up the utilities this page assigns from JavaScript — `renderCvars`
sets `className` directly, and the cvar rows use `border-[color:var(--divider)]`, which never appears
in a `class=` attribute but does appear as a literal string in the source. The only composed class
strings on the page (`` `launch-tag ${…}` ``, `` `toast ${kind} show` ``) build **custom** classes
defined in the page's own `<style>`, not Tailwind utilities.

It is a **maintenance** trade, not a correctness one. A generated stylesheet has to be regenerated
whenever this page's markup changes, and nothing in the build or CI enforces that — so a future edit
adding a utility class would silently render unstyled, with no test able to catch it. The JIT build
compiles whatever the page actually uses, needs no Node toolchain in the build, and keeps the inline
`tailwind.config` authoritative. 400 KB on local disk is irrelevant for a desktop app.

If a build step ever regenerates and diffs this CSS in CI, switching to a precompiled sheet becomes
the better option.

### Updating

Re-download, update the version and hash above, then confirm the Performance Tweak page still
renders styled with the network disabled:

```powershell
Invoke-WebRequest -Uri "https://cdn.tailwindcss.com" -OutFile Assets/Html/vendor/tailwind.min.js -UseBasicParsing
Get-FileHash Assets/Html/vendor/tailwind.min.js -Algorithm SHA256
```

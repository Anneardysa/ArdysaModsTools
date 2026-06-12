# ADR-0005: WebView2 Hybrid UI Architecture

**Date:** 2026-02-10
**Status:** Accepted
**Deciders:** @Anneardysa

## Problem Statement

The application needs modern, visually rich UI components (hero gallery grid with images, animated progress overlays) that are difficult and time-consuming to build with WinForms native controls. Standard WinForms controls offer limited styling, no CSS-based theming, and poor animation support. However, the core application shell (main form, menus, system tray) works well with WinForms and doesn't need to be replaced.

## Decision Drivers

- **Visual quality** — Hero gallery and progress overlays require modern styling (gradients, animations, responsive grids)
- **Development speed** — HTML/CSS/JS development for rich UI is faster than GDI+ custom controls
- **Maintainability** — Theme changes via CSS are trivial; WinForms custom painting requires recompilation
- **WinForms investment** — The main shell, dialogs, and system tray integration work well and shouldn't be rewritten
- **Startup performance** — WebView2 initialization adds latency; it should only be used where its benefits outweigh the cost

## Considered Alternatives

### Alternative 1: WebView2 Hybrid (WinForms Shell + WebView2 Components) — Chosen

Use WinForms for the main application shell and WebView2 for visually rich components. Communicate between C# and JavaScript using WebView2's message-passing API.

- ✅ Good, because rich components get modern CSS styling (Tailwind CSS, gradients, animations)
- ✅ Good, because the WinForms shell keeps native OS integration (system tray, file dialogs, admin elevation)
- ✅ Good, because HTML/CSS iteration speed is much faster than GDI+ custom painting
- ✅ Good, because themes can be changed with CSS variables, no recompilation needed
- ❌ Bad, because WebView2 adds ~50-100ms initialization time per component
- ❌ Bad, because debugging spans two runtime environments (C# + browser DevTools)
- ❌ Bad, because WebView2 runtime must be installed on user's machine (or shipped with the app)

### Alternative 2: Pure WinForms with Custom Controls

Build all UI components using WinForms `UserControl` and `OnPaint` with GDI+.

- ✅ Good, because no additional runtime dependency (WebView2)
- ✅ Good, because single debugging environment
- ❌ Bad, because building a responsive image grid with GDI+ requires hundreds of lines of custom painting code
- ❌ Bad, because animations require manual `Timer`-based frame updates
- ❌ Bad, because styling changes require code changes and recompilation

### Alternative 3: Full Electron / Web Application

Rewrite the entire application as a web application in Electron or similar framework.

- ✅ Good, because the entire UI benefits from web technologies
- ❌ Bad, because it requires rewriting the entire application (~15K+ lines of C# logic)
- ❌ Bad, because Electron apps consume 200-500MB RAM baseline
- ❌ Bad, because native Windows integration (VPK tools, file system, admin elevation) becomes much harder
- ❌ Bad, because the team's expertise is in C#, not full-stack JavaScript

### Alternative 4: WPF Migration

Migrate the entire application to WPF for XAML-based rich UI.

- ✅ Good, because WPF has excellent styling, animations, and data binding
- ✅ Good, because it stays in the .NET ecosystem
- ❌ Bad, because full migration is a massive effort equivalent to a rewrite
- ❌ Bad, because WPF has a steep learning curve (XAML, bindings, dependency properties)
- ❌ Bad, because WPF applications have slower startup times than WinForms

## Decision

We will use a **hybrid architecture**: WinForms for the main shell and WebView2 with Tailwind CSS for visually rich components.

### Components Using WebView2

| Component            | HTML File                              | Purpose                                                |
| -------------------- | -------------------------------------- | ------------------------------------------------------ |
| `HeroGalleryForm`    | `Assets/Html/hero_gallery.html`        | Hero selection grid with images, search, and favorites |
| `ProgressOverlay`    | `Assets/Html/progress.html`            | Animated progress bar with status updates              |
| `MiscFormWebView`    | `Assets/Html/misc_form.html`           | Miscellaneous asset selector                           |
| `SettingsFormWebView`| `Assets/Html/settings_form.html`       | Application settings with themed UI                    |
| `Dota2PerformanceView`| `Assets/Html/dota2_performance.html`  | Dota 2 performance tweaking and autoexec editor        |
| `VerifyFilesDialog`  | `Assets/Html/verify_files.html`        | Integrity check and hash verification results          |
| `StatusDetailsDialog`| `Assets/Html/status_details.html`      | Detailed status report of patched files and VPK checks |
| `SupportDialog`      | `Assets/Html/support.html`            | Social links, PayPal integration, and developer info   |
| `ModsPackUpdateDialog`| `Assets/Html/modspack_update.html`    | Modern notification dialog for ModsPack asset updates  |

### C# ↔ JavaScript Interop Pattern

```csharp
// JavaScript → C# (structured messages via postMessage)
// In JS: window.chrome.webview.postMessage({ type: "generate", data: { ... } });

private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
{
    var message = JsonSerializer.Deserialize<JsonElement>(e.WebMessageAsJson);
    var type = message.GetProperty("type").GetString();

    switch (type)
    {
        case "generate": await HandleGenerateAsync(); break;
        case "close":    this.Close(); break;
        case "startDrag": NativeDragWindow(); break;
    }
}

// C# → JavaScript (script execution for data injection)
await _webView.CoreWebView2.ExecuteScriptAsync("loadHeroes(jsonData)");
await _webView.CoreWebView2.ExecuteScriptAsync("showAlert('Title', 'Msg', 'success')");
```

### Borderless Window Pattern

WebView2 forms use borderless windows with native drag support:

```csharp
[DllImport("user32.dll")]
private static extern bool ReleaseCapture();

[DllImport("user32.dll")]
private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

// Triggered by JS: window.chrome.webview.postMessage({ type: "startDrag" })
private void NativeDragWindow()
{
    ReleaseCapture();
    SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
}
```

## Consequences

### Positive

- ✅ Rich, modern UI components with CSS animations and responsive grids
- ✅ Development speed for UI features is 3-5x faster than GDI+ equivalents
- ✅ Theme changes apply instantly via CSS — no recompilation
- ✅ Native OS integration preserved (system tray, admin, file system)

### Negative

- ❌ WebView2 runtime dependency (~150MB) must be available on user's machine
- ❌ Debugging spans C# and browser DevTools environments
- ❌ Each WebView2 component adds ~50-100ms initialization time
- ❌ C#↔JS interop is string-based (JSON serialization overhead)

### Metrics

| Metric                        | WinForms Only           | Hybrid            |
| ----------------------------- | ----------------------- | ----------------- |
| Hero gallery development time | ~2 weeks estimated      | 3 days            |
| UI theme change effort        | Code change + recompile | CSS edit only     |
| Additional runtime dependency | None                    | WebView2 (~150MB) |

## Related

- [ADR-0001: Refactor MainForm to MVP](./0001-refactor-mainform-mvp.md) — MVP pattern applies to WebView2 forms too
- `UI/Forms/HeroGalleryForm.cs` — primary WebView2 form
- `Assets/Html/hero_gallery.html` — gallery HTML/CSS/JS
- `Assets/Html/progress.html` — progress overlay
- `Assets/Html/settings_form.html` — settings UI template

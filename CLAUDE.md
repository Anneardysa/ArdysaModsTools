# CLAUDE.md — ArdysaModsTools (AMT)

Windows desktop app (.NET 8.0, C#) for managing cosmetic Dota 2 mods. Windows 10/11 x64 only.
UI is WinForms + WebView2 hybrid. Architecture is **MVP, strictly enforced**.

AMT directly rewrites Dota 2 game files. A mistake can corrupt VPKs or leave the install
broken. Correctness and rollback safety outrank speed on every file-touching path.

## Build & test

Run from the repo root. **The app must be closed before building** (it locks the output).

```powershell
dotnet build ArdysaModsTools.csproj -c Debug          # local dev
dotnet build ArdysaModsTools.csproj -c Release        # pre-commit gate
dotnet test  Tests/ArdysaModsTools.Tests.csproj --configuration Release
```

If Debug build + Release test both pass locally, CI (`.github/workflows/ci.yml`) passes.
Deeper guide: `.agent/skills/build-and-test/SKILL.md`.

## Architecture rules (non-negotiable)

- **Layering**: `Form` (View) = UI event wiring only, zero business logic → `Presenter`
  (`UI/Presenters/`) = UI logic, delegates to services → `Service`
  (`Core/Services/{Category}/`) = business logic, always behind an interface.
- **New service sequence**: interface in `Core/Interfaces/I{Name}Service.cs` → implementation →
  DI registration in `Core/DependencyInjection/ServiceCollectionExtensions.cs` → tests in
  `Tests/`. Missing any step = incomplete. (`.agent/skills/add-service/SKILL.md`)
- **DI only**: constructor injection. Never `new ConcreteService()`, never service-locator
  (`IServiceProvider.GetService()`) in business logic. Singletons for stateful (config, cache,
  PatchWatcher); transient for stateless.
- **Tests are mandatory**: every new public service method gets a test (happy path + one
  error/edge case). xUnit + Moq — follow existing test files.

## File-safety & subsystem rules

- **All writes to the Dota 2 folder go through `FileTransactionService`** (`Core/Services/FileTransaction/`):
  extract to temp → verify SHA-256 → atomic swap → rollback on any failure. Never `File.Copy`/`File.Move`
  into the game dir directly.
- **Logging**: `ILogService` only. Never `Console.WriteLine`/`Debug.Print`/`Trace`. In user-facing
  `log()`/`Fail()` strings call `items_game.txt` the **"package"** (keep the real filename in code/paths/diagnostics).
- **CDN**: URLs never hardcoded (use `Core/Constants/`). Fallback order is fixed by ADR-0003:
  R2 → jsDelivr → GitHub Raw → GFW proxy, selected by `SmartCdnSelector`. Don't bypass or duplicate it.
  SHA-256 verify after every download before swap.
- **Security**: `Core/Services/Security/` (anti-tamper / integrity, ADR-0007) — never remove, weaken,
  or "temporarily" bypass a check. Changes here need an explicit review step.
- **Async**: all I/O is `async`/`await` with `CancellationToken` propagated. No `Thread.Sleep`
  (use `Task.Delay(ms, ct)`). `async void` only on WinForms event handlers.
- **WebView2**: JS↔C# only via `CoreWebView2.WebMessageReceived`. New panel ⇒ HTML template in
  `Assets/` + hosting Form. (`.agent/skills/add-webview2-form/SKILL.md`)

## `[AMT:TIER]` code annotations

Comments like `// [AMT:OPUS] reason — constraint` or `// [AMT:PRO] …` mark risk boundaries
(concurrent state, integrity-registered resources, atomic file ops, interface/bridge contracts).
When you touch tagged code: read the referenced ADR/reason first and apply extra care.
**Never remove or downgrade a tag to make a change easier** — that's a guardrail violation. When you
introduce code matching those criteria (shared mutable state, `FileTransactionService` pipeline,
security/crypto, interface impl, WebView2 bridge handler), add the tag at the method/class boundary.

## Grounding (avoid hallucination)

Only use packages in `ArdysaModsTools.csproj` or native .NET 8. Verify exact method signatures for
`FileTransactionService`, `SmartCdnSelector`, `ILogService`, `ValveKeyValue`, and VPK tool CLI flags
(`HLExtract.exe`, `vpk.exe` in `tools/`) from the source before calling — do not invent flags/APIs.
If you can't confirm something, say so instead of guessing.

## Commits

Format: `version | category | message | build` (e.g. `2.2.13-beta | Bugfix | fix X | 2213`).
`build.txt` holds the current build number (now `2212`); increment by 1 each commit.
Categories: Feature, Bugfix, Security, Performance, Refactor, Documentation, UI/UX, Build, Other.
**No AI co-author trailer** — just the bare formatted line. See `COMMIT_GUIDE.md`.
Only commit/push when asked; if on `main`, branch first.

## Where to look

- `docs/adr/` — read the relevant ADR before any structural change.
- `.agent/skills/` — deep-dive guides (add-service, add-presenter, add-webview2-form, write-tests,
  safe-refactor, code-style, build-and-test).
- `docs/developer/` — API reference and architecture.
- `/daily` — manual project health check (CI, issues, debt, test health, one improvement).

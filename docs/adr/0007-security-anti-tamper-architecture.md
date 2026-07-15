# ADR-0007: Security & Anti-Tamper Architecture

**Date:** 2026-02-10
**Status:** Superseded (2026-07) — client-side anti-tamper removed; see status note
**Deciders:** @Anneardysa

> ⛔ **Superseded 2026-07.** The runtime anti-tamper layer described below has been **removed
> entirely** — `AntiDebug`, `SecurityManager`, and the startup security gate are deleted. Most of
> the planned stack (`SecureConfig`, `StringProtection`, ConfuserEx) was never built, and what
> existed (`IntegrityCheck`, the ntdll anti-debug probes, the RE-tool process scanner, the periodic
> self-exit watchdog) was either fails-open theater or trivially bypassed, while collectively driving
> **antivirus hacktool false positives** (Zillya `Tool.BroHack`) for a tool that legitimately edits
> Dota 2 VPKs. No client-side check survives a determined reverse-engineer; the value it bought did
> not justify the false-positive cost. **Tamper-evidence now depends on Authenticode code signing**
> (not yet in the build). Asset-at-rest protection (`AssetCipher` / `EmbeddedAssetKey`) is unrelated
> and remains. The design below is retained only as historical context.

## Problem Statement

ArdysaModsTools manages premium mod assets and interacts with game files. Without protection, the application binary can be reverse-engineered to extract internal logic, bypass licensing checks, or modified to distribute tampered versions that could harm users' game installations. The application needs a defense-in-depth security layer that raises the bar against casual reverse engineering while accepting that no client-side protection is unbreakable.

## Decision Drivers

- **Defense in depth** — Multiple independent layers so bypassing one doesn't defeat all protection
- **Tamper detection** — The application should detect if its binary has been modified after release
- **Anti-debugging** — Raise the difficulty of attaching debuggers for dynamic analysis
- **Build integration** — Security must be applied automatically during the build process, not manually
- **Performance impact** — Security checks must not noticeably affect application startup or runtime
- **Pragmatic scope** — Accept that client-side protection is never absolute; focus on raising the effort required

## Considered Alternatives

### Alternative 1: Multi-Layer Security Architecture — Chosen

Combine compile-time obfuscation (ConfuserEx) with runtime checks (anti-debug, integrity validation, encrypted config). Each layer operates independently.

- ✅ Good, because bypassing one layer doesn't defeat the others (defense in depth)
- ✅ Good, because ConfuserEx integrates into the MSBuild pipeline (automated)
- ✅ Good, because runtime checks add negligible latency (~10ms total at startup)
- ✅ Good, because integrity checking detects binary tampering post-release
- ❌ Bad, because ConfuserEx can cause debugging difficulties during development
- ❌ Bad, because determined attackers with sufficient time can still bypass all protections

### Alternative 2: ConfuserEx Only (Obfuscation Only)

Apply only compile-time obfuscation without runtime security checks.

- ✅ Good, because it is the simplest to implement (single build step)
- ✅ Good, because zero runtime overhead
- ❌ Bad, because obfuscation alone can be reversed with tools like de4dot
- ❌ Bad, because a tampered binary would run normally (no integrity check)
- ❌ Bad, because debuggers can freely attach and inspect runtime behavior

### Alternative 3: Commercial Protection (VMProtect, Themida)

Use a commercial packer/virtualizer for maximum protection strength.

- ✅ Good, because commercial virtualizers provide the strongest available protection
- ❌ Bad, because licensing costs $100-250+ per developer
- ❌ Bad, because virtualized code runs 10-100x slower (unacceptable for performance-sensitive paths)
- ❌ Bad, because antivirus software frequently flags packed binaries as suspicious

### Alternative 4: No Protection

Ship the application without any obfuscation or anti-tamper measures.

- ✅ Good, because zero complexity and zero build overhead
- ✅ Good, because debugging is trivial
- ❌ Bad, because the binary can be trivially decompiled with ILSpy/dnSpy
- ❌ Bad, because internal logic, API endpoints, and encryption keys are fully exposed
- ❌ Bad, because tampered versions can be distributed with no detection

## Decision

We will implement a **multi-layer security architecture** combining compile-time and runtime protections:

> ⚠️ **Implementation status (2026-07).** The multi-layer stack below was the original plan; almost
> none of it survives. **The entire runtime anti-tamper layer is now removed** — see the Superseded
> banner at the top. Nothing in this ADR describes current behavior.

### Component Responsibilities (all removed or never built)

| Component          | Type    | Status | Notes                                              |
| ------------------ | ------- | ------ | -------------------------------------------------- |
| `SecurityManager`  | Runtime | ❌ removed 2026-07 | Only wrapped the debugger check          |
| `AntiDebug`        | Runtime | ❌ removed 2026-07 | Trivially bypassed; drove AV false positives |
| `IntegrityCheck`   | Runtime | ❌ removed 2026-07 | Fails-open theater (`VerifyAssembly` always returned `true`) |
| `SecureConfig`     | Runtime | ❌ never built | —                                              |
| `StringProtection` | Compile | ❌ never built | —                                              |
| **ConfuserEx**     | Build   | ❌ never wired | —                                              |

### Startup Flow

```csharp
// Program.cs — security check is the first action
public static void Main()
{
    if (!SecurityManager.Initialize())
    {
        // Security check failed — exit silently
        return;
    }

    // Continue normal startup...
    Application.Run(factory.Create());
}
```

### AntiDebug Techniques

```csharp
public static class AntiDebug
{
    public static bool IsBeingDebugged()
    {
#if DEBUG
        return false;
#else
        return CheckManagedDebugger() ||   // Debugger.IsAttached / IsLogging
               CheckNativeDebugger();       // kernel32 IsDebuggerPresent
#endif
    }
}
```

> **2026-07 revision — antivirus false-positive reduction.** The original design also included
> `CheckRemoteDebugger`, an `NtQueryInformationProcess` debug-port probe, a timing-anomaly check,
> an `NtSetInformationThread` hide-from-debugger call, and `CheckForDebugTools` (a running-process
> scan for dnSpy / Cheat Engine / WinDbg / Wireshark / Fiddler that self-terminated on a match).
> These were **removed**: the ntdll imports and the process-scan-and-exit pattern are classic
> hacktool/rootkit heuristics and caused antivirus false positives (Zillya `Tool.BroHack.Win64.*`)
> for a tool that legitimately edits Dota 2 VPKs. They were trivially bypassed (rename the exe) and
> bought negligible protection, so the concrete harm — users unable to run the app — won. The
> managed check plus `IsDebuggerPresent` is retained as a low-signal baseline. Binary reputation is
> better addressed by **Authenticode code signing** (see Consequences).

### Build Integration (ConfuserEx)

```xml
<!-- Applied during Release build via post-build event -->
<PostBuildEvent>
  "$(SolutionDir)tools\ConfuserEx\Confuser.CLI.exe" "$(ProjectDir)confuser.crproj"
</PostBuildEvent>
```

## Consequences

### Positive

- ✅ Multiple independent layers provide defense in depth
- ✅ Startup security checks add ~10ms total (negligible impact)
- ✅ ConfuserEx integrates into the build pipeline automatically
- ✅ Integrity check detects modified binaries before they execute
- ✅ Encrypted config protects sensitive values at rest

### Negative

- ❌ ConfuserEx complicates debugging release builds (Expected; debug builds skip obfuscation)
- ❌ Determined attackers can still bypass client-side protection given enough time
- ❌ Antivirus occasionally flags obfuscated binaries (mitigated by code signing)
- ❌ Security code must be excluded from unit tests (uses conditional compilation)

### Metrics

| Metric                   | No Protection   | Multi-Layer                 |
| ------------------------ | --------------- | --------------------------- |
| Decompilation difficulty | Trivial (ILSpy) | Significant effort required |
| Tamper detection         | None            | Assembly hash validation    |
| Debugger resistance      | None            | Baseline debugger detection |
| Startup overhead         | 0ms             | ~10ms                       |
| Build complexity         | None            | ConfuserEx post-build step  |

## Related

- [ADR-0002: DI Migration with Factory Pattern](./0002-complete-di-migration-factory-pattern.md) — SecurityManager runs before DI container setup
- `Core/Services/Security/SecurityManager.cs`
- `Core/Services/Security/AntiDebug.cs`

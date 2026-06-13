# ADR-0009: CDN Download Resilience Layer

**Date:** 2026-06-13
**Status:** Accepted
**Deciders:** @Anneardysa
**Amends:** [ADR-0003](0003-multi-cdn-strategy-r2-primary.md)

## Problem Statement

ADR-0003 established a five-tier CDN fallback chain (R2 → jsDelivr → GitHub Raw → ghfast.top → gh-proxy.com). The chain is sound, but users still reported intermittent download failures from R2, jsDelivr, and GitHub. Investigation of the pipeline (`Core/Services/Cdn/*`, `Helpers/HttpClientProvider.cs`, `Core/Services/Hero/HeroSetDownloaderService.cs`) found the chain was **fragile to transient and partial failures** and **mis-prioritised CDNs**:

1. **No per-CDN retry.** `CdnConfig.MaxRetryPerCdn` (2) was defined but never used. Every download path made a single attempt per CDN, so a transient blip (timeout, 5xx, 429, connection reset, TLS hiccup) immediately burned that CDN — and failed the whole download if it was the last one.
2. **HEAD-reachability bug.** `SmartCdnSelector.TestCdnAsync` marked any CDN returning a non-2xx **HEAD** as UNREACHABLE and buried it for 6 hours. The GFW proxies (ghfast.top, gh-proxy.com) and occasionally jsDelivr reject HEAD with 403/405 yet serve GET fine — so working CDNs were demoted, hurting exactly the region-blocked users the proxies exist for.
3. **No failure penalty.** `SmartCdnSelector.ReportFailure` was a no-op, so a dead/blocked CDN was retried first on every asset, costing ~30s before fallback each time.
4. **No rate-limit handling.** jsDelivr and GitHub Raw return 429; the chain treated it as a generic hard failure and ignored `Retry-After`.
5. **GitHub token dead-end.** `GitHubTokenHandler` attached the user's token to `*.githubusercontent.com`; an expired/invalid token made the entire GitHub tier return 401, even though the repo is public.
6. **Weak completion validation.** Only byte-count-vs-`Content-Length` was checked, and not on every path.

## Decision Drivers

- **Reliability** on flaky and rate-limited connections without manual retries.
- **Region availability** — proxies must not be wrongly demoted (GFW/ISP users).
- **No change to the ADR-0003 fallback order** — the priority chain is load-bearing and must be preserved.
- **Reuse** existing infrastructure (`RetryHelper`, `CdnConfig`) rather than duplicate it.

## Decision

Add an **additive resilience layer** over the existing chain. The fixed fallback order is unchanged; only retry behaviour, transient-failure recovery, and CDN health tracking are added.

| Concern | Mechanism | Location |
| --- | --- | --- |
| Transient-failure retry | `DownloadRetryPolicy.ExecuteWithRetryAsync` — bounded by `MaxRetryPerCdn`, exponential backoff + jitter, honours a capped `Retry-After` | `Core/Services/Cdn/DownloadRetryPolicy.cs` (new) |
| Transient vs permanent classification | `DownloadRetryPolicy.IsTransientException` (status-aware: a 404 is permanent and falls through; a 503/429/socket error is transient). Status codes delegated to existing `RetryHelper.IsTransientStatusCode` | same |
| Whole-chain retry | `CdnConfig.ChainRetryPasses` sweeps of the full chain with backoff between passes | `CdnFallbackService`, `ResumableDownloadService` |
| Session circuit breaker | `SmartCdnSelector.ReportFailure`/`ReportSuccess` — after `CdnFailureThreshold` consecutive failures a CDN is deprioritized to the **end** of the order for `CdnCooldownSeconds`, then auto-restored. Never dropped (fallback completeness preserved) | `SmartCdnSelector` |
| HEAD-reachability fix | A non-2xx HEAD no longer marks a CDN unreachable; reachability is confirmed by the GET test | `SmartCdnSelector.TestCdnAsync` |
| Rate-limit handling | 429/503 with `Retry-After` surfaces as `TransientDownloadException` carrying the (capped) wait | all download paths |
| Anonymous GitHub retry | On 401/403 from a GitHub host with a token attached, replay the bodyless request once without the `Authorization` header | `GitHubTokenHandler` |
| Size-only integrity | Reject empty bodies and payloads whose length ≠ declared `Content-Length` (per file and per split part) | `CdnFallbackService`, `ResumableDownloadService`, `HeroSetDownloaderService` |

New tunables live in `Core/Constants/CdnConfig.cs`: `RetryBaseDelayMs`, `RetryMaxDelayMs`, `MaxRetryAfterSeconds`, `ChainRetryPasses`, `CdnFailureThreshold`, `CdnCooldownSeconds` (and the now-used `MaxRetryPerCdn`).

### Risk classification

`DownloadRetryPolicy` and `SmartCdnSelector` are annotated `[AMT:OPUS]` (they drive CDN selection and retry for the entire pipeline). `GitHubTokenHandler` is `[AMT:PRO]` (auth-sensitive cross-cutting handler).

## Considered Alternatives

- **Hedged/racing requests** (start the top 2 CDNs, take the first to respond). Rejected: doubles bandwidth on metered/region-limited links and complicates progress/resume. The circuit breaker + retry achieves most of the benefit at no extra egress.
- **Extend `RetryHelper` to honour `Retry-After`.** Rejected: its loop is exception-only with a fixed delay schedule and is shared by `UpdaterService`/`MiscUtilityService`; a per-failure `Retry-After` delay would require signature changes that ripple to those callers. `DownloadRetryPolicy` instead reuses `RetryHelper.IsTransientStatusCode` and adds only the download-specific behaviour.
- **SHA-256 content verification.** Deferred — requires a server-side hash manifest that does not yet exist (see below).

## Consequences

### Positive

- Transient failures (timeouts, 5xx, 429, resets) recover automatically instead of failing the download.
- Dead/blocked CDNs are skipped quickly after a few failures and auto-recover after cooldown.
- Proxies are no longer wrongly buried by HEAD-hostile responses → better for GFW/ISP-restricted users.
- A stale/invalid GitHub token no longer kills the GitHub tier.
- Truncated/empty payloads are rejected rather than extracted into corrupt archives.

### Negative

- A persistently failing endpoint now costs up to `MaxRetryPerCdn` attempts × `ChainRetryPasses` before total failure (bounded; backoff keeps it modest).
- Circuit-breaker state is in-memory and per-session (intentionally simple; not persisted).

### Future work

- ~~**SHA-256 content verification**~~ — **Implemented** in [ADR-0010](0010-asset-hash-verification.md): a server-published `Assets/asset_hashes.json` manifest plus client-side verification before extraction/install/launch, with hard-fail-and-fall-through-to-next-CDN and graceful skip when an asset is absent. The size-only check here remains the fallback when the manifest has no entry for an asset.

## Related

- [ADR-0003](0003-multi-cdn-strategy-r2-primary.md) — multi-CDN fallback chain (amended by this ADR; order unchanged)
- `Core/Services/Cdn/DownloadRetryPolicy.cs`, `SmartCdnSelector.cs`, `CdnFallbackService.cs`, `ResumableDownloadService.cs`
- `Core/Services/Hero/HeroSetDownloaderService.cs` — split-archive part retry + validation
- `Helpers/HttpClientProvider.cs` — `GitHubTokenHandler` anonymous retry
- `Core/Helpers/RetryHelper.cs` — reused status-code classifier

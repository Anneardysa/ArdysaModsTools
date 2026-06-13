# ADR-0003: Multi-CDN Strategy with R2 Primary

**Date:** 2026-02-04
**Status:** Accepted
**Deciders:** @Anneardysa

## Problem Statement

The application downloads assets (hero sets, config files, ModsPack) from GitHub repositories. Users in China and behind certain ISPs reported "CONNECTION TO SERVER FAILED" errors because jsDelivr — the sole CDN — is blocked in their regions. Additionally, jsDelivr enforces a 20MB file size limit which restricts larger asset bundles.

## Decision Drivers

- **Global availability** — The application must work for users worldwide, including China and restricted ISP regions
- **No file size limits** — Asset bundles may exceed jsDelivr's 20MB cap
- **Reliability** — If one CDN goes down, asset delivery must not break entirely
- **Cost efficiency** — CDN costs should be proportional to the project's low traffic volume
- **Professional branding** — A custom domain looks more trustworthy than a third-party CDN URL

## Considered Alternatives

### Alternative 1: Multi-CDN with Cloudflare R2 Primary — Chosen

Prioritized fallback chain: R2 → jsDelivr → GitHub Raw. The application tries each CDN in order and falls through on failure.

- ✅ Good, because R2 uses Cloudflare's global edge network (unrestricted in most regions)
- ✅ Good, because R2 has no file size limits (unlike jsDelivr's 20MB cap)
- ✅ Good, because custom domain `cdn.ardysamods.my.id` allows full control and professional appearance
- ✅ Good, because fallback to jsDelivr and GitHub Raw ensures resilience even if R2 has issues
- ✅ Good, because R2's egress cost is $0 (Cloudflare's unique pricing)
- ❌ Bad, because files must be synced to R2 separately from GitHub pushes
- ❌ Bad, because R2 bucket requires management and monitoring

### Alternative 2: jsDelivr Only (Status Quo)

Continue using jsDelivr as the sole CDN.

- ✅ Good, because zero additional cost and zero infrastructure to manage
- ✅ Good, because it auto-syncs from GitHub repositories
- ❌ Bad, because it is blocked in China and by some ISPs — the core problem remains unsolved
- ❌ Bad, because 20MB file size limit restricts larger asset bundles
- ❌ Bad, because a single CDN is a single point of failure

### Alternative 3: GitHub Raw Only

Serve all assets directly from `raw.githubusercontent.com`.

- ✅ Good, because it is always available (no CDN blocking)
- ✅ Good, because zero additional cost
- ❌ Bad, because it is significantly slower than any CDN (no edge caching)
- ❌ Bad, because GitHub rate-limits raw file requests (60/hour unauthenticated)
- ❌ Bad, because download speeds are throttled for large files

### Alternative 4: Self-Hosted CDN (Own Server)

Host assets on a dedicated VPS with Nginx.

- ✅ Good, because full control over everything
- ❌ Bad, because monthly VPS costs ($5-20/mo) are disproportionate for this project
- ❌ Bad, because requires server administration, SSL management, and uptime monitoring
- ❌ Bad, because a single server is less reliable than a CDN edge network

## Decision

We will implement a **multi-CDN fallback strategy** with Cloudflare R2 as the primary CDN:

| Priority | CDN               | Base URL                        | Rationale                             |
| -------- | ----------------- | ------------------------------- | ------------------------------------- |
| 1        | **Cloudflare R2** | `cdn.ardysamods.my.id`          | Global edge, no size limit, $0 egress |
| 2        | jsDelivr          | `cdn.jsdelivr.net/gh/...`       | Fast cached fallback                  |
| 3        | GitHub Raw        | `raw.githubusercontent.com/...` | Always-available fallback             |
| 4        | **ghfast.top**    | `ghfast.top/raw.github...`      | Primary GFW bypass mirror proxy       |
| 5        | **gh-proxy.com**  | `gh-proxy.com/raw.github...`    | Secondary GFW bypass mirror proxy     |

### Implementation

```csharp
// CdnConfig.cs — centralized CDN priority chain
public static class CdnConfig
{
    public const string R2BaseUrl = "https://cdn.ardysamods.my.id";
    public const string JsDelivrBaseUrl = "https://cdn.jsdelivr.net/gh/Anneardysa/ModsPack@main";
    public const string GitHubRawBaseUrl = "https://raw.githubusercontent.com/Anneardysa/ModsPack/main";
    public const string GitHubProxyPrimaryUrl = "https://ghfast.top/" + GitHubRawBaseUrl;
    public const string GitHubProxySecondaryUrl = "https://gh-proxy.com/" + GitHubRawBaseUrl;

    public static bool IsR2Enabled { get; set; } = true;

    public static string[] GetCdnBaseUrls()
    {
        if (IsR2Enabled)
        {
            return new[]
            {
                R2BaseUrl,
                JsDelivrBaseUrl,
                GitHubRawBaseUrl,
                GitHubProxyPrimaryUrl,
                GitHubProxySecondaryUrl
            };
        }

        return new[]
        {
            JsDelivrBaseUrl,
            GitHubRawBaseUrl,
            GitHubProxyPrimaryUrl,
            GitHubProxySecondaryUrl
        };
    }
}
```

### Fallback Logic

```csharp
// Connection check iterates through CDNs until one responds
foreach (var baseUrl in CdnConfig.GetCdnBaseUrls())
{
    var url = $"{baseUrl}/Assets/heroes.json";
    try
    {
        using var response = await client.SendAsync(request, cts.Token);
        if (response.IsSuccessStatusCode)
            return true;
    }
    catch (Exception ex)
    {
        _logger.Log($"[NET] CDN attempt failed ({baseUrl}): {ex.Message}");
        // Fall through to next CDN
    }
}
return false;
```

### R2 Sync Operations

Files synced to R2 via `sync-to-r2.ps1` (maintained in the ModsPack repository):

| Path                  | Content                         |
| --------------------- | ------------------------------- |
| `/Assets/heroes.json` | Hero definitions                |
| `/Assets/models/`     | Hero skin ZIPs                  |
| `/config/`            | Remote configuration            |
| `/remote/`            | ModsPack hashes, gameinfo files |
| `/releases/`          | Application update binaries     |

## Consequences

### Positive

- ✅ Works for users in China and regions where jsDelivr is blocked (GFW bypass)
- ✅ No 20MB file size limit for asset bundles
- ✅ Custom domain `cdn.ardysamods.my.id` provides professional branding
- ✅ $0 egress cost (Cloudflare R2 doesn't charge for bandwidth)
- ✅ Five-tier fallback ensures maximum reliability

### Negative

- ❌ R2 requires periodic file syncing via `sync-to-r2.ps1`
- ❌ Adds operational complexity (R2 bucket management)
- ❌ Minor storage cost on R2 (negligible at current scale)

### Metrics

| Metric                     | Before                | After                                         |
| -------------------------- | --------------------- | --------------------------------------------- |
| CDN availability           | 1 (jsDelivr)          | 5 (R2 + jsDelivr + GitHub Raw + 2 GFW mirrors) |
| Regions with access issues | China + some ISPs     | None reported                                 |
| Max file size              | 20MB (jsDelivr limit) | Unlimited (R2)                                |

## Related

- [ADR-0009](0009-cdn-download-resilience-layer.md) — adds a resilience layer (retry/backoff, 429 handling, session circuit breaker, HEAD-reachability fix, anonymous GitHub fallback, size validation) over this chain. The fallback **order defined here is unchanged**.
- `Core/Constants/CdnConfig.cs` — CDN URL configuration
- `Core/Services/Hero/HeroSetDownloaderService.cs` — uses multi-CDN for downloads
- `F:\Projects\ModsPack\sync-to-r2.ps1` — R2 sync automation in asset repository

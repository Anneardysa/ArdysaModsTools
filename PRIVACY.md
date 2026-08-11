# Privacy Policy for ArdysaModsTools

ArdysaModsTools is an open-source client-side mod manager for Dota 2. This Privacy Policy explains our approach to privacy and user data.

## 1. No Data Collection
ArdysaModsTools **does not collect, store, or transmit any personal data, telemetry, analytics, or usage statistics**. 
* We do not track what mods you apply.
* We do not collect your Steam ID, account details, or system specifications.
* There are no tracking scripts, cookies, or analytics services embedded in the application.

## 2. Network Requests
The application performs outgoing network requests only to check for updates and to download mod
assets. Every asset request goes to one of two hosts, both operated for this project:

| Host | Purpose |
| ---- | ------- |
| `cdn.ardysamods.my.id` | Primary CDN (Cloudflare R2) — release manifest, mod archives, thumbnails, remote config |
| `cdn2.ardysamods.my.id` | Fallback CDN (Backblaze B2 behind a Cloudflare Worker), used when the primary is unreachable |
| `api.github.com` | Public release metadata — checking whether a newer version of AMT or the ModsPack exists |
| `cdn.cloudflare.steamstatic.com` | Valve's own public image CDN, for default hero portraits in the Skin Selector |

In addition, links you click in the app (Discord, YouTube, Ko-fi, the releases page, an issue
template, the website) open in **your browser**. Those are your actions, not background calls.

These are standard HTTP GET requests. They share only the connection metadata any web request
shares — your IP address and User-Agent — with the hosting servers, which is unavoidable when
downloading a file. No account is used and no identifier for you, your Steam profile, or your
machine is attached to any of them.

> AMT contacts **no** analytics, telemetry, crash-reporting, or advertising endpoint. If you
> want to verify that, block everything except the hosts above and the app keeps working —
> minus update checks and hero portraits.

## 3. Local Configurations
All configuration, favorites, and settings are saved locally in `%AppData%\ArdysaModsTools`.
Diagnostic logs (`ardysa_fallback.log`, generation reports) are also written locally and are
**never uploaded** — if you attach one to a bug report, that is you choosing to share it.
Nothing is synced to any cloud service.

## 4. Open Source Transparency
ArdysaModsTools is licensed under GPL-v3 and its source is published, so you can inspect it and
verify that no data is collected:
[https://github.com/Anneardysa/ArdysaModsTools](https://github.com/Anneardysa/ArdysaModsTools)

That repository is an automatically-updated public mirror of a private development repository,
and source comments are stripped from `.cs` files when publishing. The code itself is complete
and auditable.

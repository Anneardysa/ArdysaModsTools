# Privacy Policy for ArdysaModsTools

ArdysaModsTools is an open-source client-side mod manager for Dota 2. This Privacy Policy explains our approach to privacy and user data.

## 1. No Data Collection
ArdysaModsTools **does not collect, store, or transmit any personal data, telemetry, analytics, or usage statistics**. 
* We do not track what mods you apply.
* We do not collect your Steam ID, account details, or system specifications.
* There are no tracking scripts, cookies, or analytics services embedded in the application.

## 2. Network Requests
The application only performs outgoing network requests to check for updates and download mod files. These include:
* Checking the latest release manifest on public CDNs (Cloudflare R2, jsDelivr, GitHub Raw).
* Downloading mod archive files (`.zip` / `.vpk`) from official CDN endpoints.

These requests are standard HTTP GET requests and only share basic connection metadata (such as your IP address and User-Agent) with the hosting servers, which is necessary to download the assets.

## 3. Local Configurations
All configurations, favorites, and settings are saved locally on your computer in the `%AppData%\ArdysaModsTools` folder. No settings are synced to external cloud services.

## 4. Open Source Transparency
Since ArdysaModsTools is completely open-source under the GPL-v3 license, you can inspect the full source code at any time to verify that no data is collected:
[https://github.com/Anneardysa/ArdysaModsTools](https://github.com/Anneardysa/ArdysaModsTools)

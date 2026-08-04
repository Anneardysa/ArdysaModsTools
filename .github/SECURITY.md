# Security Policy

## Supported versions

Only the latest released version of AMT is supported. AMT rewrites Dota 2 game files, so
running an old build against a newer Dota 2 patch is itself a risk.

| Version        | Supported |
| -------------- | --------- |
| Latest release | ✅        |
| Anything older | ❌        |

## Reporting a vulnerability

**Do not open a public issue for a security vulnerability.**

Report it privately through
[GitHub Security Advisories](https://github.com/Anneardysa/ArdysaModsTools/security/advisories/new).
That channel is private until a fix ships, and it lets us credit you in the advisory.

Please include:

- What the vulnerability is and where it lives
- Steps to reproduce
- The impact you think it has
- A suggested fix, if you have one

### What to expect

| Stage          | Timeline                                             |
| -------------- | ---------------------------------------------------- |
| Acknowledgment | within 48 hours                                       |
| Assessment     | within 1 week                                         |
| Fix            | critical: as soon as possible; otherwise next release |

Please give us a chance to ship a fix before disclosing publicly.

## Notes for users

- **Always run the latest release.** Old builds are refused by the asset pipeline on purpose
  (error `DL_009`) once the asset format moves on.
- **Download only from [official releases](https://github.com/Anneardysa/ArdysaModsTools/releases)**
  or the official site. Anything else is not AMT.
- **Antivirus warnings on VPK tools are false positives.** AMT bundles Valve's `vpk.exe` and
  HLLib's `HLExtract.exe` to repack game archives; some scanners flag any tool that writes to
  game files. If your AV strips part of the install, AMT may fail to start — see
  [TROUBLESHOOTING.md](../docs/TROUBLESHOOTING.md).
- **AMT sends no telemetry.** See [PRIVACY.md](../PRIVACY.md).

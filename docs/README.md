# ArdysaModsTools Documentation

Documentation hub for AMT. Start with the guide that matches what you're trying to do.

---

## For users

| Guide                                  | Description                                       |
| -------------------------------------- | ------------------------------------------------- |
| **[Main README](../README.md)**        | Project overview, features, installation          |
| **[Quick Start](user/QUICK_START.md)** | Detect Dota 2, install, and press Play in 5 minutes |
| **[User Guide](user/USER_GUIDE.md)**   | Every feature explained, plus FAQ                 |
| **[Troubleshooting](TROUBLESHOOTING.md)** | Error codes and common failures                |
| **[Changelog](../CHANGELOG.md)**       | What changed in each release                      |

## For contributors

| Guide                                          | Description                              |
| ---------------------------------------------- | ---------------------------------------- |
| **[Contributing](../.github/CONTRIBUTING.md)** | How to report, request, and send patches |
| **[Security Policy](../.github/SECURITY.md)**  | Reporting vulnerabilities privately      |
| **[Code of Conduct](../.github/CODE_OF_CONDUCT.md)** | Ground rules for participation     |
| **[Privacy Policy](../PRIVACY.md)**            | What leaves your machine (and what doesn't) |

---

## Where things live

```
docs/
├── README.md              ← You are here
├── TROUBLESHOOTING.md     ← Error codes, common failures
├── INSTALL_INFO.txt       ← Text shown by the installer
└── user/                  ← End-user guides
    ├── QUICK_START.md
    ├── USER_GUIDE.md
    └── images/
```

> [!NOTE]
> **Maintainers:** the internal documentation tree — `docs/developer/` (architecture, API
> reference), `docs/adr/` (Architecture Decision Records), and `docs/dev/INSTALLER.md` — is
> listed in [`.mirrorignore`](../.mirrorignore) and exists **only in the private upstream
> repository**. It is deliberately not linked from here, because those links resolve to 404
> on the public GitHub mirror. See [CONTRIBUTING.md](../.github/CONTRIBUTING.md#how-this-repository-works)
> for how the mirror works.

---

## Key concepts

| Concept                     | Description                                                              |
| --------------------------- | ------------------------------------------------------------------------ |
| **MVP pattern**             | View → Presenter → Service; forms hold no business logic                 |
| **DI + factory**            | `IMainFormFactory` bridges the DI container with `Application.Run()`     |
| **Multi-CDN fallback**      | Cloudflare R2 → Backblaze B2 (Cloudflare Worker), chosen by `SmartCdnSelector` |
| **SHA-256 gate**            | Every download is hash-verified before it is allowed near the game folder |
| **Transactional writes**    | Extract to temp → verify → atomic swap → rollback on any failure          |
| **`OperationResult`**       | Services return results for expected failures instead of throwing        |
| **Package Sync**            | The mod package carries its own item data; Play rebuilds it after a Dota 2 patch |

---

## Links

- [Releases](https://github.com/Anneardysa/ArdysaModsTools/releases)
- [Issues](https://github.com/Anneardysa/ArdysaModsTools/issues)
- [Discord](https://discord.gg/5xKg4fyumv)
- [Website](https://ardysamods.my.id)

---

<div align="center">

**[⬆ Back to Main README](../README.md)**

</div>

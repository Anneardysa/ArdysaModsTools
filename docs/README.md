# 📚 ArdysaModsTools Documentation

Welcome to the AMT 2.0 documentation hub. Find everything you need to use or contribute to the project.

---

## 🎯 Quick Navigation

### 👥 For Users

| Guide                                  | Description                                       |
| -------------------------------------- | ------------------------------------------------- |
| **[Main README](../README.md)**        | Complete project overview, features, installation |
| **[Quick Start](user/QUICK_START.md)** | Get up and running in 5 minutes                   |
| **[User Guide](user/USER_GUIDE.md)**   | Detailed usage walkthrough with FAQ               |

### 👨‍💻 For Developers

| Guide                                             | Description                    |
| ------------------------------------------------- | ------------------------------ |
| **[Development Setup](developer/development.md)** | Environment, building, testing |
| **[Architecture](developer/architecture.md)**     | System design, DI, MVP pattern |
| **[Services API](developer/api/services.md)**     | Core service reference         |
| **[Contributing](dev/CONTRIBUTING.md)**           | How to contribute code         |
| **[Installer Guide](dev/INSTALLER.md)**           | Installer build process        |
| **[Security](dev/SECURITY.md)**                   | Security model & anti-tamper   |

---

## 📖 Documentation Structure

```
docs/
├── README.md                            ← You are here
├── TROUBLESHOOTING.md                   ← Common issues & solutions
│
├── user/                                ← End-user guides
│   ├── QUICK_START.md
│   └── USER_GUIDE.md
│
├── dev/                                 ← Contributor guidelines
│   ├── CONTRIBUTING.md
│   ├── SECURITY.md
│   └── INSTALLER.md
│
├── developer/                           ← Technical documentation
│   ├── architecture.md                  ← System design, DI, CDN
│   ├── development.md                   ← Setup & building
│   └── api/
│       ├── services.md                  ← Service reference
│       ├── models.md                    ← Data models
│       ├── ui-components.md             ← Forms & presenters
│       ├── helpers.md                   ← Utilities
│       ├── exceptions.md                ← Error codes
│       ├── active-mods.md               ← Query installed mods
│       ├── misc-mods.md                 ← Misc mod control
│       ├── auto-patching.md             ← Auto re-patching
│       └── mod-file-structure.md        ← File/folder specs
│
├── adr/                                 ← Architecture Decision Records
│   ├── README.md                        ← ADR index
│   ├── TEMPLATE.md                      ← ADR template
│   ├── 0001-refactor-mainform-mvp.md
│   ├── 0002-complete-di-migration-factory-pattern.md
│   ├── 0003-multi-cdn-strategy-r2-primary.md
│   ├── 0004-presenter-decomposition-srp.md
│   ├── 0005-webview2-hybrid-ui.md
│   ├── 0006-automated-patch-watcher.md
│   ├── 0007-security-anti-tamper-architecture.md
│   ├── 0008-hero-cosmetic-priority-merge.md
│   ├── 0009-cdn-download-resilience-layer.md
│   ├── 0010-asset-hash-verification.md
│   └── 0012-incremental-delta-updates.md
│
└── samples/                             ← Example JSON configs (empty)
```

### 🆘 Need Help?

- **[Troubleshooting Guide](TROUBLESHOOTING.md)** — Common issues and solutions
- **[Changelog](../CHANGELOG.md)** — What's new in each version

---

## 🔑 Key Concepts

| Concept                     | Description                                    |
| --------------------------- | ---------------------------------------------- |
| **MVP Pattern**             | UI uses Model-View-Presenter for testability   |
| **DI + Factory Pattern**    | `IMainFormFactory` bridges DI with WinForms    |
| **Multi-CDN Fallback**      | R2 → jsDelivr → GitHub Raw → GFW proxy mirrors |
| **Smart CDN Selection**     | Latency benchmark picks fastest CDN per user   |
| **OperationResult**         | Service returns instead of throwing exceptions |
| **Presenter Decomposition** | 3 SRP presenters split from MainFormPresenter  |

---

## 🔗 External Links

- 📦 [Releases](https://github.com/Anneardysa/ArdysaModsTools/releases)
- 🐛 [Issues](https://github.com/Anneardysa/ArdysaModsTools/issues)
- 💬 [Discord](https://discord.gg/ardysa)

---

<div align="center">

**[⬆ Back to Main README](../README.md)**

</div>

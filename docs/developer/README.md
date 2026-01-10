# Developer Documentation

Technical documentation for AMT 2.0 contributors and developers.

---

## 📚 Contents

### Getting Started

-  **[Development Setup](development.md)** - Environment setup, building, running
-  **[Architecture Overview](architecture.md)** - System design and patterns

### API Reference

Detailed technical documentation:

-  **[Services API](api/services.md)** - Core service implementations
-  **[Data Models](api/models.md)** - Domain models and DTOs
-  **[UI Components](api/ui-components.md)** - Forms and presenters
-  **[Utilities](api/helpers.md)** - Helper classes and extensions

---

## 🚀 Quick Start

```bash
# Clone and setup
git clone https://github.com/Anneardysa/ArdysaModsTools.git
cd ArdysaModsTools
cp .env.example .env

# Build and run
dotnet restore
dotnet build -c Debug
dotnet run
```

See [development.md](development.md) for detailed instructions.

---

## 🏗️ Project Structure

```
AMT2.0/
├── Core/              # Business logic layer
│   ├── Controllers/   # Application controllers
│   ├── Interfaces/    # Service contracts
│   ├── Models/        # Domain models
│   └── Services/      # Service implementations
├── UI/                # Presentation layer
│   ├── Forms/         # Windows Forms
│   └── Presenters/    # MVP presenters
├── Helpers/           # Utility classes
├── Assets/            # Static resources
└── tools/             # External binaries
```

---

## 🔧 Technology Stack

| Component | Technology                 |
| --------- | -------------------------- |
| Language  | C# 12                      |
| Framework | .NET 8.0                   |
| UI        | Windows Forms              |
| Pattern   | MVP (Model-View-Presenter) |
| Tools     | HLExtract, vpk.exe         |

---

## 🔗 Related Docs

-  [Main README](../../README.md) - Project overview
-  [Contributing Guidelines](../dev/CONTRIBUTING.md) - How to contribute
-  [User Documentation](../user/) - End-user guides

---

<div align="center">

**[⬅ Back to Docs](../README.md)**

</div>

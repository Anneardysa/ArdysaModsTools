# Contributing to ArdysaModsTools

Thank you for your interest in contributing to AMT 2.0!

## Getting Started

### Prerequisites

-  Windows 10/11 (64-bit)
-  [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
-  Visual Studio 2022 or VS Code with C# extension
-  Git

### Setup

1. **Clone the repository**

   ```bash
   git clone https://github.com/Anneardysa/ArdysaModsTools.git
   cd ArdysaModsTools
   ```

2. **Configure environment**

   ```bash
   # Copy the example environment file
   cp .env.example .env

   # Edit .env and fill in your configuration
   # You need your own mod repository for full functionality
   ```

3. **Build the project**

   ```bash
   dotnet build
   ```

4. **Run**
   ```bash
   dotnet run
   ```

## Configuration

The application uses environment variables for sensitive configuration. See `.env.example` for required variables:

-  `AMT_ARCHIVE_PASSWORD` - Password for encrypted mod archives
-  `GITHUB_OWNER` - Your GitHub username for mod repository
-  `GITHUB_MODS_REPO` - Name of your mods repository
-  `GITHUB_TOOLS_REPO` - Name of the tools repository

## Project Structure

```
ArdysaModsTools/
├── Core/                 # Core business logic
│   ├── Services/         # Service implementations
│   ├── Models/           # Data models
│   └── Interfaces/       # Service interfaces
├── UI/                   # Windows Forms UI
│   ├── Forms/            # Form implementations
│   └── Presenters/       # MVP presenters
├── Helpers/              # Utility classes
├── Assets/               # Embedded resources
├── scripts/              # Build and automation scripts
└── tools/                # External tools (vpk.exe, HLExtract.exe)
```

## Development Guidelines

### Code Style

-  Follow C# naming conventions
-  Use meaningful variable and method names
-  Add XML documentation for public APIs
-  Keep methods focused and under 50 lines when possible

### Commits

-  Use clear, descriptive commit messages
-  Reference issue numbers when applicable
-  Keep commits focused on single changes

### Pull Requests

1. Create a feature branch from `main`
2. Make your changes
3. Test thoroughly
4. Update documentation if needed
5. Submit PR with clear description

## Testing

Run the test suite:

```bash
cd Tests
dotnet test
```

## Security

-  **Never commit secrets** - use environment variables
-  Review `.gitignore` before committing
-  Report security issues privately (see SECURITY.md)

## Questions?

Open an issue for questions or discussions.

## License

By contributing, you agree that your contributions will be licensed under the **GNU General Public License v3.0** (GPL-3.0), the same license as this project.

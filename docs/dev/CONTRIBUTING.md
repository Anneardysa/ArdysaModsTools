# Contributing to ArdysaModsTools

Thank you for your interest in contributing to AMT 2.0!

## 🌿 Branch Strategy

We use a **Git Flow** branching model to maintain code quality and stable releases.

### Branch Overview

```
main (stable releases only)
  │
  └── develop (integration branch)
        │
        ├── feature/xxx
        ├── bugfix/xxx
        └── hotfix/xxx
```

| Branch      | Purpose                 | Merges Into             |
| ----------- | ----------------------- | ----------------------- |
| `main`      | Stable releases only    | —                       |
| `develop`   | Integration branch      | `main` (via maintainer) |
| `feature/*` | New features            | `develop`               |
| `bugfix/*`  | Bug fixes               | `develop`               |
| `hotfix/*`  | Urgent production fixes | `main` and `develop`    |

### Branch Naming Conventions

```
feature/hero-gallery-improvements
feature/add-new-mod-type
bugfix/webview-crash-on-startup
bugfix/fix-path-detection
hotfix/critical-security-fix
```

---

## 🚀 Contribution Workflow

### Step 1: Fork & Clone

```bash
# Fork the repository on GitHub first, then:
git clone https://github.com/YOUR_USERNAME/ArdysaModsTools.git
cd ArdysaModsTools

# Add upstream remote
git remote add upstream https://github.com/Anneardysa/ArdysaModsTools.git
```

### Step 2: Create Your Branch

> ⚠️ **Important**: Always branch from `develop`, NOT from `main`!

```bash
# Sync with upstream
git fetch upstream
git checkout develop
git merge upstream/develop

# Create your feature branch
git checkout -b feature/your-feature-name
```

### Step 3: Make Changes

- Write your code
- Follow our [code style guidelines](#code-style)
- Test your changes thoroughly
- Commit with clear messages

```bash
git add .
git commit -m "feat: add hero gallery search filter"
```

### Step 4: Push & Create Pull Request

```bash
git push origin feature/your-feature-name
```

Then on GitHub:

1. Go to your fork
2. Click **"Compare & pull request"**
3. **Set base branch to `develop`** (not `main`)
4. Fill in the PR template
5. Submit!

### Pull Request Checklist

- [ ] Branch created from `develop`
- [ ] PR targets `develop` branch
- [ ] Code follows project style guidelines
- [ ] Changes are tested locally
- [ ] Documentation updated if needed
- [ ] Commit messages are clear and descriptive

---

## 🛠️ Getting Started

### Prerequisites

- Windows 10/11 (64-bit)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 or VS Code with C# extension
- Git

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

- `AMT_ARCHIVE_PASSWORD` - Password for encrypted mod archives
- `GITHUB_OWNER` - Your GitHub username for mod repository
- `GITHUB_MODS_REPO` - Name of your mods repository
- `GITHUB_TOOLS_REPO` - Name of the tools repository

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

## Code Style

- Follow C# naming conventions
- Use meaningful variable and method names
- Add XML documentation for public APIs
- Keep methods focused and under 50 lines when possible

## Commit Message Convention

We follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>: <description>

[optional body]

[optional footer]
```

**Types:**

- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting, etc.)
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

**Examples:**

```
feat: add hero search functionality
fix: resolve WebView2 crash on startup
docs: update contribution guidelines
```

## Testing

Run the test suite:

```bash
cd Tests
dotnet test
```

## Security

- **Never commit secrets** - use environment variables
- Review `.gitignore` before committing
- Report security issues privately (see SECURITY.md)

## Questions?

Open an issue for questions or discussions.

## License

By contributing, you agree that your contributions will be licensed under the **GNU General Public License v3.0** (GPL-3.0), the same license as this project.

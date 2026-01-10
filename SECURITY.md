# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 2.0.x   | :white_check_mark: |
| < 2.0   | :x:                |

## Reporting a Vulnerability

If you discover a security vulnerability in ArdysaModsTools, please report it responsibly:

### Do NOT:

-  Create public GitHub issues for security vulnerabilities
-  Share vulnerability details publicly before they are fixed
-  Attempt to access or modify other users' data

### Do:

1. **Email**: Send details to the project maintainer privately
2. **GitHub Security Advisories**: Use GitHub's private vulnerability reporting feature

### What to Include:

-  Description of the vulnerability
-  Steps to reproduce
-  Potential impact
-  Any suggested fixes (optional)

### Response Timeline:

-  **Acknowledgment**: Within 48 hours
-  **Assessment**: Within 1 week
-  **Fix/Patch**: Depends on severity (critical: ASAP, other: within release cycle)

## Security Best Practices for Users

1. **Keep Updated**: Always use the latest version of AMT 2.0
2. **Download from Official Sources**: Only download from the official GitHub releases
3. **Verify Integrity**: Check that downloaded files match published hashes
4. **Antivirus Alerts**: Some antivirus software may flag VPK manipulation tools as suspicious - this is a false positive

## For Contributors

When contributing code:

-  Never commit secrets, API keys, or passwords
-  Use environment variables for sensitive configuration
-  Run security scans before submitting PRs
-  Follow the `.env.example` pattern for configuration

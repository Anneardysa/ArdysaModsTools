---
name: Install and Manage Mods
description: Install, update, disable, and manage Dota 2 mods using ModInstallerService with VPK validation and multi-CDN fallback
---

# Install and Manage Mods

Use `IModInstallerService` to install, update, disable, and manage Dota 2 mod packs.

## Setup

Resolve via DI (registered in `ServiceCollectionExtensions.AddCoreServices()`):

```csharp
var installer = serviceProvider.GetRequiredService<IModInstallerService>();
```

## Install Mods

Downloads the ModsPack from CDN (Cloudflare R2 → jsDelivr → GitHub Raw fallback), validates the VPK, and applies patches:

```csharp
var result = await installer.InstallModsAsync(dotaPath, force: false, ct);

if (result.Success)
    Console.WriteLine("Mods installed successfully");
else
    Console.WriteLine($"Failed: {result.Message}");
```

### Force reinstall (bypass version check):

```csharp
var result = await installer.InstallModsAsync(dotaPath, force: true, ct);
```

## Update Patcher After Dota Update

When Dota 2 updates, re-apply patches to restore mod loading:

```csharp
var result = await installer.UpdatePatcherAsync(
    dotaPath,
    statusCallback: msg => Console.WriteLine($"[Patch] {msg}"),
    ct);
```

### What UpdatePatcherAsync does:

1. Pre-validates if patch is needed
2. Updates binary signatures in VPK
3. Ensures `_ArdysaMods` mount point in gameinfo
4. Saves patched version to `version.json`
5. Rolls back atomically on failure

## Disable Mods

Remove mod patches without deleting mod files:

```csharp
var result = await installer.DisableModsAsync(dotaPath, ct);
```

## Validate VPK

Check if a VPK file contains the `_ArdysaMods` marker:

```csharp
bool valid = await installer.ValidateVpkAsync(vpkPath, ct);
```

## Manual Install (User-provided VPK)

Install a VPK file provided by the user:

```csharp
var result = await installer.ManualInstallModsAsync(dotaPath, userVpkPath, ct);
```

## Check for Updates

```csharp
bool hasNewer = await installer.HasNewerModsPackAsync(dotaPath, ct);
if (hasNewer)
    Console.WriteLine("A newer ModsPack is available");
```

## Key Types

- **`OperationResult`** — Standard result type with `Success`, `Message`, `Exception`
- **`IModInstallerService`** — Interface in `Core/Interfaces/IModInstallerService.cs`
- **`ModInstallerService`** — Implementation in `Core/Services/Mods/ModInstallerService.cs`

## File Locations

| File             | Path                                  |
| :--------------- | :------------------------------------ |
| Main VPK         | `game/_ArdysaMods/pak01_dir.vpk`      |
| Patched gameinfo | `game/dota/gameinfo.gi`               |
| Version cache    | `game/_ArdysaMods/_temp/version.json` |

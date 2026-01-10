# Helpers & Utilities Reference

Reference for helper classes in `Helpers/` and `Core/Helpers/`.

---

## Overview

| Helper                 | Purpose                          |
| ---------------------- | -------------------------------- |
| `FileHelper`           | File operations with retry logic |
| `PathUtility`          | Path manipulation for Dota 2     |
| `NetworkHelper`        | Download and connectivity        |
| `RegistryHelper`       | Windows registry access          |
| `ProcessChecker`       | Process detection                |
| `UIHelpers`            | WinForms utilities               |
| `HttpClientProvider`   | Singleton HttpClient             |
| `KeyValuesBlockHelper` | Valve KV format parsing          |

---

## Global Helpers

### FileHelper

**File:** `Helpers/FileHelper.cs`

File operations with retry logic for handling locked files.

#### WaitForFileReady

Poll until a file can be opened for read/write.

```csharp
// Wait up to 5 seconds, polling every 200ms
bool available = FileHelper.WaitForFileReady(
    path: @"C:\path\to\file.vpk",
    timeout: TimeSpan.FromSeconds(5),
    pollInterval: TimeSpan.FromMilliseconds(200));

if (!available)
    throw new IOException("File locked");
```

#### SafeCopyFileWithRetries

Copy file with exponential backoff and unique naming.

```csharp
string destPath = FileHelper.SafeCopyFileWithRetries(
    src: @"C:\source\mod.vpk",
    destFolder: @"C:\dest",
    maxRetries: 6,
    baseDelayMs: 150);
```

#### RunProcessCaptureAsync

Run external process and capture output.

```csharp
string output = await FileHelper.RunProcessCaptureAsync(
    fileName: "HLExtract.exe",
    args: "-p pak01_dir.vpk -d output",
    workingDir: @"C:\tools",
    ct: cancellationToken);
```

---

### PathUtility

**File:** `Helpers/PathUtility.cs`

Path manipulation for Dota 2 directories.

| Method                      | Description                       |
| --------------------------- | --------------------------------- |
| `NormalizeTargetPath(path)` | Ensure path ends with `game\dota` |
| `GetVpkPath(targetPath)`    | Get `pak01_dir.vpk` path          |
| `GetModsFolder(targetPath)` | Get `_ArdysaMods` folder          |
| `EnsureDirectory(path)`     | Create directory if needed        |

```csharp
string dotaPath = PathUtility.NormalizeTargetPath(userPath);
// "C:\Steam\steamapps\common\dota 2 beta\game\dota"

string vpkPath = PathUtility.GetVpkPath(dotaPath);
// "C:\...\game\dota\pak01_dir.vpk"

string modsFolder = PathUtility.GetModsFolder(dotaPath);
// "C:\...\game\dota\_ArdysaMods"
```

---

### NetworkHelper

**File:** `Helpers/NetworkHelper.cs`

Network operations with progress reporting.

#### DownloadFileAsync

```csharp
await NetworkHelper.DownloadFileAsync(
    url: "https://cdn.example.com/mods.zip",
    destination: @"C:\temp\mods.zip",
    progress: percent => progressBar.Value = percent,
    ct: cancellationToken);
```

#### DownloadStringAsync

```csharp
string json = await NetworkHelper.DownloadStringAsync(
    url: "https://api.example.com/config.json",
    ct: cancellationToken);
```

#### IsInternetAvailable

```csharp
if (!NetworkHelper.IsInternetAvailable())
{
    ShowError("No internet connection");
    return;
}
```

---

### RegistryHelper

**File:** `Helpers/RegistryHelper.cs`

Windows registry access wrapper.

```csharp
// Read Steam path
string? steamPath = RegistryHelper.GetValue(
    @"HKCU\Software\Valve\Steam",
    "SteamPath");

// Check if key exists
bool exists = RegistryHelper.KeyExists(
    @"HKLM\SOFTWARE\Valve\Steam");
```

---

### ProcessChecker

**File:** `Helpers/ProcessChecker.cs`

Process detection utility.

```csharp
// Used at startup to block if Dota 2 is running
if (ProcessChecker.IsProcessRunning("dota2"))
{
    MessageBox.Show("Please close Dota 2 first");
    return;
}
```

---

### UIHelpers

**File:** `Helpers/UIHelpers.cs`

WinForms utilities.

#### InvokeIfRequired

Thread-safe control invocation.

```csharp
UIHelpers.InvokeIfRequired(label, () =>
{
    label.Text = "Updated from background thread";
});
```

#### SetDoubleBuffered

Enable double buffering to reduce flicker.

```csharp
UIHelpers.SetDoubleBuffered(panel);
```

---

### HttpClientProvider

**File:** `Helpers/HttpClientProvider.cs`

Singleton HttpClient to avoid socket exhaustion.

```csharp
var response = await HttpClientProvider.Instance
    .GetAsync("https://api.example.com/data");
```

---

## Core Helpers

### KeyValuesBlockHelper

**File:** `Core/Helpers/KeyValuesBlockHelper.cs`

Parses and manipulates Valve's KeyValues format (used in `items_game.txt`).

#### ExtractBlockById

Extract a block by its numeric ID.

```csharp
string? block = KeyValuesBlockHelper.ExtractBlockById(
    content: itemsGameContent,
    id: "555",
    requireItemMarkers: true);

// Returns:
// "555"
// {
//     "name" "Item Name"
//     "prefab" "wearable"
// }
```

#### ReplaceIdBlock

Replace a block with new content.

```csharp
string modified = KeyValuesBlockHelper.ReplaceIdBlock(
    content: itemsGameContent,
    id: "555",
    replacementBlock: newBlockContent,
    requireItemMarkers: true,
    out bool didReplace);
```

#### ParseKvBlocks

Parse `index.txt` format (multiple blocks).

```csharp
var blocks = KeyValuesBlockHelper.ParseKvBlocks(indexContent);
// Returns: Dictionary<string, string> { "555" => "...", "556" => "..." }
```

#### Balanced Brace Parsing

The helper uses quote-aware brace matching:

```csharp
int endIndex = KeyValuesBlockHelper.ExtractBalancedBlockEnd(
    text: content,
    firstBraceIdx: openBracePosition);
```

Handles:

-  Nested braces
-  Quoted strings with escaped characters
-  Both multi-line and one-liner formats

#### KeyValues Format

**Multi-line:**

```
"555"
{
    "name"       "Item Name"
    "prefab"     "wearable"
    "used_by_heroes"
    {
        "npc_dota_hero_antimage"    "1"
    }
}
```

**One-liner:**

```
"555"{"name""Item Name""prefab""wearable""used_by_heroes"{"npc_dota_hero_antimage""1"}}
```

Both formats are handled transparently.

#### Item Block Detection

Heuristic markers for identifying item blocks:

```csharp
string[] markers = {
    "\"used_by_heroes\"",
    "\"prefab\"",
    "\"model_player\"",
    "\"item_name\"",
    "\"image_inventory\"",
    "\"portraits\"",
    "\"visuals\"",
    "\"item_slot\"",
    "\"item_type_name\"",
    "\"name\""
};
```

---

## Common Patterns

### Retry with Backoff

```csharp
async Task<T> RetryAsync<T>(Func<Task<T>> action, int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await action();
        }
        catch when (i < maxRetries - 1)
        {
            await Task.Delay(100 * (i + 1));
        }
    }
    return await action(); // Last attempt throws
}
```

### Thread-Safe UI Updates

```csharp
void UpdateStatus(string text)
{
    if (statusLabel.InvokeRequired)
    {
        statusLabel.Invoke(() => statusLabel.Text = text);
    }
    else
    {
        statusLabel.Text = text;
    }
}
```

### Path Normalization

```csharp
string NormalizePath(string path)
{
    path = Path.GetFullPath(path);
    if (!path.EndsWith(@"game\dota", StringComparison.OrdinalIgnoreCase))
    {
        path = Path.Combine(path, "game", "dota");
    }
    return path;
}
```

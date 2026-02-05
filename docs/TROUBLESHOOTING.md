# Troubleshooting Guide

Common issues and solutions for developers and users.

---

## 🔌 Connection Issues

### "CONNECTION TO SERVER FAILED" in Skin Selector

**Symptoms:**

- Cannot open Skin Selector
- Console shows `[NET] Timeout connecting to...`

**Causes:**

1. **CDN blocked in your region** (jsDelivr blocked in some countries)
2. **Firewall/antivirus blocking** the application
3. **DNS issues** with your ISP
4. **Rate limiting** from too many requests

**Solutions:**

1. Update to latest version (uses R2 CDN with fallback)
2. Check console log for specific error:
   - `[NET] Timeout` → Slow connection, try again
   - `[NET] Server returned 403` → Rate limited, wait 1 hour
   - `[NET] Connection failed` → Network issue, check firewall
3. Try changing DNS to `8.8.8.8` or `1.1.1.1`
4. Whitelist `cdn.ardysamods.my.id` in firewall

---

## 🏗️ Build Issues

### Missing .NET 8 SDK

**Error:** `The SDK 'Microsoft.NET.Sdk' specified could not be found`

**Solution:**

```bash
# Download and install from
# https://dotnet.microsoft.com/download/dotnet/8.0
```

### WebView2 Runtime Not Found

**Error:** `WebView2 runtime not found`

**Solution:**

```bash
# Install from Microsoft
# https://developer.microsoft.com/microsoft-edge/webview2/
```

### tools/ Directory Missing

**Error:** `HLExtract.exe not found`

**Solution:**
Ensure `.csproj` copies tools:

```xml
<Content Include="tools\**\*">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
```

---

## 🧪 Test Issues

### Tests Fail with DI Errors

**Error:** `Unable to resolve service for type 'IConfigService'`

**Cause:** `ServiceLocator` not initialized in test setup

**Solution:**

```csharp
[SetUp]
public void Setup()
{
    var services = new ServiceCollection();
    services.AddArdysaServices();
    var provider = services.BuildServiceProvider();
    ServiceLocator.Initialize(provider);
}

[TearDown]
public void TearDown()
{
    ServiceLocator.Dispose();
}
```

### Tests Pass Locally but Fail in CI

**Common causes:**

1. **Path differences** - Use `Path.Combine()` not hardcoded paths
2. **Missing test data** - Ensure embedded resources are included
3. **Timing issues** - Add proper async waits

---

## 🎮 Runtime Issues

### "Dota 2 Not Detected"

**Causes:**

1. Steam not installed to default path
2. Dota 2 installed in non-standard location
3. Registry entries missing

**Solutions:**

1. Click "Manual Detect" and browse to `dota 2 beta` folder
2. Run AMT as Administrator
3. Reinstall Steam (recreates registry entries)

### Mods Not Showing in Game

**Causes:**

1. Dota 2 was updated (signatures changed)
2. Gameinfo.gi not patched
3. VPK file corrupted

**Solutions:**

1. Click "Patch Update" in AMT
2. Check console for errors
3. Reinstall mods with "Install ModsPack"

### "Signature Mismatch" After Dota Update

**Normal behavior!** Dota 2 updates change file signatures.

**Solution:**

```
Click "Patch Update" → Wait for completion → Launch Dota 2
```

---

## 🔒 Security Issues

### App Won't Launch (Security Check Failed)

**Causes:**

1. Debugger attached (expected in development)
2. Antivirus flagging as suspicious
3. Running in VM/sandbox (detected)

**Solutions:**

1. **Development:** Comment out `SecurityManager.Initialize()` in `Program.cs`
2. **Antivirus:** Add exception for AMT folder
3. **Release testing:** Use unprotected build: `dotnet publish -c Release -p:SkipInternalProtection=true`

---

## 📁 File Issues

### "Access Denied" Errors

**Causes:**

1. Dota 2 is running (locks VPK files)
2. Antivirus scanning files
3. No write permission to game folder

**Solutions:**

1. Close Dota 2 completely
2. Temporarily disable real-time antivirus scan
3. Run AMT as Administrator

### VPK Recompilation Fails

**Error:** `vpk.exe returned non-zero exit code`

**Causes:**

1. Missing vpk.exe dependencies
2. Corrupted extraction directory
3. Disk space full

**Solutions:**

1. Check `tools/Source 2 Viewer/` has all DLLs
2. Delete `_ArdysaMods/_temp/` and retry
3. Free up disk space (need ~2GB for extraction)

---

## 💡 Debugging Tips

### Enable Verbose Logging

Check console in main window for detailed logs. Copy with the "Copy" button.

### Log File Location

```
[Dota 2 Path]/game/dota/_ArdysaMods/_temp/logs/
```

### Common Log Patterns

| Pattern   | Meaning                      |
| --------- | ---------------------------- |
| `[VPK]`   | VPK extraction/recompilation |
| `[NET]`   | Network operations           |
| `[PATCH]` | Signature patching           |
| `[GEN]`   | Hero/misc generation         |

### Debug Build

```bash
dotnet build -c Debug
# Then run from bin/Debug/net8.0-windows/
```

---

## 🔗 Getting Help

1. **Check console logs** - Copy and share error messages
2. **GitHub Issues** - [Open an issue](https://github.com/Anneardysa/ArdysaModsTools/issues)
3. **Discord** - [Join community](https://discord.gg/ffXw265Z7e)

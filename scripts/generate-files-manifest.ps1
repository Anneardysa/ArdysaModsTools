<#
.SYNOPSIS
    Generates files.json — the per-file SHA-256 manifest that powers incremental ("delta") app
    updates. Published to R2 as releases/<version>/files.json next to a copy of the file tree.

.DESCRIPTION
    Walks a published application tree and emits { relativePath -> { sha256, size } } for every file
    that the installer actually installs. The client (Core/Services/Update/DeltaUpdateService.cs)
    hashes its own install folder against this manifest and downloads only the entries that differ,
    from releases/<version>/files/<relativePath>.

    The JSON shape is deliberately identical to Assets/asset_hashes.json so the client reuses
    AssetHashManifestService.ParseManifest / AssetHashVerifier unchanged: keys are forward-slashed
    relative paths, hashes are uppercase hex (matching Convert.ToHexString).

    IMPORTANT: the exclusions below must match the rclone --exclude filters used to upload the tree
    in .github/workflows/release.yml. A file in the manifest that was never uploaded is a 404 that
    kills the delta for everyone; a file uploaded but not listed is merely dead weight.

.PARAMETER PublishDir
    The published tree (e.g. publish/installer) — the same files Inno Setup packages.

.PARAMETER Output
    Output manifest path. Defaults to "<PublishDir>/../files.json".

.PARAMETER Exclude
    Wildcard patterns (matched against the relative path) to leave out of the manifest.

    The defaults are exactly the files the publish tree contains but Inno Setup does NOT install
    (Build/Installer/ArdysaModsTools.iss copies the app exe, *.dll, *.json, *.xml, Assets\* and the
    explicit tool binaries — nothing else). Listing a file the installer never places would make the
    diff report it as "missing" on EVERY update for every installed user: re-downloaded every time,
    written into {app} outside the installer's file list, and left behind on uninstall.
    Keep this list in step with the ISS [Files] section.

.EXAMPLE
    .\generate-files-manifest.ps1 -PublishDir publish/installer -Output delta/files.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PublishDir,
    [string]$Output,
    # unins000.exe: the WPF build pipeline copies its slim uninstaller into the publish tree, and
    # legacy Inno installs have their own unins000.exe (paired with unins000.dat). The delta must
    # never overwrite either with the other — uninstallers are the full installer's business.
    [string[]]$Exclude = @('*.pdb', 'createdump.exe', '*.dll.config', 'unins000.exe')
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path $PublishDir).Path
if (-not $Output) {
    $Output = Join-Path (Split-Path $root -Parent) 'files.json'
}

$outDir = Split-Path $Output -Parent
if ($outDir -and -not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

$files = [ordered]@{}
$skipped = 0

Get-ChildItem -Path $root -Recurse -File | Sort-Object FullName | ForEach-Object {
    $rel = $_.FullName.Substring($root.Length).TrimStart('\', '/') -replace '\\', '/'

    foreach ($pattern in $Exclude) {
        if ($rel -like $pattern) {
            $skipped++
            return
        }
    }

    $hash = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
    $files[$rel] = [ordered]@{ sha256 = $hash; size = $_.Length }
}

if ($files.Count -eq 0) {
    throw "No files found under '$root' — refusing to publish an empty manifest."
}

$generatedAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')

# Emit the JSON by hand so the layout is byte-stable regardless of the PowerShell host
# (same reasoning as generate-asset-hashes.ps1): UTF-8 no BOM, CRLF, 2-space indent.
function ConvertTo-JsonString([string]$s) {
    return '"' + $s.Replace('\', '\\').Replace('"', '\"') + '"'
}

$nl = "`r`n"
$sb = New-Object System.Text.StringBuilder
[void]$sb.Append('{').Append($nl)
[void]$sb.Append('  "version": 1,').Append($nl)
[void]$sb.Append('  "algorithm": "SHA-256",').Append($nl)
[void]$sb.Append('  "generatedAt": ').Append((ConvertTo-JsonString $generatedAt)).Append(',').Append($nl)
[void]$sb.Append('  "assets": {').Append($nl)

$keys = @($files.Keys)
for ($i = 0; $i -lt $keys.Count; $i++) {
    $key = $keys[$i]
    $entry = $files[$key]
    $sep = if ($i -lt $keys.Count - 1) { ',' } else { '' }
    [void]$sb.Append('    ').Append((ConvertTo-JsonString $key)).Append(': {').Append($nl)
    [void]$sb.Append('      "sha256": ').Append((ConvertTo-JsonString $entry.sha256)).Append(',').Append($nl)
    [void]$sb.Append('      "size": ').Append([string]$entry.size).Append($nl)
    [void]$sb.Append('    }').Append($sep).Append($nl)
}
[void]$sb.Append('  }').Append($nl)
[void]$sb.Append('}').Append($nl)

[System.IO.File]::WriteAllText($Output, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))

# Companion upload list: the exact set of paths in the manifest, for `rclone copy --files-from`.
# The manifest is then the single source of truth for what is published — a file listed but never
# uploaded is a 404 that breaks the delta for every user, and keeping two separate exclusion lists
# in step by hand is how that happens.
$listPath = [System.IO.Path]::ChangeExtension($Output, '.list')
[System.IO.File]::WriteAllLines($listPath, [string[]]@($files.Keys), (New-Object System.Text.UTF8Encoding($false)))

$totalMb = [math]::Round((($files.Values | ForEach-Object { $_.size } | Measure-Object -Sum).Sum / 1MB), 1)
Write-Host "Wrote $($files.Count) file hashes ($totalMb MB, $skipped skipped) to $Output"
Write-Host "Wrote upload list to $listPath"

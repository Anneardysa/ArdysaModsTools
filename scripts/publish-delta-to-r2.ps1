<#
.SYNOPSIS
    Uploads a release's delta-update payload to R2: the per-file tree + files.json manifest that
    let installed clients download only what changed instead of the full installer.

.DESCRIPTION
    Run AFTER scripts/build/build_installer.py (which generates build/Installer/Output/delta/) and
    BEFORE publishing the GitHub release. Order matters:

        build_installer.py  →  THIS SCRIPT  →  publish the GitHub release

    because sync-release-to-r2.yml (triggered by the release publish) only advertises the delta —
    the "files" field in releases.json — if files.json already exists on R2. Upload after
    publishing and the field stays null until you re-run that workflow via workflow_dispatch.

    The upload is driven by files.list (emitted next to files.json by the manifest generator), so
    the uploaded tree and the manifest cannot drift apart — they come from the same enumeration.
    files.json is uploaded LAST: until it exists, no client is told a delta is available, so a
    half-finished upload can never break anyone.

.PARAMETER Version
    Release version, exactly as tagged/foldered on R2 (e.g. "2.2.15-beta" for releases/2.2.15-beta/).

.PARAMETER PublishDir
    The published app tree the manifest was generated from. Default: publish/installer.

.PARAMETER DeltaDir
    Folder holding files.json + files.list. Default: build/Installer/Output/delta.

.PARAMETER Remote
    rclone remote name for R2. Default: r2 (same as the GitHub workflows).

.PARAMETER Bucket
    R2 bucket. Default: modspack-asset (same as the GitHub workflows).

.EXAMPLE
    pwsh scripts/publish-delta-to-r2.ps1 -Version 2.2.15-beta
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$PublishDir = 'publish/installer',
    [string]$DeltaDir = 'build/Installer/Output/delta',
    [string]$Remote = 'r2',
    [string]$Bucket = 'modspack-asset'
)

$ErrorActionPreference = 'Stop'

# Resolve everything relative to the repo root (this script's parent's parent).
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

$manifest = Join-Path $DeltaDir 'files.json'
$fileList = Join-Path $DeltaDir 'files.list'

if (-not (Get-Command rclone -ErrorAction SilentlyContinue)) {
    throw "rclone not found on PATH. Install it and configure the '$Remote' remote for Cloudflare R2."
}
if (-not (Test-Path $manifest) -or -not (Test-Path $fileList)) {
    throw "Delta manifest not found in '$DeltaDir'. Run scripts/build/build_installer.py first."
}
if (-not (Test-Path (Join-Path $PublishDir 'ArdysaModsTools.exe'))) {
    throw "'$PublishDir' does not look like a publish tree (no ArdysaModsTools.exe)."
}

# Sanity: every path in files.list must exist in the publish tree. A manifest generated from a
# DIFFERENT publish run than the tree being uploaded would 404 for every client.
$missing = Get-Content $fileList | Where-Object {
    -not (Test-Path (Join-Path $PublishDir ($_ -replace '/', '\')))
}
if ($missing) {
    throw "files.list references $(@($missing).Count) file(s) missing from '$PublishDir' (first: $($missing | Select-Object -First 1)). Manifest and publish tree are out of sync — rebuild."
}

$dest = "${Remote}:${Bucket}/releases/${Version}"
$count = (Get-Content $fileList | Measure-Object -Line).Lines

Write-Host "Uploading $count file(s) to $dest/files/ ..."
# --s3-no-check-bucket: R2 tokens scoped to one bucket can't list/create buckets; without this
# rclone tries CreateBucket and gets 403 AccessDenied (the workflows set no_check_bucket=true in conf).
rclone copy $PublishDir "$dest/files/" --files-from $fileList --transfers 16 --stats 15s --s3-no-check-bucket
if ($LASTEXITCODE -ne 0) { throw "rclone failed to upload the file tree (exit $LASTEXITCODE)" }

# Manifest LAST — its presence is what tells sync-release-to-r2.yml (and clients) the delta exists.
rclone copyto $manifest "$dest/files.json" --s3-no-check-bucket
if ($LASTEXITCODE -ne 0) { throw "rclone failed to upload files.json (exit $LASTEXITCODE)" }

Write-Host ""
Write-Host "Delta payload published for v$Version."
Write-Host "Now publish the GitHub release — sync-release-to-r2.yml will add the 'files' field to releases.json."

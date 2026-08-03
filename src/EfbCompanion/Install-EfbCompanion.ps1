param(
    [string]$InstalledPackagesPath
)

$ErrorActionPreference = "Stop"
$packageName = "noscapect-vfo-efb"
$distRoot = Join-Path $PSScriptRoot "PackageSources\VfoEfb\dist"
$contentInfoRoot = Join-Path $PSScriptRoot `
    "PackageDefinitions\$packageName\ContentInfo"
if (-not (Test-Path -LiteralPath $distRoot)) {
    throw "The EFB app has not been built. Run Build-EfbCompanion.ps1 first."
}
if (-not (Test-Path -LiteralPath $contentInfoRoot)) {
    throw "The EFB ContentInfo assets were not found at '$contentInfoRoot'."
}

if ([string]::IsNullOrWhiteSpace($InstalledPackagesPath)) {
    $userCfg = Join-Path $env:LOCALAPPDATA `
        "Packages\Microsoft.Limitless_8wekyb3d8bbwe\LocalCache\UserCfg.opt"
    if (-not (Test-Path -LiteralPath $userCfg)) {
        throw "MSFS 2024 UserCfg.opt was not found; provide -InstalledPackagesPath."
    }

    $pathLine = Get-Content -LiteralPath $userCfg |
        Where-Object { $_ -match '^\s*InstalledPackagesPath\s+"(.+)"' } |
        Select-Object -First 1
    if ($pathLine -notmatch '^\s*InstalledPackagesPath\s+"(.+)"') {
        throw "InstalledPackagesPath was not found in UserCfg.opt."
    }
    $InstalledPackagesPath = $Matches[1]
}

$communityRoot = Join-Path $InstalledPackagesPath "Community"
$communityFull = [IO.Path]::GetFullPath($communityRoot)
$target = Join-Path $communityFull $packageName
$targetFull = [IO.Path]::GetFullPath($target)
$requiredPrefix = $communityFull.TrimEnd('\') + '\'
if (-not $targetFull.StartsWith(
        $requiredPrefix,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to install outside the resolved MSFS Community directory."
}

if (-not (Test-Path -LiteralPath $communityFull)) {
    throw "MSFS Community directory does not exist: '$communityFull'."
}

if (Test-Path -LiteralPath $targetFull) {
    Remove-Item -LiteralPath $targetFull -Recurse -Force
}

$appTarget = Join-Path $targetFull "html_ui\efb_ui\efb_apps\VfoEfbV11"
New-Item -ItemType Directory -Path $appTarget -Force | Out-Null
Copy-Item -Path (Join-Path $distRoot "*") -Destination $appTarget -Recurse -Force
$contentInfoTarget = Join-Path $targetFull "ContentInfo\$packageName"
New-Item -ItemType Directory -Path $contentInfoTarget -Force | Out-Null
Copy-Item -Path (Join-Path $contentInfoRoot "*") `
    -Destination $contentInfoTarget -Recurse -Force

$contentFiles = Get-ChildItem -LiteralPath $targetFull -Recurse -File |
    Where-Object { $_.Name -notin @("layout.json", "manifest.json") }
$layoutContent = foreach ($file in $contentFiles) {
    $relativePath = $file.FullName.Substring($targetFull.Length + 1).Replace('\', '/').ToLowerInvariant()
    [ordered]@{
        path = $relativePath
        size = $file.Length
        date = $file.LastWriteTimeUtc.ToFileTimeUtc()
    }
}

$totalSize = ($contentFiles | Measure-Object -Property Length -Sum).Sum
$manifest = [ordered]@{
    dependencies = @()
    content_type = "MISC"
    title = "MSFS 2024 Virtual First Officer EFB"
    manufacturer = "noscapect"
    creator = "noscapect"
    package_version = "0.2.11"
    minimum_game_version = "1.7.0"
    minimum_compatibility_version = "1.0.0.0"
    export_type = "Community"
    builder = "MSFS 2024 Virtual First Officer"
    package_order_hint = "MISC"
    release_notes = [ordered]@{
        neutral = [ordered]@{
            LastUpdate = "Release 0.2.11: clear taxi-to-holding-point guidance while Flow 6 is gated."
            OlderHistory = ""
        }
    }
    total_package_size = "$totalSize"
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$layoutJson = [ordered]@{ content = @($layoutContent) } |
    ConvertTo-Json -Depth 6
$manifestJson = $manifest | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText(
    (Join-Path $targetFull "layout.json"),
    $layoutJson,
    $utf8NoBom)
[IO.File]::WriteAllText(
    (Join-Path $targetFull "manifest.json"),
    $manifestJson,
    $utf8NoBom)

Write-Output "Installed '$packageName' to '$targetFull'."
Write-Output "Restart MSFS 2024 to load the new EFB application."

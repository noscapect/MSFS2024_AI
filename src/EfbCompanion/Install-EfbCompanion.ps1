param(
    [string]$InstalledPackagesPath
)

$ErrorActionPreference = "Stop"
$packageName = "noscapect-vfo-efb"
$distRoot = Join-Path $PSScriptRoot "PackageSources\VfoEfb\dist"
if (-not (Test-Path -LiteralPath $distRoot)) {
    throw "The EFB app has not been built. Run Build-EfbCompanion.ps1 first."
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

$appTarget = Join-Path $targetFull "html_ui\efb_ui\efb_apps\VfoEfb"
New-Item -ItemType Directory -Path $appTarget -Force | Out-Null
Copy-Item -Path (Join-Path $distRoot "*") -Destination $appTarget -Recurse -Force

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
    package_version = "0.1.0"
    minimum_game_version = "1.7.0"
    minimum_compatibility_version = "1.0.0.0"
    export_type = "Community"
    builder = "MSFS 2024 Virtual First Officer"
    package_order_hint = "MISC"
    release_notes = [ordered]@{
        neutral = [ordered]@{
            LastUpdate = "Initial EFB companion integration."
            OlderHistory = ""
        }
    }
    total_package_size = "$totalSize"
}

[ordered]@{ content = @($layoutContent) } |
    ConvertTo-Json -Depth 6 |
    Set-Content -LiteralPath (Join-Path $targetFull "layout.json") -Encoding UTF8
$manifest |
    ConvertTo-Json -Depth 6 |
    Set-Content -LiteralPath (Join-Path $targetFull "manifest.json") -Encoding UTF8

Write-Output "Installed '$packageName' to '$targetFull'."
Write-Output "Restart MSFS 2024 to load the new EFB application."

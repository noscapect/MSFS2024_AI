param(
    [string]$Repository = "noscapect/MSFS2024_AI",
    [switch]$Draft,
    [switch]$Prerelease
)

$ErrorActionPreference = "Stop"

$workspace = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot ".."))
Set-Location $workspace

$projectExecutable = Join-Path $workspace "src\Copilot\bin\Release\net472\Copilot.exe"
$runningProjectApplication = Get-Process Copilot -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $projectExecutable }
if ($runningProjectApplication) {
    throw "Close the running project Copilot application before publishing a release."
}

function Invoke-GitHubJson {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Method,
        [Parameter(Mandatory = $true)]
        [string]$Uri,
        [Parameter(Mandatory = $true)]
        [hashtable]$Headers,
        [object]$Body
    )

    $parameters = @{
        Method = $Method
        Uri = $Uri
        Headers = $Headers
    }
    if ($null -ne $Body) {
        $parameters.Body = $Body | ConvertTo-Json -Depth 8
        $parameters.ContentType = "application/json"
    }
    Invoke-RestMethod @parameters
}

$projectPath = Join-Path $workspace "src\Copilot\Copilot.csproj"
[xml]$project = Get-Content -LiteralPath $projectPath
$version = [string]$project.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Copilot.csproj does not define a Version."
}
$tag = "v$version"

$branch = git branch --show-current
if ($LASTEXITCODE -ne 0 -or $branch -ne "main") {
    throw "Releases must be published from the main branch (current: $branch)."
}

$status = git status --porcelain
if ($LASTEXITCODE -ne 0) {
    throw "Git status failed."
}
if ($status) {
    throw "The Git working tree must be clean before publishing a release."
}

if (git tag --list $tag) {
    throw "Git tag $tag already exists."
}

dotnet build .\src\Copilot\Copilot.csproj -c Release --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "Release build failed."
}
dotnet test .\tests\Copilot.Tests\Copilot.Tests.csproj -c Release --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "Automated tests failed."
}

powershell.exe -NoProfile -ExecutionPolicy Bypass `
    -File .\src\EfbCompanion\Build-EfbCompanion.ps1
if ($LASTEXITCODE -ne 0) {
    throw "EFB companion build failed."
}

$artifactRoot = Join-Path $workspace "artifacts\release"
$packageName = "MSFS2024-AI-First-Officer-$tag"
$stageRoot = Join-Path $artifactRoot $packageName
$zipPath = Join-Path $artifactRoot "$packageName.zip"
$checksumPath = "$zipPath.sha256"
if (Test-Path -LiteralPath $artifactRoot) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $stageRoot | Out-Null

$outputRoot = Join-Path $workspace "src\Copilot\bin\Release\net472"
Copy-Item -LiteralPath (Join-Path $outputRoot "Copilot.exe") -Destination $stageRoot
Copy-Item -LiteralPath (Join-Path $outputRoot "Copilot.exe.config") -Destination $stageRoot
Copy-Item -LiteralPath (Join-Path $outputRoot "Microsoft.FlightSimulator.SimConnect.dll") -Destination $stageRoot
Copy-Item -LiteralPath (Join-Path $outputRoot "SimConnect.dll") -Destination $stageRoot
Copy-Item -LiteralPath (Join-Path $outputRoot "Assets") -Destination $stageRoot -Recurse
Copy-Item -LiteralPath (Join-Path $workspace "README.md") -Destination $stageRoot
Copy-Item -LiteralPath (Join-Path $workspace "docs\RELEASE_NOTES.md") -Destination $stageRoot

$efbPackagesRoot = Join-Path $stageRoot "EFB Community Package"
New-Item -ItemType Directory -Force `
    -Path (Join-Path $efbPackagesRoot "Community") | Out-Null
powershell.exe -NoProfile -ExecutionPolicy Bypass `
    -File .\src\EfbCompanion\Install-EfbCompanion.ps1 `
    -InstalledPackagesPath $efbPackagesRoot
if ($LASTEXITCODE -ne 0) {
    throw "EFB Community package staging failed."
}

$installText = @"
MSFS 2024 Virtual First Officer $version

Requirements:
- Windows 10 or Windows 11
- Microsoft Flight Simulator 2024
- A supported aircraft profile
- MobiFlight WASM module installed for Airbus aircraft
- .NET Framework 4.7.2 or newer

The required Microsoft SimConnect client libraries are included. Start MSFS
2024, load a supported aircraft, and run Copilot.exe.

Optional in-simulator EFB companion:
1. Close MSFS 2024.
2. Copy the noscapect-vfo-efb folder from
   "EFB Community Package\Community" into your MSFS 2024 Community folder.
3. Start MSFS 2024, open the aircraft EFB app library, and select
   "Virtual First Officer".

The desktop app remains fully functional without the EFB, GSX Pro,
SayIntentions, or SimBrief. Every integration is optional.

Project:
https://github.com/$Repository
"@
Set-Content -LiteralPath (Join-Path $stageRoot "INSTALL.txt") -Value $installText -Encoding UTF8

Compress-Archive -LiteralPath $stageRoot -DestinationPath $zipPath -Force
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $checksumPath -Value "$hash  $([System.IO.Path]::GetFileName($zipPath))" -Encoding ASCII

$credentialInput = "protocol=https`nhost=github.com`n`n"
$credential = $credentialInput | git credential fill
$passwordLine = $credential |
    Where-Object { $_ -like "password=*" } |
    Select-Object -First 1
if (-not $passwordLine) {
    throw "No GitHub credential is available in Windows Credential Manager."
}
$token = $passwordLine.Substring("password=".Length)
$headers = @{
    Authorization = "Bearer $token"
    Accept = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
    "User-Agent" = "MSFS2024-AI-release-publisher"
}

$previousTag = git tag --sort=-creatordate |
    Select-Object -First 1
$logRange = if ($previousTag) { "$previousTag..HEAD" } else { "HEAD" }
$changes = git log $logRange --pretty=format:"- %s"
$releaseNotesPath = Join-Path $workspace "docs\RELEASE_NOTES.md"
$highlights = if (Test-Path -LiteralPath $releaseNotesPath) {
    Get-Content -LiteralPath $releaseNotesPath -Raw
}
else {
    "## Changes`n`n$changes"
}
$releaseNotes = @"
## MSFS 2024 Virtual First Officer $version

$highlights

### Installation

Download the ZIP, extract all files into one folder, and follow `INSTALL.txt`.
The matching Microsoft SimConnect client libraries are included.
The optional MSFS 2024 EFB Community package is included in the archive.

### Verification

- Release build completed successfully.
- Automated test suite passed.
- SHA-256 checksum is attached.
"@

git tag -a $tag -m "Release $tag"
if ($LASTEXITCODE -ne 0) {
    throw "Could not create tag $tag."
}
git push origin $tag
if ($LASTEXITCODE -ne 0) {
    throw "Could not push tag $tag."
}

$release = Invoke-GitHubJson `
    -Method Post `
    -Uri "https://api.github.com/repos/$Repository/releases" `
    -Headers $headers `
    -Body @{
        tag_name = $tag
        target_commitish = "main"
        name = "MSFS 2024 Virtual First Officer $version"
        body = $releaseNotes
        draft = [bool]$Draft
        prerelease = [bool]$Prerelease
        generate_release_notes = $false
    }

foreach ($assetPath in @($zipPath, $checksumPath)) {
    $assetName = [System.IO.Path]::GetFileName($assetPath)
    $uploadUri =
        "https://uploads.github.com/repos/$Repository/releases/$($release.id)/assets" +
        "?name=$([uri]::EscapeDataString($assetName))"
    Invoke-RestMethod `
        -Method Post `
        -Uri $uploadUri `
        -Headers @{
            Authorization = "Bearer $token"
            Accept = "application/vnd.github+json"
            "X-GitHub-Api-Version" = "2022-11-28"
            "User-Agent" = "MSFS2024-AI-release-publisher"
        } `
        -ContentType "application/octet-stream" `
        -InFile $assetPath | Out-Null
}

Remove-Variable token
Write-Host "Published $($release.html_url)"
Write-Host "Package: $zipPath"
Write-Host "SHA-256: $hash"


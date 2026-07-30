param(
    [string]$SdkRoot = "C:\MSFS 2024 SDK"
)

$ErrorActionPreference = "Stop"
$projectRoot = $PSScriptRoot
$sourceRoot = Join-Path $projectRoot "PackageSources"
$appRoot = Join-Path $sourceRoot "VfoEfb"
$sdkCache = Join-Path $sourceRoot ".sdk"
$sdkEfbSource = Join-Path $SdkRoot "Samples\DevmodeProjects\EFB\PackageSources\efb_api"
$sdkVendorSource = Join-Path $SdkRoot "Samples\DevmodeProjects\EFB\PackageSources\vendor"

if (-not (Test-Path -LiteralPath $sdkEfbSource)) {
    throw "MSFS 2024 EFB SDK files were not found under '$SdkRoot'."
}

New-Item -ItemType Directory -Path $sdkCache -Force | Out-Null
Copy-Item -LiteralPath $sdkEfbSource -Destination $sdkCache -Recurse -Force
Copy-Item -LiteralPath $sdkVendorSource -Destination $sdkCache -Recurse -Force

Push-Location $appRoot
try {
    & npm.cmd install
    if ($LASTEXITCODE -ne 0) {
        throw "npm install failed with exit code $LASTEXITCODE."
    }

    & npm.cmd run build
    if ($LASTEXITCODE -ne 0) {
        throw "EFB TypeScript build failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

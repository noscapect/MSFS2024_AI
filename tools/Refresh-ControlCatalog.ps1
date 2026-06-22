$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass `
        -File ".\tools\Build-ControlMatrix.ps1"
    if ($LASTEXITCODE -ne 0) {
        throw "Control-matrix generation failed."
    }

    $matrix = Import-Csv -LiteralPath ".\docs\control-matrix.csv"
    $duplicates = $matrix |
        Group-Object Preset |
        Where-Object Count -gt 1
    if ($duplicates) {
        $names = ($duplicates | ForEach-Object Name) -join ", "
        throw "Duplicate preset names found: $names"
    }

    $requiredPresets = @(
        "Battery_1_On",
        "Battery_1_Off",
        "Battery_2_On",
        "Battery_2_Off"
    )
    $available = @{}
    foreach ($row in $matrix) {
        $available[$row.Preset] = $true
    }
    $missing = $requiredPresets | Where-Object { -not $available.ContainsKey($_) }
    if ($missing) {
        throw "Required documented presets are missing: $($missing -join ', ')"
    }

    & powershell.exe -NoProfile -ExecutionPolicy Bypass `
        -File ".\tools\Generate-ControlCatalog.ps1"
    if ($LASTEXITCODE -ne 0) {
        throw "C# control-catalog generation failed."
    }

    Write-Output(
        "Control catalog refreshed: " +
        "$($matrix.Count) documented presets; required cockpit controls present.")
}
finally {
    Pop-Location
}

param(
    [string]$InputPath = "docs\events.txt",
    [string]$CsvPath = "docs\control-matrix.csv"
)

$category = ""
$rows = foreach ($line in Get-Content -LiteralPath $InputPath) {
    $trimmed = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed)) { continue }

    if ($trimmed.StartsWith("//")) {
        $category = ($trimmed -replace '^//IniBuilds/A320/', '').Trim()
        continue
    }

    $parts = $trimmed -split '#', 2
    if ($parts.Count -ne 2) { continue }

    $name = $parts[0].Trim()
    $code = $parts[1].Trim()
    $interfaces = @()
    if ($code -match '\(>L:') { $interfaces += "LVar" }
    if ($code -match '\(>K:') { $interfaces += "KEvent" }
    if ($code -match '\(>B:') { $interfaces += "BEvent" }
    if ($code -match '\(A:')  { $interfaces += "SimVarRead" }

    $writeTargets = [regex]::Matches($code, '\(>([LBK]):([^)]+)\)') |
        ForEach-Object { "$($_.Groups[1].Value):$($_.Groups[2].Value)" } |
        Select-Object -Unique
    $readTargets = [regex]::Matches($code, '\(([LA]):([^,)]+)(?:,[^)]+)?\)') |
        ForEach-Object { "$($_.Groups[1].Value):$($_.Groups[2].Value)" } |
        Select-Object -Unique

    $phase = switch -Regex ("$category $name") {
        'Battery|Electrical' { "Cockpit Preparation"; break }
        'Strobe|Beacon|Seat|Door|APU|Fuel|Engine' { "Before Start / Engine Start"; break }
        'EFIS|MCDU|Autopilot' { "Cockpit Preparation / Flight"; break }
        'Brake|Spoiler' { "Before Start / Taxi"; break }
        default { "Other" }
    }

    [pscustomobject]@{
        Category = $category
        Preset = $name
        CalculatorCode = $code
        Interfaces = ($interfaces -join "+")
        WriteTargets = ($writeTargets -join ";")
        ReadTargets = ($readTargets -join ";")
        RelevantPhase = $phase
        Source = "MobiFlight HubHop export supplied in docs/events.txt"
        Evidence = "Documented preset"
        LiveVerified = "No"
    }
}

$rows | Export-Csv -LiteralPath $CsvPath -NoTypeInformation -Encoding UTF8
Write-Output "Wrote $($rows.Count) controls to $CsvPath"

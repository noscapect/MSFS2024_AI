# MSFS 2024 EFB Companion

## Status

The EFB companion is in the current unreleased source. Its TypeScript build,
Community package layout, desktop bridge, and protocol tests are complete. It
still requires an in-simulator interaction pass after restarting MSFS 2024.

## What it does

The EFB app provides:

- Current flow, step, crew role, waiting reason, and completion percentage
- Start-next-flow, manual flow selection, confirm, pause, resume, and cancel
  controls
- Gate-to-gate flow status and next-flow selection
- Aircraft phase and AGL/altitude/airspeed/vertical-speed telemetry
- GSX live status, boarding percentage, and action-required prompts
- Connection and stale-state warnings

`Copilot.exe` remains mandatory. The EFB does not contain aircraft mappings or
procedure logic.

## Build

Requirements:

- MSFS 2024 SDK installed at `C:\MSFS 2024 SDK`, or pass another `-SdkRoot`
- Node.js 18 or newer
- NPM

From the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\src\EfbCompanion\Build-EfbCompanion.ps1
```

The script copies the installed SDK's EFB API and SDK package into a local,
ignored cache, installs pinned build dependencies, type-checks the app, and
builds `PackageSources\VfoEfb\dist`.

## Install

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\src\EfbCompanion\Install-EfbCompanion.ps1
```

The installer resolves `InstalledPackagesPath` from the MSFS 2024
`UserCfg.opt`, validates that the destination remains below the Community
directory, adds the required `ContentInfo`, and generates `manifest.json` and
`layout.json` as UTF-8 without a byte-order mark before installing:

```text
Community\noscapect-vfo-efb
```

Restart MSFS after installing or updating the Community package. Open the
simulator EFB app list and select **Virtual First Officer**. Start
`Copilot.exe` before using its controls.

The current development app uses the versioned internal identity `VfoEfbV5`
and displays EFB build `0.2.3`. MSFS applies the internal name to the host
`.efb-view` element, and the generated stylesheet is deliberately scoped to
`.efb-view.VfoEfbV5`. Increment this identity when a simulator-level asset
cache must be invalidated.

## CommBus protocol

Protocol version: `2`

| Direction | Event |
| --- | --- |
| EFB to desktop | `VFO_EFB_COMMAND_V2` |
| Desktop to EFB | `VFO_EFB_STATE_V2` |

Allowed actions are:

- `request_state`
- `start_flow` with a current-aircraft flow ID
- `start_next_flow`
- `confirm`
- `pause`
- `resume`
- `cancel`

Every command carries a request ID and protocol version. The desktop sends an
accepted/rejected result and continues publishing authoritative state
snapshots. Unknown actions, protocol versions, flow IDs, and operations that
are invalid for the current procedure state are rejected.

## Required live test

After restarting MSFS:

1. Confirm **Virtual First Officer** appears in the EFB app list.
2. Open it with the desktop app stopped and verify the disconnected state.
3. Start `Copilot.exe` and verify connection within five seconds.
4. Start a safe ground flow from the EFB.
5. Verify Confirm is enabled only for a manual-action step.
6. Exercise pause and resume.
7. Cancel a test flow and verify the confirmation prompt.
8. Check live aircraft telemetry and GSX boarding progress.
9. Restart the desktop app and verify EFB reconnection.
10. Confirm no raw cockpit command can be submitted from the EFB.

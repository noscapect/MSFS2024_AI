# MSFS 2024 EFB Companion

## Status

The optional EFB companion ships with desktop release 0.9.6 as EFB build
0.2.10. Its TypeScript build, Community package layout, desktop bridge, and
protocol tests are complete; continued in-simulator field validation remains
part of normal beta testing.

## What it does

The EFB app provides:

- Current flow, step, crew role, waiting reason, and completion percentage
- Start-next-flow, manual flow selection, confirm, pause, resume, and cancel
  controls
- Gate-to-gate flow status and next-flow selection
- Aircraft phase and AGL/altitude/airspeed/vertical-speed telemetry
- GSX live status, boarding percentage, and action-required prompts
- Selectable responses for active non-root GSX menus, including tug prompts;
  GSX hide-panel events retain the pending choices until GSX explicitly closes
  or times out the question
- GSX-aware pushback/start-clearance gating while boarding is incomplete
- Connection and stale-state warnings

`Copilot.exe` remains mandatory. The EFB does not contain aircraft mappings or
procedure logic.

Background synchronization is bounded to one outstanding state request. State
refreshes return only a state envelope and are not displayed as command
results; compatibility handling also ignores refresh acknowledgements from
older desktop builds. This prevents the acknowledgement/request feedback loop
observed during the 2026-07-31 engine-start test.

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

The current app uses the versioned internal identity `VfoEfbV11`
and displays EFB build `0.2.10`. MSFS applies the internal name to the host
`.efb-view` element, and the generated stylesheet is deliberately scoped to
`.efb-view.VfoEfbV11`. Increment this identity when a simulator-level asset
cache must be invalidated.

## CommBus protocol

Protocol version: `2`

| Direction | Event |
| --- | --- |
| EFB to desktop | `VFO_EFB_COMMAND_V2` |
| Desktop to EFB | `VFO_EFB_STATE_V2` |

Allowed actions are:

- `request_state`
- `start_flow` with the next eligible current-aircraft flow ID
- `start_next_flow`
- `gsx_open_menu`
- `gsx_menu_choice` with an index from the currently open non-root GSX prompt
- `confirm`
- `pause`
- `resume`
- `cancel`

Every command carries a request ID and protocol version. The desktop sends an
accepted/rejected result and continues publishing authoritative state
snapshots. Unknown actions, protocol versions, flow IDs, and operations that
are invalid for the current procedure state are rejected.

Only the next incomplete flow is selectable and accepted. This prevents the
EFB from skipping operational prerequisites even if it holds stale state.
Flow 5 also uses signed longitudinal movement, so backward GSX pushback cannot
be mistaken for the captain commencing forward taxi.

The GSX card presents choices while a remote prompt is active and provides an
**Open GSX menu** control when no choices are available. A new GSX question
replaces any older desktop prompt, and timed-out questions are removed from the
EFB instead of remaining as an action that can no longer be answered.

Routine state publication is coalesced during high-rate engine telemetry. If
the simulator nevertheless delivers one malformed background snapshot, the
EFB retains its last valid display and requests a replacement without showing
the event as a failed pilot command.

Flow 6 is locked until forward taxi at 3 knots or more has been observed and
the aircraft subsequently stops with both engines running. Automatic Flow
5-to-6 chaining waits for that holding-point latch, and every aircraft's Flow
6 begins with the same non-bypassable observation.

## Required live test

After restarting MSFS:

1. Confirm **Virtual First Officer** appears in the EFB app list.
2. Open it with the desktop app stopped and verify the disconnected state.
3. Start `Copilot.exe` and verify connection within five seconds.
4. Start a safe ground flow from the EFB.
5. Click the left, center, and right portions of the wide recommended-action
   button and verify its complete visible surface is interactive.
6. Verify Confirm is enabled only for a manual-action step.
7. During Flow 3, verify Confirm remains disabled while GSX boarding is below
   its total and becomes available only after boarding completes.
8. Exercise pause and resume.
9. Cancel a test flow and verify the confirmation prompt.
10. Check live aircraft telemetry and GSX boarding progress.
11. Trigger a GSX secondary prompt such as **Attach Pushback Tug now?** and
   answer it from the EFB; verify the prompt closes and GSX continues.
12. Restart the desktop app and verify EFB reconnection.
13. Confirm no raw cockpit command can be submitted from the EFB.

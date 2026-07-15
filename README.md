# MSFS 2024 Virtual First Officer

A Windows first-officer companion for supported aircraft in Microsoft Flight
Simulator 2024:

- **iniBuilds A320neo V2**
- **iniBuilds A321LR**
- **iniBuilds A330** *(experimental/work in progress)*
- **FlyByWire A32NX**
- **PMDG 737-800**

Application icon artwork contributed by the project owner.
The icon is embedded in the executable and assigned to the running WinForms
window so it is also used by the Windows taskbar.

The application connects to the simulator through SimConnect, the installed
MobiFlight WASM module for Airbus aircraft, and the PMDG NG3 SDK client-data
channel for PMDG 737 support. It guides a complete 12-flow
gate-to-gate flight, automates verified First Officer actions, monitors
Captain actions, and speaks important callouts.

> This is an independent community project. It is not affiliated with or
> endorsed by Microsoft, Asobo Studio, iniBuilds, FlyByWire Simulations,
> PMDG, Boeing, Airbus, or MobiFlight.

> **Beta notice:** Versions below 1.0 are development releases. Stable aircraft
> profiles have completed live validation, while profiles explicitly marked
> experimental may contain incomplete flows and require manual intervention.

## Current capabilities

- Complete flow from cold and dark through shutdown
- Automatic First Officer switch and lever actions where reliable aircraft
  commands and independent readback are available
- Monitoring of Captain actions without requiring app confirmations during
  takeoff, approach, landing, or taxi
- Native iniBuilds Airbus-family and FlyByWire A32NX state monitoring through
  MobiFlight
- PMDG 737-800 aircraft-family routing, Boeing procedures and PMDG NG3 SDK
  state/control integration
- Verification after every automatic aircraft action
- Optional Windows offline voice callouts
- Persistent preflight settings for V1, VR, and transition altitude
- Optional free SimBrief latest-OFP import by Pilot ID or username, with a
  per-flight operational briefing, mismatch/staleness warnings, block-fuel
  comparison, aircraft-specific flap normalization, PMDG FMC takeoff-data
  comparison, and planned-cruise detection
- A distance-aware standard approach schedule that can be overridden for flap,
  gear, and landing-configuration gates
- Aircraft-specific standard approach profiles selected automatically for the
  loaded aircraft, with separately saved airline-SOP overrides per profile
- Configurable automatic chaining between Flow 6 to 7, Flow 10 to 11, Flow 11
  to 12, and optional earlier-flow handoffs
- One-second flight telemetry recording with 10x replay for procedure testing;
  only the latest three flights are retained
- Prioritized voice-callout queue that prevents overlapping speech
- Automatic flow recommendation based on the current flight phase
- Saved active-procedure sessions that resume after app or simulator restarts
- A `New flight / Reset progress` control that clears restored flow progress
  without changing settings or deleting flight recordings
- Late-start recovery for transient engine-start and takeoff milestones
- Current-step telemetry with the active altitude, speed, configuration, and
  trigger thresholds
- Readback sanity checks comparing flap-handle and flap-surface telemetry
- Visible application version and GitHub release update status
- Monitor-only and confirmation-based automation modes
- Runtime activity log and diagnostic status display
- Quiet diagnostic capture and export for verification failures without
  cluttering the normal player-facing activity log

Voice callouts include engine-start monitoring, takeoff calls, landing gear up
and down, minimums, spoilers, reverse green, and deceleration.

The gameplay flow is defined in [docs/checklist.md](docs/checklist.md).
Planned and deliberately deferred features are tracked in
[docs/ROADMAP.md](docs/ROADMAP.md). The design and implemented first scope for
optional SimBrief support is documented in
[docs/SIMBRIEF_INTEGRATION_PLAN.md](docs/SIMBRIEF_INTEGRATION_PLAN.md).

The iniBuilds A321LR profile has completed live validation of all twelve flows.
Its procedures, checklists, flap mappings, and sign-selector policy are kept in
an aircraft-specific profile and protected by regression tests. See
[docs/A321_SUPPORT_STATUS.md](docs/A321_SUPPORT_STATUS.md).

## Supported aircraft

The application has moved from a single-aircraft A320 assistant to a
multi-aircraft virtual first officer. Current aircraft profiles are:

- iniBuilds A320neo V2
- iniBuilds A321LR
- iniBuilds A330 *(experimental/work in progress; Flows 1-4 live validated)*
- FlyByWire A32NX for MSFS 2024
- PMDG 737-800

The application deliberately avoids guessed generic events for unsupported
aircraft controls.

## Requirements

To run the application:

- Windows 10 or Windows 11
- Microsoft Flight Simulator 2024
- A supported aircraft profile: iniBuilds A320neo V2, iniBuilds A321LR,
  iniBuilds A330, FlyByWire A32NX for MSFS 2024, or PMDG 737-800
- MobiFlight WASM module installed in MSFS
- .NET Framework 4.7.2 or newer

PMDG 737-800 support also requires PMDG's SDK data broadcast to be enabled in
the aircraft options file. See
[docs/PMDG_737_SUPPORT_PLAN.md](docs/PMDG_737_SUPPORT_PLAN.md).

GitHub release packages include the matching managed and native Microsoft
SimConnect client libraries. End users do not need the MSFS SDK.

To build from source:

- .NET SDK 10 or newer
- MSFS 2024 SDK installed at `C:\MSFS 2024 SDK`

The project currently references the SDK's SimConnect libraries from that
default location.

## Build

```powershell
dotnet build .\src\Copilot\Copilot.csproj -c Release
```

The executable is created at:

```text
src\Copilot\bin\Release\net472\Copilot.exe
```

## Run

Start MSFS 2024, load a supported aircraft, and then run:

```powershell
.\src\Copilot\bin\Release\net472\Copilot.exe
```

The dashboard lets you select and start each flow, choose the automation
policy, enable voice callouts, pause or cancel a procedure, and inspect live
aircraft status. `Approach & chaining settings` opens the configurable
approach schedule and flow-handoff options. Completed recordings can be
selected at the bottom of the dashboard and replayed at 10x speed; replay
never transmits cockpit commands. Use `New flight / Reset progress` before a
new sector when the previous flight's saved progress is still displayed.

Settings are stored in:

```text
%LOCALAPPDATA%\MSFS2024_AI\settings.xml
```

Runtime logs are stored in:

```text
%LOCALAPPDATA%\MSFS2024_AI\logs\copilot.log
```

Detailed verification-failure diagnostics are stored quietly in:

```text
%LOCALAPPDATA%\MSFS2024_AI\diagnostics
```

Use **Export diagnostics** or **Copy last diagnostic** in the app when
reporting a test issue. Normal users can ignore these files.

The latest three flight telemetry recordings are stored in:

```text
%LOCALAPPDATA%\MSFS2024_AI\flights
```

The resumable flight session is stored in:

```text
%LOCALAPPDATA%\MSFS2024_AI\session.xml
```

## Gate-to-gate flows

1. Power Up & Initial Setup
2. Flight Computer & Pre-Flight
3. APU Start & Pushback
4. Engine Start Sequence
5. After Start & Taxi
6. Before Takeoff
7. Takeoff & Climb
8. Cruise
9. Descent Preparation
10. Approach & Landing
11. After Landing & Taxi
12. Parking & Shutdown

ATC communication and flight-computer data entry remain pilot tasks. Automatic
STD/QNH switching is intentionally excluded because no reliable verified
aircraft interface was found.

## Safety model

Sending a command is not considered success. Each automatic action:

1. Uses a documented or Behavior Viewer-confirmed aircraft interface.
2. Waits for separate aircraft-state readback.
3. Completes only after the requested state is verified.
4. Stops the active flow if verification fails.

The app does not silently try guessed alternate events. The final cold-and-dark
section also requires explicit confirmation before electrical power is removed.

## Diagnostic probe

`SimConnectProbe` is a read-only development utility for inspecting aircraft
Input Events and native variables:

```powershell
dotnet build .\src\SimConnectProbe\SimConnectProbe.csproj -c Release
.\src\SimConnectProbe\bin\Release\net472\SimConnectProbe.exe list-lvars
```

See [docs/NATIVE_CONTROL_STRATEGY.md](docs/NATIVE_CONTROL_STRATEGY.md),
[docs/FBW_A32NX_SUPPORT_PLAN.md](docs/FBW_A32NX_SUPPORT_PLAN.md), and
[docs/LIVE_TESTS.md](docs/LIVE_TESTS.md) for control evidence and test
history.

## Automated tests

Procedure recovery and aircraft-state sanity checks can be run without MSFS:

```powershell
dotnet test .\tests\Copilot.Tests\Copilot.Tests.csproj -c Release
```

## Publishing a release

Maintainers can build, test, package, tag, and publish the current version with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\Publish-Release.ps1
```

See [docs/RELEASING.md](docs/RELEASING.md).

## Project structure

- `src/Copilot` — WinForms application, simulator integration, procedures, and UI
- `src/SimConnectProbe` — read-only simulator discovery utility
- `docs` — checklist, architecture, mappings, and live-test evidence
- `tools` — control-catalog generation scripts

## Development status

The iniBuilds A320neo V2 and fully live-validated iniBuilds A321LR flows are
mature baselines. FlyByWire A32NX and PMDG 737-800 are supported through their
own procedure paths. iniBuilds A330 is included as an experimental beta
profile: Flows 1-4 passed live testing, Flow 5 and Flow 6 remain partially
validated, and Flows 7-12 are pending. Remain ready to operate an experimental
aircraft manually.


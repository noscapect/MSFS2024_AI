# MSFS 2024 AI First Officer - Project Handoff

Use this file as the starting context in a new chat.

Suggested opening prompt:

> Continue the project described in `C:\CODE\MSFS2024_AI\PROJECT.md`. Read that
> file and the referenced documents first. Work documentation-first, avoid
> speculative switch testing, and keep the iniBuilds A320neo V2 as the only
> supported aircraft for now.

## Product goal

Build a normal external Windows application that acts as a virtual first
officer for the iniBuilds A320neo V2 in Microsoft Flight Simulator 2024.

The eventual scope is gate-to-gate:

- Cold-and-dark cockpit preparation
- Before start
- Engine start
- After start
- Taxi and before takeoff
- Takeoff, climb, cruise, descent, approach, landing
- Taxi-in, shutdown, and deboarding

The immediate milestone remains reliable checklist/flow assistance through
cruise. Controls must actually operate the aircraft and independently verify
the resulting state. Hermes may be used as a development tool but must not be
a runtime dependency.

## Current application

- Project: `src\Copilot\Copilot.csproj`
- Framework: WinForms, .NET Framework 4.7.2, x64
- Output: `src\Copilot\bin\Release\net472\Copilot.exe`
- The project builds successfully with no warnings or errors.
- The application has a normal dashboard, not only a console.
- It connects through SimConnect and uses the installed MobiFlight WASM event
  module for aircraft-specific calculator code and native-variable readback.
- Settings are persisted under `%LOCALAPPDATA%\MSFS2024_AI`.
- Runtime log:
  `%LOCALAPPDATA%\MSFS2024_AI\logs\copilot.log`

Build:

```powershell
dotnet build .\src\Copilot\Copilot.csproj -c Release --no-restore
```

Run:

```powershell
.\src\Copilot\bin\Release\net472\Copilot.exe
```

MSFS and the iniBuilds A320neo V2 must already be loaded. Desktop-session
access matters: a sandboxed/background process may not be able to reach the
running simulator even when MSFS is open.

## Architecture

Important files:

- `src\Copilot\CopilotService.cs` — SimConnect, MobiFlight, commands, UI,
  state monitoring, and action verification
- `src\Copilot\AircraftState.cs` — normalized aircraft state
- `src\Copilot\Procedures\A320ProcedureLibrary.cs` — flows through cruise
- `src\Copilot\Procedures\ProcedureRunner.cs` — procedure orchestration
- `src\Copilot\Checklists\A320ChecklistLibrary.cs` — checklist verification
- `src\Copilot\Domain\AircraftCapabilities.cs` — honest support status
- `src\Copilot\Controls\IniBuildsA320ControlCatalog.Generated.cs` —
  generated documented-control catalog
- `docs\events.txt` — user-supplied iniBuilds A320 HubHop export
- `docs\control-matrix.csv` — parsed control matrix
- `docs\behavior-viewer-mappings.csv` — sourced native-control evidence
- `docs\before-start-input-event-inspection.tsv` — read-only Input Event metadata
- `docs\LIVE_TESTS.md` — dated end-to-end verification evidence
- `docs\NATIVE_CONTROL_STRATEGY.md` — mandatory command/state control pattern
- `docs\CONTROL_MAPPING.md` — mapping evidence and readiness
- `docs\ARCHITECTURE.md`
- `docs\PRODUCT_VISION.md`
- `docs\FSFO_BENCHMARK.md`

MobiFlight protocol currently used:

- Init areas: `MobiFlight.Command`, `MobiFlight.Response`
- Calculator command: `MF.SimVars.Set.<calculator code>`
- Runtime client: `MSFS2024_AI_Copilot`
- Runtime LVar/BVar monitoring is registered through the client's `LVars`
  data area.

Aircraft-specific controls should use native iniBuilds command/state LVar
pairs whenever available. Command LVars are pulsed through MobiFlight and
verified against separate native state LVars. See
`docs\NATIVE_CONTROL_STRATEGY.md`.

## Documentation-driven control workflow

Do not guess control expressions repeatedly in the live simulator.

After changing `docs\events.txt`, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Refresh-ControlCatalog.ps1
```

This:

1. Parses `events.txt`
2. Regenerates `docs\control-matrix.csv`
3. Checks duplicate names and required battery controls
4. Regenerates the C# control catalog

Current catalog size: 249 documented presets.

Evidence classifications:

- Documented preset
- Aircraft-discovered Input Event
- Live verified
- Inferred/unverified

Only documented or Behavior-Viewer-confirmed commands with independent
readback should enter automatic flows.

## Live-verified controls

### BAT 1

- ON: `1 (>L:INI_OVHD_ELEC_BAT_1_PB_IS_AUTO_SWITCH)`
- OFF: `0 (>L:INI_OVHD_ELEC_BAT_1_PB_IS_AUTO_SWITCH)`
- Readback: `INI_OVHD_ELEC_BAT_1_PB_IS_AUTO_SWITCH`
- Documented in `events.txt`
- Live verified

### BAT 2

- ON: `1 (>L:INI_OVHD_ELEC_BAT_2_PB_IS_AUTO_SWITCH)`
- OFF: `0 (>L:INI_OVHD_ELEC_BAT_2_PB_IS_AUTO_SWITCH)`
- Readback: `INI_OVHD_ELEC_BAT_2_PB_IS_AUTO_SWITCH`
- Documented in the updated `events.txt`
- Live verified

### External power

- Command: SimConnect `SET_EXTERNAL_POWER`
- Readback: `EXTERNAL POWER ON:1`
- Live verified

### Beacon

- Command: SimConnect `BEACON_LIGHTS_SET`
- Readback: `LIGHT BEACON`
- Live verified

### NAV & LOGO selector

The generic MSFS `NAV_LIGHTS_SET` and `LIGHT NAV` pair was proven incorrect
for this selector and must not be used.

Behavior Viewer information supplied by the user:

- Input Event: `AIRLINER_LT_NAVLOGO`
- Explicit bindings: `STATE1`, `STATE2`, `STATE3`, `Set`, `Inc`, `Dec`
- Native variable: `INI_LOGO_LIGHT_SWITCH`

Verified mapping:

- Selector position **2**:
  - Command: ` (>B:AIRLINER_LT_NAVLOGO_STATE1)`
  - Readback: `INI_LOGO_LIGHT_SWITCH == 0`
  - Live verified by the user
- Selector **OFF**:
  - Command: ` (>B:AIRLINER_LT_NAVLOGO_STATE3)`
  - Readback: `INI_LOGO_LIGHT_SWITCH == 2`
  - Live verified by the user

Position 1 did not actuate reliably and is intentionally not used. Only
position 2 and OFF should be exposed or used in flows.

## Cockpit Preparation status

The automatic Cockpit Preparation flow:

1. Confirms A320neo V2 loaded
2. Confirms stationary on the ground
3. Confirms parking brake set
4. Confirms engines off
5. Sets BAT 1 ON and verifies native readback
6. Sets BAT 2 ON and verifies native readback
7. Connects external power and verifies readback
8. Sets NAV & LOGO to position 2 and verifies

The complete flow passed live on June 19, 2026. BAT 1 and BAT 2 changed to ON
with native readback, external power reported ON, and NAV & LOGO position 2
reported native value 0. Detailed evidence is in `docs\LIVE_TESTS.md`.

## Before Start status

Before Start exists structurally but is not ready for unattended automation.

Current steps include:

- Aircraft/stationary/electrical-power checks
- Doors closed and boarding complete — manual captain confirmation
- Six fuel-pump pushbuttons (L1/L2/C1/C2/R1/R2) — exact Behavior Viewer
  Mouserect code with independent native readback; live verified ON and OFF
- APU master/start selection — automatic native command pulses and state verification
- APU available — separate native observed condition after start/warm-up
- APU generator pushbutton — observed in its normal ON configuration; no routine press
- APU bleed ON — exact Behavior Viewer Mouserect code; live verified ON/OFF
- Beacon ON — automatic and verified
- External power disconnect — automatic only after native APU available and
  generator-on verification

All automatic cockpit actions in the current Before Start flow are mapped and
live verified. Doors closed/boarding complete remains an intentional captain
confirmation because it represents operational context, not only switch state.

## Known unreliable or unfinished areas

### Fuel pumps

- The aircraft exposes B-events such as:
  - `AIRLINER_FUEL_ENG1_L1`
  - `AIRLINER_FUEL_ENG1_L2`
  - `AIRLINER_FUEL_ENG2_R1`
  - `AIRLINER_FUEL_ENG2_R2`
- ON commands appeared to work and native B-event readback later showed all
  four ON.
- OFF did not verify reliably.
- These controls are absent from the supplied A320 HubHop export.
- They are excluded from the normal command switch and automatic flow.
- Experimental actuation code was removed from `CopilotService.cs`. Read-only
  discovery remains isolated in `SimConnectProbe`.

### APU

- Generic telemetry has produced suspicious/inconsistent results, including
  reporting an operating APU after resets.
- APU generator command verification failed.
- APU start/generator controls are not documented in the supplied A320
  HubHop export.
- Do not disconnect external power based only on APU RPM, generator-switch
  indication, or generic generator-active telemetry.
- Automatic external-power OFF is blocked while engines are stopped. Before
  Start requires manual pilot verification and disconnection until native APU
  power readback is mapped.
- Experimental APU-generator actuation code was removed from
  `CopilotService.cs`.

The read-only Input Event inspector confirms that APU master, start, bleed,
and generator events exist and accept a `FLOAT64` parameter. It does not
establish value semantics or independent native readback.

A passive subscription and polling test showed that these APU Input Events
remain at 0 even while generic APU master/RPM/generator telemetry changes.
Treat them as command endpoints, never as state readback.

Behavior Viewer later confirmed explicit `PUSH` bindings for APU master,
start, bleed, and generator. MobiFlight LVar enumeration discovered
`INI_APU_MASTER_SWITCH`, `INI_APU_START_BUTTON`, `INI_APU_BLEED_BUTTON`,
`INI_APU_GEN_ON`, and `INI_APU_AVAILABLE` for independent verification.
Implementation is ready for controlled live verification.

### Doors, ADIRS, signs, ground services

Not mapped sufficiently for automation. Boarding complete/ground crew clear
is operational context and may still require pilot confirmation even after
door-state telemetry is added.

### SimConnect/MobiFlight noise

`ALREADY_CREATED` exceptions can occur when MobiFlight client-data areas
already exist. They have been harmless but make logs noisy.

Current application strings are UTF-8 and no known mojibake remains.

## Critical lessons

Read `docs\NATIVE_CONTROL_STRATEGY.md` before implementing or testing another
aircraft-specific control.

1. Cockpit visual state and native selector variables override generic
   SimVars when they disagree.
2. A successful command call is not success; require independent readback.
3. Never mark a control supported from its name alone.
4. Prefer Behavior Viewer or a documented HubHop preset before implementation.
5. Test complete subsystems in one controlled batch rather than repeatedly
   guessing individual suffixes.
6. If a test fails, do not retry variations blindly. Update the control
   matrix/evidence first.
7. Keep Hermes out of the shipped runtime.

## Recommended next work

1. Use MSFS Behavior Viewer to document the missing Before Start controls:
   - APU master/start/available/generator
   - Six fuel-pump pushbuttons (left 1/2, center 1/2, right 1/2)
   - ADIRS selectors
   - Seat-belt signs
   - Relevant door states
2. Add each Behavior Viewer result to `docs\behavior-viewer-mappings.csv` before
   adding code.
3. Implement and batch-test Before Start only after every automatic step has
   a command, native readback, timeout, and safe failure behavior.

## Current operational note

The Copilot process may not be running because it is routinely stopped before
rebuilding to release the executable lock. Start the latest Release executable
manually when beginning a new live test.

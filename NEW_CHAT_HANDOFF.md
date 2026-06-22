# MSFS 2024 AI First Officer — Current Project State

**Authoritative handoff date:** June 20, 2026  
**Workspace:** `C:\CODE\MSFS2024_AI`

Use this document as the starting point for a new chat. It describes the code
and live-verified state after completing the first two flows. If an older note
conflicts with this file, inspect the current code and prefer this file plus
`docs/LIVE_TESTS.md`.

Suggested opening prompt:

> Continue the project in `C:\CODE\MSFS2024_AI`. Read
> `NEW_CHAT_HANDOFF.md` first, then inspect the current code. Flows 1 and 2
> contain working controls but need to be redesigned to reflect real A320
> procedures more accurately. Preserve live-verified control implementations,
> require independent readback, and do not introduce workarounds or guessed
> event names.

## Product goal

Build an external Windows first-officer application for the iniBuilds
A320neo V2 in Microsoft Flight Simulator 2024.

The application should eventually provide realistic gate-to-gate flows,
checklists, monitoring, and first-officer actions. The user now wants to
redesign the flows so that:

- Actions match real A320 crew duties and sequencing.
- Captain actions, first-officer actions, observations, and confirmations are
  distinguished.
- The application performs only actions appropriate to its intended role.
- Operational context is not replaced by switch manipulation.
- Every automatic action has a verified command and independent readback.

Do not preserve the present Flow 1/2 sequence merely because its controls work.
The controls are reusable building blocks; the flow design is the next task.

## Application and build

- Project: `src\Copilot\Copilot.csproj`
- UI: WinForms
- Target: .NET Framework 4.7.2, x64
- Simulator connection: SimConnect 12.2
- Aircraft-specific bridge: installed MobiFlight WASM module
- Supported aircraft: iniBuilds `A320neo V2`
- Release executable:
  `src\Copilot\bin\Release\net472\Copilot.exe`
- Runtime log:
  `%LOCALAPPDATA%\MSFS2024_AI\logs\copilot.log`

Build:

```powershell
dotnet build .\src\Copilot\Copilot.csproj -c Release
```

Run:

```powershell
.\src\Copilot\bin\Release\net472\Copilot.exe
```

The Release build was rebuilt successfully on June 20, 2026 with zero warnings
and zero errors.

## Important source files

- `src\Copilot\CopilotService.cs`
  - SimConnect and MobiFlight integration
  - Native state registration
  - Cockpit commands and verification
  - Dashboard and one-shot test commands
- `src\Copilot\AircraftState.cs`
  - Normalized simulator and native aircraft state
- `src\Copilot\Procedures\A320ProcedureLibrary.cs`
  - Current procedure definitions
- `src\Copilot\Procedures\ProcedureRunner.cs`
  - Procedure execution, waiting, failure, pause, and confirmation behavior
- `src\Copilot\Checklists\A320ChecklistLibrary.cs`
  - Checklist definitions
- `src\Copilot\Domain\AircraftCapabilities.cs`
  - Capability display
- `src\SimConnectProbe\Program.cs`
  - Read-only discovery and monitoring utility
- `docs\LIVE_TESTS.md`
  - Dated live-test evidence
- `docs\NATIVE_CONTROL_STRATEGY.md`
  - Correct aircraft-specific command patterns
- `docs\behavior-viewer-mappings.csv`
  - Behavior Viewer evidence
- `docs\events.txt`
  - Supplied MobiFlight/HubHop control export

## Runtime telemetry architecture

Native iniBuilds LVars are monitored through a MobiFlight runtime client.

Current runtime client name:

```text
MSFS2024_AI_Copilot_v2
```

The suffix is a schema version. MobiFlight client-data layouts persist during
the simulator session. Increment the suffix whenever the ordered runtime LVar
list or offsets change. Failure to do this can cause a new build to read stale
offsets until MSFS is restarted.

This was the cause of misleading shifted values while expanding fuel telemetry
from four to six pumps. Versioning the client name fixed it without restarting
MSFS.

## General control rule

A transmitted command is not success.

An automatic action is complete only when:

1. The correct aircraft interface was used.
2. A separate state variable changed to the requested value.
3. Any required system-level result occurred.
4. Verification completed safely.

Use native iniBuilds state over contradictory generic APU or switch SimVars.
Do not write a state LVar solely to make a cockpit indication appear correct.

## Flow 1 — current Cockpit Preparation

Current code sequence:

1. Verify A320neo V2.
2. Verify aircraft stationary on the ground.
3. Verify parking brake set.
4. Verify engines off.
5. BAT 1 ON.
6. BAT 2 ON.
7. External power ON.
8. NAV & LOGO selector to position 2.

This flow passed live, but it has not yet been reviewed against the intended
real-world division of duties or a complete A320 cockpit-preparation flow.

### Working Flow 1 controls

#### BAT 1 and BAT 2

Commands are documented MobiFlight calculator presets:

```text
1 (>L:INI_OVHD_ELEC_BAT_1_PB_IS_AUTO_SWITCH)
1 (>L:INI_OVHD_ELEC_BAT_2_PB_IS_AUTO_SWITCH)
```

OFF uses value `0`.

Readback:

- `INI_OVHD_ELEC_BAT_1_PB_IS_AUTO_SWITCH`
- `INI_OVHD_ELEC_BAT_2_PB_IS_AUTO_SWITCH`

Both directions were independently verified.

#### External power

- Command: SimConnect `SET_EXTERNAL_POWER`
- Readback: `EXTERNAL POWER ON:1`
- Live verified.

#### NAV & LOGO selector

Generic NAV light events were wrong for this aircraft selector.

Verified mapping:

- Aircraft selector position `2`
  - Command: `(>B:AIRLINER_LT_NAVLOGO_STATE1)`
  - Readback: `INI_LOGO_LIGHT_SWITCH == 0`
- Aircraft selector `OFF`
  - Command: `(>B:AIRLINER_LT_NAVLOGO_STATE3)`
  - Readback: `INI_LOGO_LIGHT_SWITCH == 2`

The aircraft labels and native numeric values are inverted in this mapping.
Position 1 was not reliable and is intentionally unsupported.

## Flow 2 — current Before Start

Current code sequence:

1. Verify A320neo V2.
2. Verify stationary with parking brake set.
3. Verify electrical power.
4. Ask captain to confirm boarding complete and required doors closed.
5. Set all six fuel pumps ON.
6. Set APU MASTER ON.
7. Wait for APU intake flap open.
8. Select APU START.
9. Wait for APU AVAIL.
10. Observe APU generator pushbutton in normal ON configuration.
11. Set APU BLEED ON.
12. Set beacon ON.
13. Disconnect external power.

The automatic cockpit controls in this sequence are working. The sequence
itself now needs a reality-based redesign, including crew role allocation,
prerequisites, timing, and missing actions.

### Six fuel pumps

The A320 panel has six pump pushbuttons, not four:

- Left tank 1 and 2
- Center tank 1 and 2
- Right tank 1 and 2

Passive monitoring showed that manually clicking all six buttons changed all
six native pump-state LVars from `0` to `1`, while the base Input Events
remained `0`. Input Events were therefore not usable as readback.

Behavior Viewer then exposed the actual L1 Mouserect code:

```text
(L:INI_OUTER_TANK_LEFT) ! (>L:INI_OUTER_TANK_LEFT)
(L:__FUEL_ENG1_L1IsPressed) ! (>L:__FUEL_ENG1_L1IsPressed)
```

The important discovery was that the real cockpit click toggles both:

1. The selector LVar.
2. The press-animation LVar.

The Input Event `Set` code only wrote `_ButtonAnimVar`, which explained why
earlier Input Event tests moved nothing useful.

Implemented mappings:

| Button | Selector LVar | Press-animation LVar | Independent readback |
|---|---|---|---|
| L1 | `INI_OUTER_TANK_LEFT` | `__FUEL_ENG1_L1IsPressed` | `INI_OUTER_TANK_LEFT_PUMP_ON` |
| L2 | `INI_INNER_TANK_LEFT` | `__FUEL_ENG1_L2IsPressed` | `INI_INNER_TANK_LEFT_PUMP_ON` |
| C1 | `INI_CENTER_TANK_LEFT` | `__FUEL_CTR_1IsPressed` | `INI_CENTER_TANK_LEFT_PUMP_ON` |
| C2 | `INI_CENTER_TANK_RIGHT` | `__FUEL_CTR_2IsPressed` | `INI_CENTER_TANK_RIGHT_PUMP_ON` |
| R1 | `INI_INNER_TANK_RIGHT` | `__FUEL_ENG2_R1IsPressed` | `INI_INNER_TANK_RIGHT_PUMP_ON` |
| R2 | `INI_OUTER_TANK_RIGHT` | `__FUEL_ENG2_R2IsPressed` | `INI_OUTER_TANK_RIGHT_PUMP_ON` |

The code only toggles a button if its independent state differs from the
requested state, making group ON/OFF commands idempotent.

Live verification:

- OFF: `0/0/0/0/0/0`
- ON: `1/1/1/1/1/1`

Both directions passed.

### APU MASTER

Working command/state pair:

- Momentary command: `INI_APU_MASTER_SWITCH_CMD`
- Readback: `INI_APU_MASTER_SWITCH`

The command is pulsed `1`, then returned to `0`. ON and OFF passed live.

Do not replace this with:

- Generic APU SimVars
- `AIRLINER_APU_MASTER_PUSH`
- Direct writes to `INI_APU_MASTER_SWITCH`

Those routes were misleading or failed.

### APU intake flap

- Readback: `INI_APU_FLAP_PERCENT`
- The value is normalized; `1.0` means open.
- The procedure condition is `>= 0.95`, not `>= 95`.

### APU START and AVAIL timing

Working start selection:

- Momentary command: `INI_APU_START_BUTTON_CMD`
- Button readback: `INI_APU_START_BUTTON`
- System readiness: `INI_APU_AVAILABLE`

START selection and APU availability are separate events.

The START action completes when the native START button accepts the selection.
The following observation step waits for `INI_APU_AVAILABLE`.

Do not apply a fixed 60-second AVAIL failure to the START command. That caused
a false failure: the APU later became available normally after its start and
warm-up period.

### APU generator

The APU GEN pushbutton is normally left ON. The current Before Start flow only
observes:

```text
INI_APU_GEN_ON != 0
```

It does not routinely press the generator button.

Generic APU generator telemetry has previously been contradictory. Do not
disconnect external power based only on generic RPM, switch, or voltage data.

### APU BLEED

Behavior Viewer exposed the exact Mouserect code:

```text
(L:INI_APU_BLEED_BUTTON) ! (>L:INI_APU_BLEED_BUTTON)
(L:__APU_BLEEDIsPressed) ! (>L:__APU_BLEEDIsPressed)
```

Readback:

```text
INI_APU_BLEED_BUTTON
```

The code only toggles when readback differs from the requested state.

Live verification:

- ON passed.
- OFF passed.
- Restored ON successfully.

The Input Event `Set` code only changed `_ButtonAnimVar`; direct Input Event
tests were not the real cockpit command.

### Beacon

- Command: SimConnect `BEACON_LIGHTS_SET`
- Readback: `LIGHT BEACON`
- Live verified.

### External-power disconnect

- Command: SimConnect `SET_EXTERNAL_POWER`
- Readback: `EXTERNAL POWER ON:1`

The current procedure reaches this only after native APU AVAIL and the native
APU generator pushbutton state are true. Review this guard during the realistic
flow redesign; system power availability should remain the safety criterion.

### Doors and boarding

Currently a manual captain confirmation:

```text
Confirm boarding complete and all required doors closed.
```

This is intentional for now. Boarding complete is operational context and
cannot be inferred from one cockpit switch. Door telemetry and ground-service
coordination remain future work.

## Failed approaches that must not be repeated

These failures consumed substantial time and are recorded here deliberately.

### Input Event values as state

APU and pump Input Events frequently remained `0` while cockpit/system state
changed. Treat Input Events as possible command endpoints, never readback.

### Generic key events for aircraft-specific pumps

`FUELSYSTEM_PUMP_SET` was accepted by SimConnect but ignored by this aircraft.

### Direct `SetInputEvent`

Both manual P/Invoke and the managed SimConnect `SetInputEvent` call reached
the event without producing pump operation.

### Guessed B-event suffixes

Guessed `_PUSH`, `_Set`, `_Inc`, and `_Dec` expressions did not establish the
real control path. Behavior Viewer binding names are not enough; inspect the
expanded Mouserect or data code.

### Press-animation LVars alone

Pulsing `__...IsPressed` alone did not operate the pumps. The real Mouserect
also toggles the selector LVar.

### Direct state writes

Writing a visible state can move a button without operating the system.
Example: setting APU START indication without achieving APU AVAIL.

### Generic APU telemetry

Generic RPM/starter values have reported an apparently operating APU while
native iniBuilds availability or electrical state disagreed. Native iniBuilds
variables are authoritative.

### Blocking sleep

Momentary releases use WinForms timers. Do not block the UI/SimConnect message
loop with `Thread.Sleep`.

### Procedure continuation after failed verification

The runner now fails the active procedure when an automatic native action
times out. It must not continue to beacon or external-power disconnect after
an earlier action failed.

## Procedure-runner safety behavior

`ProcedureRunner.Fail(...)` is available and used for failed automatic action
verification.

Automatic actions:

- Send once.
- Wait for independent state verification.
- Complete only on verified state.
- Fail safely on timeout.
- Do not silently fall back through alternate command guesses.

## Current known gaps

The controls working in Flows 1 and 2 do not mean the flows are operationally
complete.

Likely redesign/research areas include:

- Real A320 cockpit-preparation and before-start sequence.
- Captain versus first-officer task allocation.
- ADIRS selector commands and alignment state.
- Seat-belt/no-smoking or signs logic.
- Emergency lights and other overhead setup.
- Packs, crossbleed, and air-conditioning configuration where applicable.
- Fuel quantity/center-pump operational rules rather than blindly commanding
  all six pumps in every situation.
- Door-state telemetry and ground-service/boarding completion.
- Pushback and ground-crew coordination.
- MCDU/FMGS preparation and route/performance data.
- Takeoff data, flight-control checks, trim, rudder trim, and briefing state.
- Engine-start sequencing and post-start configuration.

The first task in the new chat should be to define a realistic Flow 1 and Flow
2 specification before changing procedure code.

## Recommended next-chat workflow

1. Inspect `A320ProcedureLibrary.cs` and this handoff.
2. Research or agree on the intended real-world A320 flow and crew roles.
3. Write the desired Flow 1 and Flow 2 sequence in documentation first.
4. Classify every step as:
   - First-officer automatic action
   - Captain action
   - Observation
   - Verbal/manual confirmation
   - Operational/ground-service condition
5. Reuse the verified control implementations unchanged where appropriate.
6. Identify missing telemetry/commands.
7. For each missing aircraft-specific control:
   - Inspect Behavior Viewer Mouserect and expanded Input Event code.
   - Monitor native state during a manual click.
   - Implement the exact aircraft code.
   - Verify independent native readback.
   - Test both directions.
   - Document the result.
8. Run complete flows from a clean aircraft state before promoting them.

## Useful one-shot commands

Examples:

```powershell
.\src\Copilot\bin\Release\net472\Copilot.exe --command "status"
.\src\Copilot\bin\Release\net472\Copilot.exe --command "fuel-pumps on"
.\src\Copilot\bin\Release\net472\Copilot.exe --command "fuel-pumps off"
.\src\Copilot\bin\Release\net472\Copilot.exe --command "apu-master on"
.\src\Copilot\bin\Release\net472\Copilot.exe --command "apu-start on"
.\src\Copilot\bin\Release\net472\Copilot.exe --command "apu-bleed on"
.\src\Copilot\bin\Release\net472\Copilot.exe --command "apu-bleed off"
.\src\Copilot\bin\Release\net472\Copilot.exe --command "procedure start cockpit-preparation"
.\src\Copilot\bin\Release\net472\Copilot.exe --command "procedure start before-start"
```

## Final verified state at handoff

At the end of live testing:

- Batteries 1/2: ON/ON
- External power: ON
- APU native available: YES
- APU START: ON
- APU BLEED: ON
- APU generator pushbutton: ON
- Six fuel pumps: `1/1/1/1/1/1`
- Beacon: OFF
- Engines: OFF/OFF
- Aircraft stationary with parking brake set

This state is test context only, not a proposed realistic flow endpoint.


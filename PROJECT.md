# MSFS 2024 Virtual First Officer - Project Status and Handoff

This file is the primary technical handoff for continuing development. It
describes the state of the project at public release **v0.9.2** on July 17,
2026. The flows implemented in the application are authoritative; supporting
documents must follow the application when they differ.

Suggested opening prompt for a new development chat:

> Continue the project described in `C:\CODE\MSFS2024_AI\PROJECT.md`. Read
> `README.md`, `docs/ARCHITECTURE.md`, `docs/checklist.md`, and the relevant
> aircraft support document before changing code. Preserve every completed
> aircraft profile behind its aircraft-specific implementation and regression
> tests.

## Product

MSFS 2024 Virtual First Officer is a free external Windows companion for
Microsoft Flight Simulator 2024. It provides a complete twelve-flow,
gate-to-gate First Officer experience: it performs supported cockpit actions,
monitors Captain actions, verifies actual aircraft state, gives spoken
callouts, and coordinates optional flight-planning and ATC integrations.

The project is multi-aircraft and remains beta software. It is assistance
software, not an autopilot, and the pilot must always be able to take over.

## Current release

- Public version: **0.9.2**
- Repository: <https://github.com/noscapect/MSFS2024_AI>
- Latest release: <https://github.com/noscapect/MSFS2024_AI/releases/tag/v0.9.2>
- Main project: `src/Copilot/Copilot.csproj`
- UI/runtime: WinForms, .NET Framework 4.7.2, x64
- Release executable: `src/Copilot/bin/Release/net472/Copilot.exe`
- Settings and runtime data: `%LOCALAPPDATA%\MSFS2024_AI`
- Primary runtime log: `%LOCALAPPDATA%\MSFS2024_AI\logs\copilot.log`

Build and test:

```powershell
dotnet build .\src\Copilot\Copilot.csproj -c Release --no-restore
dotnet test .\tests\Copilot.Tests\Copilot.Tests.csproj -c Release --no-restore
```

The v0.9.2 release was built with no warnings or errors and passed the full
151-test release suite.

## Supported aircraft

| Aircraft | Status | Runtime interface |
| --- | --- | --- |
| iniBuilds A320neo V2 | Gate-to-gate live validated | SimConnect and MobiFlight WASM |
| iniBuilds A321LR | Gate-to-gate live validated | SimConnect and MobiFlight WASM |
| FlyByWire A32NX for MSFS 2024 | Gate-to-gate live validated | SimConnect and MobiFlight WASM |
| PMDG 737-800 | Gate-to-gate live validated | SimConnect and PMDG SDK data broadcast |
| iniBuilds A330-300 (GE) | Gate-to-gate implemented and live tested; continue field validation | SimConnect and MobiFlight WASM |

Unsupported aircraft are identified but never controlled with guessed generic
commands. FlyByWire A380X research code is deliberately dormant and hidden
from users; it is not a supported profile.

The PMDG aircraft requires `EnableDataBroadcast=1` in its SDK options. Airbus
profiles require the MobiFlight WASM module. Release packages include both
required SimConnect client DLLs, so end users do not need the MSFS SDK.

## Gate-to-gate flow

The application currently exposes these twelve flows:

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

The aircraft profile owns the actions, readbacks, Captain/First Officer roles,
timing, approach schedule, and callouts inside those common flow names. Flows
6 to 7 and the landing/taxi sequence can chain automatically so the pilot does
not need to interact with the app during high-workload phases.

Flow 12 supports both outcomes: **Confirm** continues to final cold-and-dark
secure, while **Cancel** preserves the aircraft for a follow-up/turnaround
flight instead of forcing shutdown.

Automatic QNH/STD operation remains excluded because no sufficiently reliable
cross-aircraft control and independent readback was found. ATC and FMC/MCDU
entry remain pilot responsibilities except for explicitly enabled integration
features.

## Major user features

- Prominent flow start and confirmation controls with clear running/completed
  states
- Automatic First Officer actions with independent cockpit-state verification
- Monitored Captain actions without app confirmation during takeoff, approach,
  landing, and taxi where reliable telemetry exists
- Aircraft-specific, editable approach profiles with practical defaults and
  persistent airline-SOP overrides
- Editable transition altitude, V1, VR, and V2 values
- `Minimal`, `Standard`, and `Expanded` voice-callout detail
- Aircraft/livery information card with package images and exterior fallbacks
- Safe interrupted-flight restore that requires the user to resume, plus a
  visible `New flight / Reset progress` action
- Unified integration manager and top-level integration status badges
- Quiet diagnostics, exportable issue reports, and bounded flight recording;
  only the last three flights are retained
- Developer jump-to-flow support for targeted approach and landing tests

## SimBrief integration

SimBrief support is optional, read-only, and free for both the project and its
users. A Pilot ID or username is sufficient; no paid API key is required.

The latest generated OFP provides the app with operational briefing data such
as route and runway information, cruise data, fuel, takeoff flap setting,
transition altitude, and V1/VR/V2 references. Imported data is displayed for
comparison and pre-fills supported flight settings. SimBrief never writes to
the aircraft FMC/MCDU and cannot block a flow when unavailable.

The dashboard has a SimBrief status badge, and all integration configuration
is available from the single **Manage integrations** screen.

## SayIntentions integration

SayIntentions support is optional and requires its running Windows client and
an active SayIntentions flight. The app discovers the local session and does
not store the user's account API key.

Implemented functionality includes:

- Optional SayIntentions First Officer voices with local speech fallback
- Read-only ATIS, weather, frequency, gate, and communication context
- Checkpoint-scoped communications assignment through the official
  `SIAI_COPILOT=1` interface
- Native SayIntentions Copilot callback actions through SAPI `sendCallback`
  when the corresponding action identifier is known. IFR clearance uses event
  `copilot_request` with action `preflight_request_clearance_ifr`, while
  pushback/start uses action `preflight_request_push_and_start`.
- Establish SayIntentions Copilot communications when its active session is
  discovered. Do not fire a native callback in the same instant as a required
  `SIAI_COPILOT` mode change; the desktop client applies that handoff
  asynchronously even after SAPI accepts `setVar`.
- Preserve pending ATC checkpoints across transient SayIntentions discovery or
  SAPI outages. If no verifiable exchange appears within three minutes, release
  the wait and let the user retry instead of leaving the flow blocked forever.
- Before triggering an ATC callback, scan recent communications from the active
  SayIntentions flight. A matching clearance obtained automatically by the
  SayIntentions Copilot satisfies the checkpoint and suppresses a duplicate
  request, including automatic takeoff calls made when lining up.
- Private pilot-to-First-Officer instructions through `sayAs` on `INTERCOM1`
  remain in use only for checkpoints whose native callback identifier has not
  yet been captured and verified.
- Audible First Officer radio requests, ATC responses, readbacks, and radio
  management produced entirely by SayIntentions
- Matching ATC-response verification before the active flow advances
- Mirroring of SayIntentions First Officer transmissions and ATC
  responses in the activity log, including station and frequency information

When SayIntentions is disabled or unavailable, its steps bypass cleanly and
the simulator's built-in ATC or the pilot handles communication. No supported
aircraft procedure may depend on SayIntentions being installed.

The post-v0.9.2 integration is being completed on the dedicated SayIntentions
branch. SayIntentions owns its complete ATC workflow; the app provides the
Copilot handoff, private checkpoint instruction, voice-callout routing,
passive communication readouts, and non-SayIntentions fallback.

### SayIntentions implementation lessons

The live-tested ownership boundary is important and must not be replaced by a
more complicated imitation of the SayIntentions client:

- The app sets `SIAI_COPILOT=1`, then triggers the same SAPI `sendCallback`
  action used by the corresponding SayIntentions UI button whenever its exact
  event and action name have been captured.
- `INTERCOM1` means pilot-to-intercom-passenger. `INTERCOM1_IN` means the
  intercom passenger speaking to the pilot and remains the correct channel for
  exact cockpit callouts such as `Flaps up`.
- A generic `INTERCOM1` instruction is not a reliable substitute for a native
  callback. Live testing showed that pushback produced only an intercom reply
  ("I'll contact Ground") and no COM transmission. The native
  `preflight_request_push_and_start` callback created the real radio exchange.
- The app must not send the ATC request itself on `COM1`. Doing so represents
  the pilot, can produce text-only or incorrect voice ownership, and bypasses
  the native Copilot workflow.
- The app must never call `setFreq`, infer a station, or tune a radio.
  SayIntentions performs its own SimBrief import, station selection, radio
  tuning, request wording, audible First Officer transmission, ATC response,
  and readback.
- Do not automate or simulate clicks on the SayIntentions desktop UI. Its
  buttons expose `javascript:void(0)` and are an internal UI implementation,
  not a stable integration contract.
- Do not add an extra spoken announcement such as "I will contact ATC now."
  It is not an SOP callout. The only required speech is the actual radio
  request generated by SayIntentions.
- Use `getCommsHistory` only for passive activity-log mirroring and matching
  clearance verification. Capture a baseline before the request so stale
  messages from an earlier flight cannot complete the current checkpoint.
- SayIntentions can add the ATC response to an existing communication record
  after first publishing that record with only the aircraft request. Track the
  incoming and outgoing text per record ID; deduplicating solely by increasing
  ID loses the later ATC response and can leave pushback waiting forever.
- A successful HTTP status is insufficient: SAPI can return an error object in
  a successful response. Parse the response and reject payloads containing an
  `error` field.
- `rephrase=0` is supported for `_IN` channels and preserves exact operational
  cockpit callouts. It is intentionally omitted from the private `INTERCOM1`
  instruction so the SayIntentions copilot can formulate the radio request.
- Leave the delegated conversation with SayIntentions long enough to complete
  its audible request, ATC exchange, readback, and automatic radio handling.
  The VFO flow advances only after its matching response classifier succeeds.
- If SayIntentions is unavailable, disabled, or has no active flight, the
  normal built-in-ATC/pilot path must remain unblocked.

Live acceptance at an IFR checkpoint confirmed that this model makes the
configured SayIntentions First Officer audibly request clearance, produces an
audible ATC response and First Officer readback, and allows the VFO flow to
verify the exchange without controlling the radios.

## Architecture and stability boundary

The application deliberately shares the user experience and normalized
aircraft state, but does not share aircraft-specific cockpit assumptions.

Important layers and files:

- `src/Copilot/CopilotService.cs` - runtime orchestration, SimConnect,
  integration coordination, command dispatch, and normalized telemetry
- `src/Copilot/AircraftState.cs` - normalized simulator state
- `src/Copilot/Procedures/ProcedureRunner.cs` - ordered flow execution,
  waiting, retry, verification, pause/resume, and cancellation
- `src/Copilot/Procedures/A320ProcedureLibrary.cs` - iniBuilds A320neo V2
- `src/Copilot/Procedures/A321ProcedureLibrary.cs` - iniBuilds A321LR
- `src/Copilot/Procedures/FbwA320ProcedureLibrary.cs` - FlyByWire A32NX
- `src/Copilot/Procedures/A330ProcedureLibrary.cs` - iniBuilds A330
- `src/Copilot/Procedures/B737ProcedureLibrary.cs` - PMDG 737-800
- Matching aircraft-specific libraries under `src/Copilot/Checklists`
- `docs/ARCHITECTURE.md` - detailed adapter and regression boundaries
- `docs/NATIVE_CONTROL_STRATEGY.md` - required command/readback discipline

Each supported aircraft owns its procedure, checklist, command/readback, and
approach behavior. Similar aircraft must not silently fall through to another
profile's native variables. Completed profiles are protected by structural
fingerprint and regression tests. A change for one aircraft must add or use an
aircraft-specific path and must not weaken another profile's verification to
make a new aircraft pass.

### Native-control rule

An automatic action is complete only when the aircraft reports the intended
state through a reliable and independent readback. Sending a command, seeing a
generic SimVar change, or observing a cockpit animation alone is not enough.

Use this order of evidence:

1. Aircraft documentation or SDK
2. MSFS Behavior Viewer/Input Event evidence
3. A controlled probe that records command and state changes
4. Live verification in the exact aircraft profile

Do not repeatedly guess LVars, B-events, Input Event values, or Rotor Brake
codes. Record discovered mappings in the relevant support document or control
matrix and add a regression test before considering the fix complete.

## Testing and release discipline

Before handing a build to the user or publishing a release:

1. Confirm the intended aircraft profile is selected and no other profile was
   modified unintentionally.
2. Run the focused tests for the changed subsystem and aircraft.
3. Run the complete Release test suite.
4. Build the x64 Release package and test the executable from the Release
   folder, because that is the build used during live flights.
5. Update `README.md`, `docs/checklist.md`, `docs/LIVE_TESTS.md`, and aircraft
   support documents when behavior or validation status changes.
6. Use `tools/Publish-Release.ps1` and follow `docs/RELEASING.md` for public
   releases.

Never restart the app during an active user flight unless explicitly asked.
When a debug/probe build is required, say so clearly; the user normally tests
the Release build and the UI does not identify Debug versus Release.

## Deferred and remaining work

- Continue real-flight validation of SayIntentions exchanges and the A330
  profile after simulator or aircraft updates
- Add GSX Pro ground-service integration using its official bidirectional
  Remote Control SDK; feasibility research is in
  `docs/GSX_INTEGRATION_FEASIBILITY.md`
- Improve interactive checklist and crew-audio behavior
- Revisit FlyByWire A380X support only on a dedicated development branch
- Add further aircraft only with a separate adapter/procedure/checklist and a
  full gate-to-gate validation plan
- Implement go-around and rejected-takeoff procedures last; both require safe
  interruption and recovery branches rather than small additions to normal
  flows

An AI-controlled Pilot Flying mode, taxi automation, and the former Boeing
autoland-assist experiment are not current product features.

## Authoritative supporting documents

- `README.md` - customer-facing installation and use
- `docs/checklist.md` - detailed implemented flow content
- `docs/LIVE_TESTS.md` - aircraft and flow validation evidence
- `docs/ARCHITECTURE.md` - contributor architecture and isolation rules
- `docs/NATIVE_CONTROL_STRATEGY.md` - cockpit command/readback standard
- `docs/SIMBRIEF_INTEGRATION_PLAN.md` - SimBrief design and status
- `docs/SAYINTENTIONS_INTEGRATION_PLAN.md` - SayIntentions design and status
- `docs/ROADMAP.md` - deliberately deferred features
- `docs/RELEASING.md` - release process

## Operational note

MSFS and the selected aircraft must be loaded before live testing. Desktop
session access matters: a hidden or sandboxed process may not connect to the
running simulator. The app is normally stopped before rebuilding because a
running executable locks the Release output.

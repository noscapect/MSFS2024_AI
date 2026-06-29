# FBW A32NX support development plan

Branch: `feature/fbw-a320neo-support`

Goal: add FlyByWire A32NX support without destabilizing the released
iniBuilds A320neo V2 app.

## Development strategy

The existing gate-to-gate procedure engine remains the source of truth. FBW
support should be added through an aircraft-adapter layer, not by mixing FBW
control mappings into iniBuilds-specific code paths.

The first development phase is discovery only:

1. Load the FBW A32NX cold and dark at a gate.
2. Allow the app to recognize FBW as an experimental supported A320.
3. Start the current flows from cold and dark.
4. Record which normalized readbacks work through generic SimVars.
5. Record where iniBuilds-only native commands block.
6. Convert those gaps into FBW-specific state and command mappings.

iniBuilds-only automatic commands must remain blocked on FBW until an explicit
FBW equivalent has been mapped and verified.

## Phase 1 scope: cold and dark through before takeoff

Target flows:

1. Power Up & Initial Setup
2. Flight Computer & Pre-Flight
3. APU Start & Pushback
4. Engine Start Sequence
5. After Start & Taxi
6. Before Takeoff

Do not include Flow 7 takeoff yet.

## What to capture during live tests

For every step:

- Does the app detect the aircraft state correctly?
- Does the step complete without aircraft-specific native readback?
- If the app blocks, what condition or command caused it?
- Does FBW expose an equivalent LVar, SimVar, InputEvent, or H/Event?
- Is the action safe to automate, or should it remain manual/monitor-only?

Use the app's diagnostic export after each run:

```text
%LOCALAPPDATA%\MSFS2024_AI\diagnostics
%LOCALAPPDATA%\MSFS2024_AI\flights
%LOCALAPPDATA%\MSFS2024_AI\logs\copilot.log
```

## Expected initial blockers

- ADIRS selectors and ON BAT logic
- Crew oxygen
- NAV/LOGO/strobe selector positions
- Fire test buttons and warning/sound readbacks
- Fuel pumps
- Seatbelt/no-smoking/emergency-exit selectors
- APU master/start/bleed/available readbacks
- Transponder/TCAS controls
- Ground spoilers, flaps, autobrake
- Nose and landing light selector positions

Some generic SimVars may already work for aircraft state, engines, basic
lights, landing gear, flaps, speed, altitude, and parking brake. Those should
be reused where reliable.

## Adapter target

Create aircraft-specific adapters:

- `IniBuildsA320NeoV2Adapter`
- `FbwA32NxAdapter`

The procedure engine should request normalized actions like `SetApuBleedOn`
or `SetNoseLightTakeoff`. The active adapter decides whether the loaded
aircraft supports that action and how to command/verify it.

## Release rule

Do not merge to `main` or publish a public release with FBW enabled until:

- Flows 1 through 6 are live-tested on FBW.
- Existing iniBuilds flows still pass regression tests and at least one smoke
  live test.
- Unsupported FBW actions fail cleanly or are marked manual/monitor-only.

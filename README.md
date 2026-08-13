# MSFS 2024 Virtual First Officer

MSFS 2024 Virtual First Officer is a free Windows companion for Microsoft
Flight Simulator 2024. It guides a complete gate-to-gate flight, performs
supported First Officer duties, monitors Captain actions, verifies cockpit
changes, and provides spoken operational callouts.

> This project is in beta. Keep flying the aircraft and be prepared to take
> over whenever an action cannot be completed or verified.

## Supported aircraft

- iniBuilds A320neo V2
- iniBuilds A321LR
- iniBuilds A330-300 (GE)
- iniBuilds A310-300
- FlyByWire A32NX for MSFS 2024
- PMDG 737-800
- Asobo 737 MAX 8 (**development beta; see the warning below**)

Unsupported aircraft are detected but not controlled. The app does not send
guessed generic commands to an unknown cockpit.

## Highlights

- Twelve flows covering cold and dark through parking and shutdown
- Aircraft-specific Airbus and Boeing procedures
- Automatic First Officer actions with cockpit-state verification
- Monitoring of Captain actions without requiring app interaction during
  takeoff, approach, landing, or taxi
- Aircraft-specific approach schedules with optional airline-SOP overrides
- Automatic handoff between the most time-critical consecutive flows
- Spoken engine-start, takeoff, configuration, minimums, gear, and landing
  callouts
- `Minimal`, `Standard`, and `Expanded` voice detail
- Optional SimBrief, SayIntentions, and GSX Pro integration
- Optional MSFS 2024 EFB companion for starting and controlling flows without
  leaving the simulator
- Saved flight progress with a clear `New flight / Reset progress` control
- Quiet diagnostic recording for troubleshooting

Routine switch movements are intentionally not narrated. Standard voice mode
focuses on verified configuration changes and operational calls.

## Requirements

- Windows 10 or Windows 11
- Microsoft Flight Simulator 2024
- One of the supported aircraft
- .NET Framework 4.7.2 or newer
- MobiFlight WASM module installed in MSFS for supported Airbus aircraft

The release package contains the required SimConnect client libraries. The
MSFS SDK is not required to run the app.

### PMDG 737-800

PMDG SDK data broadcast must be enabled in the aircraft options file:

```ini
[SDK]
EnableDataBroadcast=1
```

The dashboard should show `PMDG SDK OK` after the aircraft is loaded.

### Asobo 737 MAX 8 experimental support

The repository contains a dedicated Asobo 737 MAX profile using native
SimConnect Input Events and aircraft-specific procedures. Flows 1–6 have
received iterative live testing, but the MAX profile is not yet gate-to-gate
validated. It is included in v0.9.7 only as an explicitly
**experimental**, and further MAX development is paused while work focuses on
the desktop UX redesign and MSFS 2024 EFB companion.

Do not rely on its current "Takeoff configuration normal" callout as an
independent trim or flight-control check. Before takeoff, manually verify the
FMC flap/V-speed configuration, stabilizer trim, yoke/elevator movement,
hydraulics, and the aircraft's own takeoff-configuration warning. Flow 7 is
not cleared for unattended use until actual trim, elevator, and control-input
telemetry has been implemented and live validated. See
[Asobo 737 MAX support status](docs/ASOBO_737_MAX_SUPPORT_STATUS.md).

## Installation

1. Download the latest package from
   [GitHub Releases](https://github.com/noscapect/MSFS2024_AI/releases/latest).
2. Extract the complete ZIP file to a normal folder. Do not run the executable
   from inside the ZIP.
3. Confirm that the MobiFlight WASM module is installed when using an Airbus.
4. Start MSFS 2024 and load a supported aircraft.
5. Run `Copilot.exe` from the extracted release folder.

Keep all DLLs beside `Copilot.exe`. Windows may show a SmartScreen warning for
an unsigned community application; verify that the download came from this
repository before allowing it to run.

### In-simulator EFB companion

The v0.9.7 release includes an optional native MSFS 2024 EFB app. It displays
the current flow and step, allows the pilot to start, confirm, pause, resume, or
cancel a flow, and surfaces aircraft telemetry and GSX boarding progress
without Alt-Tabbing.

The EFB remains a thin remote: `Copilot.exe` must be running and continues to
own all procedure, verification, aircraft-control, GSX, and SayIntentions
logic. The EFB cannot send arbitrary cockpit commands.

The release ZIP contains the ready-to-install Community package under
`EFB Community Package\Community`. Close MSFS, copy `noscapect-vfo-efb` into
the simulator's Community folder, and restart MSFS. Developer build and
installation instructions are in
[EFB Companion](docs/EFB_COMPANION.md).

## First flight

1. Load the aircraft at the gate, preferably cold and dark.
2. Start the app and verify that both MSFS and the correct aircraft are shown
   as connected.
3. Select `New flight / Reset progress` when beginning a new sector.
4. Select Flow 1 and press the prominent start button.
5. Perform Captain tasks when requested. Use `Confirm now` only after the
   requested action is complete.
6. Continue through the recommended flows. Enabled flow handoffs will start
   automatically.

The app may restore an interrupted flight after restart. It does not
immediately resume cockpit actions: review the aircraft state and use Resume
only when it is safe. Use `New flight / Reset progress` instead when starting
a different flight.

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

The exact actions and ownership differ between aircraft. The loaded aircraft
always determines which procedure profile is used.

## SimBrief

SimBrief support is free and optional. Open **Manage SimBrief**, enter a Pilot
ID or username, and import the latest generated OFP.

The app uses the OFP as a read-only operational briefing for route, runway,
cruise, fuel, flap, and takeoff-reference comparisons. It never writes to the
aircraft FMC or MCDU and will not block a flight if SimBrief is unavailable.
Pilots with a paid Navigraph subscription can generate the OFP using the current
AIRAC; the app still connects only to SimBrief and requires no Navigraph login.

## SayIntentions

SayIntentions support is optional and requires its Windows client with an
active flight. The app discovers the active local SayIntentions session; it
does not store the account API credential.

Available features include:

- SayIntentions First Officer voices with automatic local voice fallback
- Read-only ATIS, weather, frequency, gate, and communication information
- Optional, checkpoint-scoped assignment of radio communications to the
  SayIntentions First Officer
- Flow checkpoints that trigger the corresponding native SayIntentions
  Copilot action when its verified SAPI callback is available
- First Officer transmissions and ATC responses mirrored into the activity log
- IFR, pushback/start, taxi, and takeoff steps remain open until the matching
  SayIntentions ATC clearance is detected

SayIntentions owns each delegated ATC conversation, including the audible
First Officer request, ATC response, readback, and automatic radio tuning. This
app triggers the native Copilot workflow at the matching flow checkpoint and
monitors communication history before advancing. It never selects or tunes a
frequency. If SayIntentions is unavailable, built-in MSFS ATC remains the
normal fallback.

## Voice callouts

- **Minimal:** essential takeoff, gear, minimums, and landing calls
- **Standard:** adds verified flap, speedbrake, stable-approach, and takeoff
  configuration calls; recommended for normal use
- **Expanded:** also adds selected autobrake/APU status and checklist
  completion calls

SayIntentions may apply the language configured for its First Officer. The app
always supplies standard operational callouts in English.

## GSX Pro

GSX Pro support is optional beta functionality. When enabled under **Manage
integrations**, the app can request the user-configured boarding service and
coordinate **Prepare for Pushback and Departure** with the normal preflight and
engine-start flows. GSX remains responsible for service configuration,
aircraft and airport profiles, doors, loading, deicing, and pushback choices.

Every contextual GSX question is shown to the pilot rather than guessed. The
integration does not take control from another GSX-aware add-on without an
explicit recovery choice, and normal flows remain available when GSX is not
installed or disabled.

## Important limitations

- This is assistance software, not an autopilot and not a replacement for
  aircraft knowledge or checklists.
- ATC communication is handled by the selected ATC service. FMC/MCDU data
  entry remains a pilot responsibility.
- Automatic QNH/STD switching is excluded because a sufficiently reliable,
  verified interface was not found for every supported aircraft.
- Airline procedures vary. The included configuration is a practical standard
  profile and can be overridden where settings are available.
- Aircraft or simulator updates can change cockpit interfaces. Stop the flow
  and take over if the displayed state does not match the cockpit.
- The Asobo 737 MAX profile is experimental and active development is paused.
  Its imported
  SimBrief takeoff flap value is not currently compared with live MAX FMC
  takeoff data, and stabilizer/elevator state is not yet verified.
- The iniBuilds A310-300 implementation is complete for the current scope. Its
  dedicated twelve-flow catalog, checklists, SimBrief and approach contracts,
  A310-only commands, and native readbacks cover the full gate-to-gate sequence.
  Deliberate captain decisions and controls without a safe native interface
  remain manual. Further A310 work is maintenance and field validation rather
  than planned feature development.
- PMDG 777-300ER integration has started. The app recognizes the installed
  `777-300ER` title and isolates the official `PMDG_777X` SDK namespace, but it
  does not yet expose 777 procedures or send cockpit controls. Detection is not
  a claim of operational support.

## Troubleshooting and issue reports

If an automatic action fails:

1. Pause or cancel the active flow and keep control of the aircraft.
2. Note the aircraft, flow, step, and approximate time.
3. Use **Export diagnostics** or **Copy last diagnostic** in the app.
4. Open a report in
   [GitHub Issues](https://github.com/noscapect/MSFS2024_AI/issues) and attach
   the exported information.

Normal diagnostic and flight-recording retention is limited automatically so
the app does not continuously consume disk space.

User data is stored under:

```text
%LOCALAPPDATA%\MSFS2024_AI
```

Deleting this folder resets settings, saved progress, logs, diagnostics, and
retained flight recordings.

## Documentation

- [Detailed gate-to-gate checklist](docs/checklist.md)
- [Supported-aircraft and live-test status](docs/LIVE_TESTS.md)
- [Asobo 737 MAX development status](docs/ASOBO_737_MAX_SUPPORT_STATUS.md)
- [iniBuilds A310-300 support status](docs/A310_SUPPORT_PLAN.md)
- [PMDG 777-300ER integration status](docs/PMDG_777_300ER_SUPPORT_PLAN.md)
- [SayIntentions integration status](docs/SAYINTENTIONS_INTEGRATION_PLAN.md)
- [SimBrief integration](docs/SIMBRIEF_INTEGRATION_PLAN.md)
- [Product roadmap](docs/ROADMAP.md)
- [Architecture and contributor information](docs/ARCHITECTURE.md)

## Disclaimer

This is an independent freeware community project. It is not affiliated with
or endorsed by Microsoft, Asobo Studio, iniBuilds, FlyByWire Simulations,
PMDG, Boeing, Airbus, MobiFlight, SimBrief, or SayIntentions.AI. Use it only
for flight simulation.

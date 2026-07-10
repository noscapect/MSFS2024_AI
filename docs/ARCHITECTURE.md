# Architecture

## Application layers

### External application

Provides the management interface, settings, logs, procedure controls, flight
overview, and pilot interaction.

### Procedure engine

Owns the gate-to-gate state machine. Each procedure contains ordered and
conditional steps with:

- Preconditions
- Assigned crew role
- Action
- Verification
- Timeout and retry policy
- Failure handling
- Spoken or displayed callout

### Aircraft domain

Represents normalized aircraft state such as electrical power, engines, lights,
flight controls, autoflight modes, navigation, radios, doors, and ground
services. Procedures depend on this normalized model rather than raw SimVars
or aircraft-specific SDK structures.

### Aircraft adapters

Maps normalized state and commands to:

- SimVars
- SimConnect Key Events
- SimConnect Input Events
- MobiFlight calculator execution for native command-LVar pulses
- MobiFlight native-variable monitoring
- PMDG NG3 SDK client-data monitoring and control events

Every capability is marked as supported, unsupported, read-only, or requiring
manual confirmation.

The application is now structured around aircraft-family adapters rather than a
single A320-specific flow. The public release supports multiple Airbus and
Boeing aircraft profiles:

- iniBuilds A320neo V2
- iniBuilds A321LR
- iniBuilds A330 *(experimental branch profile)*
- FlyByWire A32NX for MSFS 2024
- PMDG 737-800

iniBuilds command/state pairs and the mandatory momentary-command workflow are
defined in `docs/NATIVE_CONTROL_STRATEGY.md`. The A321LR and experimental A330
use the shared iniBuilds Airbus adapter where their controls match the
A320neo V2.
FBW-specific mappings were added through the same normalized state/action model
so procedures can stay shared.

Dormant FlyByWire A380X research code may exist in the repository for future
development, but the aircraft is not exposed as a supported public profile.

PMDG 737-800 support is implemented as a separate Boeing aircraft family.
Airbus procedures remain in `A320ProcedureLibrary`; Boeing procedures live in
`B737ProcedureLibrary`; `ProcedureCatalog` selects the correct catalog from the
loaded aircraft so the user still sees one app. PMDG cockpit state is read from
the official `PMDG_NG3_Data` client-data area and controls are sent through
`PMDG_NG3_Control` or aircraft-confirmed PMDG `ROTOR_BRAKE` switch events
where required.

### SimConnect transport

Maintains the simulator connection, subscriptions, event transmission,
reconnection, and raw telemetry. It contains no procedural decisions.

### Flight telemetry and replay

The app records one normalized aircraft-state snapshot per second while a
flight is active. Recordings are finalized after landing and one minute
stationary, and retention is limited to the newest three flights. Replay feeds
the same normalized state into the procedure engine at 10x speed while
simulator commands are suppressed.

### Voice queue

Spoken callouts use one non-overlapping priority queue. Time-critical takeoff,
gear, minimums, and landing calls are ordered ahead of routine queued speech;
calls with equal priority preserve their original sequence.

## Operational state

```text
ColdAndDark
  -> CockpitPreparation
  -> ReadyForBoarding
  -> BeforeStart
  -> EngineStart
  -> AfterStart
  -> TaxiOut
  -> BeforeTakeoff
  -> Takeoff
  -> Climb
  -> Cruise
  -> DescentPreparation
  -> Descent
  -> Approach
  -> Landing
  -> AfterLanding
  -> TaxiIn
  -> Shutdown
  -> Secured
```

Transitions use aircraft state and explicit pilot authorization. They are not
based only on elapsed time.

Active and completed flow progress is persisted across restarts. The
`New flight / Reset progress` action clears only that procedure-session state;
user settings and the retained telemetry recordings are preserved.

## Safety model

The application separates deciding from doing:

1. The procedure engine requests a normalized action.
2. The aircraft adapter checks whether it supports that action.
3. Preconditions are evaluated against a recent state snapshot.
4. The adapter transmits the mapped simulator command.
5. Telemetry verifies the expected postcondition.
6. Failure is reported and the procedure pauses, retries, or requests manual
   completion according to policy.

No future AI component receives direct simulator-control access.


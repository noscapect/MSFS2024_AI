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

Represents normalized A320 state such as electrical power, engines, lights,
flight controls, autoflight modes, navigation, radios, doors, and ground
services. Procedures depend on this normalized model rather than raw SimVars.

### Aircraft adapters

Maps normalized state and commands to:

- SimVars
- SimConnect Key Events
- SimConnect Input Events
- MobiFlight calculator execution for native command-LVar pulses
- MobiFlight native-variable monitoring

Every capability is marked as supported, unsupported, read-only, or requiring
manual confirmation.

The application supports three A320-family aircraft profiles:

- iniBuilds A320neo V2
- iniBuilds A321LR
- FlyByWire A32NX for MSFS 2024

iniBuilds command/state pairs and the mandatory momentary-command workflow are
defined in `docs/NATIVE_CONTROL_STRATEGY.md`. The A321LR uses the shared
iniBuilds A320-family adapter where its controls match the A320neo V2.
FBW-specific mappings were added through the same normalized state/action model
so procedures can stay shared.

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

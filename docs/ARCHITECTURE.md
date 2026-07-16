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
- iniBuilds A330 *(experimental/work-in-progress beta profile)*
- FlyByWire A32NX for MSFS 2024
- PMDG 737-800

iniBuilds command/state pairs and the mandatory momentary-command workflow are
defined in `docs/NATIVE_CONTROL_STRATEGY.md`. Each supported aircraft has its
own procedure and checklist library. The iniBuilds A320neo V2 owns a dedicated
fuel-pump control profile that locks its six live-verified commands and native
readbacks away from the A321, A330 and FBW implementations. The live-validated A321LR also owns an
aircraft-specific control profile for its flap and sign-selector mappings;
generic flap-surface fallbacks are forbidden for this aircraft. Experimental
A330 work must use A330-specific code paths and may not weaken A321 completion
conditions or command mappings. FBW-specific mappings use their own adapter
and procedure libraries through the same normalized state/action model.

The A330 additionally owns an `a330` procedure-command namespace. Runtime
dispatch rejects those commands for every other aircraft variant. Automated
contracts verify dedicated procedure/step instances and freeze the A330 flow,
role, command, and checklist structure while live completion work continues.
Its SimBrief input is optional and aircraft neutral; A333 matching and the
no-active-OFP path are covered independently.

### Stable-aircraft regression boundary

The iniBuilds A321LR is a frozen, end-to-end validated profile. Changes for
another aircraft must not alter `A321ProcedureLibrary`,
`A321ChecklistLibrary`, or `A321ControlProfile`. Automated tests assert that
aircraft detection selects all twelve dedicated A321 definitions, that the
physical flap handle remains the only flap-detent authority, that live-tested
flap commands remain unchanged, and that sign selectors are verified by their
actual AUTO/OFF positions. Intentional A321 bug fixes or features may update
this baseline only with matching tests and renewed live validation.

Dormant FlyByWire A380X research code may exist in the repository for future
development, but the aircraft is not exposed as a supported public profile.

PMDG 737-800 support is implemented as a separate Boeing aircraft family.
Airbus aircraft use separate A320, A321, A330, and FBW procedure libraries;
Boeing procedures live in `B737ProcedureLibrary`. `ProcedureCatalog` selects
the correct catalog from the loaded aircraft so the user still sees one app.
PMDG cockpit state is read from
the official `PMDG_NG3_Data` client-data area and controls are sent through
`PMDG_NG3_Control` or aircraft-confirmed PMDG `ROTOR_BRAKE` switch events
where required.

The PMDG 737-800 is a frozen, gate-to-gate live-validated profile. Its twelve
procedures and checklists are separate objects, every automatic command is
required to remain in the dedicated `pmdg` command namespace, and no PMDG
command may be shared with a released Airbus profile. A cryptographic
gate-to-gate structure fingerprint detects unreviewed step, role, command, or
checklist changes. Variant-routing tests also prevent another 737 or Airbus
title from silently inheriting the PMDG implementation. Intentional PMDG
changes require updated tests and renewed proportional live validation.

These contracts prevent unrelated aircraft work from silently changing the
released PMDG flow. Shared transport and normalized-domain changes still run
the complete regression suite before release; no software test can guarantee
compatibility with future simulator or aircraft updates.

### SimConnect transport

Maintains the simulator connection, subscriptions, event transmission,
reconnection, and raw telemetry. It contains no procedural decisions. A
simulator disconnect invalidates all MobiFlight runtime registrations and
cached native values; the ordered SimVar table is rebuilt before native
readback is accepted after reconnecting.

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


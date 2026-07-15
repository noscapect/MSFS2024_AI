# PMDG 737-800 support status

Status: gate-to-gate implementation completed and live validated for v0.8.1.

The app detects the PMDG 737-800 as a separate Boeing aircraft profile and
loads a Boeing-specific twelve-flow procedure and checklist set. The user sees
the same application and flow selector used by supported Airbus aircraft, but
the PMDG profile does not inherit Airbus cockpit commands or procedures.

## Required PMDG SDK setting

PMDG exposes authoritative cockpit state through its NG3 SDK client-data
channel. Enable the data broadcast in the PMDG aircraft options file:

```ini
[SDK]
EnableDataBroadcast=1
```

The dashboard shows `PMDG SDK OK` after `PMDG_NG3_Data` is received. Exact
switch verification is unavailable while it shows `PMDG SDK WAITING`.

## Implementation boundary

- Boeing procedures live only in `B737ProcedureLibrary`.
- Boeing checklist verification lives only in `B737ChecklistLibrary`.
- `ProcedureCatalog` selects the PMDG catalog only for the explicit
  `Pmdg737800` aircraft variant.
- Every PMDG automatic procedure command uses the dedicated `pmdg` namespace.
- PMDG switch state is read from `PMDG_NG3_Data`; controls use
  `PMDG_NG3_Control` or Behavior Viewer-confirmed `ROTOR_BRAKE` events.
- Released-aircraft tests verify dedicated routing, prohibit shared procedure
  or step objects, prohibit shared Airbus/PMDG commands, and fingerprint every
  PMDG step, role, command, manual instruction, and checklist item.

This makes unrelated aircraft work fail regression testing if it changes the
released PMDG flow contract. Shared transport or domain changes still require
the complete test suite and proportional PMDG review before release.

## Operational details

- Flow 1 performs the FAULT/INOP, OVHT/FIRE, extinguisher 1, and extinguisher 2
  tests after electrical power is established. Spring-loaded controls are
  held, full annunciator patterns and neutral release are verified, and the
  master FIRE WARN light is pressed during OVHT/FIRE to verify bell cancel.
- Electrical transfer verifies actual APU or engine-generator bus power before
  ground power is removed.
- Fuel-pump logic enables the four main pumps and enables center pumps only
  when center-tank fuel requires them.
- IRS, packs, isolation valve, hydraulics, lights, transponder/TCAS, flaps,
  speedbrake, autobrake, and landing gear use PMDG-specific commands and
  readbacks.
- The approach schedule uses runway distance when available: Flaps 1 by 15 NM,
  Flaps 5 at or inside 12 NM and at 190 knots or less, followed by dedicated
  gear, Flaps 15, and landing-flap gates. Altitude gates are fallbacks only
  when usable runway-distance data is unavailable.
- Flow 11 retracts landing lights, selects steady position lights, turns the
  taxi light on, keeps runway-turnoff lights on for taxi, stows speedbrake,
  retracts flaps, starts the APU, and establishes APU bleed.

## SimBrief behavior

When an OFP is active, the PMDG profile receives the same route, runway, fuel,
and cruise briefing as other aircraft. It also compares SimBrief V1, VR, and
takeoff flap data with PMDG FMC TAKEOFF REF readback. These are advisory
comparisons only; SimBrief never operates the FMC or blocks a flow.

## Validation status

All twelve PMDG flows have been exercised gate-to-gate in MSFS 2024. The v0.8.1
release additionally includes regression coverage for the complete flow
structure, fire-test sequence, approach scheduling, APU bus-power transfer,
configuration detents, FMC landing data, and after-landing taxi-light action.

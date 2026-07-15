# PMDG 737-800 support notes

Goal: add PMDG 737-800 support without mixing Boeing procedures into the
existing Airbus A320-family flow.

## Branch status

Development branch: `feature/pmdg-737-800-support`.

The app now detects the PMDG 737-800 as a separate Boeing aircraft family and
loads a Boeing-specific 12-flow procedure/checklist set. The user still sees
one app and one flow selector; the app chooses the Airbus or Boeing procedure
catalog from the loaded aircraft title.

## Required PMDG SDK setting

PMDG exposes cockpit state through its NG3 SDK client-data channel. The PMDG
package includes the official SDK header and sample under:

`Community\pmdg-aircraft-738\Documentation\SDK`

For live testing, enable the PMDG data broadcast in the aircraft options file:

```ini
[SDK]
EnableDataBroadcast=1
```

The app shows `PMDG SDK OK` when the `PMDG_NG3_Data` client-data broadcast is
received. If it shows `PMDG SDK WAITING`, the aircraft can still expose some
generic SimVars, but exact PMDG switch readback is not available yet.

## Implementation approach

- Airbus procedure code remains in `A320ProcedureLibrary`.
- Boeing procedure code lives separately in `B737ProcedureLibrary`.
- Aircraft-family routing is centralized in `ProcedureCatalog`.
- Boeing checklist verification lives in `B737ChecklistLibrary`.
- PMDG SDK state is normalized into the existing `AircraftState` model where
  concepts overlap: battery, ground power, IRS, APU, fuel pumps, lights,
  signs, speedbrake, autobrake, gear, transponder and fire-test indicators.
- PMDG commands use the official `PMDG_NG3_Control` client-data channel where
  PMDG SDK events are known.
- The approach schedule is PMDG-specific: Flaps 1 uses the 15 NM gate when
  runway distance is available; Flaps 5 requires both 12 NM or closer and
  190 knots or less. The 10,000/4,000-foot gates are fallbacks only when no
  usable runway-distance readback exists. Gear, Flaps 15, and landing flaps
  then continue through their separate distance/AGL and speed gates so the
  aircraft is configured by the stabilized-approach gate.
- After landing, once reverse is stowed and taxi speed is reached, Flow 11
  retracts the landing lights, selects steady position lights, turns the taxi
  light on, and keeps the runway-turnoff lights on for taxi to the stand.
- Flow 1 performs and independently verifies the FAULT/INOP and OVHT/FIRE
  detection tests plus both extinguisher tests after electrical power is
  established. Each spring-loaded control is held, its complete annunciator
  pattern is verified, and its return to neutral is confirmed before the flow
  advances. During OVHT/FIRE, the First Officer pushes the master FIRE WARN
  light to verify that the bell cancels while the remaining fire indications
  stay illuminated. This uses PMDG SDK event/state definitions rather than
  generic Airbus fire-test logic.

## First live-test target

Start cold and dark at a gate and test through, but not including, takeoff:

1. 737 Power Up & Initial Setup
2. 737 FMC & Pre-Flight
3. 737 APU Start & Pushback
4. 737 Engine Start Sequence
5. 737 After Start & Taxi
6. 737 Before Takeoff

Expect some PMDG event parameters to need live adjustment. The branch is
structured so those fixes stay inside the Boeing/PMDG path and do not affect
the iniBuilds or FlyByWire Airbus flows.

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
- Flow 1 performs and independently verifies the FAULT/INOP and OVHT/FIRE
  detection tests plus both extinguisher tests after electrical power is
  established. Each spring-loaded control is held, its complete annunciator
  pattern is verified, and its return to neutral is confirmed before the flow
  advances. This uses PMDG SDK event/state definitions rather than generic
  Airbus fire-test logic.

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

# FBW A380X support notes

Branch: `feature/fbw-a380-support`

Goal: add FlyByWire A380X support by reusing the proven FlyByWire Airbus
adapter where the upstream A380X source shares A32NX local variables, while
keeping A380-specific differences explicit and live-testable.

## Research findings

The FlyByWire A380X source still uses many `A32NX_...` local variables for
shared Airbus systems. This means the existing FBW A32NX adapter is a useful
base for:

- batteries
- ADIRS selectors
- APU master/start/available
- APU bleed
- flaps handle
- autobrakes
- spoilers armed
- fire-test buttons
- WXR/PWS
- TCAS/transponder variables

Known A380X-specific difference found before live testing:

- External power pushbutton readback is indexed on the A380X:
  `L:A32NX_OVHD_ELEC_EXT_PWR_1_PB_IS_ON`.
  The app now reads that alongside the A32NX unindexed variable and writes both
  forms when commanding external power.

## Initial implementation

- Added `IsFlyByWireA380X` and shared `IsFlyByWireAirbus` detection.
- Kept A380X on the Airbus procedure catalog for the first experimental pass.
- Reused the FBW runtime/MobiFlight adapter where source variables match.
- Added A380X-specific external-power readback.
- Updated the dashboard badge and FBW bridge snapshot so A32NX and A380X are
  visibly distinct.

## Live-test priorities

1. Flow 1 from cold and dark:
   - battery detection
   - external power available/on
   - ADIRS selector movement/readback
   - crew oxygen
   - NAV/logo and strobe behavior
   - fire tests
2. Flow 2 and 3:
   - signs
   - APU available/bleed
   - fuel-pump assumptions
3. Engine-start flow:
   - the A380X is a four-engine aircraft; the current first pass still follows
     the existing two-engine Airbus flow model and must be validated before it
     can be considered operationally correct.

Unsupported or mismatched A380X actions should be kept explicit rather than
silently pretending to work.

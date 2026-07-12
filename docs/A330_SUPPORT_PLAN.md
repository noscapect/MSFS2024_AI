# iniBuilds A330 support notes

Goal: add support for the built-in MSFS 2024 iniBuilds A330 by starting from
the existing iniBuilds Airbus flow and validating each system live.

Initial branch strategy:

- Detect aircraft titles containing `A330`.
- Route the aircraft through the shared Airbus procedure catalog.
- Reuse the iniBuilds native/MobiFlight adapter where controls match the
  A320neo V2/A321LR implementation.
- Keep mismatches aircraft-specific instead of weakening the proven A320/A321
  behavior.

Live-test priority:

1. Flow 1: batteries, external power, ADIRS, oxygen, NAV/LOGO, strobes, fire
   tests.
2. Flow 2 and 3: signs, flight computer preparation, APU, ATC pilot tasks.
3. Flow 4 and 5: engine start, after-start/taxi, spoilers, flaps, autobrake,
   weather radar and exterior lights.
4. Flow 6 and 7: before takeoff, takeoff/climb, gear, flap retraction,
   lighting.

Known assumptions to verify:

- The A330 package is close to the iniBuilds A320neo V2/A321LR but not fully
  compatible. Live testing has already found A330-specific handling for EXT
  A/B, NAV/LOGO, strobe, APU BAT, and fire-test completion.
- APU BAT readback is `L:INI_OVHD_ELEC_BAT_3_PB_IS_AUTO_SWITCH`; the
  Behavior Viewer input event is `AIRLINER_ELEC_APU_BAT`. The readback is
  inverted for flow purposes: `0` means ON/AUTO, `1` means OFF.
- The A330 approach flap schedule may need aircraft-specific gates after live
  testing, similar to the A321LR.

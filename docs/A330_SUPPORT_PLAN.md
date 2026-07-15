# iniBuilds A330 support notes

Goal: add support for the built-in MSFS 2024 iniBuilds A330 by starting from
the existing iniBuilds Airbus flow and validating each system live.

Current status (2026-07-15): A330 support is included in `main` as an explicit
experimental/work-in-progress profile during the pre-1.0 beta phase. Flows
1-4 passed live testing. Flow 5 and Flow 6 have partial control validation but
have not completed end-to-end sign-off. Flows 7-12 remain pending. Users must
remain ready to intervene manually.

A330 changes must remain in `A330ProcedureLibrary`, `A330ChecklistLibrary`,
and explicit A330 command/readback branches. They may not modify the frozen
A321 control profile or relax A321 verification.

Initial branch strategy:

- Detect aircraft titles containing `A330`.
- Route the aircraft through the dedicated A330 procedure catalog.
- Reuse the iniBuilds native/MobiFlight adapter where controls match the
  A320neo V2/A321LR implementation.
- Keep mismatches aircraft-specific instead of weakening the proven A320/A321
  behavior.

Remaining live-test priority:

1. Complete Flow 5 end-to-end validation: after-start/taxi, spoilers, flaps,
   autobrake, weather radar and exterior lights.
2. Complete Flow 6 validation: before-takeoff lights, TCAS and altitude
   reporting.
3. Validate Flow 7: takeoff/climb, gear, flap retraction and lighting.
4. Validate Flows 8-12 through cruise, approach, landing, taxi-in and shutdown.

Known assumptions to verify:

- The A330 package is close to the iniBuilds A320neo V2/A321LR but not fully
  compatible. Live testing has already found A330-specific handling for EXT
  A/B, NAV/LOGO, strobe, APU BAT, and fire-test completion.
- APU BAT readback is `L:INI_OVHD_ELEC_BAT_3_PB_IS_AUTO_SWITCH`; the
  Behavior Viewer input event is `AIRLINER_ELEC_APU_BAT`. The readback is
  inverted for flow purposes: `0` means ON/AUTO, `1` means OFF.
- The A330 approach flap schedule may need aircraft-specific gates after live
  testing, similar to the A321LR.

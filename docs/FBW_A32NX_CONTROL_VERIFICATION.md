# FBW A32NX control verification protocol

Branch: `feature/fbw-a320neo-support`

This document is the source-of-truth workflow for adding FBW A32NX support. Do not add controls by guessing LVars from behavior screenshots or generic MSFS SimVars first. Use the FBW source package and verify each control through the app bridge.

## Source priority

Use these sources in order:

1. FBW aircraft source behavior XML, especially `fbw-a32nx/src/behavior/src`.
2. FBW aircraft checklist and preset procedures:
   - `Checklist/Library.xml`
   - `config/a32nx/a320-251n/aircraft_preset_procedures.xml`
3. FBW docs/API pages.
4. Generic MSFS SimVars only as fallback or secondary cross-check.

## Verification rule

Each control must have a row with:

- source file and line/section;
- readback variable;
- command/write code;
- whether manual cockpit movement updates the app;
- whether app command moves the cockpit;
- whether verification is passive, command-state-backed, or manual-confirm-only.

## Current verified controls

| Control | Source readback | Source command | Manual readback | App command | App strategy |
|---|---|---|---|---|---|
| BAT 1 | `L:A32NX_OVHD_ELEC_BAT_1_PB_IS_AUTO` | `1 (>L:A32NX_OVHD_ELEC_BAT_1_PB_IS_AUTO, Bool)` | Verified updating after restart | Verified moves switch | Passive readback when captain acts |
| BAT 2 | `L:A32NX_OVHD_ELEC_BAT_2_PB_IS_AUTO` | `1 (>L:A32NX_OVHD_ELEC_BAT_2_PB_IS_AUTO, Bool)` | Verified updating after restart | Verified moves switch | Passive readback when captain acts |
| EXT PWR available | `L:A32NX_EXT_PWR_AVAIL:1`; cross-check `EXTERNAL POWER AVAILABLE` | N/A | LVar stayed false in first test | N/A | Use any verified true source |
| EXT PWR ON | `L:A32NX_OVHD_ELEC_EXT_PWR_PB_IS_ON`; FBW checklist verifies `EXTERNAL POWER ON` | `1 (>L:A32NX_OVHD_ELEC_EXT_PWR_PB_IS_ON, Bool)` when available; FBW checklist uses `TOGGLE_EXTERNAL_POWER` | LVar stayed false in first test | Not tested from app | Use any verified true source |

## Repeatable test flow

For every blocked FBW control:

1. Open Tools & diagnostics in the app.
2. Click `FBW bridge status` before touching the cockpit control.
3. Move the cockpit control manually.
4. Click `FBW bridge status` again.
5. If the readback did not change, use the app command button if available.
6. Click `FBW bridge status` again.
7. Compare:
   - documented FBW LVar readback;
   - generic SimConnect state;
   - actual cockpit visual state.

Only after this three-point check should code be changed.

## Flow 1 current operating policy

- BAT 1/2 remain captain actions.
- EXT PWR remains a captain action.
- If passive readback works, the app proceeds automatically.
- If passive readback fails, the user may press Confirm during discovery builds, but the mapping must stay marked as incomplete until source-backed readback is verified.

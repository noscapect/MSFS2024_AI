# A320 control mapping

This document started as the iniBuilds A320neo V2 mapping record. The app now
also supports the iniBuilds A321LR through a dedicated procedure/checklist
catalog and an A321 control profile for mappings that require isolation. The
FlyByWire A32NX uses separate FBW-specific state and command mappings.

## Evidence levels

- **Documented preset**: present in `docs/events.txt`, exported from MobiFlight HubHop.
- **Aircraft-discovered**: exposed by the loaded A320 through the MSFS Input Event catalog.
- **Live verified**: command was executed and its resulting aircraft state was independently read back.
- **Inferred**: based on naming or another iniBuilds aircraft; never eligible for an automatic procedure.

## Current documented coverage

The supplied HubHop export contains 249 A320 controls across autopilot/FCU,
brakes, ECAM, EFIS, electrical batteries, one engine-mode command, exterior
strobe lights, both MCDUs, and master caution/warning.

It does not document A320 presets for:

- APU master, start, bleed, or generator
- Main fuel pumps
- ADIRS selectors
- Seat-belt signs
- Beacon switch
- Passenger/cargo door operation
- External power

Those missing controls require another established profile or an official
aircraft/MSFS interface. An Input Event name alone is not enough evidence of
command semantics or readback behavior.

Passenger and cargo door closed-state monitoring is now implemented through
the official indexed `EXIT OPEN`, `EXIT TYPE`, and exit-position SimVars.
Door actuation and boarding-complete context remain outside this mapping.

Behavior Viewer discoveries and live-test evidence are recorded in
`docs/behavior-viewer-mappings.csv` before they are admitted to automatic
flows.

The read-only SimConnect inspection in
`docs/before-start-input-event-inspection.tsv` confirms that the named APU,
ADIRS, seat-belt, and fuel-pump events exist and expose a `FLOAT64` parameter.
It does not provide binding semantics or independent native readback, so none
of those controls is eligible for automation yet.

A passive manual test captured value transitions for all three ADIRS events
and established selector values OFF=0 and NAV=1. Controlled live testing then
verified `SetInputEvent` operation against separate `INI_IRS1_STATE`,
`INI_IRS2_STATE`, and `INI_IRS3_STATE` native readbacks. The complete flow
uses `INI_IRS_ON_BATTERY` to wait between selectors. APU and fuel-pump events did not emit subscription
updates. A polling repeat also left those Input Events at 0 while independent
APU SimVars changed, proving that the APU events are not usable as state
readback. Later native LVar monitoring independently established all six
fuel-pump states.

Behavior Viewer subsequently exposed the ENG 1 L1 Mouserect code. It toggles
both `INI_OUTER_TANK_LEFT` and `__FUEL_ENG1_L1IsPressed`; the Input Event Set
code only changes `_ButtonAnimVar`. The selector/press pattern was mapped to
all six pump buttons and independently verified against six pump-on LVars.

The first APU MASTER test showed that the parameterless
`AIRLINER_APU_MASTER_PUSH` calculator command does not actuate the control.
The expanded PUSH binding semantics must be captured before further testing.

Direct Input Event and B-event approaches were retired after clean
verification failures. Fuel pumps use exact Mouserect calculator code;
APU master/start use native command LVars. Every action is verified against
separate native state LVars.

The direct Input Event retirement applies to the APU and fuel-pump controls
that failed testing. ADIRS rotary Input Events are independently verified and
are supported.

Before Start uses exact Mouserect code for the six fuel pumps and APU bleed,
native command/state pairs for APU master/start, and observes the APU generator
pushbutton in its normal ON configuration. External power OFF is permitted
only after native `INI_APU_AVAILABLE` and `INI_APU_GEN_ON` are both true.

## Procedure readiness

| Procedure action | Command source | Readback source | Status |
|---|---|---|---|
| BAT 1 ON | HubHop LVar preset | Native LVar | Live verified |
| BAT 2 ON | HubHop LVar preset | Native LVar | Live verified |
| External power ON | MSFS SimConnect event | MSFS SimVar | Live verified |
| External power OFF while engines stopped | SimConnect event | Native APU available + generator state | Guarded automatic action |
| NAV & LOGO selector position 2 | Behavior Viewer `AIRLINER_LT_NAVLOGO_STATE1` | `INI_LOGO_LIGHT_SWITCH == 0` | Live verified by user |
| NAV & LOGO selector OFF | Behavior Viewer `AIRLINER_LT_NAVLOGO_STATE3` | `INI_LOGO_LIGHT_SWITCH == 2` | Live verified by user |
| Six fuel pumps ON/OFF | Exact Behavior Viewer Mouserect selector/press toggles | Six native `INI_*_PUMP_ON` LVars | Live verified in both directions |
| APU generator ON | Missing from supplied A320 export | Generic SimVar only | Unverified; excluded from automatic flow |
| Doors closed/boarding complete | Operational confirmation required | No complete mapping | Captain confirmation |
| Beacon ON | MSFS SimConnect event | MSFS SimVar | Live verified |

The complete Cockpit Preparation flow passed live on June 19, 2026. See
`docs/LIVE_TESTS.md` for the captured command and readback evidence.

## Refresh workflow

After changing `docs/events.txt`, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Refresh-ControlCatalog.ps1
```

This regenerates `docs/control-matrix.csv`, validates unique preset names and
required battery controls, and regenerates the application catalog in
`src/Copilot/Controls/IniBuildsA320ControlCatalog.Generated.cs`.

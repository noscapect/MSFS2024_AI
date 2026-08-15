# PMDG 777-300ER integration status

## Status

Integration remains a deliberately pre-operational profile. The app now
recognizes the PMDG aircraft's installed `777-300ER` title, exposes a distinct
`Pmdg777300Er` variant, validates the SimBrief `B77W` contract, and records the
official PMDG 777X SDK client-data identifiers. The complete dedicated
twelve-flow gate-to-gate procedure and checklist catalog is visible. Flow 1 is
live validated through its complete power-up sequence. Flow 2 now follows the
PMDG checklist and the app's Boeing crew split: Captain flight-deck/UFT/CDU/
clearance setup, then detailed FO preflight actions and observations. It is
ready for ordered live validation. The aircraft remains a development
integration rather than operationally supported.

## Research basis

The local `pmdg-aircraft-77w` package and its shipped
`Documentation/SDK/PMDG_777X_SDK.h` are authoritative for the initial adapter
boundary. The header defines:

- telemetry: `PMDG_777X_Data`, ID `0x504D4447`, definition `0x504D4448`;
- controls: `PMDG_777X_Control`, ID `0x504D4449`, definition `0x504D444A`;
- options file: `777_Options.ini` with `[SDK] EnableDataBroadcast=1`.

These identifiers are independent from the 737's `PMDG_NG3_Data` and
`PMDG_NG3_Control` areas. No 737 data offsets, switch semantics, commands,
procedures, checklists, or completion assumptions may be reused merely because
both aircraft are Boeing products.

## Completed bootstrap

1. Exact identity matching for the installed `777-300ER` title and explicit
   `PMDG 777-300ER` titles, without accepting another vendor's 777.
2. Package discovery for `pmdg-aircraft-77w` aircraft metadata and thumbnails.
3. Dedicated SDK names and IDs in `Pmdg777ControlProfile`.
4. SimBrief `B77W` validation and 777 takeoff-flap normalization for 5, 15, and
   20.
5. Dashboard development-state and independent 777X SDK readiness diagnostics.
6. Regression tests proving the 777 cannot inherit the released PMDG 737
   profile or its NG3 namespace.
7. Exact 684-byte 777X data subscription and a tested read-only parser for the
   Flow 1 battery, electrical, hydraulic, wiper, gear, alternate-flap, exterior
   light, pack/recirculation, ADIRU, emergency-light, and parking-brake state.
8. A dedicated twelve-flow procedure and checklist catalog based on the PMDG
   tutorial's preparation, departure, cruise, descent, approach, landing and
   shutdown sequence. Flow 1 now includes BATTERY, both available external
   power sources, and ADIRU actions plus PMDG-backed electrical/hydraulic
   starting-state verification. Each item uses deliberate cockpit-scan timing.
9. A unique SimConnect request ID for the 777 data block, payload-type guards,
   fatal exception logging, and idempotent application disposal. This prevents
   unrelated MobiFlight float callbacks from entering the 777 parser.
10. Flow 2 exact readbacks for IFE, cabin utility, emergency-light selector and
    guard, navigation light, right flight director, source selectors, display
    formats, console positions, IRS alignment, CDU route/performance/takeoff
    data, and the PMDG electronic PREFLIGHT checklist. Captain UFT/CDU work is
    explicit, while MCP setup remains in Before Start per PMDG SOP.
11. Failure recovery retries the current step. It cannot skip a failed action.
12. Flow 2 contains no manual-confirmation placeholders for FO work. Engine,
    fuel, fire, anti-ice, exterior-light, air-system and autobrake configuration
    is commanded by the virtual FO and verified from the 777X data block. The
    final app checklist gate is computed from the complete FO readback set.
13. The user-facing Flow 2 is consolidated to 23 SOP-level steps rather than
    exposing every individual switch as a separate step. Its grouped FO actions
    still retain per-switch PMDG commands and independent readbacks. Radio and
    audio panels are never changed by Flow 2 while SayIntentions owns
    communications; only the non-invasive transponder altitude-source readback
    is checked.
14. The Flow 2 signs configuration commands and independently verifies the
    no-smoking selector at AUTO and the seat-belt selector at OFF, matching the
    PMDG preflight procedure. Flow 3 then commands and verifies the seat-belt
    selector at AUTO during Before Start.
15. Flow 3 contains no manual First Officer confirmations. Required doors and
    the electronic BEFORE START checklist are telemetry observations; APU
    start, generator/bleed supply, external-power disconnect and seat-belts
    AUTO are PMDG-commanded actions with independent SDK readbacks.

## Prepared flow catalog

1. Power Up & Preliminary Preflight
2. Flight Deck & Preflight
3. Before Start & Pushback
4. Engine Start
5. Before Taxi & Taxi
6. Before Takeoff
7. Takeoff & Climb
8. Climb & Cruise
9. Descent Preparation
10. Approach & Landing
11. After Landing & Taxi In
12. Parking, Shutdown & Secure

The canonical procedure IDs match the other gate-to-gate aircraft profiles so
the desktop and EFB clients can navigate them consistently. The procedures and
checklists themselves are 777-specific objects and do not reuse the 737
library. An unknown checklist result means that the crew must confirm the item;
it must not be presented as telemetry-verified.

## Crew-role contract

The integration models the Captain as pilot flying and the virtual First
Officer as pilot monitoring unless a step explicitly says otherwise. Boeing
procedures assigned to either pilot may be performed by the virtual First
Officer when that is the configured split. In particular, electrical power-up
and BATTERY ON are valid Captain-or-First-Officer preliminary-preflight duties;
this app assigns them to the First Officer. During engine start, the Captain
operates the START selectors and the First Officer moves the fuel-control
switches to RUN and monitors the starts. The First Officer sets takeoff flaps,
taxi lights, landing gear and airborne flap selections on the Captain/PF's
command. Procedure-role tests protect these assignments from regression.

The Flow 1 battery test proved that the official PMDG 777X control event with
direct ON position semantics moves the cockpit switch. Investigation then
found that the active PMDG WASM work-folder options file lacked `[SDK]
EnableDataBroadcast=1`; the apparent payload was a zero-filled client area,
not published aircraft state. The parser now rejects that block unless
`AircraftModel` is `6` for the 777-300ER. Generic SimConnect `ELECTRICAL MASTER
BATTERY:1` remains rejected. After PMDG reload, the battery step will complete
only from `ELEC_Battery_Sw_ON`.

The first valid broadcast exposed a separate command gate: telemetry was ready
but PMDG had not emitted an initial control-area callback. That callback is not
a prerequisite in PMDG's shipped connection sample. The command now requires
the initialized control mapping and valid model `6` telemetry, then relies on
the control-area reset only for pending-command tracking.

## Next implementation slice

1. Live-validate revised Flow 2 in order, including the separate emergency
   lights ARMED and guard CLOSED event/readback pairs; stop at the first failure.
2. Confirm that the PMDG electronic PREFLIGHT checklist flag changes only when
   the cockpit checklist is genuinely complete.
3. Progress through Flows 3-12 in order, adding readbacks and tightening manual
   gates only after the preceding flow passes live validation.
4. Enable each later automatic action only after its 777X event semantics,
   independent readback, momentary/held behavior, and in-simulator result are
   verified.

Until those stages are complete, the detected 777 remains a development
integration and the pilot must not expect procedure assistance or cockpit
automation from the app.

# PMDG 777-300ER integration status

## Status

Integration is a deliberately pre-operational profile. The app now
recognizes the PMDG aircraft's installed `777-300ER` title, exposes a distinct
`Pmdg777300Er` variant, validates the SimBrief `B77W` contract, and records the
official PMDG 777X SDK client-data identifiers. The complete dedicated
twelve-flow gate-to-gate procedure and checklist catalog is visible. Flow 1
has initial read-only telemetry; unvalidated steps throughout the catalog are
manual crew confirmations. The app does not execute 777 cockpit commands or
classify the aircraft as operationally supported.

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
   shutdown sequence. Every flow contains zero automatic commands at this
   integration stage.
9. A unique SimConnect request ID for the 777 data block, payload-type guards,
   fatal exception logging, and idempotent application disposal. This prevents
   unrelated MobiFlight float callbacks from entering the 777 parser.

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

## Next implementation slice

1. Live-validate the Flow 1 structure size, offsets, selector semantics, and
   checklist results from cold and dark at the gate.
2. Capture before/after data for each candidate Flow 1 control event without
   enabling automatic execution.
3. Progress through Flows 2-12 in order, adding readbacks and tightening manual
   gates only after the preceding flow passes live validation.
4. Enable each automatic action only after its 777X event semantics,
   independent readback, momentary/held behavior, and in-simulator result are
   verified.

Until those stages are complete, the detected 777 remains a development
integration and the pilot must not expect procedure assistance or cockpit
automation from the app.

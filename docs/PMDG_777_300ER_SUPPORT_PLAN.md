# PMDG 777-300ER integration status

## Status

Integration has started as a deliberately non-operational profile. The app now
recognizes the PMDG aircraft's installed `777-300ER` title, exposes a distinct
`Pmdg777300Er` variant, validates the SimBrief `B77W` contract, and records the
official PMDG 777X SDK client-data identifiers. It does not yet publish a 777
procedure catalog, execute cockpit commands, or classify the aircraft as
supported.

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
5. Dashboard development-state messaging with procedures and controls disabled.
6. Regression tests proving the 777 cannot inherit the released PMDG 737
   profile or its NG3 namespace.

## Next implementation slice

1. Mirror the exact shipped `PMDG_777X_Data` structure with an independently
   verified size and field offsets.
2. Subscribe to the 777 data area only while the exact 777 variant is loaded,
   and expose an SDK-ready diagnostic independently from NG3 readiness.
3. Map a read-only cold-and-dark electrical/APU state before enabling commands.
4. Research and encode a dedicated twelve-flow 777-300ER procedure and
   checklist catalog.
5. Enable each automatic action only after its 777X event semantics,
   independent readback, momentary/held behavior, and in-simulator result are
   verified.

Until those stages are complete, the detected 777 remains a development
integration and the pilot must not expect procedure assistance or cockpit
automation from the app.

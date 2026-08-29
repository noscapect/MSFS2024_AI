# Version 0.9.8

Version 0.9.8 adds the PMDG 777-300ER development integration and improves
runtime-state ownership across the supported aircraft adapters.

## Highlights

- Added the dedicated PMDG 777-300ER twelve-flow procedure and checklist
  catalog, its isolated 777X SDK adapter, and PMDG-specific command/readback
  paths. The profile remains a development integration pending ordered live
  validation and is not operationally supported.
- Extracted native, PMDG NG3, PMDG 777, and Asobo 737 MAX runtime/readback
  state into focused components, preserving aircraft-specific control paths.
- Hardened reconnection/runtime-generation handling and FlyByWire approach
  configuration gates.
- Expanded regression coverage to 497 automated desktop tests.

## Compatibility

The desktop app works without the EFB companion, GSX Pro, SayIntentions, or
SimBrief. Every integration remains optional and normal flows continue with
any combination of them enabled or unavailable.

The iniBuilds A310-300 implementation is complete for its current scope. The
Asobo 737 MAX 8 profile remains experimental. This is beta assistance
software: the pilot remains responsible for aircraft
configuration, flight-path control, and deciding whether it is safe to
continue each phase.

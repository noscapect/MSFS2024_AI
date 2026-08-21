# Version 0.9.7

Version 0.9.7 expands aircraft coverage and strengthens native cockpit-state
verification throughout gate-to-gate operation.

## Highlights

- Added the dedicated twelve-flow iniBuilds A310-300 procedure and checklist
  framework, using A310-specific commands and native
  readbacks instead of modern-Airbus mappings.
- Expanded A310 startup, payload, takeoff, climb, approach, landing, and
  turnaround automation, including corrected gear, altimeter, and approach
  flap verification.
- Fixed FlyByWire A32NX landing-gear verification when generic wheel telemetry
  remains stale after the cockpit handle moves.
- Made the native A32NX flap-handle signal authoritative across recurring
  simulator updates, preventing false flap completion during climb and taxi-in.
- Added the A32NX seat-belt-sign action above 10,000 feet and retained the
  approach/descent sign transition.
- Expanded regression coverage to 437 automated desktop tests.

## Compatibility

The desktop app works without the EFB companion, GSX Pro, SayIntentions, or
SimBrief. Every integration remains optional and normal flows continue with
any combination of them enabled or unavailable.

The iniBuilds A310-300 implementation is complete for its current scope. The
Asobo 737 MAX 8 profile remains experimental. This is beta assistance
software: the pilot remains responsible for aircraft
configuration, flight-path control, and deciding whether it is safe to
continue each phase.

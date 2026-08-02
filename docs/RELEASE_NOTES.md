# Version 0.9.6

Version 0.9.6 focuses on a clearer desktop experience and an optional
in-simulator companion, while preserving the aircraft-specific safety and
verification boundaries of the existing flows.

## Highlights

- Redesigned the desktop dashboard around the active flow, next required
  action, aircraft telemetry, and concise integration status.
- Added the optional MSFS 2024 EFB companion build 0.2.10. It can start the
  first or next flow, select a flow, confirm pilot actions, pause, resume, and
  cancel without leaving the simulator.
- Added resilient, bounded EFB/desktop synchronization and visible GSX prompt
  choices for tug, deicing, and pushback questions.
- Improved optional GSX coordination for boarding gates, pushback readiness,
  engine-start prompts, arrival-stand handoff, paged position menus, and
  correct boarding/deboarding status.
- Improved exact-event callout delivery and aircraft-state verification for
  takeoff, gear, flap, and ground-spoiler actions.
- Strengthened flow sequencing so taxi and runway flows cannot be completed
  prematurely from an unrelated aircraft phase.
- Included the dedicated Asobo 737 MAX 8 profile as **experimental support**.
  Its first six flows have received iterative testing, but Flow 7 and the
  complete profile are not cleared for unattended use.

## Compatibility

The desktop app works without the EFB companion and without GSX Pro,
SayIntentions, or SimBrief. Every integration remains optional and normal
flows continue with any combination of them enabled or unavailable.

This remains beta assistance software. The pilot is responsible for aircraft
configuration, flight-path control, and deciding whether it is safe to
continue each phase.

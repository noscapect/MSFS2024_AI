# FSFO product benchmark

Reviewed June 19, 2026:

- https://flightsimfirstofficer.com/
- https://flightsimfirstofficer.com/about/
- https://flightsimfirstofficer.com/products/

## Patterns worth adopting

FSFO presents the first officer as an active crew member rather than a passive
checklist reader. The useful product patterns for our application are:

- Flows and checklists are separate concepts. A flow performs or prompts work;
  the checklist verifies the resulting aircraft state.
- The user chooses how much work the first officer performs.
- Actions can be triggered manually or from aircraft state.
- Checklist items verify actual switch/system state where telemetry exists.
- Missed procedures are surfaced rather than silently ignored.
- Callouts and crew prompts are tied to flight phase.
- Aircraft support is implemented as an aircraft-specific profile.
- The main application makes active flow, current step, aircraft state, and
  configuration visible.

## Current project scope

For the present milestone, our application will cover:

1. Cockpit Preparation
2. Before Start
3. Engine Start
4. After Start
5. Taxi
6. Before Takeoff
7. Takeoff and Initial Climb
8. Climb to Cruise

Voice control, FMC/MCDU entry, pushback, cabin announcements, RAAS, career mode,
and post-cruise phases are deliberately deferred.

## Product differences

This project uses SimConnect as its primary integration and keeps procedure
logic separate from aircraft control mappings. Unsupported iniBuilds controls
remain visible manual steps until a reliable adapter is proven.

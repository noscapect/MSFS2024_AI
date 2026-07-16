# Product vision

MSFS 2024 Virtual First Officer is an external application that performs the
duties of a first officer in supported Microsoft Flight Simulator 2024 aircraft
throughout a complete gate-to-gate operation.

The app supports the iniBuilds A320neo V2, iniBuilds A321LR, iniBuilds
A330-300 (GE), FlyByWire A32NX for MSFS 2024, and PMDG 737-800. Each supported
aircraft routes through a dedicated procedure profile and guarded command
implementation.

The application begins with a cold-and-dark aircraft at the departure gate and
continues through shutdown and passenger offboarding at the destination.

## Product boundaries

- The application runs externally to Microsoft Flight Simulator 2024.
- SimConnect is the primary simulator integration.
- Aircraft-specific adapters supplement SimConnect only when a required
  aircraft control is not available through a working standard interface.
- Operational procedures are deterministic, observable, and configurable.
- Every automated action has preconditions, a timeout, and a verified
  postcondition.
- The pilot can pause automation, skip or retry steps, take control, and
  configure the division of duties.
- Hermes Agent is not a runtime dependency. It may be used during development.
- An in-game panel or optional AI integration may be added later without
  changing the procedure and aircraft-control core.

## Operational scope

1. Preflight and cockpit preparation
2. Aircraft electrical power-up
3. ADIRS alignment and initial setup
4. MCDU and flight-plan preparation
5. Performance initialization
6. Before-start preparation and checklist
7. Pushback and engine start
8. After-start flow and checklist
9. Taxi-out
10. Before-takeoff flow and checklist
11. Takeoff monitoring and callouts
12. Climb
13. Cruise monitoring
14. Descent preparation
15. Descent
16. Approach preparation and checklist
17. Approach and landing monitoring
18. After-landing flow
19. Taxi-in
20. Parking, shutdown, securing, and offboarding

## First-officer responsibilities

The target includes:

- Cockpit flows and switch operation
- Checklist challenge-and-response
- Flight-phase monitoring
- Standard callouts
- Mode and configuration monitoring
- Radio and transponder management
- MCDU data entry where supported
- Performance-data handling
- Engine-start support
- Exterior-light management
- System-page selection and monitoring
- Abnormal-state detection and escalation
- Ground-service coordination hooks
- Configurable pilot-flying and pilot-monitoring duties

The application does not silently improvise procedures. Aircraft actions come
from versioned procedure definitions and aircraft capability mappings.


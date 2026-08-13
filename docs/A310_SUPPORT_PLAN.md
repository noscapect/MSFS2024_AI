# iniBuilds A310-300 support status

## Status

The iniBuilds Airbus A310-300 has a dedicated twelve-flow procedure catalog,
checklist library, aircraft identity, SimBrief ICAO contract, approach
schedule, and control-profile boundary. Its implementation is **complete for
the current gate-to-gate scope**. Further work is maintenance, simulator-update
compatibility, and field validation rather than planned feature expansion.

The preliminary-panel flow has isolated automatic F/O actions for BAT 1/2/3,
wipers/radar, APU fire testing, IRS, oxygen, annunciator testing, and initial
exterior lights, with A310-native readbacks. Flow 2 additionally maps signs,
ATS/flight-control computers, window/probe heat, emergency lights,
cargo-smoke and EGPWS tests, autobrake deselection, rudder-trim reset, TCAS
preflight mode, and weather-radar OFF. Flow 3 maps APU start and availability,
APU generator/bleed, beacon, XPDR ground mode, and external-power disconnect,
and uses the shared SayIntentions/GSX pushback-clearance gate. Cockpit actions
without a safe validated interface deliberately remain manual, while
informational scans and conditional reminders do not create confirmation
gates. The app must never send A320,
A321, A330, FBW, PMDG, or 737 MAX commands to the A310 merely because a panel
or control looks similar.

## Research basis

The operational sequence comes from the official
[iniBuilds A310-300 MSFS Manual v2](https://flightsimulator.azureedge.net/wp-content/uploads/2023/02/A310_MSFS_Manual.pdf),
linked by the
[Microsoft Flight Simulator aircraft-manual catalog](https://www.flightsimulator.com/aircraft-manuals/).
The manual explicitly describes a simulation-oriented, single-pilot-capable
normal procedure assembled from operator material.

The local MSFS 2024 streamed package
`fs24-microsoft-aircraft-a310-300` was also inspected. Its panel-state files
confirm an aircraft-specific `a310_` state namespace, including three
batteries, APU, bleed/pack, ignition, hydraulic, electrical-bus and flap
states. Those names are recorded in `A310ControlProfile`. Directly writing an
internal state variable is prohibited unless it is an aircraft control
interface with a live panel-state readback.

## Implemented flow content

| Flow | A310-specific coverage |
| --- | --- |
| 1. Preliminary Cockpit Preparation | Three batteries AUTO; hydraulic ground safety; wipers, radar, fuel levers and reversers safe; external/APU power; APU fire test before IRS; three IRS selectors; ISDU; oxygen; annunciator test; initial exterior lights and VHF radios |
| 2. Flight Deck Preparation & Pre-Flight | FMC route and performance; loadsheet/fuel; V-speeds and TRP; signs; hydraulic/servo, electrical, fire, fuel, pressure, bleed, conditioning, heat, smoke and ventilation panels; ATS/yaw dampers; instruments; EGPWS; brakes; takeoff-warning test; ATC/TCAS and IFR clearance |
| 3. Before Start & Pushback | APU power and bleed; checklist to/below the line; doors/slides; beacon; parking brake; elapsed time; XPDR ground mode; external-power disconnect |
| 4. Engine Start | Area clear; ignition A/B; packs closed; Engine 2 first; fuel lever ON at 20% N2; stable-start monitoring; Engine 1 repeated |
| 5. After Start & Taxi | Ignition/APU/anti-ice; spoilers armed; rudder trim zero; performance slat/flap setting; pitch trim from ECAM takeoff CG; taxi and brake check; full flight-control check; 250-knot preselect, PROF/NAV and flight directors; autobrake MAX; radar/transponder and takeoff-config test |
| 6. Before Takeoff | Holding-point gate; runway/clearance verification; brake fan/temperature limits; full takeoff-light configuration; ignition and packs as required; TCAS TA/RA; below-the-line checklist |
| 7. Takeoff & Climb | 40% N1 stabilization and go-levers; 100/V1/Rotate; positive climb and gear UP; AP as required; CL thrust; flap then slat retraction at F/S speeds; spoilers disarmed; gear lever OFF; packs restored sequentially with ten-second spacing; lights/APU; transition-altitude and 10,000-foot actions |
| 8. Cruise | TRP CR; ECAM/status/system scan; waypoint and fuel-progress checks; signs as required |
| 9. Descent Preparation | Weather/runway, ECAM status, landing elevation, fuel, FMS arrival/approach, DH, autobrake, GPWS alternate-flap selection and full approach briefing |
| 10. Approach & Landing | QNH, signs/lights, 15/0 at 245 kt, LAND/LOC/G/S monitoring, 15/15 at 210 kt, gear and spoilers, 15/20 at 195 kt, 30/40 at 180 kt, configured/stable gates, minimums, touchdown, spoilers, reverse and 80-knot callout |
| 11. After Landing & Taxi | Lights; anti-ice/ignition; APU; spoilers; transponder/radar; pitch trim 1° nose up; staged flap retraction with icing exception; brake temperatures; taxi/gate coordination |
| 12. Parking & Securing | Nose light before stand; APU/external power before shutdown; engines/beacon; zero differential pressure; signs; APU-specific left-inner Pump 2 exception; probe heat; IRS/brake fans; turnaround choice; ten-second IRS memory delay; oxygen, lights, CRTs, APU, emergency lights and all three batteries |

## Published limits represented in the flow

- Slats 15 / flaps 0: maximum 245 kt / M 0.54
- Slats 15 / flaps 15: maximum 210 kt
- Slats 15 / flaps 20: maximum 195 kt
- Slats 30 / flaps 40: maximum 180 kt
- Gear operation and extended limit: 270 kt, subject to the manual's Mach
  limits
- Standard ILS target: gear no later than five miles, fully configured by
  1,000 ft AGL, stable by 500 ft AGL

These are guards for the normal flow, not a substitute for the aircraft
manual, current charts, performance calculation, ECAM, or abnormal procedure.

## Maintenance boundary

The current implementation is frozen unless a simulator compatibility issue,
field-validation correction, or clearly bounded safety improvement justifies a
change. A manual A310 item can become automatic only after all of the following
exist:

1. Exact aircraft identification in MSFS 2024.
2. A native Input Event or aircraft behavior event captured from the A310.
3. An independent state readback that cannot be satisfied merely by sending
   the command.
4. Correct selector-value semantics and momentary/held timing.
5. A cold-and-dark and in-sequence live test.
6. A regression test proving the command remains inside the A310 boundary.

There is no remaining planned A310 mapping backlog for the current product
scope.

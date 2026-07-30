# Asobo 737 MAX 8 Support Status

## Status

The Asobo 737 MAX 8 is an unreleased, experimental aircraft profile. It was
merged to `main` after iterative cold-and-dark testing, but no new public
release was built. It is not yet gate-to-gate validated. Active development is
paused while the project focuses on the desktop UX redesign and MSFS 2024 EFB
companion.

- Flows 1–6: implemented and iteratively live tested; further regression
  flights remain necessary.
- Flow 7: implemented, but **not cleared for unattended use** after the first
  takeoff attempt ended in a crash.
- Flows 8–12: implemented structurally; full MAX live validation remains
  outstanding.

The profile is independent from PMDG and owns:

- `Asobo737MaxProcedureLibrary`
- `Asobo737MaxChecklistLibrary`
- `Asobo737MaxControlProfile`
- the `asobo737max` command namespace
- native SimConnect Input Event polling and MAX-specific normalization

## Live-derived control mappings

The live test established several aircraft-specific selector conventions:

| Control | Native value/state |
| --- | --- |
| PACK | AUTO `1`, OFF `2` |
| Isolation valve | OPEN `0`, AUTO `1` |
| Electric hydraulic pump | ON `0`, OFF `1` |
| Taxi light | AUTO `0`, OFF `1` |
| Runway turnoff lights | ON `0`, OFF `1` |
| Fixed landing lights | ON `0`, OFF `1` |
| Anti-collision light | ON `0`, OFF `1` |
| XPDR selector | STBY `0`, AUTO `1`, ON `2` |
| TCAS operating mode | TA/RA `3` |

These values must not be replaced with generic Boeing, PMDG, or iniBuilds
assumptions.

## Known takeoff safety gap

The app currently imports MAX V1/VR/V2 and takeoff flaps from SimBrief and can
move the flap handle to that imported target. It does not have a verified live
MAX FMC TAKEOFF REF readback. Missing FMC data has therefore been presented
too optimistically as a SimBrief/FMC match.

The app also does not currently record or verify:

- stabilizer trim position or takeoff green-band status;
- pilot control-column/yoke input;
- elevator command and actual elevator deflection;
- Speed Trim System commands;
- MCAS command state;
- hydraulic elevator authority;
- the aircraft's takeoff-configuration warning.

Until those signals are available, the generic "Takeoff configuration normal"
callout is not valid evidence for the MAX.

## July 28 takeoff incident

The first Flow 7 takeoff recording showed:

- both engines near 99% N1;
- parking brake released;
- spoilers down;
- Flaps 5, handle index 3, surfaces approximately 12.5%;
- VR configured at 141 knots;
- pitch remaining near zero through approximately 192 knots;
- first airborne transition near 197 knots without a normal rotation;
- a subsequent descent and crash.

The app did not issue elevator or trim commands. The evidence rules out
insufficient thrust, deployed spoilers, and the parking brake as the immediate
cause. It does not establish whether the pilot input failed to reach the
elevator or whether stabilizer trim prevented normal rotation.

## Required before further Flow 7 validation

1. Add actual stabilizer-trim and takeoff-range telemetry.
2. Add pilot yoke/elevator-command and actual elevator-position recording.
3. Expose hydraulic/control-authority status relevant to elevator operation.
4. Record STS/MCAS activity if the aircraft exposes reliable state.
5. Detect or explicitly leave unknown the aircraft takeoff-configuration
   warning.
6. Change missing MAX FMC data from "match" to "unavailable/not verified."
7. Prevent a configuration-normal callout when required evidence is unknown.
8. Add a pre-takeoff control check and hard safety gate.
9. Repeat the takeoff manually with automation paused before re-enabling
   automatic Flow 7 actions.

## Release gate

The MAX profile may be considered release eligible only after:

- all required takeoff signals are independently verified;
- focused and full regression suites pass;
- cold-and-dark through shutdown is repeated successfully;
- the takeoff, climb, approach, landing, and turnaround flows are live tested;
- this status and `LIVE_TESTS.md` are updated with passing evidence.

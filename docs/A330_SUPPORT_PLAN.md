# iniBuilds A330 support notes

Goal: add support for the built-in MSFS 2024 iniBuilds A330 by starting from
the existing iniBuilds Airbus flow and validating each system live.

Current status (2026-07-16): all twelve A330 flows have completed live
gate-to-gate validation. The profile has dedicated procedures, checklists,
approach defaults, control mappings, command routing, and regression guards.

A330 changes must remain in `A330ProcedureLibrary`, `A330ChecklistLibrary`,
and explicit A330 command/readback branches. They may not modify the frozen
A321 control profile or relax A321 verification.

The completion branch adds a hard isolation boundary around the A330 profile:

- every automatic A330 procedure action is emitted in the dedicated `a330`
  command namespace;
- an `a330` cockpit command is rejected unless the loaded aircraft resolves
  specifically to `IniBuildsA330`;
- A330 procedure and step objects may not be shared with another aircraft;
- the complete twelve-flow structure, roles, commands, and checklist contract
  have a cryptographic regression fingerprint;
- the A330 uses its own approach profile and explicit Input Event readbacks.
- the A330 landing-light panel is modeled as its actual single ON/OFF switch,
  using `AIRLINER_LDGLIGHT_TOGGLE`; it never inherits the A320's paired
  retractable landing-light selectors or RETRACT terminology.

SimBrief remains optional. A generated A333 OFP is accepted for the built-in
A330-300 and can provide route, runway, cruise, fuel, and takeoff-flap context.
An A339 OFP is reported as an aircraft mismatch. With no configured or active
SimBrief flight, all A330 flows continue using aircraft state and saved/default
settings; network availability is never a flow precondition.

Implemented branch strategy:

- Detect the built-in `A330-300 (GE)`/`iniBuilds A330` signatures only; other
  developers' A330 aircraft remain unsupported instead of inheriting this
  profile.
- Route the aircraft through the dedicated A330 procedure catalog.
- Reuse the iniBuilds native/MobiFlight adapter where controls match the
  A320neo V2/A321LR implementation.
- Keep mismatches aircraft-specific instead of weakening the proven A320/A321
  behavior.

Live-test result: Flows 1-12 completed gate-to-gate. Approach configuration,
landing, after-landing flap cleanup, APU start, and electrical readiness before
engine shutdown were verified and corrected during the completion pass.

Aircraft-specific implementation notes:

- The A330 package is close to the iniBuilds A320neo V2/A321LR but not fully
  compatible. Live testing has already found A330-specific handling for EXT
  A/B, NAV/LOGO, strobe, APU BAT, and fire-test completion.
- APU BAT readback is `L:INI_OVHD_ELEC_BAT_3_PB_IS_AUTO_SWITCH`; the
  Behavior Viewer input event is `AIRLINER_ELEC_APU_BAT`. The readback is
  inverted for flow purposes: `0` means ON/AUTO, `1` means OFF.
- The A330 has a dedicated heavy-Airbus decelerated-approach profile. The
  standard schedule uses distance-to-touchdown when available (CONF 1 at 16
  NM, CONF 2 at 11 NM, gear at 8 NM and landing configuration at 5 NM) and
  only falls back to altitude when distance telemetry is unavailable. The
  speed gates are 230/195/210/185/177 kt for CONF 1, CONF 2, gear, CONF 3 and
  FULL respectively. Airline-specific settings remain user-overridable.

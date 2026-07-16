# Live test evidence

## 2026-07-15 - PMDG 737-800 end-to-end validation complete

The PMDG 737-800 has completed live testing of all twelve Boeing-specific
flows from cold and dark through shutdown. The completed implementation uses
PMDG NG3 SDK readbacks and PMDG/Behavior Viewer-confirmed controls for power,
fire tests, IRS, pneumatics, engines, hydraulics, lights, TCAS, landing gear,
flaps, speedbrake, autobrake, approach, and after-landing cleanup.

Final corrections in the v0.8.1 validation cycle include the complete four-part
fire-warning test with bell cancellation, position-and-speed-gated Flaps 5,
and taxi-light selection immediately after leaving the runway. Released-profile
tests freeze the twelve-flow structure and reject PMDG commands leaking into or
from an Airbus procedure library.

Result: **Flows 1 through 12 passed. PMDG 737-800 support is considered stable
and protected from unrelated aircraft-development changes.** Future PMDG
changes require explicit contract review and renewed affected-flow validation.

## 2026-07-13 - iniBuilds A321LR end-to-end validation complete

The iniBuilds A321LR has now completed live testing of all twelve flows in the
current multi-aircraft application, from cold and dark through approach,
landing, taxi-in, parking, and shutdown.

Final corrections confirmed during this validation cycle:

- Flow 7 uses the physical A321 flap-handle readback as the only clean-detent
  authority. Generic surface SimVars may incorrectly report zero while the
  cockpit lever remains at Flaps 1.
- Flow 8 and Flow 10 keep seatbelt and no-smoking selectors in AUTO and verify
  the selector positions instead of requiring a particular cabin-sign output.
- Flow 12 leaves both selectors in AUTO during normal turnaround. NO SMOKING
  moves to OFF only after final cold-and-dark secure is explicitly confirmed.
- A321 procedures, checklists, flap commands, and sign mappings are now guarded
  by a dedicated stable-profile regression suite.

Result: **Flows 1 through 12 passed. A321LR support is considered stable and
closed to unrelated aircraft-development changes.** Future A321 modifications
require an A321 bug report or requested feature plus renewed affected-flow
validation.

## 2026-07-01 - iniBuilds A321LR branch live test status

Development branch: `feature/inibuilds-a321lr-support`.

The iniBuilds A321LR was detected from MSFS as aircraft title `A321` and is
handled as an iniBuilds A320-family aircraft. The branch reuses the verified
iniBuilds native/MobiFlight control path where the A321LR matches the A320neo
V2.

Live test results from the A321LR validation sessions:

- Flow 1, Power Up & Initial Setup: passed.
- Flow 2, Flight Computer & Pre-Flight: passed.
- Flow 3, APU Start & Pushback: passed.
- Flow 4, Engine Start Sequence: passed.
- Flow 5, After Start & Taxi: passed.
- Flow 6, Before Takeoff: passed.
- Flow 7, Takeoff & Climb: passed after flap-clean verification was changed to
  require the actual iniBuilds flap-handle detent.
- Flow 8, Cruise: passed.
- Flow 9, Descent Preparation: passed.
- Flow 10, Approach & Landing: passed with A321-specific approach flap-speed
  handling, distance-authoritative gear extension, landing callouts, and
  automatic handoff to Flow 11.
- Flow 11, After Landing & Taxi: passed after runway-cleanup ordering was
  corrected and APU BLEED was allowed while taxiing on the ground.
- Flow 12, Parking & Shutdown: passed, including gate shutdown and optional
  cold-and-dark secure.

The branch also changes the default Flow 11-to-12 chaining option to enabled.
Flow 12 immediately waits for parked-at-gate conditions, so this is safe for
hands-off landing/taxi/gate testing while still preventing shutdown actions
from running during taxi.

## Current verification status — 2026-06-23

User testing has verified the complete behavior of Flows 1 through 10,
including automatic aircraft actions, independent readback, sequencing,
lighting, landing configuration, and existing voice callouts.

The only items still awaiting live verification are:

- New-flight reset of active and completed procedure progress.
- Configurable approach-schedule overrides.
- Automatic flow chaining options, including the standard Flow 10-to-11
  handoff.
- Three-flight telemetry retention and 10x command-suppressed replay.
- Prioritized, non-overlapping voice callout playback.
- Procedure-session restoration after restarting the app or simulator.
- Late-start recovery for transient engine-start and takeoff milestones.
- Current-step telemetry and flap handle/surface sanity warnings.
- Version display and GitHub release update status.
- Revised Flow 1 timing: 45-second display/warning-system initialization gate
  before fire tests.
- Revised automatic-action pacing and one-second fuel-pump intervals.
- Revised Flow 10 exact flap-handle verification and earlier 230-knot
  CONFIG 1 trigger below 10,000 feet.
- Captured WXR/PWS selector InputEvent polarity: physical OFF=1, mode 1=0,
  mode 2=2. Flow 5 now commands mode 1 with `SetInputEvent(..., 0)`.
- Adjusted approach/landing sequencing after live-test observations: Flow 6
  leaves strobes in AUTO; Flow 10 calls cabin crew at the approach
  configuration point, selects flaps CONFIG 2 before gear down, and treats
  "Reverse green" as optional when reverse thrust is not used.
- Added distance-aware Flow 10 approach gates. The app now uses MSFS ATC
  runway distance or localizer DME, then uses altitude/speed fallback gates
  when neither runway-specific source is available. Generic GPS target and
  waypoint distances are rejected because an intermediate fix can reach zero
  many miles before touchdown. Standard gates are Flaps 1 at 15 NM/230 kt, Flaps 2 at
  10 NM/200 kt, gear down at 7 NM/210 kt, and landing configuration at
  5 NM/185 kt. Live validation pending.
- Landing test telemetry showed the current reverse-thrust SimVars did not
  report reverse engagement even when reverse was used. Flow 10 therefore
  still skips the Reverse Green callout when no positive readback is seen.
- Flow 11 now turns autobrake OFF at 70 kt after reverse is stowed, starts the
  APU while taxiing after landing, and retracts landing lights instead of
  setting them to OFF.
- Follow-up landing test showed Flow 11 waited for APU AVAIL before runway
  cleanup, delaying landing lights, spoilers, flaps, and transponder until
  taxi. Flow 11 now starts the APU early, performs after-landing cleanup at
  taxi speed, then waits for APU AVAIL before chaining to parking/shutdown.
- Follow-up recording showed Flow 10 selected nose light TAXI during approach.
  Flow 10 now selects the T.O. / landing nose-light position; Flow 11 sets TAXI
  only after landing during runway cleanup.
- Added quiet diagnostic capture for verification failures. The activity log
  remains player-friendly, while detailed expected/actual context and aircraft
  state snapshots are written to `%LOCALAPPDATA%\MSFS2024_AI\diagnostics` and
  can be exported with the new diagnostics buttons.
- Automatic handoff from Flow 10 to Flow 11.
- Flow 10 voice callout: `Cabin crew, prepare for landing`.
- Complete Flow 11: After Landing & Taxi.
- Complete Flow 12: Parking & Shutdown, including optional cold-and-dark
  secure.

Earlier failed tests remain documented below as development history. Where an
older note conflicts with this summary, this current status is authoritative.

## 2026-06-23 - Reliability and recovery upgrade

The application now persists the active procedure ID, exact step index, and
completed-flow IDs in `%LOCALAPPDATA%\MSFS2024_AI\session.xml`. Once valid
A320 and native state are available after restart, the saved procedure resumes
from that step and re-evaluates current aircraft state.

Transient engine-start and takeoff milestones have explicit recovery
conditions so starting or restoring a flow after the event does not leave it
waiting for an event that cannot occur again.

The dashboard now shows telemetry relevant to the current step, including
altitude, AGL, airspeed, vertical speed, flap handle/surface state, gear state,
minimums, and active trigger thresholds.

Flap verification now cross-checks handle detent against left and right
trailing-edge flap positions. Contradictory or impossible telemetry is shown as
`READBACK INCONSISTENT` and cannot satisfy an automatic flap step.

The dashboard displays the application version and checks the public GitHub
repository for a newer published release.

Five automated tests cover procedure recovery and recorded-state sanity cases.
They pass under .NET Framework 4.7.2 without a running simulator.

## 2026-06-21 - Transponder mode selector

Behavior Viewer exposed explicit `AIRLINER_TCAS_MODE_State1`, `State2`, and
`State3` bindings, corresponding to transponder mode positions STBY=`0`,
AUTO=`1`, and ON=`2` in `INI_TCAS_STBY_STATE`.

- Position 0 to position 1 passed with native readback.
- Position 1 to position 0 passed with native readback.

Flow 3 uses AUTO. The separate ATC SYS 1/2 switch toggles
`INI_TCAS_ATC_STATE` and does not select STBY/AUTO/ON.

The Flow 3 AUTO command was subsequently run again and passed native readback,
leaving the selector in AUTO.

## 2026-06-21 - A320 door-state discovery

Read-only Microsoft `EXIT OPEN:index`, `EXIT TYPE:index`, and exit-position
SimVars identified eight configured A320 exits:

- Main cabin: 1, 2, 3
- Cargo: 6, 7
- Emergency: 4, 5, 8

Flow 3 now independently requires every configured main and cargo exit to
report no more than 0.5% open. Nonexistent indices are excluded through their
zero exit-position data.

## 2026-06-21 - Fuel-pump sequencing refinement

The verified six-pump Mouserect commands remain unchanged. Group ON/OFF
actions now press L1, L2, C1, C2, R1, and R2 sequentially at one-second intervals
instead of transmitting all six commands in one burst. Pumps already in the
requested state are skipped. Final completion still requires independent
readback from all six pump-on LVars.

## 2026-06-20 - Signs and emergency-exit selectors

Result: **Passed**

- Seatbelt selector AUTO=`1` and OFF=`2` verified against
  `INI_SEATBELTS_SWITCH`.
- No-smoking selector AUTO=`1` and OFF=`2` verified against
  `INI_NO_SMOKING_SWITCH`.
- Emergency-exit selector ARM=`1` and OFF=`2` verified against
  `INI_EMER_EXIT_SWITCH`.

The gameplay-authoritative Flow 2 configuration is seatbelts AUTO,
no smoking AUTO, and emergency exit lights ARM.

## 2026-06-20 - APU and engine fire tests

Environment:

- Microsoft Flight Simulator 2024 SimConnect 12.2
- iniBuilds A320neo V2
- Aircraft stationary with BAT 1/2 and external power ON

Result: **Passed**

Behavior Viewer showed that the APU test uses Mouserect Lock/Unlock semantics:
Lock writes `INI_APU_FIRE_TEST=1`, and Unlock writes `0`. The corresponding
engine test variables follow the same aircraft pattern.

Each test:

1. Writes its test state ON once.
2. Verifies an independent fire light or sound response.
3. Remains held for five seconds.
4. Writes its test state OFF once.
5. Verifies the independent response clears.

APU, Engine 1, and Engine 2 all passed the complete cycle independently.

After later testing showed the first fire test could begin while the aircraft
display self-tests were still running, Flow 1 gained a 45-second continuous
electrical-power stabilization gate before the APU fire test. The aircraft's
display tests normally take approximately 40 seconds, leaving a five-second
safety margin. This revised timing awaits live verification.

Separate automatic cockpit actions now also observe a one-second minimum
cadence after the preceding automatic action completes. The delay is internal
and is not written to the activity log. This pacing change awaits live
verification.

## 2026-06-20 - STROBE selector

Environment:

- Microsoft Flight Simulator 2024 SimConnect 12.2
- iniBuilds A320neo V2
- Aircraft stationary at the gate

Result: **Passed**

The `AIRLINER_LT_STROBE` FLOAT64 Input Event uses the documented selector
positions ON=`0`, AUTO=`1`, and OFF=`2`. Independent readback is
`INI_STROBE_LIGHT_SWITCH`.

- OFF to AUTO: native readback changed from `2` to `1`.
- AUTO to OFF: native readback changed from `1` to `2`.
- Flow 1 now selects AUTO automatically.

## 2026-06-20 - ADIRS selector automation

Environment:

- Microsoft Flight Simulator 2024 SimConnect 12.2
- iniBuilds A320neo V2
- Aircraft stationary, engines off, BAT 1/2 and external power ON

Result: **Passed**

The three `AIRLINER_ADIRS_n` FLOAT64 Input Events use `0` for OFF and `1` for
NAV. ADIRS 1 was tested NAV and OFF independently. The complete Power Up flow
then selected ADIRS 1, 2, and 3 to NAV in sequence.

| Selector | Command | Independent selector readback |
|---|---|---|
| ADIRS 1 | `SetInputEvent(AIRLINER_ADIRS_1, 1)` | `INI_IRS1_STATE == 1` |
| ADIRS 2 | `SetInputEvent(AIRLINER_ADIRS_2, 1)` | `INI_IRS2_STATE == 1` |
| ADIRS 3 | `SetInputEvent(AIRLINER_ADIRS_3, 1)` | `INI_IRS3_STATE == 1` |

After ADIRS 1 and ADIRS 2, the flow waited approximately five seconds for
`INI_IRS_ON_BATTERY` to extinguish before selecting the next unit.

Final native state:

- ADIRS selectors 1/2/3: `1/1/1`
- ADIRS ON BAT: OFF
- Flow advanced to Crew Oxygen

## 2026-06-19 - Cockpit Preparation

Environment:

- Microsoft Flight Simulator 2024 SimConnect 12.2
- iniBuilds A320neo V2
- Aircraft stationary on the ground with parking brake set and engines off

Result: **Passed**

| Step | Command evidence | Independent readback |
|---|---|---|
| BAT 1 ON | Documented `Battery_1_On` preset executed | Native BAT 1 state changed to ON |
| BAT 2 ON | Documented `Battery_2_On` preset executed | Native BAT 2 state changed to ON |
| External power ON | `SET_EXTERNAL_POWER` issued | `EXTERNAL POWER ON:1` reported ON |
| NAV & LOGO position 2 | Already in the requested position | `INI_LOGO_LIGHT_SWITCH` reported native value 0 |

Final observed state:

- BAT 1: ON
- BAT 2: ON
- External power available/on: YES/ON
- NAV & LOGO selector: position 2
- Beacon: OFF
- Engines: OFF/OFF
- Parking brake: SET

The generic APU telemetry still reported master ON and 100% RPM while the APU
generator switch, generator active state, and voltage were OFF/OFF/0 V. This
test does not promote any APU telemetry or control to verified status.

## 2026-06-19 - Before Start passive Input Event monitoring

The user manually operated APU, fuel-pump, ADIRS, and seat-belt controls while
the probe subscribed to all 12 candidate Input Events.

Observed transitions:

| Control | Initial value | Observed value | Result |
|---|---:|---:|---|
| ADIRS 1 | 0 | 1 | Subscription notification captured |
| ADIRS 2 | 0 | 1 | Subscription notification captured |
| ADIRS 3 | 0 | 1 | Subscription notification captured |
| Seat-belt selector | 2 | 1 | Subscription notification captured |

No subscription notifications were received for APU master/start/bleed/
generator or the four fuel pumps. A read-only snapshot after the manual test
still reported those eight Input Events as 0. This does not prove that the
cockpit controls failed to move; it proves that subscription alone is not a
reliable observation method for those controls.

The probe was subsequently enhanced with 250 ms read-only polling as a
fallback. APU and fuel-pump controls require a short repeat capture.

### Polling repeat

The user repeated the APU and fuel-pump test with 250 ms polling enabled.
Polling again reported all APU and fuel-pump Input Events as 0 throughout.

An independent aircraft-state snapshot immediately afterward reported:

- APU master switch: ON
- APU RPM: 100%
- APU generator switch: ON
- APU generator active: ON
- APU volts: 0 V
- Four native fuel-pump values: 0/0/0/0

Conclusion: the APU Input Events behave as command endpoints and cannot be
used as state readback. The generic APU values provide partial corroboration,
but the contradictory 0 V indication means the APU subsystem remains
unverified for automatic control. Fuel-pump state was not proven ON by this
test; Behavior Viewer/native-variable evidence is still required.

## 2026-06-19 - APU MASTER PUSH command test

Behavior Viewer exposed `AIRLINER_APU_MASTER_PUSH`, and native state was
monitored through `INI_APU_MASTER_SWITCH`.

With the aircraft stationary, engines off, batteries on, and external power
connected, the parameterless calculator command
`(>B:AIRLINER_APU_MASTER_PUSH)` was sent once. The native master state remained
ON for the full eight-second verification window.

Result: **Failed safely; no aircraft state changed.**

The binding name alone is insufficient. Its expanded Behavior Viewer details
are required before another command test.

## 2026-06-20 - Native APU command/state strategy

The control path was refocused from Input Events to iniBuilds native
command/state LVar pairs.

APU master test:

1. Cockpit Preparation established BAT 1/2 ON and external power ON.
2. `INI_APU_MASTER_SWITCH_CMD` was pulsed from 1 to 0.
3. `INI_APU_MASTER_SWITCH` changed to ON within approximately one second.
4. The command was repeated from the ON state.
5. `INI_APU_MASTER_SWITCH` changed to OFF within approximately one second.

Result: **Passed in both directions.**

Writing APU state LVars directly was separately shown to change indications
without establishing `INI_APU_AVAILABLE`. Direct state writes are therefore
not a valid command path.

The user subsequently observed the command-LVar pulse approach operating the
aircraft as intended. The permanent implementation rules are documented in
`docs/NATIVE_CONTROL_STRATEGY.md`.

## 2026-06-20 - APU timing and six-pump model correction

- APU START selected successfully.
- `INI_APU_AVAILABLE` became true after the earlier 60-second command timeout.
- START selection and APU AVAIL are now separate procedure conditions.
- Fuel-pump coverage was corrected from four to all six panel pushbuttons:
  L1, L2, center 1, center 2, R1, and R2.
- A versioned MobiFlight runtime schema removed stale offset reuse without
  requiring an MSFS restart.

All six native pump states then read coherently as `0/0/0/0/0/0`. The known
Input Event and `__...IsPressed` command attempts still did not actuate them,
so the automatic fuel-pump step now fails safely instead of claiming support.

## 2026-06-20 - Six fuel pumps live verification

Behavior Viewer revealed that the real cockpit Mouserect toggles both the
pump selector LVar and its `IsPressed` animation LVar. The Input Event `Set`
code only writes `_ButtonAnimVar`, explaining why earlier Input Event tests
did not operate the pumps.

The exact Mouserect pattern was implemented for all six buttons and tested:

| Test | Native result |
|---|---|
| Six pumps OFF | `0/0/0/0/0/0`; verified |
| Six pumps ON | `1/1/1/1/1/1`; verified |

Result: **Passed in both directions.** Fuel-pump automation is now eligible
for Before Start.

## 2026-06-20 - APU bleed live verification

Behavior Viewer showed that the Mouserect toggles both
`INI_APU_BLEED_BUTTON` and `__APU_BLEEDIsPressed`.

| Test | Native result |
|---|---|
| APU bleed ON | `INI_APU_BLEED_BUTTON = 1`; verified |
| APU bleed OFF | `INI_APU_BLEED_BUTTON = 0`; verified |
| Restore ON | Verified |

Result: **Passed in both directions.**
## ATC communication

ATC communication is a pilot task because the MSFS communication menu is
dynamic and the simulator already automates the exchange. Flow 3 passively
monitors `ATC CLEARED IFR` to recognize completed IFR clearance. The pilot
confirms the pushback and engine-start clearance step after completing it
through the MSFS ATC interface.

## Flow 4 engine-start monitoring — live verified

Flow 4 keeps the engine mode selector and both engine masters as Captain
actions. The first officer now monitors each start from official simulator
telemetry and advances through `Starter Valve Open`, `Fuel Flow`, and
`Stabilized` callouts. Stabilized currently requires combustion, starter
disengaged, corrected N1 at or above 15 percent, and positive fuel flow.

Result: **Passed.** Engine-start monitoring and its spoken callouts were
verified in the complete Flow 4 test.

## Flow 5 taxi monitoring — live verified

Taxi commencement is monitored by the first officer and never requires an app
confirmation. The step completes when the aircraft is on the ground, the
parking brake is released, and groundspeed exceeds 0.5 knots.

Flow 5 now automatically performs the first-officer after-start actions before
monitoring taxi: APU bleed OFF, APU master OFF, ground spoilers ARMED, takeoff
flaps CONFIG 1, and autobrake MAX. Autobrake uses the native iniBuilds command
and readback. Flaps use the aircraft's `HANDLING_Flaps_Inc` B-event with
flap-handle-index readback.

The first spoiler test failed because writing `INI_SPOILERS_ARMED` did not
operate the lever. The generic `SPOILERS_ARM_SET` event also failed. Behavior
Viewer showed that this aircraft uses the function events
`INI.SPOILERS_SET` and `INI.SPOILERS_ARM_ON`, followed by
`AIRLINER_SPEEDBRAKE_Set`. Flow 5 now reproduces that exact path and accepts
either native `INI_SPOILERS_ARMED` or generic `SPOILERS ARMED` readback. The
first successful lever movement did not update the generic SimVar.

The generic `FLAPS_SET` event failed to operate the iniBuilds lever. Behavior
Viewer showed that one lever detent uses
`16384 / FLAPS NUM HANDLE POSITIONS (>B:HANDLING_Flaps_Inc)`. Flow 5 now uses
that exact aircraft path for CONFIG 1.

Result: **Passed.** The complete Flow 5 sequence, including spoiler arming,
flaps CONFIG 1, autobrake MAX, WXR/PWS position 1, and taxi monitoring, was
verified.

## Flow 6 before takeoff — live verified

Flow 6 now automatically sets the already live-verified strobe selector to ON.
TCAS TA/RA now uses the exact `AIRLINER_TCAS_STBY_TARA` B-event. The original
`INI_TCAS_MODE` readback was disproven when it reported 2 while the cockpit
selector visibly remained at STBY. Readback now uses the separate
`INI_TCAS_MODE_PEDESTAL` variable. Landing-light and nose-light operation uses
the exact aircraft B-events and native selector readback. Engine anti-ice remains an
as-required first-officer decision because the operational condition depends
on temperature and visible moisture. Captain runway-entry lights and briefing
remain Captain tasks; cabin readiness remains an operational confirmation.

During the first Flow 6 test, the incorrect `INI_TCAS_MODE` signal caused TCAS
to be skipped even though the cockpit selector was at STBY. That signal is no
longer used for this selector. Automatic steps that are genuinely already
configured are still explicitly reported.

The lower-left OFF/ON selector is TCAS altitude reporting. Flow 6 now sets it
ON using its exact Behavior Viewer toggle and verifies `INI_TCAS_ALT_STATE = 0`
before selecting traffic mode TA/RA.

The first altitude-reporting test was blocked before command transmission
because the client-data receiver range had not been extended for the newly
registered telemetry item. The receiver now includes that item.

Live cockpit comparison then showed the native polarity is inverted:
`INI_TCAS_ALT_STATE = 1` is OFF and `0` is ON. The normalized readback now
uses that polarity.

Result: **Passed.** The complete Flow 6 configuration was verified, including
altitude reporting ON, TCAS TA/RA, strobes, nose T.O. light, landing lights,
and the prepare-for-takeoff voice callout.

## Flow 7 takeoff and climb — live verified

Flow 7 contains no app-confirmation steps. It automatically monitors and
reports Thrust Set, Takeoff Roll, 100 Knots, Rotate and Positive Climb. Rotate
uses the configured VR speed, while actual liftoff and Positive Climb remain
dynamically detected before gear-up actions can begin. Landing gear is
commanded UP after Positive Climb. After the thrust-reduction point at 1,500
feet AGL, flaps are commanded CLEAN using `HANDLING_Flaps_Dec`. AP1 and climb
milestones are monitored without requiring the Captain to interact with the
app.

The dashboard now has separate persisted preflight `V1` and `VR` speed fields
because no verified MCDU takeoff-speed readback has been identified. Flow 7
calls `V1` at the configured V1 IAS and `Rotate` at the configured VR IAS.
Observed milestones now emit an explicit completion/callout message when
crossed instead of only showing that the app is waiting for them.

The first landing-gear test failed because the generic `GEAR_UP` event did not
operate the iniBuilds lever. Behavior Viewer showed the exact airborne path:
`LANDING_GEAR_Gear_Inc` followed by the `INI.GEAR_UP` function event. Flow 7
now uses that path and retains gear-handle-position verification.

Live testing confirmed that exact command moved the lever, but generic
`GEAR HANDLE POSITION` did not follow it. Verification now uses native
`INI_GEAR_HANDLE_STATUS_ANIMATION` with the generic signal only as a fallback.

Ground spoilers are now automatically disarmed immediately after Positive
Climb using the exact inverse Behavior Viewer path:
`INI.SPOILERS_SET`, `INI.SPOILERS_ARM_OFF`, then
`AIRLINER_SPEEDBRAKE_Set`.

The first disarm test physically succeeded and native
`INI_SPOILERS_ARMED` changed to 0, but normalized state incorrectly latched
the previous true value. Native spoiler state is now authoritative in both
directions. That false failure had stopped Flow 7 before gear and flap actions;
the preceding test log confirmed both those actions and verifications worked.

An earlier implementation attempted to use the persisted transition-altitude
field to trigger automatic STD selection through the Captain and First Officer
QNH push Input Events.

The Behavior Viewer `_Push_Mode` alias and command-LVar approaches did not
operate the QNH mode in live tests. Input Event enumeration exposed the actual
external interfaces and hashes now used by Flow 7.

Those direct push Input Events also failed to change mode in a live test. The
MSFS SDK cockpit template identifies indexed `BAROMETRIC_STD_PRESSURE` as the
authoritative state operation, but that path also failed on this aircraft.

After repeated live failures through the Behavior Viewer alias, command LVar,
direct Input Event, and indexed SDK K-event paths, altimeter STD has been
removed from Flow 7 and its checklist verification. The pilot manages STD
manually; the app neither operates nor monitors it.

Flow 5 now includes WXR/PWS as a First Officer after-start action. A dedicated
InputEvent capture showed `AIRLINER_WER_SWITCH_PWS` uses physical OFF=1,
mode 1=0, and mode 2=2. The flow therefore selects physical mode 1 with
`SetInputEvent(14794713865952973521, 0)` and verifies `INI_WX_SYS_SWITCH = 0`.

Result: **Passed.** The complete Flow 7 takeoff/climb sequence and callouts,
including gear UP, spoiler disarm, flap cleanup, nose light OFF, and landing
light retraction above 10,000 feet, were verified.

## Flow 8 cruise — live verified

Flow 8 has no confirmation step. Once cruise is established above 10,000 feet
AGL with vertical speed within 300 feet per minute, the First Officer sets
seatbelts OFF using the already live-verified selector control. Cruise
monitoring remains active after the flow completes. It selects seatbelts ON
when measured G-load leaves the 0.85–1.15 range, and returns them OFF only
after five continuous minutes of smooth flight with vertical speed within
500 feet per minute.

Result: **Passed.**

## Flow selector UI

The flow list is updated in place during telemetry refreshes. The user's
selected flow and scroll position are preserved; active/recommended flow
markers no longer force selection back to another item or snap the list to
the top.

## Voice callouts — live verified except prepare for landing

The dashboard now has a persisted `Voice callouts` option using Windows
offline speech synthesis. Flow 7 speaks `Thrust set`, `One hundred knots`,
`V one`, `Rotate`, and `Positive climb` when the corresponding monitored
milestones complete. Text messages remain in the activity log.

Flow 7 also speaks `Landing gear up` after the independent gear-handle
readback confirms UP. Flow 10 speaks `Landing gear down` after the readback
confirms DOWN.

Flow 4 also speaks each engine's `On`, `Starter valve open`, `Fuel flow`, and
`Stabilized` milestones. Flow 6 automatically calls `Cabin crew, prepare for
takeoff` at the former cabin-confirmation step.

All listed voice callouts have been live verified except Flow 10's newly added
`Cabin crew, prepare for landing`.

## Nose and landing light sequence — live verified

- Flow 5: First Officer sets nose light TAXI.
- Flow 6: First Officer sets nose light T.O. and both landing lights ON.
- Flow 7: First Officer sets nose light OFF immediately after gear UP.
- Flow 7: First Officer sets both landing-light selectors to RETRACT after passing
  10,000 feet indicated altitude.

All commands use the exact aircraft B-events and native selector readbacks.
The complete light sequence passed live testing.

## Flow 9 and Flow 10 structure — live verified

Flow 9 is Captain-only and covers flight-computer descent preparation:
arrival/approach entry, PERF APPR destination data, and descent-profile review.
Landing autobrake was removed from Flow 9. Flow 10 begins with an automatic
First Officer selection of autobrake LOW using native
`INI_AUTOBRAKE_LEVEL = 1` readback.

Result: **Passed.** Flow 9's Captain-only descent preparation and its handoff
to Flow 10 were verified.

## Flow 10 approach and landing — live verified

Flow 10 contains no app-confirmation steps. It monitors descent and
automatically performs the First Officer workload using gated aircraft state:

- Flow 10 may be started during descent or after the aircraft is already below
  10,000 feet; a level segment below 10,000 feet no longer blocks entry.
- On passing or starting below 10,000 feet, the First Officer calls
  `Cabin crew, prepare for landing`.
- Below 10,000 feet: seatbelts ON, landing lights ON, nose light TAXI.
- Below 10,000 feet indicated and at or below 230 knots: flaps CONFIG 1.
- At or below 2,000 feet AGL and 210 knots: gear DOWN, spoilers ARMED, flaps 2.
- At or below 1,200 feet AGL and 185 knots: flaps 3 then FULL.

Approaching Minimums and Minimums use simulator decision-height and radio-height
data. Touchdown, Spoilers, Reverse Green and Decel are monitored callouts with
optional voice output. Destination QNH remains pilot-managed and unmonitored,
consistent with the removal of automatic altimeter handling.

Sign-selector validation permits operation in flight. The previous shared
native-action guard incorrectly required the aircraft to be stationary on the
ground, which blocked Flow 10 from selecting the seatbelt signs ON.

Flow 7 flap cleanup no longer requires an arbitrary minimum of 180 knots.
The flow already waits until the thrust-reduction point at 1,500 feet AGL, so
the shared flap-clean action now only blocks airborne retraction below 400 feet
AGL. This prevents normal takeoff cleanup from stalling at lower climb speeds.

The first live test established that selector values follow physical
top-to-bottom order: nose T.O./TAXI/OFF = 0/1/2 and landing
ON/OFF/RETRACT = 0/1/2. The original labels were reversed and are now
corrected.

Result: **Passed.** The complete Flow 10 sequence has been live verified,
including late-start handling below 10,000 feet. Only the newly added
`Cabin crew, prepare for landing` voice callout remains untested.

During a later Flow 10 test, all four flap steps were incorrectly skipped as
`Already set`. The completion conditions used permissive `>=` comparisons, so
an unexpected higher handle-index value satisfied every detent. CONFIG 1, 2,
3, and FULL now require exact handle-index readback values 1, 2, 3, and 4.
Flap completion now uses the exact physical handle detent. Surface-position
sanity remains visible as a warning but no longer blocks the sequence while
the surfaces are moving. CONFIG 1 begins below 10,000 feet indicated at
230 knots or less, rather than waiting until 5,000 feet AGL. This correction
awaits live verification.

Flow 10 now starts Flow 11 automatically after the landing callouts complete.

## Flow 11 after landing and taxi — pending live verification

Flow 11 no longer asks the flying Captain to confirm actions in the app.
It monitors that reverse thrust is stowed at or below 70 knots, then uses
30 knots groundspeed as the automatic runway-exit/taxi-speed gate.

After that gate the First Officer automatically:

- sets both landing-light selectors to OFF;
- sets strobes OFF and the nose light to TAXI;
- disarms the ground spoilers;
- retracts the flaps fully using the documented `HANDLING_Flaps_Set` binding;
- starts the APU and waits for APU AVAIL;
- returns the transponder mode selector to STBY.

The WXR/PWS selector remains at the required position 1. The unsupported
Captain and First Officer radar display selectors are not operated. Engine
anti-ice is not yet included because no aircraft-specific command and
independent readback have been captured.

## Flow 12 parking and shutdown — pending live verification

Flow 12 monitors the Captain parking, setting the parking brake and shutting
down both engines. No app confirmation is required for those cockpit actions.
Once both engines are off, the First Officer automatically turns the nose
light and beacon OFF, turns all fuel pumps OFF, turns the seatbelt signs OFF
and turns APU BLEED ON. Door telemetry then waits until a configured required
cabin or cargo door is open.

The final cold-and-dark section retains one deliberate confirmation to avoid
unexpectedly removing all aircraft power during a normal turnaround. After
confirmation it automatically turns crew oxygen, emergency lights, NAV/LOGO
lights and all ADIRS selectors OFF; turns APU BLEED OFF; disconnects external
power while native APU power is still available; turns APU MASTER OFF; waits
for the APU flap to close; then turns BAT 1 and BAT 2 OFF.

# Application Flow Checklist

This document describes the detailed Airbus gate-to-gate flow implemented by
the application. Airbus procedures live in
`src/Copilot/Procedures/A320ProcedureLibrary.cs`; Boeing 737 procedures live in
`src/Copilot/Procedures/B737ProcedureLibrary.cs`. The application code is
authoritative. Update this file whenever a flow changes.

The PMDG 737-800 also has twelve gate-to-gate flows, but uses its own Boeing
procedures, terminology, switch commands, PMDG NG3 SDK readbacks, approach
schedule, and checklist library. It does not inherit these Airbus steps. Its
released behavior and live-validation status are documented in
`docs/PMDG_737_SUPPORT_PLAN.md`.

An interrupted active flow is saved locally and restored at its exact step
after restart. During restoration, transient milestones that have already
passed are recovered from current aircraft state rather than awaited again.

Step types:

- **Monitor** — the app waits for aircraft state; no app confirmation.
- **Captain** — the Captain performs the action. The app either detects it or
  requests confirmation when no reliable readback exists.
- **F/O automatic** — the app operates the control and verifies independent
  aircraft readback.
- **F/O confirmation** — the First Officer/pilot completes or checks the item
  and confirms it in the app.
- **Callout** — logged and spoken when voice callouts are enabled.

The normal activity log intentionally stays concise for regular players. When
an action fails verification, the app quietly records a detailed diagnostic
snapshot under `%LOCALAPPDATA%\MSFS2024_AI\diagnostics`. Use **Export
diagnostics** or **Copy last diagnostic** when reporting a test issue.

## 1. Power Up & Initial Setup

1. **Monitor:** Supported Airbus aircraft loaded: iniBuilds A320neo V2,
   iniBuilds A321LR, iniBuilds A330, or FlyByWire A32NX.
2. **Monitor:** Aircraft stationary on the ground at 0.5 knots or less.
3. **Monitor:** Both engines off.
4. **Captain:** Turn BAT 1 and BAT 2 ON; detected from battery state.
5. **Captain:** When EXT PWR is available, turn external power ON; detected
   from external-power state.
6. **F/O automatic:** Set ADIRS 1 to NAV.
7. **Monitor:** Wait for ADIRS ON BAT to extinguish.
8. **F/O automatic:** Set ADIRS 2 to NAV.
9. **Monitor:** Wait for ADIRS ON BAT to extinguish.
10. **F/O automatic:** Set ADIRS 3 to NAV.
11. **F/O automatic:** Turn crew oxygen ON.
12. **F/O automatic:** Set NAV & LOGO lights ON at selector position 2.
13. **F/O automatic:** Set strobes to AUTO.
14. **Monitor:** Wait until BAT 1, BAT 2, and external power have remained
    continuously established for 45 seconds so the approximately 40-second
    cockpit display and warning-system tests can finish.
15. **F/O automatic:** Hold and verify the APU fire test.
16. **F/O automatic:** Hold and verify the Engine 1 fire test.
17. **F/O automatic:** Hold and verify the Engine 2 fire test.

## 2. Flight Computer & Pre-Flight

1. **Captain confirmation:** Flight Director ON and local QNH set.
2. **Captain confirmation:** PFD and ND checked.
3. **Captain:** Parking brake ON with pressure indicated; brake state is
   monitored.
4. **Captain confirmation:** MCDU DATA page checked.
5. **Captain confirmation:** MCDU INIT A complete with FROM/TO, flight number,
   cost index, and cruise altitude.
6. **Captain confirmation:** MCDU flight plan complete with SID, route, and
   STAR.
7. **Captain confirmation:** MCDU RAD NAV checked.
8. **Captain confirmation:** MCDU INIT B complete with zero fuel weight and
   block fuel.
9. **Captain confirmation:** MCDU PERF complete with V1, VR, V2, transition
   altitude, and takeoff flap setting.
10. **Pilot/F/O ATC:** After MCDU programming is complete, press **Confirm
    now** to reaffirm that the SayIntentions First Officer owns communications
    and instruct that First Officer to obtain IFR clearance. SayIntentions handles conversation timing,
    and radio tuning. Without SayIntentions, use built-in MSFS ATC.
11. **F/O automatic:** Turn all six fuel pumps ON, pressing them sequentially
    at one-second intervals.
12. **F/O automatic:** Set seatbelt signs to AUTO.
13. **F/O automatic:** Set no-smoking signs to AUTO.
14. **F/O automatic:** Set emergency exit lights to ARM.

For the PMDG 737-800, the equivalent IFR-clearance step follows completion of
the FMC TAKEOFF REF setup in this same flow.

## 3. APU Start & Pushback

1. **Monitor:** Aircraft stationary on the ground with parking brake set.
2. **Captain:** Press APU MASTER ON; detected from native state.
3. **Captain:** Wait at least three seconds, then press APU START; detected
   from native state.
4. **Monitor:** Wait for APU AVAIL.
5. **Captain:** Turn APU BLEED ON; detected from native state.
6. **Captain:** Disconnect external power; detected from aircraft state.
7. **Captain:** Turn beacon ON; detected from aircraft state.
8. **Pilot/F/O ATC:** Press **Confirm now** to reaffirm the SayIntentions
   Copilot handoff and delegate pushback/start clearance. SayIntentions handles the ATC exchange and
   radio tuning. Without SayIntentions, use built-in MSFS ATC.
9. **F/O automatic:** Set transponder mode to AUTO.
10. **Monitor:** Wait until all configured cabin and cargo doors are closed.

## 4. Engine Start Sequence

1. **Monitor:** Aircraft on the ground with beacon ON.
2. **Captain monitored:** Set engine mode selector to IGN/START; the app
   continues automatically when the selector readback reaches IGN/START.
3. **Captain:** Set Engine 2 Master ON; starter/running state is monitored.
4. **F/O callout:** “Engine two starter valve open.”
5. **F/O callout:** “Engine two fuel flow.”
6. **F/O callout:** “Engine two stabilized.”
7. **Captain:** Once Engine 2 is stable, set Engine 1 Master ON;
   starter/running state is monitored.
8. **F/O callout:** “Engine one starter valve open.”
9. **F/O callout:** “Engine one fuel flow.”
10. **F/O callout:** “Engine one stabilized.”
11. **Captain confirmation:** Return engine mode selector to NORM.

The app also speaks “Engine two on” and “Engine one on” when their respective
Master ON steps complete.

## 5. After Start & Taxi

1. **Monitor:** Both engines running.
2. **F/O automatic:** Turn APU BLEED OFF.
3. **F/O automatic:** Turn APU MASTER OFF.
4. **F/O automatic:** Arm ground spoilers.
5. **F/O automatic:** Set takeoff flaps to CONFIG 1.
6. **F/O automatic:** Set autobrake MAX.
7. **F/O automatic:** Set WXR/PWS selector to position 1.
8. **F/O automatic:** Set nose light to TAXI.
9. **F/O confirmation:** Check ECAM for remaining memos or system warnings.
10. **F/O ATC:** Press **Confirm now** to reaffirm the SayIntentions Copilot
    handoff and delegate taxi clearance. SayIntentions manages taxi ATC, the correct radio frequency, and
    the subsequent exchange. Without an active SayIntentions flight,
    this step is bypassed automatically so MSFS built-in ATC can continue
    handling taxi and takeoff requests without extra app confirmations.
11. **Monitor:** Detect Captain commencing taxi when the parking brake is
    released and groundspeed exceeds 0.5 knots.

## 6. Before Takeoff

1. **F/O automatic:** When cleared to line up, set runway turnoff lights ON.
   The cockpit state is verified automatically; no confirmation is required.
2. **Captain confirmation:** Takeoff briefing complete.
3. **F/O callout:** “Cabin crew, prepare for takeoff.”
4. **F/O automatic:** Set TCAS altitude reporting ON.
5. **F/O automatic:** Set TCAS traffic mode to TA/RA.
6. **F/O confirmation:** Configure engine anti-ice as required.
7. **F/O automatic:** Set nose light to T.O. Strobes remain in AUTO from
   Flow 1; the aircraft manages takeoff strobe behavior.
8. **F/O automatic:** Set both landing lights ON.
9. **F/O ATC:** With an active SayIntentions flight and while holding short,
   press **Confirm now** to reaffirm the Copilot handoff and delegate the
   takeoff-ready call. SayIntentions decides
   when to report ready for departure and manages Tower tuning and the
   continuing exchange. Without SayIntentions, this step is
   bypassed automatically and MSFS built-in ATC continues normally.

By default, completing Flow 6 automatically starts Flow 7. This keeps the
before-takeoff and takeoff phases hands-off while the aircraft is entering the
runway and beginning the takeoff roll.

## 7. Takeoff & Climb

This flow requires no app confirmations during takeoff.

1. **Monitor/callout:** “Thrust set” when the aircraft is on the ground,
   indicated airspeed exceeds 20 knots, and both engines reach at least 80%
   N1.
2. **Monitor:** Takeoff roll once indicated airspeed exceeds 40 knots on the
   ground.
3. **Monitor/callout:** “One hundred knots” at 100 knots while on the ground.
4. **Monitor/callout:** “V one” at the configured V1 while on the ground.
5. **Monitor/callout:** “Rotate” at the configured VR.
6. **Monitor/callout:** “Positive climb” only after liftoff with vertical
   speed above 100 feet per minute.
7. **F/O automatic:** Disarm ground spoilers.
8. **F/O automatic/callout:** Raise landing gear and say “Landing gear up”
   after verified UP readback.
9. **F/O automatic:** Set nose light OFF.
10. **Monitor:** AP1 step completes when AP1 is engaged or the aircraft reaches
    400 feet AGL.
11. **Monitor:** Wait until thrust-reduction altitude at 1,500 feet AGL.
12. **F/O automatic:** Retract flaps to CLEAN. Airborne retraction is blocked
    only below 400 feet AGL.
13. **Monitor:** Wait until indicated altitude reaches 10,000 feet.
14. **F/O automatic:** Set both landing-light selectors to RETRACT.

Altimeter STD selection is not operated or monitored by the app.

## 8. Cruise

1. **Monitor:** Cruise established while airborne, at least 10,000 feet AGL,
   and vertical speed within ±300 feet per minute.
2. **Monitor:** Smooth conditions with G-load between 0.85 and 1.15 and
   vertical speed within ±300 feet per minute.
3. **F/O automatic:** Turn seatbelt signs OFF.

After the flow completes, cruise monitoring remains active:

- Seatbelt signs turn ON when G-load leaves the 0.85–1.15 range.
- They return OFF only after five continuous smooth minutes with vertical
  speed within ±500 feet per minute.

**iniBuilds A321LR exception:** Both seatbelt and no-smoking selectors remain
in AUTO. The aircraft controls the cabin signs automatically; the app does not
run turbulence-driven ON/OFF selector commands for this profile.

## 9. Descent Preparation

Flow 9 is Captain-only.

1. **Captain confirmation:** Enter and verify arrival, approach, STAR, and
   transition in the MCDU.
2. **Captain confirmation:** Enter destination QNH, temperature, wind, and
   minima on PERF APPR.
3. **Captain confirmation:** Review descent profile, constraints, and landing
   configuration data.

## 10. Approach & Landing

Landing autobrake LOW is selected only after descent/below-10,000-feet
monitoring has completed; starting Flow 10 early no longer arms it
immediately.

This flow requires no app confirmations and may be started during descent or
after the aircraft is already below 10,000 feet.

1. **Monitor:** Accept an airborne descent below -300 feet per minute or an
   airborne aircraft already at or below 10,000 feet.
2. **Monitor:** Wait until indicated altitude is at or below 10,000 feet.
3. **F/O automatic:** Set landing autobrake LOW.
4. **F/O automatic:** Turn seatbelt signs ON. On the iniBuilds A321LR, verify
   the seatbelt and no-smoking selectors remain AUTO instead.
5. **F/O automatic:** Turn both landing lights ON.
6. **F/O automatic:** Set nose light to T.O. / landing position.
7. **Monitor:** Approach configuration point at or inside 15 NM to touchdown
   or below 10,000 feet indicated.
8. **F/O callout:** “Cabin crew, prepare for landing.”
9. **Monitor:** Wait until speed is safe for flaps CONFIG 1.
10. **F/O automatic:** Set flaps CONFIG 1.
11. **Monitor:** Flaps 2 point at or inside 10 NM to touchdown or below
    4,000 feet AGL.
12. **Monitor:** Wait until speed is safe for flaps CONFIG 2.
13. **F/O automatic:** Set flaps CONFIG 2.
14. **Monitor:** Gear-down point at or inside 7 NM to touchdown and at gear
    speed. If no runway/route distance is available, radio-altitude fallback
    is allowed; if distance is available, low altitude alone does not trigger
    gear extension.
15. **F/O automatic/callout:** Lower landing gear and say “Landing gear down”
    after verified DOWN readback.
16. **F/O automatic:** Arm ground spoilers.
17. **Monitor:** Landing-configuration point at or inside 5 NM to touchdown
    or below 1,800 feet AGL.
18. **Monitor:** Wait until speed is safe for landing configuration.
19. **F/O automatic:** Set flaps CONFIG 3.
20. **F/O automatic:** Set flaps FULL.
21. **F/O callout:** “Approaching minimums” at decision height plus 100 feet.
22. **F/O callout:** “Minimums” at decision height.
23. **Monitor:** Touchdown.
24. **F/O callout:** “Spoilers” after actual left and right spoiler deployment.
25. **F/O callout:** “Reverse green” only if reverse thrust engages; if no
    reverse is used, the flow continues once rollout slows below 40 knots.
26. **F/O callout:** “Decel” when autobrakes are active or groundspeed falls
    below 80 knots.
27. **Automatic handoff:** Start Flow 11 after Flow 10 completes when the
    standard Flow 10-to-11 chaining option is enabled.

Destination QNH remains pilot-managed and is not operated or monitored.
The approach gates use distance to the selected runway when MSFS exposes it,
then GPS distance as a backup. Flap gates still have altitude fallbacks, but
gear extension uses distance as the authority whenever distance is available so
a low intercept does not drop the gear before the normal approach point. Each
flap selection has its own monitored safe-speed step before the F/O moves the
lever. If the aircraft is too fast, the flow waits at the relevant speed-safe
step instead of failing the action. The included standard configuration can be
overridden in `Approach & chaining settings`. The iniBuilds A321LR uses
separate approach flap speed limits for Flaps 2/3/FULL so it does not inherit
the more conservative A320 schedule during landing tests.

## 11. After Landing & Taxi

1. **Monitor:** Aircraft on the ground.
2. **Monitor:** Reverse thrust stowed at or below 70 knots.
3. **F/O automatic:** Set autobrake OFF.
4. **Monitor:** Wait until after-landing taxi speed at or below 30 knots.
5. **F/O automatic:** Set both landing-light selectors to RETRACT.
6. **F/O automatic:** Set strobes OFF.
7. **F/O automatic:** Set nose light to TAXI.
8. **F/O automatic:** Disarm ground spoilers.
9. **F/O automatic:** Retract flaps fully to zero.
10. **F/O automatic:** Set transponder mode to STBY.
11. **F/O automatic:** Turn APU MASTER ON while taxiing after landing.
12. **Monitor:** Wait for APU intake flap to open.
13. **F/O automatic:** Select APU START.
14. **Monitor:** Wait for APU AVAIL.
15. **F/O automatic:** Turn APU BLEED ON before handing off to
    parking/shutdown.

The WXR/PWS selector remains at position 1. The unsupported Captain and F/O
radar display selectors and engine anti-ice are not operated by this flow.

## 12. Parking & Shutdown

### Normal turnaround shutdown

1. **Monitor:** Aircraft parked at the gate: stopped on the ground, parking
   brake ON, and both engines shut down.
2. **Monitor:** Parking brake ON.
3. **Monitor:** APU available or external power connected.
4. **Monitor:** Captain switches both engine masters OFF.
5. **F/O automatic:** Verify/turn APU BLEED ON.
6. **F/O automatic:** Set nose taxi light OFF.
7. **F/O automatic:** Turn beacon OFF after engine shutdown.
8. **F/O automatic:** Turn all six fuel pumps OFF.
9. **F/O automatic:** Turn seatbelt signs OFF. On the iniBuilds A321LR, keep
   both seatbelt and no-smoking selectors in AUTO during normal turnaround.

Doors and slides are treated as a real-world turnaround/ground-handling item
and do not block the cockpit shutdown flow.

### Optional cold-and-dark secure

The app requests one explicit confirmation before removing electrical power.

1. **Captain/F/O decision:** Press **Confirm now** to continue to final
   cold-and-dark secure. For a follow-up flight, press **Cancel** to stop Flow
   12 here and keep the aircraft in turnaround configuration on APU or
   external power.
2. **A321 F/O automatic:** Set NO SMOKING from AUTO to OFF only now, after
   passenger turnaround and final-secure authorization.
3. **Automatic:** Turn crew oxygen OFF.
4. **Automatic:** Turn emergency exit lights OFF.
5. **Automatic:** Turn NAV & LOGO lights OFF.
6. **Automatic:** Set ADIRS 1 OFF.
7. **Automatic:** Set ADIRS 2 OFF.
8. **Automatic:** Set ADIRS 3 OFF.
9. **Automatic:** Turn APU BLEED OFF.
10. **Monitor:** If external power is connected, require APU AVAIL and APU
   generator ON before disconnecting it.
11. **Automatic:** Disconnect external power.
12. **Automatic:** Turn APU MASTER OFF.
13. **Monitor:** Wait until the APU intake/exhaust flap closes.
14. **Automatic:** Turn BAT 1 OFF.
15. **Automatic:** Turn BAT 2 OFF.


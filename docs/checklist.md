# Application Flow Checklist

This document mirrors the flows currently implemented in
`src/Copilot/Procedures/A320ProcedureLibrary.cs`. The application code is
authoritative. Update this file whenever a flow changes.

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

## 1. Power Up & Initial Setup

1. **Monitor:** A320neo V2 loaded.
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
10. **F/O automatic:** Turn all six fuel pumps ON, pressing them sequentially
    at one-second intervals.
11. **F/O automatic:** Set seatbelt signs to AUTO.
12. **F/O automatic:** Set no-smoking signs to AUTO.
13. **F/O automatic:** Set emergency exit lights to ARM.

## 3. APU Start & Pushback

1. **Monitor:** Aircraft stationary on the ground with parking brake set.
2. **Captain:** Press APU MASTER ON; detected from native state.
3. **Captain:** Wait at least three seconds, then press APU START; detected
   from native state.
4. **Monitor:** Wait for APU AVAIL.
5. **Captain:** Turn APU BLEED ON; detected from native state.
6. **Captain:** Disconnect external power; detected from aircraft state.
7. **Captain:** Turn beacon ON; detected from aircraft state.
8. **Pilot:** Request and acknowledge IFR clearance through MSFS ATC. The app
   monitors IFR-clearance state.
9. **Pilot confirmation:** Request and acknowledge pushback and engine-start
   clearance through MSFS ATC, then confirm in the app.
10. **F/O automatic:** Set transponder mode to AUTO.
11. **Monitor:** Wait until all configured cabin and cargo doors are closed.

## 4. Engine Start Sequence

1. **Monitor:** Aircraft on the ground with beacon ON.
2. **Captain confirmation:** Set engine mode selector to IGN/START.
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
10. **Monitor:** Detect Captain commencing taxi when the parking brake is
    released and groundspeed exceeds 0.5 knots.

## 6. Before Takeoff

1. **Captain confirmation:** When cleared to line up, turn runway turnoff
   lights ON.
2. **Captain confirmation:** Takeoff briefing complete.
3. **F/O callout:** “Cabin crew, prepare for takeoff.”
4. **F/O automatic:** Set TCAS altitude reporting ON.
5. **F/O automatic:** Set TCAS traffic mode to TA/RA.
6. **F/O confirmation:** Configure engine anti-ice as required.
7. **F/O automatic:** Set nose light to T.O. Strobes remain in AUTO from
   Flow 1; the aircraft manages takeoff strobe behavior.
8. **F/O automatic:** Set both landing lights ON.

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

## 9. Descent Preparation

Flow 9 is Captain-only.

1. **Captain confirmation:** Enter and verify arrival, approach, STAR, and
   transition in the MCDU.
2. **Captain confirmation:** Enter destination QNH, temperature, wind, and
   minima on PERF APPR.
3. **Captain confirmation:** Review descent profile, constraints, and landing
   configuration data.

## 10. Approach & Landing

This flow requires no app confirmations and may be started during descent or
after the aircraft is already below 10,000 feet.

1. **F/O automatic:** Set landing autobrake LOW.
2. **Monitor:** Accept an airborne descent below −300 feet per minute or an
   airborne aircraft already at or below 10,000 feet.
3. **Monitor:** Wait until indicated altitude is at or below 10,000 feet.
4. **F/O automatic:** Turn seatbelt signs ON.
5. **F/O automatic:** Turn both landing lights ON.
6. **F/O automatic:** Set nose light to TAXI.
7. **Monitor:** Approach configuration point at or inside 15 NM to touchdown
   or below 10,000 feet indicated, and at or below 220 knots by default.
8. **F/O callout:** “Cabin crew, prepare for landing.”
9. **F/O automatic:** Set flaps CONFIG 1.
10. **Monitor:** Flaps 2 point at or inside 10 NM to touchdown or below
    4,000 feet AGL, and at or below 200 knots by default.
11. **F/O automatic:** Set flaps CONFIG 2.
12. **Monitor:** Gear-down point at or inside 7 NM to touchdown or below
    2,500 feet AGL, and at or below 210 knots by default.
13. **F/O automatic/callout:** Lower landing gear and say “Landing gear down”
    after verified DOWN readback.
14. **F/O automatic:** Arm ground spoilers.
15. **Monitor:** Landing-configuration point at or inside 5 NM to touchdown
    or below 1,800 feet AGL, and at or below 185 knots by default.
16. **F/O automatic:** Set flaps CONFIG 3.
17. **F/O automatic:** Set flaps FULL.
18. **F/O callout:** “Approaching minimums” at decision height plus 100 feet.
19. **F/O callout:** “Minimums” at decision height.
20. **Monitor:** Touchdown.
21. **F/O callout:** “Spoilers” after actual left and right spoiler deployment.
22. **F/O callout:** “Reverse green” only if reverse thrust engages; if no
    reverse is used, the flow continues once rollout slows below 40 knots.
23. **F/O callout:** “Decel” when autobrakes are active or groundspeed falls
    below 80 knots.
24. **Automatic handoff:** Start Flow 11 after Flow 10 completes when the
    standard Flow 10-to-11 chaining option is enabled.

Destination QNH remains pilot-managed and is not operated or monitored.
The approach gates use distance to the selected runway when MSFS exposes it,
then GPS distance as a backup, and finally altitude/speed fallback gates. The
included standard configuration can be overridden in `Approach & chaining
settings`.

## 11. After Landing & Taxi

1. **Monitor:** Aircraft on the ground.
2. **Monitor:** Reverse thrust stowed at or below 70 knots.
3. **Monitor:** Wait until after-landing taxi speed at or below 30 knots.
4. **F/O automatic:** Set both landing-light selectors to OFF.
5. **F/O automatic:** Set strobes OFF.
6. **F/O automatic:** Set nose light to TAXI.
7. **F/O automatic:** Disarm ground spoilers.
8. **F/O automatic:** Retract flaps fully to zero.
9. **F/O automatic:** Turn APU MASTER ON.
10. **Monitor:** Wait for APU intake flap to open.
11. **F/O automatic:** Select APU START.
12. **Monitor:** Wait for APU AVAIL.
13. **F/O automatic:** Set transponder mode to STBY.

The WXR/PWS selector remains at position 1. The unsupported Captain and F/O
radar display selectors and engine anti-ice are not operated by this flow.

## 12. Parking & Shutdown

### Normal turnaround shutdown

1. **Monitor:** Aircraft parked on the ground at 0.5 knots or less.
2. **Monitor:** Parking brake ON.
3. **Monitor:** APU available or external power connected.
4. **Monitor:** Captain switches both engine masters OFF.
5. **F/O automatic:** Set nose taxi light OFF.
6. **F/O automatic:** Turn beacon OFF after engine shutdown.
7. **F/O automatic:** Turn all six fuel pumps OFF.
8. **F/O automatic:** Turn seatbelt signs OFF.
9. **F/O automatic:** Turn APU BLEED ON.
10. **Monitor:** Wait until a configured required cabin or cargo door opens.

### Optional cold-and-dark secure

The app requests one explicit confirmation before removing electrical power.

1. **Captain/F/O confirmation:** Continue to final cold-and-dark secure.
2. **Automatic:** Turn crew oxygen OFF.
3. **Automatic:** Turn emergency exit lights OFF.
4. **Automatic:** Turn NAV & LOGO lights OFF.
5. **Automatic:** Set ADIRS 1 OFF.
6. **Automatic:** Set ADIRS 2 OFF.
7. **Automatic:** Set ADIRS 3 OFF.
8. **Automatic:** Turn APU BLEED OFF.
9. **Monitor:** If external power is connected, require APU AVAIL and APU
   generator ON before disconnecting it.
10. **Automatic:** Disconnect external power.
11. **Automatic:** Turn APU MASTER OFF.
12. **Monitor:** Wait until the APU intake/exhaust flap closes.
13. **Automatic:** Turn BAT 1 OFF.
14. **Automatic:** Turn BAT 2 OFF.

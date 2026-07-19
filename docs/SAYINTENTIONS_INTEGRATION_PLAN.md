# SayIntentions Integration

## Status

The SayIntentions ownership model is released in v0.9.3 and was live validated
gate-to-gate on July 19, 2026. Persistent Copilot handoff, native IFR and
pushback/start requests, automatic takeoff-clearance reuse, response
verification, voice-callout routing, and passive communication mirroring are
covered by the release regression suite.

SayIntentions is optional. No aircraft procedure, callout, or normal flow may
require its client, subscription, or API to be available.

## Ownership boundary

SayIntentions owns:

- COM frequency selection and automatic tuning;
- continuing ATC exchanges and readbacks while its Copilot has communications;
- generated First Officer and ATC audio.

MSFS 2024 Virtual First Officer owns:

- enabling and reaffirming SayIntentions Copilot communications mode;
- routing existing operational callouts through the SayIntentions voice engine;
- flow progression and passive activity-log presentation;
- clean fallback to built-in MSFS ATC when SayIntentions is unavailable.

The application must never call SayIntentions `setFreq` or select a clearance,
ground, or tower frequency. Frequency information is read-only in this app.

## Implemented functionality

- Detect the local SayIntentions client through
  `http://127.0.0.1:63287/flightJSON`.
- Read the active callsign, route, airport, gate, flight identifier, and SAPI
  context without asking the user to copy an API key.
- Show offline, ready, or connected status on the dashboard.
- Show read-only ATIS, METAR, TAF, runway, frequency, parking, and recent
  communications information in **Manage integrations**.
- Offer optional SayIntentions First Officer voices with local Windows speech
  fallback.
- Offer `Minimal`, `Standard`, and `Expanded` callout detail.
- Send exact operational callout text through `INTERCOM1_IN` with rephrasing
  disabled.
- Provide a First Officer voice test.
- Offer a persisted, default-enabled setting that permits checkpoint-scoped
  assignment of ATC communications to the SayIntentions First Officer.
- Set Copilot communications through the official SAPI `setVar` endpoint using
  `SIAI_COPILOT=1`. This does not depend on MobiFlight and therefore also works
  with the PMDG profile.
- Enable and reaffirm Copilot communications at IFR, pushback/start, taxi, and
  takeoff checkpoints. Leave SayIntentions in control of the delegated radio
  conversation so it can perform audible requests, readbacks, and tuning.
- Apply Copilot communications proactively when the active SayIntentions
  session is discovered. SAPI acceptance of `SIAI_COPILOT=1` is not a desktop
  client readback, so allow a bounded synchronization interval when a native
  action genuinely requires a new mode change.
- Keep pending clearance verification attached during temporary client/SAPI
  outages, but release it after three minutes without a verifiable exchange so
  the flow provides a retry instead of waiting indefinitely.
- Treat a recent matching clearance already present in the active flight's
  communication history as completion. This supports airports where
  SayIntentions automatically reports ready for departure on line-up and avoids
  a second takeoff request from the VFO.
- Restore `SIAI_COPILOT=0` when the user disables the setting or the app closes
  normally.
- At pushback/start, use the normal Confirm button to invoke the native SAPI
  `sendCallback` event `copilot_request` with action
  `preflight_request_push_and_start`. This is the same workflow used by the
  SayIntentions UI and produces the real Copilot radio exchange.
- At IFR clearance, use the corresponding native SAPI `sendCallback` event
  `copilot_request` with action `preflight_request_clearance_ifr`.
- Do not assume a generic `INTERCOM1` instruction triggers an ATC action. Keep
  it only for checkpoints whose native callback has not yet been captured,
  and replace those paths as their exact action identifiers are verified.
- Do not require SayIntentions `flight_destination` before requesting IFR
  clearance: SayIntentions imports SimBrief itself and populates that field as
  part of the clearance workflow.
- Let SayIntentions select and tune the appropriate station. The app does not
  transmit on `COM1` and never calls `setFreq`.
- Poll communication history and mirror new First Officer
  transmissions and ATC responses into the activity log with station and
  frequency information.
- Keep the flow waiting until the matching clearance is found. IFR requires an
  accepted readback; standby, unable, and unrelated clearances do not advance.

## Security and isolation

- API credentials stay in memory and are never saved in settings.
- Flight JSON, API keys, SAPI request URIs, and credential-bearing exception
  details are never written to user or diagnostic logs.
- A host supplied by the local payload is accepted only when it uses HTTPS and
  belongs to `sayintentions.ai`; otherwise the official primary API host is
  used.
- All integration code stays under `src/Copilot/SayIntentions` and does not
  participate in aircraft identification, command routing, checklists, or
  aircraft-native readback.
- When SayIntentions is disabled, closed, or has no active flight, its ATC
  steps bypass cleanly so built-in MSFS ATC or the pilot can continue.

## Completed live acceptance

The v0.9.3 gate-to-gate acceptance flight verified:

1. At an ATC checkpoint, the SayIntentions desktop client changes to
   **Co-Pilot** and remains responsible for the delegated conversation.
2. The app does not guess, hard-code, select, or tune frequencies.
3. Confirm invokes the verified native callback where available; the actual
   request is heard over the radio using the configured SayIntentions First
   Officer voice.
4. The flow waits for the matching response and logs both sides of the
   exchange before completing the step.
5. New First Officer and ATC radio messages appear once in the activity log.
6. Existing operational callouts still use the selected SayIntentions voice.
7. Disabling Copilot communications returns the SayIntentions client to Pilot.
8. Closing SayIntentions or running without an active SayIntentions flight
   leaves the normal built-in-ATC path unblocked.

The corresponding result is recorded in `docs/LIVE_TESTS.md`. Future changes
must preserve this ownership boundary and pass the SayIntentions regression
suite before release.

## Official interfaces

- Local flight discovery: `flightJSON`
- First Officer callouts and private Copilot instructions: SAPI `sayAs`
- Communications history: SAPI `getCommsHistory`
- Copilot handoff: SAPI `setVar`, category `L`, variable `SIAI_COPILOT`
- Operational briefing: SAPI `getWX` and `getParking`

Official documentation:
<https://p2.sayintentions.ai/p2/docs/>

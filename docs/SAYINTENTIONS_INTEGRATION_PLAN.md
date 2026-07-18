# SayIntentions Integration

## Status

The planned SayIntentions feature scope is implemented on the
`feature/sayintentions-completion` branch. Automated regression coverage is
complete; the persistent Copilot handoff and four departure conversations
still require final live acceptance in SayIntentions before release.

SayIntentions is optional. No aircraft procedure, callout, or normal flow may
require its client, subscription, or API to be available.

## Ownership boundary

SayIntentions owns:

- COM frequency selection and automatic tuning;
- continuing ATC exchanges and readbacks while its Copilot has communications;
- generated First Officer and ATC audio.

MSFS 2024 Virtual First Officer owns:

- the point in the operational flow where the pilot authorizes an initial ATC
  request;
- the initial request context and standard phraseology;
- conservative detection of the required ATC authorization;
- flow completion, timeout, activity-log presentation, and manual fallback.

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
- Offer a persisted, default-enabled setting that assigns ATC communications
  to the SayIntentions First Officer.
- Set Copilot communications through the official SAPI `setVar` endpoint using
  `SIAI_COPILOT=1`. This does not depend on MobiFlight and therefore also works
  with the PMDG profile.
- Keep Copilot mode active for the SayIntentions flight instead of releasing it
  after each transmission.
- Restore `SIAI_COPILOT=0` when the user disables the setting or the app closes
  normally.
- Use the normal flow confirmation button to authorize:
  - IFR clearance;
  - pushback and engine-start clearance;
  - taxi clearance;
  - ready-for-departure/takeoff clearance.
- Build requests from the active callsign, route, gate, and optional SimBrief
  context.
- Detect a recent matching request already initiated by SayIntentions and
  suppress a duplicate transmission.
- Ignore outgoing-message echoes and unrelated communications.
- Treat `stand by`, `unable`, `hold position`, and `line up and wait` as
  non-completing responses where applicable.
- Require step-specific authorization, including `cleared for takeoff` for the
  takeoff-clearance step.
- Keep waiting for up to 60 seconds and then expose a normal manual fallback.
- Record First Officer transmissions and ATC responses in the activity log,
  including the station and frequency reported by SayIntentions.

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

## Final live acceptance

Run one normal departure and verify:

1. The SayIntentions desktop client changes from **Pilot** to **Co-Pilot**
   after the integration detects the active flight.
2. The app does not tune COM1 and SayIntentions changes frequencies itself.
3. IFR clearance is requested audibly, the reply is logged, and the flow
   completes only after clearance is issued.
4. Pushback/start, taxi, and ready-for-departure behave the same way.
5. A `stand by` or `line up and wait` response leaves the relevant flow waiting.
6. Repeatedly pressing Confirm cannot send duplicate requests.
7. Disabling Copilot communications returns the SayIntentions client to Pilot.
8. Closing SayIntentions or running without an active SayIntentions flight
   leaves the normal built-in-ATC path unblocked.

After this matrix passes, mark the integration complete in `docs/LIVE_TESTS.md`
and proceed to GSX development on a separate branch.

## Official interfaces

- Local flight discovery: `flightJSON`
- First Officer callouts and ATC transmissions: SAPI `sayAs`
- Communications history: SAPI `getCommsHistory`
- Copilot handoff: SAPI `setVar`, category `L`, variable `SIAI_COPILOT`
- Operational briefing: SAPI `getWX` and `getParking`

Official documentation:
<https://p2.sayintentions.ai/p2/docs/>

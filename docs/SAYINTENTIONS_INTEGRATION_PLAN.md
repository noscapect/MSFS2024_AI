# SayIntentions Integration

## Goal

Add optional SayIntentions companion features without making SayIntentions a
runtime requirement and without coupling it to any aircraft adapter.

## Implemented first slice

- Detect the local SayIntentions Windows client through
  `http://127.0.0.1:63287/flightJSON`.
- Read the active callsign, route, airport, and assigned gate.
- Obtain the active SAPI context from the local payload without asking the
  user to copy an API key into this application.
- Show `OFFLINE`, `READY`, or `CONNECTED` state in the dashboard.
- Offer an opt-in `SayIntentions voices` preference for existing First Officer
  callouts.
- Send exact callout text through `INTERCOM1_IN` with rephrasing disabled.
- Fall back to the existing Windows voice if SayIntentions is unavailable or
  rejects a callout.
- Show read-only ATIS, METAR, TAF, active-runway, frequency, assigned-parking,
  and recent-communications data in `Manage SayIntentions`.
- Provide a user-triggered First Officer voice test.
- During the existing IFR-clearance and pushback/start-clearance pilot steps,
  reuse the normal `Confirm now` button as the pilot's authorization for the
  First Officer to contact SayIntentions ATC on COM1.
- Build and send the request directly from the active callsign, route, and
  gate context without opening a second dialog.
- Keep the flow waiting while monitoring for a new ATC response. Complete the
  clearance step only after a reply is detected; after a timeout, retain a
  normal manual-confirm fallback without blocking the flight.

## Security and isolation

- API credentials are held only in memory and are never saved in settings.
- The app never writes the flight JSON, API key, SAPI request URI, or SAPI
  exception details to player or diagnostic logs.
- A hostname supplied by the local payload is accepted only when it is HTTPS
  and belongs to `sayintentions.ai`; otherwise the official primary API host
  is used.
- SayIntentions code lives under `src/Copilot/SayIntentions`. It does not
  participate in aircraft identification, command routing, state readback,
  checklists, or procedures.
- All existing functionality remains available when the SayIntentions client
  is closed or no active SayIntentions flight exists.

## Next slices

1. Live-validate IFR-clearance and pushback transmissions across multiple
   airports, frequencies, and callsigns.
2. Consider automatic clearance recognition only after that validation. It
   must remain conservative and must never block a normal flight when
   SayIntentions is unavailable.

Frequency changes remain out of scope so the app cannot fight SayIntentions
or cockpit radio management. The normal flow confirmation is the explicit
authorization for each automated transmission.

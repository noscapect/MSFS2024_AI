# SimBrief integration

Status: implemented and included in v0.8.1.

SimBrief support is optional, free, and read-only. A user supplies a SimBrief
Pilot ID or username under **Manage SimBrief**. The app fetches that user's
latest generated OFP through SimBrief's public latest-OFP endpoint. It stores
no SimBrief/Navigraph password, requires no app API key or paid subscription,
and does not depend on a developer-hosted service.

## User workflow

1. Open **Manage SimBrief** and enter a Pilot ID or username.
2. Import the latest generated flight manually, or enable import when starting
   a new flight.
3. Review freshness and aircraft-mismatch warnings.
4. Activate the reviewed OFP for the current flight session.
5. Open **Review flight briefing** to inspect the operational summary.

An import failure leaves existing app settings and every cockpit flow
unchanged. **New flight / Reset progress** clears the active OFP so cached data
from an earlier sector cannot silently influence a later flight. The latest
downloaded normalized summary remains cached for review.

## Operational use

The active OFP provides:

- flight number, aircraft type and registration;
- origin, destination, alternate, and planned runways;
- route and available SID/STAR information;
- planned cruise altitude and cost index;
- block, taxi, trip, reserve, and arrival fuel/weight data when supplied;
- departure/arrival timing and weather when supplied;
- transition altitude and takeoff references when supplied;
- a normalized block-fuel comparison with live aircraft fuel;
- aircraft-family normalization of imported takeoff flap settings;
- imported metadata in the normal bounded flight telemetry recording.

Planned cruise altitude improves cruise and initial-descent phase detection.
For the PMDG 737-800, the app also compares imported V1, VR, and takeoff flaps
with live FMC TAKEOFF REF data. Missing values are shown as unavailable rather
than guessed.

SimBrief information remains dispatch/advisory input. It does not write to an
MCDU/FMC, operate QNH controls, transmit cockpit commands, or block a flow.

## Safety and privacy

- The imported OFP is explicitly reviewed before session activation.
- Stale plans and detected-aircraft mismatches produce visible warnings.
- Network errors never pause or fail a cockpit procedure.
- Only a normalized latest-flight summary is cached locally.
- The active flight is session scoped and cleared by a new-flight reset.
- Transport, parsing, validation, operational interpretation, and aircraft
  adapters remain separate code paths.
- Parser, validation, unit conversion, flap normalization, PMDG comparison,
  and procedure-session behavior have offline automated tests.

## Implementation

- `SimBriefClient`: HTTPS latest-OFP request and error handling.
- `SimBriefJsonMapper`: optional-field parsing and normalized model mapping.
- `ImportedFlightPlan`: aircraft-neutral cached/session model.
- `SimBriefImportValidator`: freshness and aircraft mismatch warnings.
- `SimBriefOperationalContext`: fuel, flap, and PMDG FMC comparisons.
- `SimBriefCacheStore`: bounded latest-summary persistence.

The fetch endpoint is:

```text
https://www.simbrief.com/api/xml.fetcher.php?userid={pilot_id}&json=1
```

## Deliberately excluded

Generating or editing an OFP inside the app is not included. That is a separate
authenticated API workflow and may require a SimBrief developer integration.
The freeware desktop app therefore imports only an already generated OFP.

Official references:

- https://developers.navigraph.com/docs/simbrief/fetching-ofp-data
- https://developers.navigraph.com/docs/simbrief/introduction
- https://developers.navigraph.com/docs/simbrief/how-it-works

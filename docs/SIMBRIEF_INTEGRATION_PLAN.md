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
- structured SID/STAR identifiers and transitions, Navigraph-formatted route,
  and navlog fixes for optional Navigation Data validation;
- planned cruise altitude and cost index;
- block, taxi, trip, reserve, and arrival fuel/weight data when supplied;
- departure/arrival timing and weather when supplied;
- transition altitude and takeoff references when supplied;
- origin/destination transition altitude and transition level when supplied;
- a normalized block-fuel comparison with live aircraft fuel;
- aircraft-family normalization of imported takeoff flap settings;
- imported metadata in the normal bounded flight telemetry recording.

Planned cruise altitude improves cruise and initial-descent phase detection.
For the PMDG 737-800, the app also compares imported V1, VR, and takeoff flaps
with live FMC TAKEOFF REF data. Missing values are shown as unavailable rather
than guessed.

For the development Asobo 737 MAX profile, imported V1/VR/V2 and takeoff flaps
currently become procedure targets because no verified MAX FMC TAKEOFF REF
readback has been implemented. Therefore the app cannot claim that SimBrief
and the MAX FMC match. A July 28 live test demonstrated this limitation when
the imported OFP supplied Flaps 5 while the pilot expected Flaps 15. The
comparison label and takeoff-configuration callout must be corrected before
the MAX profile is release eligible.

SimBrief information remains dispatch/advisory input. It does not write to an
MCDU/FMC, operate QNH controls, transmit cockpit commands, or block a flow.

## Current-cycle Navigraph value without direct integration

The project deliberately has no direct Navigraph API integration and will not
obtain or embed a Navigraph application client ID. It does not authenticate a
Navigraph account, inspect Navigraph Hub files, or download raw Navigation Data
packages.

Pilots may optionally use their own paid Navigraph subscription when generating
the SimBrief OFP. SimBrief then supplies the current-cycle dispatch data through
the same public latest-OFP endpoint already used by the app. The imported model
retains the OFP AIRAC, Navigraph-formatted route, structured SID/STAR identifiers
and transitions, origin/destination transition altitude and level, and detailed
navlog fixes. These values enrich briefing and future validation without adding
another account connection or runtime dependency.

The application must describe these values as SimBrief OFP data. It cannot claim
an independent Navigraph database validation, current subscription status, or
agreement with the simulator/aircraft navigation database. Free SimBrief OFPs
remain fully supported and may contain an older AIRAC; no flow may be blocked on
that basis.

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

Direct Navigraph Navigation Data and Charts APIs are also deliberately excluded
because they require an application client ID. Reading or redistributing data
installed by Navigraph Hub or an aircraft add-on is not a supported substitute.

Official references:

- https://developers.navigraph.com/docs/simbrief/fetching-ofp-data
- https://developers.navigraph.com/docs/simbrief/introduction
- https://developers.navigraph.com/docs/simbrief/how-it-works

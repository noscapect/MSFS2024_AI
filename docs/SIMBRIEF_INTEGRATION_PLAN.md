# SimBrief Integration Plan

Status: research complete; no runtime implementation yet.

## Recommended first release

Add optional, read-only import of the user's latest generated SimBrief OFP.
The user enters a SimBrief Pilot ID (preferred) or username. The app requests:

```text
https://www.simbrief.com/api/xml.fetcher.php?userid={pilot_id}&json=1
```

The official latest-OFP fetcher does not require the app to store a SimBrief
or Navigraph password and does not require a flight-generation API key.

The app should provide an explicit **Import latest SimBrief flight** button.
Optional automatic import may run only when the user starts a new flight. A
network failure or missing OFP must never block a checklist flow.

## Useful imported data

- OFP generation time and flight number
- aircraft ICAO type and registration
- origin, destination, alternate, and planned runways
- route, SID/STAR data when present, cruise altitude, and cost index
- block, taxi, trip, reserve, zero-fuel, takeoff, and landing weights/fuel
- passenger/cargo totals and units
- scheduled and estimated times
- departure/destination weather supplied in the OFP
- transition altitude/level when present
- V1, VR, V2, flap and thrust data only when a SimBrief Takeoff and Landing
  Report is present

SimBrief data should initially populate the app's flight summary and editable
preflight settings. It must not write directly to an aircraft MCDU or operate
QNH controls.

## Validation and safety rules

1. Treat imported data as dispatch information, not live aircraft state.
2. Show the OFP generation time and warn when it is stale.
3. Compare the OFP aircraft type with the detected aircraft profile.
4. Require the user to accept a mismatch in aircraft, origin, destination, or
   planned runway.
5. V-speeds may be offered only when the Takeoff and Landing Report contains
   them; they remain editable and require user review before use.
6. Do not silently replace settings while a procedure is active.
7. Do not let network availability block, pause, or fail a cockpit flow.
8. Cache only the last successful normalized summary locally; avoid storing
   the complete OFP unless a future feature requires it.
9. Keep SimBrief transport/parsing separate from aircraft adapters. Imported
   dispatch data is aircraft-neutral input consumed by the existing flow
   settings.

## Suggested user interface

- A SimBrief section in settings for Pilot ID/username and optional auto-import
- **Import latest SimBrief flight** and **Open OFP** actions
- A compact flight card showing route, aircraft, runway, cruise level, age,
  block fuel, and import status
- A review dialog listing values that will update app settings
- Clear states for not configured, loading, imported, stale, mismatch, and
  unavailable

## Implementation shape

- `SimBriefClient`: HTTPS request, timeout, cancellation, and error handling
- `SimBriefOFP` DTOs: transport schema only
- `ImportedFlightPlan`: normalized aircraft-neutral model
- `SimBriefMapper`: optional/missing-field and unit normalization
- `SimBriefImportValidator`: freshness and aircraft/route mismatch checks
- settings additions for Pilot ID/username and auto-import preference
- parser, mapper, mismatch, stale-data, and offline tests using committed
  sanitized fixtures

Use the JSON response rather than XML for the desktop implementation. Apply a
short timeout and identify the application through a normal HTTP User-Agent.

## Later phase: generating a flight plan

Generating or editing an OFP from inside the app is a different API workflow.
It requires a SimBrief developer API key and preserves the SimBrief/Navigraph
login process, normally through a browser or popup. This should not be part of
the first integration.

## Official references

- https://developers.navigraph.com/docs/simbrief/fetching-ofp-data
- https://developers.navigraph.com/docs/simbrief/introduction
- https://developers.navigraph.com/docs/simbrief/how-it-works
- https://developers.navigraph.com/docs/simbrief/using-the-api


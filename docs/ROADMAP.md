# Product Roadmap

This roadmap records intentionally deferred product work. It is not a promise
that experimental features belong in the next public release. Stable aircraft
profiles remain protected by their aircraft-specific regression tests.

## Current development

1. Complete the final live-acceptance matrix for persistent SayIntentions
   Copilot communications and its four departure ATC conversations. Frequency
   tuning belongs exclusively to SayIntentions.
2. SimBrief latest-OFP integration is released as an optional read-only
   operational briefing. Future work may add more advisory comparisons after
   live validation, without allowing network data to control cockpit systems.

## Later enhancements

1. GSX Pro ground-service integration using the official bidirectional Remote
   Control SDK. See `GSX_INTEGRATION_FEASIBILITY.md`.
2. Interactive checklist and crew-audio improvements.

## Final planned major feature

Go-around and rejected-takeoff procedures are deliberately parked until the
end of the current feature roadmap. They require dedicated procedure branches,
safe interruption of the active normal flow, aircraft-specific command and
readback coverage, recovery paths, and complete live testing. Do not implement
them as small additions to the normal takeoff or approach flows.


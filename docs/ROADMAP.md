# Product Roadmap

This roadmap records intentionally deferred product work. It is not a promise
that experimental features belong in the next public release. Stable aircraft
profiles remain protected by their aircraft-specific regression tests.

## Current development

1. Begin GSX Pro integration on a dedicated development branch, preserving all
   completed aircraft and SayIntentions behavior behind regression tests.
2. SimBrief latest-OFP integration is released as an optional read-only
   operational briefing. Future work may add more advisory comparisons after
   live validation, without allowing network data to control cockpit systems.

SayIntentions Copilot communication and departure ATC workflow acceptance was
completed gate-to-gate for v0.9.3. Frequency tuning remains exclusively owned
by SayIntentions.

## Later enhancements

1. GSX Pro ground-service integration using the official bidirectional Remote
   Control SDK. See `GSX_INTEGRATION_FEASIBILITY.md`.
2. Interactive checklist and crew-audio improvements.

## Post-1.0 features

1. Configurable single-engine taxi procedures. Preserve dual-engine taxi as
   the universal default, then add aircraft- and airline-specific options for
   single-engine taxi-in and taxi-out with the appropriate engine selection,
   APU coordination, operating restrictions, and engine warm-up/cool-down
   timing. Do not introduce this feature before the public 1.0 release.

## Final planned major feature

Go-around and rejected-takeoff procedures are deliberately parked until the
end of the current feature roadmap. They require dedicated procedure branches,
safe interruption of the active normal flow, aircraft-specific command and
readback coverage, recovery paths, and complete live testing. Do not implement
them as small additions to the normal takeoff or approach flows.


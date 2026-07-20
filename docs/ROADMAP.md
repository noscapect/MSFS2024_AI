# Product Roadmap

This roadmap records intentionally deferred product work. It is not a promise
that experimental features belong in the next public release. Stable aircraft
profiles remain protected by their aircraft-specific regression tests.

## Current development: v0.9.5 to v1.0

Development is restricted to bug fixing and stabilization until the public
v1.0 release. Do not add new aircraft, integrations, procedure branches, or
major UX features during this phase.

Priorities are:

1. Fix reproducible defects and regressions.
2. Protect completed aircraft profiles with their existing isolation and
   contract tests.
3. Continue live validation of optional SimBrief, SayIntentions, and GSX
   behavior without making them mandatory for normal flows.
4. Keep customer-facing diagnostics concise while retaining bounded flight
   recordings for support.

SayIntentions Copilot communication and departure ATC workflow acceptance was
completed gate-to-gate for v0.9.3. Frequency tuning remains exclusively owned
by SayIntentions.

## Post-1.0 enhancements

1. GSX arrival services and deeper ground-service coordination. The optional
   departure coordinator first released in v0.9.5 uses the official
   bidirectional Remote Control SDK.
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


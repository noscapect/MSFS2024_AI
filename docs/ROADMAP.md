# Product Roadmap

This roadmap records intentionally deferred product work. It is not a promise
that experimental features belong in the next public release. Stable aircraft
profiles remain protected by their aircraft-specific regression tests.

## Current stabilization

The latest public release is v0.9.6. It includes the dashboard redesign, the
optional EFB companion, expanded optional GSX coordination, and the Asobo 737
MAX profile under an explicit experimental warning. Active MAX development is
paused while UX, EFB, and integration reliability are stabilized on `main`.

Priorities are:

1. Continue live validation of the MSFS 2024 EFB companion,
   including start, confirm, pause, resume, cancel, reconnect, and stale-state
   behavior.
2. Keep the EFB a narrow remote over the versioned CommBus protocol; do not
   duplicate procedure or aircraft-control logic in JavaScript.
3. Retain the completed dashboard redesign, GSX passenger progress, and
   collapsible diagnostics on the desktop app.
4. Preserve the MAX limitations and release gates documented in
   `ASOBO_737_MAX_SUPPORT_STATUS.md`; do not resume Flow 7 testing without an
   explicit decision to restart MAX development.
5. Protect completed aircraft profiles with their existing isolation and
   contract tests.
6. Continue live validation of optional SimBrief, SayIntentions, and GSX
   behavior without making them mandatory for normal flows.
7. Keep customer-facing diagnostics concise while retaining bounded flight
   recordings for support.

The EFB may expose existing flow controls and operational state, but must not
refactor simulator command paths or weaken procedure verification.

SayIntentions Copilot communication and departure ATC workflow acceptance was
completed gate-to-gate for v0.9.3. Frequency tuning remains exclusively owned
by SayIntentions.

## Post-1.0 enhancements

1. Deeper GSX arrival services and ground-service coordination. The optional
   coordinator uses the official bidirectional Remote Control SDK; v0.9.6
   already includes exact arrival-stand handoff and deboarding coordination.
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


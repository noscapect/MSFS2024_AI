# iniBuilds A321LR stable support contract

Status as of 2026-07-13: **all twelve gate-to-gate flows live tested and
verified from cold-and-dark startup through final parking/shutdown.**

## Stable implementation boundary

The A321LR is selected from aircraft title `A321` and is routed exclusively to:

- `A321ProcedureLibrary` for all twelve flows.
- `A321ChecklistLibrary` for checklist verification.
- `A321ControlProfile` for locked aircraft-specific flap and sign mappings.

The A321 flap handle is authoritative. Generic left/right flap-surface SimVars
can report clean while the cockpit handle is still at Flaps 1 and must never be
used to declare an A321 flap step complete.

Seatbelt and no-smoking selectors remain AUTO through flight and normal gate
turnaround. The aircraft controls the cabin signs. NO SMOKING changes to OFF
only after the user confirms continuation from normal parking to final
cold-and-dark secure.

## Regression policy

Development for another aircraft must not modify A321 procedures, checklist
conditions, control hashes, command strings, or readback rules. The automated
suite locks the dedicated catalog selection, flap commands/detent authority,
and sign-selector mappings. Any intentional A321 change requires:

1. An A321-specific bug report or requested feature.
2. A regression test reproducing the change.
3. A successful Release test/build.
4. Renewed live validation of every affected flow.

The detailed live-test history remains in `docs/LIVE_TESTS.md`.

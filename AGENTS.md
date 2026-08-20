# Agent development guide

Read `PROJECT.md`, `docs/ARCHITECTURE.md`, `docs/NATIVE_CONTROL_STRATEGY.md`,
and the relevant aircraft support document before changing runtime behavior.
The implementation is authoritative when supporting documents disagree with
the code.

## Core rules

- This product is automation-first. Never use a manual-confirmation step as a
  placeholder for an action assigned to the virtual First Officer or for a
  condition the app can verify through telemetry.
- A First Officer action must use an aircraft-specific command plus an
  independent readback. A First Officer check must use an aircraft-specific
  readback and advance without pilot confirmation.
- Manual confirmation is reserved for genuine Captain decisions, physical
  actions outside the app's interface, or data that cannot be obtained
  programmatically. Label those limitations honestly; do not assign them to
  the virtual First Officer.
- Keep aircraft implementations isolated. Do not reuse commands, offsets,
  selector values, procedures, checklists, or completion assumptions merely
  because two aircraft share a manufacturer or cockpit family.
- Do not call a flow ready for testing while it contains manual-confirmation
  placeholders for First Officer work. Audit the complete flow, final checklist
  gate, timing, retries, recovery, cancellation, and every command/readback pair.

## PMDG boundaries

The PMDG 737 and PMDG 777 use separate SDK namespaces and data layouts. The 737
uses the NG3 profile; the 777 uses the isolated 777X adapter under
`src/Copilot/AircraftAdapters/Pmdg777`. Never allow one profile to fall back to
the other. The PMDG 777 twelve-flow implementation on `main` remains a
development integration until ordered simulator validation is complete.

## Verification

Run focused tests while developing, then run the complete Release suite before
handoff. Development builds require the MSFS 2024 SDK SimConnect libraries at
the path configured in `src/Copilot/Copilot.csproj`, currently
`C:\MSFS 2024 SDK\SimConnect SDK\lib`.

```powershell
dotnet restore .\tests\Copilot.Tests\Copilot.Tests.csproj
dotnet build .\src\Copilot\Copilot.csproj -c Release --no-restore
dotnet test .\tests\Copilot.Tests\Copilot.Tests.csproj -c Release --no-restore
```

For a public release, test the executable from the Release folder, update the
customer and aircraft-status documentation, and follow `docs/RELEASING.md`.
Do not restart the application during an active flight unless the user asks.

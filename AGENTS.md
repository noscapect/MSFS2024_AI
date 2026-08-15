# Development rules

- This product is automation-first. Never use a manual-confirmation step as a
  placeholder for an action assigned to the virtual First Officer or for a
  condition the app can verify through telemetry.
- A First Officer action must use an aircraft-specific command plus an
  independent readback. A First Officer check must use an aircraft-specific
  readback and advance without pilot confirmation.
- Manual confirmation is reserved for genuine Captain/pilot decisions,
  physical actions outside the app's interface, or data that cannot be obtained
  programmatically. Label those limitations honestly; do not assign them to the
  virtual First Officer.
- Do not call an aircraft flow ready for testing while it still contains
  manual-confirmation placeholders for First Officer work. Audit the complete
  flow, its final checklist gate, recovery behavior, and every command/readback
  pair first.

# Companion relay

This Cloudflare Worker routes live messages between one Windows companion and
one Android tablet. It does not connect to MSFS and does not contain procedure
or aircraft-control logic.

The relay only authenticates the session and routes opaque encrypted
envelopes. It cannot decrypt telemetry or commands. The Windows application
remains the enforcement point and keeps companion control disabled unless an
explicit development override is set.

The relay is deliberately **not production deployable yet**. Abuse limits,
operational monitoring, and live cross-network validation must be completed
first.

## Development

```powershell
npm.cmd install
npm.cmd run check
npm.cmd run dev
```

Never place session credentials in query parameters or commit deployment
credentials. Native clients send the short-lived session credential through
the `Authorization` header.

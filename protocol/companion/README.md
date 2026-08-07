# Companion protocol

This directory contains the transport-neutral contract shared by the Windows
companion, the native Android application, and the optional relay.

The existing MSFS CommBus protocol remains version 2. The Android transport
uses protocol version 1 and deliberately preserves the same command semantics:

- `request_state`
- `start_flow`
- `start_next_flow`
- `gsx_open_menu`
- `gsx_menu_choice`
- `confirm`
- `pause`
- `resume`
- `cancel`

The Windows application remains authoritative. A transport accepting a JSON
message does not imply that the requested operation is valid; the operation is
revalidated against the current aircraft and procedure state immediately
before it is queued.

`fixtures/state.json` is the compatibility fixture consumed by Android and
desktop tests. Additive fields are allowed. Removing a field, changing its
type, or changing command meaning requires a new protocol version.

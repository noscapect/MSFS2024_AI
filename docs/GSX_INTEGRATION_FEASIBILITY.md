# GSX Pro Integration Feasibility

## Conclusion

GSX Pro integration is feasible and has an official implementation path. It
should be developed after the SayIntentions integration is complete, as an
optional ground-services adapter that is independent of every aircraft
adapter.

FSDreamTeam publishes a GSX Remote Control SDK. The locally installed SDK is
available at:

`C:\Program Files (x86)\Addon Manager\couatl\GSX\SDK`

It contains design documentation and a C++ Remote Control sample. The sample
uses SimConnect, GSX LVars, two GSX events, and the dynamically generated GSX
menu and tooltip files. This is substantially safer than automating toolbar
clicks or guessing undocumented aircraft variables.

## Development status

The initial departure coordinator is released as optional beta functionality
in v0.9.5. Its scope is intentionally smaller than complete GSX automation:

- GSX keeps all service-depth and timing configuration.
- The app requests boarding during preflight when enabled.
- The app requests **Prepare for Pushback and Departure** after the existing
  ATC clearance checkpoint.
- The app displays later GSX questions and returns the user's selected answer;
  captain choices remain under pilot control.
- Arrival services and deeper automation remain deferred until after v1.0.

The protocol proof, settings UI, Couatl status, dynamic menu parser, ownership
guard, and the two initial request hooks are implemented. Parking-brake and
engine-start prompt handling still requires live GSX menu/status captures.
Flow 4 is sequenced behind confirmed pushback movement whenever GSX departure
coordination is enabled, preventing engine start at the stand before pushback.
Live testing confirmed that Remote Control cannot be used as a one-way service
launcher. After the tug connects, GSX emits additional menus (for example,
deicing and pushback direction) to the registered remote controller. The app
must display every unhandled menu dynamically and return the user's selected
zero-based choice through `L:FSDT_GSX_MENU_CHOICE`; otherwise GSX remains at
`Locking gear` until its hidden prompt times out.

## Confirmed capabilities

The official interface can:

- Detect whether the Couatl/GSX engine is running.
- Put GSX into Remote Control mode.
- Open and read the current contextual GSX menu without displaying the MSFS
  toolbar panel.
- Select any action that the user could select from that menu.
- Receive subsequent menus, questions, status messages, and timeouts.
- Reproduce those interactions inside another application.

FSDreamTeam explicitly states that the SDK can command anything available to
the user and can replicate GSX menus in another gauge or application. Its
guidance also says a robust implementation must remain bidirectional for the
whole operation because pushback, deicing, refuelling, and other services can
produce additional contextual questions.

GSX itself supports MSFS 2020 and 2024 and provides boarding/deboarding,
baggage and cargo, refuelling, catering, pushback, deicing, follow-me,
marshalling, jetway/stair operations, aircraft profiles, and SimBrief-based
passenger and fuel loading.

## Official protocol found in the installed SDK

The current sample performs the following sequence:

1. Locate the FSDT installation through
   `HKEY_CURRENT_USER\Software\Fsdreamteam`.
2. Locate the GSX package `menu` and `tooltip` files below
   `MSFS\fsdreamteam-gsx-pro\html_ui\InGamePanels\FSDT_GSX_Panel`.
3. Register these LVars through SimConnect:
   - `L:FSDT_GSX_COUATL_STARTED`
   - `L:FSDT_GSX_MENU_OPEN`
   - `L:FSDT_GSX_MENU_CHOICE`
   - `L:FSDT_GSX_SET_REMOTECONTROL`
4. Set `L:FSDT_GSX_SET_REMOTECONTROL` to `1` for the session.
5. Subscribe to `EXTERNAL_SYSTEM_TOGGLE` for menu lifecycle events and
   `EXTERNAL_SYSTEM_SET` for tooltip/status messages and their timeouts.
6. Read every menu dynamically and return the selected index through
   `L:FSDT_GSX_MENU_CHOICE`.
7. Set Remote Control back to `0` during clean shutdown.

These implementation details must be rechecked against the installed SDK at
development time because GSX is independently updated.

## Useful features for this application

### Departure

- Show GSX connection and service status without opening the toolbar menu.
- Request boarding, baggage loading, catering, and refuelling.
- Reuse the imported SimBrief passenger/fuel context while avoiding duplicate
  loading commands; GSX can already import SimBrief directly.
- Coordinate doors, jetway or stairs, ground power, and service completion
  with the preflight flows.
- Request deicing when applicable, while leaving fluid and treatment choices
  visible to the pilot.
- Request and monitor pushback from the normal flow at the correct point.
- Coordinate parking brake, beacon, APU, and engine-start prompts with the
  existing aircraft-specific procedures.

### Arrival

- Offer follow-me and marshalling to the selected stand.
- Use an assigned destination gate from SayIntentions when available, while
  allowing the pilot to override it.
- Request jetway/stairs, deboarding, baggage unloading, catering, and other
  turnaround services.
- Monitor service completion before a later turnaround or shutdown action.

### User experience

- Keep the integration completely optional; flights must work normally when
  GSX is not installed or Couatl is offline.
- Add a small `GSX OFFLINE`, `GSX READY`, or `GSX ACTIVE` dashboard state.
- Put configuration and manual service controls under `Manage GSX` instead of
  crowding the primary flight controls.
- Surface contextual GSX questions in the app and reuse the normal Confirm
  action only where the answer is unambiguous and pilot-authorized.
- Never force users who do not own GSX to buy or install it.

## Required architecture

Create an isolated `Gsx` integration service. It must not be added to an
Airbus, Boeing, or individual-aircraft adapter. Aircraft adapters may expose
generic facts such as door state, beacon state, parking brake, or APU status,
but the GSX service owns all GSX protocol and workflow state.

Use a state machine with at least:

- Disconnected
- Ready
- Requesting service
- Awaiting GSX menu response
- Service in progress
- Completed
- Failed or timed out

The menu parser must consume the live menu every time. Do not hardcode a
sequence such as “open GSX and choose item 4,” because menu content and
follow-up dialogs depend on position, aircraft profile, active services,
weather, and GSX version.

Only one component may own GSX Remote Control in a session. Startup must
detect an existing Remote Control owner and avoid disrupting aircraft EFBs or
other add-ons. The implementation records VFO ownership in a short-lived
local lease so a replacement VFO process can recover after an interrupted
shutdown. Because GSX exposes only a binary active flag and no owner identity,
an active flag without a valid VFO lease requires explicit user confirmation
through `Manage GSX`; it is never taken over silently. Clean shutdown releases
the GSX flag and clears the lease.

## Risks and constraints

- GSX is paid third-party software. We must not redistribute GSX files, its
  SDK, profiles, or assets. The integration should use the user's installed
  copy.
- The SDK sample reads menu and tooltip files. Localisation and text changes
  make exact English-text matching unsafe; parsing should preserve GSX's
  dynamic choices and use conservative semantic matching only where needed.
- Remote Control must be bidirectional for the complete service. Enabling it
  only long enough to start a service can hide later prompts and break the
  operation.
- Pushback, refuelling, and aircraft EFB loading can conflict with other
  add-ons. The app must detect active operations and never issue duplicate
  requests.
- Aircraft and airport GSX profiles vary. Every supported aircraft needs live
  departure and arrival regression tests, even though the protocol adapter is
  shared.
- The app must provide a clean manual fallback after a timeout instead of
  blocking a flight.
- Seated passengers are not available for encrypted Marketplace aircraft,
  although the wider GSX ground-service feature set remains available.

## Recommended implementation phases

1. **Protocol proof:** connect to the installed SDK interface, detect Couatl,
   mirror dynamic menus and tooltip messages in diagnostics, and send no
   service commands.
2. **Manual GSX control:** add `Manage GSX`, display the live menu, and allow
   user-selected actions through the official protocol.
3. **Departure assistance:** integrate boarding/service completion and then
   pushback, with every secondary GSX prompt handled dynamically.
4. **Arrival assistance:** add stand selection, follow-me/marshalling,
   jetway/stairs, deboarding, and turnaround services.
5. **Safe automation profiles:** add standard defaults with user/airline
   overrides only after gate-to-gate testing on every supported aircraft.

The protocol proof should be a modest task. A polished, reliable gate-to-gate
integration is a larger feature because the difficult part is coordinating
dynamic services and aircraft state, not sending the initial GSX command.

Implemented baseline: VFO can request boarding, prepare departure/pushback,
and request deboarding after Flow 12 detects the aircraft parked at the gate
with parking brake set, engines off, and APU or external power available. Each
automation is optional in the GSX integration settings so cargo or positioning
flights can keep GSX services manual.

## Sources

- [FSDreamTeam GSX Pro product and feature documentation](https://www.fsdreamteam.com/products_gsxpro.html)
- [FSDreamTeam administrator: official Remote Control SDK capabilities](https://www.fsdreamteam.com/forum/index.php?topic=33300.0)
- [FSDreamTeam administrator: required bidirectional Remote Control design](https://www.fsdreamteam.com/forum/index.php?topic=32882.0)
- Locally installed official SDK design document and C++ sample under
  `C:\Program Files (x86)\Addon Manager\couatl\GSX\SDK\Samples\GSX_Remote`


# Native Android companion

## Implementation status

The Android companion foundation is under active development. The repository
currently contains:

- A native Kotlin and Jetpack Compose tablet application
- Adaptive landscape and portrait tablet cards for aircraft, telemetry, current flow, flow
  selection, procedure controls, and GSX prompts
- A transport-neutral version 1 companion contract and shared JSON fixtures
- A Windows bridge that reuses the existing CommBus command parser and all
  authoritative runtime guards
- An outbound development WebSocket client for datacenter testing
- ChaCha20-Poly1305 end-to-end encryption with separate relay credentials,
  timestamp validation, and replay rejection
- QR generation in the Windows app and permission-less native QR scanning on
  Android
- Pairing secrets protected with Windows DPAPI and Android Keystore
- Desktop-side pairing replacement and revocation
- Encrypted direct-LAN transport, with LAN addresses carried in the pairing QR
  and automatic fallback to the internet relay
- A Cloudflare Durable Object relay that supports one desktop and one tablet
  per session while seeing only encrypted message envelopes

The Windows configuration keeps remote controls disabled by default. Do not set
`VFO_COMPANION_ALLOW_CONTROL=1` until live security and network validation are
complete.

The existing MSFS EFB remains on CommBus protocol version 2 and is not replaced
by the Android protocol.

## Android build

Requirements:

- JDK 17
- Android SDK Platform 36
- Android SDK Build Tools

From `src/AndroidCompanion`:

```powershell
.\gradlew.bat testDebugUnitTest assembleDebug
```

The beta package ID is `com.noscapect.vfo.companion.beta`. Production Play
builds will use a separate stable package and signing configuration so a
GitHub beta APK cannot conflict with the Play-installed application.

## Distribution decision

Keep the Android app in this repository as its own Gradle project and publish
it as a separate artifact. It shares a versioned protocol and release context
with the Windows program, so a separate repository would make compatible
changes and security review harder. It must not be embedded in the Windows ZIP
or installed by the Windows updater.

During development, attach a signed beta APK to GitHub releases. For general
availability, publish the stable application through Google Play (initially an
internal or closed testing track), using Play App Signing and an application ID
that is distinct from the beta build. The desktop release and Android release
may share a version tag, but each remains independently installable and
rollbackable.

## Relay development

From `src/CompanionRelay`:

```powershell
npm.cmd install
npm.cmd run check
npm.cmd run deploy -- --dry-run
```

The Worker is not deployed by the normal desktop release process.

## Explicit development connection

The Windows client is off unless all four environment variables are set:

```text
VFO_COMPANION_DEVELOPMENT=1
VFO_COMPANION_RELAY=wss://relay.example
VFO_COMPANION_SESSION=<20-80 URL-safe characters>
VFO_COMPANION_SECRET=<base64url-encoded 32-byte secret>
# Keep omitted until the remaining security gates pass:
VFO_COMPANION_ALLOW_CONTROL=1
```

The corresponding temporary Android pairing URI is:

```text
vfo://pair?relay=wss%3A%2F%2Frelay.example&session=<session>&secret=<secret>&controls=0
```

Never publish that URI or include it in diagnostics. The pairing dialog adds
current LAN addresses to its QR code. Windows Firewall may ask whether Copilot
can accept TCP connections; allow this only on trusted Private networks. The
tablet tries those direct addresses first, then falls back to the relay without
exposing message contents to it.

## Remaining gates before a test APK

1. Enable control messages only after security tests pass.
2. Compile, lint, unit-test, and instrument-test with Android SDK 36.
3. Test reconnection over same-LAN Wi-Fi, mobile hotspot, and a remote
   datacenter PC.

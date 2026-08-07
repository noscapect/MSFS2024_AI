# Virtual First Officer Android companion

This is a native Android tablet app built with Kotlin and Jetpack Compose. It
is a thin client for the authoritative Windows Copilot runtime; it does not
contain a second procedure engine and it never connects directly to MSFS.

The app tries an encrypted direct-LAN connection from addresses in the pairing
QR, then falls back to the encrypted internet relay. See
[`docs/ANDROID_COMPANION.md`](../../docs/ANDROID_COMPANION.md) for build,
security, deployment, and distribution details.

Build with JDK 17 and Android SDK Platform 36:

```powershell
.\gradlew.bat testDebugUnitTest lintDebug assembleDebug
```

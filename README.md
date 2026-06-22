# MSFS 2024 AI First Officer

A Windows first-officer companion for the **iniBuilds A320neo V2** in
Microsoft Flight Simulator 2024.

The application connects to the simulator through SimConnect and the installed
MobiFlight WASM module. It guides a complete 12-flow gate-to-gate flight,
automates verified First Officer actions, monitors Captain actions, and speaks
important callouts.

> This is an independent community project. It is not affiliated with or
> endorsed by Microsoft, Asobo Studio, iniBuilds, Airbus, or MobiFlight.

## Current capabilities

- Complete flow from cold and dark through shutdown
- Automatic First Officer switch and lever actions where reliable aircraft
  commands and independent readback are available
- Monitoring of Captain actions without requiring app confirmations during
  takeoff, approach, landing, or taxi
- Native iniBuilds A320neo V2 state monitoring through MobiFlight
- Verification after every automatic aircraft action
- Optional Windows offline voice callouts
- Persistent preflight settings for V1, VR, and transition altitude
- Automatic flow recommendation based on the current flight phase
- Monitor-only and confirmation-based automation modes
- Runtime activity log and diagnostic status display

Voice callouts include engine-start monitoring, takeoff calls, landing gear up
and down, minimums, spoilers, reverse green, and deceleration.

The gameplay flow is defined in [docs/checklist.md](docs/checklist.md).

## Supported aircraft

Only the Microsoft Flight Simulator 2024 **iniBuilds A320neo V2** is supported.
The application deliberately avoids guessed generic events for unsupported
aircraft controls.

## Requirements

To run the application:

- Windows 10 or Windows 11
- Microsoft Flight Simulator 2024
- iniBuilds A320neo V2
- MobiFlight WASM module installed in MSFS
- .NET Framework 4.7.2 or newer

To build from source:

- .NET SDK 10 or newer
- MSFS 2024 SDK installed at `C:\MSFS 2024 SDK`

The project currently references the SDK's SimConnect libraries from that
default location.

## Build

```powershell
dotnet build .\src\Copilot\Copilot.csproj -c Release
```

The executable is created at:

```text
src\Copilot\bin\Release\net472\Copilot.exe
```

## Run

Start MSFS 2024, load the iniBuilds A320neo V2, and then run:

```powershell
.\src\Copilot\bin\Release\net472\Copilot.exe
```

The dashboard lets you select and start each flow, choose the automation
policy, enable voice callouts, pause or cancel a procedure, and inspect live
aircraft status.

Settings are stored in:

```text
%LOCALAPPDATA%\MSFS2024_AI\settings.xml
```

Runtime logs are stored in:

```text
%LOCALAPPDATA%\MSFS2024_AI\logs\copilot.log
```

## Gate-to-gate flows

1. Power Up & Initial Setup
2. Flight Computer & Pre-Flight
3. APU Start & Pushback
4. Engine Start Sequence
5. After Start & Taxi
6. Before Takeoff
7. Takeoff & Climb
8. Cruise
9. Descent Preparation
10. Approach & Landing
11. After Landing & Taxi
12. Parking & Shutdown

ATC communication and flight-computer data entry remain pilot tasks. Automatic
STD/QNH switching is intentionally excluded because no reliable verified
aircraft interface was found.

## Safety model

Sending a command is not considered success. Each automatic action:

1. Uses a documented or Behavior Viewer-confirmed aircraft interface.
2. Waits for separate aircraft-state readback.
3. Completes only after the requested state is verified.
4. Stops the active flow if verification fails.

The app does not silently try guessed alternate events. The final cold-and-dark
section also requires explicit confirmation before electrical power is removed.

## Diagnostic probe

`SimConnectProbe` is a read-only development utility for inspecting aircraft
Input Events and native variables:

```powershell
dotnet build .\src\SimConnectProbe\SimConnectProbe.csproj -c Release
.\src\SimConnectProbe\bin\Release\net472\SimConnectProbe.exe list-lvars
```

See [docs/NATIVE_CONTROL_STRATEGY.md](docs/NATIVE_CONTROL_STRATEGY.md) and
[docs/LIVE_TESTS.md](docs/LIVE_TESTS.md) for control evidence and test history.

## Project structure

- `src/Copilot` — WinForms application, simulator integration, procedures, and UI
- `src/SimConnectProbe` — read-only simulator discovery utility
- `docs` — checklist, architecture, mappings, and live-test evidence
- `tools` — control-catalog generation scripts

## Development status

The 12 flows are implemented, but later flows still require full end-to-end
live testing. Treat this as active development software and remain ready to
operate the aircraft manually.

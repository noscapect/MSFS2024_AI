# Native iniBuilds control strategy

## Purpose

This document defines the preferred method for operating iniBuilds A320neo V2
controls. It exists to prevent repeating the long and unreliable process of
guessing Input Event bindings.

## The working architecture

iniBuilds commonly separates a control into two kinds of LVars:

1. **Command LVar** — a momentary input consumed by the aircraft systems.
2. **State LVar** — the resulting cockpit or system state used for independent
   verification.

Example:

| Function | Command | State |
|---|---|---|
| APU master | `INI_APU_MASTER_SWITCH_CMD` | `INI_APU_MASTER_SWITCH` |
| APU start | `INI_APU_START_BUTTON_CMD` | `INI_APU_START_BUTTON` |

The command LVar must be treated as a momentary pushbutton:

```text
1 (>L:INI_APU_MASTER_SWITCH_CMD)
wait approximately 150 ms
0 (>L:INI_APU_MASTER_SWITCH_CMD)
```

The application then waits for:

```text
INI_APU_MASTER_SWITCH == requested state
```

For APU start, button-state verification is only the first postcondition.
Procedure completion must additionally wait for:

```text
INI_APU_AVAILABLE != 0
```

## Required control workflow

For every new automatic control:

1. Enumerate the loaded aircraft's LVars with `MF.LVars.List`.
2. Look for an explicit command/state pair.
3. If a `_CMD` or `_COMMAND` LVar exists, pulse it from `1` to `0`.
4. Never use the command LVar itself as the resulting state.
5. Verify an independent state LVar.
6. Apply a timeout and fail without retrying alternate expressions.
7. Test both directions when the control supports ON and OFF.
8. Record the mapping and live-test result before adding it to a procedure.

## What not to do

### Do not write state LVars as the normal command path

Writing a state LVar can move a cockpit indication without causing the
underlying aircraft system to act.

Observed example:

- Writing `INI_APU_START_BUTTON = 1` changed the button state.
- `INI_APU_AVAILABLE` remained false.

That is visual-state manipulation, not verified aircraft operation.

### Do not treat Input Event values as readback

Input Events such as `AIRLINER_APU_MASTER` remained at `0` while the actual
APU state changed. They are command interfaces, not reliable state telemetry.

### Do not guess B-event suffixes

`_Set`, `_PUSH`, `_ON`, and `_OFF` must not be tried in sequence until one
appears to work. Even Behavior Viewer binding names may require invocation
details that are not visible while collapsed.

### Do not trust generic APU SimVars over native state

Generic APU RPM/starter telemetry reported an operating APU while native
iniBuilds state reported the APU unavailable and switched off.

Native iniBuilds variables take precedence.

### Do not accept command transmission as success

Success requires all of:

- Command accepted by the transport
- Independent state transition
- Expected system-level result
- Completion before timeout

## Known native APU variables

| Variable | Role |
|---|---|
| `INI_APU_MASTER_SWITCH_CMD` | Momentary APU master command |
| `INI_APU_MASTER_SWITCH` | APU master state |
| `INI_APU_START_BUTTON_CMD` | Momentary APU start command |
| `INI_APU_START_BUTTON` | APU start-button state |
| `INI_APU_AVAILABLE` | APU available system state |
| `__APU_BLEEDIsPressed` | APU bleed press-animation state |
| `INI_APU_BLEED_BUTTON` | APU bleed button state |
| `__ELEC_APU_GENIsPressed` | Momentary APU generator command |
| `INI_APU_GEN_ON` | APU generator switch state |
| `INI_APU_N1` | Native APU speed indication |

### Verified APU bleed command

Behavior Viewer exposed the exact Mouserect code:

```text
(L:INI_APU_BLEED_BUTTON) ! (>L:INI_APU_BLEED_BUTTON)
(L:__APU_BLEEDIsPressed) ! (>L:__APU_BLEEDIsPressed)
```

Execute this only when `INI_APU_BLEED_BUTTON` differs from the requested
state. Live ON and OFF tests both passed on June 20, 2026.

## Known native fuel-pump state variables

| Aircraft area | State LVar |
|---|---|
| Left outer tank pump | `INI_OUTER_TANK_LEFT_PUMP_ON` |
| Left inner tank pump | `INI_INNER_TANK_LEFT_PUMP_ON` |
| Right inner tank pump | `INI_INNER_TANK_RIGHT_PUMP_ON` |
| Right outer tank pump | `INI_OUTER_TANK_RIGHT_PUMP_ON` |
| Center left pump | `INI_CENTER_TANK_LEFT_PUMP_ON` |
| Center right pump | `INI_CENTER_TANK_RIGHT_PUMP_ON` |

The native momentary input LVars are:

- `__FUEL_ENG1_L1IsPressed`
- `__FUEL_ENG1_L2IsPressed`
- `__FUEL_ENG2_R1IsPressed`
- `__FUEL_ENG2_R2IsPressed`

These are press-animation variables, not standalone command endpoints. Pulsing
them alone did not operate the pump buttons.

The A320 fuel panel has six pump pushbuttons:

- Left tank pumps 1 and 2
- Center tank pumps 1 and 2
- Right tank pumps 1 and 2

Native readback must therefore include all six corresponding `INI_*_PUMP_ON`
variables. Four-pump completion logic is invalid.

## Verified six-pump command pattern

Behavior Viewer exposed the actual Mouserect code for left tank pump 1:

```text
(L:INI_OUTER_TANK_LEFT) ! (>L:INI_OUTER_TANK_LEFT)
(L:__FUEL_ENG1_L1IsPressed) ! (>L:__FUEL_ENG1_L1IsPressed)
```

The same selector/press-state pattern applies to all six buttons:

| Button | Selector LVar | Press-animation LVar | Verification LVar |
|---|---|---|---|
| L1 | `INI_OUTER_TANK_LEFT` | `__FUEL_ENG1_L1IsPressed` | `INI_OUTER_TANK_LEFT_PUMP_ON` |
| L2 | `INI_INNER_TANK_LEFT` | `__FUEL_ENG1_L2IsPressed` | `INI_INNER_TANK_LEFT_PUMP_ON` |
| C1 | `INI_CENTER_TANK_LEFT` | `__FUEL_CTR_1IsPressed` | `INI_CENTER_TANK_LEFT_PUMP_ON` |
| C2 | `INI_CENTER_TANK_RIGHT` | `__FUEL_CTR_2IsPressed` | `INI_CENTER_TANK_RIGHT_PUMP_ON` |
| R1 | `INI_INNER_TANK_RIGHT` | `__FUEL_ENG2_R1IsPressed` | `INI_INNER_TANK_RIGHT_PUMP_ON` |
| R2 | `INI_OUTER_TANK_RIGHT` | `__FUEL_ENG2_R2IsPressed` | `INI_OUTER_TANK_RIGHT_PUMP_ON` |

Only execute a button's toggle expression when its independent pump-on state
differs from the requested state. This makes the group command idempotent.

Live testing on June 20, 2026 passed all six pumps OFF and then ON, with native
readback changing respectively to `0/0/0/0/0/0` and `1/1/1/1/1/1`.

## Evidence status

On June 20, 2026, the APU master command/state pair successfully completed an
ON and OFF cycle with native verification. The momentary pulse implementation
for APU master/start was then adopted as the primary control pattern.

The APU START command is complete when the native START button accepts the
selection. `INI_APU_AVAILABLE` is a later condition: the procedure waits for
it in a separate observation step while the APU completes its start and
warm-up cycle. A fixed 60-second AVAIL deadline produced a false failure and
must not be used.

MobiFlight runtime LVar layouts persist for the simulator session. The client
name includes a schema suffix (`MSFS2024_AI_Copilot_v2`); increment that suffix
whenever the ordered LVar list changes. Otherwise a new build can read old
offsets until MSFS is restarted.

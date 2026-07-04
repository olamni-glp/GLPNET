# Contract — Terminal command surface (typed commands + PF keys)

**Status**: authoritative for the operator-facing surface of `--tui`. Every action has a **typed-command
equivalent** (RDP-safe, FR-002/SC-001); function keys are accelerators only where the host terminal passes them
(FR-008/FR-020). Extends the prototype surface (`tui.py`) — the existing commands are preserved (FR-026).

## Transmit

- **`//` + Enter** — transmit the composed block (authoritative, RDP-safe). FR-003.
- **F9 / Ctrl-X / Alt-Enter** — transmit accelerators where passed. FR-008.

## Typed slash-commands

| Command | Action | FR | Status vs prototype |
|---|---|---|---|
| `/help` | help page | FR-004 | present |
| `/theme [name]` | cycle or set `GREEN\|AMBER\|WHITE\|PAPER\|COLOR` | FR-004/FR-021 | present |
| `/pages` | list pages with owner (me / peer name) + current | FR-004/FR-009 | present (add owner-by-name) |
| `/new [name]` | new page | FR-004/FR-009 | present |
| `/next` `/prev` `/goto N` | switch page | FR-004/FR-009 | present |
| `/focus` | toggle screen↔command focus | FR-004 | present |
| `/quit` | quit | FR-004 | present |
| `/send <text>` | send text as one message | FR-004 | present |
| `@<name> <text>` | direct to a named peer; unknown ⇒ reported (not redirected) | FR-006/FR-040 | **harden** (resolve vs `peers()`) |
| `/transmit` | transmit the current **page** as an owned block | FR-007/FR-010 | **new** |
| `/joint [on\|off]` | toggle joint mode on the current page | FR-012 | **new** |
| `/pin R C "block" [transient\|permanent]` | send a pinpoint change to a joint page | FR-013/FR-014 | **new** |
| `/undo-pin` | dismiss a transient pinpoint → restore saved original | FR-014 | **new** |
| `/mask` … / `/fill` … | define / fill a mask-form on a page | FR-015 | **new** |
| `/repl [name]` | spawn a REPL-bound page; goals entered there evaluate over the link | FR-016 | **new** |
| `/layout [lines N\|two-strip]` | choose compose layout (default ~3 lines) | FR-023 | **new (two-strip)** |
| `/bind Fx <action>` | bind a free PF key to a page action / server signal | FR-019 | **new** |
| `/rcopy [init]` | open the file-transfer wizard (client); `init` configures the responder | FR-018/FR-032 | **new** |

## PF-key activation (where passed) — FR-020

- `Fx` fires directly, **no modifier**.
- `Shift+F1..F12` = **PF13..PF24** (3270 convention).
- `Ctrl` alternates are provided as fallbacks for keys the host terminal reserves (e.g. Windows Terminal F11).
- **Every** bound key shows its typed-command equivalent in the legend, so it remains usable over Remote Desktop.

## Dynamic PF-legend — FR-025

Rendered as small **reverse-video blocks** just above the command line; each block shows its action and its
typed-command equivalent. The legend reflects the current bindings live (FR-019).

## OIA status line — FR-022

Shows: mode · current page `X/N : name (owner)` · link info/state (up / closed / faulted-token, R6) · the PF-key
legend.

## Presentation — FR-021/FR-024

Five themes (command lines keep the purple/magenta accent in every theme); an ASCII screen-art splash on startup.

## Fallback — FR-005/FR-041

When stdin/stdout is not a TTY, `--tui` runs the plain line console (`link_console`) instead, still sending/
receiving over the link and honoring `@name` routing with unknown-peer reporting.

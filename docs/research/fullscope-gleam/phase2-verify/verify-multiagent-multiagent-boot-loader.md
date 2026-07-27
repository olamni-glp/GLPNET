<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-multiagent-multiagent-boot-loader` (WP b3-c1-007, wave 2)

**Date**: 2026-07-21
**Method**: source-verification (`glp/multiagent.gleam`) + attempted loads of the named reference programs on the Gleam instance, with a Dart cross-check.
**Paired close**: `close-multiagent-multiagent-boot-loader` — **activated** (all three ABSENT). This is the US3/G2 multiagent port (largest wholly-absent subsystem).

## Verdict table

| # | detail_id | verdict | basis |
|---|-----------|---------|-------|
| 1 | `multiagent-boot-loader` | **ABSENT** | `multiagent.gleam` is an empty placeholder; no `@agent`-directive parsing / boot loader |
| 2 | `multiagent-global-send` | **ABSENT** | no global-send protocol / globalized-writer table (empty module) |
| 3 | `multiagent-isolate-manager` | **ABSENT** | no per-agent BEAM-process isolation (empty module) |

**Tally**: ABSENT 3.

## Evidence

### Source — the module is an intentional empty placeholder
`glp_gleam/src/glp/multiagent.gleam` (its entire body): *"Empty-but-building (feature 033): intentionally carries no exported definitions and no ported GLP semantics yet … The heavy port lands in a downstream feature (F4+)."* So none of the boot-loader, global-send, or isolate-manager runtime exists in Gleam.

### Port-cost scoping (per the risk note — not a generic ABSENT)
The Dart reference `glp_runtime/lib/multiagent/` is **9 modules**: `agent_runtime`, `boot_loader`, `global_send`, `global_writers_table`, `isolate_manager`, `mad_context`, `mad_helpers`, `message_queue`, `payload_serializer`. The close/build must port all of it, mapping Dart **isolates → BEAM processes** for per-agent isolation. This is the feature's largest single build (G2: mandatory/critical/urgent).

### Runnable attempts (both named acceptance programs)
- **`programs/multiagent/play_alice_bob.glp`** — Gleam load **fails**: `StagedError(ParseStage, ParseError, Pos(16,24), "Expected \".\" after type definition")`. **Dart fails identically**: `[syntax] Expected "." after type definition at Line 16, Column 24`. Root cause: the play uses `|` as the type-alternative separator (`UserCmd ::= connect(_) | decision(_,_,_).`) where GLP uses `;`. So the play is **malformed on BOTH runtimes** — Gleam is at parity at the error, and this is **not** a Gleam multiagent gap. The file also contains **no `@agent` directives**, so it is not actually a boot-loader probe.
- **`programs/tests/test_relay_send.glp`** — Gleam **loads and runs** as ordinary GLP: `relay([a,b], Out, ch(1,In))` → `In = [a, b | X20], Out = X2, → succeeds`. But this program is a local `send/3`-as-defined-guard stream relay, **not** the multiagent global-send protocol (globalized-writer table across agents). It therefore does not probe `multiagent-global-send` either way; the global-send runtime is absent per the source above.

## Finding (routed to the close / US3 acceptance)

The named reference plays are a broken acceptance target: **`play_alice_bob.glp` does not load on the Dart reference either** (malformed `|` type-alternative syntax). US3's Independent Test relies on `programs/multiagent/play_alice_bob.glp` (and the plan's other named plays) running green on the Gleam instance — that cannot happen until the plays are repaired to current GLP type syntax (`;`) **and** the multiagent runtime is ported. The close WP `close-multiagent-multiagent-boot-loader` must therefore include (a) the full ~9-module runtime port, (b) `@agent`-directive boot parsing, and (c) fixing/replacing the reference plays so they are a runnable parity target on both runtimes.

## Activation

`close-multiagent-multiagent-boot-loader` (US3/G2 `build-multiagent`) — activated for all three, with the reference-play repair called out above.

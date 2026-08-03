# T001 Baseline — 064 post-wave gap closure (host: ariellas, 2026-08-03, branch 064 @ 275dc3c2)

## Green baselines (zero-failure gates for every later checkpoint)

| Suite | Result | Invocation notes (this host) |
|---|---|---|
| REPL unified, Sections A/B/C | **381/381** (A 221, B 110, C 50) | requires `DART=/d/BSTDEV/tools/dart-sdk/bin/dart.exe` (dart not on PATH; suite fallback is a Linux path) |
| C# glp_link.tests | **165/165** | |
| C# glp_il_codec.tests | **64/64** | |
| C# glp_engine_host.tests | **63/63** | |
| C# glp_wire_registry.tests | **6/6** | |
| Gleam suite (smoke gate) | **569/569, gate PASS** | WSL + user-space OTP 25.3.2.8 (`export PATH=$HOME/otp-25.3.2.8/bin:$PATH`); built this session (CFLAGS=-std=gnu17, --disable-jit); `rm -rf build` needed after any OTP switch |
| Parity corpus (206-case) | **206/206, 100% in-scope agreement, 0 out-of-scope** | WSL gleam shim (single-runtime — WSL networking asymmetry does not apply) |

## Environment repairs performed to reach baseline (no repo code changed)

1. `DART` override for the REPL suite (dart absent from PATH; SDK at `D:\BSTDEV\tools\dart-sdk`).
2. Rebuilt the stale `out/csharp` C# REPL (binary predated the hasPathish load fix; Section I "File not found: glp\…" symptom).
3. Built Erlang/OTP 25.3.2.8 from source in WSL user space (Ubuntu 26.04 system OTP is 27; Windows OTP is 29 — both off-pin).
4. All notes promoted to CLAUDE.md (Test Protocol section) for future sessions.

## Known host-environment deviation (recorded, NOT a code regression)

**Cross-runtime Section I: 12/18 on this host** (recorded fleet baseline: 18/18 at v2026.08.03.1, gavriella's host).
- Passing: pc_integers/strings/terms, rt_integers/strings/structs, link scenarios [G→C] direction, mismatch[C#].
- Failing (6): rt_send_face [C→G], rt_recv_chain [both], rt_monitor_eos [both], mismatch[Gleam].
- Root cause: the harness runs the Gleam peer under **Windows gleam + Windows Erlang OTP 29** (pin is OTP 25). The failures match the pre-D-9 truncation class, consistent with OTP-29 socket-behavior drift.
- WSL routing is NOT a fix: WSL2 localhost is asymmetric — Windows→WSL dials work, WSL→Windows 127.0.0.1 does not, so [C→G] scenarios structurally fail with the Gleam peer in WSL (verified this session).
- **Fix path (engineer/admin)**: install Windows Erlang/OTP 25 side-by-side and prepend its `bin` for suite runs. Until then, Section I zero-regression on this host is measured against the 12/18 host baseline, and full 18/18 verification is delegated to the recorded-green environment.

## Zero-regression rule for 064 checkpoints

Every checkpoint compares against THIS table. Any suite count decrease = STOP. Section I compares against 12/18 host-local (and 18/18 must be re-verified before ship on an OTP-25 environment or after the Windows OTP 25 install).

## Checkpoint T019 (US2) + T036 (US5) — 2026-08-03

link 171 · il_codec 64 · engine_host 73 · wire_registry 6 · split_protocol 46 · gleam 591. All >= baseline+new; zero regression.
Flake note: one glp_link failure appeared ONLY with the Gleam suite running concurrently in WSL (WSL2 shares localhost; both suites bind TCP ports); 4/4 green serial. Rule: run C# and Gleam suites serially on this host; recorded for the GEPA/tooling backlog.

## Checkpoint T032 (US4) — 2026-08-03

link 171 · il_codec 64 · engine_host 73 · wire_registry 6 · split_protocol 46 · gleam 618 (serial runs). Zero regression.
US4 delivered: FE/BE split (T026-T028 + two-OS-process smoke), embed surface (T030), cross-runtime smoke (T029, binding-render divergence recorded), 059 sweep (T031: 16 discharged, 23 deferrals, 5 engineer flags in close-out-064.md).

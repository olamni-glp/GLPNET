# Full wire-protocol SPIN model — RESULT

**Status**: ✅ **PASS** — recorded against **real SPIN 6.5.1** on 2026-07-29 (feature 061, T015).
This discharges the FR-040 SPIN obligation and **DEF-A3** (the full-protocol model the 027
HANDSHAKE-1 spike deferred to the wire seeds). Desk research does not satisfy this; an executed
real-tool model-check does.

## What was verified

`models/spin/wire_protocol.pml` — the complete client↔engine protocol of
`specs/061-wave-2-consolidated-repl-engine-split-spine/contracts/wire-protocol.md`: all six
request kinds (LOAD_SOURCE, RUN_GOAL, SNAPSHOT, STATUS, SHUTDOWN, PING) plus a malformed/unknown
kind, all five response kinds (RESULT, ACK, DEFERRED, PROTOCOL_ERROR, ENGINE_BUSY), the
restore window (wire rule 4: only STATUS/PING served, ENGINE_BUSY for the rest, nondeterministic
restore completion), deferred-snapshot parking and quiescence completion (wire rule 5), and
graceful shutdown with the final snapshot subsuming a parked one (wire rule 6). One client, one
engine, depth-1 channels (FR-002).

| # | Check | SPIN configuration | Named property | Verdict |
|---|---|---|---|---|
| 1 | Liveness/progress | claim + fairness (`./pan -a -f -N request_eventually_answered`) | `request_eventually_answered` = `[] (awaiting -> <> !awaiting)` | **errors: 0** (5610 states) |
| 2 | Liveness/progress | claim + fairness (`./pan -a -f -N deferred_snapshot_eventually_completes`) | `deferred_snapshot_eventually_completes` = `[] (pending -> <> !pending)` | **errors: 0** (5569 states) |
| 3 | Safety / deadlock | LTL lines removed → invalid-end-states + assertions ENABLED (`./pan`) | deadlock-freedom; no unspecified receptions (`xs`/`xr`) | **errors: 0** (4037 states) |

All runs are FULL statespace searches; **0 unreached states** in both proctypes on every run
(28/28 client states, 47/47 engine states) — every protocol transition modelled is exercised.

> Run 3 removes the `ltl` lines because an active never claim disables SPIN's invalid-end-state
> detection. The committed `wire_protocol.pml` is unchanged; `run.sh` derives the claim-free
> variant in a temp dir (same discipline as the 027 spike).

## Modelling notes

- The engine's single `req ? k` receive sits at the end-labelled `do`, so an idle serving
  engine is a VALID end state; the kind × state dispatch `if` is exhaustive, which is exactly
  wire rule 2 (every request gets one terminal response) in structural form.
- `xs`/`xr` exclusive-ownership assertions are on both channels (client sole sender / engine
  sole receiver of `req`, converse for `resp`) — SPIN's POR validity check makes channel polls
  incompatible with these assertions, which forced (and improved) the single-receive shape.
- The client's request budget (`MAXREQ 5`) bounds the statespace; the engine loop itself is
  session-unbounded.
- Crash/restore-consistency is deliberately out of scope here — that property class belongs to
  the TLA+ model (`models/tla/`, T035); timed supervision bounds belong to UPPAAL
  (`models/uppaal/`, T030).

## No LM on the verification path (Constitution V)

SPIN is a deterministic local model checker — model in, verdict/counterexample out. No language
model participates.

## Reproduction

- Canonical (WSL2): `models/spin/run.sh` → three verifier runs, asserts `errors: 0` on each.
- Windows wrapper: `models/spin/run.ps1` → forwards to `run.sh` via `wsl.exe -d Ubuntu`.
- Tool versions: `models/spin/tool-versions.txt` — SPIN 6.5.1, gcc 15.2.0, WSL2 Ubuntu.

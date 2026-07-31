# Crash/restore/resume TLA+ model — RESULT

**Status**: ✅ **PASS** — recorded against **real TLC 2.19** (OpenJDK 25, WSL2) on 2026-07-30
(feature 061, T035). This discharges the FR-040 TLA+ obligation: at-most-once committed-stream
consistency (FR-032/SC-002) checked over **all crash points** of the crash/restore/resume state
machine. Desk research does not satisfy this; an executed real-tool model-check does.

## What was verified

`models/tla/CrashRestore.tla` — the US4 state machine: an engine producing a value stream over
an established link (bind ⇒ ship as ONE atomic runner-thread step, because
`LinkEgress.ShipGround` runs synchronously inside the bind's `OnBind` callback),
quiescence-gated snapshots (a between-steps copy of the bound chain), a crash allowed between
ANY two steps (bounded budget), and supervised restore that reloads the last complete snapshot
and re-arms egress at the first UNSHIPPED position (`RewireHandle.ResumeEgress`'s
shipped-count walk — the count, persisted in section 0x09, is what makes "unshipped"
well-defined even when a pre-snapshot bind's synchronous ship threw).
Values carry the driver's monotone issue order; each is issued exactly once (the no-replay
crash boundary — in-flight-request replay stays deferred, DEF-X3).

| # | Run | Config | Checked | Verdict |
|---|---|---|---|---|
| 1 | Implemented semantics | `pass.cfg` (MaxVals=4, MaxCrashes=2, sync ship, rearm-past-committed) | invariants `NoDup` + `Ordered` + `TypeOK`; temporal `NoCommittedLoss` + `EventuallyAllObserved` (5 liveness branches) | **No error** — complete statespace, 242 distinct states, depth 11 |
| 2 | Negative control: restore re-ships the committed chain (`RearmAtZero=TRUE` — what `heap.OnBind` on pre-bound cells would do without the unshipped-tail walk) | `dup.cfg` | `NoDup` | **Violation found** (expected) — duplication counterexample |
| 3 | Negative control: bind and ship as separate steps (`AsyncShip=TRUE` — an asynchronous egress) | `loss.cfg` | `NoCommittedLoss` | **Violation found** (expected) — committed-loss counterexample (snapshot captures a bound-but-unshipped value; the crash loses it) |

**Reading**: the SC-002 property — the peer-observable committed stream equals an uninterrupted
run's stream (no committed value lost, duplicated, or reordered) — holds across every crash
point of the implemented design, and the two design decisions it rests on are each shown
load-bearing by counterexample when negated:

1. **Egress re-arms past the committed chain** (`RewireHandle`'s walk to the first unbound
   tail): negating it (run 2) re-ships committed work — at-most-once broken.
2. **Ship is synchronous inside the bind** (`ShipGround` via `GetAwaiter().GetResult()` in the
   `OnBind` callback, so a quiescent snapshot can never capture a bound-but-unshipped value):
   negating it (run 3) loses committed work.

## Modelling notes

- The peer-observation abstraction is "handed to the transport" (FR-032's committed
  definition): a completed `SendBytesAsync` puts the frame in the OS send buffer, which
  survives a process kill (kernel delivers buffered data before FIN). Wire-level delivery
  is TCP's guarantee, outside this model.
- Snapshot is always enabled while serving, which also keeps every serving state live —
  TLC's deadlock check passes structurally.
- `MaxVals=4, MaxCrashes=2` bound the statespace; the search is FULL (0 states left on
  queue), so the verdict is exhaustive within those bounds.

## Reproduction

- `run.ps1` (Windows) → forwards to the canonical `run.sh` in WSL2.
- `run.sh` runs the three TLC configurations and asserts run 1 clean AND runs 2–3 each
  produce their expected counterexample; exit 0 = PASS.
- Tool pins in `tool-versions.txt`.

# RESULT — UPPAAL timed supervision model (061 T030, FR-040)

**Status**: BLOCKED — model + queries + harness complete; the real-tool
verdict is pending an UPPAAL license key (engineer action). NO verdict is
claimed. This file is refreshed by `run.sh` once a key is available.

## What is modelled (supervision.xml)

Timed automata for the 061 supervision loop (contracts/supervision.md), time
unit 100 ms, shipped defaults as constants: ping_interval 5 s, ping_timeout
3 s, backoff_initial 1 s, backoff_max 30 s, restore bound 10 s,
crash_threshold 3 (DEF-F2).

- **Engine**: Serving → (crash) → Down → (restart?) → Booting[≤ RESTORE] →
  up! → Serving; answers ping? with ack! only while Serving; may crash during
  Booting too (the restart-storm shape).
- **Supervisor**: pings every PI; missing ack within PT ⇒ death; classify —
  below threshold ⇒ Backoff[BI..BMAX] → restart! → WaitUp[≤ PI+PT+RESTORE];
  at threshold ⇒ absorbing Stopped (the DEF-F2 loud stop). A silent boot
  (no up within the boot window) re-enters classification.

## Properties (queries.q)

| # | Property | Meaning |
|---|---|---|
| Q1 | `A[] not deadlock` | the composed system never wedges |
| Q2 | `Engine.Down --> (Engine.Serving \|\| Supervisor.Stopped)` | no silent death: every death ends in serving again or a loud taxonomy stop |
| Q3 | `A[] (!Engine.Serving && !Supervisor.Stopped imply gdead <= BOUND)` | SC-003: per recovery cycle, detect→restart→restore completes within BOUND of the most recent death |
| Q4 | `A[] (Supervisor.Stopped imply crashes >= THRESHOLD)` | the stop fires only at the recorded threshold (FR-023) |

`BOUND = (PI + PT + RESTORE) + BMAX + RESTORE` — the worst case is a crash
DURING boot: the supervisor may sit out the remaining boot window before
detecting, then apply up to the max backoff, then wait a full restore.
**Spec-precision note (for the wave close): contracts/supervision.md states
the timing obligation as "≤ ping_interval + ping_timeout + restore(snapshot)",
which omits the backoff term the same contract mandates before the restart.
The model carries the backoff term explicitly; the contract sentence should
gain it (backoff_initial in the healthy-first-crash case).**

## Verdict

| Run | Date | Tool | Outcome |
|---|---|---|---|
| — | 2026-07-30 | verifyta 5.0.0 | **NOT RUN — license gate** ("License does not cover verifier"; key issuance requires engineer registration). 5.0.0-beta5 probed: same gate; no 4.1 build published any more (tool-versions.txt). |

## To produce the verdict

1. Obtain the free academic key (uppaal.org registration — engineer action).
2. `$env:UPPAAL_KEY = "<key>"; docs/research/repl-engine-separation/models/uppaal/run.ps1`
3. run.sh refreshes the verdict table above from the real output; exit 0 = all
   four properties satisfied.

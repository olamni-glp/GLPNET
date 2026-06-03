# Contract — Durable `equiv` step (replay-safe; 019 R3 carry-forward)

`tools/equiv/workflow.py:register()` adds an `equiv` step to the 018 durable builder, sequenced AFTER `codegen` (`durable/workflows.py`).

## Replay-safety (HARD GATE — 019 R3)
The DBOS step body is a **deterministic function of recorded inputs**:
- Input: the recorded normalized golden + candidate traces for a (tombstone_key, source) — produced earlier by the agent/CLI `capture` (where the nondeterministic REPL spawn + timing live).
- Body: apply pure `relation.py:compare(...)` → verdict; apply pure `fidelity.py` aggregation; two-phase write `dart_equivalence`.
- **No** REPL spawn, **no** wall-clock, **no** LM call, **no** network inside the step. Same inputs ⇒ same verdict ⇒ replay-safe.

## `needs_agent_work` sentinel (convspec/019 R3 pattern, verbatim)
If recorded traces are absent for a target, the step returns a typed `needs_agent_work` sentinel — it **never raises**. The `/codeconv-equiv` skill then drives `capture` (dual-REPL) and re-runs `compare`. A `divergent` verdict is a normal result (not an exception); it sets `verdict=divergent` + the `DivergenceRecord`.

## Frontier / curriculum
`readiness.py` admits a file when its dependencies are converted AND equivalent, in the subsystem curriculum order (strict subsystems first, `multiagent` last — `subsystem_curriculum.md`). The durable stage advances the equiv frontier the same way 018's builder advances codegen.

## Idempotency / resumability
Re-running `compare`/`ingest` on an already-`compared` row is a no-op unless the Dart source hash changed (then it's `stale` → recapture). DBOS workflow IDs keyed by (tombstone_key, source) so a crashed run resumes without double-writing (018 discipline).

# Contract — `codeconv equiv` CLI (deterministic, LM-free)

Auto-discovered tool subpackage `tools/equiv/`. Typer app; bare invocation = `status`. Reuses `codeconv.bridge_client` (no `D2Net.BridgeClient`); `--data-dir C:/pglite/research/glpnet`.

## Subcommands

| Command | Purpose | Durable? |
|---|---|---|
| `status` | frontier + per-subsystem fidelity rollup (≤5 s warm) | no (read) |
| `next` | next file to verify in curriculum order (deps converted+equivalent; subsystem/tier) | no (read) |
| `capture <tombstone_key> <source_path>` | run Dart (golden) + C# (candidate) REPLs, normalize both traces, write the recorded trace artifacts; sets `phase=captured` (agent/CLI layer — nondeterministic spawn lives HERE, not in the DBOS step) | no |
| `compare <tombstone_key> <source_path>` | deterministic verdict from recorded traces via `relation.py`; writes `dart_equivalence` row (two-phase); `phase=compared` | invokes durable `equiv` step |
| `ingest <tombstone_key>` | ingest recorded traces for all the file's in-scope sources + compute verdicts (batch of `compare`) | invokes durable step |
| `bytecode-diff <source_path>` | early checkpoint: C#-emitted vs Dart-emitted bytecode (FR-004) | no |
| `fidelity <tombstone_key>` | print the tiered score (calls `fidelity.py`) — identical to the GEPA metric | no (read) |
| `promote <subsystem>` | promote subsystem⇔ full trace-equivalence over its in-scope corpus (FR-014) | no (gate) |
| `mark-stale <tombstone_key>` | flip affected rows to `stale` on Dart source drift (FR-016) | no |
| `aggregate-escalations` | write `.codeconv/conversion-equiv/_escalations-report.md` | no |
| `retry <tombstone_key>` | one bounded re-verify (recapture+compare) before escalation | no |

## Exit codes
`0` ok / equivalent; `2` divergent (with divergence record on stdout JSON); `3` `needs_agent_work` (no recorded trace yet — typed sentinel, never a crash); `64` bad data-dir / filesystem guard (012). Never raises an uncaught exception on `divergent` or `needs_agent_work` — those are verdicts, not errors (DISCIPLINE §1.7).

## Output
Human-readable default; `--json` for machine use. `compare`/`ingest` emit the trace-divergence record verbatim (it is also the GEPA reflective feedback — single representation).

## Invariants
- Imports NO dspy/litellm/openai/torch (asserted by `test_equiv_no_lm_import`).
- Reads `.glp` sources in place under `programs/` — never copies (FR-006).
- All deterministic given recorded trace inputs (the verdict is reproducible).

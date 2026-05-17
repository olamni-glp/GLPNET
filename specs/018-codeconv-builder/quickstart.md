# Quickstart — codeconv-builder (018)

Prereq: `--data-dir C:/pglite/research/glpnet` on this checkout (pass
proactively per CLAUDE.md). venv: `codeconv/.venv`.

## Flow B — durable end-to-end, with kill/resume

```
codeconv --data-dir C:/pglite/research/glpnet migrate          # single head 0005 (FR-015)
codeconv --data-dir C:/pglite/research/glpnet init              # one unified workspace (016 reused)
/codeconv-builder                                               # skill: durable orchestration loop
#   → discover → depgraph order → scaffold → convspec → plan, per file in 015 topo/SCC order
#   (each (file,stage) a DBOS step; agent work spawned on NeedsAgentWork)
# kill the run anytime (Ctrl-C / reboot / bridge restart)
/codeconv-builder                                               # re-run: resumes; 0 completed files redone (SC-002)
```

## convspec (per file)

On `NeedsAgentWork`, the skill spawns the analysis sub-agent (+ a *separate*
research sub-agent only on an idiom-KB miss). Output: checked-in
`.codeconv/conversion-specs/<rel>.dart.md` (structured block + human
rationale/provenance, **no C#** — FR-023). Recurring constructs reuse
`conversion_idioms` (no re-research — FR-012/FR-024). Undecidable / conflict ⇒
escalation in `.codeconv/conversion-idioms/_escalations-report.md`
(FR-013/014), conversion blocked for that file only.

## Observe & recover

```
codeconv builder status                 # per-file state + counts, <5 s (FR-017/SC-009)
codeconv builder trace --file lib/x.dart # DBOS step history (debug/plan — D1=a)
codeconv builder retry --file lib/x.dart # one file, others undisturbed (FR-018)
codeconv builder aggregate-escalations   # single report (FR-013/014)
```

## Acceptance smoke (maps to SC-00x)

1. fresh cluster → `migrate` → one head, 0 dup/multi-head (SC-004).
2. full run over the subtree → every file processed once in dep order (SC-003).
3. kill after K/N → re-run → files 1..K not redone, final == uninterrupted
   (SC-002).
4. file with `Stream`/async + no idiom → spec cites analysis + official-doc
   research, records idiom; second file reuses idiom, not re-researched
   (SC-006/SC-007).
5. undecidable construct → escalation, 0 silent guesses (SC-008).
6. empty subtree → "nothing to convert", exit 0 (FR-020).
7. every 015/016/017 entrypoint still reachable (SC-005).

## T001 Baseline (recorded 2026-05-17, pre-Phase-2)

Per CLAUDE.md Test Protocol — recorded before any Phase-2 change.

- **Suite total**: 260 tests collected (`pytest codeconv/tests`).
- **Pure / no-bridge regression guard** (8 bridge-free files —
  `test_depgraph_algorithm`, `test_langpair_registry`,
  `test_mirror_gitignore`, `test_parse`, `test_pubspec`,
  `test_runner_registry`, `test_tombstone`, `test_walker`):
  **62 passed, 1 skipped, 0 failed in 0.87 s** — GREEN. This is the
  authoritative regression guard for Phase-2 pure-Python work
  (tombstone `_FIELD_ORDER`, `workspace.py`, `status.py`, `durable/`).
- **`@needs_bridge` tests**: green **per-test** (verified:
  `test_bridge_client.py::test_acquire_or_discover_lock_winner` →
  1 passed in 12.16 s in isolation; bridge + exact client
  `--data-dir/--port 0/--daemon` invocation write
  `<data_dir>/bridge.json` and become reachable in ~8 s). The **full
  serial suite** exhibits a **pre-existing test-harness bridge-contention
  defect** (sequential `@needs_bridge` tests do not fully tear down their
  spawned bridge/lock/port before the next spawns, causing 30 s
  `BridgeStartupTimeout` in-suite). This is **NOT a product bug and NOT
  introduced by feature 018** (015/016/017 merged green; consistent with
  memory `project_pglite_cold_init_windows.md` — bridge tests must run
  serially/isolated). Tracked as an orthogonal harness issue.
- **Phase-2 verification mode**: bridge-dependent Phase-2 tests
  (T005/T006/T017/T018) are validated **individually / in small isolated
  groups** (the proven-working mode), not via one giant serial suite.
  The migration fix T003/T004 is itself a precondition for a clean full
  bridge baseline later (Phase-7 T050).

## Harness fix (2026-05-17) — bridge-suite contention RESOLVED

The pre-existing suite-level `@needs_bridge` contention was root-caused
to `codeconv/tests/conftest.py::isolated_repo` having **no teardown**
(`return tmp_path`), leaking each test's spawned bridge node process
until accumulated orphans pushed PGLite cold-init past the 30 s
`ready_timeout` → progressive `BridgeStartupTimeout` cascade. **Fix**:
`isolated_repo` now `yield`s then calls the proven `kill_bridge(tmp_path)`
(the same per-test teardown the working `discover_repo` fixture uses) +
an operator progress-indicator hook (`[codeconv i/N] OUTCOME nodeid
(Xs)` per test). **Independently verified by two parallel agents**: 30
bridge-heavy tests run *together* → 30/30 pass, 0 `BridgeStartupTimeout`,
0 orphan accumulation; isolated cold-init measured 5.2–6.2 s (5× margin).
**Consequence**: the full `@needs_bridge` suite is now viable run
together; bridge-dependent verification (T005/T006/T011–T018, US1–US4)
is unblocked. This harness defect was pre-existing and orthogonal to
018; the fix is additive test-infra only (no product/spec change).

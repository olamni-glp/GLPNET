# Research — 049 Wave 1 (GLP policy-guard + 036 link full acceptance)

All NEEDS CLARIFICATION items from Technical Context are resolved below. R1 resolves to a
**scheduled confirmation with the language authority**, which is itself the decision (the plan
does not guess a language mechanism — DISCIPLINE §1.10/§1.14).

## R1 — Form-(a) realization mechanism (CRITICAL — gates all guard code)

**Question**: the recorded §1.14 ruling approves `satisfiable(Policy?, Reachable?)` staged as
"form (a) defined guard first, then form (b) system guard". Can form (a) be realized by the
typed-glp-manual §8 defined-guard mechanism the proposal names?

**Finding (verified against primary sources)**: **No, not as the mechanism stands.**
- typed-glp-manual §8: a defined guard is "exactly one clause with no guards and no body",
  unfolded **at compile time** by the partial evaluator.
- `glp_runtime/lib/compiler/partial_evaluator.dart` confirms: `_collectUnitClauses` admits only
  single-clause/no-guard/no-body procedures; `_transformClause` raises CompileError on
  `UnifySuspend` — "Defined guards must be fully reducible at compile time" — and on any
  guard call to a non-unit-clause procedure.
- `satisfiable/2` requires **runtime recursion** over `Targets`/`Reachable`/`Excludes` (runtime
  list data) with three-valued outcomes; in the intended use inside `forward/4`, its arguments
  are clause variables, which the PE cannot reduce at compile time ⇒ CompileError today.
- Additionally, the kernel guard set (`analyzer.dart` `_negatableGuards`/`_nonNegatableGuards`)
  is fixed; user procedures are not callable in guard position.

**Decision**: the FIRST implementation task is a **STOP-and-confirm checkpoint with Gabi**
presenting exactly this evidence and the candidate realizations; **no guard implementation,
compilation, or execution before his answer is recorded in the spec's Clarifications** (an
addendum to the 2026-07-08 ruling). Candidates to present:
1. **(a1) Compiler extension to defined guards**: allow a declared guard procedure with multiple
   guarded clauses to compile to a runtime guard evaluation reusing the existing three-valued
   goal machinery (suspension on unbound readers). Keeps the staged (a)→(b) shape; touches
   `partial_evaluator.dart`/codegen.
2. **(a2) Bounded compile-time unfolding** — REJECTED in research: policies and reachable sets
   are runtime data; no static bound exists.
3. **(a3) Re-stage to form (b) directly**: implement the system guard primitive (additive entry
   in the analyzer guard tables + three-valued evaluation in `runner.dart`), treating the
   equivalence obligation as guard-vs-C#-matcher parity plus the four worked examples. Requires
   Gabi's express re-ruling of the staging (the ruling's equivalence intent is then carried by
   FR-005 parity + FR-007 dual-run of the suite before/after any later mechanism change).

**Rationale**: FR-001's gate is the approval vehicle; the ruling approved signature + semantics +
staging, but the named §8 mechanism cannot carry the semantics — an inconsistency only the
language authority may resolve (constitution IV-a). Scheduling the confirmation first keeps the
gate airtight while Deliverable B proceeds in parallel.

**Alternatives considered**: implementing satisfiability as an ordinary body procedure feeding a
decision variable (no language change) — rejected: it abandons the approved guard surface, which
is the entire point of Deliverable A.

## R2 — Shared decision-vector set (FR-005 / SC-003 / SC-009)

**Decision**: one JSON vector file, `specs/049-wave1-guard-link-acceptance/contracts/vectors.json`
(schema in `contracts/decision-vectors.md`), consumed by BOTH sides:
- a new xUnit test `csharp/glp_crdtmsg.tests/PolicyVectorParityTests.cs` driving
  `PolicyMatcher.Evaluate` (matcher file untouched — FR-006);
- generated typed GLP test programs in `programs/tests/typed/` running the guard in the REPL
  (wired into `test/run_all_tests.sh`), executed under form (a) and re-executed after (b).
Vectors cover: the four proposal worked examples; excluded∧reachable; multiple targets with
partial reachability; waypoints present (advisory — must not affect the decision); and the
Suspend arm as `guard_only` vectors (unbound reachable) that the C# side skips by design.

**Empty-targets semantic edge (flagged for the R1 checkpoint)**: `PolicyMatcher.Evaluate`
**delivers** on `targets = []` (vacuous policy); the proposal's reading ("some T in Targets is a
member of Reachable") **fails**. FR-005 demands 100% parity, so the empty-targets vector's
expected outcome needs Gabi's ruling at the same checkpoint — parity favors Success-on-empty;
the proposal text favors Fail. Recorded, not guessed.

**Rationale**: a single vector artifact is the simplest thing that makes "100% parity" and
"(a) ≡ (b)" mechanically checkable from both runtimes.
**Alternatives considered**: duplicating vectors in each test suite — rejected (SSOT, VIII).

## R3 — Profile C sourcing + two-host run (US2/US3)

**Decision (per recorded clarification)**: delegate US2 + US3 to the **gavri** host as a
sub-feature worktree task: own branch off `049-wave1-guard-link-acceptance` (push-only-own-branch),
BEAM/quicer provisioning there (gavri's toolchain is expected to build `quicer` where this host's
MSVC absence blocked it — `gleam_quic/profile_c/README.md`), Profile C in-process conformance,
then the two-host LAN run paired with this host (server side here per 036 quickstart §7,
`--addr 192.168.0.143`, cert material distributed out-of-band per the 036 trust model, UDP port
opened). Evidence lands under `specs/049-wave1-guard-link-acceptance/evidence/gavri/` and is
pushed early and continuously. The delegation prompt is the FR-016 artifact
`gavri-task-prompt.md`; its content contract is `contracts/gavri-delegation.md`.
**Rationale**: recorded clarification; avoids faking in-process QUIC on a host that cannot build it.
**Alternatives considered**: MSVC Build Tools install here (heavyweight, duplicates gavri's
toolchain); prebuilt quicer binaries (unsupported provenance) — both rejected.

## R4 — Marathon durability verification method (US4, FR-012)

**Decision**: verify on a **real persisted run** using the installed buildkit
(`D:\bstdev\research\buildkit\.venv313\Scripts\` — Python 3.13 venv; system 3.14 breaks DBOS):
create/adopt a marathon run for this wave, drive it to ≥2 checkpointed steps, **kill the owning
process mid-flight** (taskkill of the keeper/CLI process), resume from a fresh session via the
marathon resume command, and assert: reported position == durable rows, zero re-execution of
completed checkpoints, zero lost state. Separately exercise the durable-first/commit re-drive
path (checkpoint written durable-first, scoped commit withheld, resume completes the commit
without duplicating the step). Capture commands + outputs to `evidence/marathon/`.
**Rationale**: the originally-cited run `mrun-15d7dd0ffbc2` was never persisted (036 brief);
only a real run closes T003+T036. The harness is verified, **not modified**.
**Alternatives considered**: unit-test-level simulation — rejected (the deferral was exactly
about a real run).

## R5 — Carried codexreview fixes (FR-015)

**Decision**: four point fixes, each with a regression test, tested at their existing suites:
- **#3** `csharp/glp_quick_host/Program.cs`: on unregister, remove from `_byId` only if
  `_byId[id] == link` (duplicate `endpoint_id` must not evict a still-connected sibling). xUnit.
- **#5** `glp_quick/demo.py:79`: a `None` recv on handshake timeout records `SC-001 FAIL`
  instead of raising AttributeError on `.sender`. pytest.
- **#6** `glp_quick/stacks/csharp.py` `spawn_handle`: attach the stdout reader before the
  readiness wait so the pipe cannot fill pre-readiness. pytest.
- **#7** `gleam_quic/src/glpq_ffi.erl:17`: replace `{line, 1048576}` with a length-framed read so
  >1 MiB envelopes are neither split nor misrouted to stderr. Erlang-side test; toolchain not on
  PATH on this host — use the documented full paths (Gleam/Erlang installed but unlinked) or
  delegate the test run to gavri with the rest of the BEAM work.
**Rationale**: the 036 brief carries these here explicitly; all are spec'd defects, not
robustness workarounds (constitution II).

## R6 — Guard test-suite integration (FR-007 / SC-004)

**Decision**: worked-example + vector programs are typed GLP programs with `procedure`
declarations under `programs/tests/typed/`, added to `test/run_all_tests.sh` (Section A runtime;
Suspend-arm cases assert `→ suspended`, distinguished from hangs by the REPL step limit).
Baseline before any change: REPL suite at 524/525 (single pre-existing AOT-smoke failure is the
recorded baseline), C# suites green; re-run after every change (constitution VII). If a stale
REPL kernel snapshot (`glp_runtime/.dart_tool/repl.dill`) causes unexpected failures, delete and
re-run (CLAUDE.md).

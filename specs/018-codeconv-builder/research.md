# Phase 0 Research — codeconv-builder (018)

All findings respect the **D2 governing principle** (Gabi, emphatic 2026-05-17):
where DBOS offers options, the least-invasive option that preserves an existing,
working flow wins; no proven flow is obliterated by an unproven idiomatic
alternative; every flow-touching choice is justified against the working
baseline it preserves and traced to a spec FR.

Each item: **Decision / Rationale / Alternatives considered / Baseline preserved**.

---

## R1 — DBOS-activation model (resolves D1=a "how")

**Decision.** A builder run is an outer `@DBOS.workflow` that, walking the
feature-015 `codeconv.dart_depgraph` order (read-only, never recomputed),
launches one **child** `@DBOS.workflow` per file — or per SCC group as one
indivisible unit. Each child workflow's body is a fixed sequence of
`@DBOS.step`s, one step per pipeline stage (`discover` is the inventory entry
stage; then `depgraph` readiness check, `scaffold`, `convspec`, `plan`, …). A
step body **calls the existing tool entrypoint verbatim** and records the same
two-phase + tombstone projection the tool already writes. DBOS checkpoints each
step's return value; on resume DBOS **skips already-completed steps** and
re-enters the child workflow at the interrupted step — exactly the
per-(file, stage) durable unit (FR-003) and idempotent re-run (FR-004).

**Rationale.** This is the *additive* activation: the dormant
`tools/*/workflow.py::register()` no-ops are superseded by builder-side step
registration in the new `durable/` package, so each tool's behaviour is
unchanged and the activation lives in exactly one place. DBOS step-replay-skip
*is* the resumability/completability/recoverability mechanism the literal spec
word "DBOS" requires (D1=a) — not the nominal two-phase model.

**Alternatives considered.** (i) Decorate each tool's own functions with
`@DBOS.workflow` in-place — rejected: changes six proven flows, scatters DBOS
across the codebase, violates the D2 principle. (ii) One flat workflow over all
files — rejected: a single giant workflow cannot express per-file recovery
granularity or the SCC-as-one-unit edge case, and replay cost is unbounded.

**Baseline preserved.** Every `tools/*` entrypoint is called unmodified; the
existing two-phase columns + tombstone writes still happen inside the step.

---

## R2 — Builder/convspec skill-as-orchestrator (justified deviation)

**Decision.** `/codeconv-builder` carries a durable-orchestration loop and a
`NeedsAgentWork` handler; `/codeconv-convspec` carries the analysis + *separate*
research sub-agent prompt contracts. The Python tools stay pure/deterministic.

**Rationale.** Identical justified-deviation class to feature-017 planagents
(precedent set + accepted): spawning Claude sub-agents is a Claude Code harness
capability the Python CLI structurally lacks, and putting LLM calls in Python
would also poison DBOS step replay-safety (R3).

**Alternatives considered.** SDK/API in Python (secret + network +
nondeterminism + breaks DBOS replay); `claude -p` shell-out (fragile, no
provenance). Both rejected — see plan Complexity Tracking.

**Baseline preserved.** Same skill/CLI split the repo already uses for
planagents; no new pattern invented.

---

## R3 — convspec step: deterministic ingest boundary around agent work

**Decision.** The `convspec` `@DBOS.step` body is **deterministic**: it looks
up the idiom KB, then checks for an agent-produced, checked-in artifact at
`.codeconv/conversion-specs/<rel>.dart.md`. If present + structurally valid it
records state and returns (replay-safe — pure DB/file read). If absent it
raises a typed `NeedsAgentWork(file)` signal; the **skill** catches it, spawns
the convspec analysis sub-agent (and, on the agent's request and only if the
idiom KB lacks the construct, a separate research sub-agent), waits for the
checked-in artifact, then re-drives the builder. On re-drive the step finds the
artifact and completes durably.

**Rationale.** DBOS step bodies must be replay-safe; an LLM/web call inside a
step would be re-invoked on every recovery and would produce divergent output.
Moving the nondeterminism *out* of the step and making the step a pure
ingest+persist of a durable artifact is the only model that satisfies both
DBOS replay-safety and the agent-driven clarification (spec Q1) — and it reuses
the exact feature-017 artifact+escalation discipline.

**Alternatives considered.** `@DBOS.step` that calls the LLM and relies on
DBOS to memoize the first result — rejected: brittle across DBOS versions,
couples a paid nondeterministic call to recovery, untestable offline. A DBOS
"communicator/transaction" wrapping the agent — rejected: still nondeterministic
on first run, more invasive, unproven here (D2 principle).

**Baseline preserved.** Mirrors feature-017's "Python is the deterministic
state engine; the skill spawns agents; the artifact is the checked-in truth"
flow verbatim.

---

## R4 — convspec schema

**Decision.** New `codeconv.dart_convspecs` — per-file two-phase convspec
state (`path` PK FK→`dart_files`; `convspec_started_at`,
`convspec_completed_at`, `spec_path`, `open_escalation_count`,
`sha256_of_dart_at_spec_start`), structurally **parallel to feature-017's
`codeconv.dart_plans`**.

**Rationale.** Reusing the proven two-phase shape (the same shape 015's
`dart_conversions` and 017's `dart_plans` use) keeps drift detection,
idempotence and `--respec` semantics identical to the patterns already tested —
no new state model invented (D2).

**Alternatives considered.** Fold convspec state into `dart_plans` — rejected:
conflates two distinct stages, breaks 017's contract, loses per-stage recovery
granularity.

**Baseline preserved.** `dart_plans` / `dart_conversions` lifecycle copied, not
reinvented.

---

## R5 — convspec artifact format (FR-011, FR-023)

**Decision.** One checked-in markdown per file `.codeconv/conversion-specs/
<rel>.dart.md` containing (a) a **fenced structured block** (YAML/JSON,
schema'd: source facts, per-construct conversion decisions with idiom-id +
research-finding-id references, target code-unit shape, decomposed conversion
units) that the later codegen stage parses deterministically, and (b)
**embedded human-readable rationale + research provenance prose** per
non-trivial decision. **No compilable C# is emitted** (FR-023) — spec only.

**Rationale.** FR-011 demands both machine-consumable and human-reviewable;
one artifact with a structured block + prose satisfies both and is reviewable
in a PR before any code is written (FR-023). Same checked-in-artifact discipline
as feature-017's conversion-plans.

**Alternatives considered.** Separate `.json` + `.md` pair — rejected:
two-file drift risk, weaker round-trip; single artifact keeps provenance and
decisions co-located.

**Baseline preserved.** `.codeconv/conversion-plans/<rel>.dart.md` layout +
tombstone-link discipline from 017.

---

## R6 — Conversion-idiom knowledge base (FR-012, SC-007)

**Decision.** New `codeconv.conversion_idioms` (idiom-id PK, construct
signature/key, source-form, target-form, rationale, originating
research-finding-id, first-seen file, status). convspec **always looks up the
KB by construct key before any research** (FR-012/FR-024); a hit is reused
verbatim (no re-derive, no re-research); a miss triggers research, and the
resolved decision is **written back** as a new idiom so later files are
consistent and quality compounds.

**Rationale.** Directly implements FR-012/SC-007 (≥95% recurring constructs
resolved via recorded idiom) and FR-024 (offline-reproducible after first
research). DB is the runtime store; `.codeconv/conversion-idioms/` is the
checked-in round-trip export (same DB-runtime / tombstone-truth split the repo
already uses).

**Alternatives considered.** Idioms only in artifacts (no table) — rejected:
no cross-file query, no consistency enforcement, O(files) re-scan. Idioms in
tombstone YAML — rejected: tombstones are per-file; idioms are codebase-scoped.

**Baseline preserved.** DB-runtime + checked-in-export split mirrors the
tombstone model.

---

## R7 — Research provenance + caching (FR-024)

**Decision.** `codeconv.research_findings` caches every research result
(construct key, query, **official-docs-authoritative** source URL/citation,
conclusion, retrieved-at). A finding is **authoritative only if grounded in
official Dart or .NET/C# documentation**; broader web is corroboration only and
never the sole basis. Findings are referenced by id from idioms and per-file
specs; once cached, the construct is **never re-researched** (FR-012/FR-024) →
the conversion is reproducible offline after first research.

**Rationale.** Verbatim FR-024. Caching by construct key makes research a
one-time cost per construct and makes every decision auditable.

**Alternatives considered.** No cache, re-research per file — rejected:
violates FR-012/FR-024, nondeterministic, costly. Trust arbitrary web —
rejected by FR-024.

**Baseline preserved.** Provenance-logging discipline from feature-017's
research sub-agent contract.

---

## R8 — Refactor-scope boundary (D2)

**Decision.** Builder wraps existing entrypoints as steps (R1). The **only**
unifications: `workspace.py` (single accessor over the 016
`workspace_settings`/`excluded_directories`/`phase_*` tables — replacing
per-tool ad-hoc reads *by delegation*, not by changing what those tools read),
`status.py` (one per-file state enum + escalation vocabulary), and the single
linear migration chain (R3-mig). Existing tool modules keep their public
surface; their `register()` becomes "delegate to `durable/`" rather than a
no-op. **Nothing else is refactored** (FR-022/FR-016/SC-005, D2 principle).

**Rationale.** This is the minimal set that removes the genuine duplication
(three workspace notions, three status vocabularies, a broken migration graph)
without touching proven conversion logic. Anything broader risks capability
regression (SC-005) and violates the D2 governing principle.

**Alternatives considered.** "Clean re-architecture" of all six tools behind a
new internal model — rejected explicitly by Gabi (D2 emphatic): unproven,
obliterates working flows, large regression surface. Leave duplication, only
add builder — rejected: fails FR-022 (overlapping concepts must be unified) and
leaves the broken migration graph.

**Baseline preserved.** Tool entrypoints, two-phase columns, tombstone format,
015 depgraph contract — all unchanged; `workspace.py`/`status.py` are read
facades over existing tables.

---

## R9 — Builder durable state + deterministic workflow IDs (FR-004/SC-002)

**Decision.** Outer workflow id `builder:{workspace_id}:{run_started_epoch}`;
child id `file:{stable-hash(rel_path)}` or `scc:{stable-hash(sorted members)}`.
`builder run` reuses the **most recent non-terminal** outer workflow if one
exists (resume) rather than minting a new one; `builder resume` is explicit.
Child workflow ids are content-stable so re-running recovers the same child
(DBOS dedups by workflow id) → resumed run is bit-identical to an uninterrupted
run (SC-002). `builder_runs` table records run id ↔ workflow id ↔ counts for
trace join (R11).

**Rationale.** DBOS keys idempotency on workflow id; deterministic ids are the
mechanism that makes "re-running the same command resumes, does not restart"
(FR-004) literally true.

**Alternatives considered.** Random/UUID workflow ids + a side "is this a
resume?" heuristic — rejected: nondeterministic, can double-process a file,
fails SC-002.

**Baseline preserved.** No change to how files are identified (the 015
`rel_path` key); ids are derived from it.

---

## R10 — Tombstone ↔ DB divergence detection (FR-019)

**Decision.** Before processing, the builder compares each file's tombstone
YAML state keys against the durable DB state; on divergence (e.g. tombstone
says `specced` but no `dart_convspecs` row, or `sha256` mismatch) it **refuses
to proceed on that file** and escalates "stale state — rebuild required",
never silently proceeding. Tombstone `_FIELD_ORDER` is extended **append-only**
with convspec/builder-state keys *after* feature-017's keys (canonical YAML,
sorted lists, pinned order) so the 012/014/015/017 idempotence proof carries.

**Rationale.** Verbatim FR-019; append-only extension is the proven
non-breaking way to evolve the tombstone (used by 014/015/017).

**Alternatives considered.** Auto-heal divergence — rejected: silent state
mutation violates FR-019 and the no-workaround discipline.

**Baseline preserved.** Tombstone canonicalisation + append-only `_FIELD_ORDER`
discipline unchanged.

---

## R11 — Unified status + DBOS workflow-trace surface (FR-017/SC-009 + D1 trace)

**Decision.** `codeconv builder status` projects, in <5 s on a warm bridge, one
per-file state from {`not_started｜blocked_on_deps｜analysed｜specced｜
scaffolded｜converted｜escalated｜complete`} + aggregate counts, reconciled
against durable state (a single join over `dart_depgraph` + `dart_convspecs` +
`dart_plans` + `dart_conversions` + escalation counts). `codeconv builder
trace [--file R | --run ID]` exposes the **DBOS workflow/step history** read
from DBOS's own `dbos`-schema status tables (`workflow_status`,
`operation_outputs`) joined via `builder_runs` — the explicit "workflow trace
analysis for debugging / planning" half of D1=a.

**Rationale.** FR-017/SC-009 give the snapshot; Gabi's D1 trace requirement
gives the history. Reading DBOS's own tables is the supported, non-invasive way
to get trace data — no custom event log invented (D2).

**Alternatives considered.** Build a parallel custom event log — rejected:
duplicates what DBOS already persists, drift risk, violates D2.

**Baseline preserved.** Reuses 015's status-query shape; reads DBOS's native
tables rather than adding a competing store.

---

## R12 — DBOS + single-writer PGLite constraints (top analyze/clarify risk)

**Decision & mitigations.** (1) **uuid-ossp**: the vendored
`_vendor/dbos_pglite_patch._apply_pglite_compat_patch()` MUST be applied before
`dbos.launch()` (already the proven path in `codeconv migrate`); 018 reuses
that launch verbatim. (2) **Single writer**: PGLite serves one connection;
DBOS recovery threads + queue workers must not contend with the bridge. The
DBOS Queue concurrency cap is set to **1 in-process worker by default**
(configurable) so step execution is serial through the existing 012 bridge
lock — proven flow, not a new concurrency model. Parallelism for *agent* work
is in the skill layer (≤N sub-agents, 017-style), not in DBOS workers. (3)
**Cold-init ~7 s** (Windows): builder waits on the existing bridge-ready
protocol; no new timeout logic. (4) **Conductor / external orchestration
disabled**: DBOS runs embedded, sysdb = the same bridge Postgres URL used by
`setup_dbos(endpoint)` today — unchanged. (5) **Recovery**: rely on DBOS
startup `recover_pending_workflows` + explicit `builder resume`; no custom
recovery loop.

**Rationale.** Every item reuses an already-proven mechanism (the working
`setup_dbos` launch, the 012 bridge lock, the bridge-ready wait) rather than a
speculative DBOS-native alternative — directly applying the D2 governing
principle to the highest-risk surface.

**Open risk (flagged for `/speckit-analyze` + human/clarify gate).** DBOS-on-
single-writer-PGLite at 128-file scale with serial workers is **proven only at
launch, not at sustained workflow throughput**. This is the designated top
remedy candidate: analyze must decide whether the serial-worker default is
sufficient for SC-009 (<5 s status) and whether a bounded smoke benchmark is
required before US1 is accepted.

**Baseline preserved.** `setup_dbos(endpoint)` launch, bridge lock, bridge-
ready wait, vendored patch — all reused unchanged.

---

## R13 — Crash / cycle-group / mid-run-code-change semantics (edge cases)

**Decision.** (1) **SCC = one indivisible child workflow** spanning all member
files' steps; downstream is blocked until the whole group's terminal step
completes (FR-002/edge case). (2) **Crash mid-file** resumes at the interrupted
*step* (DBOS replay skips completed steps) — never a corrupt "done"; the
two-phase `*_completed_at` is only written by the step's terminal action so a
half-step is observably incomplete. (3) **Code change mid-run**: DBOS replays a
recovered workflow against the *new* code; the contract is "completed steps are
not re-run; not-yet-run steps run new code"; the builder records the code
version in `builder_runs` so a behaviour-changing edit is visible in trace and
the operator can choose `--restart-run` (explicit, non-default) rather than
resume. Deterministic, documented — not undefined behaviour (edge case).

**Rationale.** Each is the DBOS-native semantic plus the existing two-phase
write-ordering; nothing invented.

**Alternatives considered.** Auto-restart on any code change — rejected:
discards completed durable work; violates FR-004/SC-002.

**Baseline preserved.** Two-phase terminal-write ordering from 015/017;
SCC-as-unit from 015 depgraph.

---

**All template NEEDS CLARIFICATION are closed.** The single genuine
planning-phase open question is R12's sustained-throughput risk — resolved with
a proven-mechanism mitigation and **explicitly handed to `/speckit-analyze` and
the human/clarify gate** as the top item, per the user's chained workflow.

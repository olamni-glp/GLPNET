# Phase 0 Research: Semantic Tombstone Enrichment

**Feature**: `035-semantic-tombstone-enrichment` | **Date**: 2026-06-25
**Input**: `spec.md` (15 FRs, 8 SCs, 2 clarifications resolved)

This document resolves every NEEDS CLARIFICATION from the Technical Context by
grounding each decision in the verified codeconv source (file:line cited). The
guiding rule (refinement guidance): *prefer the simplest design that satisfies
the spec; call out constraints and rejected alternatives explicitly.*

---

## R-001 — Tool shape: a new auto-discovered `tools/enrich/` subpackage

**Decision.** Ship enrichment as a new subpackage
`codeconv/src/codeconv/tools/enrich/` exporting `app: typer.Typer` (and a no-op
`register_workflows(dbos_app)`), with a `run` subcommand. No edits to
`runner.py` / `cli.py`.

**Rationale.** `tool_registry()` (`codeconv/src/codeconv/runner.py:85-133`)
auto-discovers subpackages via `pkgutil.iter_modules` over
`codeconv.tools.__path__`; the only hard contract is "export `app:
typer.Typer`" (runner.py:1-13, the FR-016 zero-edit registry). `discover` and
`depgraph` are the templates (`tools/discover/__init__.py:24-28`,
`tools/depgraph/__init__.py:39-44`). This satisfies the spec Assumption
"Enrichment is a new auto-discovered codeconv tool subpackage; adding it
requires no edits to the runner/CLI."

**Alternatives rejected.** (a) Extend `discover` with an `--enrich` flag —
rejected: conflates mechanical inventory with LM inference, violates the
single-responsibility split the spec draws between discover (mechanical) and
this tool (semantic). (b) A standalone script outside the registry — rejected:
loses bridge/runner coordination and CLI uniformity.

---

## R-002 — FR-008 preservation: discover already skips unchanged files; the real work is the re-write path

**Finding.** `discover._process_one_file` has an idempotence short-circuit
(`tools/discover/workflow.py:512-519`): it reads the existing `dart_files`
row's `sha256`; if it equals the current file hash, it `return "skipped"`
**before any DB or tombstone write**. So on the normal incremental path,
discover never re-seeds an unchanged file — an `inferred` value is already
safe there.

**Where clobbering can still happen.** discover *does* write (overwriting
`purpose`/`key_idea` with the mechanical seed at workflow.py:527-528,547-569)
when EITHER (a) the `dart_files` row is absent (inventory was rebuilt via
`codeconv discover` — CLAUDE.md notes inventory is regenerable, the markdown
tombstone is the durable record), OR (b) `sha256` differs (the file changed).

**Decision.** FR-008 = make discover's *re-write* path provenance-aware:

- Read the existing **tombstone's** `purpose_source` / `key_idea_source` and
  its recorded `sha256` before seeding.
- **Case (a) — row absent, tombstone present, `*_source: inferred`, tombstone
  `sha256` == current file hash:** restore the inferred `purpose`/`key_idea`
  (and `*_source: inferred`) from the tombstone into the new DB row and re-emit
  them in the tombstone. (This is the literal FR-008 case: "preserve an
  inferred value when re-run on a file whose `sha256` is unchanged.")
- **Case (b) — `sha256` differs:** the file changed; any prior inference is
  stale → seed mechanically and reset provenance (a leading doc-comment ⇒
  `doc`, else `absent`). Enrichment re-infers on its next run.
- The mechanical seed step also **sets** `purpose_source`/`key_idea_source`
  going forward (`doc` when `extract_leading_doc` returns non-empty, else
  `absent`).

**Rationale.** Keys on the recorded content checksum exactly as FR-007/FR-008
require; reuses the existing append-only round-trip machinery
(`merge_preserving_feature015` / `_PRESERVED_APPENDED_KEYS`,
`tools/discover/tombstone.py:165-177`) which already proves the cross-feature
"carry forward keys I did not author" discipline.

**Alternatives rejected.** (a) Add `purpose`/`key_idea` to
`_PRESERVED_APPENDED_KEYS` for *unconditional* carry-forward — rejected: would
preserve a stale inferred value across a real source change (violates FR-007
re-inference). The preservation must be conditional on `*_source == inferred`
AND unchanged sha256. (b) Do nothing in discover and rely only on the
idempotence skip — rejected: leaves the DB-rebuilt case (a) clobbering inferred
values, breaking SC-003 (`discover` re-run preserves 100% of inferred values).

---

## R-003 — Provenance representation: append-only frontmatter keys + DB columns

**Decision.** Add exactly two provenance fields, each ∈ {`doc`, `inferred`,
`absent`}:

- **Frontmatter:** append `purpose_source`, `key_idea_source` to the END of
  `_FIELD_ORDER` (`tools/discover/tombstone.py:31-76`) as the "feature-035
  appended fields", and add them to `_PRESERVED_APPENDED_KEYS` (so they
  round-trip through any discover re-write). Appending (never interleaving)
  keeps every existing key's emission position byte-identical — the established
  convention used by features 015/017/018/019/020 and required by FR-004
  ("preserving the pinned frontmatter field order").
- **DB:** add `purpose_source`, `key_idea_source` columns to
  `codeconv.dart_files` via additive migration `0011` (see R-005).

`purpose`/`key_idea` remain the canonical value fields (spec clarification +
FR-005). The tombstone markdown **body** stays `= purpose`
(`write_tombstone`, tombstone.py:203-226) — an inferred purpose therefore
also appears in the body, surfacing in git diffs (FR-014).

**Alternatives rejected.** (a) A single combined `source` map / JSON blob —
rejected: not per-field, harder to diff, and the spec names two distinct keys.
(b) Sentinel prefixes in the value text (e.g. `[inferred] …`) — rejected:
pollutes the canonical value and is brittle to parse.

---

## R-004 — The Claude/Agent inference seam (mirror `codegen-opt`)

**Decision.** Define an injected, Claude-backed callable and enforce
"no-API-default" exactly as `codegen-opt` does:

```python
# tools/enrich/optimize-analog seam
InferFn = Callable[[InferRequest], InferResult]
#   InferRequest:  rel_path: str, source_text: str
#   InferResult:   purpose: str, key_idea: str, grounded: bool, reason: str
```

`run_enrich(repo_root, *, infer_fn: Optional[InferFn] = None, …)` calls
`_require_fn(infer_fn, "infer_fn")` — which raises `RuntimeError` (NOT a silent
external-API fallback) when `None`, mirroring
`tools/codegen_opt/optimize.py:100-117`. The CLI `run` command catches that
`RuntimeError` and exits 2 with the "drive me through the skill" message
(mirroring `codegen_opt/__init__.py:120-129`). A new `/codeconv-enrich` skill
injects the Claude-backed `infer_fn` (one Claude sub-agent per file, or a
bounded batch) reading the actual Dart source.

**Rationale.** This is the project-wide hard constraint (Constitution V, FR-003,
SC-004; the GEPA-no-API rule). `codegen-opt` is the ratified precedent; copying
its `_require_fn` shape makes the no-API guarantee structurally identical and
testable (SC-004: zero external LM API calls, verifiable by the absence of any
key / network-LM dependency).

**Alternatives rejected.** (a) A built-in LM backend / litellm / openai —
**forbidden** by Constitution V; "needs an external API" is a defect to delete,
not a constraint. (b) Inference inside the Python process via a local model —
out of scope and not the Claude seam discipline.

---

## R-005 — Additive migration `0011`, single linear head, exact backfill

**Decision.** Add `codeconv/src/codeconv/db/migrations/versions/0011_enrich_provenance.py`:
- `revision = "0011"`, `down_revision = "0010"` (chains off the current head,
  `0010_marathon_schema.py`; verified by the head-assertion tests).
- `upgrade()`: `ALTER TABLE codeconv.dart_files ADD COLUMN purpose_source text
  NOT NULL DEFAULT 'absent'` and the same for `key_idea_source`, then
  **backfill** existing rows:
  `UPDATE codeconv.dart_files SET purpose_source = CASE WHEN purpose = '' THEN
  'absent' ELSE 'doc' END, key_idea_source = CASE WHEN key_idea = '' THEN
  'absent' ELSE 'doc' END`.
- `downgrade()`: `DROP COLUMN` both (additive-reversible).
- Add `codeconv/tests/test_migration_0011_single_head.py` mirroring
  `test_migration_0010_single_head.py` (assert `heads == ["0011"]` and the
  linear chain `0011→0010→…→0001`).

**Rationale — backfill is exact.** Today `purpose`/`key_idea` are seeded
**only** mechanically: a leading doc-comment or `''`
(`tools/discover/workflow.py:527-528`). So a non-blank existing value is, by
construction, doc-derived ⇒ `doc`; a blank value ⇒ `absent`. The CASE backfill
therefore classifies every pre-existing row correctly with no inference. This
keeps Constitution VI-a satisfied (additive, idempotent, single linear head)
and SC-006 holds retroactively (every field's provenance is determinable).

**Alternatives rejected.** (a) Default both columns to `'absent'` with no
backfill — rejected: mislabels existing doc-derived rows as `absent`, breaking
SC-006 and FR-006's "don't treat doc text as inferrable". (b) A data-only
backfill script outside Alembic — rejected: violates the single-source
migration discipline (VI-a) and is not idempotent-by-construction.

---

## R-006 — Idempotence & change-awareness for enrichment itself (FR-007, SC-002)

**Decision.** A file is an **enrichment candidate** iff it is in-scope,
non-orphan, and its tombstone `purpose` and/or `key_idea` is blank. Enrichment:
- **Skips** a candidate-that-isn't (already `inferred` or `doc`) — no inference.
- **Re-infers** only when the value is blank again (which, per R-002, happens
  precisely when discover re-seeded after a real source change). Change
  detection thus keys on the recorded content checksum transitively through
  discover's blank-reset, plus a direct guard: if the tombstone's recorded
  `sha256` ≠ the current file hash (a **stale** tombstone, edge case),
  enrichment does NOT infer from stale metadata — it skips-and-warns (reconcile
  via discover first).

**Rationale.** Inference is the expensive step (spec US2 "Why this priority").
Gating on blank-ness + provenance makes a no-change re-run perform **zero**
inferences and emit a byte-identical tombstone set (SC-002): the only state
that could change is provenance, which is already written on the first pass.

**Alternatives rejected.** Storing a separate `enriched_at_sha256` column to
detect "source changed since enriched" — rejected as redundant: discover
already owns sha256 and re-blanks on change (R-002 case b), so the blank check
is sufficient; the stale-tombstone guard covers the residual case without new
state.

---

## R-007 — Grounding / anti-fabrication (FR-009, SC-008) and fault isolation (FR-010)

**Decision.**
- The `infer_fn` contract requires the seam to return `grounded: bool` and a
  bounded-length `purpose`/`key_idea`. The tool **rejects** a result with
  `grounded == False` (or empty/over-long text): the file is recorded as
  `low_confidence` (outcome), its tombstone left **unchanged**, and the reason
  is reported. The tool itself does not "verify" grounding semantically — the
  Claude sub-agent is instructed to ground its description in the actual source
  and to self-signal low confidence rather than fabricate (the spec edge case
  "must not fabricate behavior").
- **Fault isolation (FR-010):** any exception from `infer_fn`, or seam
  unavailability (the `_require_fn` RuntimeError is a *startup* condition; a
  per-file failure is caught per-file), leaves that file's tombstone +
  `dart_files` row **unchanged** and appends a failure entry to the run report.
  Other candidates still process. Writes are per-file and atomic (one
  `engine.begin()` transaction per file, the discover pattern).

**Rationale.** SC-007 (prior tombstone preserved on failure in 100% of cases)
and SC-008 (≥90% sampled-accurate, source-grounded) demand both a
non-corrupting failure path and an explicit low-confidence outcome instead of a
guess. Per-file transactions mirror `discover`'s atomic-per-file write.

**Alternatives rejected.** Best-effort writing of partial/low-confidence text —
rejected: violates SC-001's "none left silently blank" *intent* (a reason is
recorded) and SC-008 (no fabricated behavior). A blank-with-reason outcome is
honest; a fabricated fill is not.

---

## R-008 — Markdown ⇔ DB provenance agreement for pre-existing doc'd files (FR-004 scope)

**Tension.** Migration `0011` backfills provenance in the **DB** for all
existing rows. Existing **doc'd** tombstones (non-candidates) carry no
provenance key in their markdown yet, and FR-006 forbids enrichment from
rewriting their `purpose`/`key_idea`.

**Decision (recommended).** Enrichment's single scan stamps
`purpose_source`/`key_idea_source` into **every in-scope tombstone it reads**,
derived from the DB/blank-ness, WITHOUT touching `purpose`/`key_idea` text on
non-candidates (adding a provenance *key* is not "overwriting a value", so FR-006
holds). Candidates additionally get inferred values. Net effect: after the
first enrichment run, markdown and DB agree on provenance for the whole in-scope
set (FR-004 "kept in agreement"), and subsequent runs are byte-identical
(SC-002). This is a one-time, inference-free reconciliation folded into the pass
the tool already makes.

**Alternative (documented, not chosen).** Scope enrichment writes to candidates
only and let doc'd-file markdown provenance be filled lazily the next time
discover rewrites them. Simpler tool, but leaves a window where DB says `doc`
and markdown is silent — a soft FR-004 tension. **Flagged for `/bk-analyze` to
confirm** which reading of FR-004's "in agreement" governs; the recommended
single-pass stamping removes the tension at trivial cost.

---

## Resolved Technical Context unknowns

| Unknown | Resolution |
|---|---|
| Language/Version | Python ≥3.11 (codeconv harness; `from __future__ import annotations`) |
| Primary deps | Typer (CLI), SQLAlchemy + `codeconv.bridge_client`/`db.engine`, PyYAML (tombstone frontmatter), Alembic (migration); **no** openai/litellm |
| Storage | `.pgdb` PGLite cluster (`codeconv.dart_files`) + `.codeconv/tombstones/<rel>.dart.md` files |
| Testing | pytest under `codeconv/tests/`, `@needs_bridge` + `run_codeconv()` integration helpers; `codeconv/.venv/Scripts/python.exe -m pytest` |
| LM seam | Injected `InferFn` (Claude sub-agent), `_require_fn` no-API-default (R-004) |
| Migration head | currently `0010`; this feature adds `0011` (R-005) |
| Scope | discovered set under `glp_runtime_net/`, Dart→C# pair, excluding `.orphaned/` |

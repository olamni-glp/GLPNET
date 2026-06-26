# Feature Specification: Semantic Tombstone Enrichment

**Feature Branch**: `035-semantic-tombstone-enrichment`
**Created**: 2026-06-25
**Status**: Draft
**Input**: User description: "Semantic tombstone enrichment — infer purpose and key_idea for blank-doc Dart tombstones via the Claude/Agent seam"

## Context

The `codeconv discover` tool records one tombstone (`.codeconv/tombstones/<rel>.dart.md`)
per discovered Dart file under the conversion subtree. Two of the tombstone's
fields — `purpose` and `key_idea` — are today seeded **mechanically**: discover
copies the file's leading documentation-comment block verbatim if one exists,
otherwise it leaves both fields blank (`''`). This was a deliberate first-pass
decision (`specs/012-codeconv-runner/spec.md:33`: *"Mechanical-only on first
pass … Semantic enrichment (LLM-backed inference of purpose / algorithm) is out
of scope for `/codeconv-discover` and reserved for a future codeconv-* tool."*).
This feature **is that future tool**.

Two consequences of the mechanical seeding motivate the work:

1. **Blank-doc files carry no semantics.** Many Dart files have no leading
   doc-comment (e.g. `lib/compiler/codegen.dart` → `purpose: ''`, `key_idea: ''`).
   Their tombstones therefore contribute nothing to downstream reasoning.
2. **`key_idea` is currently a duplicate of `purpose`.** Discover sets
   `key_idea = purpose` from the same verbatim comment, so even doc'd files do not
   distinguish *what the file is for* from *how it works*.

The audience for enriched tombstones is the codeconv pipeline — the `convspec`
and `planagents` stages that consume tombstone metadata when scoping conversion
work — and the human reviewer who reads tombstones in git diffs. Today neither
`convspec` nor `planagents` actively reasons over `purpose`/`key_idea` (they only
carry the fields forward); richer, trustworthy semantics is the precondition that
makes consuming them worthwhile.

**Hard constraint (project-wide).** All language-model inference in codeconv runs
through the **Claude/Agent seam** — an injected, Claude-backed callable, the same
discipline `codeconv codegen-opt` already follows. There is **no `OPENAI_API_KEY`,
no litellm, no openai** anywhere on this path (the GEPA-no-API rule).

## Clarifications

### Session 2026-06-25

- Q: How are inferred `purpose`/`key_idea` values marked, and how do they survive a
  later `discover` re-run that re-seeds those fields mechanically? → A: Add per-field
  provenance keys `purpose_source` / `key_idea_source` ∈ {`doc`, `inferred`, `absent`}
  to the tombstone frontmatter (and the `dart_files` record). `purpose`/`key_idea`
  remain the canonical fields. `discover` MUST preserve an `inferred` value (and its
  `*_source: inferred`) when the file's `sha256` is unchanged, rather than resetting
  it to blank.
- Q: For inferred files, should `key_idea` be produced as a value distinct from
  `purpose` (today it is a verbatim copy)? → A: Yes — `purpose` = the file's
  responsibility/role, `key_idea` = its central algorithm/mechanism, produced as
  genuinely different text. Applies to inferred files only; existing
  doc-comment-derived copies are left unchanged (blank-only scope, FR-006).

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Fill blank tombstones with inferred semantics (Priority: P1)

A codeconv operator has already run `discover`, leaving a set of tombstones whose
`purpose` and `key_idea` are blank because the underlying Dart files have no
leading doc-comment. The operator runs the enrichment tool. For each such file,
the system reads the actual Dart source and infers (a) a concise **purpose** — the
file's responsibility/role — and (b) a distinct **key_idea** — the file's central
algorithm, mechanism, or data structure. Both inferred values are written back to
the file's tombstone and the matching `dart_files` record, and are marked as
inferred so they are not mistaken for authored documentation.

**Why this priority**: This is the feature's reason to exist — turning empty
metadata into meaningful, source-grounded descriptions for the files that have
none. It is independently valuable and demonstrable on its own.

**Independent Test**: Pick a known blank-doc file (e.g. `lib/compiler/codegen.dart`),
run enrichment, and confirm its tombstone gains a non-blank `purpose` and a
distinct, source-grounded `key_idea`, both flagged as inferred, with the file's
checksum unchanged.

**Acceptance Scenarios**:

1. **Given** a tombstone with blank `purpose` and `key_idea` for an existing
   in-scope Dart file, **When** enrichment runs, **Then** the tombstone's `purpose`
   and `key_idea` become non-blank, each describes behavior actually present in the
   source, and `key_idea` is not a verbatim copy of `purpose`.
2. **Given** a tombstone whose `purpose`/`key_idea` were already populated from a
   real doc-comment, **When** enrichment runs, **Then** those values are left
   unchanged (enrichment does not overwrite authored or doc-comment-derived text).
3. **Given** the enrichment run completes, **When** the operator inspects a tombstone
   it filled, **Then** the tombstone records that the values were inferred (not
   authored), distinguishable from doc-comment-derived values.

---

### User Story 2 — Idempotent, change-aware, non-clobbering re-runs (Priority: P2)

The operator re-runs enrichment, possibly interleaved with further `discover`
runs. Files that are unchanged and already enriched are skipped (no new
inference). A file whose source content changed since it was enriched is
re-inferred. A subsequent `discover` run on an unchanged file does **not** erase
inferred values.

**Why this priority**: Inference is the expensive step; re-running the pipeline is
routine. Without change-awareness and clobber-resistance, every pipeline pass would
re-pay inference cost or silently lose enriched data, making the feature unusable in
practice.

**Independent Test**: Run enrichment twice with no source changes and confirm the
second run performs zero inferences and produces a byte-identical tombstone set;
then run `discover` and confirm the inferred values survive.

**Acceptance Scenarios**:

1. **Given** a fully-enriched tombstone set and no source changes, **When**
   enrichment runs again, **Then** zero new inferences occur and no tombstone bytes
   change.
2. **Given** an enriched file whose source content has since changed, **When**
   enrichment runs, **Then** that file is re-inferred and its tombstone updated.
3. **Given** an enriched, unchanged file, **When** `discover` is re-run, **Then**
   the inferred `purpose`/`key_idea` are preserved, not reset to blank.

---

### User Story 3 — Bounded, observable, fault-isolated runs (Priority: P3)

The operator scopes a run to a subset of files (by path) and gets a clear summary
of what happened: how many files were candidates, enriched, skipped, and failed. If
inference for one file fails (or the Agent seam is unavailable), that file's
existing tombstone is left intact and the failure is reported; other files still
get enriched.

**Why this priority**: Inference touches many files and costs real budget; the
operator needs to target work, see results, and trust that a partial failure never
corrupts existing tombstones.

**Independent Test**: Run enrichment scoped to one subdirectory with one file forced
to fail inference; confirm the scoped subset is processed, the failing file's
tombstone is unchanged, the failure is reported, and the run emits accurate counts.

**Acceptance Scenarios**:

1. **Given** a path filter, **When** enrichment runs, **Then** only candidate files
   under that path are considered and the summary counts reflect that scope.
2. **Given** a file whose inference fails, **When** the run completes, **Then** that
   file's tombstone is unchanged, the failure is listed in the run report, and the
   run still enriches the other candidates.
3. **Given** any run, **When** it completes, **Then** it emits a summary with counts
   of candidates / enriched / skipped / failed and a durable log.

---

### Edge Cases

- **File already documented**: not a candidate; left untouched (per US1 scenario 2).
- **Orphaned tombstones** (`.codeconv/tombstones/.orphaned/`): out of scope — never
  enriched.
- **Stale tombstone** (recorded checksum ≠ current file): the file changed since
  discover; enrichment must reconcile against the *current* source or skip-and-warn
  rather than infer from stale metadata.
- **Trivial / generated / empty source**: inference may not yield a confident,
  grounded description; the run must not fabricate behavior — it records a
  low-confidence/blank outcome with a reason rather than guessing.
- **Agent/Claude seam unavailable**: no inference is possible; existing tombstones
  remain intact and the run reports the condition (no silent corruption).
- **Inference returns fabricated or out-of-source content**: must be rejected /
  flagged; inferred text must be grounded in the file's actual source.
- **Concurrent codeconv activity**: enrichment reuses the existing per-repo bridge /
  runner coordination; it does not introduce a second uncoordinated PGLite consumer.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a codeconv operation that enriches tombstones
  whose `purpose` and/or `key_idea` are blank, for in-scope discovered Dart files.
- **FR-002**: For each candidate file, the system MUST infer a concise **purpose**
  (the file's responsibility/role) and a **key_idea** (its central algorithm,
  mechanism, or data structure) from the file's actual source.
- **FR-003**: All inference MUST run through the Claude/Agent seam (an injected
  Claude-backed callable), and MUST NOT depend on any external language-model API —
  no `OPENAI_API_KEY`, no litellm, no openai on the path (GEPA-no-API rule;
  precedent: `codeconv codegen-opt`).
- **FR-004**: Enriched `purpose`/`key_idea` MUST be persisted to **both** the
  tombstone `.dart.md` file (preserving the pinned frontmatter field order and
  format) and the corresponding `codeconv.dart_files` record, keeping the two in
  agreement.
- **FR-005**: The system MUST record per-field provenance via tombstone frontmatter
  keys `purpose_source` and `key_idea_source`, each ∈ {`doc`, `inferred`, `absent`},
  mirrored to the `dart_files` record, so a consumer or reviewer can distinguish
  **inferred** values from **doc-comment/authored** values and from **absent** values.
  `purpose`/`key_idea` remain the canonical value fields.
- **FR-006**: Enrichment MUST only fill blank fields; it MUST NOT overwrite a
  `purpose`/`key_idea` that was derived from a real doc-comment or authored by a
  human. (Augmenting or replacing weak/short doc-comments is out of scope for this
  feature — blank-field-only; see Assumptions.)
- **FR-007**: Enrichment MUST be idempotent and change-aware: an unchanged,
  already-enriched file MUST be skipped on re-run; a file whose source changed MUST
  be re-inferred. Change detection MUST key on the file's recorded content checksum.
- **FR-008**: `discover` MUST preserve an inferred value (and its `*_source:
  inferred`) when re-run on a file whose `sha256` is unchanged — a later mechanical
  pass MUST NOT reset inferred `purpose`/`key_idea` to blank. (Provenance-aware
  preservation inside `discover`, per FR-005.)
- **FR-009**: Inferred text MUST be grounded in the file's source (describe behavior
  actually present) and bounded in length; the system MUST NOT fabricate
  capabilities the source does not contain.
- **FR-010**: Inference failure or seam unavailability for a given file MUST leave
  that file's existing tombstone and `dart_files` record unchanged (no partial
  corruption), and MUST be surfaced in the run report.
- **FR-011**: Each run MUST emit a summary with counts of candidates, enriched,
  skipped, and failed files, and a durable run log.
- **FR-012**: The operator MUST be able to scope a run to a subset of files (e.g. by
  path); the default scope is all blank-field candidates in the discovered set,
  excluding orphaned tombstones.
- **FR-013**: Enrichment MUST operate only on the discovered conversion subtree
  (consistent with `discover`'s scope) and MUST NOT enrich orphaned tombstones.
- **FR-014**: Enriched tombstones MUST remain checked-in / git-reviewable, so
  inferred values appear in diffs for human review.
- **FR-015**: For inferred files, `key_idea` MUST be produced as a value distinct
  from `purpose` (not a verbatim copy): `purpose` conveys the file's
  responsibility/role and `key_idea` its central algorithm/mechanism. This applies to
  inferred files only; existing doc-comment-derived `purpose`/`key_idea` copies are
  left unchanged (consistent with the blank-only scope, FR-006).

### Key Entities *(include if feature involves data)*

- **Tombstone**: the per-file `.codeconv/tombstones/<rel>.dart.md` record; relevant
  fields are `path`, `name`, `purpose`, `key_idea`, `sha256` (content checksum), plus
  the provenance keys `purpose_source` / `key_idea_source` ∈ {`doc`, `inferred`,
  `absent`} introduced by this feature.
- **`dart_files` record**: the database mirror of the tombstone's `purpose`/`key_idea`,
  their `purpose_source`/`key_idea_source` provenance, and `sha256`; kept in agreement
  with the markdown tombstone.
- **Enrichment candidate**: an in-scope, non-orphan discovered file whose tombstone
  has a blank `purpose` and/or `key_idea`.
- **Inference result**: the produced `purpose` and `key_idea` for a candidate, with
  provenance = inferred and an outcome status (enriched / low-confidence / failed).
- **Agent/Claude seam**: the injected, Claude-backed inference backend (no external
  API), supplied by the driving skill — the same injection discipline as
  `codegen-opt`'s generate/propose callables.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After an enrichment run, 100% of in-scope blank-field candidates are
  either filled with non-blank inferred `purpose` and `key_idea` or explicitly
  reported with a reason (low-confidence/failed) — none left silently blank.
- **SC-002**: A re-run with no source changes performs zero new inferences and leaves
  the tombstone set byte-identical (full idempotence).
- **SC-003**: After enrichment, a `discover` re-run on unchanged files preserves 100%
  of inferred values (zero clobbered).
- **SC-004**: Zero external language-model API calls occur on the enrichment path,
  verifiable by the absence of any LM API key / network-LM dependency.
- **SC-005**: For enriched files, `key_idea` differs from `purpose` (not a verbatim
  duplicate) in at least 90% of cases.
- **SC-006**: A reviewer can determine, for any tombstone field, whether its value is
  inferred or authored/doc-derived, without inspecting source history.
- **SC-007**: When inference fails for a file, that file's prior tombstone is
  preserved unchanged in 100% of cases, and the failure appears in the run report.
- **SC-008**: On a sampled human review, at least 90% of inferred `purpose`/`key_idea`
  values are judged accurate and source-grounded (no fabricated behavior).

## Assumptions

- `discover` has already produced tombstones; enrichment consumes discover's output
  and does not re-implement file discovery or graph extraction.
- The Claude/Agent seam (a Claude-backed inference callable) is provided by the
  driving skill — one Claude sub-agent per file (or batch) — mirroring the injection
  design of `codeconv codegen-opt`; the tool itself ships no built-in LM backend.
- Scope is the existing discovered set under the conversion subtree
  (`glp_runtime_net/`), for the Dart→C# language pair, consistent with `discover`.
- The two-field intent is: `purpose` = the file's responsibility/role; `key_idea` =
  its central algorithm/mechanism — to be confirmed in clarification (FR-015).
- Active downstream consumption of enriched values by `convspec`/`planagents` is
  future work; this feature's deliverable is trustworthy, provenance-marked
  semantics in the tombstones (and DB), not new consumer logic. Today both tools
  only carry these fields forward.
- Enrichment is a new auto-discovered codeconv tool subpackage; adding it requires no
  edits to the runner/CLI (zero-edit tool registry).
- Scope is blank-field-only for this feature (FR-006): files with an existing
  doc-comment-derived or authored `purpose`/`key_idea` are not re-described.
  Improving weak/short authored docs is deferred.

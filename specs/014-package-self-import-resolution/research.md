# Phase 0 Research: 014-package-self-import-resolution

This document resolves the technical unknowns of the plan. Every section follows Decision / Rationale / Alternatives considered. The spec was clarified in Session 2026-05-11 (3 questions; see spec.md § Clarifications); items already settled there are referenced, not re-litigated.

This feature does NOT reopen feature 012's research notes. R12 (parser-level skip of `package:` / `dart:`) stays as written. The new notes R14-R16 below ADD a layered rule on top of R12; the supersession is explicitly scoped to the self-package case.

---

## R14. Self-package rewrite — layered rule on top of R12

**Decision**: At parser entry (`extract_imports`), accept a new optional argument `package_name: str | None`. When non-None and an import target matches `package:<package_name>/<rest>` exactly (case-sensitive), rewrite the target to `lib/<rest>` (POSIX, single forward slash) BEFORE the existing in-subtree relative-path resolution runs. The rewritten target then flows through the same resolve-against-subtree-root, in-subtree-membership, and dedup logic as today. Truly external `package:` targets (where `<other>` ≠ `<package_name>`) and all `dart:` / `dart-ext:` targets continue to be skipped silently per R12.

The same rewrite is applied inside `workflow._scan_outside_callers`'s inline `_IMPORT_RE` loop, so an outside-subtree file importing into the subtree via `package:glp_runtime/...` triggers an `outside_caller` warning naming both files (parity with the existing relative-path-outside-caller behaviour, FR-023 from feature 012).

**Rationale**:

- Spec FR-001/FR-002 fix the rule shape: rewrite if-and-only-if the prefix matches the subtree's own package name; everything else stays skipped. This matches Dart's own `package:` resolution rules (`package:<NAME>/X` → `<package_root>/lib/X`) per the pub specification, so the rewrite is total within Dart's own semantics.
- Layering on top of R12 (rather than replacing it) keeps feature 012's contract sealed. R12 said "skip every `package:`"; this feature refines that to "skip every `package:` whose name is not our own". The skip-truly-external behaviour is unchanged.
- The rewrite is positionally cheap — one `str.startswith(prefix)` check + one slice + reuse of the existing relative-path resolution path. No regex change to `_IMPORT_RE`. No new dedup logic (the existing `seen: set[str]` over POSIX-relative paths handles dedup-against-relative-form-of-the-same-target by construction; FR-007).

**Alternatives considered**:

- **Resolve `package:`-form targets via a Dart-side tool (`dart pub deps` / `dart analyze`)**. Rejected: pulls Dart SDK into the codeconv runtime; breaks the spec assumption that codeconv is pure-Python; trips Windows-vs-mac SDK availability concerns. The mapping is mechanical enough to do in Python.
- **Pass the rewrite ALL the way down through a pluggable resolver protocol**. Rejected: over-engineered for one rewrite rule; we can revisit if a future feature adds a second package or pub-workspace support (out-of-scope per spec line 92).
- **Special-case the rewrite only for the file-walker pass, not the outside-subtree pass**. Rejected: spec FR-006 explicitly mandates symmetry between the two passes; otherwise the warning channel under-reports outside-subtree consumers using the package form, which is exactly the failure mode US2 exists to prevent.

**Validation criterion**: SC-001 (isolated count < 20 on the live `glp_runtime_net/`), SC-002 (heap_fcp.dart tombstone shows the four expected deps), SC-005 (missing-pubspec fallback emits one warning, completes successfully). All three are checked in `quickstart.md` Flow G.

**What this supersedes from R12**: R12 step 1 ("If target starts with `package:` ... skip") becomes step 1a ("If target is `package:<package_name>/<rest>` AND `package_name` is known: rewrite") + step 1b ("Else if target starts with `package:` / `dart:` / `dart-ext:`: skip"). Steps 2-3 of R12 (relative-path resolve + in-subtree membership check) are reused unchanged. The dedup behaviour at R12's tail is unchanged.

---

## R15. pubspec.yaml caching shape — per-run, in-memory

**Decision**: A new module `codeconv/src/codeconv/tools/discover/pubspec.py` exposes one function:

```python
def read_package_name(subtree_root: Path) -> tuple[str | None, dict | None]:
    """Return (package_name, warning_or_None).

    package_name is the YAML 'name:' field if pubspec.yaml exists, parses,
    and contains a non-empty 'name'. Otherwise None.

    warning_or_None is None on the happy path; otherwise a dict
    {"kind": "pubspec_missing", "path": <posix-rel-or-abs path to expected pubspec>,
     "reason": "absent" | "unparseable" | "no_name_field"}.
    """
```

`workflow.run_discover()` calls this exactly once per invocation, immediately after computing the resolved subtree path (line 82 of workflow.py today) and BEFORE the bridge acquisition. The result is bound to local variables `package_name`, `pubspec_warning`. `package_name` is then passed positionally through `_run_normal` → `_process_one_file` → `extract_imports` and through `_scan_outside_callers`. `pubspec_warning`, if non-None, is appended to `warnings_list` exactly once (NOT per file).

**Rationale**:

- Spec FR-004 mandates "at most once per `/codeconv-discover` invocation". A per-run local variable is the simplest enforcement; no thread-safety question because `run_discover` is single-threaded today.
- A returned-tuple shape (rather than raising / using a singleton) keeps the caller in control of warning aggregation. The same shape is used for the existing duplicate-import warnings — the workflow collects all warnings into the summary's `warnings_list` and reports them at completion.
- Edge Case "pubspec modified mid-run" (spec line 57): the cached value wins for the current run; the next run picks up the new value. This is the natural consequence of "read once at entry"; no additional logic needed.

**Alternatives considered**:

- **Read on first use, cache on a class instance** (e.g. a `PubspecResolver` object). Rejected: introduces an object lifecycle for a single value; the closure-over-locals shape is simpler and as testable.
- **Read inside `parse.extract_imports` itself, with `functools.lru_cache`**. Rejected: hides the "where is the read happening" question; makes the per-run-once guarantee fragile across test runs (cache survives between tests unless explicitly cleared).
- **Eager-parse on `discover` import** (module-level). Rejected: violates spec assumption that `pubspec.yaml` may be absent; would need lazy semantics anyway.

---

## R16. Warning shape — `pubspec_missing` and the three reasons

**Decision**: When `pubspec.yaml` is absent / unparseable / lacks `name:`, the workflow emits exactly one warning of shape:

```json
{
  "kind": "pubspec_missing",
  "path": "glp_runtime_net/pubspec.yaml",
  "reason": "absent" | "unparseable" | "no_name_field"
}
```

The three `reason` values map to:

| reason          | Triggered by                                                                                        |
|-----------------|-----------------------------------------------------------------------------------------------------|
| `absent`        | `pubspec.yaml` does not exist at the expected path (`subtree_root / "pubspec.yaml"`).               |
| `unparseable`   | The file exists but `yaml.safe_load(text)` raises `yaml.YAMLError` (or returns a non-mapping).      |
| `no_name_field` | The YAML parses to a mapping but `data.get("name")` is missing, empty, or non-string.               |

`path` is the POSIX-relative path against `repo_root` (e.g. `"glp_runtime_net/pubspec.yaml"`), to match the path-shape convention used elsewhere in the discover summary (`outside_caller`'s `outside_file` / `inside_file` are also repo-relative POSIX). When the workflow is invoked with `root` outside `repo_root` (a future case not exercised today), `path` falls back to the absolute string of the resolved expected path.

**Rationale**:

- Spec Clarification 2 explicitly fixes the kind name as `"pubspec_missing"` and mandates the `path` + `reason` fields with the three reasons listed. No room for invention here.
- A single `kind` (rather than three sibling kinds) keeps consumer code's `if w["kind"] == "pubspec_missing"` simple. The `reason` field gives downstream tooling enough to diagnose without parsing free-text.
- Repo-relative POSIX path matches the precedent set by the `outside_caller` warning (workflow.py lines 500-507) — one consistent path-shape for human readers grepping the summary.

**Alternatives considered**:

- **Three sibling kinds** (`pubspec_absent`, `pubspec_unparseable`, `pubspec_no_name`). Rejected: spec Clarification 2 fixed `kind` to a single value; respect the spec.
- **Include the parse exception text in the warning**. Considered. Rejected for the spec contract: the spec only mandates `kind`/`path`/`reason`. Implementation is free to LOG the exception (via `_LOG.warning`) but the summary warning stays the spec-mandated shape — keeps the JSON output stable across PyYAML versions.

---

## R17. Idempotence under the new rewrite (FR-008 / SC-004)

**Decision**: Idempotence is preserved by construction because:

1. The per-file `(mtime, sha256)` short-circuit in `_process_one_file` (workflow.py lines 322-328) is unchanged. A file unchanged on disk is skipped; no parse, no rewrite, no DB write.
2. The first run after this feature lands will trigger reparse for every file (because `extract_imports` now returns more edges → upsert path runs → tombstones rewritten with new `dependencies` lists). The SECOND consecutive run finds every `(mtime, sha256)` matches; takes the short-circuit; produces zero diff in DB rows AND zero diff in tombstone files.
3. `_backfill_tombstone_callers` runs unconditionally per-run, but writes byte-identical YAML when the underlying graph is identical (the `write_tombstone` helper sorts + canonicalises field ordering; verified by feature 012's `test_discover_idempotence.py`).

**Validation criterion**: SC-004 (`files_skipped_idempotent == files_walked` on the second consecutive run; `files_processed == 0`). The existing `codeconv/tests/test_discover_idempotence.py` is the regression guard — must continue to pass with zero modification.

**Rationale**: Spec FR-008 explicitly requires idempotence to hold even after the post-feature tombstone refresh. The short-circuit's `sha256` check is content-based, not edge-graph-based, so the new edges don't perturb it.

**Alternatives considered**:

- **Add an "edge graph hash" to the short-circuit**. Rejected: unnecessary; the file's `sha256` already covers everything that affects `extract_imports`'s output (the import directives are inside the file). If two files cross-reference and one changes, the changed file is reparsed and its edges are recomputed — the unchanged file's outgoing edges are unaffected.

---

## R18. Performance under the new rewrite (SC-006 / SC-013 carry-forward)

**Decision**: The added per-import work is one `target.startswith("package:" + package_name + "/")` check + one slice `target[len(prefix):]`. At ~5 imports per file × 128 files = ~640 checks per discover run. Sub-millisecond aggregate cost on Windows Python 3.11; well under the 60 s / 5 s SLO bounds.

**Rationale**:

- The dominant cost in `extract_imports` today is the file read + regex match (one regex over the full file content). Adding a startswith check per match is negligible.
- The `read_package_name` call adds one `pathlib.Path.read_text` + one `yaml.safe_load` per discover run — a few milliseconds, amortised across 128 files.

**Validation criterion**: `pytest --run-perf` on `codeconv/tests/test_discover_perf.py` (existing; gates SC-013 / SC-006). Must still pass within the 60 s / 5 s budgets.

**Alternatives considered**:

- **Pre-compile the prefix string at discover entry**. Considered. Rejected as premature: `startswith` is already a single C call; pre-allocation would save microseconds. Reconsider if perf tests fail by a slim margin.

---

## R19. Out of scope (explicit non-decisions)

The following are deferred by spec and NOT resolved here:

- **Pub workspace resolution** (multiple packages in the same repo cross-referencing by `package:`). Spec line 92.
- **`package:`-form references that resolve OUTSIDE the subtree's `lib/`** (e.g. `package:glp_runtime/test/foo.dart`). Per spec edge case (line 55), only `lib/` is the package root; non-`lib/` package paths are skipped silently. Verified by Dart's own pub conventions.
- **Reopening feature 012's R12**. R12 stays as-written; this feature ADDS a layered rule.
- **Additional warning kinds beyond `pubspec_missing`**. The malformed-import-path edge case (spec line 53) is silently dropped per the spec's explicit "skip silently" instruction; no warning emitted, mirroring feature 012's relative-path-unparseable behaviour.

---

## Open questions for implementation

None. The 3 spec-side clarifications (Session 2026-05-11) plus R14-R18 above constitute the closed set.

If implementation discovers that the `pubspec.yaml` `name:` field can validly contain dashes or other characters that affect the prefix-match (e.g. `name: glp-runtime`), STOP and escalate per spec Assumptions before changing the rewrite rule's prefix-construction logic. Today's verified value is `glp_runtime` (underscore).

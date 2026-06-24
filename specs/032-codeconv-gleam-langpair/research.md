# Phase 0 Research: codeconv Gleam langpair (Dart→Gleam)

All NEEDS-CLARIFICATION from Technical Context are resolved below. Guidance
applied (baseline): *prefer the simplest design that satisfies the spec; call
out constraints and rejected alternatives explicitly.*

Authoritative inputs read: `specs/016-.../contracts/langpair_plugin_contract.md`;
`codeconv/src/codeconv/langpairs/{base,__init__}.py`;
`codeconv/src/codeconv/langpairs/dart_csharp/{__init__,source_dart,target_csharp,mirror_dart}.py`;
`codeconv/src/codeconv/tools/scaffold/{planner,workflow}.py`;
`codeconv/src/codeconv/tools/mirror/workflow.py`;
`codeconv/tests/test_langpair_registry.py`; `docs/research/gleam-atomvm/dossier.md`
(F1 GO verdict, source basis = Dart `glp_runtime/`, Gleam 1.17.0).

---

## R-001 — Source-side reuse strategy

**Decision**: `dart_gleam/source_dart.py` is a thin module that **independently
delegates** to `codeconv.tools.discover.{parse,pubspec,walker}` — the same single
source of truth that `dart_csharp/source_dart.py` delegates to — and exposes the
same five source hooks (`source_extensions`, `tool_exclusion_globs`,
`read_package_name`, `extract_imports`, `extract_leading_doc`).

**Rationale**: FR-002 requires the source side to be *identical in result* to the
existing Dart source side because Dart is the shared authoritative source for
both targets. Delegating to `tools/discover` (where the parser is proven and
tested) keeps a single source of truth (DISCIPLINE §1.3) and makes the
`extract_imports`/`extract_leading_doc` parity test trivially pass (same
underlying function). The byte-faithfulness is structural, not aspirational.

**Alternatives considered**:
- *Import `dart_csharp.source_dart` directly and reuse it.* Fewer lines, but
  couples the two production pairs — `dart_gleam` would break if `dart_csharp` is
  renamed/removed, and it muddies the "each pair package is self-contained"
  boundary the 016 contract draws. Rejected for coupling.
- *Copy the parser logic into `dart_gleam`.* Guarantees drift between two
  inventories of "the same" logic — the exact anti-pattern DISCIPLINE §1.3
  forbids. Rejected.

---

## R-002 — Gleam target conventions

**Decision**:
- **Target extension**: `.gleam` (the Gleam source file extension; dossier
  toolchain-inventory confirms Gleam 1.17.0).
- **Module-segment rule**: a legal Gleam module path segment matches
  `^[a-z][a-z0-9_]*$` and is not a Gleam reserved word. Path separators stay `/`
  (POSIX in / POSIX out, as `dart_csharp.target_for`).
- **Companion comment syntax**: `//` (Gleam line comment). Gleam has `//`
  (line), `///` (statement doc), `////` (module doc) and **no block comment** —
  so the `dart_csharp` `// TODO: …` one-line stub form is already Gleam-legal;
  only the category label changes (`Gleam source` for the `.gleam` companion).
- **Tracker filename**: `codeconv-gleam-tracker.json` (pair-defined). The C#
  pair deliberately keeps the legacy literal `d2net-tracker.json` for behavioural
  fidelity; the spec (Assumption) directs the Gleam pair to choose its own.
- **Companion set**: `(".gleam", ".ana", ".tst", ".con", ".dep", ".cgn",
  ".iss", ".sta", ".ver")` — the C# pair's nine with `.gleam` replacing `.cs`,
  order fixed for deterministic tracker records (FR-010).
- **Mirror prune set**: identical to `dart_csharp`
  (`.dart_tool, build, archive, backup, .git, .idea, .vscode`) — these prune the
  *Dart source* tree (source-side concern, identical for both targets).
- **Preserved-source suffix**: `""` (verbatim mirror) — same reason as
  `dart_csharp`: codeconv `discover` detects source by the `.dart` extension, so
  a renamed copy would yield an empty inventory / dead pipeline (016 Amendment 1,
  FR-032 Option 1).
- **`workdir_name`**: `__<basename-without-ext>` — same D2NET-parity convention
  as `dart_csharp` (a working dir per scaffolded source file).

**Rationale**: FR-004 + the spec Assumptions direct the Gleam pair to mirror the
C# pair's mirror-side artifacts with the Gleam target substituted and a
pair-defined tracker name. `//` stubs are valid Gleam; reusing the comment form
minimizes surface area while staying faithful (FR-004).

**Alternatives considered**:
- *`gleam-tracker.json` vs `codeconv-gleam-tracker.json`.* Chose the
  `codeconv-`-prefixed form to make provenance explicit (de-branded toolchain);
  either satisfies "pair-defined". Pinned in `data-model.md` so the test asserts
  the exact literal.
- *`///` doc-comment stubs.* Rejected — `///` attaches as documentation to the
  following statement and would change meaning if a real port follows; a plain
  `//` TODO is inert.

**Reserved-word list note**: the exact Gleam 1.17.0 reserved set is a
target-language fact (not a GLP-semantics question), to be pinned as a module
constant `_GLEAM_RESERVED` in `target_gleam.py` and cited to the Gleam 1.17.0
language reference. Conservative working set (verify against the 1.17.0 docs at
implement time): `as, assert, auto, case, const, delegate, derive, echo, else,
fn, if, implement, import, let, macro, opaque, panic, pub, test, todo, type,
use`. A reserved word as a segment is normalized by appending `_` (e.g.
`type` → `type_`).

---

## R-003 — Segment normalization + collision detection (LOAD-BEARING)

This is the one decision the plan does **not** resolve unilaterally. It is
surfaced to `/bk-analyze` and routed to an owner gate before `/bk-implement`,
per the Bug-Protocol (report a spec tension; never work around it).

### The facts on disk

- `scaffold/planner.py::plan_target_tree` calls `pair.target_for(src)` **once per
  source file** and appends a `PlannedFile`; it does **not** check for duplicate
  `target_rel`.
- `scaffold/workflow.py` then does `shutil.copyfile(src_abs, dst_abs)` per
  `PlannedFile`. Two sources mapping to the same `target_rel` ⇒ the second
  **silently overwrites** the first (and both tombstones point to one target).
- `mirror/workflow.py` does **not** call `target_for` at all — it mirrors the
  source dir tree verbatim and emits `{stem}{companion_ext}` in place. So Gleam
  module-path normalization is a **`target_for` / scaffold** concern only.
- `target_for` is contractually a **pure, per-file** function (016 contract
  behaviour 2; FR-009). It cannot observe cross-file collisions without becoming
  stateful, which would break per-file purity and idempotency.

### The tension (why it cannot be fully satisfied as written)

1. FR-003 AS-2: an already-legal basename is **preserved unchanged** (identity).
2. FR-008: an illegal segment is **normalized** to a legal segment.
3. (1)+(2) ⇒ **collisions are provably possible** — an illegal segment can
   normalize onto a sibling that is already that legal segment (e.g. sibling
   files `Runner.dart` and `runner.dart` both → `runner.gleam`; or `my-mod.dart`
   and `my_mod.dart` both → `my_mod.gleam`). Pigeonhole: a map that is identity
   on the legal subset and also maps illegal inputs into that same subset cannot
   be injective over the `[a-z0-9_]` alphabet without a reserved escape char,
   and reserving an escape char would break the identity guarantee (1).
4. FR-008 also requires the collision be **detected and surfaced as an error**.
   Detection is an aggregate (all target paths) check; the only aggregators are
   the scaffold stage tools.
5. FR-005/SC-003 forbid editing any stage tool ("zero stage-tool source files
   changed", verified by diff).

⇒ "detect & surface collision at runtime" (FR-008) and "zero stage-tool edits"
(FR-005/SC-003) are jointly unsatisfiable for a normalizing pair, given today's
stage architecture and the per-file pure-hook protocol.

### Decision: default + recommended alternative (owner to rule)

- **R3-a — DEFAULT carried into `tasks.md` (zero stage edit, honors FR-005/SC-003
  literally).** `target_for` does deterministic, **identity-preserving**
  normalization (legal segments untouched → FR-003 AS-2; illegal segments
  lowercased, illegal chars → `_`, leading non-`[a-z]` prefixed with `g_`,
  reserved words suffixed `_`). A **unit test** asserts the authoritative
  `glp_runtime/` Dart corpus normalizes **collision-free** (FR-008's operative
  guarantee — "never silently merged or overwritten" — is *proven* for the only
  tree downstream F3/F4 run against). The runtime "surface an error" sub-clause
  is documented as unreachable without a seam (carried as a known limitation).
  *Risk*: a future abnormal tree could collide undetected at scaffold runtime.

- **R3-b — RECOMMENDED (strongest correctness; needs a narrow SC-003 carve-out).**
  Everything in R3-a, **plus** one *generic, pair-agnostic* assertion at the end
  of `scaffold/planner.py::plan_target_tree`: if two `PlannedFile`s share a
  `target_rel` (or `workdir_rel`), raise an actionable error naming the colliding
  sources, write nothing. ~3 lines, benefits every present/future normalizing
  pair, and is not pair-specific logic. Cost: it is a diff to `tools/scaffold`,
  so the owner must amend SC-003 to "zero *pair-specific* stage edits; one
  generic target-uniqueness seam permitted." This gives the real runtime
  guarantee FR-008 asks for.

- **R3-c — rejected as heavier.** Add `LangPair.validate_target_mapping(rels)`
  to the 016 protocol and call it from the planner. Edits both a stage tool
  *and* the 016 contract; more surface for the same outcome as R3-b.

**Recommendation to owner**: adopt **R3-b** (correct + generic, small carve-out)
if the SC-003 amendment is acceptable; otherwise ship **R3-a** (zero-edit, with
the documented limitation). Either way the pigeonhole reality means FR-003 AS-2
+ FR-008's full runtime-erroring cannot both hold without *some* aggregate seam —
the spec should be reconciled to say so explicitly.

**OWNER RULING (2026-06-23): R3-b.** Implemented as `TargetCollisionError` + a
generic, pair-agnostic uniqueness check appended to
`scaffold/planner.py::plan_target_tree` (raises before any staging write, naming
both colliding sources). The default `dart_csharp` `target_for` is injective on
`.dart` inputs so the guard never fires for it (FR-006/SC-002 preserved). Both
guarantees are tested: corpus no-collision over `glp_runtime/` (221 pruned files,
0 collisions) AND planner-raises on a synthetic `Runner.dart`/`runner.dart` pair.
SC-003/FR-005/FR-008 reconciled in `spec.md` (Clarifications 2026-06-23).

### Normalization algorithm (applies under both R3-a and R3-b)

Per path segment (directory or basename stem, after stripping the source
extension and before re-appending `.gleam`):
1. If the segment already matches `^[a-z][a-z0-9_]*$` **and** is not reserved →
   emit unchanged (FR-003 AS-2).
2. Else: lowercase ASCII letters; replace every char not in `[a-z0-9_]` with `_`;
   collapse no characters (1:1 char map keeps determinism); if the result is
   empty or starts with a non-`[a-z]` char, prefix `g_`; if the result is a
   reserved word, append `_`.
3. The extension swap and verbatim directory mirroring are unchanged from
   `dart_csharp.target_for` (only the per-segment normalization is added).

Deterministic and pure (FR-009/FR-010): output depends solely on the input
string.

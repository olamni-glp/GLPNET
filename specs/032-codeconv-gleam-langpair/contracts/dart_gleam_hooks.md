# Contract: `dart_gleam` pair hooks (delta over the 016 langpair contract)

**Authoritative base**: `specs/016-codeconv-init-scaffold-langpair/contracts/langpair_plugin_contract.md`
(the `LangPair` protocol, registry surface, stage-enforcement, and Extensibility
proof). This document specifies **only** the `("dart","gleam")` realization — it
does not restate or modify the base contract. Any deviation from the base
contract is a bug (single source of truth).

## Identity & registry

- `DartGleam.key()` == `("dart", "gleam")`.
- Importing `codeconv.langpairs.dart_gleam` registers the pair (idempotent).
- `langpairs/__init__.py` `_PRODUCTION_PAIR_MODULES` gains exactly
  `"codeconv.langpairs.dart_gleam"` — the **one** edit outside the new package
  (FR-005). After this, `list_pairs()` == `[("dart","csharp"),("dart","gleam")]`
  (sorted), and `get("dart","gleam")` returns the pair.
- An unregistered request (e.g. `get("dart","rust")`) raises `UnknownLangPair`
  whose message names **both** `dart->csharp` and `dart->gleam` (FR-007).
- A workspace bound to `(dart,gleam)` with an override of a different pair raises
  `PairMismatch` (FR-007) — unchanged 016 behavior.

## Source side (FR-002 — identical-in-result to the Dart source side)

`source_extensions`, `tool_exclusion_globs`, `read_package_name`,
`extract_imports`, `extract_leading_doc` MUST return results **equal** to
`dart_csharp`'s on the same inputs (both delegate to `tools/discover`). The
parity test asserts `dart_gleam.extract_imports(...) == dart_csharp.extract_imports(...)
== discover.parse.extract_imports(...)` on a fixture.

## Target side (FR-003 / FR-008 / SC-004)

- `target_extension()` == `".gleam"`.
- `target_for(source_rel)`:
  - POSIX separators in / out; directory structure mirrored **verbatim** (no
    layout prefix — F3's concern).
  - Basename extension swapped to `.gleam`.
  - **Every path segment normalized** to a legal Gleam module segment via the
    rule in `data-model.md` (matches `^[a-z][a-z0-9_]*$`, non-reserved).
  - Already-legal segments preserved byte-identically (FR-003 AS-2).
- `workdir_name(source_rel)` == `"__" + stem` (never `None`).

**Determinism (FR-010)**: `target_for` is a pure function of `source_rel`.

**Collision (FR-008) — R-003 owner decision** (see `research.md`/`plan.md`):
default R3-a guarantees collision-freedom over the authoritative `glp_runtime/`
corpus via a unit test; recommended R3-b adds a generic uniqueness seam in the
scaffold planner (needs an SC-003 carve-out). The contract for *this pair* is:
`target_for` is identity-preserving + emits only legal segments; whether runtime
collision-erroring is in scope is the owner's R-003 ruling.

## Mirror side (FR-004)

Exact values per `data-model.md`: prune set (Dart-tree, same as C#), `""`
preserved suffix, the nine companions with `.gleam` for `.cs`, `//`-comment
stubs, tracker `"codeconv-gleam-tracker.json"`. All pure (no fs/DB).

## Extensibility proof (FR-005 / SC-003)

The feature diff MUST touch only `langpairs/dart_gleam/**`, the single
`_PRODUCTION_PAIR_MODULES` line in `langpairs/__init__.py`, the new test
`codeconv/tests/test_langpair_dart_gleam.py`, and `specs/032/**`. **Zero** edits
to `tools/{init,discover,depgraph,scaffold,mirror}` — UNLESS the owner adopts
R3-b, which permits exactly one generic uniqueness assertion in
`tools/scaffold/planner.py` (then SC-003 is restated as "zero *pair-specific*
stage edits"). `test_langpair_registry.py` (the existing suite) MUST stay green
(FR-006/SC-002 — Dart→C# unchanged).

## Test obligations (new `test_langpair_dart_gleam.py`, pure unit — no bridge)

1. Identity + extensions: `key()`, `source_extensions()==(".dart",)`,
   `target_extension()==".gleam"`.
2. Registry: `("dart","gleam")` in `list_pairs()`; `get` returns it; both pairs
   listed; `UnknownLangPair` for an absent pair names both.
3. Source parity: `extract_imports`/`extract_leading_doc`/`tool_exclusion_globs`
   equal `dart_csharp` (and `tools/discover`) on a fixture (FR-002).
4. `target_for` positive: legal paths (identity + ext swap), Windows-sep input.
5. `target_for` normalization: uppercase, leading-digit, hyphen/punctuation,
   reserved-word — each → a legal segment matching `^[a-z][a-z0-9_]*$`; legal
   basename preserved (FR-003 AS-2); output is non-reserved (SC-004).
6. Mirror hooks exact-value asserts: prune set, `""` suffix, the nine companions
   (order), `//` stub form + `Gleam source` category, tracker literal.
7. SC-003 structural proxy: `tools/{init,discover,depgraph,scaffold}` still
   import and expose `app`; discover still resolves source hooks via the registry
   (mirrors `test_langpair_registry.py`'s SC-003 asserts).
8. **Corpus no-collision (R3-a)**: over the authoritative `glp_runtime/` source
   set, `target_for` produces no two equal targets (FR-008 guarantee for the
   production corpus). *(If R3-b is adopted, additionally assert the planner
   raises on a synthetic colliding pair.)*

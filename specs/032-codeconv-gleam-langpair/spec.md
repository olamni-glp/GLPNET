# Feature Specification: codeconv Gleam langpair (Dart→Gleam)

**Feature Branch**: `032-codeconv-gleam-langpair`
**Created**: 2026-06-22
**Status**: Draft
**Input**: User description: "F2 codeconv-gleam-langpair (Dart→Gleam)."

**Epic**: Gleam AtomVM (`gleam-atomvm`) — feature F2 (roadmap #7, `refined`, WSJF 4.20), blocked-by F1 `gleam-port-source-and-toolchain-spike` (shipped `v2026.06.22.1`).
**Authoritative references**:
- Language-pair plugin contract: `specs/016-codeconv-init-scaffold-langpair/contracts/langpair_plugin_contract.md` (the `LangPair` protocol + registry + stage-enforcement rules; single source of truth).
- F1 handoff: `docs/research/gleam-atomvm/dossier.md` §6 ("Downstream handoff for F2/F3") — chosen source basis is Dart `glp_runtime/`; F2 targets the Dart→Gleam direction mirroring the existing Dart→C# pair's input.

## Clarifications

### Session 2026-06-22

- Q: Should the pair's source→target path mapping mirror the Dart structure verbatim with an extension swap (layout-agnostic, like `dart_csharp`), or emit a Gleam project layout (e.g. a `src/glp/...` prefix) itself? → A: Mirror verbatim + swap extension, layout-agnostic — the `glp_gleam/` project layout is F3's responsibility, not this pair's.

## User Scenarios & Testing *(mandatory)*

The "users" are GLP maintainers driving the codeconv conversion toolchain for the Gleam port. The feature adds Gleam as a *selectable conversion target* alongside the existing C# target — nothing more. It is the structural enabler that later epic features (F3 subtree scaffold, F4+ runtime port) build on.

### User Story 1 - Run codeconv stages targeting Gleam (Priority: P1)

A maintainer binds the conversion workspace to the Dart→Gleam pair (or passes a per-invocation override) and runs the codeconv inventory/structure stages (`discover`, `scaffold`, `mirror`) against the authoritative Dart runtime tree. The toolchain produces a Gleam-targeted mirror — one target file plus its companion tracking artifacts per non-excluded Dart source file — exactly as it already does for C#, but with Gleam target conventions.

**Why this priority**: This is the whole point of F2. Without a registered Dart→Gleam pair the toolchain refuses (UnknownLangPair) and no downstream Gleam-port feature can run the pipeline. Delivering just this story is a viable MVP: the pipeline runs end-to-end for the new target.

**Independent Test**: Bind the workspace to `(dart, gleam)`, run `scaffold` then `mirror` over a small Dart subtree, and confirm a complete Gleam-targeted output tree (target files + companions + tracker) is produced and that the default Dart→C# behavior is unaffected when the pair is not selected.

**Acceptance Scenarios**:

1. **Given** a workspace bound to source=`dart`, target=`gleam`, **When** the structure stages run over a Dart subtree, **Then** each non-excluded Dart source file yields a corresponding Gleam target file plus its companion artifacts under the mirrored directory structure.
2. **Given** no Dart→Gleam binding and no override, **When** a stage runs, **Then** the toolchain behaves exactly as before this feature (default Dart→C#), with no change in output.
3. **Given** the Dart→Gleam pair is registered, **When** the registry is queried, **Then** both `(dart, csharp)` and `(dart, gleam)` are listed.

---

### User Story 2 - Faithful Gleam target conventions (Priority: P2)

The produced target tree follows Gleam's conventions: target files carry the Gleam source extension, target paths mirror the Dart subsystem structure using Gleam-legal module path segments, companion/tracker artifacts use Gleam-appropriate comment syntax, and the per-file working-directory and tracker naming are defined for the pair.

**Why this priority**: A pipeline that emits structurally invalid Gleam paths (illegal module names, wrong extension) blocks the downstream port. Correct conventions make the F3/F4 work mechanical instead of corrective. Builds directly on US1's running pipeline.

**Independent Test**: Run the structure stages and inspect the output — every target file uses the Gleam extension, every target module path is a valid Gleam module identifier, companion stub bodies use Gleam comment syntax, and a pair-defined tracker file is present at the tree root.

**Acceptance Scenarios**:

1. **Given** a Dart source file at a mirrored path, **When** the target path is computed, **Then** it preserves the source's relative directory structure with the Gleam extension and only Gleam-legal segments.
2. **Given** a source file whose name is already Gleam-legal, **When** the target is produced, **Then** the basename is preserved unchanged apart from the extension swap.
3. **Given** the mirror stage runs, **Then** each source file's preserved copy, companion set, and the root tracker file are emitted deterministically (stable ordering).

---

### User Story 3 - Extensibility proof, zero stage-tool change (Priority: P3)

Adding the Gleam pair is confined to a new language-pair package plus a single registration line; no inventory/structure stage tool is modified. The existing test suite stays green, proving the language-pair plugin boundary holds (the contract's "Extensibility proof").

**Why this priority**: Confirms the 016 plugin architecture actually delivers pluggability and that F2 introduces no regression to the production Dart→C# path. Valuable as an architectural guarantee but the pipeline already works without explicitly asserting it.

**Independent Test**: Inspect the change set — only files under the language-pairs area and one registry registration line differ; run the full codeconv test suite and confirm it is green with the pre-existing Dart→C# behavior unchanged.

**Acceptance Scenarios**:

1. **Given** the feature change set, **When** it is diffed against the prior state, **Then** the only edits outside the new pair's package are the single registry registration line — no stage-tool source is touched.
2. **Given** the full codeconv test suite, **When** it runs after the change, **Then** all pre-existing tests pass and new tests cover the Dart→Gleam pair.

---

### Edge Cases

- **Gleam-illegal source segment**: a Dart file or directory name that is not a legal Gleam module path segment (uppercase, leading digit, hyphen/punctuation, or a Gleam reserved word such as `type`/`case`/`import`/`fn`). The target path computation MUST normalize it deterministically to a legal segment rather than emit an invalid path.
- **Normalization collision**: two distinct Dart sources that would normalize to the same Gleam target path. The toolchain MUST detect the collision and surface it (no silent overwrite / lost source).
- **Pair mismatch**: workspace bound to `(dart, gleam)` but a per-invocation override requests a different pair → refuse with the contract's mismatch error (no mixed-pair output).
- **Unregistered pair**: a request for a pair that is not registered → the actionable error must name the registered pairs (now including `(dart, gleam)`).
- **Missing/unresolvable package metadata**: a Dart subtree without resolvable package metadata behaves identically to the existing Dart source side (same warning, no crash) — the source side is unchanged Dart behavior.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The toolchain MUST register a Dart→Gleam language pair identified by `(source="dart", target="gleam")` that satisfies the existing language-pair plugin contract in full (identity + source-side + target-side + mirror-side hooks), discoverable via the registry's list and retrievable by its identity.
- **FR-002**: The pair's source-side behavior (source file extensions, tool-exclusion globs, package-name reading, import extraction, leading-doc extraction) MUST be identical in result to the existing Dart source side — Dart is the shared, authoritative source for both the C# and Gleam targets.
- **FR-003**: The pair's target file extension MUST be the Gleam source extension, and the source→target path mapping MUST mirror the Dart subsystem directory structure verbatim with only an extension swap (no Gleam project-layout prefix), while producing only Gleam-legal module path segments. Rooting the mapped output into the `glp_gleam/` project layout is out of scope (F3's responsibility).
- **FR-004**: The pair MUST define its mirror-side artifacts: directory-prune segments, the preserved-source suffix, the companion-artifact set (the codeconv per-source stage-tracking companions with the Gleam target file in place of the C# one), the companion stub-comment body in Gleam comment syntax, and a pair-defined root tracker filename.
- **FR-005**: Introducing the pair MUST be confined to a new language-pair package plus a single registry registration line; no inventory/structure stage tool (init, discover, scaffold, mirror) source may be modified ("Extensibility proof").
- **FR-006**: The pair MUST be selectable via the workspace source/target binding and/or a per-invocation override, and MUST NOT change the default workspace pair `(dart, csharp)` nor any pre-existing default behavior.
- **FR-007**: Pair-resolution refusals MUST follow the contract for the new pair: an unregistered-pair request yields the actionable "unknown pair (lists known)" error; a binding-vs-override disagreement yields the "pair mismatch — refusing mixed-pair output" error.
- **FR-008**: Source path segments that are not legal Gleam module path segments MUST be normalized deterministically to legal segments; any normalization that would map two distinct sources to the same target MUST be detected and surfaced as an error, never silently merged or overwritten.
- **FR-009**: All of the pair's hooks MUST remain pure / side-effect-free (filesystem read at most — no database, bridge, or network), so they are unit-testable without the bridge harness.
- **FR-010**: Structure-stage output for the pair MUST be deterministic (stable ordering of companions and tracker records), matching the contract's determinism guarantees.
- **FR-011**: The pre-existing codeconv test suite MUST remain green after the change, and new unit tests MUST cover the pair's target-side and mirror-side hooks plus its registry presence and selectability.

### Key Entities *(include if feature involves data)*

- **Dart→Gleam language pair**: the per-stage hook bundle binding the `(dart, gleam)` identity to the shared Dart source side and the new Gleam target/mirror sides.
- **Language-pair registry**: the process-wide map from `(source, target)` identity to a pair; gains the `(dart, gleam)` entry alongside `(dart, csharp)`.
- **Workspace pair binding**: the recorded source/target selection that the stages resolve against (default `(dart, csharp)`).
- **Target tree artifacts**: per source file — the Gleam target file, the preserved source copy, the companion tracking set, and the root tracker file.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With the workspace bound to Dart→Gleam, the structure stages run end-to-end over the authoritative Dart runtime tree producing a complete Gleam-targeted mirror — 100% of non-excluded Dart source files have a corresponding target file and companion set.
- **SC-002**: The existing Dart→C# path is unchanged — 100% of pre-existing codeconv tests pass, with no change to default-pair output.
- **SC-003**: Enabling the pair is verifiable by diff to touch only the new pair's package plus exactly one registry registration line — zero stage-tool source files changed.
- **SC-004**: 100% of produced Gleam target module paths are valid Gleam module identifiers (no illegal segments emitted).
- **SC-005**: The pair is discoverable and selectable — the registry lists both pairs, an unregistered-pair request names both, and a mismatched override is refused without producing mixed output.

## Assumptions

- **Source basis is Dart `glp_runtime/`** — per the F1 dossier GO-with-revisions verdict (the tracked C# is generated from the Dart; the Dart tree is the single authoritative source). The Gleam target therefore reuses the Dart source side unchanged.
- **Scope is the structural language pair only.** F2 does NOT create the `glp_gleam/` subtree (that is F3 `glp-gleam-subtree-scaffold`) and does NOT port or translate any runtime semantics (F4+). Actual Dart→Gleam code *content* generation is out of scope here — F2 delivers the pipeline plumbing (identity, structure mirroring, conventions, tracking), not translated Gleam code.
- **Companion set mirrors the existing Dart→C# pair**, with the Gleam target file replacing the C# one in the per-source companion set; the root tracker filename is pair-defined (the C# pair keeps a legacy literal for fidelity, so the Gleam pair chooses its own).
- **Target path policy parallels the existing Dart→C# pair** (resolved — see Clarifications 2026-06-22): mirror the Dart subsystem directory structure verbatim with an extension swap, plus Gleam-legal segment normalization (FR-003/FR-008). A Gleam project-layout prefix (e.g. placing modules under a `src/glp/...` root) is explicitly NOT baked into this pair's mapping — it is the F3 subtree-layout concern.
- **Toolchain/runtime versions** named in the dossier (Gleam 1.17.0, OTP 25.3.2.8, etc.) bear only on downstream features that build/run Gleam; F2 is pure Python and target-language-agnostic except for the Gleam naming/extension/comment rules it encodes.
- **No new language primitives or GLP semantics** are involved — this is conversion-toolchain plumbing, not a GLP language change.

# Feature Specification: /glptutorial-list — GLP tutorial browser

**Feature Branch**: `022-glptutorial-list`  
**Created**: 2026-06-03  
**Status**: Draft  
**Input**: User description: "Build /glptutorial-list — a GLP tutorial browser delivered as BOTH a Claude skill and a Python tool. It lists all available tutorials, or the scripts within a specific named tutorial, each with a brief one-line description, so an engineer/learner can choose what to run. It is the selection front-end for the companion /glptutorial-run (soft ordering: list before run). Source corpus: the GLP tutorial set where each chNN/exercise-MM = a .glp script + goals + an outcome-only golden; corpus location is TBC (sibling repo D:/bstdev/research/glp/GLP/olamni/tutorial/ vs a glpnet copy) — surface this as an open decision in the spec. Behaviour: with no argument, list every tutorial and its scripts grouped by chapter; with a tutorial name/id, list just that tutorial's scripts; each entry shows script name + a short description sourced from the script or its tutorial .md. Output is a readable terminal listing; the Python tool is the engine, the skill is the thin front-end."

## Clarifications

### Session 2026-06-03

- Q: Tutorial corpus location (FR-007) → A: Vendor a **copy** of the tutorial corpus into glpnet — the lister reads the in-repo copy; it does **not** read the sibling GLP repo in place.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browse the whole tutorial catalog (Priority: P1)

An engineer or learner wants to see what GLP tutorials exist and what each one contains, without opening directories or files. They issue the lister with no argument and get a single readable listing of every tutorial, grouped by chapter, with each script under it and a one-line description beside the script.

**Why this priority**: This is the core value and the minimum viable product — the whole point of the feature is to make the corpus discoverable at a glance so the user can decide what to run next. With only this story shipped, the feature is already useful on its own.

**Independent Test**: Run the lister with no argument against the corpus and confirm it prints every tutorial and its scripts in one grouped listing — fully testable in isolation, delivering the browse value.

**Acceptance Scenarios**:

1. **Given** a reachable tutorial corpus with multiple chapters, **When** the user runs the lister with no argument, **Then** every tutorial is shown grouped by chapter, each with its scripts listed beneath it.
2. **Given** the same corpus, **When** the listing is produced, **Then** each script line shows the script's name and a brief one-line description.
3. **Given** a tutorial chapter that contains no recognizable scripts, **When** the full listing is produced, **Then** that tutorial still appears, with an explicit empty indicator rather than being silently omitted.

---

### User Story 2 - List a single named tutorial (Priority: P2)

A user already knows roughly which tutorial they care about (e.g. a chapter) and wants to see just that tutorial's scripts, not the whole catalog. They pass a tutorial identifier and get only that tutorial's scripts with descriptions.

**Why this priority**: Narrows a large catalog to the relevant slice and feeds directly into selecting a script for `/glptutorial-run`. Valuable, but the full-catalog browse (P1) is the prerequisite experience.

**Independent Test**: Run the lister with a valid tutorial identifier and confirm only that tutorial's scripts are listed; run it with an unknown identifier and confirm a clear "no match" message plus the set of available tutorials.

**Acceptance Scenarios**:

1. **Given** a corpus containing a tutorial identified as `ch03`, **When** the user runs the lister with `ch03`, **Then** only `ch03`'s scripts and their descriptions are shown.
2. **Given** the same corpus, **When** the user passes an identifier that matches no tutorial, **Then** the lister reports that nothing matched and lists the available tutorial identifiers.

---

### User Story 3 - Descriptions informative enough to choose from (Priority: P3)

A user scanning the listing should be able to tell what each script demonstrates without opening any file. Each script's one-line description is sourced from the tutorial's own documentation (its `.md`) or the script's leading comment.

**Why this priority**: Raises the listing from a bare filename index to a genuine selection aid. The listing is still functional without rich descriptions (P1/P2), so this is a quality enhancement.

**Independent Test**: For a sample of scripts that have descriptive text in their tutorial `.md` or script header, confirm the listing shows a meaningful one-line description for each, derived without user interaction.

**Acceptance Scenarios**:

1. **Given** a script whose tutorial `.md` describes it, **When** the listing is produced, **Then** the script's line carries a concise description drawn from that `.md`.
2. **Given** a script with no description available from any source, **When** the listing is produced, **Then** the script still appears with an explicit "no description" indicator.

### Edge Cases

- **Corpus unreachable**: the configured corpus path does not exist or cannot be read → the lister reports a clear, actionable error naming the path it tried, and exits without a partial/misleading listing.
- **Empty tutorial**: a chapter directory with no scripts → shown with an explicit empty indicator (see US1 #3).
- **No description available**: a script with neither `.md` nor header text → shown with a "no description" indicator (see US3 #2).
- **Unknown identifier**: a tutorial argument matching nothing → "no match" message plus the list of available identifiers (see US2 #2).
- **Nonstandard layout**: a directory under the corpus that does not follow the `chNN/exercise-MM` convention → skipped with a warning rather than crashing or being silently absorbed.
- **Duplicate exercise numbers across chapters**: disambiguated by always grouping under their owning chapter.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: With no argument, the lister MUST enumerate every available tutorial and the scripts within each, grouped by chapter.
- **FR-002**: Given a tutorial identifier (chapter id or name), the lister MUST list only that tutorial's scripts.
- **FR-003**: For each script, the lister MUST show the script's name together with a brief one-line description.
- **FR-004**: The lister MUST derive each description without user interaction, preferring the tutorial's `.md` documentation and falling back to the script's own leading documentation when the `.md` does not describe it.
- **FR-005**: The lister MUST present output as a readable terminal listing organized as tutorial (chapter) → exercise → script → description, grouped and indented for scannability.
- **FR-006**: The lister MUST emit a clear, actionable message when the tutorial corpus cannot be located or read, and when a requested tutorial identifier matches no tutorial.
- **FR-007**: The lister MUST read the GLP tutorial corpus from a **copy vendored into the glpnet repository** — the in-repo copy is the lister's source of truth; the lister does not read the sibling GLP repo in place.
- **FR-008**: The lister MUST include a tutorial that contains no recognizable scripts in the full listing, marked with an explicit empty indicator rather than omitting it.
- **FR-009**: The capability MUST be reachable both as a `/glptutorial-list` skill and via its underlying command-line tool, and the two entry points MUST produce equivalent listings.
- **FR-010**: The lister MUST be read-only — it never executes a tutorial script (execution is the companion `/glptutorial-run` feature's responsibility).
- **FR-011**: A directory under the corpus that does not follow the recognized `chNN/exercise-MM` layout MUST be skipped with a warning, not silently absorbed into another tutorial nor allowed to abort the listing.

### Key Entities *(include if feature involves data)*

- **Tutorial**: a chapter-level grouping (e.g. `chNN`) containing one or more exercises; has an identifier, optionally a human title, and optionally descriptive text from its documentation.
- **Exercise**: an `exercise-MM` unit within a tutorial — the description anchor — composed of one or more `.glp` scripts plus their goals and an outcome-only golden, with an `ex-MM` guide (`.md`) that describes them.
- **Tutorial script**: a single `.glp` file within an exercise — the listable/runnable unit; has a name and a derivable one-line description. An exercise may contain more than one script (e.g. a composed pipeline, or a corrected/failing pair).
- **Tutorial corpus**: the in-repo copy of the tutorial collection vendored into glpnet (see FR-007); the source of truth the lister reads.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: From a single command with no argument, a user sees the complete catalog of tutorials and their scripts in one readable listing.
- **SC-002**: Every tutorial present in the corpus appears in the full listing — 100% coverage, no silent omissions.
- **SC-003**: Filtering by a valid tutorial identifier returns only that tutorial's scripts, and an invalid identifier yields a clear "no match" message with the available identifiers.
- **SC-004**: For at least 95% of scripts that have descriptive text available in their tutorial `.md` or script header, the listing shows a meaningful one-line description (meaningful = a non-empty single line that is not merely the script's filename) — so a user can choose a script to run from the listing alone, without opening any file.
- **SC-005**: The full-catalog listing for the current corpus is produced in under 3 seconds.

## Assumptions

- **Delivery split**: the command-line tool is the engine that does the work; the `/glptutorial-list` skill is a thin front-end that invokes it. Both surfaces produce the same listing (FR-009).
- **Corpus layout**: tutorials follow the `chNN/exercise-MM` convention, where each exercise is a `.glp` script plus a goals file and an outcome-only golden, consistent with the established GLP tutorial corpus.
- **Tutorial identifier matching**: a user identifies a tutorial by its chapter id (e.g. `ch03`) or chapter title, with reasonable case-insensitive / prefix matching.
- **Description precedence**: tutorial `.md` description first, then the script's leading comment, otherwise marked as having no description (FR-004).
- **Read-only scope**: this feature only lists/browses; running scripts and analyzing outcomes is the separate `/glptutorial-run` feature (soft ordering: list before run).
- **Corpus is vendored (resolved, FR-007)**: a copy of the tutorial corpus lives inside glpnet and is the lister's source of truth — a deliberate self-containment choice (no runtime dependency on the sibling GLP repo). This diverges from reading the sibling corpus in place (the feature-020 equivalence driver reads it in place per FR-006), so the vendored copy is a **snapshot** that needs a defined refresh/sync story to avoid drift from the authoritative sibling corpus. The snapshot's in-repo location and its sync mechanism are planning details for `/buildkit-plan`.

## Dependencies

- The vendored in-repo copy of the GLP tutorial corpus (per FR-007); no runtime dependency on the sibling GLP repo being present.
- Companion feature `/glptutorial-run` (this feature is its selection front-end; the two share the corpus-discovery layer). `/glptutorial-list` is independently usable and does not require `/glptutorial-run` to ship first.

# Feature Specification: glp_gleam core terms + heap + unification

**Feature Branch**: `034-glp-gleam-core-terms-and-heap`
**Created**: 2026-06-24
**Status**: Draft
**Input**: User description: "glp_gleam core terms + heap + unification"

**Epic**: Gleam AtomVM (`gleam-atomvm`) — feature F4 (roadmap rank 9, `glp-gleam-core-terms-and-heap`), blocked-by F1 `gleam-port-spike` (shipped `v2026.06.22.1`), F2 `codeconv-gleam-langpair` (shipped `v2026.06.24.1`), and F3 `glp-gleam-subtree-scaffold` (shipped `v2026.06.24.2`).

**Authoritative references**:
- **Source of truth (port basis)**: the Dart runtime `glp_runtime/lib/runtime/` — `terms.dart` (term model) and `heap_fcp.dart` (heap + binding), ratified by F1 as the single authoritative source (`docs/research/gleam-atomvm/dossier.md` §2.3, overturning the roadmap's initial C#-lean).
- **Heap semantics (normative)**: `docs/heap/heap-pointer-architecture-spec.md` v3.4 — the FCP bidirectional-pointer architecture (cell tags, writer/reader pairs, dereferencing with path compression, binding, suspension, WxW prohibition).
- **Unification & term semantics (normative)**: `docs/glp-cheat-sheet.md` (three-valued unification; writer/reader; SRSW), `docs/typed-glp-manual.md`, and `CLAUDE.md` GLP Quick Reference (Writer MGU: only binds writers, never readers, never writer-to-writer).
- **Architectural-fit / re-scope handoff**: dossier §4.1, §5 — the WAM mutable heap cannot be transliterated into Gleam; it must be re-expressed (immutable threaded store **or** process-cell heap; both proven feasible by the F1 smoke). This is a planning decision (see Assumptions), not a spec-level one.
- **Landing site**: F3's `glp_gleam/` subtree, filling the `runtime` subsystem placeholder (`glp_gleam/src/glp/runtime.gleam`), per `specs/033-glp-gleam-subtree-scaffold/spec.md` (1:1 with the Dart subsystems).

## Clarifications

### Session 2026-06-24

- Q: The roadmap brief says "parity tests vs **C#** term/unification behaviour" — but F1 ratified Dart as the source. Which is the parity baseline? → A: **The Dart source-of-truth** (`glp_runtime/`). F1's dossier §2.3 explicitly overturned the C#-lean; the tracked C# is itself a generated mirror of the Dart. The brief's "vs C#" predates and is superseded by the F1 ratification. (Resolves FR-009 / SC-005.)
- Q: Does F4 include the goal scheduler / suspension-reactivation loop, or only the heap-level suspension machinery? → A: **Only the heap-level machinery** — recording a suspension on an unbound writer and *producing* the activation list when that writer is bound. The *scheduler/runner* that consumes activations and re-runs goals is F5 (dossier §5, "F5 — bytecode runner"). This keeps F4 the "smallest runnable kernel" the brief calls for.
- Q: Are multiagent imported variables (cross-agent readers / `VariableEntry`) in scope? → A: **Out of scope** for F4. F4 is the single-runtime core kernel; imported-variable / cross-runtime support lands with the multiagent + link features (F9+, dossier §5 "F9 — link layer: RE-SCOPE"). The core term/heap/unify model must be complete and correct for the single-runtime case.

### Session 2026-06-25

- Q: At what level is "parity with the Dart source-of-truth" (FR-009 / SC-005) measured — observable outcomes only, or also internal heap layout? → A: **Observable outcomes only.** Parity is pinned to the dereferenced result, the three-valued unification verdict, and the activation set produced on binding — **NOT** internal heap representation (cell addresses, tags, layout), which legitimately differs once the WAM mutable heap is re-expressed for Gleam. (Constrains the parity corpus and test design; an internal-layout parity bar would over-constrain the re-expression and is explicitly rejected.)

## User Scenarios & Testing *(mandatory)*

The "users" are the GLP maintainers driving the Gleam port. F4 is the **first heavy port feature** and the **smallest runnable kernel**: it gives the empty-but-building `glp_gleam/` subtree (F3) its term model, its variable store (heap), and writer-MGU unification — the foundation every later feature (F5 runner, F6 compiler/loader, F7 REPL, F8 test corpus, F9 link) builds on. It ports the *data and binding core* of GLP faithfully from the Dart source-of-truth; it does **not** port the bytecode runner, the scheduler, the compiler, or the link layer.

### User Story 1 - A term model and variable store exist and bind (Priority: P1)

A maintainer constructs GLP terms (constants, compound structures, lists, and logic variables) in Gleam, allocates a fresh logic variable, binds its writer to a ground value, and dereferences it back to that value — all on the Erlang/BEAM target, with behaviour matching the Dart source.

**Why this priority**: This is the kernel the brief names ("terms … heap/store"). Without a term representation and a variable store that can allocate, dereference, and bind, there is nothing for unification, the runner, or any later feature to operate on. Delivered alone it is already a viable, demonstrable MVP: GLP data can be represented and a logic variable can be bound and read back.

**Independent Test**: From the `glp_gleam/` subtree on the pinned toolchain, run tests that (a) build each kind of term and inspect its structure, (b) allocate a fresh variable, dereference it (observed: unbound), bind its writer to a ground value, and dereference again (observed: the value) — all green on the Erlang/BEAM target.

**Acceptance Scenarios**:

1. **Given** the term model, **When** a maintainer constructs a constant (atom / integer / real / string), a compound structure (functor + ordered arguments), a list, and a variable reference, **Then** each term can be structurally inspected and compared for equality, with the same shape the Dart model produces.
2. **Given** a freshly allocated logic variable, **When** it is dereferenced before any binding, **Then** the result identifies it as an unbound variable (not a value).
3. **Given** a freshly allocated logic variable, **When** its writer is bound to a ground value and it is dereferenced, **Then** the result is that ground value; **and** a second dereference returns the same value without re-traversing the binding chain (path compression).
4. **Given** a variable reference, **When** its reader/writer role is queried, **Then** the role is determined by the cell's tag, never by address arithmetic (no `reader == writer + 1` assumption).

---

### User Story 2 - Writer-MGU three-valued unification (Priority: P2)

A maintainer unifies two terms and receives exactly one of three outcomes — **success**, **suspend**, or **fail** — with the correct heap effects: only writers are bound (never readers, never writer-to-writer), and an attempt that would require an as-yet-unbound reader suspends rather than failing.

**Why this priority**: Writer-MGU three-valued unification is the headline semantic the brief centres on ("writer-MGU three-valued unification"). It builds directly on US1's term model and heap. It is the capability that distinguishes GLP unification from textbook (two-valued) unification and is the prerequisite for the HEAD phase of the future runner (F5).

**Independent Test**: Run a truth-table suite of unification calls covering constant/constant, structure/structure (matching and mismatching functor/arity), variable/value, value/variable, and variable/variable, plus the unbound-reader case; confirm each returns the correct one of {success, suspend, fail} and leaves the heap in the expected state — matching the Dart source.

**Acceptance Scenarios**:

1. **Given** two equal ground terms, **When** they are unified, **Then** the outcome is **success** and the heap is unchanged.
2. **Given** two ground terms that differ in value, functor, or arity, **When** they are unified, **Then** the outcome is **fail**.
3. **Given** an unbound writer and a ground term, **When** they are unified, **Then** the outcome is **success** and the writer is bound to the term.
4. **Given** a unification step that requires the value of an as-yet-unbound reader, **When** it is unified, **Then** the outcome is **suspend** (not fail), and a suspension is recorded against the relevant writer.
5. **Given** any unification, **When** it binds, **Then** it binds **only a writer** — never a reader, and never a writer to another writer; an attempted writer-to-writer binding is detected and reported, never silently performed.

---

### User Story 3 - Suspension storage, activation, and Dart parity corpus (Priority: P3)

A maintainer records a suspension on an unbound writer, later binds that writer, and receives the list of activations that the (future) scheduler will reactivate; and a parity test corpus pins the ported kernel's observable behaviour to the Dart source-of-truth so later ports cannot silently drift.

**Why this priority**: The heap-level suspension machinery is what makes GLP's "suspend on unbound reader, reactivate on bind" model possible — but the part F4 owns is the *storage and activation-list production*, not the scheduler (that is F5). The parity corpus protects correctness as F5–F9 land. Both are valuable, but US1+US2 already deliver the demonstrable kernel, so this is P3.

**Independent Test**: Run tests that (a) suspend a goal-record on an unbound writer, bind the writer to a ground value, and confirm the produced activation list contains exactly the armed suspension(s); (b) bind a writer to another variable and confirm pending suspensions are forwarded to the target; (c) execute the cross-source parity corpus and confirm 100% agreement with the recorded Dart behaviour.

**Acceptance Scenarios**:

1. **Given** an unbound writer with a recorded suspension, **When** the writer is bound to a ground value, **Then** the operation returns an activation list containing that (armed) suspension, and the writer's reader pairing is preserved up to the moment of binding.
2. **Given** an unbound writer with a recorded suspension, **When** the writer is bound to another (unbound) variable, **Then** the suspension is forwarded to the target variable's writer and no activation fires yet.
3. **Given** the defined micro-scenario corpus (allocate / dereference / bind-to-value / bind-to-variable / unify / suspend-and-activate), **When** it runs against the Gleam kernel, **Then** every scenario's observable outcome matches the Dart source-of-truth's outcome for the same scenario.

---

### Edge Cases

- **Dereference of an unbound variable**: must yield "unbound variable", not a spurious value, and must not loop on the reader↔writer bidirectional pointers.
- **WxW (writer-to-writer) detection**: if a binding or a dereference would land a writer on another writer, the kernel must detect and report it loudly (the writer-MGU never binds writer-to-writer); it must never silently produce a writer→writer chain.
- **Single-assignment**: a writer is bound at most once; a second binding attempt on an already-bound writer is a defined, reported condition, not a silent overwrite.
- **Suspend vs fail**: an unbound reader needed by unification must produce **suspend**, never **fail** (the most common GLP correctness error; cheat-sheet §8 "three-valued").
- **No occurs-check**: consistent with FCP/the Dart source, unification performs no occurs-check; this is an explicit non-behaviour, recorded so a later "fix" is not mistaken for a bug.
- **Path compression is read-only-safe**: dereferencing (which compresses the path) must not change the logical value of any variable, only the chain length.
- **Imported reader encountered**: out of scope for F4 (single-runtime), but the kernel must not assume a cross-agent reader will ever appear — the core remains correct and total for the single-runtime case (multiagent is F9+).
- **Empty / nested structures and lists**: zero-arity constants, the empty list, and deeply nested compound terms must all be representable and unifiable.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The kernel MUST provide a GLP term model covering: constants (atoms, integers, reals, strings), compound structures (a functor with an ordered list of argument terms), lists (the conventional cons/nil encoding), and variable references — structurally inspectable and comparable for equality, faithful to the Dart `terms.dart` model.
- **FR-002**: The kernel MUST provide a variable store ("heap") that allocates a fresh logic variable as a writer/reader pair following the FCP bidirectional-pointer architecture (writer and reader each reference the other), such that a variable's reader-or-writer role is determined by its cell tag and NEVER by address arithmetic.
- **FR-003**: The kernel MUST dereference a reference by following its chain to the final target — a ground value or an unbound variable — applying path compression so that repeated dereferences of the same reference are constant-time after the first, without altering any variable's logical value.
- **FR-004**: Dereferencing and binding MUST detect a writer-to-writer (WxW) situation and report it loudly (an error/abort, never a silent writer→writer chain), per the heap spec §4.5 and §5.2 invariant.
- **FR-005**: The kernel MUST bind an unbound writer to a ground value (the cell becomes a value cell holding that term), enforcing single-assignment — an already-bound writer is not silently re-bound.
- **FR-006**: The kernel MUST bind an unbound writer to another variable (writer→reader, via the target's reader), forwarding any pending suspensions to the target; it MUST NOT bind a writer directly to another writer.
- **FR-007**: The kernel MUST provide writer-MGU three-valued unification of two terms returning exactly one of {**success**, **suspend**, **fail**}, where: equal grounds → success; structural mismatch (value, functor, or arity) → fail; a needed unbound reader → suspend; and any binding performed binds **only writers** (never readers, never writer-to-writer).
- **FR-008**: The kernel MUST record a suspension against an unbound writer (preserving the writer's reader pairing while suspensions are attached) and, upon binding that writer to a ground value, MUST produce the list of armed activations to be reactivated; upon binding to another variable it MUST forward the suspensions to the target writer. (Consuming the activation list — scheduling/re-running goals — is OUT of scope; that is F5.)
- **FR-009**: The kernel's **observable behaviour** MUST match the Dart source-of-truth across a defined micro-scenario corpus (allocate, dereference, bind-to-value, bind-to-variable, unify across the three-valued truth table, suspend-and-activate), verified by automated tests. Parity is measured on **observable outcomes only** — the dereferenced result, the three-valued unification verdict, and the activation set produced on binding — and explicitly **NOT** on internal heap representation (cell addresses, tags, or layout), which legitimately differs once the heap is re-expressed for Gleam. (Parity baseline is **Dart**, per the F1 ratification — superseding the roadmap brief's "vs C#".)
- **FR-010**: The kernel MUST land in the F3 `glp_gleam/` subtree, filling the `runtime` subsystem (1:1 with the Dart `glp_runtime/lib/runtime/` source-of-truth), MUST build cleanly to the Erlang/BEAM target on the F1-pinned toolchain, MUST run its test suite green (≥1 test, 0 failures), and MUST NOT introduce the disallowed OTP-actor dependency (`gleam_otp`).
- **FR-011**: Adding the kernel MUST be additive only — it MUST NOT change the build, test, or behaviour of any existing subtree (`glp_runtime/`, `glp_runtime_net/`, `out/csharp/`, `codeconv/`), and MUST commit no build/output artifacts (compiled BEAM, build caches).
- **FR-012**: The kernel MUST NOT change the GLP language — no new primitives, guards, system predicates, or type-system features — it is a faithful port of the existing term/heap/unification semantics defined by the authoritative specs. (Any apparent gap in those specs is reported, not invented around.)

### Key Entities *(include if feature involves data)*

- **Term**: the GLP data model — a constant (atom / integer / real / string), a compound structure (functor + ordered arguments), a list (cons/nil), or a variable reference. The thing terms, heap cells, and unification all operate on.
- **Variable (writer/reader pair)**: a logic variable, allocated as a paired writer and reader that reference each other (FCP bidirectional). The writer is the single-assignment output side; the reader is the input side that suspends until the writer binds.
- **Heap / store**: the collection of cells holding terms and variable pairs; the unit dereferenced, bound, and (for the chosen mechanism) threaded or held in process-cells.
- **Cell + tag**: a heap slot whose tag (writer / reader / value) determines its role and content rules.
- **Binding**: the act of assigning an unbound writer to a ground value or to another variable; produces activations and (for var-to-var) forwards suspensions.
- **Suspension record + activation list**: a recorded "this goal waits on this writer" entry, and the list produced when the writer binds (to be reactivated by the future scheduler, F5).
- **Unification outcome**: exactly one of success / suspend / fail, plus the heap effect of any binding performed.
- **Parity corpus**: the defined set of micro-scenarios whose Gleam **observable outcomes** (deref result, unify verdict, activation set — not internal heap layout) are pinned to the Dart source-of-truth.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the defined term kinds (atom, integer, real, string, compound structure, empty list, non-empty list, nested structure, variable reference) can be constructed, structurally inspected, and equality-compared, with results matching the Dart model.
- **SC-002**: A freshly allocated variable dereferences to "unbound"; after binding its writer to a ground value it dereferences to that value; a repeated dereference of the same reference is constant-time (no re-traversal) — demonstrated for 100% of the allocate/deref/bind test cases.
- **SC-003**: Three-valued unification returns the correct one of {success, suspend, fail} for 100% of a defined truth table covering constant/constant (match & mismatch), structure/structure (match, functor-mismatch, arity-mismatch), variable/value, value/variable, variable/variable, and unbound-reader-needed.
- **SC-004**: Writer-to-writer binding occurs in 0 cases; every attempted WxW situation (in binding or deref) is detected and reported (0 silent occurrences).
- **SC-005**: The kernel's **observable outcomes** (deref result, three-valued unify verdict, activation set) match the Dart source-of-truth on 100% of the micro-scenario parity corpus (allocate / deref / bind-to-value / bind-to-variable / unify / suspend-and-activate); internal heap representation (addresses, tags, layout) is explicitly excluded from the parity measure.
- **SC-006**: From a clean checkout on the pinned toolchain, the subtree builds to Erlang/BEAM with zero errors and its suite runs green (≥1 test, 0 failures); the disallowed `gleam_otp` dependency is absent from the committed lock (0 occurrences).
- **SC-007**: Introducing the kernel changes zero existing-subtree build/test outcomes and commits zero build/output artifacts.

## Assumptions

- **Heap-mutation re-expression is a planning decision** *(deferred to `/bk-plan`)*. GLP's WAM-style heap binds cells in place; Gleam has no mutable variables. Per dossier §4.1/§5, the kernel must re-express this as either an **immutable threaded binding store** or a **process-cell heap** (a logic variable = a BEAM process holding the cell) — both proven feasible by the F1 smoke. This spec states behavioural requirements only (FR-002…FR-008); the mechanism choice (which cascades to F5) is settled at plan time, not here.
- **Parity baseline is Dart, not C#** *(decided — Clarifications 2026-06-24)*. The roadmap brief's "parity tests vs C#" is superseded by F1's ratified source decision (Dart `glp_runtime/`); the tracked C# is itself generated from the Dart.
- **Scheduler/runner is out of scope** *(decided — Clarifications 2026-06-24)*. F4 owns suspension *storage* and activation-list *production*; the scheduler that *consumes* activations and re-runs goals is F5 (the bytecode runner).
- **Multiagent imported variables are out of scope** *(decided — Clarifications 2026-06-24)*. `VariableEntry` / cross-agent readers (`bindImportedReader` in the Dart heap) land with the multiagent + link features (F9+). F4 is the complete, correct single-runtime core.
- **`ModuleTerm` and `MutualRefTerm` are out of the F4 core-term set.** `ModuleTerm` (module-dispatch) belongs with the compiler/loader (F6). `MutualRefTerm` (the O(1) stream-append optimization) is an optimization not named in the brief's core-term list ("atoms/ints/structs/lists/vars") and is deferred; F4 ships the core `ConstTerm`/`StructTerm`/list/`VarRef` model. If a later feature needs them, they are added then, faithful to the Dart source.
- **Build/test runtime is plain Erlang/BEAM** (the F1-proven test runtime); AtomVM-specific concerns (raw-`erlang:spawn` cells) only matter if the *process-cell* mechanism is chosen at plan time and AtomVM is exercised — out of scope to gate here. The JavaScript target is out of scope (only partially viable per F1).
- **Module decomposition within the `runtime` subsystem** (e.g. one module vs `glp/runtime/terms`, `glp/runtime/heap`, `glp/runtime/unify`) is a plan-time detail; it need only preserve F3's 1:1-with-Dart-subsystem rule and use legal Gleam module paths.
- **No GLP language change.** This is a faithful port of existing semantics; no new primitives/guards/types (DISCIPLINE §1.14). Any gap or contradiction discovered in the authoritative specs is reported for resolution, not worked around.
- **Dependency on F1, F2, F3.** F4 consumes F1's ratified source-basis (Dart) and pinned toolchain, builds in F2's recognized Dart→Gleam conversion data flow, and lands in F3's committed, building `glp_gleam/` skeleton.

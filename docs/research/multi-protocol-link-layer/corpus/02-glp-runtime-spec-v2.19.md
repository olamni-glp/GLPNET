---
title: "GLP Runtime System Specification for Dart Implementation v2.19"
authors: "GLP/glpnet project (local implementation spec; runtime model after Shapiro's GLP)"
year: "2026"
source_url: "file:///D:/bstdev/research/glp/glpnet/docs/glp-runtime-spec.txt"
retrieved: "2026-06-06"
fetched_for: "glp-local-model — the exact GLP/FCP runtime model (cell representation, writer-MGU, suspension/reactivation, three-valued unification, channels, cross-module routing, madGLP cross-isolate variables) that any distributed link-layer scheme must preserve. Fidelity yardstick for blocker B2 (distributed unification). — Fetch, preserve & extract source: GLP Runtime System Specification v2.19"
precedence_class: glp-current
access: full-text
---

# GLP Runtime System Specification v2.19 — Extraction

## Provenance & precedence note

This is the **authoritative local implementation spec** for the GLP runtime on Dart,
versioned **v2.19 (January 2026)**. Per the research thread's SOURCE PRECEDENCE rule,
local `docs/` GLP specs are the **HIGHEST authority** (precedence class `glp-current`) and
represent *current implementation truth*. The document is not published on the public web;
the local file **is** the primary source. The Shapiro arXiv papers
([2510.15747](https://arxiv.org/abs/2510.15747) "GLP: A Grassroots, Multiagent, Concurrent,
Logic Programming Language"; [2602.06934](https://arxiv.org/pdf/2602.06934) "Implementing
Grassroots Logic Programs with…") supply the *formal language semantics* (precedence class
`glp-paper`) — mechanism backing, but they do **not** override this runtime spec where the
two differ on implementation detail.

The spec defers heap-level detail to its single source of truth,
`docs/heap/heap-pointer-architecture-spec.md` (Sections 2–7, 10), which is referenced for
cell structure, dereferencing, binding, suspension management, paired-cell finding, and the
multiagent imported-variable extension. v2.19's headline change is exactly this refactor to
remove duplicated heap content, plus the **pointer-direction reversal**: readers point TO
writers, and suspensions live on **writer** cells.

---

## Why this matters for the distributed link layer (B2)

Any scheme that "distributes the one-writer/one-reader atomic pair across remote GLP REPL
instances" must preserve the invariants below verbatim. The load-bearing ones for distributed
unification (B2):

1. **Asymmetric writer MGU** — only writers bind; readers are only *verified*; writer-to-writer
   binding is **immediate failure** (WxW), never deferred/tracked. A remote link primitive that
   carries a binding must respect this asymmetry: the binding travels *from a bound writer to a
   reader*, never writer↔writer.
2. **Three-valued unification** (Success | Suspend | Fail) — a remote reader that is not yet
   bound must produce **Suspend**, not Fail. The link must surface "remote writer not yet bound"
   as suspension, registering on the (local proxy of the) writer.
3. **Suspension lives on the writer cell**; reactivation is **wake-and-retry from kappa** (the
   procedure entry PC), NOT resume-at-suspension-point. A distributed wake therefore only needs
   to deliver "the writer bound" — the woken goal re-derives everything by retrying its clauses.
   This is the property that makes a coarse, lossy-tolerant remote wake signal sufficient.
4. **Variable-to-variable binding forwards suspensions** rather than activating goals — the
   chain/forward semantics is what a cross-instance binding must emulate when the remote side
   binds X to *another* unbound variable Y instead of to ground.
5. **Committed-choice / no-trail** — once a clause commits there is no backtracking; no choice
   points, no trail. A distributed protocol never has to undo a commit.
6. **Multiagent extension already exists**: imported readers/writers have **no local paired
   cell**; the cell content is a `VariableEntry` (V_p entry) representing the **remote** variable,
   and dereferencing is extended to handle V_p targets (heap spec §10). This is the existing
   in-repo hook the link layer should build on — the cross-isolate variable mechanism is the
   nearest analog to a cross-*instance* (cross-REPL) variable.

---

## Structured extraction (load-bearing claims, verbatim where marked)

### 0. Version history (the v2.19 delta and its lineage)

- **v2.19 (Jan 2026)** *(verbatim)*: "Refactored to reference
  `docs/heap/heap-pointer-architecture-spec.md` for heap design. Removed duplicated heap content
  to maintain single source of truth. The pointer architecture reverses the pointer direction:
  readers point TO writers (not vice versa), and suspensions are stored on writer cells."
- **v2.18**: "Reader cells can now point to V_p entries (VariableEntry) for imported readers, not
  just local writer cells. Dereferencing extended to handle V_p entry targets." — the multiagent
  cross-isolate variable representation.
- **v2.17.2**: "distinction between bind-time behavior (always use writerAddr + 1) and
  suspension-time behavior (follow chains)" + "suspension forwarding when binding to another
  variable."
- **v2.17**: "Variables are identified by heap addresses directly… Writer and reader are two
  distinct addresses."
- **v2.16.2**: V1 opcode sunset; unified V2 opcodes (`GetVariable`, `GetValue`, `SetVariable`,
  `PutVariable`, `UnifyVariable`). "The compiler knows at compile time whether a variable is a
  writer or reader… The runtime determines the role by examining the cell tag."

### 1. Unification engine — the writer MGU (§ "Unification Engine")

*Verbatim:* "The writer MGU algorithm forms the core of goal reduction. Unlike standard Prolog
unification, it enforces GLP's asymmetric variable semantics where **only writers can be bound,
readers can only be verified, and writer-to-writer bindings cause immediate failure (not tracked
or deferred) to prevent abandoned readers per WxW restriction.** The engine maintains a mode flag
(READ/WRITE) that determines instruction behavior."

Head unification is **two-phase** *(verbatim)*:
- "(1) Collection—arguments are processed left-to-right, accumulating tentative writer bindings
  and a preliminary suspension set of readers matched against constants or structures;
- (2) Resolution—the preliminary set is filtered against the collected bindings, removing readers
  whose paired writers were bound. If the resolved set is empty, unification succeeds; otherwise
  the clause suspends on the remaining readers."

### 2. Three-phase reduction model

The runtime realizes the HEAD → GUARDS → BODY phases via:
- **HEAD instructions** perform *tentative term matching*, building **σ̂w without heap mutation**.
  *Verbatim:* "All head instructions are **pure** - they never mutate the heap, only extend σ̂w and
  Si."
- **GUARD instructions** are three-valued (see §5).
- **BODY** begins only at `commit`, which sets `inBody = true` "(enable heap mutations)". Body
  instructions (`put_structure`, `put_writer`, `put_reader`) "never suspend as they only prepare
  data rather than performing term matching."

### 3. Goal state — the σ̂w / Si / U triple (§ "Goal State", `RunnerContext`)

The execution context (`lib/bytecode/runner.dart`) holds, verbatim from the code block:
- `final Map<int, Object?> sigmaHat = {};   // σ̂w: tentative writer substitution`
- `final Set<int> si = {};                  // Si: clause-local suspension set`
- `final Set<int> U = {};                   // U: goal-level suspension set`
- `final Map<int, Object?> clauseVars = {}; // Clause variable bindings`
- `final int kappa;  // Entry PC (first clause of procedure)`
- `bool inBody = false;  // Phase: HEAD/GUARDS vs BODY`
- `int tailRecursionBudget = 26;  // Budget for tail call fairness`
- `int S = 0;  // Subterm pointer for structure unification`
- `Mode mode = Mode.read;  // READ or WRITE mode for structures`

Roles: **σ̂w** = tentative writer substitution accumulated during HEAD; **Si** = clause-local
suspension set (readers seen this clause); **U** = goal-level suspension set (the union across
tried clauses, the set the goal ultimately suspends on); **kappa** = procedure entry PC = the
wake-and-retry target.

### 4. Commit & clause control (§ "Control Flow: Clause Selection and Suspension")

- **`clause_try`**: "Clear Si… Save restore point for σ̂w (in case clause fails)."
- **`clause_next label`**: "Union Si into U (`U := U ∪ Si`); Discard σ̂w (abandon tentative
  bindings); Clear Si; Jump to label."
- **`commit`** *(verbatim)*: "Apply σ̂w to heap atomically (bind all writers); For each newly
  bound writer, process its suspension list to reactivate suspended goals; Clear σ̂w and Si; Set
  `inBody = true` (enable heap mutations)."
- **`no_more_clauses`** *(verbatim)*: "If U non-empty: suspend goal via `suspendGoal(goalId,
  kappa, U)`, return SUSPENDED. If U empty: definitive failure, return TERMINATED."
- **`proceed`**: "Return TERMINATED (success)."
- *Verbatim:* "There are NO explicit `suspend`, `reactivate`, or `abandon` bytecode instructions.
  These are runtime operations triggered automatically by control flow instructions."

**Soft-fail invariant** (mid-clause transitions: head functor/value mismatch, nested mismatch,
indeterminate guard) *(verbatim)*: "A soft-fail performs the same Si→U merge as `clause_next`:
1. Union Si into U (`U := U ∪ Si`) 2. Discard σ̂w (abandon tentative bindings) 3. Clear Si 4. Jump
to the next clause's `clause_try`." — Rationale: "Without this merge, a goal that should suspend…
would instead fail when `no_more_clauses` finds U empty."

### 5. Guard three-valued semantics (§ "Guard Three-Valued Semantics")

*Verbatim outcomes:*
- "**True**: The guard succeeds, execution continues to the next instruction"
- "**False**: The guard definitively fails (e.g., `string(42)`…), soft-fail to next clause"
- "**Indeterminate**: The guard cannot be evaluated because its argument depends on an unbound
  reader (e.g., `ground(X?)`…), soft-fail to next clause with the unbound reader(s) added to Si"
- "In the indeterminate case, the readers are added to Si before the soft-fail, so the Si→U merge
  preserves them for potential suspension at `no_more_clauses`."

### 6. Suspension & reactivation (wake-and-retry from kappa)

**Suspension Process** (`no_more_clauses`, U ≠ ∅) *(verbatim)*:
1. "Runtime calls `suspendGoal(goalId, kappa, U)` with the suspension set"
2. "Create ONE `SuspensionRecord(goalId, kappa)`"
3. "For each address in U: if reader, follow pointer to writer; add suspension to writer's list"
4. "Goal returns `RunResult.suspended` and is removed from GQ"

**Reactivation Process** (during `commit` when writer binds) *(verbatim)*:
1. "`bindWriter(writerAddr, value)` binds writer"
2. "Save and clear writer's suspension list"
3. "Update cell content/tag per binding type"
4. "Walk saved list, for each armed record: Enqueue `GoalRef(goalId, kappa)` to GQ; Disarm the
   record (set goalId = null)"
5. *(verbatim, load-bearing)* "**Reactivated goal resumes at PC = kappa (first clause), NOT at
   suspension point**"

**Single-shot mechanism** *(verbatim)*: "The shared record with `armed` flag prevents duplicate
reactivation if a goal was suspended on multiple variables and more than one binds." The **same
record object** appears in multiple suspension lists; disarm (goalId=null) on first bind.

**Suspension processing at bind time** depends on the binding target *(verbatim)*:
1. "**Bound to ground value:** Activate all armed suspension records… Goals wake and retry from
   their procedure entry point (kappa)."
2. "**Bound to another variable (via reader):** Forward suspension records from this writer to the
   target variable's writer. Do NOT activate goals yet. This preserves wake-and-retry semantics…
   See heap spec Section 6.4 for forwarding details."

**Variable-chain suspension (wake-and-retry)** — suspend on the FINAL unbound writer in a chain
(via `derefAddr()`), because a writer already bound to another unbound variable will never wake if
naively suspended on. *Verbatim semantics:* "Goals wake when ANY binding occurs (even
variable-to-variable bindings); If still unbound after waking, goal re-suspends (possibly on
different variable); This is simpler than transitive chain tracking."

### 7. Committed-choice / no-trail (§ "Goal Reduction Cycle")

*Verbatim:* "**No backtracking**: GLP uses committed-choice semantics - once a clause commits via
`commit`, there is no going back. Failed clauses simply try the next clause via `clause_next`. No
choice points or trail stack needed." Space complexity confirms: "**No trail stack**:
Committed-choice semantics eliminate backtracking, so no trail needed."

Execution-cycle results: `CONTINUE` / `SUSPENDED` / `TERMINATED` / `OUT_OF_REDUCTIONS`. Each
active goal runs in a Dart microtask "until it suspends, fails, or completes."

### 8. Scheduling & fairness (two-level)

*Verbatim:*
- "**Intra-GLP fairness**: Active goals are queued as Dart microtasks in FIFO order. Both `spawn`
  and `requeue` place goals at the tail of the queue…"
- "**System-level fairness**: … bounded tail recursion. Each goal has a tail-recursion budget
  counter that decrements on each `requeue`… When the counter reaches zero, the goal is scheduled
  via `Timer.run()` instead of `scheduleMicrotask()`, yielding to the event queue…" (default
  budget e.g. 26).
- `spawn` = new microtask at queue tail (non-final body goals); `requeue` = tail-recursion reuse
  with budget; `proceed` = return to continuation saved by `spawn`.

### 9. Heap / cell representation (deferred to heap spec; key points restated here)

*Verbatim key design points:*
- "Each logical variable consists of two heap cells at consecutive addresses (**writer at N,
  reader at N+1**)"
- "**Reader cells point TO writer cells** (not vice versa)"
- "Writer cells contain: `null` (unbound), `SuspensionListNode`, or `Pointer` (bound to another
  variable)"
- "Suspensions are stored on **writer cells**, not reader cells"
- "`VarRef` contains a single `addr` field; the cell tag determines reader/writer role"
- "`allocateVariable()` returns `(writerAddr, readerAddr)` tuple"
- Cell tags referenced in code: `CellTag.RoTag` (reader/read-only), writer tag (WrtTag) implied.

**Imported variables (multiagent / cross-isolate)** *(verbatim)*: "For multiagent GLP, imported
readers/writers have no local paired cell. Instead, the cell content is a `VariableEntry` (V_p
entry) representing the remote variable. See heap spec Section 10 for details." — This is the
existing cross-isolate-variable representation the distributed link layer extends to
cross-instance.

### 10. SO / SRSW enforcement

*Verbatim:* "Runtime assumes compiler-verified SRSW syntactic restriction (which preserves SO
invariant). Runtime must fail on writer-to-writer term matching attempts (WxW). Circular terms
may form through cross-goal communication; implementations must handle them gracefully."

### 11. Circular term handling

*Verbatim formation example:* "Clause `p(X?,X)` with goals `p(X,f(Y?)), p(Y,f(X?))` produces
`X = f(f(X?))`." Requirements: deref must detect cycles & terminate; ground iff no variables
(cycles ≠ non-ground); equality must terminate & succeed iff identical structure incl. cycle
points; copy must preserve cyclic structure; display must terminate finitely.

### 12. Opcode name mappings (paper ↔ implementation)

- paper "suspend" (predicate end) ↔ impl `NoMoreClauses` + `SuspendEnd` (`SuspendEnd` legacy ≡
  `NoMoreClauses`).
- paper "proceed" ↔ `Proceed`; paper "clause_try"/"clause_next" ↔ `ClauseTry`/`ClauseNext`.
- All `head_*`, `put_*`, `commit`, `spawn`, `requeue` identical in both.

### 13. Multiagent roadmap (Phase 5)

*Verbatim:* "Phase 5: Multiagent Extension — Extend to multiple isolates for true multiagent
execution, adding inter-isolate message passing and distributed variable management. This
completes the full GLP vision for grassroots platforms." — The link-layer feature is the
*cross-instance* (cross-REPL/cross-host) generalization of this in-process multi-isolate phase.

---

## Cross-references inside this corpus / repo

- Heap single source of truth: `docs/heap/heap-pointer-architecture-spec.md` (cell structure §2,
  alloc §3, deref §4, binding §5, suspension §6 incl. forwarding §6.4, paired cells §7, imported
  V_p entries §10). **Required companion read for B2.**
- Multiagent / cross-isolate specs: `docs/ma/madGLP-spec.md`, `docs/ma/agent-runtime-spec.md`,
  `docs/ma/isolate-boot-spec.md`, `docs/mutual-ref-spec.md`.
- Channels / cross-module routing: `programs/self.glp` (prelude), `docs/glp-cheat-sheet.md`.
- Formal-semantics backing (precedence `glp-paper`): Shapiro, *GLP: A Grassroots, Multiagent,
  Concurrent, Logic Programming Language*, arXiv:2510.15747; *Implementing Grassroots Logic
  Programs…*, arXiv:2602.06934.

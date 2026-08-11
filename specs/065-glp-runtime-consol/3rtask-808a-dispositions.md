# 3rtask 808a — engineer disposition round

**Date**: 2026-08-10
**Run**: `.specify/3rtask/runs/20260807T094711Z-808a/` (verdict `budget_stop`, 3 cycles)
**Authority**: Gabi's directive 2026-08-10 delegated working the open items O1–O7 and the
fix-class staging decision to this session. Normative GLP-semantics rulings (O1/O2) are
**proposals only** in this document — they take effect only on Gabi's express approval
(DISCIPLINE §1.14); everything else is decided/settled here.

---

## §1 Fix-class staging — DECIDED (Gabi-directed)

**Adopted: F5+F6 → F2 → (decision point) F1-or-F4**, the Curator's statable-without-a-winner
staged path (curator_report.md §e).

- **Stage 1 — F5 (provenance enforcement) + F6 (close the reclassification channel).**
  Records/harness only, days-scale blast radius. No behaviour ruling needed to start.
- **Stage 2 — F2 (executable cross-runtime conformance suite over the D-grid, wired blocking).**
  Directly closes the demonstrated E10 permeability hole; addresses R1. Per E16d-ii the corpus
  expected outcomes start **blank and are engineer-populated per D-point** — the O1/O2 rulings
  below are the population inputs. F2 harness construction can precede the rulings; corpus
  **content** cannot.
- **Stage 3 — decision point F1-or-F4**, taken only after F2 exists (F2 collapses F1's S4
  residual). Deferred; no ruling now.
- **E16d-iii audit**: no "preserve current behaviour" default is adopted anywhere in this
  staging; every semantic slot remains an explicit engineer decision.

## §2 O1 — unbound-argument semantics + the Goal-mode contradiction (PROPOSAL)

### O1a — the Goal-mode contradiction (spec defect, confirmed verbatim)

The normative documents disagree on the mode of `_activate/2`'s second argument:

- `docs/type system/dynamic-module-dispatch.md:133` (signature):
  `'_activate'(Module?, Goal)` — Goal in writer position.
- `docs/body-kernels-reference.md:123` (signature):
  `'_activate'(Module?, Goal)` — Goal in writer position.
- `docs/type system/dynamic-module-dispatch.md:158` (the spec's own serve loop):
  `'_activate'(Module?, Goal?),` — Goal passed as **reader**.
- `docs/modules/dynamic-dispatch-implementation-plan.md:112` (the plan's serve loop):
  `'_activate'(Module?, Goal),` — Goal passed as writer.

**Proposed ruling**: the canonical signature is **`'_activate'(Module?, Goal?)`** — Goal is an
input. Rationale: the caller provides the goal term and the kernel consumes it (resolves it
against `_select/1`); per typed-glp-manual §1.4, caller-provides → input (`Type?`). The serve
loop at `dynamic-module-dispatch.md:156-159` already implements the fundamental
receive-and-pass shape (head writer `Goal`, body reader `Goal?` — cheat-sheet §3b). Reply
writers embedded *inside* the goal term are carried by the term itself (manual §16: the type's
`?` describes the data, not the clause variable).

**Doc repairs on approval**: fix the two signature lines (`body-kernels-reference.md:123`,
`dynamic-module-dispatch.md:133`) to `'_activate'(Module?, Goal?)`; align
`dynamic-dispatch-implementation-plan.md:110-113` to the DMD serve loop (which also carries the
SRSW-required `ground(Module?)` guard the plan's version lacks) or mark the plan section
superseded by DMD §3.4. Single source of truth: `dynamic-module-dispatch.md`.

### O1b — unbound Module (D1) / unbound Goal (D5) at kernel entry

**Proposed ruling**: body kernels **never suspend** — an unbound reader reaching a kernel is a
caller bug and **aborts**. This is already the composed spec position:

- `docs/glp-predicate-taxonomy.md:173`: "Unbound operand → **abort** \"Unbound reader in body
  kernel\""
- `docs/body-kernels-reference.md` Design Principle 3: "guards handle the three-valued
  (success/suspend/fail) patient semantics, while kernels handle the two-valued
  (success/abort) computation."

The suspension point is the serve loop's `ground(Module?)` guard, upstream of the kernel. The
run's finding that "no runtime suspends on unbound Module" is therefore **conformant**; what
diverges (and must equalize) is the abort *route* — ruled in §3.

## §3 O2 — failure-route taxonomy over the D-grid (PROPOSAL table)

Inputs: divergence matrix rows A1–A7 + builder-7's escalated mandate rows. Every row below is
**PROPOSED — awaiting Gabi's per-row ruling**; on approval the rows become the normative
D-grid semantics table (root-cause R2's missing forcing function) and the F2 corpus
expectations. "Abort" below always means: kernel returns abort status to the engine; the
engine reports one defined diagnostic through the runtime's standard error sink and aborts the
**scheduler-scope** computation — never a bare host print, never an uncaught host exception,
never a whole-run takedown.

| D | Condition | Current (Dart / C# / Gleam) | Proposed normative outcome |
|---|---|---|---|
| D1 | Module unbound reader | abort-print / abort-log / typed-error; none suspend | Abort `"Unbound reader in body kernel"` (§2 O1b) |
| D2 | Module bound, not a module | abort-print / abort-log / typed-error | Abort `"Type error in body kernel"` |
| D3 | Module present, payload absent | as D2 routes | Abort `"Type error in body kernel"` (invalid module value) |
| D4 | Payload present but corrupt | Dart silent fallthrough / C# uncaught host exception / Gleam unrepresentable | Abort (defined diagnostic). The Dart fallthrough and C# uncaught propagation are both defects. |
| D5 | Goal unbound reader | silent (state diverges Dart; indistinguishable C#/Gleam) | Abort `"Unbound reader in body kernel"` (§2 O1b). Current silence is a defect in all three. |
| D6/D7 | Goal wrong kind / wrong shape | silent (as D5) | Abort `"Type error in body kernel"` |
| D8 | Module carries no `_select` table | vacuous success family | Abort — a module without `_select/1` violates the construction precondition (`body-kernels-reference.md:127`) |
| D9 | Table has no entry for functor | vacuous success, silent, all three | **Keep spec as written**: `dynamic-module-dispatch.md:145` — "On no match (unknown goal), the `otherwise` clause of `_select/1` handles it silently." Silent-otherwise is the normative handler; the F2 corpus must still pin it so a flip (the E10 proof) fails the gate. |
| D10 | Functor matches, arity does not | vacuous success | Same route as D9 (`_select` matching is functor+arity; no match → otherwise clause) |
| D11/D12 | Duplicate / ambiguous entries | unpinned | **Forbidden by construction**: the `_select/1` generator must never emit two entries for one functor+arity; a duplicate is a compiler defect, not a runtime case. Runtime behaviour on a malformed table: committed-choice first-match, explicitly non-normative. |
| D15 | Entry names an absent target | Dart vacuous-success / C# enqueues invalid PC unvalidated | Abort (malformed module binary). C#'s unvalidated enqueue is a defect. |
| D17 | Error during target resolution | unpinned | Abort (defined diagnostic), scheduler scope |
| D23 | Kernel return protocol | two-status confirmed | Two-status (success \| abort) is normative; no third value may be constructed |
| D24 | Engine mapping per kernel value | partially inconsistent (O5) | success → continue per D20/D22 ruling; abort → the §3 abort route |
| D25–D27 | Post-dispatch failure: scope / catch / propagation | scope: Dart,C# scheduler vs Gleam whole-run; catch+disposition diverge | Abort scope = **SCHEDULER** (never whole-run — Gleam defect); catch at host boundary; disposition = COMPUTATION_ABORTED with diagnostic (C# route); a post-dispatch error never silently binds or fails the RPC reply |
| D29 | Reentrant activation | unpinned | Permitted (modules are stateless procedure tables; serve serializes per-channel anyway) — needs Gabi confirmation for monitor-style modules |

Also requiring ruling (from A6): **D20/D22 caller-goal fate on success** — settled factually in
§4/O5: in both Dart and C# the kernel executes inline and returns a two-valued status; on
success the engine does `pc++` and the **activating goal continues executing** (REMAINS_ACTIVE
at the dispatch instruction; the dispatched work is enqueued as a separate goal). Proposed
normative wording: "the `'_activate'` call is an inline body-kernel step; on success the
enclosing goal proceeds to its next body instruction, and the resolved procedure body runs as
an independently scheduled goal in the module's program context." The A6 cross-runtime split
reduces to verifying Gleam against this wording (one targeted read, F2 corpus row).

## §4 O3/O4/O5 — factual investigations (settled by targeted reads, this session)

### O4 — corpus manifest vs `corpus.list`: **`corpus.list` governs; the manifest is prose-only**

- `test/parity/run_gleam_corpus.sh:19,173` and `test/parity/record_dart_goldens.sh:23,215`
  both read only `test/parity/corpus.list`; `corpus.list:2-3` declares itself the single
  source of truth for both.
- `corpus-manifest.md` is opened by **no executable** (all references are comments); its
  "Sections L–R … OUT" declaration (`corpus-manifest.md:80-82`) is unenforced and holds only
  because `corpus.list` currently contains no dispatch case. The manifest itself concedes
  primacy at `:8-11`. One doc defect: `test/parity/README.md:4-5` calls the manifest the
  "pinned case list" — wrong; the scripts and the manifest both say it is `corpus.list`.
- `expected.list` only reclassifies (blocked/gap/fork → out_of_scope), never adds/removes
  cases (`run_gleam_corpus.sh:27-33`); it is currently comment-only (no active
  reclassification).
- Wiring: `test/run_all_tests.sh` invokes **neither** `run_gleam_corpus.sh` nor
  `run_differential.sh` (Section I at `:2224` runs only
  `parity/cross_runtime/run_all.sh` → `link_both_ways.sh round_trip.sh mismatch.sh`). Both
  corpus tools are manual-only. **Confirms b8 rows 1/4 and refines O4: scope is governed by
  `corpus.list`, and in the unified suite, by nothing at all.** F6 must therefore add: the
  manifest's out-of-scope claims each name a covering gate, and `run_gleam_corpus.sh` gets
  wired into the suite (that wiring is F2's blocking-gate half).

### O5 — kernel-vs-engine INTERNAL_INCONSISTENCY at D18/D20/D21/D23: **abstraction artifact, not real**

- **Dart**: `body_kernels.dart:820-881` — the kernel enum is strictly two-valued (`:22-28`);
  successful dispatch enqueues a **new** goal (`:878`) and returns success (`:880`); the
  engine's sole dispatch site (`runner.dart:3220-3252`) handles abort → terminated
  (`:3244-3247`) and success → `argSlots.clear(); pc++; continue` (`:3249-3251`). Caller
  goal REMAINS_ACTIVE.
- **C#**: `out/csharp/lib/runtime/body_kernels.cs:1015-1080` + `out/csharp/lib/bytecode/runner.cs:3219-3258`
  mirror Dart line-for-line (`_Step.Advance()` = pc+1). Caller goal REMAINS_ACTIVE.
  (Note: `csharp/**` proper contains no `_activate` — the only C# kernel is the generated
  port under `out/csharp/`.)
- The slice disagreement traces to the overloaded phrase "the goal silently succeeds" in the
  kernel doc comments (`body_kernels.dart:818-819,842,853` / `body_kernels.cs:1011-1012`):
  kernel-altitude "goal succeeds" ≠ engine-altitude per-instruction continue. **No code path
  in either runtime completes or re-enqueues the activating goal on successful dispatch.**
  Scope caveat: this settles the fate at the dispatch instruction only; the caller's
  subsequent body instructions are separate decision points.
- Follow-up doc repair (with O1a's): reword those kernel doc comments to "the dispatch is
  silently skipped and the kernel returns success" so the two altitudes can no longer be
  read as disagreeing.

### O3 — codeconv build gate + tombstone liveness: **gate is compile-only; tombstones are live**

- **(a) The build gate never compared behaviour.** The whole gate is two subprocess calls —
  `dotnet build` (Increment 1) / `dotnet test` (Increment 2) in
  `codeconv/src/codeconv/tools/codegen/buildgate.py:98-186`, invoked from
  `workflow.py:630-635`. No Dart is ever run on the codegen path; the pre-gate
  `validate_generated` is regex text-shape only; the promotion gate adds a human median
  score ≥ 4 (`review.py:44-60`). The metric contract says it plainly
  (`specs/019-codeconv-codegen/contracts/metric_contract.md:4-8`). Behavioural comparison
  exists only as feature 020's separate `equiv` dual-REPL trace oracle
  (`tools/equiv/capture.py`), which codegen does not import. **b8's "compile-only" claim is
  CONFIRMED**: a generated file can be `built`/promoted while behaviourally wrong.
- **(b) Tombstones are live, load-bearing state — NOT historical.** At least eight run-time
  read sites, including a hard pipeline gate: `builder run` exits `EXIT_STALE` on
  tombstone-vs-DB divergence (`tools/builder/__init__.py:126-138,436-468`); rebuild sources
  of truth (`discover --from-tombstones`, `depgraph rebuild-conversions-from-tombstones`,
  `planagents rebuild-plans-from-tombstones`); sha256 audit (`discover
  --verify-tombstones`); enrich candidacy; merge-before-write in every stage writer. Caveat:
  no tombstone is consumed by the build gate itself.
- **Consequence for F5**: the provenance fix extends the *existing* live tombstone
  discipline (content hashes are already recomputed there) rather than inventing a new
  mechanism — record generated-file content hashes in the same records and add the
  verify-both-directions check; create the missing Gleam provenance record.

## §5 O6 — refuted claims

The 14 Critic-refuted claims stand corrected per the Critic's citations; the two cycle-3
refutes (b7 D22, b8 corpus-manifest) had no post-cap re-run — their corrected values are
carried in O4/O5 above. No further action.

## §6 O7 — engineer inputs the method requires

**Cost bands (proposal, commensurable, per F-candidate)**: XS < 1 day · S 1–3 days ·
M 1–2 weeks · L 2–6 weeks · XL > 6 weeks (single-session-equivalent effort, excluding the
engineer's own D-grid sitting time, which is priced separately as ~one sitting).

| Candidate | Band | Note |
|---|---|---|
| F6 | XS | expected.list expiry + manifest gate-naming |
| F5 | S | hash records + checker + Gleam provenance record |
| F2 | M | harness + 3 adapters; corpus content = engineer sitting (separate) |
| F1 | L–XL | generator + per-host adapters |
| F4 | XL | re-founds three runtimes |

**Disposition of O1/O2 before F1/F2 population**: §2/§3 above are exactly that disposition,
pending approval.

## §7 Sequenced next actions

1. Gabi rules on §2 (O1a signature, O1b abort) and §3's rows (or amends them).
2. F5+F6 start immediately (no ruling dependency) — candidate next feature after the current
   marathon queue, or folded into 065 follow-up.
3. F2 harness lands next; its corpus is populated from the approved §3 table, one row per
   D-point, all three runtimes blocking.
4. F1-or-F4 decision only after F2 runs green on the ruled rows.

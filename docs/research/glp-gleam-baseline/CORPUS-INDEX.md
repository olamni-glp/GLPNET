# Corpus Index — GLP → Gleam/AtomVM Baseline Program (T001)

**Feature** `036-glp-gleam-baseline-program` · **Marathon run** `mrun-5611c436ba95` · **Authored** 2026-06-29 (T001, Phase A).

The shared map of **every grounding source** the research pipelines (P2–P8 + ANTLR deep-dive) must
cite. Per the pipeline contract, *"if unread, do not assert"* and every claim cites `file:line` /
page / URL (`contracts/pipeline-contract.md:16`). All paths verified to exist 2026-06-29.

**Repo root** = `D:\bstdev\research\glp\glpnet` — in-repo paths below are repo-relative. Sibling
repos live under `D:\bstdev\research\` and are marked **[sibling, READ-ONLY]**.

> 🔴 **Guardrail (FR-010 / SC-007).** Until the owner approves the P8 synthesis (discharge gate
> T014), this program is **read-only on the target roadmap, specs, code, and on every sibling
> repo**. Sibling repos are *cited*, never written. New artifacts go only under
> `docs/research/glp-gleam-baseline/` (FR-016).

---

## A. Primary GLP corpus — the semantic source of truth (P4 faithfulness MUST cite these)

| Source | Path | Authoritative for |
|---|---|---|
| GLP implementation spec (PDF) | `D:\bstdev\research\glp\GLP\GLP_IMPLEMENTATION.pdf` **[sibling, READ-ONLY]** | Core GLP implementation semantics — the primary citation target for M1/M2 faithfulness criteria (FR-003). |
| Art of GLP (language ref, PDF) | `D:\bstdev\research\glp\GLP\GLP_ART.pdf` **[sibling, READ-ONLY]** | GLP language reference / programming model. |
| Art-of-GLP-2025 (formal book, TeX) | `D:\bstdev\research\Art-of-GLP-2025\` **[sibling, READ-ONLY]** | The formal book. `formal.tex` (theorem/definition/proof infra), `main_AofGLP.tex` / `main_AofGLP.pdf` (book), `chapters/`, `appendices/`, `AofGLP/` (code+exercises: `04_distribution`, `24_consensus`, `book_examples`, `exercise_solutions`), `bib.bib`. Source for distribution/consensus semantics behind M2. |
| GLP cheat-sheet (in-repo) | `docs/glp-cheat-sheet.md` | Quick GLP correctness rules — SRSW (`:78-81`), outputs-built-in-heads (`:9`), three-valued guards (`:238`). Used by the P5 spike for faithfulness grounding. |
| Typed GLP manual (in-repo) | `docs/typed-glp-manual.md` | Type system + programming model. |
| Bytecode ISA (in-repo) | `docs/glp-bytecode-v216-complete.md` | The v2.16.3 instruction set (ED-2 frozen ISA). |
| Runtime spec (in-repo) | `docs/glp-runtime-spec.txt` | Runtime architecture (RunnerContext, three-phase exec, suspension). |

## B. Dart reference runtime — the PARITY source-of-truth (what M1/M2 are measured against)

`glp_runtime/` — the Dart reference implementation. Faithfulness = identical observable execution
semantics vs this (and the C# mirror). Key `lib/` subtrees:

| Subtree | Authoritative for |
|---|---|
| `glp_runtime/lib/runtime/` | **Core heap / binding / suspension — the parity source-of-truth.** `heap_fcp.dart` (two-cell writer/reader; bidirectional self-bind recognizer at `:312-323`), `scheduler.dart:7-21` (`ExecutionStatus`/`DrainResult`). |
| `glp_runtime/lib/bytecode/` | Opcodes — `opcodes.dart` (HEAD/COMMIT/BODY families), `opcodes_v2.dart` (unified reader/writer ops, `isReader`). |
| `glp_runtime/lib/compiler/` | Front-end compiler → IL/bytecode — `codegen.dart`, `analyzer.dart:823-831` (register assignment). |
| `glp_runtime/lib/engine/` | Query execution — `glp_engine.dart:485-558` (`_runSingleGoal`). |
| `glp_runtime/lib/link/` | IL linking / load phase. |
| `glp_runtime/lib/lint/`, `analysis/` | Static analysis / instruction analysis. |
| `glp_runtime/lib/multiagent/` | maGLP coordination — the M2 term-level agent-link seam (ED-1). |
| `glp_runtime/bin/glp_repl.dart` | REPL entry (the one unified compile/typecheck/run pipeline). |

> The sibling GLP language repo (`D:\bstdev\research\glp\GLP\` **[sibling, READ-ONLY]**) is the
> upstream source-of-truth; glpnet's `glp_runtime/` is kept byte-converged with it. Cite the in-repo
> copy for this program; note the sibling as upstream where it matters.

## C. Gleam port + parity evidence

| Source | Path | Authoritative for |
|---|---|---|
| Gleam subtree | `glp_gleam/` (`src/glp/`, `test/`, `gleam.toml`, `smoke.sh`) | The Gleam port of the GLP kernel (core terms, heap, unify, suspension). Builds/tests on BEAM under WSL (F3/F4 shipped). |
| 034 parity evidence | `specs/034-glp-gleam-core-terms-and-heap/parity-evidence.md` | The M1 kernel parity audit — **11 scenarios** cross-validated Gleam ↔ Dart `glp_runtime/lib/runtime/` (deref, bind-to-value/var, chains, WxW detect, unify truth table, suspend/activate, disarmed suspension, forwarding, self-bind). The seed parity-criteria list for P4's M1 bar. |

## D. In-repo research corpora (`docs/research/`)

| Subdir | Authoritative for |
|---|---|
| `glp-gleam-baseline/` | **This program.** `feature-definition.md` (durable source of truth), `pipelines/` (P1…P8 artifacts; see `pipelines/INDEX.md`). |
| `repl-engine-separation/` | Engine-separation dossier + the **verification armoury** (`spikes/`, see §E), `reconciliation/` (MLIR-GLP-DIALECT.md, LEAN-TACTIC-LOOP.md, PROTOCOL-VERIFICATION-ARMOURY.md), `design-dossier.md`, `llvm-feasibility.md`, `requirements.md`, `research-programme.md`. Primary input to the realignment (T006). |
| `multi-protocol-link-layer/` | The link-layer corpus (`corpus/`), `B2-B3-G-decision.md` — the M2 / distributed-unification grounding (P4 M2 bar, P6). |
| `gleam-atomvm/` | Gleam/AtomVM porting research — `dossier.md`, `toolchain-inventory.md`, `hello-glp-term`, `js-probe`. Primary input to P6. |
| `bridge-daemon-coordination/` | Bridge lifecycle/coordination semantics (`01-problem-specification.md`, `02-external-formulations.md`). |
| `pgbridge-reference/` | PGLite bridge reference impls (JS). Infrastructure, not a faithfulness source. |

## E. Verification armoury (the proof tools — invocation in `PROOF-HARNESS.md`)

`docs/research/repl-engine-separation/spikes/` — real-tool spikes already recorded green (027). The
**T002 `PROOF-HARNESS.md`** holds exact reproduce commands + tool versions.

| Spike | Path | Proves (precedent outcome) |
|---|---|---|
| Lean 4 tactic-loop | `…/spikes/lean/` | Semantic invariants on a real Lean kernel — precedent: SRSW-preservation **PROVED** (5/20). |
| SPIN/Promela | `…/spikes/spin/` | Protocol / linked-distribution liveness+safety — precedent: front↔back handshake **PASS**. |
| MLIR round-trip | `…/spikes/mlir/` | IL round-trip on real compiled-LLVM bindings — precedent: `decode(encode(p))==p` **PASS**. |
| exec-equivalence | `spike/p5-il-merge/lib/exec.dart` | Real-runner behavioural equivalence (Suspend-not-Fail, reactivate+commit) — precedent: `merge/3` byte-identical + execution-equivalent **PASS**. |

## F. The P5 IL / machine-language spike (DONE — fixed input ED-1…ED-6)

`spike/p5-il-merge/` — the ratified-architecture verification. `SPIKE-RESULT.md` (verdict YES),
`grammar/merge.g4` (+ vendored `lib/antlr_gen/` parser), `lib/{il,verifiers,lowering,exec,antlr_adapter}.dart`,
`bin/{phase_a,phase_b,probe}.dart`. Authoritative for ED-5 (byte-identical + execution-equivalent +
verifiers fire) and the ANTLR-integration starting point (T008). Decision record:
`pipelines/P5-il-machine-language/{DOSSIER.md,DECISIONS.md}`.

## G. Sibling repos (READ-ONLY — cite, never write)

| Repo | Path | Top-level | Used by |
|---|---|---|---|
| GLP (language, upstream) | `D:\bstdev\research\glp\GLP\` | `glp/`, `glp_runtime/`, `glp_multiagent/`, `programs/`, `plays/`, `docs/`, `specs/`, `AofGLP/` | Corpus (§A), parity upstream (§B). |
| qhstate | `D:\bstdev\research\qhstate\` | `src/`, `docs/`, `specs/`, `tests/`, `codeconv/`, `CMakeLists.txt` (C++ / LLVM-Clang) | P7 (QHSM), ANTLR `.g4` precedent (T008). |
| qhstate-Yngenios | `D:\bstdev\research\qhstate-Yngenios\` | `src/`, `Csharp/`, `specs/` (incl. `034-*` full pipeline, `023-aok-os-synthesis`), `synthesis-os/`, `zephyr/`, `ports/aok/`, `vendor/rtos-kernels-cxx23/aok/`, `codeconv/`, `workflows/`, `tools/`, `tests/`, `docs/`, `examples/` — a ~3820-file QHSM tree; `.git` is a **worktree of `qhstate`**. **(CORRECTED 2026-06-29 per P7 DOSSIER.md:120 — was mislabeled "stub".)** | P7 (QHSM/YngeniOS; AOK C++23 port + spec-034 pipeline) — the real YngeniOS grounding. |
| mstack-coop | `D:\bstdev\research\mstack-coop\` | **thin — coordination notes only**; only structured dir is `COOP/`; **no `docs/` dir** (`COOP/README.md`, `architecture-evidence-captured.md`, `task-diana-research.md`, `note-phaseb-gabi-named-components.md`). **(CORRECTED 2026-06-29 — the prior `src/…/synthesis-os/` listing here actually described qhstate-Yngenios.)** | P7 (YngeniOS microkernel context; NATO-DIANA tender notes). |
| olamnit | `D:\bstdev\research\olamnit\` | `Olamnit/`, `Olamnit.EdgeHost/`, `docs/`, `specs/`, `COOP/` (C#/.NET edge) | P7 (RTOS / edge host). |

## H. Gaps & cautions (record-the-gap, do not fabricate — spec Edge Cases)

- ⚠️ **`MSTACK/docs/diana` does NOT exist.** `tasks.md` T010 grounds P7 on "`MSTACK/docs/diana`".
  There is **no `MSTACK/` repo** — the closest is `mstack-coop/`, which has **no `docs/diana`**.
  → P7 (T010) must treat the `diana` grounding as **missing**; mark any design resting on it
  **provisional** (spec: *"A sibling repo is inaccessible or ambiguous → the gap is reported; the
  affected design is marked provisional"*). If `diana` material is needed, escalate to the owner.
- ✅ **`qhstate-Yngenios` is NOT a stub** (CORRECTED 2026-06-29 per P7 direct `ls`, DOSSIER.md:120).
  It is a ~3820-file QHSM tree whose `.git` is a worktree of `qhstate`; `specs/034-*` carries the full
  pipeline artifact set, and `vendor/rtos-kernels-cxx23/aok/` + `ports/aok/` hold the real AOK C++23
  port (spec-023, `Status: Draft`). P7's YngeniOS grounding cites it firsthand. The
  "coordination-notes-only / thin" label belongs to **`mstack-coop`** (only structured dir `COOP/`, no
  `docs/`) — the original index transposed the two siblings.
- The sibling `GLP` repo duplicates much of glpnet; for this program cite the **in-repo** copy and
  note the sibling as upstream where load-bearing (avoids ambiguity over which tree is canonical).

## I. Consumed-by map (pipeline → primary sources)

- **P4 faithfulness** (T004–T005): §A (corpus), §B (Dart runtime), §C (034 parity), `multi-protocol-link-layer/corpus/` (M2), §E (proofs).
- **P1b realignment** (T006): `repl-engine-separation/` dossier + reconciliation, `pipelines/P1/` (superseded — read for *why it failed*), `pipelines/P5-…/DECISIONS.md`, P4 PARITY-BAR.
- **ANTLR deep-dive** (T008): §F (the spike), `qhstate/` `.g4` work, `glp_runtime/lib/compiler/`.
- **P6 Gleam/AtomVM** (T009): `gleam-atomvm/`, `glp_gleam/src/`, `docs/glp-bytecode-v216-complete.md`, §A PDF.
- **P7 QHSM/YngeniOS** (T010): §G (read-only) — mind §H gaps.
- **P2 concerns / P3 opportunities** (T011/T012): all of the above as evidence.
- **P8 synthesis** (T007): every artifact above + the P4 criteria + ED-1…ED-6 obligations.

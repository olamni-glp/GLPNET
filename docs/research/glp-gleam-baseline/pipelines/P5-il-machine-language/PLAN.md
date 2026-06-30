# P5 — Parser → IL → Machine-Language Strategy (research pipeline plan)

**Owner:** Gabi · **Plan date:** 2026-06-26 · **Status:** structured research plan (read-only).
Part of the 036 program. Built and run with the LEJEPA/Yngenios/Beacon/MSTACK multi-stage
pipeline approach (ground → web → design → adversarial review → synthesis), with the anti-bias
rules forced after the P1 failure.

## Owner questions this pipeline must answer concretely

- **Q1** How do logic languages structure the parser→backend hand-off (intermediate language and/or
  a logic abstract-machine language)?
- **Q2** Can the GLP front-end compile **directly** into a logic machine language — Shapiro's
  documented FCP/GLP abstract machine, or glpnet's existing **v2.16.3 bytecode ISA** — making the
  machine language itself the seam, with **no separate IL**?
- **Q3** Or do we need an IL — and would a **logic-centric IL** (the prototyped GLP/FCP MLIR dialect)
  **help generate the machine language better** inside the front-end (even if the ML is what crosses
  to the back-end)?
- **Q4** How does the chosen seam achieve **clean front/back separation** (in-process AND
  over-the-wire bindings; thin/heterogeneous front-end; an ANTLR-defined grammar feeding it)?
- **Q5** Does it preserve **all goals** — M1 single-instance faithfulness, M2 linked parity vs
  Dart/C#, and the Gleam/AtomVM constraints?
- **Q6** How does it **actually work in practice** for GLP (committed-choice, three-phase, SRSW,
  suspension/reactivation, the two-cell writer/reader variable model)?

## Verified grounding facts (read on disk 2026-06-26)

- Current ML = **v2.16.3 bytecode ISA** (`docs/glp-bytecode-v216-complete.md`), WAM/FCP-shaped,
  two-cell variable model, three-phase, suspension records; emitted by
  `glp_runtime/lib/compiler/codegen.dart`, executed by `glp_runtime/lib/bytecode/runner.dart`;
  opcode tables `glp_runtime/lib/bytecode/opcodes_v2.dart`.
- Front-end pipeline: `lexer.dart → parser.dart (hand-written recursive descent) → ast.dart →
  partial_evaluator.dart → pmt/type_checker.dart → analyzer.dart → codegen.dart` (`compiler.dart`).
- Faithfulness spec = `GLP_IMPLEMENTATION.pdf` (Shapiro, arXiv:2602.06934 — dGLP/madGLP, writer-MGU,
  SRSW, Appendix A traces) + `Art-of-GLP-2025/formal.tex`.
- Shapiro's machine language = **FCP Sequential Abstract Machine** (Houri & Shapiro, CS86‑20, 1986;
  Collected Papers ch.38) — get via web + the `research-programme.md` Axis 1 citations.
- Logic-IL prototype = `docs/research/repl-engine-separation/reconciliation/MLIR-GLP-DIALECT.md`
  (4 primitives HEAD-unify/GUARD-test/BODY-spawn/suspend-reactivate; real-MLIR spike PASS,
  `spikes/mlir/RESULT.md`) + `research-programme.md` Axis 1 (FCP machine, WAM skeleton, CARMEL‑2
  29 ops, BinWAM, KL1/KLIC, module/computation split) + the `#12` ANTLR memo.

## Pipeline stages

1. **Ground (parallel, direct source reading, cite file:line/page):** G1 current ML + compiler path;
   G2 Shapiro semantics (the PDF + formal.tex) → semantic obligations the seam must preserve, and
   whether any machine language is *defined* in the papers; G3 the repo's IL/ML research
   (research-programme, MLIR dialect + spike, #12 memo, llvm-feasibility); G4 faithfulness/seam +
   separation requirement + Gleam/AtomVM constraints.
2. **Web research (parallel, re-ground every finding to a primary source):** W1 WAM & abstract
   machines; W2 concurrent-logic machines (FCP sequential abstract machine, CARMEL‑2, KL1/KLIC,
   Strand/PCN); W3 modern logic IRs & multi-target backends (Mercury HLDS/MLDS/LLDS, BinProlog/BinWAM,
   SWI/GNU `.qlf`/`.wbc`, MLIR logic/relational dialects, Datalog-on-MLIR/LingoDB); W4 IL-as-seam /
   thin-client / decoupling a front-end from a backend via a stable IR, and ANTLR→IR pipelines.
3. **Design (parallel, diverse lenses):** D1 *direct-to-machine-language* (no IL; ML is the seam);
   D2 *logic-IL then lower* (ANTLR → logic-IL → ML; IL is the optimization/verification/multi-target
   layer + seam); D3 *IL aids ML generation in the front-end* (IL used inside the front-end to
   generate a better ML; the ML is what crosses) — Gabi's hypothesis.
4. **Adversarial review + synthesis-of-approach:** critique each design against Q1–Q6 + faithfulness +
   separability + AtomVM feasibility; merge into one recommended design.
5. **Synthesis dossier:** answer Q1–Q6 head-on; give the recommended seam design; present genuine
   forks as **options-with-consequences for the owner**; propose a small **verifiable spike**
   (e.g. one GLP clause: ANTLR-parse → logic-IL → ML round-trip vs the existing codegen).

## Anti-bias rules (forced)

Cite file:line / page / URL for every claim. No "fastest-path" rubric. Judge on separability,
maintainability, analyzability, multi-target reach, faithfulness, Gleam/AtomVM fit — not speed.
Re-ground web findings to primary sources. Present forks as owner options; never self-decide them.
No buildkit-DB calls (avoids pgdb lock contention). Output under
`docs/research/glp-gleam-baseline/pipelines/P5-il-machine-language/`.

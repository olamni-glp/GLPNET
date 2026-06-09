# Refinement Method — shared GEPA/DSPy + formal-metrics methodology for `engine-separation`

Cross-cutting note for the epic. Every seed's `/buildkit-specify` instantiates this
method; it is the de-facto spec deliverable of seed **#1a**
(`iterative-refinement-and-verification-framework`). Owner decisions live in
[`DECISIONS-FOR-OWNER.md`](DECISIONS-FOR-OWNER.md); per-seed detail in the numbered memos.

---

## 1. The GEPA/DSPy refinement loop (Claude-run, NO external API)

**Hard rule.** All LM-backed refinement runs **in Claude via Agent-tool seams / MCP** —
never OpenAI, litellm, or `OPENAI_API_KEY`. This mirrors the in-repo precedent
`codeconv/src/codeconv/tools/codegen_opt/optimize.py:257-335` (the `run_optimize` seam:
`generate_fn` / `propose_fn` / `oracle_fn` / `BudgetCounter`) with the no-API rule at
`:6-11`. Any contract line that mandates an API is a defect to delete, not a constraint.

The loop, per seed:

```
seed   = methodology draft / candidate artifact or code diff
loop:
  candidate ← Claude sub-agent drafts/edits the artifact or C# diff
  evaluate  ← run the seed's metric-combination table (§2) to thresholds
  if all thresholds met → terminate (owner confirms at the interactive spec step)
  reflections ← list of unmet criteria + feedback
  GEPA mutation ← Claude proposes a revised candidate from reflections
  DSPy step    ← refine the instruction set driving the drafting agent
  repeat (budget-capped; capped run yields best-so-far)
```

Termination is **metric-thresholds-met AND owner confirmation** at the interactive
`/buildkit-specify` step — not budget exhaustion alone.

---

## 2. The metric-combination principle: pragmatic + formal, always both

Every seed declares a metric table — **name | kind (pragmatic|formal) | tool |
threshold** — and **must blend pragmatic and formal** entries (brief §3.1–§3.3). The
shared Markdown table template is a #1a deliverable (decision U-M1).

- **Pragmatic** = behavioral / structural gates runnable today: the REPL suite
  (`bash test/run_all_tests.sh`, 384/384), round-trip identity tests, cross-process
  loopback equivalence, capture-coverage tests, `grep` invariants, ResourceSnapshot
  baselines, footprint measurement (massif / VmRSS).
- **Formal** = mechanized or decidable properties: byte-parity golden files (FR-060/061),
  the in-repo type-checker + SRSW validator (REPL suite sections B/C/D), Z3/CVC5
  discriminant-uniqueness, and Lean 4 / Rocq proofs.

The **byte-parity round-trip oracle** `decode(encode(p)) ≡ p` (FR-060/061 precedent,
`csharp/glp_link/reliability/FrameCodec.cs:31-32`) is the formal anchor for every codec
seed and the deterministic checker that mitigates the "LLMs struggle with IR control flow"
risk (brief §3.2) — Claude generates codec *structure*; the oracle verifies *correctness*.

---

## 3. Proof-assistant policy: evaluate both, pick best-fit primary, keep the alternative only where real

**Policy.** Each seed evaluates Lean 4 *and* Rocq, names one **primary** (best fit), and
records an **alternative_when** only where a concrete trigger makes the alternative
genuinely better.

**Verdict across the epic.**
- **Lean 4 is the primary on all 11 prover-needing seeds.** Decisive properties are clean
  inductive facts over finite types (round-trip identity, depth-bounded resolution,
  unbound-sentinel correctness, suspension/monotone-binding invariants, fan-in
  stream-merge). **Lean-LSP-MCP + APOLLO (sorry-isolation, model-agnostic) + Lean Copilot
  are Claude-native** and satisfy the no-API rule with zero adaptation. Owner-stated
  preference is to evaluate Lean 4 first (brief §3.2a).
- **Rocq is never the primary; it is the alternative on 9 seeds**, genuinely needed only
  when the property crosses into:
  - **full verified-compiler bisimulation** (`decoded-execute ≡ direct-compile`) — #4, #11,
    #14 — where TWAM (arxiv 1801.00471), the verified Prolog→WAM compiler (ScienceDirect
    0743106692900547), and Vellvm are Coq/Rocq prior art; or
  - **coinductive reasoning over unbounded streams** — #13 (non-terminating multi-client
    sessions) — where Rocq's `cofix`/`pcofix` infrastructure is more mature.
  Choosing Rocq there requires **adapting AutoRocq off its GPT-4 dependency** to Claude
  (a no-API defect to fix, brief §3.2a).
- **6 seeds need no prover** (#3, #8, #10, #16, #1.5; #1a only *specifies* the layer):
  callback-wiring, OS-host integration, transport refactor, research, and the supersession
  decision reduce to code review / `dotnet build` checks / Z3 finite-domain exhaustiveness.

---

## 4. The formal-tooling layers (specified by #1a, implemented by later seeds)

- **ANTLR4 grammar-as-verifier** (#12 Phase-A): a single `.g4` GLP grammar that **parses
  every working-definition example** before any compiler exists (brief §3.2). Threshold:
  100% of the `programs/` corpus accepted; rejection-preservation on the negative suite
  (grammar accepts syntactically-valid-but-type/SRSW-invalid programs). This is the early
  formal gate for all language-touching seeds.
- **MLIR GLP/FCP IL-dialect** (specified by #1a/#4; lowering pass in #11; deferred for
  #14/#15): primitives **HEAD-unify / GUARD-test / BODY-spawn / suspend-reactivate**,
  progressively lowered, verified via first-class verification dialects (PLDI'25).
  Round-trip criterion `decode(encode(p)) ≡ p`. **Citation to pin** (brief §6): the
  Typed-Multi-level-Datalog-IR reference — arxiv 2502.06854 is **mis-attributed** (it is an
  LLM-comprehension-of-LLVM-IR study); correct ref is **LingoDB, VLDB 2022 (Jungmair et
  al.)**.
- **Byte-parity oracle** (FR-060/061): every codec (#4, #5, #11) carries a golden-file
  round-trip identity test; cross-runtime Dart parity where the mirror is kept (§12r7).

---

## 5. The Shapiro / embedded-switch pragmatic anchor

Each seed's pragmatic criteria are framed as: **does this step preserve the
Shapiro/GLP semantic guarantees** of the engine acting as an **embedded switch** between
(a) external connectivity via the GLP link layer (`csharp/glp_link/`) and (b) internal OS
actions (QHSM/HSM actors, classical OS tasks)? The five criteria and their gates:

| criterion | enforced by | mandatory for |
|---|---|---|
| Committed-choice concurrency | post-quiescence result collection; single-owner heap (heap_fcp.cs:136-141) | #2, #5, #6, #13 (language/wire/execution) |
| SRSW (single-reader/single-writer) | in-repo SRSW validator (REPL suite §D); codec must not alias var indices | #2, #5, #11, #12 |
| Suspension correctness | faithful `SuspendedGoals`/`BlockingReaders` (scheduler.cs:67-73); re-wire proof | #2, #5, #7, #9 |
| Monotone variable binding | snapshot-at-quiescence verbatim Cells; never re-bind | #7, #9 |
| Three-valued unification (Success\|Suspend\|Fail) | faithful Status projection (scheduler.cs:33-43); no fourth outcome | #2, #5, #6 |

**Mandatory-vs-advisory mapping (decision U-M2, advisory):** mandatory for seeds touching
GLP language (#11, #12), execution semantics (#2, #4, #5), or the wire/byte contract (#3,
#4, #5); **advisory** (record N/A + justification) for host/infra seeds (#8, #10). The
embedded-switch test per criterion: run a GLP goal spanning an external link
(`link_recv`, self.glp:548) through the switch seam and confirm the criterion holds.

---

## 6. Seed → applicability + primary prover + headline metric combination

| # | feature_id | GEPA/DSPy | prover (alt) | headline metric combination |
|---|---|---|---|---|
| 1a | iterative-refinement-and-verification-framework | methodological | lean4 | template-completeness + GEPA-seam-coverage + no-API grep (prag) · Shapiro-map + formal-slots + tactic-loop arch (formal) |
| 2 | result-envelope-and-deep-resolve | methodological | lean4 | REPL 384/384 + self-containment + round-trip display (prag) · depth-truncation Lean 4 + SRSW (formal) |
| 3 | structured-output-capture-seam | methodological | n/a | REPL + OutputCallback/TraceSink capture + Console.Write=0 (prag) · type/SRSW gate (formal) |
| 4 | il-codec-spike | methodological | lean4 (rocq) | round-trip identity + execute-equiv + coverage (prag) · Lean 4 `decode∘encode=id` + Z3 discriminant-uniqueness (formal) |
| 5 | result-codec-and-framecodec-ride | methodological | lean4 (rocq) | loopback equiv + round-trip + output (prag) · byte-parity golden + Lean 4 unbound-sentinel + SRSW (formal) |
| 6 | repl-engine-process-split-mvp | methodological | lean4 (rocq) | REPL equiv + loopback + kill-restart (prag) · FrameCodec byte-parity+CRC + SRSW + type (formal) |
| 7 | engine-state-snapshot-and-persistence-api | methodological | lean4 (rocq) | kill-restart EquivTrace + snapshot round-trip + dual-store (prag) · monotone-binding + suspension-chain invariants (formal) |
| 8 | liveness-crash-restart-host | methodological | n/a | liveness-on-schedule + crash-exit + restart + REPL (prag) · FR-057 csproj + exception-taxonomy exhaustiveness (formal) |
| 9 | restore-and-resume-with-link-reestablish | methodological | lean4 (rocq) | kill-restart correctness + ResourceSnapshot baseline + REPL (prag) · suspension-after-rewire + monotone-binding (formal) |
| 10 | multi-accept-transport-extension | methodological | n/a | multi-client round-trip + LinkId uniqueness + pump stress (prag) · SRSW + byte-parity-unchanged + FR-057 (formal) |
| 11 | compiled-il-on-the-wire-and-factor-out-compiler | methodological | lean4 (rocq) | REPL + compile→encode→decode→execute equiv (prag) · IL byte-parity + Lean 4 round-trip + ModuleTerm round-trip + SRSW (formal) |
| 12 | antlr4-shared-grammar-spike | methodological | lean4 (rocq) | grammar accepts 100% corpus + rejection-preservation + execute-equiv (prag) · byte-level parity + token-coverage + SRSW-AST (formal) |
| 13 | multi-client-control-program-in-glp | methodological | lean4 (rocq) | N-client round-trip + serve/2 equiv + kill-restart-N (prag) · type/SRSW non-mwm + **mwm stream-merge Lean 4** (formal) |
| 14 | cpp-engine-feasibility | methodological | lean4 (rocq) | execute-equiv corpus + footprint + round-trip fidelity (prag) · SRSW + 3-valued-unification + byte-parity cross-runtime (formal) |
| 15 | many-instances-shared-static-memory-cooperative-scheduling | methodological | lean4 (rocq) | per-instance memory + preempt-equiv + boundary-safety (prag) · SRSW/suspension-across-preempt + footprint sub-linearity (formal) |
| 16 | research-programme-and-llvm-feasibility | **low** | n/a | report completeness + prior-art coverage + LLVM verdict + citation correctness (prag) · — |
| 1.5 | repl-engine-split-mvp-…-c (monolith) | methodological (decision) | n/a | roadmap consistency + traceability + no-forward-deps + REPL (prag) · — (structural supersession decision) |

**Reading the table.** `methodological` = the loop drives artifact/code iteration to
thresholds. `low` (#16) = a research/organizational deliverable; close it rather than
iterate. `(rocq)` = Rocq is the *alternative*, triggered only by the conditions in §3 —
no seed makes Rocq primary.

# Seed Reconciliation Memo — #16 research-programme-and-llvm-feasibility

**Feature id:** `research-programme-and-llvm-feasibility`
**Dossier entry:** §11 #16
**Kind (dossier):** EXPERIMENT
**Date:** 2026-06-09
**Branch:** 026-engine-review-dossier

---

## Dossier cross-references

- §10.10 — Deferred research dimensions: §2a ANTLR4 shared grammar, §2b C++ engine,
  §7a two-tier shared/instance memory + cooperative run-to-completion, §7b LLVM
  staging — all framed as EXPERIMENT features, do not gate the MVP.
- §0.4 (classification table) — no row for this seed: the research programme is
  read-only investigation output, not a code artefact.
- §0.3 (source inputs) — `docs/research/repl-engine-separation/research-programme.md`
  and `docs/research/repl-engine-separation/llvm-feasibility.md` are explicitly named
  as inputs feeding §11 #16.
- §2.1/§2.2 — IL codec need that the prior-art survey (Axis 1 of research-programme.md)
  directly informs: opcode encoding, WAM/FCP lineage, one-codec-for-wire-and-persistence.
- §6.3 / §6.4 — Persistence / restore-and-resume, informed by Axis 4 (orthogonal
  persistence, DBOS, Napier88, SBCL, BinProlog continuation-as-term).
- §4.3 / §7 — Control-program model (GLP-written vs OS-level mailbox), informed by Axis
  3 (cooperative scheduling, BEAM run-to-completion, FCP resolvent model).
- §9.1 / §9.2 — Premise reconciliations (compiler location; no runtime IL synthesis),
  validated by the research programme's conclusion: serialize the existing ISA, do not
  invent a new one.
- §12 risk 7 — Cross-runtime byte-parity, directly addressed by the prior-art survey's
  recommendation (one codec for wire + persistence, WAM category skeleton).
- Appendix B — Seed registry row #16: motivating §-anchor §10.10 only.

---

## Seed-vs-dossier-vs-code

### Roadmap brief (stored profile)

The `buildkit-roadmap brief` output reads:

> EXPERIMENT. The internet-research programme (FCP/WAM/KL1-KLIC/BinProlog IL prior
> art) + staged LLVM scout->deepen->spike. Informs IL design + optimization; mostly
> parallel, gates nothing critical. Reports already drafted
> (research-programme.md, llvm-feasibility.md). depends-on: #1. (§7 #16)

Stored WSJF = 3, RICE = 533. Effort = S. Problem/need, Target-user, Value, Risk fields
are blank.

Key divergence: stored notes say "(§7 #16)" — the `§7` is the stale investigation.md
numbering artefact also seen in seed #10 and #4. Current dossier anchor is §11 #16,
§10.10. Non-substantive.

Stored notes say "mostly parallel"; the dossier says "runs parallel, gates nothing
critical" — semantically identical.

Stored notes say "Reports already drafted" — confirmed: both `research-programme.md`
(523 lines, four axes, staged spikes) and `llvm-feasibility.md` (287 lines, CONDITIONAL
verdict) exist at `docs/research/repl-engine-separation/`.

### Dossier entry (§11 #16)

| Field | Dossier value | Stored value |
|---|---|---|
| Kind | EXPERIMENT | EXPERIMENT (matches) |
| Scope | Internet-research programme (FCP/WAM/KL1-KLIC/BinProlog IL prior art) + staged LLVM scout→deepen→spike | matches |
| Why | Informs IL design + optimization; runs parallel, gates nothing critical | matches |
| depends_on | 1 | 1 (matches) |
| §ref | §10.10 | (§7 #16) — stale numbering, see above |
| Effort | S (stored) | not in dossier table |
| WSJF | 3 | not in dossier (advisory scores) |
| RICE | 533 | not in dossier (advisory scores) |

### As-built code check

This seed is a **read-only investigation** — it does not claim to produce code artefacts.
Code checks are confirmatory:

1. **No IL codec anywhere** — `out/csharp/lib/bytecode/runner.cs:41` confirms
   `BytecodeProgram` is an in-memory object (`IReadOnlyList<object> Instructions` at
   `:44`); `ToDisassembly()` at `:88` is human-readable only; no `Serialize`, `Encode`,
   or `ToBytes` method exists in `runner.cs`, `opcodes.cs`, or `opcodes_v2.cs`. This
   confirms the research programme's starting premise: the IL codec is net-new
   (dossier §0.4, §2.1).

2. **Dual v1/v2 opcode split is live** — `out/csharp/lib/bytecode/opcodes.cs` and
   `opcodes_v2.cs` both exist and contain distinct opcode families. The research
   programme's Axis 1 analysis of this split (mode-as-bit-field, `isReader` flag,
   GLP bytecode v2.16.2 history) is grounded in current file structure.

3. **No ANTLR grammar file in the repo** — no `.g4` file found. This confirms the
   ANTLR4 shared-grammar goal (§10.10 §2a, research-programme.md §4 "ANTLR4
   shared-grammar" side-investigation) is genuinely net-new, not a reuse.

4. **No LLVM/MLIR dependency** — no LLVM or MLIR references anywhere in
   `out/csharp/`. The llvm-feasibility.md CONDITIONAL verdict (do not base the §3
   binary IL on LLVM/MLIR) accurately reflects the zero-footprint baseline.

5. **research-programme.md and llvm-feasibility.md are complete** — both files exist,
   authored 2026-06-08, with comprehensive prior-art survey (four axes, 40+ sources),
   binary IL comparison table (FCP/WAM/BinProlog/KL1/KLIC/GNU-Prolog/SICStus vs
   glpnet GLP v2.16.3), and LLVM scout-stage verdict. The research artefacts this seed
   promises to produce already exist.

**Critical implication:** the reports are drafted, but the research programme defines
a set of **code-level spikes** (SPIKE-1 through SPIKE-5 in research-programme.md §5)
that remain to be executed. The seed as stored covers the internet-research phase plus
the LLVM scout stage; the spikes belong to later seeds (#4, #7, #9, #14, #15) by the
dossier's topology.

---

## Classification check

**Kind: EXPERIMENT — correct.**

The scope is "internet-research programme + staged LLVM scout→deepen→spike." As-built,
both reports already exist (`research-programme.md`, `llvm-feasibility.md`). The
EXPERIMENT classification is correct: it de-risks IL design decisions for dependent
seeds (#4 il-codec-spike, #7, #11, #14) by establishing prior-art baselines. It
produces no production code. The §10.10 anchor is the right citation — these are
explicitly "deferred research dimensions" that do not gate the MVP.

The WSJF=3 / RICE=533 / Effort=S scoring is plausible for a non-gating research seed
whose primary deliverables are already drafted. The S-effort estimate matches "scout
stage complete; deeper/spike stages are gated" per the llvm-feasibility.md §3
recommendation.

No classification mismatch.

---

## Tensions

### T1: Scope creep risk — reports done, but the seed's "staged LLVM" scope includes a conditional deepen and spike

**Evidence:** `llvm-feasibility.md` §4 defines a "timeboxed ~1 week throwaway spike"
conditioned on a C++ engine variant being built AND profiling showing numeric kernels
as a bottleneck. This spike is part of the #16 scope as stated. However, the C++
engine feasibility work is seed #14 (depends_on #4, #12 — far later in the topology).
If #16 includes the LLVM spike, it has an implicit forward dependency on #14 — which
violates the dossier's topological no-forward-deps invariant (§11 "every depends_on
references a strictly smaller number").

**Options:**
1. Narrow #16's scope to "scout only + prior-art reports" (already done); declare the
   LLVM deepen/spike a sub-task of #14. The internet-research scope is fully delivered.
2. Keep the broader scope but add explicit gating text: "LLVM deepen/spike activates
   only after #14 confirms a C++ engine variant is being built." Treat #16 as a
   multi-phase EXPERIMENT with the spike phase hibernated.
3. Split into #16a (internet-research programme, done) and #16b (LLVM deepen/spike,
   blocked on #14 gate) — adds a roadmap entry but makes the dependency explicit.

### T2: Citation gap — the Typed-Multi-level-Datalog-IR paper link is mis-attributed (brief §3.2 footnote)

**Evidence:** The brief (`SEED-RECONCILIATION-BRIEF.md:36`) explicitly flags:
"the owner-supplied link `arxiv.org/html/2502.06854v1` is actually an empirical study
of LLM comprehension of LLVM IR, not the Typed-Datalog-IR paper." The Typed-Datalog-IR
concept stands (LingoDB, MLIR `relalg`/`db` dialects, VLDB 2022 paper
`vldb.org/pvldb/vol15/p2389-jungmair.pdf` is confirmed), but the arxiv link is wrong.

**Options:**
1. Pin the correct citation during the #4/#12 spike as the brief instructs — no action
   in this seed; record as an open item.
2. Pin it now: the LingoDB VLDB paper (Jungmair et al., VLDB vol.15 p.2389, 2022,
   confirmed at `dl.acm.org/doi/abs/10.14778/3551793.3551801`) is the correct
   Typed-Datalog-IR precedent reference. The `2502.06854` link is retained only for the
   "LLMs struggle with IR control flow" finding (correctly stated in the brief).
3. Replace the owner's `2502.06854` link with the correct LingoDB citation in the brief
   and remove the "⚠ Citation note" caveat.

### T3: Scope overlap with seeds #4, #12, #14 — unclear which seed owns which research output

**Evidence:** Research-programme.md §4 defines spikes SPIKE-1 through SPIKE-5 under
Areas A–D. SPIKE-1 (chain-boundary quiescence) is the exact scope of — or at minimum
a dependency of — seed #7 (engine-state-snapshot). SPIKE-2 (one codec, two uses) maps
to #4 (il-codec-spike). SPIKE-4 (.NET shared-static feasibility) maps to #14 (C++
engine feasibility) or #15 (many-instances). The research-programme.md treats all
spikes as part of one document without explicit seed ownership assignment.

**Options:**
1. Let the overlap stand — the research-programme.md is an input document (§0.3) whose
   spikes feed later seeds; each seed's own spec owns the execution. No structural
   change needed.
2. Add a spike→seed ownership table to research-programme.md §4 (or a cross-ref in
   each later seed's spec) so there is no ambiguity about where each spike is
   implemented.
3. Promote #16's scope to "research programme + research coordination" — it owns the
   prior-art synthesis and spike *design*; individual seeds own spike execution.

---

## Under-specifications

### U1: LLVM spike gate condition is currently implicit

**Why it matters:** The spike in llvm-feasibility.md §4 has a stated gate ("only if
and when a §2b high-performance C++ engine variant is actually being built AND profiling
shows numeric kernels are a bottleneck"). But there is no formal trigger in the roadmap
or pipeline that surfaces this gate. Without it, the spike might be attempted prematurely
(before a C++ engine exists) or silently dropped.

**Options:**
- Add a formal `blocked_on: #14` dependency row in the roadmap for the spike phase.
- Record the gate condition in the seed's spec.md as a blocking criterion checked at
  `/buildkit-tasks` time.
- Defer; rely on human judgment at the time seed #14 is in progress.

### U2: "Research programme" completion criteria are undefined

**Why it matters:** The seed is WSJF=3 / Effort=S and says "reports already drafted."
But the research-programme.md defines four axes and 23 sub-spikes. What constitutes
"done" for this seed — is it the two reports? The LLVM scout verdict? The spike results?
A success criterion is needed for the `/buildkit-specify` exit gate.

**Options:**
- Done = the two reports published (already done) + the LLVM scout verdict (already done
  in llvm-feasibility.md §5). Mark as essentially delivered.
- Done = the two reports + a cross-reference table mapping each spike to a responsible
  seed (resolves U3/T3).
- Done = the two reports + execution of the five high-leverage spikes (scope far exceeds
  Effort=S; would require re-scoping).

### U3: The ANTLR4 shared-grammar dimension (§10.10 §2a) appears in research-programme.md §4 as a "staged side-investigation" but seed #12 owns it in the topology

**Why it matters:** Research-programme.md §4 last paragraph says "ANTLR4 shared-grammar
(§2a/§2b): one grammar → C#/.NET + Dart + C++ compiler front-ends producing the same
binary IL (§3). Separate investigation; not on the persistence-MVP critical path." Yet
seed #12 (`antlr4-shared-grammar-spike`) depends_on #11 (much later). If #16 starts
doing ANTLR4 work, it conflicts with #12's ownership.

**Options:**
- #16 explicitly excludes ANTLR4 work (delegate entirely to #12); note it as a
  "separate investigation" only (already stated in research-programme.md).
- #16 includes a brief ANTLR4 feasibility paragraph (scout only) that feeds #12.

---

## GEPA/DSPy refinement

### Applicability: low

This is a **research survey and feasibility investigation**, not an LM/codegen program
and not a system artefact that GEPA/DSPy would iterate on in the direct sense. The
primary deliverables (research-programme.md, llvm-feasibility.md) are authored
documents; the LLVM spike is a time-boxed throwaway experiment. GEPA/DSPy's
iterate-against-a-metric discipline applies **methodologically** (structure the
research questions as evaluable hypotheses; each spike is a candidate that the metric
combination evaluates), but there is no LM pipeline or code artefact to compile/optimize.

One partial direct application exists: when the seed eventually executes SPIKE-2
("one codec, two uses"), that spike produces a codec that can be tested with an
execution-equivalence harness — which is exactly the kind of GEPA/DSPy loop used in
seed #4. At that point the methodology is direct, but that execution belongs to seed #4.

### Seed definition

A read-only internet-research programme producing: (A) a prior-art baseline for GLP
binary IL design (FCP/WAM/KL1-KLIC/BinProlog lineage survey + binary encoding options),
(B) a shared/instance memory and cooperative scheduling survey (BEAM/V8/Wasmtime/KLIC
analogues), (C) an orthogonal persistence survey (Napier88/BinProlog/DBOS precedents),
and (D) a staged LLVM/MLIR feasibility scout with a CONDITIONAL verdict on going deeper.
Deliverables: `research-programme.md` (done) + `llvm-feasibility.md` (done). Residual:
spike-execution tasks owned by later seeds; LLVM deepen/spike hibernated until #14.

### Metrics combination

| Name | Kind | Tool/Harness | Threshold |
|---|---|---|---|
| Report completeness | pragmatic | Manual review against research-programme.md §4 Area/Spike checklist | All four axes addressed; each spike has a stated owner-seed |
| Prior-art coverage | pragmatic | Cross-reference count: FCP/WAM/KL1-KLIC/BinProlog/SICStus/GNU-Prolog all cited with binary-encoding decision | ≥6 IL systems cited with concrete "borrow" conclusion |
| LLVM scout verdict | pragmatic | llvm-feasibility.md §5 one-line verdict is documented + gate condition is explicit | Verdict captured; gate condition (C++ engine + profiling bottleneck) formally recorded |
| Citation correctness | pragmatic | Each source URL verified reachable + abstract confirms the claimed finding | 0 mis-attributed links (currently 1 open: 2502.06854 re Typed-Datalog-IR) |
| Seed→spike ownership table | pragmatic | research-programme.md §4 / §5 spikes mapped to dossier §11 seed numbers | Every spike in §5 has an explicit owner seed |
| Prior-art→GLP semantic criteria alignment | formal | Manual check: for each borrowed IL precedent, state which Shapiro criteria (SRSW, committed-choice, suspension) it preserves or conflicts with | All borrowed IL precedents annotated |

No formal proof-assistant metric applies: this seed touches no language grammar, no
wire/byte contract, and produces no code. The formal metric tier is vacuous here.

### Interactive spec step

At the start of `/buildkit-specify` for this seed, the owner confirms:
1. Is the seed's scope "scout only (reports already done)"? Or does it include SPIKE-1
   through SPIKE-5 execution? (Decides the effort estimate and completion criterion.)
2. What is the formal gate for the LLVM deepen/spike? (Blocked on #14, or on-demand?)
3. Does the ANTLR4 work belong here or entirely in #12?
4. Should the citation gap (2502.06854 / LingoDB) be patched here or in #4/#12?

### Refinement loop

Not directly applicable as a code-generation/optimization loop. If the spike phases are
eventually assigned here, the loop would be:

1. Draft spike design (e.g., SPIKE-2: one-codec-for-both).
2. Evaluate against: does the codec round-trip the v2.16.3 ISA? Does it handle both
   opcode families? Does a decode→execute round-trip yield the same result?
3. Mutate the codec design based on failures (GEPA reflective step: identify which
   opcode class or constant sub-type broke round-trip, fix encoding spec).
4. Repeat until all equivalence checks pass.

This is the direct-applicability case and belongs to seed #4's loop.

---

## Formal tooling

### Lean 4 vs Rocq evaluation

**Lean 4 fit:** This seed produces no formal proofs and no mechanized semantics. If the
LLVM spike were eventually executed, a correctness property of the form "the ground
kernel slice compiled to LLVM IR computes the same value as the GLP interpreter for
fully-ground input" could be mechanized — but this is a future possibility, not a
current deliverable. Lean 4 would be the natural choice for such a property due to
mathlib's arithmetic library. For this seed as scoped, Lean 4 is not needed.

**Rocq fit:** Similarly vacuous. Rocq/Vellvm would be relevant if the GLP IL were
being formally specified at the byte level (which belongs to seeds #4 and #5). No
Rocq application here.

**Primary:** n/a — this seed produces no mechanized proof. The formal tooling decision
is deferred to seeds #4 (IL codec round-trip verification) and the
`1a-iterative-refinement-and-verification-framework` (which owns the proof assistant
choice framework).

**Alternative_when:** none.

**IL verification:** n/a — this seed produces no IL codec. The IL verification strategy
(TWAM-style certifying abstract machine; byte-parity proofs FR-060/061; MLIR
verification dialect as higher-level layer) is established in the brief (§3.2) and
applies to seeds #4, #5, #11.

---

## Shapiro criteria preserved

This seed is read-only investigation producing design guidance. It does not implement
any runtime component. The Shapiro criteria are relevant as **evaluation criteria for
the prior-art borrowings** — each surveyed system's borrowed design must be assessed
against whether adopting it would preserve GLP's semantic guarantees:

1. **Committed-choice concurrency** — the research programme's Axis 1 conclusion (adopt
   FCP/WAM opcode taxonomy, not LLVM) is directly driven by the finding that LLVM
   cannot represent committed-choice concurrency (llvm-feasibility.md §2.3). Any
   IL design borrowed from this research must not introduce a backtracking mechanism.
2. **SRSW (single-reader/single-writer)** — the mode-as-bit-field (`isReader` flag)
   design borrowed from GLP v2.16.2 must be faithfully encoded in any binary IL; the
   research programme's recommendation to serialize the existing ISA directly preserves
   this. No borrowed system may relax the SRSW discipline.
3. **Suspension correctness** — the BinProlog "continuation-as-term" insight (a
   suspended goal's continuation is a serializable term) is adopted; this must preserve
   the semantics of goal suspension on an unbound reader and reactivation on binding.
   The research programme explicitly requires encoding unbound readers/writers,
   suspensions, and partial structures (§4 Area D, item 2).
4. **Monotone variable binding** — once a writer binds a logic variable, it is permanent
   (committed-choice: no unbind). The recommended redo-only WAL (no undo log) is
   directly predicated on this: committed-choice ⇒ no backtracking ⇒ no undo needed.
   Any persistence or IL design that adds an undo path would violate this criterion.
5. **Three-valued unification (success / suspend / fail)** — the result envelope codec
   (informed by this research) must carry all three outcomes; the PayloadSerializer's
   throw-on-unbound design violates suspension correctness and is explicitly identified
   as insufficient.

For the embedded-switch framing: the research guidance (FCP/KL1 lineage, BEAM
process model, no LLVM) applies to the engine acting as a switch for connectivity and
internal OS/actor actions. QHSM/HSM actors hosted in the engine are processes in the
FCP/BEAM sense — each with its own goal queue and suspension lists, isolated heap.
The shared-static + per-instance-dynamic memory design (Axis 2) directly serves the
many-instances goal for hosting many HSM/QHSM actor instances.

---

## Recommendation

**Verdict: ALIGNED.** The seed as stored matches the dossier §11 #16 entry exactly in
kind, scope, why, and dependency. The two primary deliverables (research-programme.md
and llvm-feasibility.md) are drafted and complete. The seed is essentially at its
"scout stage done" completion point.

**Recommended action at `/buildkit-specify`:**

1. Narrow the scope to "scout stage + reports published" (already done) — declare the
   EXPERIMENT deliverable as the two documented reports plus the LLVM scout verdict.
2. Resolve tension T1 by adding explicit gating: the LLVM deepen/spike is blocked on
   seed #14 and not part of this seed's implementation scope.
3. Resolve U2 by defining done = reports published + spike ownership table added to
   research-programme.md §5 (a small documentation task, consistent with Effort=S).
4. Patch the citation gap (T2, option 2) during this seed: replace the mis-attributed
   `2502.06854` LingoDB link with the correct VLDB 2022 citation; retain the `2502.06854`
   link only under the "LLMs struggle with IR control flow" note.

---

## Options for owner

1. **Narrow and close (recommended):** Scope = reports done + spike ownership table +
   citation fix. Close this seed at specify time as a near-complete EXPERIMENT whose
   deliverables already exist. Low residual effort; preserves the Effort=S estimate.
   Consequence: LLVM deepen/spike is explicitly not part of this seed.

2. **Keep broad scope + hibernate LLVM:** Keep the full staged-LLVM scope but add a
   formal `blocked_on: #14` dependency for the deepen/spike phase. This seed remains
   active (open) until #14 resolves the C++ engine decision.
   Consequence: #16 stays open longer; creates a dependency edge not currently in the
   roadmap. Cleaner long-term but adds roadmap complexity.

3. **Split into #16a and #16b:** #16a = internet-research programme (done, close
   immediately); #16b = LLVM deepen/spike (captured as new seed, depends_on #14).
   Consequence: cleanest topology, most explicit, but requires adding a new roadmap
   entry and renaming.

---

## Open questions

1. Is the LLVM spike gate condition (`#14 confirmed + profiling bottleneck`) formal
   enough to be a `/buildkit-tasks` blocking criterion, or is it left to human judgment?

2. Should the spike ownership table (SPIKE-1→#7, SPIKE-2→#4, etc.) live in
   research-programme.md or in each downstream seed's spec as a cross-reference?

3. The owner exploration links (`arxiv 2601.14027` — Numina-Lean-Agent, 2026-01-20;
   `share.google/aimode/BMXNyJwRDQMcCyyrk`; `share.google/aimode/9AYccXYjLQz3cGXEW`)
   are cited in the brief as "to mine during spikes." Are these targeted at the LLVM
   feasibility spike specifically, or the Lean/formal verification framework (#1a)?
   The Numina-Lean-Agent paper (agentic Lean reasoning, model-agnostic) is relevant
   to #1a's formal tooling, not to the LLVM scout. Clarify ownership.

4. Does "staged LLVM scout→deepen→spike" in the seed scope mean this single seed
   implements all three stages, or is "deepen→spike" always conditional-and-later?

---

## External references

- **FCP sequential abstract machine** (Houri & Shapiro): `research-programme.md §1 Axis 1`;
  `sciencedirect.com/science/article/pii/0743106689900113`
- **TWAM: A Certifying Abstract Machine for Logic Programs** (Bohrer & Crary, VSTTE 2018):
  `arxiv.org/abs/1801.00471`; `cs.cmu.edu/~crary/papers/2018/twam.pdf`
- **First-Class Verification Dialects for MLIR** (Fehr, Fan, Pompougnac, Regehr,
  Grosser, PLDI 2025): `users.cs.utah.edu/~regehr/papers/pldi25.pdf`;
  `dl.acm.org/doi/10.1145/3729309`
- **LingoDB — Designing an Open Framework for Query Optimization and Compilation**
  (Jungmair et al., VLDB vol.15 p.2389, 2022): `vldb.org/pvldb/vol15/p2389-jungmair.pdf`;
  `dl.acm.org/doi/abs/10.14778/3551793.3551801` — the correct Typed-Datalog-IR
  precedent reference (replaces mis-attributed `arxiv 2502.06854`)
- **LLM comprehension of LLVM IR** (`arxiv 2502.06854v1`): retained only for "LLMs
  struggle with IR control flow" finding relevant to a Claude-driven IL codec
- **APOLLO: Automated LLM and Lean Collaboration for Advanced Formal Reasoning**
  (`arxiv 2505.05758`): model-agnostic agentic Lean framework; relevant to #1a
  formal tooling, not to the LLVM scout
- **Lean Copilot** (`arxiv 2404.12534`; `github.com/lean-dojo/LeanCopilot`): LLM
  inference natively inside Lean 4 (`suggest_tactics`, `select_premises`); relevant
  to #1a
- **AutoRocq** (`github.com/NUS-Program-Verification/AutoRocq`; `arxiv 2511.17330`):
  iterative LLM↔Rocq tactic loop, GPT-4 dependency to adapt away (per brief §3.2a);
  relevant to #1a
- **Vellvm — Verified LLVM** (`github.com/vellvm/vellvm`): Rocq formalization of LLVM
  IR semantics; template for verified-IL approach (relevant to seeds #4/#5, not #16
  directly)
- **Numina-Lean-Agent** (`arxiv 2601.14027`, 2026-01-20): agentic Lean reasoning
  system combining Claude Code with Lean-MCP — relevant to #1a formal tooling loop,
  not to the LLVM scout
- **KLIC (KL1→C compiler)**: `klic.kuicr.kyoto-u.ac.jp` / SAL;
  `link.springer.com/chapter/10.1007/3-540-58402-1_4`
- **BinProlog / BinWAM** (Tarau): `arxiv.org/abs/1102.1178`;
  `complang.tuwien.ac.at/ulrich/papers/PDF/binwam-nov93.pdf`

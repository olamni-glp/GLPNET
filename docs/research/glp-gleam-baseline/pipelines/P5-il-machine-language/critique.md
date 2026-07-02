# Adversarial Review — Three GLP Front/Back Seam Designs

**Shared strengths (all three).** Each correctly separates the *compile seam* (front-end→engine) from the *maGLP agent-link*, whose wire unit is globalized terms `T↑/T↓` + global names, never bytecode (G2 §3). Each bounds M1 at observable-outcomes (spec 034 line 27) and M2 at codec byte-parity + outcome-equivalent term exchange. The ANTLR fork is handled identically (no BEAM target, targets.md → Option A thin C#/Dart parser). So ANTLR is not a differentiator; the real fork is *whether an IL layer exists, and whether it crosses the wire*.

## Design 1 — direct-to-ML

**Strongest idea.** The seam already exists and is exercised: AST→bytecode with no IL (compiler.dart:56-128; codegen.dart:107-133), and the FCP/CARMEL/KL1 lineage proves a committed-choice ISA is tiny and directly targetable (W2; research-programme.md:212-213 "serialize the existing ISA — do not invent one"). Lowest new surface that still yields a real contract; aligns with the repo's own net recommendation and BinWAM/.qlf precedent (W3).

**Worst flaw.** It surrenders the analyzability layer ("no shared analysis/optimization layer", §7). SRSW, phase-ordering and writer-MGU stay *runtime assumptions* — exactly the gap that produced the two live F4 review bugs (self-bind→`Unbound`, suspension-drop; G4 §1). For a faithfulness-critical project judged on analyzability, that is the weakest axis, and §1 under-weights it relative to the codec mechanics.

**Ungrounded/overstated.** (a) "all three runtimes execute the identical frozen ISA → identical madGLP transactions → outcome-equivalent term exchange" overstates the causal chain — G2 §3 is explicit that M2 parity comes from the *term protocol*, which sits *above* the ISA; ISA-identity is neither necessary nor sufficient. (b) Reliance on feature-029 `IlCodec` to assert v2.16.3 codec feasibility leans on project memory, *outside* the on-disk corpus (G3(b): "outside these documents"); 029 is the C# `GlpRuntime`, not the Dart runtime. §7 honestly caveats "029 proves feasibility, not done here for v2.16.3," so this is borderline, not a falsehood.

## Design 2 — logic-IL-then-lower (IL **is** the seam)

**Strongest idea.** Static faithfulness verification: op-verifiers for phase-order HEAD<GUARD<BODY and single-writer/SRSW (MLIR-GLP-DIALECT.md:46-48) turn invariants from runtime assumptions into machine-checked well-formedness — the seam can *reject* a non-faithful program before any engine runs. Given the F4 bug history this is the single most valuable property on offer; one-IL→N-lowerings is the documented Mercury/LingoDB multi-target win (W3 Option B).

**Worst flaw (disqualifying as a *seam*).** MLIR is C++ infrastructure and **cannot run on AtomVM** (G4 §3; llvm-feasibility). So "the same IL artifact crosses in-process and over-the-wire to any back-end" is **false for the one back-end that matters** — the device can only receive *lowered bytecode* (AOT) or a hand-written Gleam interpreter. Making the IL the wire seam also contradicts two on-disk verdicts: research-programme.md:212-213 ("do not invent a new ISA") and llvm-feasibility.md:214-220 (CONDITIONAL-NO). Its reconciliation (MLIR-as-infra, bytecode-primary) is plausible but still posits a second normative artifact that is almost entirely unbuilt.

**Ungrounded/overstated.** "Serialized MLIR text byte-identical across Dart/C#/Gleam" presumes an MLIR presence on Gleam/AtomVM that does not and cannot exist (G4 §3) — the central unsupported leap. "Only a 4-op smoke spike PASSes" (RESULT.md, ILFRAG-1, WSL2) is correctly conceded (risk 2); production dialect, TableGen, verifiers, lowerings are deferred (#4/#11, MLIR-GLP-DIALECT.md:38-48).

## Design 3 — IL-aids-ML-generation (owner's Q3)

**Strongest idea.** It captures Design 2's verification/analyzability win *without* its AtomVM contradiction: the IL + verifiers + MLIR dependency stay **off-device**; only compact bytecode + a sequential interpreter ship to AtomVM (G4 dossier 134). Shipped precedent exists — BinProlog binarization is literally "a source-level transform that produces the ISA" (arxiv 1102.1178; W3 Option C). Directly answers the owner's stated hypothesis.

**Worst flaw.** Net-new layer over a *working* emitter (§7 risk 1, honest): justified *only* by analysis/optimization/multi-target ambition — if the project stays single-target Gleam with no optimization ambition, it is unearned overhead and Design 1 dominates (W3 decision pivot = backend count + analysis ambition). The MLIR-realization fork compounds this: real-MLIR carries a heavy C++/Python dep that ran only on WSL2 (RESULT.md:22-46), awkward inside a C#/Dart front-end; the lightweight-IR alternative means hand-building the very verifiers that justified the MLIR spike.

**Ungrounded/overstated.** §6/Q6 says invariants are "verified in the IL" — but those verifiers are the **#11 obligation, not built** (G3(a): "named as the #11 obligation, not built"). §3's "named obligation" wording is honest; the Q6 phrasing overstates current state to *verifiable-in-principle*. Shares Design 1's 029-for-v2.16.3 caveat (handled).

## Ranked verdict

**1st — Design 3.** Best across the owner's actual axes (separability, maintainability, analyzability, multi-target, faithfulness, AtomVM fit): keeps Design 1's proven simple seam *and* AtomVM fit, adds Design 2's verification/multi-target reach, and avoids both of Design 2's contradictions. Note Designs 2 and 3 share the IL layer and differ *only* in what crosses — and the AtomVM constraint resolves that fork toward bytecode (Design 3).

**2nd — Design 1.** Maximally grounded, lowest seam-risk, matches research-programme.md:212-213; but deliberately forgoes the analyzability layer, the weakest spot for a faithfulness-critical baseline. The genuine owner fork is **1 vs 3**: *build the IL at all?* — governed by analysis/multi-target ambition (W3).

**3rd — Design 2.** Highest analyzability ceiling, but fails the Gleam/AtomVM-fit axis *as a seam* (MLIR can't run on-device), conflicts with two on-disk verdicts, and is least-built. Its real value is reachable via Design 3.

## Synthesis should ADOPT
- Bytecode (v2.16.3) + **server-resolved, heap-independent result envelope** as the crossing artifact, identical in-process and on-wire (G4 §1.3, INV-5).
- 4-primitive GLP/FCP IL **inside the front-end** for static SRSW/phase-order/writer-MGU verification + indexing/guard-simplification — treated as **to-be-built (#11)**, not done (MLIR-GLP-DIALECT.md:46-48).
- Keep all compile-time machinery off the AtomVM device (G4 dossier 134); ship sequential bytecode interpreter + raw `erlang:spawn` (FR-010).
- Freeze + version the ISA and resolve the v1/v2 split **before** it crosses (SWI .qlf fragility, W3).
- Build the Section-15 codec to Dart↔C#↔Gleam byte-parity (FrameCodec/Crc32, G4 §2.5); de-embed `Object?`/`StructTerm` operands (G1 §5).
- Keep the maGLP agent-link (globalized terms) a **separate** seam (G2 §3); ANTLR Option A.

## Synthesis should AVOID
- IL/MLIR as the over-the-wire artifact to AtomVM (impossible on-device; contradicts research-programme.md:212-213).
- Lowering through LLVM's SSA core (can't represent a destructively-bound logic var, llvm-feasibility.md:110-122) — MLIR-as-infrastructure only, LLVM gated/optional.
- Citing 029 `IlCodec` as proof of the Dart v2.16.3 codec (C#, memory-only); must build + spike it, incl. **AtomVM bit-syntax** (G4 §3 gap).
- Treating ISA-identity as sufficient for M2 (parity is the term protocol + byte-identical codec).
- Building the IL absent declared optimization/multi-target ambition — owner must set ambition first (W3 pivot).
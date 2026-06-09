# Reconciliation Memo — #14 cpp-engine-feasibility

**feature_id:** cpp-engine-feasibility  
**dossier §-ref:** §10.10, §11 #14  
**kind (dossier):** EXPERIMENT  
**depends_on:** #4 (il-codec-spike), #12 (antlr4-shared-grammar-spike)  
**date:** 2026-06-09  
**roadmap state:** captured; WSJF=1.8, RICE=420  

---

## Dossier cross-references

| §-anchor | Content referenced |
|---|---|
| §10.10 | "Deferred research dimensions (non-gating): §2b C++ engine … deferred to EXPERIMENT features (§11 #14)" |
| §11 #14 | Scope + spike a C++ engine+scheduler+compiler-front-end on the same grammar/IL (footprint/perf/portability). Decisive for the many-instance goal. depends_on: 4, 12. |
| §11 #12 | antlr4-shared-grammar-spike — prerequisite: defines the single GLP grammar in ANTLR4 and confirms C#/C++/Dart parsers produce identical IL |
| §11 #4 | il-codec-spike — prerequisite: proves `BytecodeProgram`↔bytes round-trip (both opcode families) |
| §11 #15 | many-instances-shared-static-memory — EXPERIMENT/FOLLOW-UP that depends_on #14; the C++ engine informs and is itself informed by the shared/static memory design |
| §2.1 | `BytecodeProgram` structure — heterogeneous IL the C++ engine must consume |
| §2.2 | IL codec scope (opcode discriminant, v1/v2 families, recursive constants, labels, VariableMap) |
| §3 | Wire reuse decision; `FrameCodec`/`TcpTransport` as the transport substrate |
| §9.1 | Premise reconciliation: compiler is currently engine-internal; compiler relocation (needed for C++ front-end) is a deliberate follow-up |
| §0.4 | Classification table: IL/bytecode wire codec = net-new; transport = reuse |
| Appendix B | Two-way registry: #14 maps to §10.10; memo path `reconciliation/14-cpp-engine-feasibility.md` |

---

## Seed-vs-dossier-vs-code

### Roadmap brief vs dossier §11 #14

| Dimension | Dossier §11 | Roadmap brief |
|---|---|---|
| Kind | EXPERIMENT | (unstated in brief; implied by "EXPERIMENT" note) |
| Scope | "scope + spike a C++ engine+scheduler+compiler-front-end on the same grammar/IL" | "scope + spike a C++ engine+scheduler+compiler-front-end consuming the same ANTLR4 grammar + IL, for footprint/perf/portability" |
| Why | "decisive for the many-instance goal" | "decisive for the many-instance goal" |
| depends_on | #4, #12 | "#4, #12" |
| §ref | §10.10 | "(§7 #14)" — stale, references investigation.md numbering pre-decomposition |

**Divergence noted:** the roadmap brief still says "(§7 #14)" — a reference to `investigation.md §7` numbering that predates the dossier. This is a stale annotation; the authoritative §-anchor is dossier §10.10 + §11 #14.

### Code evidence for scope claims

The dossier and seed assume this experiment does not depend on any C++ code existing today. That is correct:

- **No C++ engine code exists** — `out/csharp/` contains only C# (`runner.cs:1-16` is explicitly a "Dart→C# conversion"; `glp_engine.cs:1` header "Converted from glp_runtime/lib/engine/glp_engine.dart"); no `.cpp`/`.h` files in scope.
- **No ANTLR4 grammar exists** — a repo-wide search for `*.g4` returns empty; `antlr`/`ANTLR` appears only in docs (`design-dossier.md`, `feature-definition.md`, `research-programme.md`, `investigation.md`). The experiment genuinely depends on #12 producing this artifact.
- **IL codec dependency confirmed:** `BytecodeProgram` (`out/csharp/lib/bytecode/runner.cs:41`) holds `IReadOnlyList<object> Instructions` with both v1 `IOp` (`opcodes.cs`) and v2 `IOpV2` (`opcodes_v2.cs`) plus `Label` markers. No `Serialize/Encode/ToBytes` method exists anywhere in `out/csharp/lib/bytecode/`. A C++ engine consuming the binary IL requires the codec from #4 to exist first.
- **Compiler is engine-internal** (`out/csharp/lib/engine/glp_engine.cs:487-493` — the `_RunSingleGoalAsync` method calls `new Lexer(...)` / `new Parser(...)` / `new CodeGenerator(...)` inline). Moving the compiler to a C++ front-end is the §9.1 relocation refactor — a hard dependency on #11 (compiled-il-on-the-wire + factor-out-compiler), not just #12.
- **Scheduler is a C# class** (`out/csharp/lib/runtime/scheduler.cs:93` — `Scheduler` class with `DrainResult` at `:30`). A C++ scheduler must independently implement the three-valued-unification loop, SRSW checks, suspension/reactivation, and fairness (currently in `out/csharp/lib/runtime/fairness.cs`, `suspend.cs`, `heap_fcp.cs`).
- **Heap is C# object graph** (`out/csharp/lib/runtime/heap_fcp.cs:148,154` — `List<HeapCell> Cells`, `int Hp`). A C++ engine needs its own heap — likely a flat `HeapCell[]` array, which maps naturally to C++ `std::vector<HeapCell>` or a fixed-size arena.

### Additional finding the dossier missed

The dossier's §11 #14 lists depends_on as `4, 12` only. However, code analysis reveals a **hidden dependency on #11** (compiled-il-on-the-wire + factor-out-compiler):

- The C++ front-end scenario in `feature-definition.md §2b` explicitly requires "the factored-out compiler and the REPL front-end in C++ — driven by ANTLR4" (`feature-definition.md:88-94`).
- The dossier's §9.1 Opt 2 (compiler relocation) is a prerequisite for a C++ client that can compile GLP source: without compiler relocation (#11), the C++ engine must either embed its own duplicate compiler or accept only pre-compiled IL from a C# front-end.
- This is a **scope ambiguity**: is the C++ spike (a) a pure execution engine (IL-in, result-out) or (b) a full front-end-to-engine implementation? The dossier and seed do not settle this distinction.

---

## Classification check

**Kind: EXPERIMENT — correct.** No implementation exists; the scope is to de-risk an unknown (C++ feasibility for the footprint/portability/many-instance goal). The EXPERIMENT kind matches the actual state of knowledge.

**Code support for scope:** the scope is correctly calibrated — `out/csharp/lib/bytecode/runner.cs:41` confirms the IL object graph exists and is the target for byte-round-trip from #4; `out/csharp/lib/runtime/scheduler.cs:93` and `heap_fcp.cs:148` confirm the C# execution substrate to be reimplemented. No file:line in the repo contradicts the EXPERIMENT framing.

---

## Tensions

### T1 — Scope ambiguity: pure C++ executor vs full C++ front-end

**Summary:** "C++ engine+scheduler+compiler-front-end" conflates two distinct scopes: (a) a C++ executor that consumes pre-compiled IL from a C# front-end over the wire, vs (b) a standalone C++ system with its own ANTLR4-generated compiler. These have dramatically different effort and dependency profiles.

**Evidence:** `feature-definition.md:85-96` says "we WILL need a C++ implementation of the engine + scheduler" and also "the factored-out compiler and the REPL front-end in C++"; dossier §10.10 defers both under one entry. The dossier §9.1 Opt 2 (compiler relocation, #11) is a prerequisite only for scenario (b). Scenario (a) depends only on #4 + #12.

**Options:**
1. Narrow the spike to scenario (a): C++ executor consuming wire IL; validate footprint/perf; defer compiler-in-C++ to a follow-up. Consequence: unblocked by #4 + #12; scope is tractable.
2. Expand the spike to scenario (b): C++ compiler front-end + executor. Consequence: adds depends_on #11; higher effort; validates the full language-definition-portability thesis.
3. Split into two seeds: #14a = C++ executor spike, #14b = C++ compiler front-end spike. Consequence: finer-grained roadmap; each has a clear depends_on chain.

### T2 — Depends_on chain missing #11

**Summary:** the dossier records depends_on as {4, 12}; but scenario (b) — a C++ compiler front-end — also requires #11 (compiler relocation out of the C# engine).

**Evidence:** `glp_engine.cs:487-493` (compiler internal to the C# engine); `feature-definition.md:88-96` (C++ front-end needs the shared compiler); dossier §9.1 Opt 2 = #11.

**Options:**
1. Add #11 to depends_on if the scope includes a C++ compiler front-end.
2. Constrain the spike to scenario (a) and keep depends_on = {4, 12}.
3. Record the dependency as conditional: "depends_on {4, 12, optionally 11} depending on scope decision T1."

### T3 — WSJF=1.8 / RICE=420 score vs late-in-sequence position

**Summary:** the seed is positioned late (#14 of 16), gated by #4 + #12 which are themselves non-MVP tracks. But the roadmap WSJF=1.8 / RICE=420 score is lower than the MVP features, which is consistent. The tension is whether the score is accurate given that the many-instance goal is listed as a hard requirement in `feature-definition.md §7a`.

**Evidence:** `feature-definition.md §7a` ("most likely we WILL need a C++ implementation … decisive for the many-instance goal"); roadmap WSJF=1.8, RICE=420 (low vs MVP features at higher scores); dossier §10.10 says "they do not gate the MVP."

**Options:**
1. Accept low score; the many-instance goal is post-MVP. Risk: if the C++ engine proves non-feasible, the many-instance goal collapses retroactively.
2. Raise score / promote the de-risk earlier once #4 + #12 are done, before committing to the full many-instance architecture.
3. Add a blocking flag on #15 (many-instances-shared-static-memory): #15's design is uninformative without #14's feasibility data.

---

## Under-specifications

### U1 — "Footprint" target is undefined

**Question:** What footprint does the C++ engine need to achieve to validate the many-instance goal?

**Why it matters:** without a concrete target (e.g., BEAM's ~2.6 KB per process instance as cited in `research-programme.md:80`), the spike has no pass/fail criterion. The experiment cannot be declared "successful" or "not feasible."

**Options:**
1. Adopt BEAM's ~2.6 KB per-instance dynamic heap as the target (stated in `research-programme.md:80`).
2. Define a project-specific target based on the expected number of concurrent GLP instances (e.g., 1000 instances × N KB = acceptable process footprint).
3. Make footprint measurement a first output of the spike itself, with no pre-defined threshold; declare success if the measurement is lower than the C# baseline.

### U2 — "Same grammar + IL" means what, precisely

**Question:** Does the C++ engine consume the binary IL produced by the C# compiler (wire format from #4), or does it implement its own ANTLR4-generated front-end producing the same binary IL?

**Why it matters:** these are different artifacts with different verification obligations. If the C++ engine consumes pre-compiled IL, the test is execute-equivalence (same IL → same result). If it generates IL from source, the test is IL-equivalence (same source → same IL from both C# and C++ compilers).

**Options:**
1. C++ executor only: consumes binary IL from #4; test = execute-equivalence.
2. C++ front-end + executor: generates IL via ANTLR4 C++ target; test = source→IL identity + execute-equivalence.
3. Two-stage: spike (a) first (C++ executor), then spike (b) (C++ compiler front-end).

### U3 — Build toolchain and portability requirements

**Question:** Which C++ standard (C++17/20/23), which compiler chain (MSVC/clang/gcc), and which platforms must the spike target?

**Why it matters:** the embedded-switch purpose (QHSM/HSM actors, OS tasks) implies Windows + Linux + embedded targets. These have divergent standard library support, calling conventions, and ABI constraints. A spike targeting only x86-64 MSVC is a different artifact from a portable C++17/CMake spike.

**Options:**
1. C++17, CMake, clang/MSVC dual-verified, Windows + Linux — portable-first.
2. C++20, single platform (Windows), MSVC — fastest to produce a footprint number.
3. Leave toolchain as a first output of the spike.

### U4 — Scheduler model: C++ coroutines / fibers vs event loop

**Question:** The C# scheduler (`scheduler.cs`) is a cooperative, single-threaded drain loop. In C++, the equivalent can be implemented as a simple loop (same model), C++20 coroutines, or libcppa/actor-framework actors. Which model?

**Why it matters:** `research-programme.md:488` cites libcppa for C++ actor systems; C++20 coroutines are the standard cooperative-yield mechanism. The choice affects the footprint and how suspension/reactivation maps to the language.

**Options:**
1. Simple event loop (mirror the C# drain): lowest risk, most direct translation of the existing model.
2. C++20 coroutines: idiomatic, but adds language-version constraint and coroutine frame overhead.
3. libcppa / CAF actor framework: high-performance but a large external dependency.

---

## GEPA/DSPy refinement

### Applicability: **methodological**

This seed is a systems C++ spike, not an LM/codegen program. GEPA/DSPy does not directly optimize a C++ executor. However, the **methodological** framing applies: the spike should be designed as an iterate-against-a-metric loop — each C++ prototype candidate is evaluated against the formal+pragmatic metric combination below, and the design is refined until thresholds hold or infeasibility is declared.

The agentic-ITP loop (Lean/Rocq tactic generation via Claude) is the primary formal metric driver, not DSPy's compile-time optimizer. DSPy's discipline of "seed → candidate → evaluate → mutate → repeat" applies as a process frame.

### Seed definition

> A C++ engine+scheduler+heap that (a) loads binary GLP IL produced by the C# il-codec (#4) over the `FrameCodec` wire, (b) executes it with the same three-phase HEAD/GUARD/BODY semantics, three-valued unification, SRSW, and suspension/reactivation as the C# reference, and (c) achieves a per-instance dynamic footprint at or below an owner-specified threshold. The spike targets the execution core only (scenario (a) per T1); compiler relocation (#11) is a separate follow-up.

### Metrics combination

| # | Name | Kind | Tool / harness | Threshold |
|---|---|---|---|---|
| M1 | Execute-equivalence corpus | pragmatic | Run the same IL corpus through the C# reference runner and the C++ runner; compare results (bindings + status) for each goal | 100% match on the REPL test corpus (test/run_all_tests.sh goals compiled to IL by #4) |
| M2 | Per-instance dynamic footprint | pragmatic | `valgrind --tool=massif` or `/proc/self/status VmRSS` per spawned C++ engine instance, with a minimal GLP program loaded | At or below owner-defined target (candidate: ≤ 10 KB per instance on a loaded-but-idle engine; settled interactively) |
| M3 | Round-trip IL fidelity | pragmatic | `encode(program, bytes)` → C++ `decode(bytes, program')` → execute; confirm `program'` produces the same result as the C# reference | 100% identity on the test corpus (re-uses #4's round-trip harness) |
| M4 | SRSW preservation | formal | Lean 4 mechanized check: any IL stream that passes the C# type-checker/SRSW validator also passes the C++ executor's inline SRSW assertions without suspension-set corruption | No SRSW-violation execution path exists in the C++ runner (prove by invariant: writer-MGU never binds a reader) |
| M5 | Three-valued unification soundness | formal | Lean 4 theorem: the C++ HEAD unification phase is sound w.r.t. the GLP operational semantics (Success/Suspend/Fail cases are exhaustive and correct) | Machine-verified proof of the unification step case-split |
| M6 | IL byte-parity (cross-runtime) | formal | Byte-parity test: a `BytecodeProgram` serialized by the C# il-codec (#4) is deserializable by the C++ decoder and produces an identical in-memory instruction list (byte-exact re-encoding) | `decode(encode(p)) == p` on 100% of the test corpus; FR-060/061 parity standard |

### Interactive spec step

At the start of `/buildkit-specify cpp-engine-feasibility`, the owner confirms:

1. Scope decision (T1): pure C++ executor (scenario a) or full C++ front-end (scenario b)?
2. Footprint target (U1): BEAM-style 2.6 KB, project-specific, or measurement-first?
3. Formal metric set: which of M4/M5 to pursue in the spike vs defer? (M4 SRSW preservation is the minimum formal gate for the spike; M5 can be deferred to a follow-up.)
4. Toolchain (U3): C++17/CMake portable, or MSVC-first for the spike?
5. Scheduler model (U4): simple drain loop, C++20 coroutines, or actor framework?

### Refinement loop

```
seed_definition
→ candidate_0: minimal C++ heap + executor skeleton (handles UnifyConstant, PutStructure, Spawn, Commit only)
→ evaluate against M1 (mini-corpus), M3 (round-trip), M6 (byte-parity on same mini-corpus)
→ if M3/M6 fail: mutate the IL decoder (re-derive from C# opcodes.cs + opcodes_v2.cs)
→ candidate_1: extend to full opcode set; add suspension/reactivation
→ evaluate against M1 (full REPL corpus subset), M2 (footprint)
→ if footprint too large: apply two-tier memory refactor (shared static code segment, per-instance heap only)
→ evaluate M2 again
→ candidate_N: add M4 (SRSW formal gate): generate Lean 4 invariant proof for the writer-MGU step
→ loop until M1 + M2 + M3 + M4 + M6 all pass thresholds or infeasibility declared
→ terminate: publish spike verdict (feasible / not feasible / feasible-with-scope-constraint)
```

Claude drives the loop (Agent-tool seams); no OpenAI/litellm/API. Each iteration is a checkpoint committed to the branch.

---

## Formal tooling

### Lean 4 vs Rocq evaluation

**Lean 4 fit:** Strong. The key formal properties for this seed are:
- SRSW preservation under C++ execution (M4): an invariant proof over the instruction dispatch loop — Lean 4's dependent types and `simp`/`omega` tactics handle this style of structural induction over an instruction list naturally.
- Three-valued unification soundness (M5): a case-split proof over the three unification outcomes; Lean 4's `cases` tactic and mathlib's `Finset` support map directly.
- IL byte-parity (M6): a propositional equality proof (`decode (encode p) = p`) — standard Lean 4 rewrite/simp.
- Lean-LSP-MCP (Claude-native) and Lean Copilot (model-neutral) provide the tactic loop without requiring an external API.

**Rocq fit:** Also suitable. Rocq/Coq has the stronger prior art for verified compiler/WAM proofs (TWAM — certifying abstract machine for logic programs, arxiv:1801.00471; verified Prolog→WAM compiler, ScienceDirect:0743106692900547; Vellvm for IR). For a WAM-lineage IL the Rocq/Vellvm structural template is more directly reusable. AutoRocq's iterative LLM↔Rocq tactic loop is the relevant agentic connector — but its GPT-4 dependency must be adapted to Claude (the no-API resolution from the brief §3.2a).

**Primary: Lean 4.** The tactic ecosystem (Lean-LSP-MCP, Lean Copilot, APOLLO for sorry-repair) is more mature for model-agnostic Claude-driven proof and requires no API adaptation. The properties in scope (SRSW invariant, unification soundness, byte-parity equality) are structurally simpler than the full verified-compiler proofs where Rocq's prior art dominates.

**Alternative when:** retain Rocq as the alternative specifically if the scope expands to a full verified-compiler proof (C++ codegen ≡ C# codegen ≡ semantics) — at that point, the TWAM/Vellvm structural template in Rocq is the more direct starting point. If the spike stays in executor territory (scenario a), Lean 4 suffices.

### IL verification

This seed is IL-touching (it consumes the GLP bytecode wire format from #4). The IL verification approach:

- **Byte-parity / round-trip (M6):** the primary mechanical test. `decode(encode(p)) ≡ p` as a propositional equality proof in Lean 4, over the two opcode families (v1 `IOp`, v2 `IOpV2`) and the recursive-constant sub-encoder (the `Rt.StructTerm` embedding at `codegen.cs:737-759`).
- **MLIR-dialect layer:** for this spike, the MLIR GLP-dialect (HEAD-unify / GUARD-test / BODY-spawn / suspend-reactivate primitives + progressive lowering) is a design target for a *future* follow-up, not an MVP gate for the feasibility experiment. The brief §3.2 establishes MLIR as the higher-level IL layer; this spike validates the lower binary-IL layer first. The MLIR-dialect plan feeds #15 (many-instances, which applies the two-tier memory model) rather than #14.
- **TWAM precedent:** the certifying abstract machine for logic programs (arxiv:1801.00471) is the structural model for "compiled-execution ≡ source-interpretation" for the C++ runner. Adapt the TWAM verification approach (Rocq-originated; Lean 4 port is the adaptation task) to the GLP three-phase reduction model.

---

## Shapiro criteria preserved

This step (C++ engine feasibility spike) must preserve the following original GLP/Shapiro design criteria, framed for the embedded-switch purpose:

1. **Committed-choice concurrency:** the C++ executor must implement committed-choice reduction (once a HEAD succeeds and GUARD passes, the BODY executes without backtracking). The QHSM/HSM actor model layered on top requires exactly this determinism for safe state-machine transitions.

2. **SRSW (Single-Reader / Single-Writer):** each logic variable has at most one writer and at most one reader per clause. The C++ heap and writer-MGU must enforce SRSW; a C++ implementation that silently relaxes SRSW would break the synchronization guarantees that the connectivity-switch role (routing between external I/O and internal OS actions) depends on.

3. **Suspension correctness:** a goal that reads an unbound variable must suspend and reactivate exactly once when the writer binds — no spurious reactivations, no missed reactivations. The embedded switch uses suspension for blocking on external events (link_recv waiting for an incoming frame); the C++ scheduler must preserve the reactivation semantics of `heap_fcp.cs:730-742` (`ActivateSuspendedGoals`).

4. **Monotone variable binding:** a writer binds exactly once (no rebinding). The embedded switch routing decisions are based on ground terms that arrive via the GLP variable protocol; non-monotone binding would corrupt routing state.

5. **Three-valued unification (Success/Suspend/Fail):** the C++ HEAD phase must implement all three outcomes. In the embedded-switch context, the Suspend outcome is used to park a request until a resource (channel, OS handle, sensor state) becomes available — dropping this case would cause silent loss of connectivity requests.

---

## Recommendation

**Narrow the spike scope to scenario (a):** a C++ executor that consumes binary IL from the C# compiler over the wire (from #4), with no C++ compiler front-end in scope. This:
- Keeps depends_on = {4, 12} (#12 needed only for grammar verification, not for running pre-compiled IL; could even be relaxed to depends_on = {4} if the grammar-as-verifier gate is deferred).
- Produces a clear footprint and execute-equivalence result within a bounded timeframe.
- Leaves the C++ compiler front-end (scenario b) as a follow-up that depends_on {11, 14a}.

The EXPERIMENT classification is correct. The spike should declare infeasibility explicitly if the C++ footprint cannot achieve the target for the many-instance goal — that verdict is as valuable as a feasibility verdict.

---

## Options for owner

| Label | Consequence |
|---|---|
| O1: Narrow scope to C++ executor only (scenario a); depends_on = {4, 12} | Fastest to a footprint/feasibility verdict; defers C++ compiler front-end; M1–M6 all tractable |
| O2: Full C++ front-end + executor (scenario b); add depends_on = {11} | Validates the full portability thesis; higher effort; blocks on #11 which is itself a large refactor |
| O3: Split into two seeds (#14a = C++ executor, #14b = C++ compiler front-end) | Finer roadmap control; #14a unblocks #15 independently; #14b can be prioritized separately |
| O4: Raise the WSJF/RICE score and promote earlier | De-risks the many-instance goal earlier; may conflict with MVP sequence ordering |

---

## Open questions

1. What is the concrete per-instance footprint target for the C++ engine? (U1)
2. Is the scope scenario (a) (pure executor) or scenario (b) (full front-end)? (T1 / U2)
3. Which C++ standard and toolchain? (U3)
4. What scheduler model? (U4)
5. Does the Lean 4 SRSW invariant proof (M4) need to be part of the spike or is it a follow-up formal gate?
6. Should #14 depend on #11 (compiler relocation) or is the spike scoped to IL-in only? (T2)
7. Is the MLIR GLP-dialect a target for this spike or deferred to #15 / a separate investigation?

---

## External refs

- **KLIC — portable KL1 (committed-choice) engine in C:** Ueda Lab, Waseda. Compiles KL1 to C, runs on UNIX. Direct committed-choice-logic-to-C precedent. [SAL KLIC](http://www.sai.msu.su/sal/C/1/KLIC.html); [Springer: A Portable and Efficient Implementation of KL1](https://link.springer.com/chapter/10.1007/3-540-58402-1_4)
- **BinProlog — embeddable WAM-lineage C logic engine:** ~4500 LOC C emulator, 123 instructions, designed for embedding in C applications. [arxiv:1102.1178](https://arxiv.org/abs/1102.1178); [GitHub: ptarau/binprolog](https://github.com/ptarau/binprolog)
- **InductorProlog — lightweight embeddable C++ Prolog:** designed for game AI / HTN engines; small, memory-constrained, platform-agnostic (Windows/Mac/iOS). [GitHub: EricZinda/InductorProlog](https://github.com/EricZinda/InductorProlog)
- **ANTLR4 C++ target:** mature, production-ready C++ parser generation from a single grammar; supports all 10 target languages from one `.g4` file. [antlr4 C++ target docs](https://github.com/antlr/antlr4/blob/master/doc/cpp-target.md); [ANTLR4 targets](https://github.com/antlr/antlr4/blob/master/doc/targets.md)
- **TWAM — certifying abstract machine for logic programs (Rocq):** verified Prolog→WAM execution; structural model for "C++ executor ≡ source semantics." [arxiv:1801.00471](https://arxiv.org/pdf/1801.00471)
- **Verified Prolog→WAM compiler:** compiled-execution ≡ source-interpretation proof; WAM-lineage IL correctness model. [ScienceDirect:0743106692900547](https://www.sciencedirect.com/science/article/pii/0743106692900547)
- **First-Class Verification Dialects for MLIR (PLDI'25):** makes MLIR dialect semantics first-class and verifiable; the GLP/FCP dialect layer for progressive lowering. [users.cs.utah.edu/~regehr/papers/pldi25.pdf](https://users.cs.utah.edu/~regehr/papers/pldi25.pdf)
- **FCP sequential abstract machine (Houri & Shapiro):** direct committed-choice-logic WAM ancestor of GLP's IL. [ScienceDirect:0743106689900113](https://www.sciencedirect.com/article/pii/0743106689900113)

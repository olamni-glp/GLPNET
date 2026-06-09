# Decisions for the Owner — `engine-separation` epic reconciliation

Gabi reads **only this file**. It synthesizes the 17-seed reconciliation into the
decisions you need to make before any seed enters `/buildkit-specify`. Every option
carries a consequence; every recommendation is **advisory** (you decide; nothing here
mutated code or roadmap state). Dossier §-anchors and `file:line` cite the evidence.

- Per-seed full analysis: the numbered memos in this directory.
- Index + legend: [`README.md`](README.md).
- Cross-cutting methodology: [`REFINEMENT-METHOD.md`](REFINEMENT-METHOD.md).

---

## 1. TOP TENSIONS (decide these first)

### D1 — Close the pre-decomposition monolith #1.5 as superseded *(advisory: CLOSE)*

**Statement.** Seed `repl-engine-split-mvp-binary-wire-format-intermediate-language-c`
(#1.5) is the original monolithic capture. Its scope is the *union* of dossier §11
entries #2–#16, and the dossier already decomposed it into a topologically-valid
15-feature graph.

**Evidence.** No single capability exists as a deployed wire artifact today: zero
`Serialize/Encode/ToBytes` in `out/csharp/lib/bytecode/`; `ToDisassembly()`
(runner.cs:88) is human-readable only; `ExecutionResult` (glp_engine.cs:51-80) has 3
fields with live heap refs. **Both** premises the monolith asserts are false and the
dossier corrects them: "REPL→engine carries compiled IL" (contradicted by
glp_engine.cs:487-493, goal string compiled engine-side — §9.1) and "engine generates
IL at runtime" (contradicted by zero `.Compile()` in `out/csharp/lib/runtime/` — §9.2).

**Options.**
- **(a) CLOSE as superseded** — mark closed pointing at dossier §11 + the epic; all work
  flows through #2–#16; never run `/buildkit-specify` for the monolith. *Consequence:*
  zero captured work lost, incremental delivery + the #4 de-risking spike preserved.
- **(b) RE-PURPOSE as epic umbrella/tracker** — rename to a non-buildable rollup that
  closes when #2–#16 close. *Consequence:* one extra pipeline entry, no deliverables;
  only worth it if the roadmap UI needs a feature-level rollup above the epic.
- **(c) RETAIN monolithic** — *Consequence:* ships result-envelope + IL codec (hardest
  unknown, §12r1) + compiler relocation (large) + liveness/persistence (net-new) +
  multi-accept in one delivery; the IL codec blocks the entire MVP. Not recommended.

**Advisory:** **(a) CLOSE.** The epic already serves the tracker role; keeping the
monolith open creates ambiguity about whether to specify it alongside the seeds.

---

### D2 — The roadmap records NO hard dependency edges, but dossier §11 defines a strict topology *(advisory: encode the edges)*

**Statement.** Dossier §11 specifies a strict dependency graph (every `depends_on`
references a strictly-smaller seed number; zero forward edges). The roadmap/profile
records for the decomposed seeds do **not** carry these as hard `depends_on` edges —
they live only in the dossier prose. Several reconciliation memos surfaced edges that
are *additionally* missing or wrong even in the dossier (D3, D4, D5 below).

**Evidence.** #11's stored `depends_on: [4, 6]` omits #5 (ModuleTerm-in-binding needs
the result codec — terms.cs:146, glp_activation.cs:88, body_kernels.cs:1032). #12 records
`depends_on: [11]` only but byte-level "identical IL" silently needs #4. #6 depends on #5
which depends on #2,#3 — a transitive chain not tracked on #6. #8 restart calls the #7
persistence API. #14's full-front-end scenario needs #11 (unrecorded).

**Options.**
- **(a) Encode the full §11 topology as hard `depends_on` edges in the roadmap, plus the
  corrections in D3–D5.** *Consequence:* `buildkit-roadmap next` sequences correctly;
  no seed is specified before its prerequisites; one-time profile-edit cost.
- **(b) Leave edges in dossier prose only; rely on human sequencing.** *Consequence:*
  zero bookkeeping now; high risk a seed enters `/buildkit-specify` out of order (e.g.
  #6 before #5 is ready), discovered late.
- **(c) Encode only the MVP-critical-path edges (#2,#3→#5→#6; #6→#7→#8→#9) now; defer
  experiment/follow-up edges.** *Consequence:* protects the shippable path with minimal
  edits; experiments (#12,#14,#15,#16) sequenced by hand.

**Advisory:** **(a)** if the roadmap tool drives sequencing; **(c)** as the pragmatic
minimum. Either way, apply the D3–D5 corrections.

---

### D3 — #5 result codec: the dossier's FrameKind citation is wrong *(advisory: payload-type byte inside the chunk)*

**Statement.** Dossier §3 and §0.4 say the IL/result codecs are "distinguished by the
header Kind byte (FrameCodec.cs:64)". That is incorrect.

**Evidence.** `FrameCodec.cs:64` is `private const int OffKind = 1;` — the byte at that
offset is `FrameKind.Whole(0)` / `Fragment(1)` (fragmentation only). `ParseFrame`
(FrameCodec.cs:132-143) throws `FrameException` on any other value. There is no
payload-type slot in the 22-byte header. This is the live binary format shared with
feature-025 ground-relay and the Dart mirror.

**Options.**
- **(a) Payload-type prefix byte inside the codec chunk** (0x01 result / 0x02 IL / 0x03
  output). *Consequence:* no FrameCodec change, backward-compatible with 025; +1 byte/payload.
- **(b) Extend `FrameKind` enum** (ResultEnvelope=2, ILPayload=3). *Consequence:* breaks
  the live byte-parity contract FR-060/061; requires Dart-mirror + all-reader updates.
- **(c) Separate TCP connection per payload type.** *Consequence:* over-engineering for
  the MVP; contradicts "rides FrameCodec".

**Advisory:** **(a).** Correct the dossier §3/§0.4 citation and specify the prefix byte
in #5 (and #4 — same finding there).

---

### D4 — #11: missing #5 dependency + VariableMap must cross the wire *(advisory: add #5; carry VariableMap)*

**Statement.** #11 (compiler relocation + bidirectional IL-on-wire) under-records its
dependencies and omits a field that must cross the wire.

**Evidence.** ModuleTerm-wrapped `BytecodeProgram` can appear in *result* bindings
(§2.4; terms.cs:146, glp_activation.cs:88, body_kernels.cs:1032) — that path needs #5's
result codec, but #5 is not in `depends_on`. `CompilationResult.VariableMap` (result.cs:9,
`Dictionary<string,long>`) maps var-name→register and is required engine-side to build
`queryVarWriters` (glp_engine.cs:515); after the compiler moves front-end it must travel
on the request frame.

**Options.**
- **(a) Add #5 to `depends_on`; carry `BytecodeProgram`+`VariableMap` on the request
  frame** (within #4's codec scope, already in §2.2). *Consequence:* correct graph; full
  bidirectional IL-on-wire incl. ModuleTerm-in-binding.
- **(b) Split: compiler-relocation (dep #4,#6) first; ModuleTerm-in-result a later
  follow-up (dep #5).** *Consequence:* smaller first increment; unblocks #12 sooner; +1
  roadmap entry.
- **(c) Engine recomputes writer names from IL.** *Consequence:* not currently possible —
  register indices carry no embedded names; rejected.

**Advisory:** **(a)** if delivering the full feature; **(b)** if you want #12 unblocked
fastest. Note: the refactor is **lower effort than "large"** — `GlpCompiler` is already a
standalone class (compiler.cs:29).

---

### D5 — #12: hidden #4 dependency; verifier-role vs production-parser split *(advisory: verifier-first, two-phase)*

**Statement.** #12 (ANTLR4 shared grammar) records `depends_on: [11]` only, but its
"confirm identical IL" success criterion silently requires #4, and it conflates two
different roles with different dependency chains.

**Evidence.** Byte-level "identical IL" needs deterministic `BytecodeProgram`
serialization = #4 (zero serialization exists today). The *grammar-as-verifier* role
(brief §3.2: "parse every working-definition example before any compiler exists") needs
only parse-accept and can run independent of #11; the *production-parser* role (generate
C# front-end, confirm identical IL) needs #11 (compiler relocation) + #4 (byte identity).

**Options.**
- **(a) Two-phase: Phase-A grammar-verifier (dep #1a/corpus); Phase-B production parser
  + IL identity (dep #4+#11).** *Consequence:* formal grammar metric available early as a
  gate for all language-touching seeds; drop C++ (defer to #14).
- **(b) Production-parser only (dep #4+#11).** *Consequence:* stronger end state but all
  formal value delayed behind both prerequisites.
- **(c) Split into two roadmap entries** (grammar-verifier dep #1a; multi-target-parser
  dep #11). *Consequence:* cleanest topology; +1 roadmap entry.

**Advisory:** **(a)** define "identical IL" (byte vs execution-equivalence — D-U below),
add #4 to `depends_on` if byte-level, scope verifier-first, drop the C++ target.

---

## 2. PER-SEED DECISIONS

### #2 result-envelope — `Bindings` representation + output-field dependency
- **Decision A (T3):** how to expose deep-resolved bindings. Options: **(1, advisory)**
  add a parallel `ResolvedBindings` field (backward-compatible; REPL `FormatTerm`
  unaffected) · (2) replace `Bindings` values and audit all callers (double-deref risk at
  glp_repl.cs:479+) · (3) lazy `engine.DeepResolve(result)` method. *Note:* code finding
  beyond the dossier — `Bindings` is **shallow** `Heap.Dereference` (glp_engine.cs:578),
  not deep-resolved; `_ResolveDeepForTrace` (glp_engine.cs:607-619) runs only for
  EquivTrace.
- **Decision B (T2):** output field. Options: **(1, advisory)** add hard dep #2→#3 ·
  (2) collapse output-routing into #2 · (3) exclude output, add it in #5. (Ties to D2.)

### #3 output-capture — scope boundary + TraceSink exposure
- **Decision:** scope. Options: (narrow) route `OutputCallback`+`TraceSink` only ·
  **(phased, advisory)** narrow now + structured compiler/type errors via the #5 envelope
  `errors` field · (broad) new `DiagnosticSink` for all 25+ engine `Console.Write*` sites.
- **Required regardless:** add `GlpEngine.TraceSink` and pass it at Scheduler construction
  (gap at **glp_engine.cs:535** — TraceSink seam exists at scheduler.cs:138 but is never
  wired); remove the codegen.cs:182/215/217 `foo/1` debug artifact.

### #5 result codec — forks that define the binary format
- **Decision:** settle **§10.3** (display-only vs round-trip unbound VarRef/ModuleTerm)
  and **§10.4** (stable `GlobalVarId` vs raw heap int for var→writer identity) *before*
  the format is specified — `DrainResult.BlockingReaders` is `IReadOnlySet<int>` heap
  addresses (scheduler.cs:73), meaningless cross-process under §10.4. Advisory: §10.3
  display-only + §10.4 `GlobalVarId` for the MVP. Also resolve assembly placement (FR-057:
  engine must not reference GlpLink) — advisory: host/composition-root layer.

### #6 MVP — the four spec-blockers
- **Decision:** (1) FrameKind discriminant approach (direction-implicit for MVP vs
  extension — see D3); (2) host layout (new `glp_engine_host/` project vs `--server-mode`
  flag on `glp_repl`); (3) envelope field set (ground-only vs full §2.3); (4) client
  `self.glp` need (thin terminal vs local context). Advisory: ground-only subset, new host
  project, thin terminal, server pre-renders bindings to strings. **Treat FrameCodec
  byte-parity (FR-060/061) as a hard MVP gate** — cheapest formal property, sets the
  discipline before the IL codec arrives.

### #7 persistence — scope expansion (required before specify)
- **Decision:** expand the scope line to include `_waitReaders` (runtime.cs:96),
  `GlpEngine._goalId` (glp_engine.cs:156), `InfrastructureGoalIds` (runtime.cs:112),
  `GlpChannels` (runtime.cs:53) — omitting them loses timers, collides goal-IDs, breaks
  routing. Plus: store (C# Npgsql+JSON in a new `glpengine` PGLite schema, advisory) ·
  ModuleTerm (label-reference MVP, decouples from #4, advisory) · quiescence (MVP = no
  open links, advisory) · `_waitReaders` resume (PERSISTENT+re-arm, advisory) · blob
  format (MessagePack/CBOR base64-in-JSON, advisory).

### #8 liveness — taxonomy + platform + placement
- **Decision:** (1) unrecoverable-state taxonomy — enumerate a closed set (heap OOM,
  snapshot-write failure, fatal CancellationToken) vs generic unhandled-exception=crash
  (advisory: enumerate); (2) platform — Windows-only vs cross-platform vs portable
  heartbeat (advisory: Windows-only for MVP); (3) FR-057 placement — composition root
  (§10.7 Opt 1, advisory) vs engine resume-hook; (4) self-prove GLP goal — defer (advisory;
  it needs a new system predicate = language-authority gate). Fix §ref typo §7→§5.

### #9 restore-and-resume — RewireHandle + address stability
- **Decision:** `WireEstablishedLink` **aborts on pre-bound cells**
  (LinkEstablish.cs:38-43) — exactly the post-restore state; needs a net-new
  `RewireHandle` (advisory, ~30 lines). Mandate **verbatim-address** snapshot semantics
  (T2 Opt 1, advisory) as a constraint on #7 — cheapest correctness path; the stable
  `GlobalVarId` layer is the future-proofing alternative. Scope the kill-and-restart test
  to single-link/single-goal for MVP (advisory).

### #10 multi-accept — interface stability
- **Decision:** keep `ILinkTransport.ListenAsync` returning `Task<ILinkEndpoint>` (no
  interface change); stateful `TcpListener` + per-accept atomic nonce (advisory). The
  blocking `.GetAwaiter().GetResult()` runner-thread concern (LinkListenKernel.cs:63) is
  deferred to #13.

### #11 / #12 / #14 — see D4, D5, and D6 below.

### #13 multi-client GLP — soften the #11 dependency
- **Decision:** split #13a (source-text dispatch, dep #10 only) / #13b (IL dispatch,
  dep #10+#11) — the GLP logic + mwm fan-in proof are testable on source-text dispatch
  (advisory). `mwm` is **excluded from type-checking** (self.glp:380-385) → use a Lean 4
  fan-in stream-merge proof as the formal substitute (advisory).

### D6 — #14 C++ feasibility — scope fork *(advisory: narrow to executor)*
- **Decision:** (a, advisory) C++ **executor only** (IL-in/result-out, dep #4,#12) ·
  (b) full front-end + executor (adds dep #11) · (c) split #14a/#14b. The spike **must
  emit an explicit infeasibility verdict** if the footprint target can't be met — as
  valuable as a feasibility verdict for deciding whether #15 is viable. Define the
  footprint number (BEAM ~2.6KB cited; or measurement-first).

### #15 many-instances — resolve two definitions first
- **Decision:** (1) in-process N-engines vs OS-process-per-instance (T2 — determines how
  shared-static is achieved and whether §5 liveness is per-instance); (2) the formal
  definition of "one atomic reduction chain" (U1 — drives the safe-preempt point and all
  metrics). Give the FOLLOW-UP half a concrete output gate (design doc + Lean 4 proof +
  harness → new impl spec, or fold into #14).

### #16 research-programme — narrow & close *(GEPA/DSPy applicability = low)*
- **Decision:** both reports are drafted. Narrow to reports + spike-ownership table +
  LingoDB citation fix (advisory: close at specify), hibernate the LLVM deepen/spike on a
  `blocked_on: #14` edge. Reassign owner exploration links (arxiv 2601.14027 Numina-Lean-
  Agent; share.google links) to **#1a**, not #16. Flagged: **#16 is the only seed with
  `low` GEPA/DSPy applicability** — it is a research/organizational deliverable, not an
  iterate-to-threshold artifact.

**Aligned seeds needing no further decision (proceed once their deps are met):** #1a,
#4 (apply D3+structural-identity), #6 (apply #6 decisions), #10.

---

## 3. VERIFICATION & METRICS PLAN (for the interactive `/buildkit-specify` step)

Every seed instantiates the **pragmatic + formal metric-combination** principle (brief
§3.1–§3.5; see `REFINEMENT-METHOD.md`). The interactive spec step confirms the table per
seed. Headline combinations:

| seed | pragmatic gate (tool) | formal gate (tool) | prover | IL-verification |
|---|---|---|---|---|
| 1a | metric-table template completeness; GEPA seam coverage vs optimize.py:257-335; no-API grep | Shapiro mapping completeness; formal-tooling slots specified; Lean 4 tactic-loop architecture | lean4 | specifies the layer (MLIR dialect names, byte-parity oracle) — none of its own |
| 2 | REPL 384/384; self-containment grep; round-trip display diff | depth-truncation Lean 4 proposition (depth≤32 ⇒ complete) | lean4 | n/a (in-memory only) |
| 3 | REPL 384/384; OutputCallback + TraceSink capture tests; Console.Write count=0 in-scope | type/SRSW gate | n/a | n/a |
| 4 | round-trip identity; execute-equivalence; opcode+constant coverage on `programs/` corpus | Lean 4 `decode∘encode=id` (simplified model); Z3 opcode-discriminant uniqueness | lean4 (**rocq alt** if scope→execution-semantics, per #11) | **this is the IL codec** — byte-contract + MLIR-dialect alignment |
| 5 | cross-process loopback equivalence; round-trip identity; output capture | byte-parity golden file (FR-060/061); Lean 4 unbound-sentinel proof; SRSW gate | lean4 (rocq alt) | n/a unless ModuleTerm-in-binding (then #4's layer) |
| 6 | REPL equivalence; loopback round-trip; kill-and-restart | FrameCodec byte-parity+CRC-32; SRSW; type gate | lean4 (rocq alt) | n/a (source text on wire) |
| 7 | kill-restart equivalence (EquivTrace); snapshot round-trip; dual-store consistency | monotone-binding + suspension-chain invariants (boot-time C# assert for MVP → Lean 4) | lean4 (rocq alt if byte-level blob proof) | n/a (label-ref) |
| 8 | OS liveness on schedule; non-zero crash exit + restart; REPL 384/384 | FR-057 csproj reference check; exception-taxonomy exhaustiveness (Z3 / review) | **n/a** | n/a |
| 9 | kill-and-restart correctness; ResourceSnapshot.IsBaseline; REPL 384/384 | suspension-correctness after re-wire; monotone-binding across boundary | lean4 (rocq alt) | n/a (operates above IL) |
| 10 | multi-client round-trip; LinkId uniqueness; pump thread-safety stress | SRSW gate; FrameCodec byte-parity unchanged; FR-057 isolation | **n/a** | n/a (no wire change) |
| 11 | REPL 384/384; compile→encode→decode→execute equivalence | IL byte-parity FR-060/061; Lean 4 round-trip identity; ModuleTerm round-trip; SRSW | lean4 (**rocq alt** for full bisimulation, TWAM lineage) | consumes #4's layer + MLIR lowering pass |
| 12 | grammar accepts 100% `programs/`; rejection preservation; execution-equivalence | byte-level instruction parity (needs #4); all-token-coverage; SRSW-preserving AST | lean4 (rocq alt) | byte-parity vs hand-written parser; feeds MLIR dialect |
| 13 | N-client round-trip; serve/2 dispatch equivalence; kill-restart w/ N clients | type/SRSW (non-mwm); **mwm stream-merge Lean 4 proof** (type-check substitute) | lean4 (**rocq alt** if coinductive/unbounded streams) | n/a |
| 14 | execute-equivalence corpus; per-instance footprint (massif); round-trip IL fidelity | SRSW invariant; three-valued-unification soundness; byte-parity cross-runtime | lean4 (**rocq alt** for full verified-compiler, TWAM/Vellvm) | byte-parity over both opcode families; MLIR deferred to #15 |
| 15 | per-instance memory ≤ budget; semantic equivalence across preempt; boundary safety | SRSW + suspension-reactivation across preempt; footprint sub-linearity bound | lean4 (**rocq alt** if reusing Coq FCP/WAM semantics) | n/a (scheduling/layout) |
| 16 | report completeness; prior-art coverage ≥6 systems; LLVM verdict; citation correctness | — (no mechanized proof) | **n/a** | n/a (produces no codec) |
| 1.5 | roadmap consistency; two-way traceability; no forward deps; REPL 384/384 | — (structural decision) | **n/a** | n/a |

**Lean 4 vs Rocq verdict across seeds.** **Lean 4 wins as primary everywhere a prover is
needed** (11 seeds): the decisive properties are round-trip identity, depth-bounded
resolution, unbound-sentinel correctness, suspension/monotone-binding invariants, and
fan-in stream-merge — all clean inductive properties over finite types, and **Lean-LSP-MCP
+ APOLLO + Lean Copilot are Claude-native and model-agnostic** (satisfy the no-API rule
without adaptation). **Rocq is never the primary; it is the named alternative on 9 seeds**,
genuinely needed only when scope crosses into **full verified-compiler bisimulation**
(#4/#11/#14 — TWAM, the verified Prolog→WAM compiler, Vellvm are Coq/Rocq prior art) or
**coinductive reasoning over unbounded streams** (#13). Using Rocq there means **adapting
AutoRocq off its GPT-4 dependency** (a no-API defect to fix, per brief §3.2a). **6 seeds
need no prover** (#3, #8, #10, #16, #1.5 — and #1a only *specifies* the layer).

**Flagged applicability:** **#16 = `low`** — close it as a research deliverable rather
than running an iterate-to-threshold loop (D-decision in §2).

---

## 4. UNDER-SPECIFICATION QUESTIONS (consolidated, de-duplicated)

### Wire / result (codecs, framing)
- **U-W1.** Payload-type discrimination: prefix byte inside the chunk (advisory) vs new
  `FrameKind` value vs separate connection? (D3; #4 T2, #5 T1, #6 T1)
- **U-W2.** Var→writer identity on the wire: stable `GlobalVarId(agentId:localId)`
  (payload_serializer.cs:85-88) vs raw heap int? (§10.4; #2, #5)
- **U-W3.** Unbound-var encoding for Suspended results: null=top-level-unbound +
  separate suspension fields vs explicit `UnboundVarTerm` in the RtTerm hierarchy?
  (PayloadSerializer throws at payload_serializer.cs:511; §10.3; #2, #5)
- **U-W4.** Output field layout: length-prefixed UTF-8 blob vs count-prefixed records vs
  absent-for-MVP? (#5 U4)
- **U-W5.** Format-version byte in the payload? (#5 U3)
- **U-W6.** Dart-mirror byte-parity for the *result* codec: mandatory now vs deferred
  until after #6 ships C#-only vs drop the Dart mirror? (§12r7; #5 U2, #6)

### Persistence / resume
- **U-P1.** `_waitReaders` timer state on resume: re-arm `Task.Delay` vs treat-as-expired
  vs reject non-quiescent snapshot? (#7 U1, #9)
- **U-P2.** Quiescence definition: MVP=no-open-links vs pump-extended
  (`!pump.HasPendingOrLive`) vs per-drain? (glp_engine.cs:545,555-569; #7 T4)
- **U-P3.** Heap-address stability: verbatim Cells restore (advisory) vs stable logical-id
  remapping layer? (§12r5; #7, #9 T2)
- **U-P4.** Snapshot blob format: MessagePack/CBOR vs side-car binary vs JSON throughout?
  (#7 U4)
- **U-P5.** "Resume the drain" trigger: synthetic no-op goal vs `ResumeDrainAsync()` on
  GlpEngine vs replay-from-source? (#9 U1)
- **U-P6.** Egress (`ArmEgress`) re-arm ordering vs drain resume; store `outWriterAddr` in
  the snapshot? (#9 U3)
- **U-P7.** Kill semantics in the test: graceful BackgroundService cancellation vs abrupt
  process kill (was the snapshot committed)? (#8, #9)

### Compiler-relocation
- **U-C1.** New engine public contract: `RunGoalAsync(BytecodeProgram,varMap)` overloads
  (deprecate string) vs hard cut vs compiler-plugin injection seam? (#11 U1)
- **U-C2.** `self.glp` + embedded sources (serve/2, madGLP) under relocation: pre-compiled
  `.il` artifact vs front-end-compiles-at-startup vs residual bootstrap compiler? (#11 U2/U3)
- **U-C3.** Conjunction wrapping (`_conj_wrapper_`, glp_engine.cs:621-637): move to
  front-end vs `RunConjunctionAsync(BytecodeProgram)` entry point? (#11 T3)
- **U-C4.** `VariableMap` crossing: on the request frame (advisory) vs engine-recomputed?
  (D4; #11 T2)

### Experiments (IL codec / grammar / C++ / many-instance / LLVM)
- **U-E1.** "Identical IL" definition: byte-identical (needs #4) vs execution-equivalent
  vs trace-equivalent (`ToDisassembly`)? (#12 U3; #4 T3)
- **U-E2.** Codec target: raw per-module `BytecodeProgram` (preserves private labels,
  advisory) vs `CombinedProgram` (strips them, glp_engine.cs:455-460)? (#4 U1)
- **U-E3.** Obsolete v1 opcodes (`UnionSiAndGoto`, `ResetAndGoto`, opcodes.cs:53-66):
  round-trip exactly vs normalize vs error? (#4 U3)
- **U-E4.** C++ spike scope: executor-only (dep #4,#12, advisory) vs full front-end (dep
  #11) vs split? + footprint target number + C++ std/toolchain + scheduler model. (D6; #14)
- **U-E5.** "One atomic reduction chain" definition: per-goal-step vs reactivation-cascade
  vs per-drain-epoch? (#15 U1)
- **U-E6.** In-process N-engines vs OS-process-per-instance (determines shared-static
  mechanism + per-instance liveness). (#15 T2)
- **U-E7.** LLVM deepen/spike gate: formal `blocked_on: #14` vs human judgment? (#16)

### Refinement-metrics
- **U-M1.** Per-seed metric-combination format: shared Markdown table template (name|kind|
  tool|threshold, advisory) vs buildkit-template-injected vs free-text? (#1a U1)
- **U-M2.** Shapiro criteria mandatory-vs-advisory mapping per seed type: mandatory for
  language/semantics/wire seeds, advisory (N/A+justification) for host/infra (#8,#10)?
  (#1a U2)
- **U-M3.** Depth-truncation bound for production bindings: 32 (current) vs 256 vs
  configurable vs cycle-detection? (#2 T1; affects the Lean 4 proof scope)

### Formal-tooling
- **U-F1.** Lean 4 ecosystem on Windows (Lean-LSP-MCP / Lean Copilot are Linux/Mac-first;
  cwd is `D:\...` on Win 11): WSL2/container setup note required in the #1a spec? (#1a OQ)
- **U-F2.** APOLLO availability: is the sorry-isolation code installable, or must the
  architecture be reimplemented? (#1a OQ)
- **U-F3.** AutoRocq GPT-4 adaptation: scoped? (needed only if Rocq is chosen on
  #4/#11/#13/#14 — brief §3.2a) (#1a OQ)
- **U-F4.** Citation gap: arxiv 2502.06854 is mis-attributed (it is an LLM-comprehension-
  of-LLVM-IR study, **not** the Typed-Datalog-IR paper; correct ref = LingoDB VLDB 2022,
  Jungmair et al.). Pin during #4/#12 spike as a tracked item, or block? (brief §6; #16 T2)
- **U-F5.** Are Lean/Rocq proofs on the MVP critical path? Advisory: **no** — #6 MVP is a
  source-text split; proofs gate only language-touching seeds (#4, #11, #12). (#1a OQ)

---

## 5. RECOMMENDED NEXT ACTION

**Enter `/buildkit-specify` for #6 (`repl-engine-process-split-mvp`) — but land #1a's
methodology artifacts first.**

- **Why #6.** Dossier §8.1 names Slice A (source text over TCP-loopback, compiler
  engine-side, one engine/one client) as the MVP. The as-built code is ready: `GlpEngine`
  is embeddable (glp_engine.cs:127), `RunGoalAsync` compiles engine-side
  (glp_engine.cs:349,487-493), `AfterEngineCreated` (glp_repl.cs:47,126) and `Program.cs:30-35`
  are the insertion points, `TcpTransport.ListenAsync` is one-accept (TcpTransport.cs:40-49)
  = exactly the one-client scope. The only net-new work is the host-listener process + thin
  client. #6 unblocks the whole persistence/liveness arm (#7→#8→#9) and #10.
- **Why #1a first.** #1a delivers the shared metric-table template, the no-API GEPA/DSPy
  loop spec, the proof-assistant policy, and the Shapiro mandatory/advisory mapping that
  **every** successor `/buildkit-specify` confirms. Landing it first means #6 (and #2–#16)
  inherit a common, reusable verification harness instead of each inventing its own. #1a is
  PREP with no code — it can land in parallel without blocking #6's pragmatic work.

**Decide BEFORE #6 enters specify:**
1. **D1** — close the monolith #1.5 (removes ambiguity about specifying it alongside #6).
2. **D2 / D3** — encode at least the MVP-critical-path edges (#2,#3→#5→#6) and correct the
   FrameKind citation (the payload-type byte is part of #6's wire contract).
3. **#6's four spec-blockers** — FrameKind discriminant, host layout, envelope field set,
   client `self.glp` need (§2 above).
4. **U-F5** — confirm Lean/Rocq proofs are **off** the #6 MVP critical path (advisory: yes),
   so the source-text split is not gated on formal verification.

#2 and #3 (the PREP foundations #5 depends on) should be specified immediately after #1a,
in parallel with #6's host-process work, since #6's *populated* result envelope depends on
them transitively (D2).

# Reconciliation Memo — Seed #1.5: `repl-engine-split-mvp-binary-wire-format-intermediate-language-c`

**Status:** Reconciliation complete (2026-06-09) — OWNER DECISION REQUIRED (see Options for Owner).
**Feature:** `026-engine-review-dossier` · **Sub-agent pass:** #1.5 (pre-decomposition monolith).
**Dossier:** `docs/research/repl-engine-separation/design-dossier.md`
**Methodology:** `reconciliation/SEED-RECONCILIATION-BRIEF.md`

---

## Dossier Cross-References

| Dossier anchor | Subject |
|---|---|
| §0.4 (classification table) | All nine capabilities; the monolith spans the entire table |
| §2.1–§2.4 (binary wire shapes) | IL/result wire codecs — the seed's "binary wire-format IL" claim |
| §3 (wire reuse decision) | Dedicated codecs over FrameCodec; do NOT extend PayloadSerializer |
| §8.1–§8.2 (MVP slices) | The decomposed MVP boundary; what the monolith would collapse |
| §9.1 (compiler-location premise reconciliation) | "Client→engine carries compiled IL" — the single biggest premise mismatch |
| §9.2 (engine-generated-IL premise reconciliation) | No runtime synthesis; compiled programs circulate as heap data |
| §11 (feature breakdown, entries #2–#16) | Each entry is a decomposed fragment of this monolith's scope |
| §12 risk 1 | Compiler-location mismatch — same root cause as §9.1 |
| §12 risk 7 | Cross-runtime byte-parity for new codecs |
| Appendix B, row 1.5 | Two-way traceability entry for this seed |

---

## Seed-vs-Dossier-vs-Code

### Stored seed profile (from `buildkit-roadmap brief`)

- **Scope (one line):** "detailed analysis of the full C# impl to locate the REPL-frontend vs engine+scheduler seam; factor the BIDIRECTIONAL wire format — REPL→engine = compiled IL of clauses+goals, engine→REPL = result representation; clearly-encoded binary IL; handle engine-generated IL at runtime; run REPL front-end + engine/scheduler as two separate process instances over the wire."
- **WSJF=2, RICE=2077, state=captured.**
- **Risk declared:** "coupling analysis across parser/REPL/engine/scheduler; correct IL encoding both directions; runtime engine-generated IL; preserving GLP execution semantics across the wire."
- **Notes:** "C#-first reference (Dart mirror later, optional). MVP scope: (1)–(5)…"

### Dossier position

The dossier decomposed this monolith into **fifteen successor seeds** (#2–#16) plus the methodology feature (#1a). Dossier §11 records the monolith under Appendix B as the "UMBRELLA / supersession case" with §-anchors §8, §9, §11 (whole).

### Code checks against dossier claims (as-built, current HEAD)

All dossier `file:line` citations verified against actual files. Key confirmations and additions:

| Dossier claim | Verified at | Finding |
|---|---|---|
| `GlpEngine` is extracted embeddable core | `out/csharp/lib/engine/glp_engine.cs:127` (`public sealed class GlpEngine`) | CONFIRMED. Docstring lines 5–17 call it "the ONE way to run GLP programs." |
| `ExecutionResult` has exactly three fields: `Status`, `Bindings`, `Error` | `out/csharp/lib/engine/glp_engine.cs:51–80` | CONFIRMED. `Bindings` is `IReadOnlyDictionary<string, RtTerm?>` — live heap refs, NOT self-contained. |
| `AfterEngineCreated` is in `glp_repl.cs:47`, not `glp_engine.cs` | `out/csharp/bin/glp_repl.cs:47` | CONFIRMED. Composition root `out/csharp/glp_repl/Program.cs:30–35` wires link kernels via this hook. |
| `BytecodeProgram` at `runner.cs:41` (not the stale `:452`) | `out/csharp/lib/bytecode/runner.cs:41–73` | CONFIRMED. `IReadOnlyList<object> Instructions` (heterogeneous); `Dictionary<string,int> Labels`; `ToDisassembly()` at `:88` (human-readable only — NOT a wire format). |
| Zero `Serialize/Encode/ToBytes` in opcode/runner files | grep across `out/csharp/lib/bytecode/` | CONFIRMED. No IL wire codec exists. The only opcode-related "encode" hit is a comment at `opcodes.cs:172` ("encode clause patterns" — referring to HEAD-phase encoding, not bytes). |
| `PayloadSerializer` throws on unbound `VarRef` | `out/csharp/lib/multiagent/payload_serializer.cs:509–511` | CONFIRMED. `throw new InvalidOperationException("Ground term expected, but VarRef found")`. |
| `PayloadSerializer` routes `MutualRefTerm`/`ModuleTerm` → `NotSupportedException` | `:445–449` | CONFIRMED. Default branch: `throw new NotSupportedException($"Cannot serialize term type: {term.GetType()}")`. |
| `FrameCodec` has `Kind` byte at offset 1; `HeaderSize=22` | `csharp/glp_link/reliability/FrameCodec.cs:42,45,52,56–64` | CONFIRMED. Version=0x01, Kind at `OffKind=1`, 22-byte header, 64 MiB guard. |
| `TcpTransport.ListenAsync` accepts exactly ONE connection then `listener.Stop()` | `csharp/glp_link/transports/TcpTransport.cs:30–50` | CONFIRMED. Comment at `:46–47` says "ONE link per listen … multi-accept … Phase 6." |
| Zero `IHostedService`/`BackgroundService`/`sd_notify` in `out/csharp` | grep across `out/csharp/` | CONFIRMED. No hits. Liveness layer is entirely net-new. |
| `queryVarWriters` built at `glp_engine.cs:515`, not an `ExecutionResult` field | `:515–539` | CONFIRMED. Built as a local `Dictionary<string,int>`, passed to `scheduler.SetQueryVarNames()`, then dropped — never reaches `ExecutionResult`. |
| No runtime bytecode synthesis | grep for `.Compile(`/`GenerateWithMetadata` in `out/csharp/lib/runtime/` | CONFIRMED. Zero hits. Compiled programs circulate as `ModuleTerm`-wrapped `BytecodeProgram` on the heap (`out/csharp/lib/runtime/body_kernels.cs` `_activate` → `glp_activation.cs:88`, `terms.cs:146`). |
| `_ResolveDeepForTrace` at `glp_engine.cs:607–619` | CONFIRMED. Recursive deref + struct rebuild; depth-guarded at 32. |
| `runtime.cs:InboundPump` injection seam | `out/csharp/lib/runtime/runtime.cs:129` | CONFIRMED. `IInboundPump? InboundPump` nullable property. |
| Heap `Cells` + `Hp` | `out/csharp/lib/runtime/heap_fcp.cs:148,154` | CONFIRMED. `public List<HeapCell> Cells { get; }` and `public int Hp { get; set; } = 0`. |

**Additional finding not explicitly called out in the dossier:**
- `out/csharp/lib/bytecode/runner.cs:88` `ToDisassembly()` is the ONLY serialization-like method on `BytecodeProgram` — it emits a human-readable string (`"PC {i}: ..."`) with no fixed-width, no CRC, no version byte, and no round-trip guarantee. This reinforces dossier §2.1's finding that zero wire-format infrastructure exists, and adds that even the disassembler is not a reuse candidate for a binary codec.
- `programs/self.glp:456` `Link(In,Out) ::= Channel(Stream(In), Stream(Out))` is a type definition, not a procedure — the GLP-mailbox constructs (`link_send` at `:536`, `link_recv` at `:548`, `mwm` at `:387`) are all present and verified, confirming §4.3's claim about the GLP control-loop shape.

---

## Classification Check

**Dossier §11 recorded kind:** UMBRELLA / supersession case (Appendix B row 1.5).

The stored roadmap seed's `kind` is not set to a pipeline kind (it predates the decomposition). The dossier Appendix B classifies it as the "pre-decomposition monolith" and instructs the reconciliation to determine SUPERSESSION vs RE-PURPOSE vs RETAIN.

**Does the as-built code support the seed's claimed scope?**

The seed claims five capabilities in one feature:
1. Seam analysis — DONE by the dossier (§1, §0.4); no code required.
2. Bidirectional wire format with REPL→engine = compiled IL — §9.1 establishes this is a **false premise**: the compiler lives engine-side (`glp_engine.cs:487–493`); the wire carries source text in the MVP. Compiled-IL-on-wire is a large follow-up (§11 #11).
3. Binary encoding of IL — zero in the repo today; the only serialization-like method (`ToDisassembly`, `runner.cs:88`) is human-readable only. This is the IL-codec spike (#4) + compiled-IL feature (#11).
4. Handle engine-generated IL at runtime — §9.2 debunks the premise: no IL is synthesized at runtime. Programs circulate as heap-term data. The premise is a mis-framing of the `ModuleTerm`-in-binding problem (§2.4, §6.2).
5. Two-process split over the wire — this is §11 #6 (`repl-engine-process-split-mvp`), which depends on #2, #3, #5.

**Conclusion:** The seed's scope is exactly the union of dossier §11 entries #2 through #11 (with premises #4 and #5 of the seed corrected by §9.1/§9.2). The as-built code does not contradict a monolithic implementation path — it merely shows that the five listed capabilities span multiple independent sub-problems that can be sequenced. The seed as written would be a valid monolithic feature IF the owner chose path (c) RETAIN, but the decomposed dossier makes clear the dependencies and risk profile strongly favor sequencing.

---

## Tensions

### T1 — The single most important tension: supersession vs re-purpose vs retain

**Evidence:** The dossier decomposes this monolith into 15 successor features (#2–#16) with a topologically validated dependency graph. The monolith's scope is the ENTIRE §11 table minus the methodology (#1a) and the dossier itself (#1). Continuing to work the monolith as a single feature would:
- Require ALL of result-envelope, IL codec (the hardest unknown — §12 risk 1), compiler relocation (large refactor), liveness/persistence (entirely net-new), and multi-accept in one delivery.
- Prevent incremental validation — the MVP split (#6) could ship while the IL codec is still a research item.
- Lose the dependency-aware sequencing that protects the project from tackling #11 (large refactor) before #4 (the de-risking spike).

**Options:**
1. **CLOSE as superseded** — mark this seed `closed/superseded` in the roadmap, pointing to #2–#16 as the authoritative decomposition. No work is lost; the dossier is the audit trail. The `/buildkit-specify` command for the monolith is never run; all work proceeds through the decomposed features.
2. **RE-PURPOSE as epic umbrella/tracker** — rename this seed to `engine-separation-epic-tracker`; demote it to a non-buildable tracking entry that links #2–#16 as its children. The epic `epic-separation-of-repl-front-end-from-engine-execution-scheduler` already exists in the roadmap; this seed could be its "feature-level summary" entry that closes when all children close.
3. **RETAIN as alternative monolithic path** — if the owner decides the decomposed sequencing is too fine-grained (15 features is a lot of pipeline overhead), retain this seed and implement it as a single large feature, accepting the combined risk and losing the incremental-delivery benefit.

**Advisory recommendation:** Option 1 (CLOSE as superseded) is cleanest. The epic already exists. The decomposed seeds are individually specified and prioritized. The monolith was a useful capture vehicle but its specification role is now served by the dossier + successor seeds.

---

### T2 — Premise mismatch: "REPL→engine carries compiled IL" vs source-text reality

**Evidence:** Seed notes say "REPL→engine = compiled IL of clauses+goals." As-built: `RunGoalAsync` takes a `string` and compiles it engine-side (`glp_engine.cs:487–493`). The compiler is entirely engine-internal (Lexer, Parser, TypeChecker, PartialEvaluator, Compiler all engine-side). Moving the compiler to the front-end is a large separate refactor (dossier §9.1, §11 #11).

**Options:**
1. Accept the corrected premise (source-text MVP, compiler stays engine-side) — aligns with the dossier and seeds #5, #6.
2. Override the dossier advisory and mandate compiled-IL from day one — accept the large upfront refactor.
3. Stage it: ship Slice-A (source text) first, then follow up with #11 (compiler relocation + IL wire) — the dossier's own recommendation.

**Advisory:** Option 3. (If the monolith is retained under T1 Option 3, Option 1 applies here.)

---

### T3 — Premise mismatch: "handle engine-generated IL at runtime" vs no-synthesis reality

**Evidence:** Seed risk says "runtime engine-generated IL." Dossier §9.2 + code grep (zero `.Compile()`/`GenerateWithMetadata` in `out/csharp/lib/runtime/`) confirms no runtime IL synthesis. What looks like "runtime IL" is `ModuleTerm`-wrapped `BytecodeProgram` on the heap (`terms.cs:146`, `glp_activation.cs:88`) — i.e., programs loaded at program-load time and then dispatched via `_activate`.

**Impact on the monolith:** if retained, "handle engine-generated IL" is not a real capability to implement; instead the requirement becomes "the IL codec and heap snapshot must round-trip `ModuleTerm`-embedded `BytecodeProgram` found in bindings" (§2.4, §6.2).

**Options:**
1. Reframe the capability correctly (as above) in any retained or re-purposed seed.
2. Remove the capability from the seed's scope entirely (it is handled as a consequence of §2.4 + §6.2 in #7).
3. Leave the mis-framing; accept that the implementer will discover it during #4 (IL codec spike).

**Advisory:** Option 1 if the seed is retained/re-purposed; Option 2 if closed.

---

## Under-Specifications

### U1 — Dossier §9.1 Opt 1 vs Opt 2 not locked: the wire shape changes entirely depending on this decision

**Why it matters:** The monolith scope says "bidirectional wire format — REPL→engine = compiled IL." The dossier defers this to §10.1 (compiler location, unsettled). If Opt 1 (source text), the MVP wire needs only the result codec (#5). If Opt 2 (compiled IL from day one), the IL codec (#4) becomes a BLOCKER for the MVP, not an EXPERIMENT. For the monolith path, this ambiguity means the implementer cannot scope the feature without this decision.

**Options:** (same as T2 above)

---

### U2 — Dart-mirror parity requirement for new codecs is not locked

**Why it matters:** The seed notes say "C#-first reference (Dart mirror later, optional)." The dossier §2.5 notes `FrameCodec`/`Crc32` carry explicit byte-parity remarks (FR-060/061); if the Dart mirror is kept, the new IL + result codecs must meet the same byte-parity standard — a constraint the v1/v2 opcode split complicates (§12 risk 7). "Optional" in the seed notes conflicts with "mandatory if Dart mirror kept" in the dossier. For the monolith, this means the wire format spec cannot be finalized without a decision on Dart parity.

**Options:**
1. Formally drop the Dart mirror for these codecs (reduces scope; acceptable if the C# REPL is the mandated default).
2. Mandate byte-parity from day one (adds constraint to the codec design, but avoids a painful retrofit).
3. Leave it unresolved until the Dart mirror is actually needed (defer the decision to #12 ANTLR4 spike).

**Advisory:** Option 2 for any codec that crosses a process boundary; Option 3 for compiler-front-end codecs (too far out).

---

## GEPA/DSPy Refinement

### Applicability

**methodological** — this seed is a pre-decomposition monolith, not an LM/codegen program. GEPA/DSPy does not directly optimize a prompt or code-generation program here. However, the GEPA/DSPy iterate-against-a-metric discipline applies methodologically: the supersession/re-purpose decision IS a refinement step, and the successor seeds that implement the monolith's capabilities (especially #4 IL-codec spike and #5 result-codec) are direct GEPA/DSPy targets. This memo's GEPA plan therefore covers (a) the supersession decision step and (b) the handoff to the direct-applicability successors.

### Seed Definition (for the supersession/re-purpose decision step)

> Given the decomposed dossier of the REPL/engine separation epic, determine whether seed `repl-engine-split-mvp-binary-wire-format-intermediate-language-c` should be (a) closed as superseded, (b) re-purposed as an epic tracker, or (c) retained as a monolithic alternative path. The candidate decision must satisfy: (i) no work captured in #2–#16 is lost, (ii) the dependency-aware sequencing is preserved, (iii) the corrected premises (§9.1, §9.2) are encoded, and (iv) the roadmap reflects the true work structure.

### Metrics Combination

| Metric | Kind | Tool/Harness | Threshold (the "right level") |
|---|---|---|---|
| Roadmap consistency: no orphaned successor seeds | pragmatic | `buildkit-roadmap status` — verify all #2–#16 are reachable from the epic | All 15 successors have a live epic link; zero dangling features |
| Dossier traceability: two-way §-anchor coverage | pragmatic | Manual audit of Appendix B + per-seed `dossier_cross_refs` | Every seed memo references ≥1 dossier §-anchor; dossier back-links all 17 reconciliation entries |
| Premise correctness: §9.1/§9.2 corrections encoded in successor scopes | pragmatic | Read #5/#6/#11 scopes; confirm source-text MVP + IL-codec-as-experiment framing | #6 scope says "source text" (not "compiled IL"); #4 scope says "EXPERIMENT/spike" |
| No forward dependencies in §11 table | pragmatic | Topological sort of `depends_on` entries | Zero forward edges; every entry depends only on strictly smaller numbers |
| SRSW + type-checker gates pass for any GLP code touched during epic execution | formal | `test/run_all_tests.sh` (REPL suite — Section B/C/D) | 384/384 green on any GLP-touching change |
| Wire contract formal gate (for successor #4/#5/#11): byte-parity round-trip | formal | FR-060/061 byte-parity test harness (once built in #4/#5); `decode(encode(p)) ≡ p` | Round-trip identity holds for every opcode family + recursive constant sub-term |

*Note: for THIS seed (the supersession decision), the first four pragmatic metrics are the primary signal. The two formal metrics apply to the successor seeds that implement the actual wire work.*

### Interactive Spec Step

At the start of the supersession decision (or of `/buildkit-specify` for any retained/re-purposed variant), the owner confirms:

1. **T1 decision: supersede, re-purpose, or retain?** This gates whether a spec is written at all.
2. If retained: which metric combination from the successor seeds applies (inherit from #4, #5, #6, etc.)?
3. **Dart-mirror parity** (U2): formally mandatory or deferred?
4. **Compiler location** (T2/U1, §10.1): Opt 1 (source-text MVP, IL deferred) vs Opt 2 (compiled IL from day one)?
5. **Proof assistant choice** for any language/wire-touching work in a retained monolith: Lean 4 or Rocq as primary?

### Refinement Loop (Claude-run, no API)

```
seed: supersession decision for repl-engine-split-mvp-…
candidate: chosen option (a)/(b)/(c) + corrected scope (if retained)
evaluate:
  - run buildkit-roadmap status → verify all successors reachable
  - audit Appendix B two-way traceability
  - topological check: no forward deps
  - if retained: run test/run_all_tests.sh → 384/384 green before any code change
GEPA mutation:
  - if traceability gap → add missing §-anchor to the affected seed memo
  - if forward dep found → reorder §11 entries
  - if scope retained with mis-framing → correct premise (T3 Option 1)
repeat until: all pragmatic thresholds hold + owner confirms decision
terminate: owner signs off at /buildkit-specify start
```

---

## Formal Tooling

### Lean 4 vs Rocq Evaluation

**For this seed specifically** (supersession decision — no mechanized proof required):

- **Lean 4 fit:** N/A for the supersession step. For any wire-contract work in a retained monolith, Lean 4 (+ mathlib) fits well: its dependent type system and `#eval`/`#check` tactics support byte-level encoding proofs; the APOLLO/Lean-LSP-MCP agentic connectors make Claude-driven tactic loops natural. GLP bytecode is WAM-lineage → the TWAM Lean4 precedent (if it exists) or a Rocq port are both tractable.
- **Rocq fit:** N/A for the supersession step. For verified-compiler-style proofs (compiled-exec ≡ source-interp), Rocq has the deepest prior art (verified Prolog→WAM compiler, Vellvm). The AutoRocq agentic loop applies directly — with the GPT-4 API dependency replaced by Claude-via-Agent-seam per the Brief §3.2a no-API resolution.
- **Primary proof assistant:** `n/a` — this seed needs no mechanized proof. The decision step is structural/organizational.
- **Alternative when:** none for the supersession step. For any retained/re-purposed variant that touches the IL codec or GLP semantics, the primary should be evaluated per the successor seed (see #4, #5, #11 memos).

### IL Verification

**n/a** for the supersession decision step. For any retained monolith that reaches IL-codec work: see the IL-verification plan in seed #4 (`il-codec-spike`) and #11 (`compiled-il-on-the-wire-and-factor-out-compiler`) — MLIR multi-level IR with a GLP/FCP dialect (HEAD-unify / GUARD-test / BODY-spawn / suspend-reactivate primitives), progressively lowered; byte-parity proofs FR-060/061; `decode(encode(p)) ≡ p` round-trip identity; schema conformance; self-containment / no-heap-leak invariants. The mis-attributed `arxiv 2502.06854v1` link from the Brief §3.2 is noted as the "LLMs struggle with IR control flow" warning — a real risk for any Claude-driven IL codec, regardless of which path is chosen.

---

## Shapiro Criteria Preserved

For the supersession/re-purpose decision step, no runtime code changes — no Shapiro criteria are in play. However, any retained or re-purposed monolith that proceeds to implementation MUST preserve:

1. **SRSW (single-reader/single-writer):** every `.glp` clause loaded or generated during epic work must pass the SRSW check. The wire-encoding path must not introduce shared mutable state that violates the single-owner heap invariant (`heap_fcp.cs:136–141`).
2. **Suspension correctness:** a goal suspended on an unbound `VarRef` must reactivate exactly when its writer is bound — the result-envelope codec (successor #5) must not drop or mis-encode blocking-reader information.
3. **Monotone variable binding:** once a writer is bound, the binding is permanent — the heap snapshot (#7) must capture the full `WriterContent` chain atomically at quiescence; no mid-reduction snapshot.
4. **Committed-choice concurrency:** once a clause head unifies in the HEAD phase and all guards pass, the commit is permanent — no backtracking. The process-split (#6) must not introduce a retry mechanism that simulates backtracking across the wire.
5. **Three-valued unification (Success / Suspend / Fail):** the result-envelope codec must faithfully round-trip all three outcomes — including unbound-var encoding for Suspend outcomes.

These criteria are adapted to the embedded-switch purpose: the engine acts as a connectivity switch (external world ↔ OS/actor layer via GLP channels) and as a host for QHSM/HSM actors. The split must preserve these guarantees end-to-end across the process boundary, not just within a single process.

---

## Recommendation

**CLOSE as superseded (T1 Option 1).**

The dossier's decomposition (#2–#16) is complete, topologically valid, and covers every capability the monolith claimed — with two premises corrected (§9.1, §9.2) and the dependency/risk structure made explicit. Keeping the monolith open creates confusion about whether to run `/buildkit-specify` for it alongside the decomposed seeds. The epic `epic-separation-of-repl-front-end-from-engine-execution-scheduler` already serves the tracker role (T1 Option 2 is redundant). The RETAIN path (Option 3) sacrifices the incremental-delivery and de-risking benefits at no gain.

**Mark the seed `closed/superseded` in the roadmap with a pointer to the dossier (§11) and the epic. All work proceeds through the 15 decomposed successor seeds in topological order.**

---

## Options for Owner

| Label | Consequence |
|---|---|
| (a) CLOSE as superseded | Clean break: mark the seed closed, pointing at the dossier §11 + the epic. All work proceeds through #2–#16. No `/buildkit-specify` for the monolith is ever run. Zero work is lost. |
| (b) RE-PURPOSE as epic umbrella/tracker | Rename the seed to an epic-tracker entry; it closes only when all #2–#16 close. Adds pipeline overhead (a non-buildable "feature" at the top). Acceptable if the roadmap UI needs a top-level rollup entry. |
| (c) RETAIN as monolithic path | Accept the combined risk: ALL of result-envelope, IL codec, compiler relocation, liveness, persistence, and multi-accept in one delivery. Loses incremental validation; the IL codec is the single hardest unknown and would block the entire MVP. Not recommended. |

---

## Open Questions

1. Does the owner want a roadmap-level tracker entry above the 15 seeds (Option b) or is the epic sufficient?
2. If closed, should the dossier Appendix B row 1.5 carry a `status: superseded` marker explicitly, or is the reconciliation memo sufficient as the audit trail?
3. The seed's stored WSJF=2 / RICE=2077 scores were entered against the monolith scope. Should these scores be redistributed / recalculated across the decomposed seeds, or noted as historical only?

---

## External References

- Dossier: `docs/research/repl-engine-separation/design-dossier.md` (§8, §9, §11, Appendix B row 1.5)
- Methodology brief: `docs/research/repl-engine-separation/reconciliation/SEED-RECONCILIATION-BRIEF.md` (§3.2, §3.2a, §3.5)
- TWAM (certifying abstract machine for logic programs): https://arxiv.org/pdf/1801.00471
- Verified Prolog→WAM compiler: https://www.sciencedirect.com/science/article/pii/0743106692900547
- First-Class Verification Dialects for MLIR (PLDI'25): https://users.cs.utah.edu/~regehr/papers/pldi25.pdf
- APOLLO (model-agnostic agentic Lean proving): https://arxiv.org/abs/2505.05758
- LLM comprehension of LLVM IR (the "LLMs struggle with IR control flow" warning): https://arxiv.org/html/2502.06854v1
- AutoRocq (autonomous Rocq proof agent — adapt off GPT-4 dependency): https://github.com/NUS-Program-Verification/AutoRocq
- `csharp/glp_link/reliability/FrameCodec.cs` (FR-060/061 byte-parity anchor)
- `out/csharp/lib/bytecode/runner.cs` (`BytecodeProgram`, `ToDisassembly`)
- `out/csharp/lib/engine/glp_engine.cs` (`GlpEngine`, `ExecutionResult`, `_ResolveDeepForTrace`)
- `out/csharp/bin/glp_repl.cs` (`AfterEngineCreated` at :47)
- `out/csharp/glp_repl/Program.cs` (composition root :30–35)
- `programs/self.glp` (`mwm` :387–422, `Link` :456, `link_send` :536, `link_recv` :548, `request_listener` :513–516)

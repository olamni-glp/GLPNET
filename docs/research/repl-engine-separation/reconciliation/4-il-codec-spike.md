# Seed Reconciliation Memo — #4 il-codec-spike

**Feature id:** `il-codec-spike`
**Dossier entry:** §11 #4
**Kind (dossier):** EXPERIMENT
**Date:** 2026-06-09
**Branch:** 026-engine-review-dossier

---

## Dossier cross-references

- §2.1 — `BytecodeProgram` structure: heterogeneous instruction list, dual v1/v2 opcode families, `Labels` dict (`runner.cs:41-73`)
- §2.2 — What an IL codec must capture: ordered instructions, both opcode families, Label markers, `CompilationResult.VariableMap`, `ModuleTerm`-embedded `BytecodeProgram`s on the heap
- §3 — Wire reuse decision: dedicated IL codec (codec-A) riding `FrameCodec`; may reuse `PayloadSerializer` constant sub-tag scheme; `FrameCodec.cs:42,45,52,56-62`
- §0.4 — Classification table row: "IL / bytecode wire codec" → **net-new**, zero in repo, substrate `opcodes.cs`/`opcodes_v2.cs`/`runner.cs:41`
- §9.1 — Premise reconciliation: compiler is engine-internal; wire carries source text for MVP; IL codec becomes needed only in #11 (compiled-IL-on-wire)
- §9.2 — No runtime IL synthesis; compiled programs circulate as heap data (`ModuleTerm`), so the IL codec is also required for state persistence (#7)
- §12 risk 7 — Cross-runtime byte-parity for the new codecs if the Dart mirror is kept (v1/v2 opcode split complicates a stable format)
- Appendix B — Seed registry row #4: motivating §-anchors §2.1, §2.2, §3, §0.4

---

## Seed-vs-dossier-vs-code

### Roadmap brief (stored profile)

The `buildkit-roadmap brief` output reads:

> EXPERIMENT (throwaway-or-keep). Prove BytecodeProgram<->bytes round-trip (both opcode families + recursive constant terms + labels + VariableMap) via compile->encode->decode->execute-equivalence. De-risks the hardest unknown (no codec exists). depends-on: #1. (§7 #4)

The `(§7 #4)` note is the old investigation.md reference numbering; the current dossier numbering is §11 #4. This is a harmless stale label in the stored profile — the content matches.

Stored WSJF = 5.2, RICE = 3000. Effort = M.

**Problem/need, Target-user, Value, Risk** fields are blank — sparse profile, consistent with a raw captured seed not yet through the full `review` cycle.

### Dossier entry (§11 #4)

| Field | Value |
|---|---|
| Kind | EXPERIMENT |
| Scope | Prove `BytecodeProgram`↔bytes round-trip (both opcode families + recursive constant terms + labels + `VariableMap`) via compile→encode→decode→execute-equivalence |
| Why | De-risks the single hardest unknown (no codec exists; dual opcode families; recursive constants) |
| depends_on | #1 only |
| §ref | §2.1, §2.2, §3 |

The roadmap brief matches the dossier exactly in scope and depends_on. The dossier supplies the richer motivation (§2.2, §3, §9.2), which the brief elides. No factual divergence.

### Code verification (file:line)

**`BytecodeProgram` at `out/csharp/lib/bytecode/runner.cs:41-73`** — confirmed. Fields:
- `IReadOnlyList<object> Instructions` (`:44`) — heterogeneous; holds both `IOp` (v1) and `IOpV2` (v2) instances and `Label` markers.
- `Dictionary<string, int> Labels` (`:47`) — built by `IndexLabels` (`:61-73`).
- `ToDisassembly()` (`:88`) — human-readable only; no serialization path.

**Zero serialization path confirmed.** `grep` of `Serialize|Encode|ToBytes` across `out/csharp/lib/bytecode/*.cs` returns only a comment (`opcodes.cs:172`): `// v2.16 HEAD instructions (encode clause patterns)` — not code.

**Dual opcode families confirmed** (`out/csharp/lib/bytecode/opcodes.cs`, `opcodes_v2.cs`):
- v1 `IOp` interface: ~50+ concrete classes (`ClauseTry`, `GuardFail`, `Commit`, `ClauseNext`, `BodySetConst`, `UnifyConstant`, `HeadStructure`, `GetVariable`, `GetValue`, `HeadBindWriter`, `GuardNeedReader`, etc.)
- v2 `IOpV2` interface (`opcodes_v2.cs:13`): `HeadVariable`, `GetVariable`, `GetValue`, `UnifyVariable`, `PutVariable`, `SetVariable` — each carrying `IsReader bool`
- Both namespaces coexist in a single `Instructions` list at runtime; `asm.cs:BC` factory exposes both

**Recursive constant terms confirmed** (`out/csharp/lib/compiler/codegen.cs:735-759`): ground list `ListTerm` → `Rt.StructTerm` → embedded as `UnifyConstant(Rt.StructTerm)` operand. The `Value` field of `UnifyConstant` is `object?` (`opcodes.cs:210`) — can hold a recursive `Rt.StructTerm`. A constant sub-encoder must walk this tree recursively.

**`CompilationResult.VariableMap`** at `out/csharp/lib/compiler/result.cs:9` — `Dictionary<string, long>` mapping variable name to register index. This is the metadata the codec must carry alongside the `BytecodeProgram`.

**`CombinedProgram` label mutation** at `out/csharp/lib/engine/glp_engine.cs:416-463`: the `CombinedProgram` getter constructs a merged program and **removes** non-exported labels from the `Labels` dict (`:455-460`). So the codec must decide: serialize the per-module raw `BytecodeProgram` (full labels) or the post-filter `CombinedProgram` (scoped labels). These are different objects with different `Labels` contents. The dossier notes this at §2.2 but does not flag it as a formal decision — it is an under-specification.

**`ModuleTerm`-embedded `BytecodeProgram`** at `out/csharp/lib/runtime/terms.cs:146-156` and `glp_activation.cs:78-89`: the `ModuleTerm.Bytecode` field is typed `object` (`:149`; "untyped to avoid circular import") but is always a `BytecodeProgram`. It is stored on the heap via `rt.Heap.StoreTermOnHeap(moduleTerm)` (`glp_activation.cs:89`). The IL codec must recurse into the heap to find these when serializing state.

**`PayloadSerializer` tag scheme** at `out/csharp/lib/multiagent/payload_serializer.cs:85-88`: tags `TagConstant=1/Variable=2/Struct=3/List=4`; constant subtypes nil/int64/double/string/bool. The IL codec may reuse these for the `object? Value` sub-encoder (ground constants only; recursive `Rt.StructTerm` needs additional handling).

**`FrameCodec`** at `csharp/glp_link/reliability/FrameCodec.cs:39-64`: version byte 0x01, 22-byte header, `Kind` byte at offset 1 (`:64`), CRC-32, MTU fragmentation. The `Kind` field is a `FrameKind` enum (`:6-13`) with `Whole=0` and `Fragment=1` — the dossier says to distinguish an IL frame by `Kind` byte, but the current enum has no IL-kind variant. Adding a new `Kind` value (`ILPayload`, `ResultEnvelope`) is the required extension.

---

## Classification check

**Kind = EXPERIMENT: correct.** The scope is explicitly "prove round-trip" — a throwaway-or-keep spike, not a production feature. The dossier's §11 "EXPERIMENT" tag fits: no codec exists, the dual-family design is unprecedented in this codebase, and the recursive-constant embedding makes the serialization non-trivial.

**Code supports scope:** yes, with one gap. The dossier's §2.2 codec scope list is confirmed by code:
- ordered `Instructions` — `runner.cs:44` ✓
- both opcode families — `opcodes.cs`, `opcodes_v2.cs` ✓
- `Label` markers — `opcodes.cs:16-19` ✓
- `VariableMap` — `result.cs:9` ✓
- recursive constant terms — `codegen.cs:735-759` ✓
- `ModuleTerm`-embedded programs — `terms.cs:146`, `glp_activation.cs:88` ✓

**Gap not surfaced in dossier:** `CombinedProgram` at `glp_engine.cs:416` mutates the `Labels` dict post-merge — the codec target (raw per-module program vs. post-filter combined program) is not decided. This changes the round-trip semantics: a codec that serializes/deserializes a `CombinedProgram` will lose the private-label entries, which changes the label lookup result.

---

## Tensions

### T1 — Scope of the spike vs. its downstream consumers

**Summary:** The dossier frames il-codec-spike as standalone de-risking, but its two downstream consumers (#7 engine-state persistence and #11 compiled-IL-on-wire) have divergent requirements: persistence needs heap-embedded `ModuleTerm` round-trip; the wire only needs per-module programs in the forward direction.

**Evidence:**
- §9.2: "compiled programs circulate as runtime heap data (`ModuleTerm`), any state snapshot must serialize `BytecodeProgram` instances inside the heap" — couples the codec to persistence.
- §11: #11 depends-on #4 and #6 (MVP) — the wire codec is a post-MVP follow-up.
- If the spike targets the full codec (incl. heap-embedded `ModuleTerm`), it blocks on understanding the heap snapshot design (#7), which depends on #6 (MVP). If it targets only the forward-direction per-module codec, it under-delivers for #7.

**Options:**
1. Split the spike into two sub-proofs: (a) per-module `BytecodeProgram` round-trip (unblocks #11); (b) heap-embedded `ModuleTerm` codec (feeds #7). Serialize in the same codec, test separately.
2. Scope the spike to only (a); defer (b) to #7. Explicitly mark the `ModuleTerm` case as a known gap in the spike's deliverable.
3. Do the full combined codec now, accepting that it must be designed alongside the heap snapshot model even though #7 is not started.

### T2 — `FrameCodec.Kind` has no IL or ResultEnvelope variant

**Summary:** The dossier says the IL codec rides `FrameCodec` "distinguished by the header `Kind` byte" (§3, `FrameCodec.cs:64`), but the current `FrameKind` enum (`FrameCodec.cs:6-13`) only has `Whole=0` and `Fragment=1` — no IL/result slots.

**Evidence:** `csharp/glp_link/reliability/FrameCodec.cs:6-13` (enum); `FrameCodec.cs:64` (the dossier reference is to the constant `OffKind = 1` — the byte offset of the kind field in the header, not a `Kind` enum value). The dossier is internally consistent but the distinction between "frame kind" (fragmentation) and "payload kind" (what the payload means: IL vs result) is conflated. Riding `FrameCodec` with a new `Kind` value would break the current fragmentation-semantics contract.

**Options:**
1. Add a `payload_type` byte to the payload header (not the frame header) to distinguish IL from result envelopes; leave `FrameKind` unchanged for fragmentation.
2. Add new `FrameKind` values (`ILPayload=2`, `ResultEnvelope=3`) and extend the codec's decode path; the fragment/whole dimension is orthogonal.
3. Use an outer envelope wrapper: always `FrameKind.Whole/Fragment` for transport; the first byte of the reassembled payload is the payload-type discriminant.

Option 3 is the cleanest architectural separation (transport framing vs. payload typing) and avoids touching the `FrameCodec` contract.

### T3 — Execute-equivalence harness: what "equivalence" means for v1/v2 mixed programs

**Summary:** The spike's success criterion is "compile→encode→decode→execute-equivalence." But the v1 and v2 opcode families are semantically equivalent at the execution level (the runner handles both), and `codegen.cs` emits v2 for new code while v1 may appear in legacy loaded programs. A mixed round-trip (decode produces v2 where original had v1) could pass execute-equivalence but fail structural equality.

**Evidence:** `codegen.cs:209` (dossier cites this for v2 emission); `runner.cs:3-16` (handles both families in the dispatch loop). `asm.cs:48-51` mixes `BC.*` (v1) and `V2.*` (v2) in the same builder.

**Options:**
1. Define equivalence as execute-equivalence only (same final bindings/status); structural decode need not reproduce the original opcode family for each instruction.
2. Define equivalence as structural identity (decode must reproduce the exact same opcode objects) — requires the codec to preserve the v1/v2 tag per instruction.
3. Mandate structural identity for v2 (the active family) and allow v1→v2 normalization for legacy opcodes, with an explicit normalization table.

Option 2 is the most rigorous for a verification spike and is the recommendation (it also establishes the byte-parity contract FR-060/061 requires).

---

## Under-specifications

### U1 — Raw per-module program vs. CombinedProgram as the codec target

**Why it matters:** `CombinedProgram` (`glp_engine.cs:416`) strips private labels; a round-tripped `CombinedProgram` cannot restore the original per-module programs' full label tables. If the codec targets `CombinedProgram`, persistence (#7) cannot replay per-module programs with their private labels. If it targets raw per-module programs, the codec must be applied N times (once per loaded program) and the persistence layer reassembles the combined view.

**Options:**
- Codec targets raw per-module `BytecodeProgram` (private labels preserved); the engine's merge/filter step is re-applied on load. This is the correct choice for persistence.
- Codec targets `CombinedProgram`; accept label-table truncation. Acceptable only if persistence always recompiles from source (§10.8 Opt 2).

### U2 — `VariableMap` scoping: per-clause vs. per-goal vs. per-module

**Why it matters:** `CompilationResult.VariableMap` (`result.cs:9`) is a per-compilation unit map (variable name → register). When the spike encodes a `BytecodeProgram` + `VariableMap` pair, the scope of the map needs to be defined: is it the goal's map (from `queryVarWriters`, `glp_engine.cs:515`), the module's map, or the combined program's map? These are different objects with different cardinalities.

**Options:**
- Encode only the goal-level `VariableMap` (for the result envelope context — this feeds into seed #2, not this spike).
- Encode the module-level `VariableMap` per loaded program (for persistence).
- Encode both, with a type tag.

### U3 — `Obsolete` v1 opcodes (`UnionSiAndGoto`, `ResetAndGoto`) in the codec

**Why it matters:** `opcodes.cs:53-66` marks `UnionSiAndGoto` and `ResetAndGoto` as `[Obsolete]`. The codec must decide whether to support encoding/decoding these (for round-tripping legacy programs) or to normalize them to `ClauseNext` on decode. If the codec simply errors on obsolete opcodes, any legacy bytecode in `_loadedPrograms` will fail to serialize.

**Options:**
- Include obsolete opcodes in the codec with a discriminant; round-trip exactly.
- Normalize obsolete opcodes to their `ClauseNext` replacement on encode; drop on decode.
- Error on obsolete opcodes; mandate that legacy programs are recompiled before serialization.

### U4 — `object?` constant value types that go beyond `PayloadSerializer` primitives

**Why it matters:** `UnifyConstant.Value` and `BodySetConst.Value` are typed `object?` (`opcodes.cs:210,77`). `PayloadSerializer` handles nil/int64/double/string/bool (`payload_serializer.cs:85-88`). But `codegen.cs:759` embeds a `Rt.StructTerm` as a `UnifyConstant` value. The codec must enumerate all concrete `object?` value types that appear in practice and handle them. If any type is missing from the encoder, the round-trip silently drops or corrupts that constant.

**Options:**
- Enumerate: nil | int64 | double | string | bool | Rt.StructTerm (recursive) | Rt.ConstTerm | null. Verify by scanning all `codegen.cs` emit paths.
- Add a fallback "serialize via `ToString()`" for unknown types with a warning; not round-trippable but prevents silent corruption.
- Hard-error on any value type not in the explicit whitelist.

---

## GEPA/DSPy refinement

### Applicability: `methodological`

The il-codec-spike is systems/C# code (a binary serialization format + a correctness harness), not an LM/codegen program that DSPy literally optimizes. GEPA/DSPy applies **methodologically**: the iterate-against-a-metric discipline (define the codec schema → implement → test round-trip equivalence → measure formal + pragmatic gates → mutate the schema where the gates fail → repeat) mirrors the GEPA reflective mutation loop without needing an LM in the inner loop. The LM (Claude via Agent-tool seams) drives: schema design, opcode-discriminant assignment, edge-case identification, and tactic generation for the formal IL verification.

### Seed definition

**Input:** The `BytecodeProgram` data model (`runner.cs:41-73`), dual opcode class hierarchies (`opcodes.cs` v1 `IOp`, `opcodes_v2.cs` v2 `IOpV2`), recursive-constant operand embedding (`codegen.cs:735-759`), `CompilationResult.VariableMap` (`result.cs:9`), and the `FrameCodec` transport layer (`FrameCodec.cs`).

**Output:** A self-contained `IlCodec` class (or namespace) that:
1. Encodes a `BytecodeProgram` (+ optional `VariableMap`) to a `byte[]` payload.
2. Decodes a `byte[]` payload back to a structurally identical `BytecodeProgram` (+ `VariableMap`).
3. Passes an execute-equivalence harness: for a representative set of GLP programs, `compile → encode → decode → execute` produces the same `ExecutionResult` as `compile → execute`.
4. Rides `FrameCodec` as a payload (payload-type discriminant in the payload header, not the frame header).

### Metrics combination

| Name | Kind | Tool | Threshold |
|---|---|---|---|
| Round-trip identity | pragmatic | C# xUnit test: `decode(encode(p)) == p` (structural equality over all fields) | 100 % on a corpus of ≥ 10 compiled GLP programs covering v1-only, v2-only, mixed v1/v2, recursive-constant, Label, empty-program cases |
| Execute-equivalence | pragmatic | C# xUnit test: run a GLP goal against the original program and against the round-tripped program; assert identical `ExecutionResult` (status + bindings + error) | 100 % on the same corpus |
| Opcode-family coverage | pragmatic | Code-coverage gate on the encoder/decoder: every concrete `IOp` and `IOpV2` subclass must be exercised by at least one encode + decode test | 100 % branch coverage of the discriminant switch |
| Constant-type coverage | pragmatic | The encoder's constant-value branch must handle every concrete `object?` type emitted by `codegen.cs` | Zero `NotSupportedException` / silent-null on the full `programs/` corpus compiled and encoded |
| Byte-parity (future Dart mirror) | formal | Byte-level test: the C# encoder and a Dart mirror encoder of the same program must produce byte-identical payloads | Byte-identical on ≥ 3 representative programs (can be deferred to #11 if Dart mirror is not in scope for this spike) |
| IL round-trip soundness (formal) | formal | Lean 4 mechanized proof: `decode(encode(p)) = p` as a theorem over the codec's inductive type for `BytecodeProgram`; start with a simplified model (one opcode family, ground constants) and extend | Proof compiles with zero `sorry` in the simplified model |
| Opcode-discriminant uniqueness | formal | Z3 / CVC5 SMT: assert that the encoder assigns distinct discriminant bytes to every concrete opcode class (no aliasing) | UNSAT on the collision formula |

### Interactive spec step

At the start of `/buildkit-specify il-codec-spike`, the owner confirms:

1. **Codec target:** raw per-module `BytecodeProgram` (recommended) or `CombinedProgram` (see U1).
2. **`ModuleTerm`-in-binding scope:** include `ModuleTerm` heap traversal in this spike or defer to #7 (see T1).
3. **Obsolete opcode handling:** round-trip exactly, normalize, or error (see U3).
4. **Formal tool primary:** Lean 4 (recommended, see formal tooling section) or Rocq.
5. **Byte-parity with Dart:** in scope for this spike or deferred to #11.
6. **`FrameKind` extension strategy:** payload-type byte in payload header (recommended, see T2 option 3) or new `FrameKind` values.

### Refinement loop (Claude-run, no API)

```
epoch 0: define the opcode-discriminant table and the constant-value type enum
         → propose in a spec document; owner reviews
epoch 1: implement encode for v1 IOp family; implement decode; run round-trip tests
         → measure: round-trip identity + opcode-family coverage for v1
epoch 2: extend to v2 IOpV2 family; extend tests
         → measure: same gates, now including v2 cases
epoch 3: implement recursive-constant sub-encoder (Rt.StructTerm recursive walk);
         extend tests with ground-list programs
         → measure: constant-type coverage
epoch 4: add VariableMap codec; add execute-equivalence harness; run on programs/ corpus
         → measure: execute-equivalence; constant-type coverage on full corpus
epoch 5: begin Lean 4 model (simplified: one family, ground constants); generate tactics
         via Lean-LSP-MCP / Agent-tool seam; iterate until proof compiles
         → measure: IL round-trip soundness (simplified model)
epoch 6 (if in scope): Z3 discriminant-uniqueness check; byte-parity with Dart
         → measure: opcode-discriminant uniqueness; byte-parity
terminate when: all in-scope metric thresholds hold simultaneously
```

---

## Formal tooling

### Lean 4 vs Rocq evaluation

**Lean 4 fit:** Strong. The codec's core correctness property (`decode(encode(p)) = p`) is a clean inductive-type theorem over a finite, recursively-defined data structure (`BytecodeProgram` as an inductive list of discriminated-union opcodes). Lean 4's `inductive` types, `simp` + `decide` tactics, and `mathlib` list-lemmas give direct leverage. The `Lean-LSP-MCP` connector (Claude-native via MCP) and `Lean Copilot` (`suggest_tactics`) allow Claude to drive the tactic loop without any OpenAI API dependency. The `APOLLO` framework (model-agnostic, runs with Claude via Agent seams) handles `sorry`-isolation and sub-goal repair. Lean 4's dependent-type expressiveness is overkill for a pure round-trip property but enables later extension to execution-semantics correctness without switching tools.

**Rocq fit:** Also strong. Rocq's `Coq.Lists.List` library has direct `map`/`fold`/encode-decode lemmas; `Vellvm` (Coq) is the closest prior-art template (verified LLVM IR codec). `AutoRocq` (Rocq autonomous proof agent) would be the agentic driver, but its GPT-4 dependency must be adapted away (Claude drives the tactic loop instead, per the no-API resolution). Rocq has more verified-compiler prior art in the WAM/logic-language space (the Pusch/KIV line used Isabelle; TWAM used LF; but the general verified-compiler community in Coq is larger). The certified-Prolog-compiler line (`ScienceDirect 0743106692900547`) is Isabelle, not Coq, so the direct prior-art advantage is less strong than it first appears.

**Primary:** `lean4`

**Rationale:** The round-trip property is a pure structural theorem over an inductive list — Lean 4's `decide` + `simp` automation handles it with less boilerplate than Rocq. The Claude-native `Lean-LSP-MCP` connector eliminates the API-adaptation step. Lean 4 is the owner's stated preference across the epic (methodology brief §3.2a). The `APOLLO` model-agnostic loop (arxiv 2505.05758) gives sorry-repair without tool-switching.

**Alternative when:** If the proof grows to involve the execution semantics (phase correctness, three-valued unification soundness) rather than just the codec's round-trip, Rocq becomes preferable due to the larger verified-WAM prior-art base (TWAM, the Pusch line, `Vellvm` as a template). Keep Rocq as a documented fallback for the execution-semantics extension in seed #11.

### IL verification approach

This seed directly touches the IL (GLP bytecode). The IL verification approach:

1. **Byte-contract (pragmatic):** round-trip identity (`decode(encode(p)) ≡ p`) and execute-equivalence as defined in the metrics table. These are the primary correctness gates.

2. **MLIR-dialect / progressive-lowering layer (methodological, not in this spike's MVP scope):** The dossier's §3.2 describes a GLP/FCP MLIR dialect whose primitives are HEAD-unify / GUARD-test / BODY-spawn / suspend-reactivate, lowered progressively toward the runtime. For this spike, the MLIR-dialect layer is a design goal for a follow-on (#14/#16 research program) — it defines the *target* IR shape that the IL codec's output should eventually align with. The spike should design the discriminant table and opcode encoding in a way that is compatible with a future MLIR dialect mapping (i.e., each opcode maps to a named MLIR operation without ambiguity). This is a design constraint, not an implementation requirement for the spike itself.

3. **Lean 4 formal model (in scope for the spike's formal gate):** Model `BytecodeProgram` as `List Opcode` where `Opcode` is an inductive type (one constructor per concrete IOp/IOpV2 class); model `encode` and `decode` as functions over this type; prove `decode ∘ encode = id`. Start with a simplified model (v1 family only, ground constants), extend to v2 and recursive constants. The TWAM certifying abstract machine (arxiv 1801.00471) and the verified Prolog→WAM compiler (ScienceDirect 0743106692900547) are the direct precedents for the execution-semantics extension.

4. **Citation to be pinned (per BRIEF §6 open item):** The "Typed Multi-level Datalog IR" reference (the `2502.06854` link was mis-attributed — it is actually an LLM-IR-comprehension study). Pin the correct Souffle/MLIR-Datalog-IR paper during this spike.

---

## Shapiro criteria preserved

This step (proving the IL codec is correct) does not execute GLP programs; it operates on the compiled representation. Nevertheless, the following Shapiro/GLP design criteria must be preserved by the codec design, because any codec that silently mutates bytecode semantics will violate them downstream:

1. **SRSW (Single-Reader / Single-Writer):** The codec must preserve the reader/writer polarity encoded in each opcode. v2 `IOpV2` opcodes carry an `IsReader bool` (`opcodes_v2.cs:32,60,88`); v1 has separate `GetVariable`/`GetValue` classes vs. the `HeadBindWriter`/`GuardNeedReader` classes. A codec that swaps `IsReader` or conflates v1/v2 writer-vs-reader classes violates SRSW in the decoded program. **Gate:** the round-trip identity test checks structural equality including `IsReader`.

2. **Monotone binding / Writer MGU:** The opcode sequence encodes the three-phase HEAD/GUARD/BODY order and the writer-MGU constraint. The codec must preserve the phase-ordering of opcodes (HEAD ops before GUARD ops before BODY ops in the instruction list). A codec that reorders opcodes within a clause breaks monotone binding. **Gate:** the execute-equivalence harness checks that the decoded program produces the same final bindings.

3. **Suspension correctness:** `ClauseNext`, `TryNextClause`, `NoMoreClauses`, `SuspendEnd` opcodes implement the suspension/reactivation control flow (`opcodes.cs:35-68`). A codec that drops or mutates these produces programs that either never suspend (lose suspension correctness) or never resume. **Gate:** the corpus must include programs that reach `SuspendEnd`; execute-equivalence must check that `ExecutionStatus.Suspended` is preserved.

4. **Committed-choice concurrency:** The `Commit` opcode (`opcodes.cs:28`) is the committed-choice boundary; everything after it is a BODY mutation. The codec must preserve the position of `Commit` within each clause's instruction sequence. **Gate:** structural identity test on `Commit` opcode positions.

5. **Three-valued unification (Success/Suspend/Fail):** The opcodes `GuardNeedReader`, `GuardFail`, `Otherwise`, `ClauseNext`, `NoMoreClauses` collectively implement three-valued unification outcomes. The codec must preserve each of these without aliasing. **Gate:** opcode-discriminant uniqueness (Z3) ensures no aliasing; structural identity test ensures no dropping.

---

## Recommendation

**Proceed with the spike as scoped in the dossier**, with the following refinements:

1. Explicitly scope the spike to raw per-module `BytecodeProgram` round-trip (not `CombinedProgram`), with `ModuleTerm`-in-binding deferred to #7. This avoids blocking on the heap snapshot design.
2. Design the opcode-discriminant table as a fixed-width 1-byte discriminant for v1, a separate 1-byte discriminant for v2, and a "family" prefix byte — making the codec self-describing and extensible without aliasing.
3. Add a payload-type byte in the IL payload header (not the `FrameKind` enum) to ride `FrameCodec` cleanly.
4. Use Lean 4 as the primary formal tool for the round-trip proof; start with the simplified model, deliver a sorry-free proof for v1+ground constants before v2.
5. Pin the correct Typed-Datalog-IR citation during the spike.

---

## Options for owner

| Label | Consequence |
|---|---|
| A — Scope spike to per-module round-trip only, defer heap-embedded ModuleTerm | Unblocks #11 quickly; #7 (persistence) must handle ModuleTerm separately; reduces spike complexity |
| B — Include heap-embedded ModuleTerm in scope | Full codec ready for #7 and #11; but requires understanding heap snapshot layout before #7 has started; higher risk |
| C — Formal proof in Lean 4 (recommended) | Establishes the byte-contract formally; requires Lean 4 environment setup (Lean-LSP-MCP); adds ~1 epoch to the spike |
| D — Formal proof deferred (pragmatic tests only for now) | Faster spike; byte-contract formally unproven; the proof becomes a prerequisite for #11 instead |
| E — Payload-type in payload header (recommended) | No change to FrameCodec contract; clean separation; slightly more bytes in each payload |
| F — New FrameKind values for IL and result | Cleaner frame-level dispatch; requires updating FrameCodec contract and all readers; breaking change to feature-025 tested path |

---

## Open questions

1. What is the complete set of concrete `object?` value types that appear as operands across the full `programs/` corpus when compiled? (U4 — needed to finalize the constant sub-encoder whitelist before the encode path is locked.)
2. Does the Dart `BytecodeRunner` use the same v1/v2 opcode discriminant assignment as C#, or does the Dart source have different class identities? (Needed for byte-parity FR-060/061 if the Dart mirror is kept.)
3. Is the `FrameKind` enum in `FrameCodec.cs` intended to remain the sole kind-tag surface, or was a payload-type field always planned for the payload layer? (Resolves T2 cleanly; ask the feature-025 designer.)
4. Are there any `IOp` or `IOpV2` subtypes that appear in the `Instructions` list beyond the classes defined in `opcodes.cs` and `opcodes_v2.cs`? (E.g., could a plugin or test inject a custom `IOp` subtype? If so, the codec needs an "unknown opcode" escape hatch.)
5. The `BytecodeProgram.Labels` dict is reconstructed by `IndexLabels` from the instruction list — it is a derived field. Should the codec round-trip the `Labels` dict (redundant but faster on decode) or recompute it from the decoded instruction list? (Affects whether the codec is canonical.)

---

## External references

- [TWAM: A Certifying Abstract Machine for Logic Programs (arxiv 1801.00471)](https://arxiv.org/pdf/1801.00471) — direct precedent for WAM-lineage IL correctness proofs; certifying compiler for T-Prolog using typed compilation
- [A Verified Prolog Compiler for the Warren Abstract Machine (ScienceDirect 0743106692900547)](https://www.sciencedirect.com/science/article/pii/0743106692900547) — foundational result: compiled-execution ≡ source-interpretation; the correctness statement this spike's execute-equivalence harness mirrors
- [The BinProlog Experience: Architecture and Implementation Choices (arxiv 1102.1178)](https://arxiv.org/abs/1102.1178) — BinWAM binary format and `compile/1` to `.bp` disk format; prior art for a WAM-lineage bytecode serialization; tag-on-data representation relevant to the constant sub-encoder design
- [First-Class Verification Dialects for MLIR (PLDI 2025)](https://users.cs.utah.edu/~regehr/papers/pldi25.pdf) — MLIR verification dialect making semantics first-class; relevant to the long-range GLP/FCP dialect design that the IL codec's discriminant table should align with
- [APOLLO — Model-agnostic agentic Lean prover (arxiv 2505.05758)](https://arxiv.org/abs/2505.05758) — the Claude-driven agentic ITP loop for sorry-isolation and repair; the no-API Lean tactic driver for the formal gate
- [LLM comprehension of LLVM IR (arxiv 2502.06854v1 — mis-attributed in brief)](https://arxiv.org/html/2502.06854v1) — retained for its finding: LLMs struggle with IR control-flow reasoning; a real risk for Claude-driven IL codec design (mitigated by the formal Lean proof as the ground-truth gate)
- [KLIC: A KL1 implementation for Unix systems (Springer)](https://link.springer.com/article/10.1007/BF03038274) — KL1/FCP C-translation approach; KLIC compiles to C rather than a binary bytecode, so does not directly model this spike's serialization, but confirms the GHC/FCP lineage of GLP's concurrency semantics

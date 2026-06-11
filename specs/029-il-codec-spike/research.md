# Phase 0 Research — IL/Bytecode Round-Trip Codec Spike

All unknowns are resolved. The three scope-defining forks were settled in `/buildkit-clarify`
(spec Clarifications, 2026-06-11); the lower-impact forks were settled by the seed's
recommendations and recorded as spec Assumptions A1–A8. This file pins the remaining
implementation-level decisions (D1–D8) that the seed left as open questions.

## Settled forks (from clarify + assumptions)

| Ref | Decision | Source |
|---|---|---|
| Q1 / FR-009 | Both per-module **and** heap-embedded `ModuleTerm` in scope; phased a→b | Clarify (owner override of seed T1) |
| Q2 / FR-002 | **Structural identity** is the equivalence definition; execute-equivalence is a separate FR-003 gate | Clarify (seed T3 opt 2) |
| Q3 / FR-010 | **Lean 4** round-trip proof over simplified model, in scope, sorry-free, no external API | Clarify (seed opt C) |
| A1 / U1 | Codec targets the **raw per-module** `BytecodeProgram` (full labels), not `CombinedProgram` | Seed §recommendation 1 |
| A2 / U2 | Carry the **per-module** `VariableMap`; goal-level maps belong to #2 | Seed |
| A3 / U3 | Obsolete v1 opcodes (`UnionSiAndGoto`, `ResetAndGoto`) **round-tripped exactly** with a discriminant | Seed |
| A4 / T2 | **Payload-type byte in the payload header**; `FrameKind` enum unchanged | Seed T2 opt 3 |
| A5 | Dart byte-parity **deferred to #11**; one runtime (C#) here | Seed risk 7 |
| A6 | Formal tool = **Lean 4** | Seed formal-tooling |

## Decisions resolving the seed's open questions

### D1 — Constant-operand whitelist (resolves seed OQ1 / U4)
**Decision**: explicit closed whitelist — `null | bool | long (int64) | double | string |
Rt.ConstTerm | Rt.StructTerm (recursive)`. Anything else → **loud `IlCodecException`** (FR-006,
SC-004). No `ToString()` fallback (seed U4 opt 2 rejected — not round-trippable). The set is
verified empirically by D7's corpus scan.
**Rationale**: `UnifyConstant.Value`/`BodySetConst.Value` are `object?` (`opcodes.cs:210,77`);
`codegen.cs:735-759` is the only emitter of a recursive `Rt.StructTerm` operand. Hard-error is
the only choice consistent with Principle II (no silent corruption).
**Alternatives**: `ToString()` fallback (silent-corruption risk); open registry (over-engineered
for a spike).

### D2 — Label table: recompute, don't carry (resolves seed OQ5)
**Decision**: do **not** serialize the `Labels` dict; recompute it on decode via the engine's
existing `IndexLabels` (`runner.cs:61-73`) from the decoded instruction list. The round-trip
identity gate asserts the recomputed `Labels` equals the original's.
**Rationale**: `Labels` is a derived field; carrying it is redundant bytes and a second source of
truth. Recompute makes the codec **canonical** (one byte payload per program). The spec Edge Case
"derived label table" is satisfied: lookups behave identically.
**Alternatives**: carry-and-verify (redundant; non-canonical — two encodings of one program).

### D3 — Discriminant scheme (seed recommendation 2)
**Decision**: a 1-byte **family prefix** (`0x01` = v1 `IOp`, `0x02` = v2 `IOpV2`, `0x03` = `Label`
marker), followed by a fixed-width **1-byte per-class discriminant** within the family. Tables are
explicit and closed (see `data-model.md`). Self-describing, extensible, no aliasing.
**Rationale**: makes each opcode map unambiguously to a future MLIR operation (seed formal-tooling
§2 design constraint) without committing the spike to MLIR. Z3 discriminant-uniqueness is a
*stretch* gate (D8), not required for the spike's pass bar.
**Alternatives**: single flat byte across both families (collides at >256 classes; v1 already ~50+,
headroom is fine but the family split is clearer and matches the runtime's two interfaces).

### D4 — Unknown / out-of-family opcode → fail loud (spec Edge Case)
**Decision**: an `Instructions` element that is neither a known `IOp`, known `IOpV2`, nor `Label`
raises `IlCodecException` on encode. No "unknown opcode" passthrough.
**Rationale**: Principle II + SC-004. The seed OQ4 (could a plugin inject a custom `IOp`?) is
answered: not in the current codebase, so fail-loud is safe and correct for the spike.

### D5 — Phase-b heap traversal is bounded (contains the Q1 risk)
**Decision**: phase b locates `ModuleTerm` instances on the engine heap
(`terms.cs:146-156`, stored via `StoreTermOnHeap`, `glp_activation.cs:78-89`), reads the
`ModuleTerm.Bytecode` (`object`, always a `BytecodeProgram`), and round-trips it with the **phase-a
codec**. It serializes *the embedded program*, not a full engine-state snapshot. It does **not**
define #7's snapshot envelope, heap-graph ordering, or cross-reference scheme.
**Rationale**: delivers the capability #7 needs (embedded-program round-trip) while refusing to
design #7. Keeps the accepted Q1 risk contained.

### D6 — Execute-equivalence harness shape (FR-003)
**Decision**: for each corpus program, compile to `BytecodeProgram`; run a fixed goal through the
**original** program and through `Decode(Encode(p))`; assert identical `ExecutionResult` — status
(incl. `Suspended`), bindings, error. Reuses the engine's runner; no runner changes.
**Rationale**: mirrors the verified-Prolog-compiler correctness statement (seed external ref 2).
Suspension is a first-class asserted outcome (FR-003 scenario 5; seed Shapiro criterion 3). The
**empty program is exempt** from execute-equivalence (no defined goal/result) and is verified by
structural identity only (analyze F2).

### D7 — Corpus sourced from `programs/` (A7) + a constant-type scan
**Decision**: the corpus is ≥10 programs drawn from existing `programs/` GLP sources compiled by
the standard pipeline, chosen to cover the FR-007 matrix (v1-only, v2-only, mixed, recursive
constant, label-bearing, empty, suspension-reaching, heap-embedded `ModuleTerm`). A one-off scan
of all `programs/` compiled output enumerates every concrete `object?` constant type actually
emitted, to confirm D1's whitelist is complete (constant-type coverage gate).
**Rationale**: A7 (no invented language constructs); D1's whitelist must be empirically closed.

### D8 — Formal gate scope: Lean simplified model first; Z3/byte-parity are stretch
**Decision**: the **required** formal gate is the Lean 4 proof `decode ∘ encode = id` over the
simplified model (v1 family + ground constants), sorry-free (SC-007). Extending the model to v2 +
recursive constants, the Z3 discriminant-uniqueness check, and Dart byte-parity are **stretch**
goals explicitly out of the spike's pass bar (byte-parity → #11 per A5).
**Rationale**: matches Q3's "simplified model" wording and the seed epoch plan (epoch 5 required;
epoch 6 conditional). Lean 4 chosen over Rocq per seed formal-tooling (clean inductive-list
theorem; Claude-native Lean-LSP-MCP; owner preference; APOLLO sorry-repair, all no-API).

## Risks carried forward

- **R1 (Q1 coupling)** — phase b vs not-yet-started #7 heap-snapshot. Contained by D5; phase a is
  independently shippable. (Tracked in plan Complexity Tracking.)
- **R2 (IR comprehension)** — LLMs reason poorly over IR control flow (seed external ref 6); the
  Lean proof is the ground-truth gate that mitigates a mis-designed discriminant/encoding.
- **R3 (regen clobber)** — mitigated structurally by the clobber-safe project location (D-structure).

## External references (carried from the seed)

TWAM (arxiv 1801.00471); Verified Prolog→WAM compiler (ScienceDirect 0743106692900547); BinProlog
(arxiv 1102.1178); First-Class Verification Dialects for MLIR (PLDI 2025); APOLLO (arxiv 2505.05758).

**Typed-Datalog-IR (seed formal-tooling §4 open item) — PINNED**: Pacharanukún & Szabó et al.,
"A Typed Multi-level Datalog IR and Its Compiler Framework", *Proc. ACM Program. Lang.* 8 (OOPSLA2,
2024), DOI [10.1145/3689767](https://dl.acm.org/doi/10.1145/3689767) (JGU Mainz). Relevance to this
spike: its IR type system is **three-valued** (bidirectional, flow-sensitive, bipolar) — a direct
resonance with GLP's three-valued unification and a reference design for a future *typed* IR codec
beyond this spike's structural-identity bar.

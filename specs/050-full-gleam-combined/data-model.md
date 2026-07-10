# Phase 1 Data Model: 050-full-gleam-combined

Runtime, wire, and test entities for the Gleam instance. Porting references: `glp_runtime/lib/` (Dart, source of truth), `glp_runtime_net/lib/` + `csharp/glp_link/` (C#), `docs/glp-bytecode-v216-complete.md` (normative ISA). Entities delivered by 034/038 are marked EXISTS and are extended, never rewritten.

## Runtime entities (glp_gleam)

### Term (EXISTS — `glp/runtime/terms.gleam`, 034)
The immutable GLP value ADT: constants (int/float/string/atom), structures (functor + args), lists, writer refs, reader refs. Extended only if the port surfaces a missing constructor vs the Dart term model (report first — Bug Protocol).

### Heap / BindingStore (EXISTS — `glp/runtime/heap.gleam`, 034)
Immutable binding store: writer id → binding; deref follows chains. Invariants: writer-MGU (only writers acquire bindings; readers never; never writer↔writer) — the subject of proof obligation PI:14. Value-copy semantics: lookups copy, never alias.

### SourceModule (NEW — `glp/parser/`)
A parsed `.glp` file: module name, type definitions, procedure declarations, clauses, directives. Lifecycle (the load pipeline, FR-001):
`raw text → parsed → SRSW-checked → partially-evaluated → type-checked → compiled → loaded`
Each stage either passes the module on or rejects with a staged diagnostic (stage name + location + reason). No stage may be skipped; a loaded module implies all five stages passed.

### BytecodeProgram (NEW — `glp/bytecode/`)
Compiled form: procedure table (name/arity → clause blocks), instruction stream (v2.16 opcodes), X-register file per activation (positional, FR-006). Relationships: produced by `glp/compiler/`, consumed by `glp/engine/`.

### Goal / Activation (NEW — `glp/engine/`)
A schedulable unit: goal id (unique per instance), procedure ref, argument terms (X-registers), state. State transitions:
`runnable → running → (succeeded | failed | suspended(generation))`
`suspended(g) → runnable` (on reactivation, generation g+1)
Dedup invariant (FR-005): a reactivation enqueue is keyed by (goal_id, suspension_generation); a stale generation's wake is dropped.

### SuspensionRecord (EXISTS — `glp/runtime/suspension.gleam`, 034; extended)
Links an unbound writer id → set of (goal_id, generation) waiting on it. On writer binding: all waiters become reactivation candidates; the table entry is consumed atomically (no double-wake).

### Engine (NEW — `glp/engine/` + `glp/repl/`)
The typed in-process instance value (R7): program store + heap + run queue + suspension table + trace/limit settings + reduction counter. Pure stepping: `step(Engine) -> (Engine, Event)`. No global/process state; the REPL and (later) link pumps own an Engine value.

### ResultEnvelope (EXISTS — `glp/codec/result_envelope*.gleam`, 038)
Uniform result container: outcome (success/suspend/fail), bindings (deep-resolved via builder), captured output. Produced identically for in-process (REPL) and over-the-wire (link) consumption — the ED-1 seam (FR-009).

## Wire entities (link layer)

### WireTerm / TLV encoding (EXISTS — `glp/codec/term_codec.gleam`, 038)
Section-15 TLV: LEB128 varints, 8-byte LE int64, IEEE-754 double, varint+UTF-8 strings, tags 0x02–0x07. Byte-for-byte parity with Dart/C# encodings (SC-004); golden vectors at `specs/038-result-codec-and-framecodec-ride/contracts/golden/`.

### Frame (NEW — `glp/link/reliability/`)
FrameCodec envelope port (reference: `glp_runtime/lib/link/reliability/frame_codec.dart`, `csharp/glp_link/reliability/FrameCodec.cs`): header (type, flags, sequence), CRC32, payload. Untrusted on receipt (FR-015): length/CRC/type validation precedes any decode; violations → Fault, never crash.

### Link (NEW — `glp/link/primitives/`)
A peer connection: link id, role, scheme (loopback|tcp|quicws), endpoint addresses, state:
`connecting → established → (closing → closed | faulted)`
Faults are data (FR-014): a `faulted` link delivers a fault term to the owning program.

### RemoteVarRef / dist-unify state (NEW — `glp/link/`)
Cross-instance variable reference: (instance id, writer id). Distributed unification uses deferred-local-assignment (binding happens on the owning side); `known/1` triggers globalize/localize. Convergence is proof obligation PI:17. Deref chains crossing instances must terminate (no cycles across the seam — FORK-1 discriminator applies).

### Transport (NEW — `glp/link/transports/`)
Seam-typed (port of `i_link_transport`): connect/accept/send/recv/close over loopback | gen_tcp | quic-ws (gleam_quic FFI). One implementation per scheme; the link layer is transport-agnostic above the seam.

## Test entities

### CorpusCase + GoldenOutput (NEW — `test/parity/goldens/`)
One shared-corpus program (from `programs/tests/`) + its recorded, stdout-normalized Dart reference outcome + reference wall-clock. Unit of parity (SC-001) and of the 10× bound (SC-009). GAP-G1/G2/G3/G8 and FORK-1 exist as named cases (FR-011).

### DifferentialRun (NEW — `test/parity/run_differential.sh`)
(program, {dart, csharp, gleam} outputs) → agree | diverge(report). Closes MISS-04 (FR-012).

### PairScenario (EXISTS — `test/link/`, extended)
Role-parameterized program (`programs/tests/link/*.glp`) run split across two runtimes. The C#↔Gleam matrix adds Gleam as a role host: 8 scenarios × 2 directions = 16 runs (FR-016, SC-005), executed per in-scope transport where the scenario is transport-relevant (SC-008).

### QuiescenceOracle (NEW — GAP-G6)
Distributed-run termination detector: reports quiescent (no runnable goals, no in-flight frames) vs deadlocked vs running. Prerequisite for judging distributed acceptance (FR-017).

### ProofObligation (records — `docs/research/glp-gleam-baseline/pipelines/P4-faithfulness/PROOFS/INDEX.md`)
PI:14 writer-MGU-under-value-copy (gates M1); PI:17 dist-deref convergence (gates M2). Discharge form per clarification: Lean project + prose proof + adversarial tests; INDEX status flips OPEN → discharged with artifact links.

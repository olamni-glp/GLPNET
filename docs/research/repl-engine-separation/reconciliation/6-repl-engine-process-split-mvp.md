# Reconciliation Memo — `repl-engine-process-split-mvp` (#6)

**Feature ID:** `repl-engine-process-split-mvp`  
**Dossier kind:** MVP  
**Date:** 2026-06-09  
**Branch:** `026-engine-review-dossier`  
**Methodology:** `reconciliation/SEED-RECONCILIATION-BRIEF.md`

---

## Dossier cross-references

| Anchor | Content |
|---|---|
| §4 | Control-program startup + client model |
| §4.1 | `AfterEngineCreated` startup seam as insertion point (FR-057) |
| §4.2 | `TcpTransport.ListenAsync` one-accept constraint; multi-accept deferred |
| §4.3 | GLP-written control program shape (`serve/2` + `request_listener` + `Link` channels + `mwm`) |
| §4.5 | Advisory recommendation: C# host for MVP; GLP control program as target |
| §8.1 | Slice A — the advisory-recommended MVP definition |
| §8.2 | Slice B — alternative with persistence (larger) |
| §9.1 | Premise reconciliation: compiler lives engine-side; wire carries source text |
| §0.4 | Classification table rows: Transport+framing (reuse); Result-envelope codec (net-new); Multi-accept (refactor, deferred); OS-liveness (net-new, deferred) |
| §2.3 | Result-envelope field set (the net-new engine→client codec) |
| §3 | Wire reuse decision: FrameCodec reused; payload codecs net-new |
| §1.3 | Heap-pointer leak in current `ExecutionResult`; components dropped at boundary |
| §10.1 | Open fork: compiler location |
| §12 risk 1 | Premise-mismatch risk (compiler relocation = large refactor) |

Inverse map: dossier §8.1 and §11 entry #6 `→ Successor seeds: #6` point here. Dependency on entry #5 (`result-codec-and-framecodec-ride`).

---

## Seed-vs-dossier-vs-code

### Roadmap brief (as stored)

```
Notes: MVP. Two processes (REPL client + engine host) over TCP-loopback FrameCodec:
client sends SOURCE TEXT (compiler stays engine-side for MVP), engine returns structured
result envelope; one engine/one client; C# host control program (one-accept listener);
bootstrap from self.glp. Smallest end-to-end split. depends-on: #5. (§7 #6)
```

WSJF=3.25, RICE=4500, state=captured, effort=L.

### Dossier §11 entry #6

Scope: "Two processes over TCP-loopback `FrameCodec`: client sends **source text** (compiler stays engine-side for MVP), engine returns the structured result envelope; one engine/one client; C# host one-accept listener; bootstrap from `self.glp`."  
Depends on: #5 (`result-codec-and-framecodec-ride`). Dossier §refs: §4, §8.1, §9.1.

### Alignment verdict

**Aligned** — the stored roadmap brief faithfully reflects the dossier's Slice A scope. No scope drift. One transport-layer underspecification is noted in Tensions below.

### Code verification (as-built, current HEAD)

| Claim | File:line | Verdict |
|---|---|---|
| `GlpEngine` is already embeddable execution core | `out/csharp/lib/engine/glp_engine.cs:127` (class); `:5-17` (docstring "ONE way to run GLP programs") | CONFIRMED |
| `RunGoalAsync` takes raw goal string, compiles it engine-side | `glp_engine.cs:349` (method), `:487-493` (Lexer/Parser instantiation) | CONFIRMED |
| `LoadSource` takes source text | `glp_engine.cs:251` | CONFIRMED |
| `AfterEngineCreated` static hook in `glp_repl.cs:47` | `out/csharp/bin/glp_repl.cs:47` | CONFIRMED |
| Hook invoked at `glp_repl.cs:126` | `out/csharp/bin/glp_repl.cs:126` | CONFIRMED |
| `Program.cs` is the sole composition root (FR-057) | `out/csharp/glp_repl/Program.cs:30-35` | CONFIRMED |
| `TcpTransport.ListenAsync` one-accept, then `listener.Stop()` | `csharp/glp_link/transports/TcpTransport.cs:40-49` | CONFIRMED (comment `:46-47`: "ONE link per listen ... Phase 6") |
| `FrameCodec` 0x01 version byte, 22-byte header, CRC-32 | `csharp/glp_link/reliability/FrameCodec.cs:42,45,52` | CONFIRMED |
| `_ResolveDeepForTrace` at `glp_engine.cs:607-619` | `glp_engine.cs:607-619` | CONFIRMED |
| `queryVarWriters` built at `glp_engine.cs:515`, handed to scheduler at `:539` | `glp_engine.cs:515,539` | CONFIRMED |
| `DrainResult.SuspendedGoals` + `BlockingReaders` at `scheduler.cs:58-91` | `scheduler.cs:58-91` | CONFIRMED |
| `ExecutionStatus` enum at `scheduler.cs:33-43` | `scheduler.cs:33-43` | CONFIRMED |
| `TraceSink` at `scheduler.cs:138` | `scheduler.cs:138` | CONFIRMED |
| `OutputCallback` at `runtime.cs:135` | `runtime.cs:135` | CONFIRMED |
| Zero `BackgroundService`/`IHostedService` in `out/csharp` + `csharp/glp_link` | grep (0 hits) | CONFIRMED — OS-liveness net-new |
| Zero `Serialize/Encode/ToBytes` on `opcodes*.cs`/`runner.cs` | grep (0 hits) | CONFIRMED — IL codec net-new |
| `BytecodeProgram` at `runner.cs:41` | `out/csharp/lib/bytecode/runner.cs:41` | CONFIRMED |
| `FrameKind` at `FrameCodec.cs:64` (dossier calls it the payload discriminant) | `FrameCodec.cs:64` holds `private const int OffKind = 1` — the `FrameKind` enum is `Whole=0 / Fragment=1` only | **PARTIAL — see Tension T1** |
| `mwm` at `self.glp:387-422` | `programs/self.glp:387-422` | CONFIRMED |
| `request_listener` at `self.glp:513-516` | `programs/self.glp:513-516` | CONFIRMED |
| `link_send` at `self.glp:536`, `link_recv` at `self.glp:548` | `programs/self.glp:536,548` | CONFIRMED |
| `serve/2` const at `glp_engine.cs:135-136` | `glp_engine.cs:135-136` | CONFIRMED |

---

## Classification check

**Kind (dossier): MVP.** Correct — this is the smallest shippable end-to-end split. It does not itself introduce a new foundational primitive (no IL codec, no persistence, no multi-accept); it wires together components built by its prerequisites (#2 result-envelope-and-deep-resolve, #3 structured-output-capture-seam, #5 result-codec-and-framecodec-ride) into a running two-process system.

**Code supports scope?** Yes. The host seam (`AfterEngineCreated`, `TcpTransport`, `Program.cs:30-35`), the engine boundary (`GlpEngine:127`, `RunGoalAsync:349`), and the bootstrap path (`self.glp`, `glp_engine.cs:202-217`) all exist and are wiring points, not missing infrastructure. The one-accept listener `TcpTransport.cs:40-49` exactly matches the MVP's one-engine/one-client scope.

**What code DOES NOT yet exist for this MVP:**
- The C# `BackgroundService`/host-listener that drives the split (net-new, placed in `out/csharp/glp_repl/` or a new `glp_engine_host/` project)
- The client-side process that sends source text over the wire and displays the result (a thin new entry point; the REPL loop is the template)
- The plumbing that wires the result-envelope codec (built by #5) into both the engine-side sender and the client-side display path

None of these are blockers on scope correctness — they ARE the scope of this feature.

---

## Tensions

### T1 — `FrameKind` is a fragmentation enum, not a payload-type discriminant

**Summary:** The dossier says the result-envelope and (future) IL codecs are "distinguished by the header `Kind` byte" (citing `FrameCodec.cs:64`). The actual `FrameKind` enum (`FrameCodec.cs:7-13`) has exactly two values: `Whole=0` and `Fragment=1` — both about fragmentation, not payload type. There is no payload-type field in the current 22-byte header.

**Evidence:** `csharp/glp_link/reliability/FrameCodec.cs:7-13` (`FrameKind` enum), `:64` (`private const int OffKind = 1` — the `Kind` field offset in the header is byte 1, carrying `Whole` or `Fragment`). No `PayloadType`, `MessageType`, or equivalent field exists.

**Why it matters for #6:** The MVP sends source text client→engine and receives a result envelope engine→client. If both directions use `FrameCodec`, the receiver must know which direction a frame came from to interpret its payload. In a point-to-point loopback with separate send/recv streams this is implicit from direction; but the codec itself has no type tag. When #11 (compiled-IL-on-wire) adds a second payload format, the ambiguity becomes concrete. The MVP can work without a type field; successor features may not.

**Owner options:**
1. **Accept direction-implicit disambiguation for MVP** (simpler; the application layer knows which side sent what). Note explicitly that `FrameKind` is NOT a payload discriminant and the type field is deferred.
2. **Extend `FrameKind` with new values** (`Whole=0, Fragment=1, ResultEnvelope=2, ILPayload=3, ...`). This extends the byte-parity contract (FR-060/061) and the Dart mirror must match — coordination cost.
3. **Add a new `PayloadType` byte to the header** (bumps the 22-byte header size; requires version-2 wire format; complex).

*Advisory:* Option 1 for MVP with a documented caveat. Revisit when #4 (IL-codec-spike) defines the payload format.

---

### T2 — MVP host entry-point placement: extend `glp_repl` exe vs new `glp_engine_host` project

**Summary:** The dossier (§4.1) places the control-program listener in the `AfterEngineCreated` hook at the composition root (`Program.cs:30-35`). The current `Program.cs` is the REPL's composition root — a two-process split requires a SEPARATE engine-host entry point (not the REPL). Where does the new host entry point live?

**Evidence:** `out/csharp/glp_repl/Program.cs:30-35` is the REPL host; the engine library is `out/csharp/lib/engine/`. The dossier says "C# host one-accept listener" but does not specify whether this is a new project, a mode flag on the existing exe, or a renamed entry point.

**Why it matters:** The architecture demands the engine-host and the REPL-client run as DIFFERENT processes. If they share one executable (a `--server` flag), test harnesses must launch two copies; if separate projects, deployment is cleaner. This is not a trivial layout decision.

**Owner options:**
1. **New project `glp_engine_host/`** in `out/csharp/` — clean separation; engine host has no REPL console loop; the REPL stays `glp_repl/`. Recommended.
2. **Mode flag on `glp_repl` exe** (`--server-mode`) — single binary; simpler CI; but the REPL and host share one entry point, muddying the architecture boundary.
3. **Docker/process-pair test harness only** — keep a single binary for now, test the split purely in integration tests that launch two processes. Defer binary split to a later feature.

*Advisory:* Option 1 — the architectural boundary should be explicit in the project layout.

---

### T3 — Dependency on #5 (`result-codec-and-framecodec-ride`) must itself depend on #2 and #3

**Summary:** The stored seed says `depends-on: #5`. #5 in turn depends on #2 (result-envelope-and-deep-resolve) and #3 (structured-output-capture-seam). The dep chain is therefore: #6 ← #5 ← #2, #3. This is correct per the dossier topology. However, the roadmap brief records only the direct dep (#5). If the reconciliation of #2 or #3 finds scope changes, they cascade into #6's readiness.

**Evidence:** Dossier §11 topology; #5 `depends_on: 2, 3`; #6 `depends_on: 5`.

**Owner options:**
1. **Accept indirect dep chain as-recorded** — #6 is blocked on #5; #5 tracks #2 and #3 itself.
2. **Record explicit transitive deps** on #6 in the roadmap — more conservative; avoids surprises if #5 is de-scoped.

*Advisory:* Option 1 is standard — transitive deps are the responsibility of the intermediate features. Flag for visibility only.

---

## Under-specifications

### US1 — "structured result envelope" content is not defined in this seed

**Question:** What exactly does the structured result-envelope that the engine returns to the client contain, and what is the wire format?

**Why it matters:** This seed's MVP success criterion is "engine returns the structured result envelope." But the envelope's field set (status + bindings + var-name→writer-id map + suspended detail + output + errors + unbound-var encoding) is defined in the prerequisite feature #5, not in this seed. If #5 under-delivers (e.g., defers unbound-var encoding), the MVP client cannot display suspended results.

**Options:**
1. Specify that #6 accepts whatever #5 delivers; document any deferred fields explicitly in this feature's acceptance criteria.
2. Add a minimum field set to this seed: at minimum `status + bindings (ground only) + error` — which is the current in-process `ExecutionResult` serialized.
3. Defer the question entirely to the `/buildkit-specify` interactive spec step.

---

### US2 — Client-side result display: how does the client render bindings?

**Question:** The REPL's `FormatTerm` / `PrintStatus` (`glp_repl.cs:379-388, 432-584`) dereferences heap pointers via `Heap.Dereference`. Post-split, the client receives a self-contained encoded result. Does the client need a partial GLP runtime (a deserializer), or is the display purely string-based (the server resolves terms to strings before encoding)?

**Why it matters:** If the server sends structured terms (e.g., `PayloadSerializer` tag-encoded), the client needs a decoder. If the server sends pre-rendered strings, the client is trivial but loses inspection capability. This choice shapes the client's dependency on `glp_runtime_net`.

**Options:**
1. Server pre-renders terms to strings (simplest MVP client; no runtime dep on client side).
2. Server sends structured terms; client includes a lightweight decoder (heavier but inspectable).
3. Both: server sends structured + string; client uses whichever is needed.

---

### US3 — Bootstrap: client-side `self.glp` path

**Question:** The dossier says "bootstrap from `self.glp`". In the MVP, `self.glp` is loaded by the engine-host (server side, `glp_engine.cs:202-217`). The client (REPL) today also loads `self.glp` to resolve the type environment. In the split, does the client still need `self.glp`, or does it send source text without any local GLP context?

**Why it matters:** If the client needs `self.glp`, it must locate the file at runtime (the `ResolveRootSelfGlpPath` logic in `glp_repl.cs:62-77`). If it does not, deployment is simpler (the client is a thin network terminal).

**Options:**
1. Client sends raw user-typed text; no `self.glp` on the client side. Server provides all context.
2. Client retains `self.glp` for local syntax checking / tab-completion; server is authoritative for execution.

---

## GEPA/DSPy refinement

### Applicability

**methodological** — this is a C# systems/integration feature (two-process split, host process, TCP framing). There is no LM-program GEPA/DSPy literally optimizes. However, GEPA/DSPy as a *discipline* (iterate-against-metrics until convergence) applies directly: the metric combination below defines "done", the refinement loop is the iterative spec→implement→measure→refine cycle.

### Seed definition

A single GEPA/DSPy program seed is not applicable, but the refinement discipline is: starting from the dossier §8.1 Slice A specification, iterate the C# host + client implementation against the metric combination until all thresholds hold.

The seed in refinement terms:
- **Input:** dossier §8.1 scope + prerequisite (#5) result-envelope codec
- **Candidate:** a running two-process C# system (engine host + REPL client over TCP-loopback `FrameCodec`)
- **Evaluate:** against the metric combination below
- **Mutate:** tighten the host process, the framing/codec plumbing, and the client display until all metrics pass
- **Terminate:** all pragmatic thresholds hold + wire-contract byte-parity confirmed (FR-060/061)

### Metrics combination

| Name | Kind | Tool / Harness | Threshold |
|---|---|---|---|
| REPL suite equivalence | pragmatic | `DART="..." bash test/run_all_tests.sh` | 384/384 — split result ≡ in-process result for every existing test program |
| Cross-process loopback round-trip | pragmatic | New integration test: launch engine host + client, run the 10 canonical REPL programs (`programs/tests/`), compare results | 100% result equivalence (status + bindings) |
| Result-envelope field coverage | pragmatic | Unit test: for `Succeeded`, `Failed`, `Suspended` goals, verify each field (status, bindings, error) is present and correct in the decoded envelope | All 3 statuses covered; bindings round-trip for ground terms |
| Kill-and-restart correct | pragmatic | Integration test: kill engine host mid-run; restart; client reconnects; result is `Failed` (not a hang or crash) | No hang; client receives a failure result within `ConnectTimeout` |
| Wire byte-parity (FrameCodec) | formal | FR-060/061 byte-parity harness: encode/decode round-trip for all frame types | `decode(encode(p)) ≡ p` for all valid payloads; CRC-32 verified on each fragment |
| SRSW preservation under split | formal | Type-checker + SRSW-validity gate (in-repo, usable today) on all GLP programs loaded by the engine host | 0 SRSW violations in the engine's loaded program set |
| GLP source→result correctness | formal | Use the in-repo type-checker + `well_typed_clause` gate on every program the engine host loads; result status must match in-process reference | 0 type errors on canonical test programs; result status identical to in-process run |

**Formal metrics rationale:** The wire contract (FrameCodec framing, CRC-32, byte-parity with future Dart mirror) is a byte-level contract — formal byte-parity + round-trip identity is mandatory (FR-060/061, §2.5 cross-runtime parity caveat). The SRSW and type-checker gates are already in-repo and executable as formal correctness criteria for the GLP language.

### Interactive spec step

At the start of `/buildkit-specify` for this seed, the owner confirms:
1. **Wire byte-parity scope:** is cross-runtime byte-parity (FR-060/061) a hard gate for the MVP, or deferred until the Dart mirror is re-synced? (Determines whether the formal byte-parity metric is a blocker.)
2. **Envelope field set:** does the MVP envelope require the full §2.3 field set (including unbound-var encoding) or is the ground-only subset (`status + bindings + error`) sufficient for the first client?
3. **Host process layout:** new `glp_engine_host/` project vs mode flag on `glp_repl/` (Tension T2)?
4. **Payload-type discriminant:** accept direction-implicit disambiguation for MVP (Tension T1), or extend `FrameKind`?

### Refinement loop

1. **Seed:** dossier §8.1 Slice A + §9.1 source-text MVP + prerequisite #5 codec
2. **Candidate (cycle 1):** implement the C# host listener (`BackgroundService` one-accept over `TcpTransport`); implement the client entry point (source-text sender + result-envelope decoder + display); wire into `AfterEngineCreated` seam
3. **Evaluate:** run REPL suite (pragmatic) + loopback round-trip integration test (pragmatic) + byte-parity harness (formal)
4. **Mutate:** fix any divergence (field encoding, status mapping, display rendering) identified by the metrics
5. **Repeat:** cycles 2-N until all thresholds hold
6. **Terminate:** REPL suite 384/384 + loopback round-trip 100% + byte-parity confirmed

Claude-run, no external API. All metric evaluation runs locally via `bash test/run_all_tests.sh` and the new integration test harness.

---

## Formal tooling

### Lean 4 vs Rocq evaluation

**Lean 4 fit:** The primary formal properties for this MVP are (a) FrameCodec byte-parity / round-trip identity and (b) SRSW preservation. Lean 4's mathlib has excellent support for byte-array reasoning and list invariants. The Lean-LSP-MCP connector makes the tactic loop Claude-native. For the wire-codec property `decode(encode(p)) ≡ p` over a deterministic byte layout, Lean 4 is a natural fit: the proof is a structural induction on frame format, well-supported by mathlib byte/bitvec lemmas. Lean 4 is also the owner's stated preference.

**Rocq fit:** Rocq/Coq has the verified-compiler prior art (TWAM, Vellvm, CompCert) which is more directly relevant when the IL codec arrives (seed #4). For this seed's simpler byte-parity property, Rocq is equally capable but adds setup cost relative to Lean 4. AutoRocq's GPT-4 dependency would need to be adapted (per §3.2a no-API resolution).

**Proof assistant primary:** `lean4`

**Alternative when:** If future work on the wire codec intersects the IL verification layer (seeds #4, #11) and the team has already invested in Rocq for TWAM-style verified-IL proofs, Rocq may be preferable for the byte-codec proofs to stay in one prover ecosystem. Otherwise: none.

### IL verification

The MVP does not carry compiled IL on the wire (source text only; IL codec deferred to seed #4). Therefore:

- No MLIR-dialect / IL-codec verification is required for this seed.
- The only wire-level formal property is FrameCodec byte-parity / CRC-32 round-trip (a byte-array property, not a logic-language or opcode property).
- **IL verification: n/a for this seed.** The MLIR-dialect verification layer (§3.2 brief) activates at seed #4 (IL-codec-spike) and #11 (compiled-IL-on-wire).

---

## Shapiro criteria preserved

This MVP introduces a process boundary between the REPL client and the GLP execution engine. The following Shapiro/GLP design criteria must be preserved across the boundary:

1. **Committed-choice concurrency** — the engine's single-threaded, single-owner heap (`heap_fcp.cs:136-141`) and the atomicity of 3-phase reductions must remain intact; the process split must not introduce mid-reduction interleaving. The host receives source text, runs it to quiescence, and returns the result — no concurrent write to the heap from the client during execution.

2. **SRSW (Single-Reader / Single-Writer)** — the wire carries only ground results (MVP defers unbound-var encoding); SRSW validity is a property of loaded GLP programs on the engine side and must not be weakened by the split. The type-checker/SRSW gate remains the engine-side formal gate.

3. **Suspension correctness** — a `Suspended` result from the engine must be accurately reflected in the result envelope (status + sufficient detail for the client to display the suspension reason). The engine's suspension machinery (`Suspended` map at `runtime.cs:104`, `DrainResult.SuspendedGoals` + `BlockingReaders` at `scheduler.cs:67-73`) continues to run engine-side; the envelope carries the suspension detail faithfully.

4. **Monotone variable binding** — heap bindings are engine-internal; the client receives a self-contained resolved snapshot (ground terms from `_ResolveDeepForTrace`, `glp_engine.cs:607-619`). The client never holds a mutable reference to an engine heap cell — the snapshot is immutable, preserving the monotone-binding invariant at the process boundary.

5. **Three-valued unification** — the three execution statuses (Succeeded / Failed / Suspended) must map faithfully to the result-envelope `status` field. No fourth value, no conflation. `ExecutionStatus` at `scheduler.cs:33-43` is the source of truth.

**Embedded-switch framing:** This MVP is the foundation for the GLP engine acting as a SWITCH for external connectivity (the client-facing wire) and internal OS actions (the engine running GLP programs that drive QHSM/HSM actors). The criteria above ensure that the switch's GLP semantics are preserved when the wire is introduced: committed-choice and SRSW guarantee that the engine's routing decisions are deterministic and race-free; suspension correctness ensures the switch can block on external input without losing state.

---

## Recommendation

**Proceed with #6 as scoped (Slice A MVP).** The dossier's scope is correctly reflected in the roadmap brief; the code supports every cited file:line; the prerequisite chain (#2 → #3 → #5 → #6) is sound.

Before `/buildkit-specify`, resolve the four interactive spec questions (Tension T1: FrameKind discriminant; Tension T2: host project layout; US1: envelope field set; US3: client `self.glp` need). These do not change the MVP size but determine implementation choices.

The wire byte-parity formal metric (FR-060/061) should be treated as a hard gate even for the MVP — it is the cheapest formal property to verify (structural induction on the 22-byte header) and establishes the byte-parity discipline before the more complex IL codec arrives.

---

## Options for owner

| Label | Consequence |
|---|---|
| A — Proceed as Slice A (source-text MVP, compiler engine-side, one client) | Smallest milestone; all heavy net-new (IL codec, persistence, liveness) deferred; delivers the architectural split immediately |
| B — Promote to Slice B (add snapshot persistence + liveness host) | Larger milestone; pulls §5/§6 net-new forward; delivers durability in the first milestone at higher scope cost |
| C — Resolve Tension T1 (FrameKind) before #6 (as part of #5 or a new sub-task) | Cleaner wire contract for successor codecs; small additive scope in #5 |

---

## Open questions

1. Does the cross-runtime byte-parity requirement (FR-060/061) apply to the MVP result-envelope codec, or only to the FrameCodec framing layer? (Scope of the formal byte-parity gate for this seed vs. seed #5.)
2. Is `ModuleTerm`-in-binding (a `BytecodeProgram` embedded in a result binding) explicitly an error in the MVP client, or silently omitted? This must be specified for the client's result-display path.
3. Does the MVP client display suspended results (formatted strings from `DrainResult.SuspendedGoals`) or just the `Suspended` status word? Determines whether the suspension-detail fields in the envelope are tested at this milestone.
4. Should the `TraceSink` output stream (`scheduler.cs:138`) be included in the MVP result envelope, or deferred to a follow-up (streaming output model per §10.2)?

---

## External refs

- `docs/research/repl-engine-separation/design-dossier.md` §4, §8.1, §9.1 — authoritative scope
- `docs/research/repl-engine-separation/reconciliation/SEED-RECONCILIATION-BRIEF.md` — methodology
- `csharp/glp_link/transports/TcpTransport.cs:32-50` — one-accept constraint
- `csharp/glp_link/reliability/FrameCodec.cs:7-13,39-73` — frame format (fragmentation Kind, NOT payload-type)
- `out/csharp/lib/engine/glp_engine.cs:127,349,487-493,515,539,545,607-619` — engine API + resolver
- `out/csharp/bin/glp_repl.cs:47,126` — `AfterEngineCreated` hook
- `out/csharp/glp_repl/Program.cs:30-35` — composition root (FR-057)
- `out/csharp/lib/runtime/scheduler.cs:33-43,58-91,138` — status, DrainResult, TraceSink
- `out/csharp/lib/runtime/runtime.cs:104,129,135` — Suspended map, InboundPump seam, OutputCallback
- `out/csharp/lib/runtime/heap_fcp.cs:148,154,157` — Cells, Hp, _bindCallbacks
- `out/csharp/lib/multiagent/payload_serializer.cs:85-88,511` — tag scheme, unbound-VarRef throw
- `csharp/glp_link/primitives/LinkEgress.cs:68-69` — ground-relay gate
- `programs/self.glp:387-422,456,513-516,523-526,536,548` — mwm, Link, request_listener, accept_link, link_send, link_recv
- [APOLLO — model-agnostic agentic Lean proving](https://arxiv.org/abs/2505.05758) — Lean tactic loop without API
- [First-Class Verification Dialects for MLIR (PLDI'25)](https://users.cs.utah.edu/~regehr/papers/pldi25.pdf) — MLIR verification dialect (n/a this seed; relevant at #4/#11)
- [TWAM: Certifying Abstract Machine for Logic Programs](https://arxiv.org/pdf/1801.00471) — IL verification precedent (n/a this seed)

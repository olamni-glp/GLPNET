# Reconciliation Memo — #5 result-codec-and-framecodec-ride

**Feature id:** `result-codec-and-framecodec-ride`
**Dossier entry:** §11 #5 · Kind: PREP
**Reconciliation date:** 2026-06-09
**Branch:** `026-engine-review-dossier`
**Methodology:** `reconciliation/SEED-RECONCILIATION-BRIEF.md` (authoritative)

---

## Dossier cross-references

| Anchor | Content |
|---|---|
| §2.3 | Net-new result-envelope field set: status / bindings / var→writer / suspended detail / output / errors / unbound-var encoding |
| §3 (B) | Dedicated result-envelope codec riding `FrameCodec`/`TcpTransport`, distinguished by Kind byte |
| §0.4 row 4 | Classification: **net-new** — zero existing; substrate: `FrameCodec` (Kind byte `:64`) + `PayloadSerializer` tag scheme as sub-encoder |
| §1.3 | The §1.3 leaks this feature must close: `Bindings` = live heap `VarRef`s; `queryVarWriters` dropped; `SuspendedGoals`/`BlockingReaders` dropped; output = `Console.WriteLine` side-effects |
| §10.2 | Fork: streaming vs terminal output model |
| §10.3 | Fork: display-only vs round-trip unbound `VarRef`/`MutualRefTerm`/`ModuleTerm` + `SuspendedGoals`/`BlockingReaders` |
| §10.4 | Fork: var-name→writer identity scheme (stable `GlobalVarId` vs local heap int) |
| §12 risk 4 | Suspended/partial results contain unbound vars that the only existing term codec rejects |
| §12 risk 7 | Cross-runtime byte-parity for new codecs — v1/v2 opcode split complicates; applies here for Dart mirror |

**Depends_on (§11 entries):** #2 `result-envelope-and-deep-resolve`; #3 `structured-output-capture-seam`

---

## Seed-vs-dossier-vs-code

### What the dossier says

Dossier §11 #5: "Dedicated result-envelope codec (status+bindings+var→writer+suspended+output+errors, incl. unbound-var encoding) framed over FrameCodec/TcpTransport." §3 adds: two dedicated net-new codecs riding `FrameCodec`, "distinguished by the header **Kind byte** (`FrameCodec.cs:64`)." §0.4 row 4: substrate is "`FrameCodec` (distinguish by frame `Kind`, `FrameCodec.cs:64`)".

### What the roadmap captured

WSJF=3, RICE=1680; Kind=PREP; depends_on #2,#3; §refs §2.3,§3. (`buildkit roadmap brief` command not available in the installed CLI; values read from the dossier §11 table directly.)

### What the code shows — confirmed and extended

**Confirmed (all dossier citations hold):**

- `ExecutionResult` at `out/csharp/lib/engine/glp_engine.cs:51-80`: exactly three fields — `Status`, `Bindings: IReadOnlyDictionary<string, RtTerm?>`, `Error: string?`. No output field, no var-writer map field, no suspended-detail field. Confirmed net-new.
- `_ResolveDeepForTrace` at `glp_engine.cs:607-619`: recursively dereferences through the heap, but is trace-instrumentation only (reached only when `GLP_EQUIV_TRACE` is set per comment `:598-606`). Confirmed usable as a basis; confirmed not currently in the result-return path.
- `queryVarWriters` built at `glp_engine.cs:515`, passed to `scheduler.SetQueryVarNames` at `:539` — never placed in `ExecutionResult`. Confirmed leak.
- `DrainResult` at `out/csharp/lib/runtime/scheduler.cs:58-91`: `SuspendedGoals: IReadOnlyList<string>` (`:67`) and `BlockingReaders: IReadOnlySet<int>` (`:73`) — not propagated into `ExecutionResult`. Confirmed leak.
- `OutputCallback: Action<string>?` at `out/csharp/lib/runtime/runtime.cs:135`; `TraceSink: Action<string>?` at `scheduler.cs:138`. Output currently defaults to `Console.WriteLine` in `OutputKernel` (`body_kernels.cs:940-963`). Confirmed not in result.
- `PayloadSerializer` throws on unbound `VarRef` at `:511` (ground-only), `NotSupportedException` for `MutualRefTerm`/`ModuleTerm` at `:447`. Confirmed — the only existing term codec cannot encode a Suspended result.
- `FrameCodec` at `csharp/glp_link/reliability/FrameCodec.cs:42,45,52,56-62`: 0x01 version, 22-byte header, CRC-32, MTU frag/reassembly, 64 MiB guard. Confirmed — wraps opaque bytes.
- `TcpTransport.ListenAsync` at `transports/TcpTransport.cs:32-50`: one-accept-then-`listener.Stop()`. Confirmed — multi-accept deferred.

**Critical correction to §3 / §0.4 row 4 — "Kind byte" claim:**

The dossier states the codec distinction is made via the header "Kind byte" at `FrameCodec.cs:64`. As-built: `OffKind = 1` (a compile-time constant `private const int OffKind = 1;` at `FrameCodec.cs:64`). This byte holds `FrameKind.Whole (0)` or `FrameKind.Fragment (1)` — it is a **fragmentation-state discriminant, not a payload-type discriminant**. `ParseFrame` at `:132-170` validates only values `{0, 1}` and throws `FrameException` for any other byte. There is NO payload-type (`IL` vs `result envelope` vs `output chunk`) field anywhere in the 22-byte header or in `ILinkEndpoint`. The endpoint is opaque bytes down; any payload-type byte must be **inside the payload** (the first byte of the chunk carried by `FrameCodec`), not in the `FrameCodec` header. This is a dossier precision error — the architecture intent (two payload types coexist on the same transport) is correct, but the mechanism must be a type byte inside the codec payload, not the `FrameKind` header byte.

**Additional finding — `_ResolveDeepForTrace` scope:**

The `_ResolveDeepForTrace` method (`glp_engine.cs:607-619`) is gated behind the `EquivTrace.Out` call and only executes when `GLP_EQUIV_TRACE` is set. Its "depth > 32" guard (`glp_engine.cs:609`) is conservative for trace use but may need enlarging for deep heap structures in a full result resolver. Feature #2 (`result-envelope-and-deep-resolve`) must lift this method to the main result-return path; this feature (#5) consumes #2's resolved output.

**Additional finding — `SetQueryVarNames` inverse map:**

`scheduler.SetQueryVarNames` at `scheduler.cs:180-184` builds `_queryVarNames` (writer-addr → var-name inverse), but this inverse map is not accessible from `GlpEngine` — it lives inside `Scheduler`, which is a local variable in `_RunSingleGoalAsync`. The engine cannot re-read it for the wire envelope. Feature #2 must also expose the `queryVarWriters` (var-name → writer-id) dict as part of the resolved result; this feature (#5) encodes it.

---

## Classification check

**Kind=PREP: correct.** This is an enabling foundation that #6 (the MVP split) depends on directly; nothing in the epic can ship wire results without this codec.

**Scope support in code:** the dossier's "zero existing" classification (`§0.4`) is verified. The only partial substrate is `PayloadSerializer`'s tag scheme (`tags 1–4`, `payload_serializer.cs:85-88`) which can be reused as a recursive constant sub-encoder inside the result envelope, and `FrameCodec`/`TcpTransport` which are the confirmed framing layer. No result-envelope codec class, no `ResultEnvelopeCodec`, no `UnboundVarEncoding` appears anywhere in `out/csharp` or `csharp/glp_link`. Classification is accurate.

**Citation correction:** `FrameCodec.cs:64` is `OffKind = 1` (constant), not a wire field distinguishing payload types. See Tensions §1.

---

## Tensions

### T1 — "Distinguish by the FrameCodec Kind byte" is architecturally incorrect as stated

**Evidence:** `FrameCodec.cs:64` is `private const int OffKind = 1;` — the field at that offset is `FrameKind.Whole (0)` or `FrameKind.Fragment (1)`. `ParseFrame` (`:142-143`) throws `FrameException` on any other value. There is no header slot for payload type. Using the `FrameKind` byte for payload-type discrimination would require extending the `FrameKind` enum to include values like `ResultEnvelope=2` and `ILCodec=3`, which changes a validated binary format shared with the live feature-025 ground-relay — a breaking change.

**Options:**
1. **Payload-type prefix byte inside the codec** — the first byte of the opaque `FrameCodec` chunk encodes the payload type (`0x01 = result envelope`, `0x02 = IL blob`, `0x03 = output chunk`). No change to `FrameCodec`; fully backward-compatible; the disambiguation logic lives in the caller that assembles/disassembles payloads. *Recommended.*
2. **Extend `FrameKind` enum** — add `ResultEnvelope` and `ILCodec` values to `FrameKind`; change `ParseFrame` to accept them. Reuses the existing header slot but is a breaking format change affecting all feature-025 consumers (ground-relay tests, Dart mirror). Not recommended for the MVP.
3. **Separate logical channels (distinct TCP connections/ports)** — one port for GLP ground-relay, a distinct port/connection for the result+IL wire. Clean separation; over-engineering for the MVP; contradicts the "rides `FrameCodec`/`TcpTransport`" scope.

### T2 — Dependency on #3 (output-capture seam) is real but #3 scope is non-trivial

**Evidence:** `OutputKernel` (`body_kernels.cs:940-963`) uses `rt.OutputCallback` when set; if not set it falls back to `Console.WriteLine`. `TraceSink` (`scheduler.cs:138`) routes trace lines. Feature #3 must route ALL output through these callbacks before #5 can include output in the envelope. However, the full output-capture scope includes: (a) `Console.WriteLine` in body_kernels.cs (`:944`); (b) `Console.Error.WriteLine` usages; (c) trace output routed via `TraceSink`. If #3 is incomplete, #5's output field will be silently empty for some output paths. The dependency is functional, not just topological.

**Options:**
1. **Strict: block #5 until #3 covers all output paths** — ensures completeness; correct.
2. **Partial: #5 implements the envelope with an optional output field; incomplete #3 means output is empty** — ships faster; risks silent data loss being accepted as the norm.
3. **Expand #3's scope in its spec to enumerate every callsite** — grep-verified list of all `Console.Write*` in `out/csharp`; required for correctness confidence.

### T3 — Unbound-variable encoding is underspecified; §10.3/§10.4 forks are open

**Evidence:** §2.3 table row "unbound-variable encoding" marks it as the "first-class hard case"; §10.3 Opt 1 (display-only, MVP) vs Opt 2 (full round-trip). The var→writer identity §10.4 has an open fork (stable `GlobalVarId` vs local heap int). Both forks affect the codec binary format — the owner must decide before #5's spec is finalized, because the format is a wire contract that must be byte-stable once published.

**Options:**
1. **Settle both forks before #5/buildkit-specify** — owner decides §10.3 Opt 1 (display-only suspended detail, exclude `ModuleTerm`/`MutualRefTerm`) + §10.4 Opt 1 (stable `GlobalVarId` scheme) at this gate; #5 spec encodes both decisions.
2. **Leave as options in the #5 spec; owner decides at /buildkit-specify interactive step** — defers the decision to the right stage; the interactive spec step in the methodology (§3.4 of the brief) is the correct venue.
3. **MVP: Opt 1 for both; full round-trip deferred to a follow-up** — dossier advisory is already Opt 1 for both; the owner's decision can reaffirm or override.

---

## Under-specifications

### U1 — Codec binary format versioning

**Why it matters:** the result-envelope codec becomes a wire contract between engine and client processes. With no version byte in the payload, any field addition is a breaking change. The dossier does not specify a format version field.

**Options:** (a) Reserve byte 0 of the payload chunk as a format-version byte (alongside the payload-type byte from T1 resolution); (b) version the format implicitly via the `FrameCodec` wire-version byte (but that byte is already used for the framing version, not the payload schema); (c) accept unversioned for the MVP (within-milestone compatibility only) and add versioning in a follow-up.

### U2 — Dart mirror obligation for the result-envelope codec

**Why it matters:** `FrameCodec.cs:31-32` and `Crc32.cs:7-8` carry explicit byte-parity remarks (FR-060/061). If the Dart mirror is kept (feature-definition §2a), the result-envelope codec must meet the same byte-parity standard. The dossier mentions this for the IL codec (§12 risk 7) but does not explicitly state it for the result-envelope codec. Failure to specify byte-parity for the result codec creates a hidden divergence risk between the C# and Dart paths.

**Options:** (a) Explicitly scope the result-envelope codec spec (in #5's plan) to include a Dart mirror byte-parity test analogous to FR-060/061; (b) defer Dart parity for the result codec until the IL codec spike (#4) establishes the format (reasonable since #6 MVP is C#-only); (c) drop the Dart-mirror obligation for the result codec entirely (if the Dart path is being retired).

### U3 — Output field framing (streaming vs terminal) for the MVP codec

**Why it matters:** §10.2 has two options; the codec binary format for the output field differs between them (a single length-prefixed blob vs a separate `FrameKind`-distinguished stream). The codec #5 cannot be finalized until the MVP output model is decided. Even for Opt 1 (terminal envelope), the owner must decide whether the output field is a raw string, a length-prefixed UTF-8 blob, or a list of string records.

**Options:** (a) Terminal envelope (§10.2 Opt 1) — output field = one length-prefixed UTF-8 blob of all captured output lines joined by `\n`; simplest; owner confirms at spec-step; (b) output field = a count-prefixed sequence of length-prefixed records; more structured but same single-frame commitment; (c) streaming (§10.2 Opt 2) — requires a separate `output chunk` frame type (also needs the T1 resolution), which may be deferred.

### U4 — Error-in-bindings vs top-level error: are compile errors distinguishable from runtime errors?

**Why it matters:** `ExecutionResult.Error` (`glp_engine.cs:60`) is a plain `string?`. A compile error (predicate not found at `:511-513`) and a runtime abort use the same field. A remote client may need to distinguish them for display or retry logic. The codec spec must decide whether to include an error-kind tag.

**Options:** (a) Add an error-kind discriminant (compile/runtime/guard-fail/abort) in the envelope; (b) keep a single string field; client infers kind from status + error prefix; (c) defer error-kind enrichment to a follow-up.

---

## GEPA/DSPy refinement

### Applicability

**methodological** — this seed produces C# implementation code (the codec class), not an LM/codegen program. GEPA/DSPy applies as the iterate-against-metrics discipline: define the codec spec as a DSPy signature, generate candidate implementations, evaluate them against the metric combination, mutate and re-generate until all thresholds hold.

### Seed definition

The result-envelope codec is a **boundary codec** — it transforms the engine-internal `ExecutionResult` + `DrainResult` + `queryVarWriters` + captured output into a self-contained byte sequence (`byte[]`) that can cross a process boundary, be reassembled on the client, and reconstruct a semantically equivalent view of the execution result without any heap access. The seed must deliver:

1. An encoder: `ExecutionResult` + supplementary fields (var-writers, drain-result, output list) → `byte[]` chunk.
2. A decoder: `byte[]` chunk → a client-side `RemoteExecutionResult` or equivalent DTO.
3. A payload-type prefix byte (T1 resolution).
4. A format-version byte.
5. Unbound-`VarRef` encoding (at minimum display-only: a sentinel tag + the heap-address as a display hint).
6. `PayloadSerializer` tag scheme reused as the ground-term sub-encoder.
7. A byte-parity test (encode→decode round-trip identity for ground results; encode→display correctness for suspended results).

### Metrics combination

| Name | Kind | Tool | Threshold |
|---|---|---|---|
| Cross-process loopback equivalence | pragmatic | REPL test suite (`test/run_all_tests.sh`) — add a split-process test: engine in process A, client in process B, assert `RemoteExecutionResult` ≡ in-process `ExecutionResult` for a corpus of ground-result goals | All corpus goals produce identical bindings, status, and error across the split |
| Round-trip identity | pragmatic | Unit test: `decode(encode(result)) ≡ result` for all enum status values + ground bindings + null bindings (unbound vars → sentinel round-trips to display string, not heap addr) | 100% of parametrized cases |
| Output capture completeness | pragmatic | Unit test: set `OutputCallback` to capture lines; run a goal with `_output/1`; assert output field in envelope equals captured lines | 100% match on test corpus |
| Byte-parity (wire contract) | formal | Byte-parity test harness (analogous to FR-060/061): C# encoder → write bytes → C# decoder; assert `decode(encode(x)) = x` for all field variants; record exact byte layout | Exact byte-level round-trip; no lossy fields |
| Unbound-var sentinel correctness | formal | Prove (or test exhaustively): `Suspended` status + at least one `null` binding → encoder emits an unbound-sentinel tag; decoder reconstructs the null/display string without accessing the heap | 100% of `Suspended` test cases |
| SRSW preservation | formal | Type-checker + SRSW validator run on the GLP test corpus before and after introducing the codec; assert no new SRSW violations are introduced by the output-capture seam (#3) that this feature depends on | 0 new SRSW violations in the REPL test suite (384/384) |

### Interactive spec step

At the start of `/buildkit-specify` for #5, the owner confirms:
1. **§10.3 decision** (display-only suspended detail vs round-trip) — gates the unbound-var encoding format.
2. **§10.4 decision** (stable `GlobalVarId` vs heap int for var→writer identity) — gates the var-writer map field encoding.
3. **§10.2 decision** (terminal envelope vs streaming output) for the MVP — gates the output-field binary shape.
4. **T1 resolution** (payload-type prefix byte inside the chunk) — confirm approach before the format is specified.
5. **U2 obligation** (Dart mirror byte-parity: yes/no/defer) — scopes the parity test.
6. The formal proof-assistant choice for the byte-parity formal metric (§3.2a; see Formal tooling below).

### Refinement loop

Loop: seed (this memo + §2.3 field set) → candidate C# `ResultEnvelopeCodec` class → evaluate against metric combination (cross-process loopback, round-trip identity, output capture, byte-parity, SRSW) → GEPA reflective mutation identifying which field encoding or type tag is wrong → regenerate → repeat. Terminate when: all pragmatic thresholds hold AND the byte-parity formal metric passes AND SRSW gate is green. Claude-run via Agent-tool seams; no external API.

---

## Formal tooling

### Lean 4 vs Rocq evaluation

**Lean 4 fit:** the byte-parity metric (encode → decode round-trip identity) is a clean equational property — `decode(encode(result)) = result` — that Lean 4's inductive types and `simp`/`rfl` tactics handle well. Lean 4's `mathlib` has no direct serialization library, but the proof structure is straightforward structural induction on the result type (finite enum status + finite field variants). Lean-LSP-MCP and Lean Copilot (Claude-native, model-agnostic) are the tactic-generation loop. For this seed (no complex semantics, just byte-format correctness) Lean 4 is a good fit.

**Rocq fit:** Rocq has verified serialization frameworks (compcert has binary encoders; Vellvm has IL codec proofs). For a pure wire-format correctness proof, Rocq's `omega`/`lia` + extraction to OCaml is also viable, and the AutoRocq iterative-loop (adapted off its GPT-4 dependency to use Claude) would work. However, Rocq's syntax overhead is higher for a simple codec property, and the Lean 4 path is shorter here.

**Primary:** `lean4` — the round-trip identity property is a structurally simple inductive proof; Lean 4's tactic automation is sufficient; Claude-native MCP tooling applies cleanly.

**Alternative when:** Rocq, if the byte-parity proof must be co-located with an existing Rocq-based IL codec correctness proof from seed #4 (`il-codec-spike`) — i.e. if the owner decides to unify all codec proofs in one proof assistant.

### IL verification

`il_verification: n/a` — this seed produces the **result-envelope codec**, not the IL/bytecode codec. The IL codec is seed #4 (`il-codec-spike`). This seed touches the wire only in the sense that the result envelope rides `FrameCodec`/`TcpTransport`; the byte-parity metric above covers the format correctness obligation. No MLIR dialect or bytecode round-trip verification is required here. (If a `ModuleTerm`-in-binding round-trip is added per §10.3 Opt 2, that re-couples to the IL codec and the IL-verification layer from seed #4 applies — but that is out of MVP scope.)

---

## Shapiro criteria preserved

This step must preserve the following original GLP/Shapiro design criteria, framed for the embedded-switch purpose:

1. **SRSW (Single-Reader/Single-Writer)** — the output-capture seam (feature #3, which #5 depends on) must not introduce shared writeable state between the scheduler and the result-codec path. The `OutputCallback`/`TraceSink` are C# delegates capturing output lines into a list; the scheduler is single-threaded; no concurrent writer is introduced. The SRSW gate on the *GLP program* side is unaffected. Verify: REPL suite (384/384) must pass with the seam active.

2. **Suspension correctness** — the envelope encodes `Suspended` status and the `BlockingReaders` set. The codec must not resolve or discard `BlockingReaders` heap addresses; it must transmit them faithfully (at minimum as display strings) so the client can report the suspension condition accurately. Discarding them would destroy the client's ability to diagnose why a goal suspended — breaking the correctness of the observed semantics from the client's perspective.

3. **Monotone variable binding** — the codec must never encode a "tentative" (HEAD-phase) binding as a final binding. It reads `ExecutionResult.Bindings` (post-drain, post-deep-resolve), which contains only post-commit bindings. The encoder must be invoked only after `DrainAsyncWithStatus` returns — the existing call site at `glp_engine.cs:545` is the correct and only encode-trigger point.

4. **Committed-choice concurrency** — the codec is stateless and pure (encode/decode only); it does not introduce any concurrent choice or backtracking path. This criterion is trivially preserved.

5. **Three-valued unification (Success/Suspend/Fail)** — the status field encodes all three values (`ExecutionStatus.Succeeded`, `Suspended`, `Failed` from `scheduler.cs:33-43`). The encoder must not conflate Fail and Suspend (they carry different field sets: a Failed result has an Error string; a Suspended result has `BlockingReaders` and `SuspendedGoals`). The binary format must have distinct status codes for all three.

**Embedded-switch framing:** in the target architecture (GLP engine as a connectivity/OS switch hosting QHSM/HSM actors), the result envelope is the mechanism by which external clients (OS tasks, actor frameworks, connectivity consumers) observe the outcome of GLP reduction steps. Correct suspension encoding is essential for the switch's ability to route "waiting on external input" events to the right external source. Monotone-binding fidelity ensures external observers see a consistent binding state. SRSW preservation ensures the output-capture path does not create hidden aliasing.

---

## Recommendation

This seed is **correctly scoped and correctly classified** as PREP. It is a real hard dependency on #6 (the MVP). Proceed with the following clarifications before `/buildkit-specify`:

1. Correct the dossier §3 / §0.4 "Kind byte" language: the payload-type discriminant must be **inside the codec payload** (first byte of the chunk), not in the `FrameKind` header field.
2. Owner settles §10.3 (display-only suspended detail for MVP) and §10.4 (stable `GlobalVarId`) before #5 is specified — these decisions are format-defining.
3. Feature #3's scope must enumerate all `Console.Write*` call sites explicitly before #5 is gated on it.

The GEPA/DSPy refinement loop (methodological applicability) is tractable: the codec is a closed, testable C# class with a well-defined binary contract; iterate until round-trip identity + cross-process loopback equivalence + byte-parity all pass.

---

## Options for owner

| Label | Consequence |
|---|---|
| A — Proceed with memo-recommended corrections (payload-type prefix byte, settle §10.3/§10.4 now) | Cleanest path; #5 spec is precise and format-stable; recommended |
| B — Defer §10.3/§10.4 fork decisions to the /buildkit-specify interactive step | Legitimate; the methodology brief (§3.4) designates that step for exactly this purpose; delays #5 spec start by one decision round-trip |
| C — Extend FrameKind enum for payload-type discrimination (T1 option 2) | Changes a live binary format shared with feature-025 ground-relay; requires Dart mirror update; not recommended for the MVP |

---

## Open questions

1. Is the Dart mirror obligation for the result-envelope codec **active** for the MVP, or deferred until after #6 ships in C#? (This determines whether U2 scopes into #5 or is a follow-up.)
2. Should `_ResolveDeepForTrace` at `glp_engine.cs:607-619` be promoted to a named, public/internal method in feature #2, or should #5 define its own deep-resolver on top of #2's output? (Avoid duplication; single-source the resolver.)
3. Will the result-envelope codec live in `out/csharp/lib/engine/` (alongside `GlpEngine`) or in `csharp/glp_link/` (alongside `FrameCodec`)? The dependency direction matters for FR-057 (the engine library must not reference `GlpLink`). A codec in `glp_link` cannot reference engine-internal types; a codec in the engine assembly cannot reference `FrameCodec` without adding a `GlpLink` reference to the engine. A thin shared-contracts assembly (or placing the codec in the host/composition-root layer) may be needed.
4. What is the maximum depth guarantee for the deep-resolver used by the codec? The `_ResolveDeepForTrace` guard of `depth > 32` is conservative — is 32 sufficient for real GLP programs, or should it be configurable or unbounded (with cycle detection)?

---

## External refs

- `FrameCodec.cs:39-170` — the byte-stable framing layer; wire version 0x01; `FrameKind.Whole (0)` / `Fragment (1)`.
- `out/csharp/lib/engine/glp_engine.cs:51-80` (`ExecutionResult`), `:515,539,573,580` (`queryVarWriters` path), `:545,583-586` (drain + binding collection), `:607-619` (`_ResolveDeepForTrace`).
- `out/csharp/lib/runtime/scheduler.cs:58-91` (`DrainResult` with `SuspendedGoals`/`BlockingReaders`), `:138` (`TraceSink`), `:180-184` (`SetQueryVarNames`).
- `out/csharp/lib/runtime/runtime.cs:135` (`OutputCallback`).
- `out/csharp/lib/runtime/body_kernels.cs:940-963` (`OutputKernel`).
- `out/csharp/lib/multiagent/payload_serializer.cs:85-88` (tag scheme), `:447` (`NotSupportedException`), `:511` (throws on unbound `VarRef`).
- `csharp/glp_link/primitives/LinkEgress.cs:26-44` (ground-only relay; `PayloadSerializer.SerializeAgentMessage`).
- TWAM verified IL: https://arxiv.org/pdf/1801.00471
- Lean 4: https://lean-lang.org/papers/lean4.pdf
- APOLLO (model-agnostic Lean proving): https://arxiv.org/abs/2505.05758
- First-Class Verification Dialects for MLIR (PLDI'25): https://users.cs.utah.edu/~regehr/papers/pldi25.pdf

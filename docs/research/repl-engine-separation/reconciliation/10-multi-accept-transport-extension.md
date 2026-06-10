# Reconciliation Memo — multi-accept-transport-extension (dossier §11 #10)

**Feature id:** `multi-accept-transport-extension`
**Date:** 2026-06-09
**Author:** reconciliation sub-agent (read-only w.r.t. all source code)
**Status:** DRAFT — awaiting owner decision

---

## Dossier cross-references

| Anchor | What it says |
|---|---|
| §4.2 | KEY GAP: `TcpTransport.ListenAsync` (`:32-50`) accepts exactly ONE connection then `listener.Stop()` (`:46-48`; comment `:46-47`: "ONE link per listen … multi-accept … Phase 6"). Multi-accept = yield many endpoints. |
| §4.3 | Control loop CAN be GLP — the `serve/2`-loop shape, `request_listener`, `Link(In,Out)`, `mwm` fan-in; depends on (a) multi-accept and (b) wire carrying compiled IL. |
| §4.4 | Multiple clients are already heap-safe: N clients → N links → N recv-loops → ONE inbox → ONE heap is ratified by `heap_fcp.cs:136-141` + `LinkPump`/`TryApplyNext`. Multi-client at the transport level only awaits multi-accept. |
| §4.5 | Advisory recommendation: OS-level (TCP loopback) for MVP; in-GLP mailbox as post-MVP target. |
| §0.4 | Classification row: "Multi-accept listener — refactor (Phase-6 deferred)"; substrate: `TcpTransport.cs:46-48`. |
| §12 risk 6 | "Multi-accept is a hard dep for N-clients AND a GLP control program, currently deferred" — mitigated by sequencing as #10 after the MVP (#6). |
| Appendix B | #10 maps to §4.2, §0.4. |

---

## Seed-vs-dossier-vs-code

### Stored roadmap profile (from `buildkit-roadmap brief`)

- kind: not set explicitly (notes say "FOLLOW-UP/PREP")
- scope: "Phase-6 multi-accept loop in TcpTransport.ListenAsync (yield many endpoints). Unblocks N-clients + a GLP control program."
- depends_on: #6
- §ref: §7 #10 (stored notes say §7 but dossier uses §4 — see below)

### Dossier §11 entry #10

- Kind: PREP/FOLLOW-UP
- Scope: "Multi-accept loop in `TcpTransport.ListenAsync` (yield many endpoints instead of one-accept-then-Stop)"
- Why: "Unblocks N-clients + a GLP control program"
- depends_on: 6
- §ref: §4.2

### As-built code verification

**`csharp/glp_link/transports/TcpTransport.cs:32-50`** — verified. `ListenAsync` accepts exactly one connection via `await listener.AcceptTcpClientAsync(ct)`, then the `finally` block calls `listener.Stop()` (line `:48`). Comment at `:46-47` says "One link per listen for the base MVP (a multi-accept loop is a transport-leaf concern, Phase 6)". The dossier's claim at §4.2 is accurate and current.

**`csharp/glp_link/primitives/LinkListenKernel.cs:32-81`** — verified. The kernel calls `transport.ListenAsync(...)` at `:63` (blocking, `.GetAwaiter().GetResult()`). It reads ONE request token and parks ONE endpoint in `link.Pending[id]` (`:73`). The kernel's `remarks` doc also says "Base-MVP: ONE request per rendezvous". The kernel itself is a one-shot body kernel, not a loop.

**`csharp/glp_link/seam/ILinkTransport.cs:41`** — `ListenAsync` returns `Task<ILinkEndpoint>` (singular). No `IAsyncEnumerable<ILinkEndpoint>` variant or callback overload exists anywhere in the seam. The interface contract is single-endpoint.

**`csharp/glp_link/primitives/LinkRuntime.cs:50`** — `Pending` is `Dictionary<LinkId, ILinkEndpoint>` (not a queue). A second arrival with the same `LinkId` would overwrite the first. This is structurally consistent with one-at-a-time; multi-accept requires either a multi-key pending structure or immediate adoption.

**`csharp/glp_link/primitives/LinkPump.cs:38,60`** — `_recvLoops` is `List<Task>`; `AddLink` starts one background recv loop per call (`:60`). The pump already supports N concurrent recv loops feeding one `BlockingCollection<InboundItem>` inbox. N-client support at the PUMP level requires only N calls to `AddLink` — which is already structurally capable.

**`programs/self.glp:513-516`** — `request_listener` binds to `_link_listen` which is the one-shot kernel. The GLP-level wrapper is also one-shot per call.

**DOSSIER DIVERGENCES:**
- The stored roadmap notes say `§7 #10` as the §-ref. The dossier uses `§4.2` (and the Appendix B says `§4.2, §0.4`). `§7` in the dossier is the Mailbox decision section — a different topic. The `§7` in the notes is stale and refers to the §11 entry number (row 7 of the table in an earlier draft), not a section anchor. This is a notes-formatting artefact, not a substantive error.
- Kind in stored notes: "FOLLOW-UP/PREP". Dossier §11: "PREP/FOLLOW-UP". The order difference is inconsequential; both agree this is simultaneously a refactor preparation (it unblocks #13) and a follow-up to the MVP (#6).

**ADDITIONAL CODE FINDING — LoopbackTransport has no one-accept limit:**
`csharp/glp_link/transports/LoopbackTransport.cs:39-91` — `LoopbackTransport.ListenAsync` delegates to `RendezvousAsync` which is already a multi-rendezvous mechanism (keyed by channel name in a dictionary, supports arbitrary concurrent pairs). The one-accept-then-stop pattern is **specific to `TcpTransport`**. This matters: a multi-accept extension is a `TcpTransport`-specific change, not an `ILinkTransport` interface change — the interface already returns a single `Task<ILinkEndpoint>` and that is sufficient for loopback. For TCP multi-accept, the extension is transport-internal (a loop calling `AcceptTcpClientAsync` repeatedly) rather than a new interface method.

**ADDITIONAL CODE FINDING — `LinkListenKernel` runs synchronously on the runner thread:**
`LinkListenKernel.cs:63` uses `.GetAwaiter().GetResult()` (blocking the GLP runner thread). A multi-accept loop in `ListenAsync` cannot be a simple loop here — it would permanently block the runner. The multi-accept design MUST keep `ListenAsync` returning a single endpoint per call, and instead the GLP-level control program calls `request_listener` in a recursive loop (the `serve/2` shape). This architectural constraint is implied by §4.3 but not made explicit in §4.2 — it is load-bearing for how the extension is implemented.

---

## Classification check

**Dossier kind PREP/FOLLOW-UP — does reality support it?**

Yes, with nuance. The PREP aspect: multi-accept unblocks #13 (multi-client-control-program-in-glp) and is a prerequisite for any N>1 client architecture. The FOLLOW-UP aspect: it is not needed for the #6 MVP (one engine/one REPL client). The classification matches. The scope ("multi-accept loop in `TcpTransport.ListenAsync`") partially matches reality: the extension should be INSIDE `TcpTransport`, specifically making `ListenAsync` re-bindable (keep the port open across calls) rather than a loop inside a single call. The precise mechanism — TCP `TcpListener` re-use across successive `AcceptTcpClientAsync` calls — is transport-internal.

**file:line confirmation:**
- `csharp/glp_link/transports/TcpTransport.cs:32-50` — ListenAsync one-accept confirmed
- `csharp/glp_link/transports/TcpTransport.cs:46-48` — Phase-6 comment confirmed
- `csharp/glp_link/primitives/LinkListenKernel.cs:63` — blocking `.GetAwaiter().GetResult()` on runner thread confirmed
- `csharp/glp_link/primitives/LinkRuntime.cs:50` — `Pending` dict (single endpoint per LinkId) confirmed

---

## Tensions

### T1: Transport-internal loop vs ILinkTransport interface change

**Summary:** The scope says "yield many endpoints" but the current `ILinkTransport.ListenAsync` returns `Task<ILinkEndpoint>` (singular). Implementing multi-accept requires a decision on whether to change the interface or keep it single-call and handle repetition above.

**Evidence:** `csharp/glp_link/seam/ILinkTransport.cs:41` returns `Task<ILinkEndpoint>`. `LoopbackTransport` already handles multi-pair correctly without an interface change. `LinkListenKernel.cs:63` blocks the runner thread — a streaming interface here would be architecturally wrong.

**Options:**
1. Keep `ILinkTransport.ListenAsync` returning a single `Task<ILinkEndpoint>` per call; extend `TcpTransport` to keep the `TcpListener` alive across successive calls (stateful transport instance, bound once, accept-on-demand). The GLP-level loop (`request_listener` in a recursive clause / `serve/2` shape) calls `_link_listen` repeatedly, each call gets one endpoint. This preserves the current interface and the blocking-runner-thread model.
2. Add `IAsyncEnumerable<ILinkEndpoint> ListenManyAsync(...)` alongside the existing `ListenAsync`. The `_link_listen` kernel would need a multi-stream variant. This introduces a parallel kernel path and interface complexity.
3. Add a callback/delegate variant to `ILinkTransport` for the listener role. Similar complexity to option 2.

*Advisory: Option 1 — keeps the interface stable, aligns with the blocking-kernel architectural constraint, and matches how `LoopbackTransport` works.*

### T2: Where the accept loop lives — C# host vs GLP program

**Summary:** §4.2 and §4.3 together imply the multi-accept loop should eventually be a GLP `serve/2`-style recursive program (`request_listener` → `accept_link` → recurse). But §4.5 also mentions a C# `BackgroundService` one-accept listener as the MVP path, which is a C# host loop rather than a GLP loop.

**Evidence:** `glp_engine.cs:135-136` (`serve/2` shape); `self.glp:513-516` (`request_listener`); `LinkListenKernel.cs:22-26` remarks confirm one-shot base; §4.3 says the GLP loop is feasible; §4.5 says C# host for MVP.

**Options:**
1. C# host loop in `BackgroundService` calls `transport.ListenAsync` in a loop, yielding each accepted endpoint to the link layer. The GLP program does not need to drive the listen loop. Simpler MVP path; the GLP program only calls `accept_link` once per request token it dequeues.
2. GLP recursive `request_listener` loop (a clause recursing over the `Requests` stream). Requires the kernel to be re-callable — which it already is (each call is independent). This is the target architecture of §4.3 and requires multi-accept in `TcpTransport` to support it.
3. Hybrid: C# host drives a listen loop for the MVP (#6 + #10 scope), and the GLP-written control program (#13) takes it over when compiled-IL-on-wire lands (#11).

*Advisory: Option 3 — the C# host loop is the right #10 deliverable; GLP control program is the #13 deliverable.*

### T3: LinkId nonce collision with multiple accepted connections on the same port

**Summary:** `TcpTransport.TcpEndpoint` constructs its `LinkId` as `link_id("tcp", local_addr, LinkNonce.Int(port))` (`TcpTransport.cs:42`). If the listener stays alive and accepts multiple connections on the same port, every accepted connection gets the SAME `LinkId` (same port nonce). The `LinkRegistry` (`LinkRuntime.cs:28`) is idempotent-at-identity — a second accept on the same `LinkId` would return the EXISTING handle, not a new one.

**Evidence:** `csharp/glp_link/transports/TcpTransport.cs:42` — nonce = port int. `csharp/glp_link/primitives/LinkRegistry.cs:25-34` (cited in dossier §0.4) — `GetOrEstablish` is idempotent at LinkId.

**Options:**
1. Use a per-accept incrementing nonce (e.g. `LinkNonce.Int(Interlocked.Increment(ref _nextNonce))`) so each accepted connection gets a unique `LinkId`. This is the correct fix and consistent with the "never-reused" nonce invariant (`self.glp:439`, `LinkId ::= link_id(Scheme, Endpoint, Nonce)`).
2. Use the peer's address (remote IP + port) as the nonce, which is unique per connection. Risk: same peer reconnects with ephemeral port reuse — may collide.
3. Leave nonce as port int and prohibit more than one simultaneous live link per port (back to one-accept semantics effectively).

*This tension is a genuine scope addition the dossier does not explicitly address — it must be resolved in the implementation of #10.*

---

## Under-specifications

### U1: Listener lifetime and port rebind semantics

**Question:** After `TcpTransport.ListenAsync` accepts one connection and the `finally` block calls `listener.Stop()`, the port is released. For multi-accept, when does the `TcpListener` get re-bound? Does it stay bound across accepts, or is it re-bound per-accept?

**Why it matters:** If the listener stops between accepts, there is a window where connecting clients get "connection refused". If it stays bound, the implementation must change significantly (the `TcpListener` becomes transport state, not a local variable in `ListenAsync`). The choice affects the `ILinkTransport` interface design (see T1).

**Options:**
1. `TcpListener` as transport state: bound once on first `ListenAsync` call, stays bound. Each call just `await AcceptTcpClientAsync`. Cleanest multi-accept semantics.
2. Re-bind per `ListenAsync` call: each call creates a new `TcpListener`, binds, accepts one, stops. Preserves the current structure but introduces a connection-refused window between calls.
3. Explicit `BindAsync` + separate `AcceptAsync` split — a refactor of the seam.

### U2: Backpressure and pending-connection queue depth

**Question:** If the GLP control program is slow to call `_link_listen` again (e.g. processing a previous request), the OS TCP accept queue fills up and new connectors get ECONNREFUSED. What is the maximum pending-connection count (backlog) and how is it surfaced to the GLP program?

**Why it matters:** The OS `TcpListener` backlog default (typically 10-50) is the only buffer. If the GLP program's accept rate is slower than the connect rate, connections are silently dropped at the OS level. This is a correctness issue for a many-client scenario.

**Options:**
1. Accept it as a deployment concern; document the OS backlog limit. Simplest.
2. Surface OS backlog exhaustion as a GLP fault term on the `Faults` stream.
3. Add an explicit backlog-depth parameter to the listen kernel.

### U3: Interaction with path-A `link_setup(listener)` vs path-B `request_listener` for multi-accept

**Question:** Path A (`server_listener/3` → `link_setup(listener)` → `_link_setup/5` → `LinkSetupKernel`) and path B (`request_listener/2` → `_link_listen/3` → `LinkListenKernel`) both involve TCP listening. The multi-accept extension is documented only for path B in §4.2/§4.3. Does multi-accept also apply to path A?

**Why it matters:** If only path B gets multi-accept, path A is still limited to one simultaneous listener per `link_id`. A GLP program using `server_listener` (path A) for the engine-side listener would not benefit from #10. The dossier's §4 recommendation focuses on path B + the GLP control program.

**Options:**
1. Multi-accept applies only to path B (`_link_listen`/`ListenAsync` in `TcpTransport`); path A continues as one-link-per-setup (acceptable because path A links are reused by identity, not accepted repeatedly).
2. Multi-accept applies to both paths — but path A's semantics (idempotent by `LinkId`) makes "multiple accepts of the same LinkId" semantically incoherent.
3. Document the limitation explicitly in path A's kernel.

---

## GEPA/DSPy refinement plan

### Applicability

**methodological** — this seed is a C# transport refactor, not an LM-generated program. GEPA/DSPy applies as an iterate-against-a-metric discipline: seed → candidate transport design → evaluate against correctness/parity/thread-safety metrics → refine → repeat. There is no DSPy module to optimize directly; the refinement discipline is applied to the design + implementation against the formal/pragmatic metric combination.

### Seed definition

> Extend `TcpTransport.ListenAsync` so it supports repeated accept calls on a persistently-bound `TcpListener`, with each call returning one `ILinkEndpoint` for one accepted client connection, identified by a unique per-connection nonce. The `ILinkTransport` interface remains single-endpoint-per-call. The GLP-level multi-accept loop is a recursive `request_listener`/`_link_listen` caller. Heap safety and the single-inbox pump model are preserved unchanged.

### Metrics combination

| Name | Kind | Tool / Harness | Threshold |
|---|---|---|---|
| REPL multi-client round-trip test | pragmatic | `test/run_all_tests.sh` + a new multi-accept integration test (two GLP clients connect to one engine sequentially, each gets correct bindings) | all existing tests green; new test passes |
| Cross-process loopback equivalence — N clients | pragmatic | `programs/tests/link/pc.glp`-style split test extended to 2+ clients | each client's result matches its expected bindings independently |
| `LinkId` uniqueness invariant | pragmatic | unit test: N accepts on the same port yield N distinct `LinkId` values | N distinct nonces confirmed |
| Pump thread-safety: N recv-loops → one inbox | pragmatic | stress test: 3 concurrent clients, each sending 100 messages; no lost/reordered messages per client | zero message loss; per-client FIFO preserved |
| SRSW preservation | formal | in-repo type-checker + SRSW gate (`test/run_all_tests.sh` section D) | 0 SRSW violations in the multi-accept GLP wrapper code |
| ILinkTransport seam byte-parity | formal | `FrameCodec.cs:31-32` byte-parity standard; no wire format change needed (transport-internal change only) | confirmed no new wire-format bytes; existing FR-060/061 byte-parity passes |
| Transport-layer isolation (FR-057) | formal | code review gate: no new reference from `out/csharp` to `csharp/glp_link` | static dependency check passes |

Note: no IL codec or wire-format change is involved in #10 — the wire byte contract (FrameCodec/PayloadSerializer) is unchanged. Therefore the MLIR-dialect IL-verification layer and bytecode round-trip metrics are NOT in scope for this seed.

### Interactive spec step

At the start of `/buildkit-specify multi-accept-transport-extension`, confirm with the owner:
1. Whether `ILinkTransport.ListenAsync` stays single-endpoint-per-call (recommended) or a new streaming variant is introduced.
2. `TcpListener` lifetime: stateful transport (T1 option 1) or re-bind per call (T1 option 2).
3. Nonce scheme for multi-accept uniqueness (T3 options 1 or 2).
4. Whether the multi-accept integration test target is two sequential clients or N concurrent clients.
5. Whether U2 (backlog/backpressure) is in scope for #10 or deferred.

### Refinement loop

1. **Seed → candidate:** Propose a concrete `TcpTransport` refactor (stateful `TcpListener`, per-accept nonce, remove `listener.Stop()` from `finally`).
2. **Evaluate:** Run existing REPL test suite (baseline). Add the multi-client round-trip test. Check `LinkId` uniqueness test. Run the pump thread-safety stress test.
3. **GEPA reflective mutation:** If a test fails, reflect on which design decision caused it (e.g. nonce collision → change nonce scheme; SRSW violation in GLP wrapper → fix mode declarations). Apply the mutation.
4. **Repeat** until: all pragmatic tests pass + SRSW formal gate passes + FR-057 isolation confirmed. No external API; Claude drives the evaluate/mutate loop via Agent-tool seams.

---

## Formal tooling

### Lean 4 vs Rocq evaluation

**Lean 4 fit:** This seed is a transport-layer C# refactor with no language semantic change and no IL/wire contract change. The formal metrics are SRSW correctness (already gated by the existing type-checker in-repo — not requiring a proof assistant) and byte-parity (a compile-time/test constraint, not a theorem). Lean 4 is an excellent ITP for mechanized semantics and IL verification, but those concerns do not arise here. Lean 4 offers no specific advantage over the in-repo type-checker for this seed.

**Rocq fit:** Rocq (Coq) has strong infrastructure for verified concurrent data structures and transport protocols (e.g., via Iris/Trillium concurrent separation logic). The N-recv-loops / one-inbox thread-safety argument (`heap_fcp.cs:136-141`) could in principle be formalized in Rocq/Iris. However, this is far beyond what the seed requires — the heap-safety argument is already an informal invariant, not a gap needing a mechanized proof.

**Primary:** `n/a` — this seed requires no mechanized proof. The formal metrics (SRSW, byte-parity, FR-057 isolation) are all enforced by existing in-repo tools (type-checker, static analysis, test harness). No new proof assistant work is warranted.

**Alternative when:** If the multi-accept extension introduces a new GLP kernel (`_link_listen_many` or similar) that changes mode declarations, then SRSW correctness of the new kernel would benefit from the in-repo type-checker gate (not a proof assistant). Only if the kernel introduces a concurrent-heap-access concern that the existing single-owner argument does not cover would Rocq/Iris be worth considering. Circumstance: "none" for the current recommended option (T1 option 1 / no interface change).

**IL verification:** `n/a` — no IL/wire format change. The seed is purely transport-layer.

---

## Shapiro criteria preserved

1. **Committed-choice concurrency** — multiple clients connecting concurrently must not see non-deterministic interleaving of their GLP results. The single-owner heap + single-inbox pump (`heap_fcp.cs:136-141`, `LinkPump.cs:36`) already enforces this. The multi-accept extension must not introduce a second inbox or a second runner thread. Verified by the pump thread-safety stress metric.

2. **SRSW (Single-Reader/Single-Writer)** — the `_link_listen` kernel produces a `Requests` writer (a single writer cell). A multi-accept loop means `Requests` grows as a cons-list of `request(...)` tokens, each token carrying one `LinkId`. Each `request` token is ground and bound once (SRSW-clean). The GLP wrapper `request_listener/2` declares `Requests?` as a single writer — this is preserved: the kernel binds `Requests` to `[request(Id,Peer) | Tail]` with `Tail` a fresh writer. SRSW is preserved per the existing kernel pattern (`LinkListenKernel.cs:76-78`).

3. **Suspension correctness** — a GLP program calling `request_listener/2` recursively suspends on the `Requests` tail until a new connection arrives. The kernel's runner-thread blocking (`.GetAwaiter().GetResult()`) means the runner DOES NOT suspend — it blocks the runner thread. This is the same as the current base-MVP behavior. For the multi-accept recursive loop this means the runner blocks on each accept call. This is a known architectural constraint (§4.2 KEY GAP). The embedded-switch purpose (acting as a connectivity switch) requires this to be addressed in the #10 implementation; the blocking pattern must not prevent the engine from servicing other goals while waiting for a new client.

4. **Monotone variable binding** — `LinkId` values placed on the `Requests` stream are ground constants. Once bound, they are never rebound. This criterion is trivially preserved by the ground-relay invariant.

5. **Three-valued unification** — not directly affected. The `accept_link` guard `LinkId? =?= LinkId2?` (self.glp:525) uses three-valued comparison correctly; multi-accept adds more tokens to the stream but does not change the unification semantics.

**Embedded-switch framing:** for the embedded-switch purpose (connectivity to the outside world + internal OS actions), multi-accept is the mechanism by which the switch can serve more than one external client concurrently. Preserving committed-choice concurrency and the single-owner heap is what ensures the switch's internal action (GLP reduction) remains deterministic regardless of how many external clients are connected.

---

## Recommendation

Ship `multi-accept-transport-extension` as a **PREP/FOLLOW-UP** after #6 lands. The implementation is a `TcpTransport`-internal change:

1. Make `TcpListener` a stateful field on `TcpTransport`, bound on the first `ListenAsync` call for a given `(scheme, local)` address and kept alive until explicitly released.
2. Remove `listener.Stop()` from the `ListenAsync` `finally` block; add an explicit `StopListening` or `DisposeAsync` path.
3. Use a per-accept incrementing atomic nonce (T3 option 1) so each `LinkId` is unique.
4. Keep `ILinkTransport.ListenAsync` returning `Task<ILinkEndpoint>` (singular) — no interface change.
5. Update `LinkListenKernel` docs to note it is now re-callable (no code change required; behavior is already stateless per-call).
6. Add a multi-client round-trip test (two sequential clients, correct bindings per client).

The blocking-runner-thread concern (U1 / suspension-correctness) is deferred to the #13 GLP-written control program, which will restructure the accept loop as GLP concurrent clauses.

---

## Options for owner

| Label | Consequence |
|---|---|
| A — Keep `ILinkTransport` single-endpoint-per-call; stateful `TcpListener` in `TcpTransport` | Minimal interface change; GLP-level recursion drives multi-accept; preserves blocking-kernel model |
| B — Add `IAsyncEnumerable<ILinkEndpoint>` to `ILinkTransport`; kernel becomes a streaming kernel | Richer interface; requires a new kernel variant; complicates the Dart mirror (FR-060/061 byte-parity surface) |
| C — Defer #10 until #11 (compiled-IL-on-wire) lands so the GLP control program (#13) can own the loop | Pushes N-client support further out; keeps #10 simple when it finally ships |

---

## Open questions

1. Should `TcpTransport`'s stateful `TcpListener` be scoped to a port or to a `(scheme, local)` address? What happens if `ListenAsync` is called with different `local` addresses on the same transport instance?
2. How is the `TcpListener` torn down? A `CancellationToken` passed to the first `ListenAsync` call? An explicit `Dispose`? A new `StopListeningAsync`?
3. The dossier §12 risk 6 says multi-accept is a "hard dep for N-clients AND a GLP control program" — but #13 also depends on #11 (compiled-IL-on-wire). Does the N-clients goal for #10 target the C# host scenario only, or does it include the GLP-written control program (#13)?
4. The blocking `.GetAwaiter().GetResult()` in `LinkListenKernel.cs:63` blocks the runner thread per accept call. For the GLP-written control program (#13) to drive the listen loop without blocking other goals, the kernel will need an async / suspension-based redesign. Is that redesign part of #10 or #13?

---

## External refs

- `csharp/glp_link/transports/TcpTransport.cs:32-50` — one-accept-then-Stop (primary subject)
- `csharp/glp_link/transports/TcpTransport.cs:46-48` — Phase-6 comment
- `csharp/glp_link/primitives/LinkListenKernel.cs:32-81` — one-shot kernel; blocking `.GetAwaiter().GetResult()` at `:63`
- `csharp/glp_link/seam/ILinkTransport.cs:41` — `Task<ILinkEndpoint>` (singular) interface
- `csharp/glp_link/primitives/LinkRuntime.cs:50` — `Pending` dict (single endpoint per LinkId)
- `csharp/glp_link/primitives/LinkPump.cs:38,60` — `_recvLoops: List<Task>`, N recv-loops already supported
- `csharp/glp_link/transports/LoopbackTransport.cs:39-91` — already multi-rendezvous (comparison)
- `out/csharp/lib/engine/glp_engine.cs:135-136` — `serve/2` shape
- `programs/self.glp:513-516` — `request_listener/2` GLP wrapper
- `programs/self.glp:523-526` — `accept_link/4` GLP wrapper
- `programs/self.glp:456` — `Link(In,Out)` type
- `programs/self.glp:387-422` — `mwm` multi-way merge (fan-in substrate for N clients)
- Dossier §4.2, §4.3, §4.4, §4.5, §0.4, §12 risk 6, Appendix B row #10

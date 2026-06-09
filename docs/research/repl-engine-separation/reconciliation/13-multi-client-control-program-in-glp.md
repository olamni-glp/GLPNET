# Reconciliation Memo — #13 multi-client-control-program-in-glp

**Feature id:** `multi-client-control-program-in-glp`
**Dossier entry:** §11 #13 · kind: FOLLOW-UP
**Reconciliation date:** 2026-06-09
**Branch:** `026-engine-review-dossier`
**Methodology:** `reconciliation/SEED-RECONCILIATION-BRIEF.md`

---

## Dossier cross-references

| Anchor | Topic |
|--------|-------|
| §4.2 | Multi-accept gap — `TcpTransport.ListenAsync` accepts exactly one then stops |
| §4.3 | GLP control loop shape: `serve/2`, `request_listener`, `Link` mailboxes, `mwm` fan-in |
| §4.4 | N-clients heap safety (N links → N recv-loops → ONE inbox → ONE heap) |
| §4.5 | Advisory: C# host for MVP; GLP control program as the post-MVP target |
| §7 | Mailbox decision — OS-level vs in-GLP; both substrates exist; GLP = elegant target |
| §0.4 row "Multi-accept listener" | classification: refactor (Phase-6 deferred) |
| §0.4 row "Control program / per-client mailbox" | classification: net-new (host) / reuse (GLP loop) |
| §10.10 | Deferred research dimensions (non-gating) |
| §12 risk 6 | Multi-accept is a hard dep for N-clients AND GLP control program; deferred |
| Appendix B #13 | Registry entry pointing at this memo |

---

## Seed-vs-dossier-vs-code

### Seed profile (buildkit-roadmap brief, read-only, 2026-06-09)

```
Kind:  FOLLOW-UP
Scope: GLP-written control program (serve/2 loop + request_listener + Link mailboxes + mwm),
       N clients, Option-C funneled through one-inbox pump
Why:   The elegant target
deps:  #10 (multi-accept-transport-extension), #11 (compiled-il-on-the-wire-and-factor-out-compiler)
Notes: (§7 #13)
WSJF:  1.62  RICE: 1125
```

### Dossier §11 #13 scope

> "GLP-written control program (`serve/2`-loop + `request_listener` + `Link` mailboxes + `mwm`), N clients via the one-inbox pump"
> Why: "The elegant long-term target; needs multi-accept + IL wire."
> depends_on: 10, 11. §ref: §4.3, §7.

**Divergences:**

1. **"Option-C funneled through one-inbox pump"** — the seed brief mentions "Option-C" explicitly; the dossier does not use that label for #13 specifically (Option-C is the IsolateManager unbounded-Channel per-agent model referenced at `ILinkTransport.cs:14-16`). The dossier §4.4 confirms the single-heap / N-recv-loops architecture is already heap-safe, which is what "one-inbox pump" means functionally. The label is slightly imprecise but not contradictory.
2. **dep #11** — the seed lists dep #11 (compiled-il-on-the-wire) as a hard prerequisite. The dossier §4.3 says the GLP control program depends on "(a) the multi-accept extension and (b) the wire carrying compiled IL." This is consistent. However, it is worth flagging that the IL-on-wire dependency may be softer than it sounds (see Tension T-1 below).

### As-built code checks (read-only)

| Claim | File:line | Verified |
|-------|-----------|---------|
| `serve/2` embedded const | `out/csharp/lib/engine/glp_engine.cs:135-136` | Confirmed. The string includes `serve(Module,[Goal\|In]) :- ground(Module?) \| '_activate'(Module?,Goal?), serve(Module?,In?).` |
| `mwm` implementation | `programs/self.glp:387-422` | Confirmed. Full `mwm/2` + `mwm_main/2` + `mwm1/3` + `mwm_copy/4` + fan-in mechanics present. |
| `request_listener` GLP wrapper | `programs/self.glp:513-516` | Confirmed. Generic over Scheme/Endpoint. |
| `accept_link/4` GLP wrapper | `programs/self.glp:523-526` | Confirmed. |
| `Link(In,Out)` channel type | `programs/self.glp:456` | Confirmed. `Link(In, Out) ::= Channel(Stream(In), Stream(Out)).` |
| `link_send/3` | `programs/self.glp:535-536` | Confirmed. |
| `link_recv/3` | `programs/self.glp:547-548` | Confirmed. |
| `TcpTransport.ListenAsync` one-accept-then-stop | `csharp/glp_link/transports/TcpTransport.cs:32-50` | Confirmed. `listener.Stop()` in finally block after first `AcceptTcpClientAsync`. Comment `:46-47` explicitly marks "Phase 6". |
| `LinkListenKernel` comment re base-MVP = one request per rendezvous | `csharp/glp_link/primitives/LinkListenKernel.cs:22-26` | Confirmed. "Base-MVP: ONE request per rendezvous." |
| `LinkKernels.Install` registers all 7 link kernels | `csharp/glp_link/primitives/LinkKernels.cs:59-86` | Confirmed. All 7 present. |
| `LinkRegistry.GetOrEstablish` | `csharp/glp_link/primitives/LinkRegistry.cs:25-34` | Confirmed. |
| `LinkEstablish.WireEstablishedLink` | `csharp/glp_link/primitives/LinkEstablish.cs:29` | Confirmed. |
| N-links/one-heap safety | `out/csharp/lib/runtime/runtime.cs:22-69` | Confirmed. Single `HeapFCP` (`Heap`), single `GoalQueue` (`Gq`), `InboundPump` field at `:129` used by engine pump driver. |
| Producer/consumer split precedent | `programs/tests/link/pc.glp` | Confirmed. Two-process GLP over TCP-loopback. |
| Path-B handshake (request_listener → accept_link) | `programs/tests/link/pathb.glp` | Confirmed. One-shot request/accept works today (single request). |

**Dossier missed — new finding:**

- `mwm` is explicitly noted in self.glp (`:380-385`) to be **excluded from type-checking** ("DFA builder cannot handle compound type constructors like stream(Stream) in MwmInput"). The clauses are compiled for runtime use only. This is a latent risk for the GLP control program: the fan-in component `mwm` is type-system-exempt, meaning SRSW and type-correctness cannot be statically verified for the multi-client control program's fan-in layer. The dossier does not record this.
- `_link_listen` kernel (`LinkListenKernel.cs:63`) calls `transport.ListenAsync(...).GetAwaiter().GetResult()` — a blocking wait **on the runner thread**. With multi-accept this changes to an async loop that must NOT block the runner thread; the kernel API needs redesign alongside the transport extension.
- `pathb.glp` (the path-B test) already exercises `request_listener` + `accept_link` in a one-shot scenario, confirming the GLP control-program shape is **testable today** in the one-client case. The multi-client generalization is purely a transport-layer and kernel-threading extension.

---

## Classification check

**Dossier kind: FOLLOW-UP.** This is correct.

- Does the kind match reality? Yes. The feature is correctly classified as FOLLOW-UP (post-MVP): it depends on multi-accept (#10, a PREP/FOLLOW-UP) and compiled-IL-on-wire (#11, a FOLLOW-UP) — both post-MVP features. No MVP capability is gated on #13.
- Does as-built code support the scope? Partially. The GLP-language primitives (`serve/2`, `request_listener`, `link_recv`, `mwm`) are all in-repo and exercised in tests. What is absent: (a) the multi-accept transport loop, (b) the compiled-IL-on-wire codec, (c) a GLP program that wires all three together into a multi-client control loop. The scope is therefore "substrate + primitives exist, orchestration does not."
- Code-supported scope boundary: `serve/2` at `glp_engine.cs:135-136`, GLP wrappers at `self.glp:387-422, 513-548`, one-shot path-B test at `programs/tests/link/pathb.glp`. The multi-client generalisation requires `TcpTransport.cs:32-50` to be extended and `LinkListenKernel.cs:63` to move off the runner thread.

---

## Tensions

### T-1: Is the dep on #11 (compiled-IL-on-wire) truly hard?

**Evidence.** The dossier §4.3 says the GLP control program depends on "the wire carrying compiled IL." However, the seed's actual scope is a **GLP-written** control program running **inside** the engine process, not a cross-process compilation scenario. `serve/2` dispatches by calling `'_activate'(Module?, Goal?)` against already-loaded bytecode — it never needs to receive IL over a wire unless the goal-dispatching is itself remote. The link between #13 and #11 is that the GLP control program should eventually receive goals as compiled IL (per §9.1 Opt 2) rather than source text. But the control program can be prototyped and proven correct on **source-text goals** (§9.1 Opt 1, the MVP path), deferring the IL wire to when #11 ships.

**Options:**
1. Keep dep on #11 as-is — start #13 only after compiled-IL wire exists. Risk: blocks the "elegant target" indefinitely (both #10 and #11 are substantial).
2. Soften dep on #11: prototype the GLP control program with source-text goals (engine compiles internally), layer the IL-codec path as an incremental follow-up. The GLP logic and the mailbox design are fully separable from the encoding on the wire.
3. Split #13 into two seeds: #13a (GLP control program with source-text dispatch, depends only on #10) and #13b (GLP control program with compiled-IL dispatch, depends on #10 + #11).

### T-2: `mwm` type-system exemption creates an unverified fan-in core

**Evidence.** `programs/self.glp:380-385`: `mwm` is deliberately excluded from the DFA type-checker. The control program's correctness depends on `mwm` fanning N client streams into one inbox correctly — but this cannot be type-checked or SRSW-verified by the existing toolchain.

**Options:**
1. Accept the exemption as-is, test dynamically (behavioral harness with N-client scenarios). Document the exemption explicitly in the spec.
2. Redesign the fan-in using a different combinator that the type system can handle, OR fix the DFA builder to handle parameterized Stream types before implementing #13.
3. Use the formal-tooling path (mechanized GLP semantics in Lean/Rocq) to verify `mwm`'s stream-merge correctness directly, bypassing the DFA limitation.

### T-3: `_link_listen` kernel blocks the runner thread — incompatible with multi-accept loop

**Evidence.** `csharp/glp_link/primitives/LinkListenKernel.cs:63`: `.GetAwaiter().GetResult()` blocks synchronously. For a multi-accept loop (yielding N endpoints), this must become non-blocking or run on a background thread. If the kernel signature changes, all GLP programs using `request_listener` must be re-tested.

**Options:**
1. Multi-accept is handled entirely at the transport leaf (`TcpTransport`) behind the existing kernel API — the kernel stays blocking and calls `ListenAsync` in a loop. This requires the kernel to spawn a background task and inject each accepted endpoint into `LinkRuntime.Pending` asynchronously.
2. Redesign `_link_listen` to return a stream of pending requests (a writer-filling loop on a background task), which is the natural GLP representation. The kernel becomes a "start listening" primitive that immediately returns and fills the stream asynchronously.
3. Leave kernel unchanged; expose a separate `_link_listen_loop` kernel for the multi-client case, keeping backward compatibility with existing one-shot programs.

---

## Under-specifications

### U-1: What does the GLP control program look like exactly?

**Why it matters.** The dossier gives a shape (`serve/2` + `request_listener` + `Link` mailboxes + `mwm`) but no concrete GLP clause sketch. Without it, the spec cannot be verified. In particular: how does each accepted client get its own `Link(In,Out)` channel? Where does `mwm` fan-in occur (before or after per-client dispatch)? How does `serve/2` dispatch a goal that arrives on the fanned-in stream?

**Options:**
1. The spec author writes a concrete GLP program sketch (prototype) as part of `buildkit-specify` for #13.
2. Derive it from `pathb.glp` (path-B one-shot) + `serve/2` shape, demonstrating the multi-client extension incrementally.
3. Treat #13's spec as: write the GLP control program first (as a `programs/tests/link/` program), prove it works for N=1, then N=2, then N=N using the behavioral harness.

### U-2: Per-client mailbox identity and lifetime

**Why it matters.** Each client needs a `Link(In,Out)` channel. After the client disconnects (fault), what happens to its mailbox? Does the control program reclaim it? How is the per-client state (if any) garbage-collected? This is not addressed in the dossier.

**Options:**
1. Stateless control program: each request is dispatched once to `serve/2` and the `Link` channel is closed on fault.
2. Stateful: each client's `Link` channel persists and can be resumed after reconnect (requires persistence of the per-client `LinkId` + channel state, coupling with #7/#9).
3. Out-of-scope: #13 delivers stateless fan-in dispatch; stateful per-client sessions are a separate follow-up.

### U-3: `mwm` stream input format in the control-program context

**Why it matters.** `mwm/2` takes `In` as a list of `stream(Xs)` or `merge(NewIn)` elements (`self.glp:396-404`). In the multi-client scenario, `In` must be populated dynamically as new clients connect. The mechanism for adding new client streams to the fan-in at runtime is unspecified. The `merge(NewIn)` form suggests it, but how the control program generates and adds `merge(...)` entries per accepted client is not detailed.

**Options:**
1. Pre-enumerate N client streams before starting `mwm` (works for a fixed N; unsuitable for open-ended N).
2. Use the `merge(NewIn)` form to inject new client stream tails dynamically; the accept loop appends `merge(ClientIn)` into the growing `mwm` input list.
3. Replace `mwm` with a dedicated kernel-side per-inbox pump that is transport-aware (may be simpler to implement correctly and type-check).

---

## GEPA/DSPy refinement

### Applicability

**`methodological`**. This seed is a GLP-language design + systems extension problem, not an LM/codegen program that DSPy literally optimizes as a pipeline. GEPA/DSPy applies as the iterate-against-a-metric discipline: write a candidate GLP control program → evaluate against the metric combination (N-client correctness, Shapiro-criteria preservation, type/SRSW gates) → reflect on failures → revise the design.

### Seed definition

The seed is: write a GLP program in `programs/` that implements a multi-client engine control loop using `serve/2`, `request_listener`, `Link` mailboxes, and `mwm` fan-in, running inside a single GLP engine that correctly dispatches goals from N concurrently connected REPL-client processes.

The GEPA refinement target is: a GLP program + accompanying host-side `TcpTransport` multi-accept extension such that:
- N client processes can each send goal requests concurrently;
- all N goals reach the engine's single heap/scheduler correctly;
- per-client bindings are routed back to the originating client;
- no goal is lost, duplicated, or reordered relative to its per-client stream;
- the control program terminates cleanly when all clients close.

### Metrics combination table

| Name | Kind | Tool / Harness | Threshold |
|------|------|----------------|-----------|
| N-client round-trip correctness | pragmatic | Extend `programs/tests/link/pathb.glp` to N=2,3 clients; run in the REPL test suite (`test/run_all_tests.sh`) | All N clients receive correct bindings; no cross-client contamination |
| `serve/2` dispatch equivalence | pragmatic | Single-client run of the GLP control program must produce identical result to a direct `RunGoalAsync` call on the same goal | Results byte-equal across both execution paths |
| Type/SRSW gate (control program) | formal | In-repo type-checker + SRSW verifier applied to the new `.glp` control program (excluding `mwm` — see T-2) | Zero type errors; zero SRSW violations in the control-program code outside `mwm` |
| `mwm` stream-merge correctness | formal | Lean 4 mechanized property: for a stream of N client input lists `[s1, ..., sN]`, `mwm` produces a merged output that is a fair interleaving containing every element of every `si` exactly once | Lean 4 proof compiles; checked by Lean-LSP-MCP tactic loop |
| SRSW preservation (GLP semantics) | formal | Lean 4 / Rocq mechanized: the `serve/2`-based control loop + `mwm` fan-in preserves SRSW (each variable written at most once per clause) across the whole control-program execution | Proof assistant accepts the mechanized statement |
| Multi-accept transport extension correctness | pragmatic | N parallel `TcpClient` connections to the extended `TcpTransport`; verify all N endpoints are accepted, no connection is dropped | 100% acceptance rate for N=10 parallel connections in a loopback test |
| Kill-and-restart with N active clients | pragmatic | Kill the engine process mid-run; verify clients receive `permFail` fault terms and the fault stream terminates cleanly | Fault lattice correctly surfaced on all N client fault streams |

### Interactive spec step

At the start of `/buildkit-specify` for this seed, the owner confirms:
1. Whether dep #11 is hard or soft (Tension T-1): source-text dispatch vs IL-on-wire dispatch.
2. The per-client mailbox lifetime model (Under-spec U-2): stateless vs stateful.
3. The `mwm` dynamic-injection mechanism (Under-spec U-3): pre-enumerated vs `merge(NewIn)` append.
4. Which formal properties to mechanize first: `mwm` stream-merge correctness, or the broader SRSW-preservation argument for the control loop.
5. The metric thresholds for N (how many concurrent clients is the initial correctness target: N=2 or N=10?).

### Refinement loop

Each iteration: write (or extend) the GLP control program candidate → run the REPL test suite (`bash test/run_all_tests.sh`) for the pragmatic metrics → run the type-checker/SRSW gate → invoke the Lean 4 tactic loop (via Lean-LSP-MCP, Claude-driven, no external API) for the formal metrics → identify failing metric(s) → apply a GEPA reflective mutation to the GLP program or the Lean proof sketch → repeat. Terminate when all metric thresholds hold and the roadmap-sequence fit is confirmed (deps #10 and #11 satisfied or dep scope narrowed per T-1 Option 2/3).

---

## Formal tooling

### Lean 4 evaluation

**Fit:** High. The two core formal properties — `mwm` stream-merge correctness and SRSW preservation for the control loop — are inductive properties over streams and logical variables, which are natural in Lean 4's dependent type theory. Lean 4's MathLib has streams/coinductive sequences; Lean-LSP-MCP enables a Claude-driven tactic loop. The `serve/2` loop shape maps to a well-founded recursion proof. The `mwm` fan-in is a concurrent stream merge, representable as a non-deterministic interleaving; the formal property (every element appears exactly once) is a standard trace-inclusion argument amenable to Lean 4 induction. Lean 4's `#check` / `#eval` cycle closes the feedback loop quickly.

### Rocq evaluation

**Fit:** Also high for this seed. Rocq/Coq has a strong track record for verified logic-language properties (the WAM-verification line, TWAM); the `mwm` fan-in and suspension reactivation proofs fit Rocq's coinductive reasoning well. The `CoInductive` type for potentially-infinite streams is idiomatic in Coq/Rocq.

**Primary:** `lean4`. The rationale: (a) Lean 4 + Lean-LSP-MCP is the Claude-native toolchain for the agentic tactic loop; (b) the properties in this seed (stream induction, SRSW preservation) are within Lean 4's strengths and MathLib coverage; (c) the owner's stated preference is Lean 4 as the primary across the board unless per-seed evaluation identifies a specific counter-case. No counter-case exists for #13: the proofs are inductive over finite structures (N clients, finite goal lists), not requiring advanced coinductive reasoning that would favour Rocq.

**Alternative when:** Rocq/Coq should be kept as an alternative if the `mwm` proof requires coinductive reasoning over infinite streams (i.e., if the multi-client scenario is generalized to unbounded/non-terminating sessions rather than finite lists). In that case, Rocq's `pcofix` / `cofix` infrastructure is more battle-tested for coinductive proofs in logic-language settings.

### IL verification

**n/a** — this seed does not directly introduce or modify an IL wire codec. It reuses the existing GLP-level link primitives (`link_send`, `link_recv`, `accept_link`, `mwm`) and relies on the multi-accept transport extension (#10) and (optionally) compiled-IL-on-wire (#11). The IL verification obligation belongs to #10 (transport byte-contract extension) and #11 (IL codec round-trip). If the dep on #11 is softened (T-1 Option 2/3), #13 carries zero IL-verification obligation of its own. The `FrameCodec` byte-parity standard (`FrameCodec.cs:31-32`, FR-060/061) is inherited unchanged.

---

## Shapiro criteria preserved

This step must preserve the following GLP/Shapiro design criteria, framed for the embedded-switch purpose (engine as connectivity switch + OS actor host):

1. **Committed-choice concurrency.** The `serve/2` control loop + `mwm` fan-in must preserve GLP's committed-choice semantics: once a clause is selected (guard passes), execution is deterministic and non-backtracking. The multi-client control program must not introduce OR-parallel choice between client streams — each dispatched goal is independently committed.
2. **SRSW (Single-Reader/Single-Writer).** Every variable in the control program (the per-client `Link` channels, the `mwm` input list entries, the `serve/2` stream) must satisfy SRSW: each variable written at most once per clause. The `mwm` type-system exemption (T-2) means this must be verified by an alternative mechanism (formal proof in Lean 4) for the fan-in component.
3. **Suspension correctness.** A client goal that suspends (because it is waiting on an unbound reader) must correctly remain in the suspension set until the binding arrives; it must not spuriously reactivate and must not block other clients' goals. The N-recv-loops → ONE inbox pump architecture (confirmed heap-safe at `runtime.cs:22-69`) must be shown to preserve this under multi-client concurrency.
4. **Monotone variable binding.** Once a variable is bound (writer receives a ground term), it is never re-bound. The per-client `Link` channel variables and `mwm` merge-stream variables must satisfy this. Any mechanism for "adding a new client" (U-3) must not re-bind an existing writer.
5. **Three-valued unification soundness.** The `accept_link` guard `LinkId? =?= LinkId2?` uses three-valued unification to match the arriving request; this guard must be preserved correctly in the multi-client loop (each `accept_link` call sees the right `OpenStream` view of the request stream and the right `LinkId` to match).

Pragmatic checks for these criteria: the SRSW gate (formal, in-repo type-checker for the non-`mwm` parts); behavioral N-client round-trip test (suspension correctness, monotone binding); Lean 4 mechanized proof of `mwm` stream-merge (SRSW + committed-choice for the fan-in); kill-and-restart test (suspension-correctness + monotone binding survive process restart).

---

## Recommendation

Align with the dossier advisory (§4.5, §7): #13 is the correct long-term target for the GLP-in-GLP control program. The seed is correctly classified FOLLOW-UP and correctly blocked on #10 (multi-accept).

**Primary recommendation:** Soften the dep on #11 (T-1 Option 3 — split into #13a and #13b). The GLP control program design, the `mwm` fan-in correctness proof, and the multi-client behavioral harness are all fully testable with source-text dispatch (no IL-on-wire needed). Decoupling from #11 cuts the critical path by an entire FOLLOW-UP feature.

**Secondary recommendation:** Resolve T-2 (`mwm` type-system exemption) before or during #13. Options are dynamic-testing-only (acceptable but leaves a formal gap) or fixing the DFA builder. At minimum, the Lean 4 mechanized proof of `mwm` stream-merge correctness (SRSW + committed-choice) must be in scope for #13 as its formal correctness substitute.

**Tertiary:** Address U-3 (dynamic `merge(NewIn)` injection) explicitly in the spec. The `mwm` `merge/1` form exists in the code (`self.glp:400-403`) but its use in a dynamic multi-client accept loop is unspecified and untested.

---

## Options for owner

| Label | Consequence |
|-------|-------------|
| Keep dep on #11 hard | #13 blocked until both #10 + #11 ship; longest critical path; IL-on-wire and GLP control program proven together |
| Soften dep #11: split #13 into #13a (source-text, dep on #10 only) + #13b (IL-on-wire, dep on #10+#11) | Cuts critical path; GLP control program logic proven earlier; IL dispatch added incrementally |
| Resolve `mwm` type-system exemption before #13 (fix DFA builder) | Higher confidence in fan-in correctness; unblocks type-checker gate for #13; adds scope to a prerequisite |
| Accept `mwm` exemption; use Lean 4 mechanized proof as the formal substitute | Keeps #13 self-contained; Lean 4 proof compensates for type-checker gap; consistent with formal-tooling plan |
| Add `_link_listen_loop` kernel (T-3 Option 3) to keep backward compatibility | Lowest coupling impact; existing one-shot programs unaffected; adds a parallel accept-loop kernel for the multi-client case |
| Redesign `_link_listen` to fill stream asynchronously (T-3 Option 2) | Cleanest GLP representation; allows `request_listener` to be used uniformly for both one-shot and multi-client; requires kernel signature change + test update |

---

## Open questions

1. Is `mwm`'s exclusion from type-checking a permanent design decision or a known gap to be fixed? (Affects the formal-gate design for #13.)
2. Does the GLP control program's `serve/2` dispatch model support returning per-client result bindings back over the correct client `Link` channel? (The current `serve/2` activates goals but does not have a built-in result-routing mechanism — how do result bindings reach the right client link's Out stream?)
3. What is the expected termination condition for the multi-client control loop? Does it run forever (daemon) or terminate when the last client closes?
4. How does the multi-accept `TcpTransport` loop interact with the kernel's blocking rendezvous model? Does `_link_listen` need to become a background-task-injecting kernel?
5. Should #13's test harness be a new `.glp` file in `programs/tests/link/` (extending `pathb.glp`) or a separate C# integration test that spawns N REPL processes?
6. The seed notes "Option-C funneled through one-inbox pump" — is Option-C (`IsolateManager` per-agent channel routing) still in scope here, or has the feature-025 link layer superseded it entirely for the multi-client case?

---

## External refs

- `SEED-RECONCILIATION-BRIEF.md` §3.5 (Shapiro/embedded-switch pragmatic anchor)
- `SEED-RECONCILIATION-BRIEF.md` §3.2a (Lean 4 / Rocq tooling matrix; no-API resolution)
- `design-dossier.md` §4.3 (GLP control loop shape)
- `design-dossier.md` §7 (mailbox decision)
- `design-dossier.md` §12 risk 6 (multi-accept hard dep)
- `programs/tests/link/pathb.glp` (path-B one-shot reference)
- `programs/tests/link/pc.glp` (producer/consumer split reference)
- [APOLLO — model-agnostic agentic Lean proving](https://arxiv.org/abs/2505.05758) (Claude-driven tactic loop)
- [TWAM: Certifying Abstract Machine for Logic Programs](https://arxiv.org/pdf/1801.00471) (verified-IL precedent)
- Lean-LSP-MCP / Lean Copilot (Claude-native Lean tactic integration)

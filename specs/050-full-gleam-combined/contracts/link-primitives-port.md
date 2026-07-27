# Contract: link-primitives → Gleam host-kernel port (T050.C0 scope breakdown)

**Status:** T050.C0 deliverable — the normative scope breakdown for the T050 link-primitives
port (steps T050.C1–C8 in `tasks.md` / marathon `mrun-56564f6cdca3`). Produced by reading the
ratified GLP surface (`programs/self.glp`), the ratified plan gate
(`specs/025-multi-protocol-link-layer/contracts/rulings-log.md`), the architecture context
(`.../architecture-context.md`), and the two port oracles (C# `csharp/glp_link/`, Dart
`glp_runtime/lib/link/`).

**Source precedence (DISCIPLINE §1.10) — load-bearing:**
`programs/self.glp` (shipped) + `rulings-log.md` (ratified) **>** `link-primitives.md` body / `architecture-context.md` (proposals) **>** C# reference **>** Dart reference. **C# is NEVER the source of truth**; where the C# reference and the ratified surface differ, STOP and escalate. The C#/Dart trees are *port oracles for structure*, not authorities for GLP-visible behaviour.

**Fidelity anchor:** GLP-visible semantics are frozen by `self.glp` + the 025 contracts. Deviations that change GLP-visible behaviour STOP and escalate (Language Authority §1.14). Host-mapping choices (module split, sync-vs-async, effect-state threading) are implementation decisions, recorded here.

---

## 0. What T050 actually is (the scope, corrected)

**T050 authors NO GLP.** The entire GLP-side link surface already ships in `programs/self.glp`
(relocated there Gabi-approved, commit `6c21281e`, "callable universally like send/receive"):
the 7 host-kernel **declarations** (`self.glp:469-475`) and the 12 GLP **wrapper clauses**
(`self.glp:483-571`). All types are declared (`self.glp:430-461`). Loading `self.glp` through the
Gleam pipeline already succeeds.

**T050 = implement the 7 host kernels in Gleam and wire them into the engine**, against the
existing Gleam transport seam (`glp/link/seam/`, `transports/`, `reliability/` — built T045–T049).
The gap is exactly the missing `glp_gleam/src/glp/link/primitives/` directory (confirmed absent).

**The base language surface is RATIFIED, not gate-blocked.** `rulings-log.md`: *"PLAN-APPROVAL
GATE: COMPLETE. The 9 base link primitives + the approved guard set + the three core fixes are
approved-to-implement under language authority"* (Gabi 2026-06-06) + T033 path-B ratification
(2026-06-07). The `link-primitives.md` "PROPOSAL / NOTHING decided" header is **stale** — superseded
by `rulings-log.md`.

**Primitive count, disambiguated** (the docs drift between "8/9/10"): **7 host kernels** +
**12 GLP wrapper clauses** (10 distinct wrapper names; `link_close` has /1+/2). C0 counts by the
**7 host kernels**, since those are the only Gleam work.

---

## 1. The ratified surface (authoritative — `self.glp`)

### 1.1 Types (`self.glp:430-461`) — all shipped, no work
```prolog
Scheme   ::= String.
Endpoint ::= String ; ep(String, Integer).
Nonce    ::= Integer ; String.
LinkId   ::= link_id(Scheme, Endpoint, Nonce).
AgentId  ::= String ; Integer ; peer(String, Integer).
LinkRole ::= listener ; connector.
Reason   ::= String.
Fault    ::= ok ; closed(LinkId, Reason) ; tempFail(LinkId, Reason) ; permFail(LinkId, Reason).
FaultStream ::= Stream(Fault).
Link(In, Out) ::= Channel(Stream(In), Stream(Out)).
Rendezvous ::= rendezvous(Scheme, Endpoint).
RequestMsg ::= request(LinkId, AgentId).
```

### 1.2 The 7 host kernels (`self.glp:469-475` — declarations, runtime-implemented)
| # | Kernel | Modes (`?`=consumed, bare=produced) | Role |
|---|---|---|---|
| K1 | `'_link_setup'/5`   | `(LinkId?, Role?, In, Out?, Faults)`       | path-A establish-or-reuse (idempotent at identity, FR-007) |
| K2 | `'_link_send'/3`    | `(Msg?, LinkId?, ToPeer?)`                 | ground-relay sender — **NO globalize** (R-7) |
| K3 | `'_link_request'/5` | `(LinkId?, ToPeer?, LinkIn, LinkOut?, Faults)` | path-B connector half |
| K4 | `'_link_listen'/3`  | `(Scheme?, Endpoint?, Requests)`           | rendezvous producer → `request(LinkId,FromPeer)` stream |
| K5 | `'_link_accept'/5`  | `(LinkId?, FromPeer?, LinkIn, LinkOut?, Faults)` | path-B accept half |
| K6 | `'_link_monitor'/2` | `(LinkId?, Faults)`                        | per-link fault monitor stream (independently observable, FR-008) |
| K7 | `'_link_close'/2`   | `(LinkId?, Reason?)`                       | abrupt teardown + distributed GC + terminal `closed(_,_)` |

### 1.3 The 12 GLP wrappers (`self.glp:483-571`) — all shipped, no work
`link_setup/4`, `server_listener/3`, `client_connector/3`, `request_link/4`,
`request_listener/2`, `accept_link/4`, `link_send/3`, `out_relay/3`, `link_recv/3`,
`link_monitor/2`, `link_close/1`, `link_close/2`.

Note `link_send/3` (`self.glp:536`) and `link_recv/3` (`self.glp:548`) are **pure GLP stream-cons
clauses** (the `self.glp` send/receive idiom) — they carry no kernel. The host work behind them is
the **egress drainer** (drives K2 semantics off the channel `Out`) and the **ingress pump** (extends
the channel `In`). See C5/C6.

---

## 2. Existing Gleam substrate (T045–T049) — build on, do not rebuild

| Module | Key surface | Role |
|---|---|---|
| `link/seam/endpoint.gleam` | `Endpoint(id, send: fn(BitArray)->Result(Nil,Sig), recv: fn()->Result(Option(BitArray),Sig), close: fn()->Nil, faults: Subject(Sig))` | **synchronous** vtable endpoint. `recv Ok(None)`=clean EOS. Faults on `gleam_erlang` `Subject`, **no gleam_otp**. |
| `link/seam/transport.gleam` | `Transport(supported_schemes, listen, connect)` + `serves/2` | per-scheme leaf vtable; `listen`/`connect` → `Result(Endpoint, Sig)` |
| `link/seam/link_fault.gleam` | `LinkFaultSignal(link, kind: Closed\|Transient\|Permanent, reason)` | coarse seam-level fault; sublayer refines to `ok`/`closed`/`tempFail`/`permFail` terms |
| `link/seam/link_id.gleam` | `LinkId`, `LinkNonce` | stable never-reused identity (FR-007) |
| `link/seam/{link_scheme,link_address,link_options}.gleam` | `LinkScheme` (loopback/tcp/quic), `LinkAddress` (path/endpoint), `LinkOptions` (timeouts) | seam value types |
| `link/transports/{loopback,tcp}.gleam` | `new() -> Transport` | T048 loopback, T049 TCP (Erlang FFI `src/glp_link_tcp_ffi.erl`, passive gen_tcp) |
| `link/reliability/{crc32,frame_codec}.gleam` | `compute/1`; `encode/*`, `parse_frame/1`, `version=0x01`, `header_size=22` | 038 TLV framing (byte-parity). **NOT** dedup/reorder/epoch/GC — those are T052. |

**Confirmed absent:** `glp/link/primitives/` — the C1–C8 gap.

---

## 3. Effectful-kernel seam precedent (mad `_send`, T050.A2) — the wiring template

An effectful host kernel does **not** live in the pure `kernels.dispatch` table. It is dispatched
at the **label-miss fallback** with an out-of-band effect-state value:

- Dispatch site: `runner.gleam:1910` — when `kernels.dispatch` misses (`:1890`), fall to
  `mad_spawn(ctx, ...)` (`runner.gleam:1922`).
- Effect state: `mad_kernels.MadState(w_p, m_p, mad_spawns)` (`mad_kernels.gleam:37`), threaded as
  `mad: Option(MadState)` on both `RunnerContext` (`runner.gleam:223`) and `Reduced`
  (`runner.gleam:83`); injected by `with_mad/2` (`runner.gleam:257`).
- Outcome: `mad_kernels.MadOutcome` (`:43`) = `MadEffect(heap, state, woken)` (success — mutate host
  state, accumulate work, reactivate goals) **|** `MadAbort(detail)` (non-fatal → `Failed`, NOT a
  fatal `RunnerError`).
- Reactivation without `onBind`: bind a local writer via `heap.bind_writer` → the woken `GoalRef`s
  ride `scheduler.reactivate`. **The Gleam heap has no `onBind`** — reactivation is *always* via woken
  goals. This is the single most load-bearing deviation from the C#/Dart oracles (§5).

The E5 ratification (2026-07-14): a **parallel** effect-outcome type is correct; **do not widen
`KernelOutcome`** (touches ~30 dispatch arms). The link kernels follow this precedent.

---

## 4. Gleam module map → C0 steps

New dir `glp_gleam/src/glp/link/primitives/`. Each module lists its C# / Dart oracle and the step
that builds it.

| Gleam module (proposed) | Builds in | C# oracle | Dart oracle | Role |
|---|---|---|---|---|
| `link_terms.gleam` | C1 | `primitives/LinkTerms.cs` | `link_terms.dart` | ground-resolve (deref to VarRef-free tree — ground gate) + Parse{LinkId,Role,Scheme,Endpoint,Reason,RequestToken} + build `ok`/`closed`/`tempFail`/`permFail`. **Requote/Unquote so rebuilt terms are byte-identical for `=?=`.** |
| `link_registry.gleam` | **C1** | `primitives/LinkRegistry.cs` | `link_registry.dart` | **R-5 canonical registry** — `Dict(LinkId, LinkHandle)`, `get_or_establish`/`try_get`/`remove`. Idempotency-at-identity (FR-007). |
| `link_handle.gleam` | C1 | `primitives/LinkHandle.cs` | `link_handle.dart` | per-link state: endpoint, In/Out/Faults heap cursors, monitor cursors, sequencer |
| `link_establish.gleam` | **C1** | `primitives/LinkEstablish.cs` (`WireEstablishedLink`) | `link_establish.dart` | **R-5 convergence core** — the ONE funnel all of K1/K3/K5 (+ K4's parked endpoint) pass through into the registry; wires cursors, arms egress, starts pump |
| `link_runtime.gleam` | C1 | `primitives/LinkRuntime.cs` | `link_runtime.dart` | per-engine aggregate: transports, registry, pump, capability gates, `pending` (LinkId→endpoint park for listen→accept) |
| `transport_registry.gleam` | C1 | `primitives/TransportRegistry.cs` | `transport_registry.dart` | scheme→leaf lookup (selects loopback/tcp) |
| `capability_gate.gleam` | C1 | `primitives/CapabilityGateRegistry.cs` | (n/a) | gate iface + **default allow-all** (base MVP; macaroon "quic" gate out of base scope) |
| `link_kernels.gleam` | C2 (+each) | `primitives/LinkKernels.cs` (`Install`/`Register`) | `link_kernels.dart` | the 7 kernel arms + `LinkState`/`LinkOutcome` effect pair + **dispatch wiring at the runner label-miss** (§5) |
| `link_pump.gleam` | **C6** | `primitives/LinkPump.cs` | `link_pump.dart` | ingress — process owns blocking `endpoint.recv`, forwards decoded items; `try_apply_next` extends `In` on the runner thread + wakes suspended `link_recv` |
| `link_egress.gleam` | **C5** | `primitives/LinkEgress.cs` + `LinkEstablish.ArmEgress` | `link_egress.dart` | `ship_ground` (resolve→encode→frame→seq→send) + the **Out-drainer lowered goal** (no `onBind`; §5) |
| `link_faults.gleam` | C7 | `primitives/LinkFaults.cs` | `link_faults.dart` | monitor cursors, `extend`, `deliver_fault` (fan to all cursors), `from_signal` |
| `link_teardown.gleam` | C8 | `primitives/LinkTeardown.cs` | `link_teardown.dart` | close + terminal `closed(LinkId,Reason)` + per-link GC |

### Step → deliverable
- **C1 establish-core + registry (R-5):** `link_registry` + `link_establish.wire_established_link` +
  `link_handle` + `link_runtime` + `transport_registry` + `capability_gate` + `link_terms`. The one
  canonical ground-`LinkId`-keyed registry both paths converge on; **prove both paths yield an
  indistinguishable established link (FR-002)**. Also settle the `LinkState` engine-surface shape (§5).
- **C2 `_link_setup/5`:** K1 kernel arm through `wire_established_link`.
- **C3 path A (listen/connect):** exercise K1 with `listener` + `connector` roles over the T049 TCP
  transport (the GLP `server_listener/3`/`client_connector/3` wrappers already delegate to
  `link_setup/4`). Round-trip test.
- **C4 path B:** K4 `_link_listen` (+ `pending` park) + K3 `_link_request` (in-band `request(...)`
  token) + K5 `_link_accept` (adopt parked endpoint) — all converge on `wire_established_link`.
- **C5 `_link_send/3` + egress:** `link_egress.ship_ground` (K2, ground-relay, **no globalize**) +
  the Out-drainer goal. Resolve the `onBind` deviation (§5).
- **C6 `link_recv` + pump:** `link_pump` ingress; extend `In`, wake suspended `link_recv`.
  **Dedup/reorder/reliability stays T052** — base routes through the ingress but does not build the
  sublayer.
- **C7 monitor + faults:** `link_faults` + K6 `_link_monitor`; fault vocab (§5 deviation).
- **C8 close:** `link_teardown` + K7 `_link_close`; terminal `closed(_,_)`, GC.

---

## 5. Deviation list (Gleam port ≠ C#/Dart oracle) — READ BEFORE C1

**D-1 (fault vocab — `ok` arity). RESOLVED, no escalation.** Ship bare `ok` (arity 0) + `closed/2` +
`tempFail/2` + `permFail/2`, per `self.glp:451`. The C# oracle agrees (`LinkTerms.Ok()` → `ok`).
`architecture-context.md §5`'s `ok(LinkId)` (arity 1) is a **superseded proposal** — do NOT emit it.

**D-2 (no `heap.onBind` — the egress deviation). DESIGN, decide in C5.** The C#/Dart egress arms
`heap.OnBind(outWriterAddr, …)` to observe the program binding the channel `Out` writer and ship the
cons head. **Gleam has no `onBind`.** The port must drive egress the way A3 drove `global_send`:
lower the Out-drainer to a **runnable goal guarded on `known(Out?)`** that ships the head and re-arms
on the tail, reusing the existing suspension/reactivation machinery. *Recommendation:* egress-drainer
goal, no bespoke `onBind` (mirrors A3; keeps the base discipline in existing machinery). This is a
host-mapping choice, **not** GLP-visible.

**D-3 (sync seam vs async oracle). RESOLVED by T049 precedent, no escalation.** C#/Dart use an async
`Task<byte[]?> RecvBytesAsync` + a background pump + a thread-safe inbox drained by `try_apply_next`.
Gleam `endpoint.recv` is **synchronous blocking** (T045/T049). The pump is therefore a **BEAM process**
(`process.spawn`/`new_subject`/`receive`, **no-OTP**) owning the blocking recv, forwarding decoded
items to the runner via a `Subject`, drained on the runner thread. Consistent with the ratified
madGLP Phase-B process model.

**D-4 (`_link_send` is ground-relay, NOT the globalize path). RATIFIED, guard against mis-wiring.**
`architecture-context.md §1` says the transport seam sits on the globalize/`known/1` path — that is
the **general substrate for later glink**, not the base sender. The RULED base K2 is **ground-relay**
(OQ-3 option (a), R-7): `ground(Msg?)` gate, no globalize, no `_w`/`_r` minting. Wiring K2 into the
globalize path would silently collapse to the buggy open-structure territory (R-3). Keep K2 pure
ground-relay.

**D-5 (`LinkState` engine-surface addition). DESIGN, decide in C1.** The link kernels need host effect
state (registry, pump handles, pending) threaded like `MadState`. Options: (a) a **parallel**
`Option(LinkState)` field on `RunnerContext`/`Reduced` dispatched at the same label-miss site; (b) a
generalized effect slot shared with mad. *Recommendation:* (a), following the E5-ratified "parallel
outcome, don't widen `KernelOutcome`" precedent. This is engine-internal structure (implementation),
**not** a §1.14 language change — the 7 kernels themselves are already ratified. Flag the
`RunnerContext`/`Reduced` field addition in the C1 commit for visibility, as A2 did.

**D-6 (registry identity — do NOT conflate two registries). NOTE.** The link-layer `LinkRegistry` is
keyed by **ground `LinkId`** (this contract, R-5). The madGLP distinguished-channel registry
(`madglp-port.md`) is a separate `(role, channel-tag)` namespace **above** transports. They are
different structures; C1 builds only the LinkId-keyed one.

**D-7 (capability gate — base is allow-all). NOTE.** C# has a `CapabilityGateRegistry` with a
macaroon gate for `"quic"`. The base MVP over loopback/tcp uses the **default allow-all** gate; the
gate seam exists (fail-closed on evaluation error) but carries no real policy in base scope.

**D-8 (reliability sublayer deferred to T052). SCOPE.** Sequencer/reassembler/dedup/reorder/epoch-
fencing/distributed-GC (`architecture-context.md §4`) are the load-bearing net-new work (R-1) and are
**T052**, not T050. Gleam `reliability/` today has only framing (crc32 + frame_codec). C6 routes the
base through the ingress **without** building dedup; a duplicate frame is out-of-scope for the base
(R-2 lives in T052). `link_close` GC (C8) reclaims registry/handle entries only — not the full
distributed GC.

---

## 6. Risks carried into implementation (from `link-primitives.md §8`)

- **R-5 (two paths, one registry):** the headline C1 obligation — listen/connect AND request/accept
  must converge on the same `wire_established_link` → registry keyed by identical `LinkId`
  normalization, else FR-007 idempotency + FR-026 origin-auth keying break.
- **R-1/R-2 (reliability is the real unbuilt work):** deferred to T052 (D-8); do not under-scope C6 as
  "done" — it is base-ingress only.
- **R-7 (`_send` ≠ ground-relay):** K2 is a distinct kernel; never route it through mad `_send` (D-4).
- **R-4 (host predicates authored outside `out/csharp`):** the Gleam analogue — link primitives live
  in `glp/link/primitives/`, never in generated/ported code, so nothing clobbers them.

---

## 7. C0 exit — what C1 starts from

- Gleam authors **only host kernels + wiring**; the GLP surface is shipped (`self.glp`).
- The 7 kernels + fault vocab are **ratified** (rulings-log); not §1.14-gate-blocked.
- C1 first: `link_registry` + `link_establish.wire_established_link` (R-5), the `LinkState` seam
  shape (D-5), and `link_terms` foundation.
- Oracle precedence: `self.glp` + rulings-log **>** proposals **>** C# **>** Dart. On any C#-vs-ratified
  divergence: **STOP and escalate** (DISCIPLINE §1.10).

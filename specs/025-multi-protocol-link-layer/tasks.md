# Tasks: Multi-Protocol Peer-to-Peer Link Layer for Distributed GLP

**Feature**: `025-multi-protocol-link-layer` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)
**Gate**: plan-approval COMPLETE (rulings in [contracts/rulings-log.md](contracts/rulings-log.md)).

Conventions: `[P]` = parallelizable with siblings (different files, no dep). Each task lists FR/SC refs + file targets. **C#-first** (the `out/csharp` reference + the clobber-safe `csharp/glp_link/`); the Dart mirror is Phase 8. **Baseline gate (FR-067/SC-017): `bash test/run_all_tests.sh` green before AND after every core-touching task** — listed once here, applies throughout. All exemplar GLP must clear [contracts/glp-correctness-review.md](contracts/glp-correctness-review.md) before it is promoted to a runnable test.

---

> **🔴 RESUME POINTER (2026-06-07).** Phases 0–2 complete; **T030 `'_link_setup'/5`
> + T031 `'_link_send'/3` + T032 recv-ingress DONE** (`csharp/glp_link/primitives/`:
> `LinkSetupKernel`, `LinkSendKernel`, shared `LinkEgress.ShipGround`; T032 proved the
> existing T030 pump + T022 `InboundOrdering` ingress satisfies the FULL recv contract
> (suspend / reactivate-exactly-once / dup-no-op / reorder) — NO new production code,
> contract tests only; tests `LinkSetupKernelTests`+`LinkSendKernelTests`+`LinkRecvIngressTests`;
> **76 xUnit green**; Option-B pump live). **Next = T033 request/accept**
> (`'_link_request'/5`+`'_link_accept'/5`+`request_link/4`+`accept_link/4`; in-band
> request frame, both paths converge on the T030 registry), then T034 monitor, T035
> close, T036 `programs/lib/link.glp` wrappers (the GLP clause text for `link_send/3`,
> `out_relay/3`, AND `link_recv/3` lands HERE), T040 producer/consumer over loopback
> (the functional proof). T030–T032 touched NO core (`out/csharp`/`programs/`), so the
> REPL baseline is unaffected. **`marathon resume` is STALE at T012 — ignore it; this
> file + `git log` are authoritative.**

## Phase 0 — Baseline + the three live core fixes (no link layer yet)

- [x] **T001** Record green baseline: `bash test/run_all_tests.sh`; capture counts. (SC-017) — **524/525** (1 known pre-existing AOT-smoke failure; matches T011/T012 baseline). Established green baseline for Phase 2.
- [x] **T002** Fix **FR-021 duplicate-delivery crash**: add the dedup gate to the inbound ingress so a redelivered (seq + global-name) frame is a **verified no-op** — no `StateError`, no re-bind, no re-enqueue (today `mad_context` `_handle*Assignment` throw; `bindWriter` throws on a bound cell). Files: `csharp/glp_link/reliability/` + ingress; mirror later. (FR-021/SC-008)
- [x] **T003** Fix **FR-034 compound-operand-suspend**: `_dereferenceWithTracking` must recurse into `StructTerm.args` (cycle-safe), mirroring the correct `GroundEqual.collectUnbound`, so a nested unbound reader SUSPENDS not FAILs. Files: runner guard path. (FR-034/SC-009)
- [x] **T004** Fix **FR-035 imported-reader reactivation (OQ-2 = option 1)**: wire `handleMadAssignment` → `bindImportedReader` (drain `VariableEntry.suspensions`); KEEP the `VariableEntry` path (Preserve-Working-Code). Files: `mad_context` ingress + heap dispatch. (FR-035/SC-009)
- [~] **T005** Regression after each of T002–T004: baseline green; add the new no-op/suspend/reactivate cases to Section A. (SC-008/009/017) — PARTIAL: FR-034 case present (Section A24b/c); the **FR-021 dup-no-op** and **FR-035 imported-reader reactivation** cases need the multi-instance ingress path → deferred to the integration harness (Phase 5 / T052 duplicate fault-injection).

## Phase 1 — Guards (language authority; approved)

- [x] **T010** `atom/1`: add the runner `_evaluateGuard` arm = runtime `string/1` (non-numeric atomic, excludes `[]`/`nil`); register in prelude. Aligns analyzer (already grounds it) ↔ runtime. (FR-033/SC-005)
- [x] **T011** `@< @> @=< @>=` (non-negatable; total order Number<String<compound then arity/functor/args, equality = `=?=`): lexer `@`-lookahead (must not break `Goal@Agent`) + 4 tokens + parser + runner arms + new `_compareTerms` (cycle-safe, byte/behaviour-identical Dart↔C#) + SRSW analyzer (ground-implying; `_nonNegatableGuards`) + prelude. (FR-037/SC-004/006) — **largest core edit**.
- [x] **T012** `[P]` Guard tests: Section A three-valued (succeed / suspend-then-reactivate-once / fail) for `@<` family + `atom/1`; Section B positive SRSW-relaxation; Section C negatives incl. **declines** `== \== \= reader/1` (FR-036) reject; `=\=` untouched regression (FR-038). Files: `programs/tests/typed/`. (SC-004/006)
- [x] **T013** `[P]` Consolidate `docs/guards-reference.md` as the single authoritative guard spec (fold the additions/fixes/declines in; no duplicate spec). (FR-032)

## Phase 2 — Reliability sublayer + seam (C# reference, clobber-safe `csharp/glp_link/`)

- [x] **T020** `LinkTransport` seam: `ILinkTransport`/`ILinkEndpoint` (open / send-bytes / recv-bytes / close + fault), scheme-selected. (FR-058) — `csharp/glp_link/` package created (clobber-safe, refs `out/csharp`); `seam/` = `ILinkTransport`, `ILinkEndpoint`, `LinkId(Scheme,Endpoint,Nonce)`, `LinkScheme`, `LinkAddress`, `LinkRole`, `LinkOptions(window N=8)`, `LinkFaultSignal`. Builds clean.
- [x] **T021** Wire format: version byte + length/CRC + cycle-guard (visited-set) + fragmentation/reassembly for under-MTU leaves; bad-version/bad-CRC rejected; over-MTU fragments. (FR-022) — `reliability/`: `Crc32` (IEEE/zlib, Dart-mirror-able), `FrameCodec` (22-byte BE header: version+kind+msgId+totalLen+fragIdx/count+chunkCRC+chunkLen; caller-supplied deterministic msgId), `FrameReassembler` (out-of-order + dup-frag tolerant; bounded in-flight/bytes for FR-028), `CycleGuard` (ref-identity visited-set, wired into the T031 send walker). 15 xUnit tests green (`csharp/glp_link.tests`).
- [x] **T022** Per-link sequence/dedup + FIFO + reorder buffer (in-order reconstruct; corruption detected when sublayer off); broker-relay FIFO+at-least-once enforced end-to-end. (FR-020/023/053) — `LinkSequencer` (monotone outbound seq → frame MessageId) + `InboundOrdering` (reorder buffer drains in-order; old/duplicate seq = idempotent no-op; bounded buffer FR-028). Transport-level dedup, upstream of the T002 global-name gate in `mad_context`. +7 xUnit incl. end-to-end T021+T022 reorder+dedup+exactly-once (22/22 green).
- [x] **T023** Epoch/fencing token (split-brain): competing writers for one global name → exactly one wins, loser `permFail`. (FR-047) — `EpochAllocator` (monotone per-establishment token) + `FencingRegistry` (Kleppmann fencing: highest-epoch-wins per global name; lower = stale → `Fenced`→permFail; `Forget` for T024 GC). +8 xUnit (30/30 green).
- [x] **T024** Distributed GC: on `permFail`/close, registry + send-registry goals + heap bind callbacks + reply-table return to baseline; no unreclaimable cycle. (FR-024) — `LinkReclaimer` (per-LinkId hooks; idempotent permFail-then-close; best-effort-run-all then aggregate-throw; late-registration-after-teardown runs now; hooks dropped → no retained cycle) + `ResourceSnapshot`/`IResourceProbe` (SC-014/T074 return-to-baseline probe). Subsystem hooks wired in Phase 3. +6 xUnit (36/36 green).
- [x] **T025** Bounded backpressure: default window **N=8** (scheme-overridable, below the seam); producer suspends; no OOM; no head-of-line block across independent links. (FR-025) — `SendWindow` (per-link credit window; `AcquireAsync` suspends the producer when full, `Release` on ack; over-release throws; per-link instance ⇒ no cross-link HoL). +8 xUnit (44/44 green).
- [x] **T026** Deterministic seeded **loopback transport** (FIFO/in-order/exactly-once by default; deviates only on injected fault) — substrate for hermetic tests. — `LoopbackTransport : ILinkTransport` (listener/connector rendezvous by channel name → shared LinkId; role-order-independent FR-004; cancellable pending) + `LoopbackEndpoint` (ordered `System.Threading.Channels`; graceful close drains→null). Fault injection (T052) = decorator over the `ILinkEndpoint` seam, loopback stays pure. +8 xUnit incl. **full Phase-2-stack round-trip** (window+seq+codec+reassembly+ordering over loopback → exactly-once in-order). **52/52 green**.

## Phase 3 — Base link primitives (C# reference)

- [x] **T030** `'_link_setup'/5` kernel + per-instance LinkId→handle registry (idempotent at identity). (FR-001/002/003/004/007) — **KERNEL DONE** (66 xUnit green). `csharp/glp_link/primitives/`: `LinkPump.cs` (impl `IInboundPump`: thread-safe `inbox` + per-link bg recv loop + runner-thread `TryApplyNext` stream-extend, design-ref §1.6 B5/B6); `LinkSetupKernel.cs` (the `'_link_setup'(LinkId?, Role?, In, Out?, Faults)` body: parse ground LinkId/Role → `TransportRegistry.Select` → blocking listen/connect rendezvous → `LinkRegistry.GetOrEstablish` → wire In-writer/Out-reader/Faults-writer cursors → arm egress `Heap.OnBind(Out-writer)`→ground-relay `PayloadSerializer.SerializeAgentMessage` (throws on VarRef = ground gate)→`FrameCodec`+`Sequencer`→`SendBytesAsync`, re-arm on tail → `Pump.AddLink` ingress → `rt.InboundPump ??= pump`); `LinkRuntime.cs` (per-engine holder); `LinkKernels.cs` (registers `"_link_setup"`/5 on `engine.BodyKernels` via injection seam — NO out/csharp edit; dep flows glp_link→out/csharp). Test `LinkSetupKernelTests.cs`: setup-wiring, egress ground-frame-on-wire, ingress pump-extends-In, idempotent-at-identity (re-setup **surfaced** as Abort). **Use raw arg `VarRef.Addr` for Out? — `heap.Dereference` canonicalizes reader→writer (DerefAddr:422-427).** OQ-3: new `'_link_send'/3`, receiver no kernel. **Deferred-in-T030 (own tasks):** GLP wrappers `link_setup/4`/`server_listener/3`/`client_connector/3` → T036 `link.glp`; **SendWindow backpressure NOT gated** (credit-release rides inbound ack path → T031/T034; gating Acquire w/o Release would freeze the runner); FR-007 re-setup cell-aliasing (currently Abort).
- [x] **T031** `'_link_send'/3` + `link_send/3` (channel face) + `out_relay/3` (LinkId face); ground-relay (`ground(Msg?)` gate; no `_w`/`_r`/embedded reader on the wire); host egress drainer on `Out`. (FR-010/040) — **KERNEL DONE** (72 xUnit green). `csharp/glp_link/primitives/`: `LinkEgress.cs` (the ONE ground-relay ship routine both faces share — closes risk R-5; deep `ResolveGround` flattens a ground struct to a VarRef-free tree AND is the ground gate: an unbound cell at any depth throws, never reaches the wire); `LinkSendKernel.cs` (`'_link_send'(Msg?, LinkId?, ToPeer?)` = the LinkId-keyed face backing `out_relay/3`: parse ground LinkId → `Links.TryGet` (Abort if "send before setup") → `LinkEgress.ShipGround`; non-ground Msg or unbound ToPeer = caller-bug Abort, FR-010); registered `_link_send`/3 on `engine.BodyKernels` via `LinkKernels`. **Channel face**: the T030 `'_link_setup'` egress drainer (`OnOutboundBind`) now calls the shared `LinkEgress.ShipGround` — `link_send/3` conses ground onto `Out`, the drainer ships it. **GLP wrapper clause text** (`link_send/3`, `out_relay/3`) lands in T036 `link.glp` (kernel-direct tests stand in until the compile pipeline arrives). SendWindow backpressure still NOT gated (credit-release rides inbound ack → T034). Tests `LinkSendKernelTests.cs`: ground-const ship, ground-STRUCT deep-resolve, per-link FIFO monotone seq, unknown-link Abort, non-ground-Msg/ToPeer Abort. NO core touched → REPL baseline unaffected.
- [x] **T032** `link_recv/3` + the per-link host ingress (fills `In`; routes through the T002 dedup gate; reactivate-exactly-once). (FR-017/051) — **DONE** (76 xUnit green). `link_recv/3` is **pure composable GLP** (no kernel; clause `link_recv(Msg?, ch([Msg|In], Out?), ch(In?, Out)).` lands in T036 `link.glp`) — the host-side recv machinery is the T030 `LinkPump` ingress + T022 `InboundOrdering`, which already (a) fill `In` by minting a fresh pair/cons-ing the ground value/binding the writer, (b) route a redelivered frame through the per-link sequence high-water dedup as a **verified no-op** BEFORE the heap (no inbox item ⇒ no re-bind, no throw, no second reactivation — FR-021/SC-008; the base ground-relay wire carries no global names, so the §4 sequence half is the whole gate; the global-name `mad_context` half is the deferred glink path), and (c) reactivate a reader suspended on the unarrived stream head **exactly once** (`heap.BindVariable` collects the suspension → `EnqueueReactivatedGoal`; the `SuspensionRecord` disarms on activation — FR-017/051). **No new production code** — T030 over-delivered the ingress; T032 is the contract PROOF. Tests `LinkRecvIngressTests.cs`: suspend→reactivate-exactly-once (`SuspensionRecord` on the In reader → one `Gq` entry, disarmed), duplicate=verified-no-op (re-sent seq absorbed upstream, head cell unchanged, `Gq` still 1), two-frame FIFO, reordered-frames reconstruct-in-order. NO core touched → REPL baseline unaffected.
- [ ] **T033** `'_link_request'/5` + `'_link_accept'/5` + `request_link/4` + `accept_link/4`; in-band request frame over the transport connect (OQ-A3); both paths converge on the T030 registry → equivalent link. (FR-002)
- [ ] **T034** `'_link_monitor'/2` + `link_monitor/2` + fault vocab `ok` / `closed(LinkId,Reason)` / `tempFail(LinkId,Reason)` / `permFail(LinkId,Reason)` on a per-link monitor stream (data, not a verdict). (FR-008/043-046)
- [ ] **T035** `'_link_close'/2` + `link_close/1`+`/2` (abrupt teardown → RST_STREAM-equiv) + graceful stream-end `[]` close; both run T024 GC and emit `closed(LinkId, Reason)` (`eos` for graceful). (FR-024)
- [ ] **T036** `programs/lib/link.glp`: types `Link(In,Out)` / `LinkId=link_id(Scheme,Endpoint,Nonce)` / `Fault` + the GLP wrappers. (FR-006/013)
- [ ] **T037** Role-parameterized boot: branch on ground `AgentId` (the `@`/boot idiom), one program not a fork. (FR-011)

## Phase 4 — Headline split (SC-001/002) over file/loopback

- [ ] **T040** `producer(X)/consumer(X?)` split **Dart↔Dart** over loopback → byte-identical to unsplit baseline. (SC-001)
- [ ] **T041** `[P]` File endpoints (binary + text; read/write/search) split. (FR-012)
- [ ] **T042** Same split **Dart↔C#** over loopback — the cross-runtime parity **release gate**. (SC-002/059/062)
- [ ] **T043** `[P]` request/accept (path B) split over loopback. (FR-002)

## Phase 5 — Integration-test harness (net-new)

- [ ] **T050** Implement the harness per [tests/integration-harness-design.md](tests/integration-harness-design.md): `start_instances / open_link / inject / drive / capture / assert_equiv / close / stop`; Dart↔Dart then Dart↔C# rigs.
- [ ] **T051** Wire **Section R** into `test/run_all_tests.sh` (skip-until-implemented; flips to run as each primitive/leaf lands; keeps baseline green). (SC-017)
- [ ] **T052** Fault injection: drop / reorder / duplicate / delay / partition / peer-kill / clock-jitter (seeded, replayable). (SC-008/010/011/012/013/014)

## Phase 6 — Transport leaves (each: SC-003 feasibility + close + TLS-default) `[P]` across leaves

- [ ] **T060** `ws`/`wss` leaf (native full-duplex; server→client back-channel). (SC-003) — [tutorials/websocket.md](tutorials/websocket.md)
- [ ] **T061** `https`/HTTP-2 + **mTLS** leaf (bidirectional stream; mutual-cert origin auth). (SC-002/003/007) — [tutorials/https-http2-mtls.md](tutorials/https-http2-mtls.md)
- [ ] **T062** `mqtt` leaf — **P2P to the immediate peer** (broker out of scope); request/accept rendezvous; QoS-duplicate dedup. (SC-003/008) — [tutorials/mqtt.md](tutorials/mqtt.md)
- [ ] **T063** `coap` leaf — OBSERVE back-channel; blockwise fragmentation; CON ack; DTLS. (SC-003/012) — [tutorials/coap.md](tutorials/coap.md)
- [ ] **T064** `ble-l2cap` leaf (Android; CoC bilateral stream; BIS multi-reader stays OUT/open). (SC-003) — [tutorials/ble-l2cap.md](tutorials/ble-l2cap.md)
- [ ] **T065** Per-leaf: FR-016 one-bind feasibility test, graceful/abrupt close, plain inter-host refused (TLS/DTLS default). (FR-029)

## Phase 7 — Failure model, security, advanced SCs

- [ ] **T070** Fault liveness: peer-kill mid-bind → reader does NOT fail; `tempFail`→`permFail` in bounded time; fault-guarded clause reducible. (SC-010)
- [ ] **T071** Split-brain defense (epoch/fence; loser `permFail`; no silent overwrite) **+ the `mqtt` reconnect/stale-writer witness** (matrix follow-up). (SC-011)
- [ ] **T072** `[P]` Reorder/loss recovery: sublayer-on reconstructs in-order; sublayer-off detects corruption. (SC-012)
- [ ] **T073** `[P]` Backpressure bound test (producer suspends; bounded; no HoL block across links). (SC-013)
- [ ] **T074** `[P]` Distributed GC to baseline after N `permFail`s (SnapshotResources probe). (SC-014)
- [ ] **T075** Adversarial corpus on **both** REPLs, verdict-by-verdict: forged origin (FR-026), index/cold-call flooding quota (FR-028), malformed/oversized/cyclic/huge-arity fail-safe, bad-version/bad-CRC; plain inter-host refused (FR-029). (SC-007/FR-031)
- [ ] **T076** Stream reroute `stdin`/`stdout`/`stderr` (explicit capability; control-seq sanitized; streams distinct) **+ a real-leaf `wss` reroute run** (matrix follow-up). (SC-016/FR-030)
- [ ] **T077** GEPA round-trip fidelity per primitive via **Claude Agent seams only** (never OpenAI/litellm/`OPENAI_API_KEY`). (SC-015/FR-065-066)

## Phase 8 — Dart mirror + cross-runtime parity + ship

- [ ] **T080** Dart mirror of `csharp/glp_link/` (only AFTER the C# reference passes its acceptance). (FR-056)
- [ ] **T081** Executed **Dart↔C# round-trip over a real transport** — the release gate (must pass before ship). (SC-002/062)
- [ ] **T082** `codeconv` correspondence of the language-layer edits (guards/heap fixes) Dart↔C#; byte/behaviour-identical serializer + comparator. (FR-054/060/061)
- [ ] **T083** Full regression green on both REPLs; the `=\=`-gated prelude still loads. (SC-017)

---

## Dependencies (summary)

- Phase 0 fixes (T002–T004) are prerequisites for Phase 3 (`link_recv` routes through the T002 dedup gate; remote operands rely on T003/T004).
- Phase 1 guards (T011 `@<`) are independent of the link primitives but share the SRSW-analyzer/parser edits — sequence the parser `@` change carefully against `Goal@Agent`.
- Phase 2 sublayer + seam (T020–T026) underlie all of Phase 3 + Phase 6.
- Phase 4 headline split needs Phase 3 + the loopback (T026) + harness (T050).
- Phase 6 leaves each depend on the seam (T020) + the headline split working on loopback (Phase 4).
- Phase 8 (Dart mirror + parity) is LAST and gates ship (T081).

## Deferred (post-MVP / own facet — tracked, not in this task set)

- `glink` full variable-distribution transparency (base→glink; out of MVP).
- OQ-F3 credit/back-channel-unification elaboration (program-visible credit back-channel).
- BLE LE-Audio **BIS true-multi-reader** vs SRSW (open co-design).
- The remaining FR-012 transport leaves beyond the priority 6 (AMQP, XMPP, DDS, HTTP/3, SSH, FTP, SFTP, BLE GATT, BR/EDR SPP).
- OQ-G5 `=\=`-gated-prelude target verification (SC-017 wording).
